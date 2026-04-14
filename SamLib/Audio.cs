using NAudio.Wave;
using System.Reflection;

namespace SamLib.Audio
{
    public class AudioPlayer : IDisposable
    {
        #region Data
        private class ChannelData
        {
            public float Volume { get; set; } = 1.0f;
        }

        // Key = ChannelID
        private readonly Dictionary<string, ChannelData> _channels = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Master", new ChannelData() } // Default channel
        };

        // Key = ID, Value = (Device, Stream, ChannelID, Individual Base Volume)
        private readonly Dictionary<string, (WaveOutEvent Device, WaveStream Stream, string ChannelId, float BaseVolume)> _activeSounds = new();

        // Cache instructions for replaying (Key = ID, Value = Action(ID, ChannelID) to trigger the specific loader)
        private readonly Dictionary<string, Action<string, string>> _sourceRegistry = new();

        public event EventHandler<string> PlaybackStopped;
        #endregion

        #region Channel Management

        /// <summary>
        /// Explicitly creates a channel.
        /// </summary>
        public void CreateChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return;
            lock (_channels)
            {
                if (!_channels.ContainsKey(channelId))
                    _channels[channelId] = new ChannelData();
            }
        }

        /// <summary>
        /// Stops all audio on a channel and deletes the channel.
        /// </summary>
        public void DeleteChannel(string channelId)
        {
            StopChannel(channelId);
            lock (_channels)
                _channels.Remove(channelId);
        }

        /// <summary>
        /// Sets the volume for an entire channel. Changes affect currently playing and new sounds.
        /// </summary>
        public void SetChannelVolume(string channelId, float volume)
        {
            volume = System.Math.Clamp(volume, 0f, 1f);

            // Set channel volume
            lock (_channels)
            {
                if (!_channels.ContainsKey(channelId)) _channels[channelId] = new ChannelData();
                _channels[channelId].Volume = volume;
            }

            // Update currently playing sounds
            lock (_activeSounds)
            {
                foreach (var kvp in _activeSounds.Where(x => x.Value.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                {
                    var sound = kvp.Value;
                    sound.Device.Volume = System.Math.Clamp(sound.BaseVolume * volume, 0f, 1f);
                }
            }
        }

        public void PauseChannel(string channelId)
        {
            lock (_activeSounds)
                foreach (var kvp in _activeSounds.Where(x => x.Value.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                    kvp.Value.Device.Pause();
        }

        public void ResumeChannel(string channelId)
        {
            lock (_activeSounds)
                foreach (var kvp in _activeSounds.Where(x => x.Value.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                    kvp.Value.Device.Play();
        }

        public void StopChannel(string channelId)
        {
            string[] toStop;
            lock (_activeSounds)
            {
                // Select all id's of sounds playing on the specified channel
                toStop = _activeSounds.Where(x => x.Value.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToArray();
            }
            foreach (var id in toStop) Stop(id);
        }

        private float GetChannelVolume(string channelId)
        {
            lock (_channels)
                return _channels.TryGetValue(channelId, out var data) ? data.Volume : 1.0f;
        }

        #endregion

        #region Players

        public void Play(string id, string channelId = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (_sourceRegistry.TryGetValue(id, out var playAction))
                playAction(id, channelId);
            else
                throw new KeyNotFoundException($"No sound source registered for ID: {id}");
        }

        public void Play(string id, string filePath, string channelId = "Master", float volume = 1.0f, bool loop = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid) => Play(sid, filePath, cid, volume, loop);

            var stream = new AudioFileReader(filePath);
            StartPlayback(id, channelId, stream, volume, loop);
        }

        public void Play(string id, Uri url, string channelId = "Master", float volume = 1.0f, bool loop = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid) => Play(sid, url, cid, volume, loop);

            var stream = new MediaFoundationReader(url.ToString());
            StartPlayback(id, channelId, stream, volume, loop);
        }

        public void PlayURL(string id, string url, string channelId = "Master", float volume = 1.0f, bool loop = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid) => PlayURL(sid, url, cid, volume, loop);

            var stream = new MediaFoundationReader(url);
            StartPlayback(id, channelId, stream, volume, loop);
        }

        public void Play(string id, Stream audioStream, bool isMp3 = false, string channelId = "Master", float volume = 1.0f, bool loop = false)
        {
            var ms = EnsureRepeatableStream(audioStream);
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid) => Play(sid, ms, isMp3, cid, volume, loop);

            ms.Position = 0;
            WaveStream stream = isMp3 ? new Mp3FileReader(ms) : new WaveFileReader(ms);
            StartPlayback(id, channelId, stream, volume, loop);
        }

        public void Play(string id, string resourceName, Assembly assembly, bool isMp3 = false, string channelId = "Master", float volume = 1.0f, bool loop = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid) => Play(sid, resourceName, assembly, isMp3, cid, volume, loop);

            InternalPlayResource(id, channelId, resourceName, assembly, isMp3, volume, loop);
        }

        #endregion

        #region Registration
        // Register a sound with an ID, without playing it.
        public void Register(string id, string filePath, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid) => Play(sid, filePath, cid);
        }

        public void Register(string id, Uri url, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid) => Play(sid, url, cid);
        }

        public void RegisterURL(string id, string url, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid) => PlayURL(sid, url, cid);
        }

        public void Register(string id, Stream audioStream, bool isMp3 = false, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var ms = EnsureRepeatableStream(audioStream);
            _sourceRegistry[id] = (sid, cid) => Play(sid, ms, isMp3, cid);
        }

        public void Register(string id, string resourceName, Assembly assembly, bool isMp3 = false, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid) => Play(sid, resourceName, assembly, isMp3, cid);
        }

        #endregion

        #region Playback Control
        private void StartPlayback(string id, string channelId, WaveStream stream, float volume, bool loop)
        {
            Stop(id);
            CreateChannel(channelId);

            // Wrap the stream if looped
            WaveStream finalStream = loop ? new LoopStream(stream) : stream;

            var outputDevice = new WaveOutEvent();
            outputDevice.Init(finalStream);
            outputDevice.PlaybackStopped += (s, e) => OnInternalPlaybackStopped(id);

            float channelVol = GetChannelVolume(channelId);
            outputDevice.Volume = System.Math.Clamp(volume * channelVol, 0f, 1f);

            lock (_activeSounds)
                _activeSounds[id] = (outputDevice, finalStream, channelId, volume);

            outputDevice.Play();
        }

        public void Pause(string id)
        {
            lock (_activeSounds)
                if (_activeSounds.TryGetValue(id, out var sound))
                    sound.Device.Pause();
        }

        public void Resume(string id)
        {
            lock (_activeSounds)
                if (_activeSounds.TryGetValue(id, out var sound))
                    sound.Device.Play();
        }

        public void Stop(string id)
        {
            lock (_activeSounds)
                if (_activeSounds.TryGetValue(id, out var sound))
                {
                    sound.Device.Stop();
                    sound.Device.Dispose();
                    sound.Stream.Dispose();
                    _activeSounds.Remove(id);
                }
        }

        public void StopAll()
        {
            string[] keys;
            lock (_activeSounds) { keys = _activeSounds.Keys.ToArray(); }
            foreach (var key in keys) Stop(key);
        }
        #endregion

        #region Helpers
        private void InternalPlayResource(string id, string channelId, string name, Assembly assembly, bool isMp3, float volume, bool loop)
        {
            Stream resStream = assembly.GetManifestResourceStream(name);
            if (resStream == null) throw new ArgumentException($"Resource '{name}' not found.");
            Play(id, resStream, isMp3, channelId, volume, loop);
        }

        private MemoryStream EnsureRepeatableStream(Stream input)
        {
            if (input is MemoryStream msInput) return msInput;
            var ms = new MemoryStream();
            input.CopyTo(ms);
            return ms;
        }

        private void OnInternalPlaybackStopped(string id)
        {
            if (IsPlaying(id))
            {
                Stop(id);
                PlaybackStopped?.Invoke(this, id);
            }
        }

        /// <summary>
        /// Sets the individual volume of a specific sound. Mixes automatically with the parent channel's volume.
        /// </summary>
        public void SetVolume(string id, float volume)
        {
            volume = System.Math.Clamp(volume, 0f, 1f);

            lock (_activeSounds)
            {
                if (_activeSounds.TryGetValue(id, out var sound))
                {
                    float channelVol = GetChannelVolume(sound.ChannelId);
                    sound.Device.Volume = System.Math.Clamp(volume * channelVol, 0f, 1f);

                    // Update the tuple with the new base volume
                    _activeSounds[id] = (sound.Device, sound.Stream, sound.ChannelId, volume);
                }
            }
        }

        public bool IsPlaying(string id)
        {
            lock (_activeSounds) return _activeSounds.ContainsKey(id);
        }

        public void Dispose()
        {
            StopAll();
            _sourceRegistry.Clear();
            _channels.Clear();
        }
        #endregion
    }

    public class LoopStream : WaveStream
    {
        private readonly WaveStream _sourceStream;
        public bool EnableLooping { get; set; } = true;

        public LoopStream(WaveStream sourceStream) => _sourceStream = sourceStream;

        public override WaveFormat WaveFormat => _sourceStream.WaveFormat;
        public override long Length => _sourceStream.Length;
        public override long Position
        {
            get => _sourceStream.Position;
            set => _sourceStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    if (_sourceStream.Position == 0 || !EnableLooping) break;
                    _sourceStream.Position = 0;
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }
    }
}
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

        // Helper to track individual playback instances
        private class ActiveSoundData
        {
            public WaveOutEvent Device { get; set; }
            public WaveStream Stream { get; set; }
            public string ChannelId { get; set; }
            public float BaseVolume { get; set; }
        }

        // Key = ChannelID
        private readonly Dictionary<string, ChannelData> _channels = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Master", new ChannelData() } // Default channel
        };

        // Key = ID, Value = List of active instances allowing overlaps
        private readonly Dictionary<string, List<ActiveSoundData>> _activeSounds = new();

        // Cache instructions for replaying (Key = ID, Value = Action(ID, ChannelID, AllowOverlap))
        private readonly Dictionary<string, Action<string, string, bool>> _sourceRegistry = new();

        public event EventHandler<string> PlaybackStopped;
        #endregion

        #region Channel Management

        public void CreateChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return;
            lock (_channels)
            {
                if (!_channels.ContainsKey(channelId))
                    _channels[channelId] = new ChannelData();
            }
        }

        public void DeleteChannel(string channelId)
        {
            StopChannel(channelId);
            lock (_channels)
                _channels.Remove(channelId);
        }

        public void SetChannelVolume(string channelId, float volume)
        {
            volume = System.Math.Clamp(volume, 0f, 1f);

            lock (_channels)
            {
                if (!_channels.ContainsKey(channelId)) _channels[channelId] = new ChannelData();
                _channels[channelId].Volume = volume;
            }

            lock (_activeSounds)
            {
                foreach (var kvp in _activeSounds)
                {
                    foreach (var sound in kvp.Value.Where(x => x.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                    {
                        sound.Device.Volume = System.Math.Clamp(sound.BaseVolume * volume, 0f, 1f);
                    }
                }
            }
        }

        public void PauseChannel(string channelId)
        {
            lock (_activeSounds)
                foreach (var kvp in _activeSounds)
                    foreach (var sound in kvp.Value.Where(x => x.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                        sound.Device.Pause();
        }

        public void ResumeChannel(string channelId)
        {
            lock (_activeSounds)
                foreach (var kvp in _activeSounds)
                    foreach (var sound in kvp.Value.Where(x => x.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                        sound.Device.Play();
        }

        public void StopChannel(string channelId)
        {
            string[] toStop;
            lock (_activeSounds)
            {
                toStop = _activeSounds
                    .Where(kvp => kvp.Value.Any(x => x.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                    .Select(x => x.Key).ToArray();
            }
            foreach (var id in toStop) Stop(id);
        }

        private float GetChannelVolume(string channelId)
        {
            lock (_channels)
                return _channels.TryGetValue(channelId, out var data) ? data.Volume : 1.0f;
        }

        #endregion
        // Play sounds (and register if first time), to not cache, leave id empty
        #region Players

        public void Play(string id, string channelId = "Master", bool allowOverlap = false)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (_sourceRegistry.TryGetValue(id, out var playAction))
                playAction(id, channelId, allowOverlap);
            else
                throw new KeyNotFoundException($"No sound source registered for ID: {id}");
        }

        public void Play(string id, string filePath, string channelId = "Master", float volume = 1.0f, bool loop = false, bool allowOverlap = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid, overlap) => Play(sid, filePath, cid, volume, loop, overlap);

            var stream = new AudioFileReader(filePath);
            StartPlayback(id, channelId, stream, volume, loop, allowOverlap);
        }

        public void Play(string id, Uri url, string channelId = "Master", float volume = 1.0f, bool loop = false, bool allowOverlap = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid, overlap) => Play(sid, url, cid, volume, loop, overlap);

            var stream = new MediaFoundationReader(url.ToString());
            StartPlayback(id, channelId, stream, volume, loop, allowOverlap);
        }

        public void PlayURL(string id, string url, string channelId = "Master", float volume = 1.0f, bool loop = false, bool allowOverlap = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid, overlap) => PlayURL(sid, url, cid, volume, loop, overlap);

            var stream = new MediaFoundationReader(url);
            StartPlayback(id, channelId, stream, volume, loop, allowOverlap);
        }

        private void PlayFromBytes(string id, byte[] audioBytes, bool isMp3, string channelId, float volume, bool loop, bool allowOverlap)
        {
            var ms = new MemoryStream(audioBytes);
            WaveStream stream = isMp3 ? new Mp3FileReader(ms) : new WaveFileReader(ms);
            StartPlayback(id, channelId, stream, volume, loop, allowOverlap);
        }

        public void Play(string id, Stream audioStream, bool isMp3 = false, string channelId = "Master", float volume = 1.0f, bool loop = false, bool allowOverlap = false)
        {
            byte[] bytes = EnsureRepeatableStream(audioStream);
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid, overlap) => PlayFromBytes(sid, bytes, isMp3, cid, volume, loop, overlap);

            PlayFromBytes(id, bytes, isMp3, channelId, volume, loop, allowOverlap);
        }

        public void Play(string id, string resourceName, Assembly assembly, bool isMp3 = false, string channelId = "Master", float volume = 1.0f, bool loop = false, bool allowOverlap = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid, cid, overlap) => Play(sid, resourceName, assembly, isMp3, cid, volume, loop, overlap);

            InternalPlayResource(id, channelId, resourceName, assembly, isMp3, volume, loop, allowOverlap);
        }

        #endregion
        // Register sounds
        #region Registration
        public void Register(string id, string filePath, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid, overlap) => Play(sid, filePath, cid, 1.0f, false, overlap);
        }

        public void Register(string id, Uri url, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid, overlap) => Play(sid, url, cid, 1.0f, false, overlap);
        }

        public void RegisterURL(string id, string url, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid, overlap) => PlayURL(sid, url, cid, 1.0f, false, overlap);
        }

        public void Register(string id, Stream audioStream, bool isMp3 = false, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            byte[] bytes = EnsureRepeatableStream(audioStream);
            _sourceRegistry[id] = (sid, cid, overlap) => PlayFromBytes(sid, bytes, isMp3, cid, 1.0f, false, overlap);
        }

        public void Register(string id, string resourceName, Assembly assembly, bool isMp3 = false, string defaultChannel = "Master")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _sourceRegistry[id] = (sid, cid, overlap) => Play(sid, resourceName, assembly, isMp3, cid, 1.0f, false, overlap);
        }

        #endregion

        #region Playback Control
        private void StartPlayback(string id, string channelId, WaveStream stream, float volume, bool loop, bool allowOverlap)
        {
            if (!allowOverlap)
            {
                Stop(id); 
            }

            CreateChannel(channelId);

            WaveStream finalStream = loop ? new LoopStream(stream) : stream;
            var outputDevice = new WaveOutEvent();

            var soundInstance = new ActiveSoundData
            {
                Device = outputDevice,
                Stream = finalStream,
                ChannelId = channelId,
                BaseVolume = volume
            };

            outputDevice.Init(finalStream);

            outputDevice.PlaybackStopped += (s, e) => OnInternalPlaybackStopped(id, soundInstance);

            float channelVol = GetChannelVolume(channelId);
            outputDevice.Volume = System.Math.Clamp(volume * channelVol, 0f, 1f);

            lock (_activeSounds)
            {
                if (!_activeSounds.ContainsKey(id))
                    _activeSounds[id] = new List<ActiveSoundData>();

                _activeSounds[id].Add(soundInstance);
            }

            outputDevice.Play();
        }

        public void Pause(string id)
        {
            lock (_activeSounds)
                if (_activeSounds.TryGetValue(id, out var sounds))
                    foreach (var sound in sounds)
                        sound.Device.Pause();
        }

        public void Resume(string id)
        {
            lock (_activeSounds)
                if (_activeSounds.TryGetValue(id, out var sounds))
                    foreach (var sound in sounds)
                        sound.Device.Play();
        }

        public void Stop(string id)
        {
            List<ActiveSoundData> toStop = null;

            lock (_activeSounds)
            {
                if (_activeSounds.TryGetValue(id, out var sounds))
                {
                    toStop = sounds.ToList();
                    _activeSounds.Remove(id);
                }
            }

            if (toStop != null)
            {
                foreach (var sound in toStop)
                {
                    sound.Device.Stop();
                    sound.Device.Dispose();
                    sound.Stream.Dispose();
                }
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
        private void InternalPlayResource(string id, string channelId, string name, Assembly assembly, bool isMp3, float volume, bool loop, bool allowOverlap)
        {
            Stream resStream = assembly.GetManifestResourceStream(name);
            if (resStream == null) throw new ArgumentException($"Resource '{name}' not found.");
            Play(id, resStream, isMp3, channelId, volume, loop, allowOverlap);
        }

        private byte[] EnsureRepeatableStream(Stream input)
        {
            if (input is MemoryStream msInput) return msInput.ToArray();
            using var ms = new MemoryStream();
            input.CopyTo(ms);
            return ms.ToArray();
        }

        private void OnInternalPlaybackStopped(string id, ActiveSoundData instance)
        {
            bool removed = false;
            lock (_activeSounds)
            {
                if (_activeSounds.TryGetValue(id, out var sounds))
                {
                    if (sounds.Remove(instance))
                    {
                        removed = true;
                        if (sounds.Count == 0)
                            _activeSounds.Remove(id);
                    }
                }
            }

            if (removed)
            {
                instance.Device.Dispose();
                instance.Stream.Dispose();
                PlaybackStopped?.Invoke(this, id);
            }
        }

        public void SetVolume(string id, float volume)
        {
            volume = System.Math.Clamp(volume, 0f, 1f);

            lock (_activeSounds)
            {
                if (_activeSounds.TryGetValue(id, out var sounds))
                {
                    foreach (var sound in sounds)
                    {
                        float channelVol = GetChannelVolume(sound.ChannelId);
                        sound.Device.Volume = System.Math.Clamp(volume * channelVol, 0f, 1f);
                        sound.BaseVolume = volume;
                    }
                }
            }
        }

        public bool IsPlaying(string id)
        {
            lock (_activeSounds)
                return _activeSounds.ContainsKey(id) && _activeSounds[id].Count > 0;
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
using NAudio.Wave;
using System.Reflection;

namespace SamLib.Audio
{
    public class AudioPlayer : IDisposable
    {
        // Key = ID, Value = (Hardware Device, Data Stream)
        private readonly Dictionary<string, (WaveOutEvent Device, WaveStream Stream)> _activeSounds = new();
        // Cache instructions for replaying (Key = ID, Value = Action to trigger the specific loader)
        private readonly Dictionary<string, Action<string>> _sourceRegistry = new();

        public event EventHandler<string> PlaybackStopped;

        #region Players

        /// <summary>
        /// Replays a previously registered sound by ID.
        /// </summary>
        public void Play(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            if (_sourceRegistry.TryGetValue(id, out var playAction))
                playAction(id);
            else
                throw new KeyNotFoundException($"No sound source registered for ID: {id}");
        }

        /// <summary>
        /// Plays from a file path.
        /// </summary>
        public void Play(string id, string filePath)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid) => Play(sid, filePath);

            var stream = new AudioFileReader(filePath);
            StartPlayback(id, stream);
        }

        /// <summary>
        /// Plays from a URL.
        /// </summary>
        public void Play(string id, Uri url)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid) => Play(sid, url);

            var stream = new MediaFoundationReader(url.ToString());
            StartPlayback(id, stream);
        }

        /// <summary>
        /// Plays from a URL.
        /// </summary>
        public void PlayURL(string id, string url)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid) => PlayURL(sid, url);

            var stream = new MediaFoundationReader(url);
            StartPlayback(id, stream);
        }

        /// <summary>
        /// Plays from a Stream (WAV or MP3).
        /// </summary>
        public void Play(string id, Stream audioStream, bool isMp3 = false)
        {
            var ms = EnsureRepeatableStream(audioStream);

            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid) => Play(sid, ms, isMp3);

            ms.Position = 0;
            WaveStream stream = isMp3 ? new Mp3FileReader(ms) : new WaveFileReader(ms);
            StartPlayback(id, stream);
        }

        /// <summary>
        /// Plays from an embedded resource.
        /// </summary>
        public void Play(string id, string resourceName, Assembly assembly, bool isMp3 = false)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _sourceRegistry[id] = (sid) => Play(sid, resourceName, assembly, isMp3);

            InternalPlayResource(id, resourceName, assembly, isMp3);
        }

        #endregion

        #region Playback Control
        private void StartPlayback(string id, WaveStream stream)
        {
            Stop(id);
            var outputDevice = new WaveOutEvent();
            outputDevice.Init(stream);
            outputDevice.PlaybackStopped += (s, e) => OnInternalPlaybackStopped(id);

            lock (_activeSounds)
                _activeSounds[id] = (outputDevice, stream);

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
        private void InternalPlayResource(string id, string name, Assembly assembly, bool isMp3)
        {
            Stream resStream = assembly.GetManifestResourceStream(name);
            if (resStream == null) throw new ArgumentException($"Resource '{name}' not found.");
            Play(id, resStream, isMp3);
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

        public void SetVolume(string id, float volume)
        {
            lock (_activeSounds)
                if (_activeSounds.TryGetValue(id, out var sound))
                    sound.Device.Volume = System.Math.Clamp(volume, 0f, 1f);
        }

        public bool IsPlaying(string id)
        {
            lock (_activeSounds) return _activeSounds.ContainsKey(id);
        }

        public void Dispose()
        {
            StopAll();
            _sourceRegistry.Clear();
        }
        #endregion
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayStories
{
    /// <summary>
    /// Three buses: ambience (looping, cross-fades between books), voice (narration – ducks
    /// the ambience while speaking) and sfx (pooled one-shots).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int SfxVoices = 8;
        const float DuckLevel = 0.45f;

        AudioSource _ambA, _ambB, _voice;
        readonly List<AudioSource> _sfx = new List<AudioSource>();
        int _nextSfx;
        float _ambienceTarget = 0.35f;
        float _duck = 1f;
        AudioClip _currentAmbience;
        Coroutine _ambienceFade;

        readonly Dictionary<AudioClip, float[]> _envelopes = new Dictionary<AudioClip, float[]>();
        const int EnvelopeRate = 60; // samples per second

        public bool IsVoicePlaying => _voice != null && _voice.isPlaying;
        public AudioClip CurrentVoiceClip => _voice != null && _voice.isPlaying ? _voice.clip : null;

        void Awake()
        {
            Instance = this;
            _ambA = MakeSource("Ambience A", true);
            _ambB = MakeSource("Ambience B", true);
            _voice = MakeSource("Voice", false);
            for (int i = 0; i < SfxVoices; i++) _sfx.Add(MakeSource("Sfx " + i, false));
        }

        AudioSource MakeSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void Update()
        {
            float duckTarget = _voice.isPlaying ? DuckLevel : 1f;
            _duck = Mathf.MoveTowards(_duck, duckTarget, Time.unscaledDeltaTime * (duckTarget < _duck ? 4f : 1.2f));
            float amb = Settings.AmbienceEnabled ? _ambienceTarget * Settings.MasterVolume * _duck : 0f;
            if (_ambienceFade == null)
            {
                var active = _ambA.isPlaying ? _ambA : _ambB;
                active.volume = amb;
            }
        }

        // ------------------------------------------------------------------ ambience
        public void PlayAmbience(AudioClip clip, float volume)
        {
            _ambienceTarget = volume;
            if (clip == _currentAmbience) return;
            _currentAmbience = clip;
            if (_ambienceFade != null) StopCoroutine(_ambienceFade);
            _ambienceFade = StartCoroutine(CrossFade(clip, 1.5f));
        }

        IEnumerator CrossFade(AudioClip clip, float seconds)
        {
            var from = _ambA.isPlaying ? _ambA : _ambB;
            var to = from == _ambA ? _ambB : _ambA;
            float fromStart = from.volume;
            if (clip != null)
            {
                to.clip = clip;
                to.volume = 0f;
                to.time = Random.Range(0f, Mathf.Max(0f, clip.length - 1f));
                to.Play();
            }
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                float target = Settings.AmbienceEnabled ? _ambienceTarget * Settings.MasterVolume * _duck : 0f;
                from.volume = fromStart * (1f - k);
                if (clip != null) to.volume = target * k;
                yield return null;
            }
            from.Stop();
            _ambienceFade = null;
        }

        // ------------------------------------------------------------------ voice
        public void PlayVoice(AudioClip clip)
        {
            if (clip == null) return;
            _voice.Stop();
            _voice.clip = clip;
            _voice.volume = Settings.MasterVolume;
            _voice.Play();
        }

        public void StopVoice() => _voice.Stop();

        /// <summary>0..1 loudness of whatever the narrator is saying right now.</summary>
        public float VoiceLevel => _voice.isPlaying ? SampleEnvelope(_voice.clip, _voice.time) : 0f;

        // ------------------------------------------------------------------ sfx
        public AudioSource PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;
            var s = _sfx[_nextSfx];
            _nextSfx = (_nextSfx + 1) % _sfx.Count;
            s.Stop();
            s.clip = clip;
            s.pitch = pitch;
            s.volume = volume * Settings.MasterVolume * (Settings.SoundEffectsEnabled ? 1f : 0f);
            s.Play();
            return s;
        }

        /// <summary>Animal sounds always play (they are the content), but respect master volume.</summary>
        public AudioSource PlayCharacterSound(AudioClip clip, float pitch = 1f)
        {
            if (clip == null) return null;
            var s = _sfx[_nextSfx];
            _nextSfx = (_nextSfx + 1) % _sfx.Count;
            s.Stop();
            s.clip = clip;
            s.pitch = pitch;
            s.volume = Settings.MasterVolume;
            s.Play();
            return s;
        }

        // ------------------------------------------------------------------ envelopes
        /// <summary>
        /// Loudness (0..1) of a clip at a given time. Used to make animals "speak" in sync with
        /// their sound. Requires the clip to be imported as Decompress On Load.
        /// </summary>
        public float SampleEnvelope(AudioClip clip, float time)
        {
            if (clip == null) return 0f;
            if (!_envelopes.TryGetValue(clip, out var env))
            {
                env = BuildEnvelope(clip);
                _envelopes[clip] = env;
            }
            if (env.Length == 0) return 0f;
            float f = time * EnvelopeRate;
            int i = Mathf.Clamp((int)f, 0, env.Length - 1);
            int j = Mathf.Min(i + 1, env.Length - 1);
            return Mathf.Lerp(env[i], env[j], f - i);
        }

        static float[] BuildEnvelope(AudioClip clip)
        {
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int channels = Mathf.Max(1, clip.channels);
            var data = new float[clip.samples * channels];
            if (!clip.GetData(data, 0)) return new float[0];
            int hop = Mathf.Max(1, clip.frequency / EnvelopeRate);
            int frames = clip.samples / hop;
            var env = new float[frames];
            float max = 1e-4f;
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                int start = f * hop * channels, end = Mathf.Min(data.Length, start + hop * channels);
                for (int k = start; k < end; k++) sum += data[k] * data[k];
                env[f] = Mathf.Sqrt((float)(sum / Mathf.Max(1, end - start)));
                max = Mathf.Max(max, env[f]);
            }
            // normalise + light smoothing
            float prev = 0f;
            for (int f = 0; f < frames; f++)
            {
                float v = env[f] / max;
                prev = v > prev ? Mathf.Lerp(prev, v, 0.6f) : Mathf.Lerp(prev, v, 0.25f);
                env[f] = prev;
            }
            return env;
        }
    }
}

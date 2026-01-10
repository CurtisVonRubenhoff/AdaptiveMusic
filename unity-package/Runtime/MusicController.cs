using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace AdaptiveMusic
{
    /// <summary>
    /// Music controller designed for use with AI-extracted audio loops.
    /// Supports dynamic loop selection, quality-aware crossfading, and adaptive music.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        private static MusicController _instance;
        public static MusicController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MusicController>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("MusicController");
                        _instance = go.AddComponent<MusicController>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("References")] 
        [SerializeField] private MusicDatabase musicDatabase;

        [Header("Music Settings")] 
        [SerializeField] private int beatsPerBar = 4;
        [SerializeField] private float baseCrossfadeDuration = 0.1f;
        [SerializeField] private bool useQualityAdaptiveCrossfade = true;
        [SerializeField] private AnimationCurve crossfadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Loop Management")]
        [SerializeField] private bool autoProgressThroughLoops = false;
        [SerializeField] private float loopProgressionChance = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioMixerGroup audioMixerGroup;

        private AudioSource currentSource;
        private AudioSource nextSource;

        private LoopData currentLoop;
        private LoopData nextLoop;
        private string currentTrackKey;
        
        private bool isTransitioning = false;
        private float currentBeatDuration;
        private float currentBarDuration;

        // Events
        public System.Action<string> OnTrackChanged;
        public System.Action<LoopData> OnLoopChanged;
        public System.Action OnMusicStopped;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeAudioSources();
            
            if (musicDatabase != null)
            {
                musicDatabase.Init();
            }
        }

        private void InitializeAudioSources()
        {
            currentSource = gameObject.AddComponent<AudioSource>();
            nextSource = gameObject.AddComponent<AudioSource>();
            
            if (audioMixerGroup != null)
            {
                currentSource.outputAudioMixerGroup = audioMixerGroup;
                nextSource.outputAudioMixerGroup = audioMixerGroup;
            }

            currentSource.loop = true;
            nextSource.loop = true;
            currentSource.playOnAwake = false;
            nextSource.playOnAwake = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                musicDatabase?.Teardown();
                _instance = null;
            }
        }

        private void Update()
        {
            if (autoProgressThroughLoops && currentSource.isPlaying && !isTransitioning)
            {
                CheckForLoopProgression();
            }
        }

        void CalculateTimings(float bpm)
        {
            currentBeatDuration = 60f / bpm;
            currentBarDuration = currentBeatDuration * beatsPerBar;
        }

        /// <summary>
        /// Set the music database to use
        /// </summary>
        public void SetMusicDatabase(MusicDatabase database)
        {
            if (musicDatabase != null)
            {
                musicDatabase.Teardown();
            }
            
            musicDatabase = database;
            
            if (musicDatabase != null)
            {
                musicDatabase.Init();
            }
        }

        /// <summary>
        /// Play a specific track, starting with a random loop
        /// </summary>
        public void PlayTrack(string trackKey)
        {
            if (musicDatabase == null)
            {
                Debug.LogError("MusicDatabase not assigned!");
                return;
            }

            TrackData track = musicDatabase.GetTrack(trackKey);
            if (track != null && track.loops.Count > 0)
            {
                LoopData startLoop = track.GetRandomLoop();
                PlayLoop(trackKey, startLoop);
            }
        }

        /// <summary>
        /// Play a specific loop from a track
        /// </summary>
        public void PlayLoop(string trackKey, LoopData loop)
        {
            if (loop == null || loop.clip == null) return;

            currentTrackKey = trackKey;
            currentLoop = loop;
            currentSource.clip = loop.clip;
            currentSource.volume = 1f;
            currentSource.Play();

            TrackData track = musicDatabase.GetTrack(trackKey);
            if (track != null)
            {
                CalculateTimings(track.bpm);
            }

            OnTrackChanged?.Invoke(trackKey);
            OnLoopChanged?.Invoke(loop);
        }

        /// <summary>
        /// Play a specific loop by index from the current track
        /// </summary>
        public void PlayLoopByIndex(int loopIndex)
        {
            if (string.IsNullOrEmpty(currentTrackKey)) return;

            TrackData track = musicDatabase.GetTrack(currentTrackKey);
            if (track != null && loopIndex >= 0 && loopIndex < track.loops.Count)
            {
                TransitionToLoop(currentTrackKey, track.loops[loopIndex]);
            }
        }

        /// <summary>
        /// Transition to a new track (picks random starting loop)
        /// </summary>
        public void TransitionToTrack(string trackKey)
        {
            if (musicDatabase == null)
            {
                Debug.LogError("MusicDatabase not assigned!");
                return;
            }

            TrackData track = musicDatabase.GetTrack(trackKey);
            if (track != null && track.loops.Count > 0)
            {
                LoopData targetLoop = track.GetRandomLoop();
                TransitionToLoop(trackKey, targetLoop);
            }
        }

        /// <summary>
        /// Transition to a specific loop with quality-aware crossfading
        /// </summary>
        public void TransitionToLoop(string trackKey, LoopData targetLoop)
        {
            if (targetLoop == null || isTransitioning) return;

            if (!currentSource.isPlaying)
            {
                PlayLoop(trackKey, targetLoop);
                return;
            }

            if (currentLoop == targetLoop) return;

            StartCoroutine(CrossfadeToNextLoop(trackKey, targetLoop));
        }

        IEnumerator CrossfadeToNextLoop(string trackKey, LoopData targetLoop)
        {
            isTransitioning = true;
            nextLoop = targetLoop;

            float actualCrossfadeDuration = GetAdaptiveCrossfadeDuration(currentLoop, targetLoop);

            nextSource.clip = targetLoop.clip;
            nextSource.volume = 0f;
            nextSource.pitch = 1f;
            nextSource.Play();

            float elapsed = 0f;
            float startVolumeA = currentSource.volume;

            while (elapsed < actualCrossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / actualCrossfadeDuration;
                float curveValue = crossfadeCurve.Evaluate(t);

                // Equal-power crossfade
                currentSource.volume = Mathf.Cos(curveValue * Mathf.PI * 0.5f) * startVolumeA;
                nextSource.volume = Mathf.Sin(curveValue * Mathf.PI * 0.5f);

                yield return null;
            }

            currentSource.volume = 0f;
            nextSource.volume = 1f;
            currentSource.Stop();

            AudioSource temp = currentSource;
            currentSource = nextSource;
            nextSource = temp;

            currentTrackKey = trackKey;
            currentLoop = targetLoop;

            TrackData track = musicDatabase.GetTrack(trackKey);
            if (track != null)
            {
                CalculateTimings(track.bpm);
            }

            isTransitioning = false;
            
            OnTrackChanged?.Invoke(trackKey);
            OnLoopChanged?.Invoke(targetLoop);
        }

        private float GetAdaptiveCrossfadeDuration(LoopData fromLoop, LoopData toLoop)
        {
            if (!useQualityAdaptiveCrossfade)
                return baseCrossfadeDuration;

            float duration = baseCrossfadeDuration;

            if (fromLoop != null && toLoop != null)
            {
                float avgQuality = (fromLoop.quality + toLoop.quality) / 2f;

                if (avgQuality > 0.8f)
                    duration = baseCrossfadeDuration * 0.5f;
                else if (avgQuality > 0.6f)
                    duration = baseCrossfadeDuration;
                else if (avgQuality > 0.4f)
                    duration = baseCrossfadeDuration * 1.5f;
                else
                    duration = baseCrossfadeDuration * 2f;
            }

            return duration;
        }

        private void CheckForLoopProgression()
        {
            if (string.IsNullOrEmpty(currentTrackKey) || currentLoop == null)
                return;

            float timeRemaining = currentLoop.duration - (currentSource.time % currentLoop.duration);
            float checkWindow = GetAdaptiveCrossfadeDuration(currentLoop, null) + 0.1f;

            if (timeRemaining <= checkWindow && Random.value < loopProgressionChance)
            {
                ProgressToNextLoop();
            }
        }

        private void ProgressToNextLoop()
        {
            if (string.IsNullOrEmpty(currentTrackKey)) return;

            TrackData track = musicDatabase.GetTrack(currentTrackKey);
            if (track == null || track.loops.Count <= 1) return;

            LoopData nextLoop = track.GetRandomLoopExcluding(currentLoop);
            if (nextLoop != null)
            {
                TransitionToLoop(currentTrackKey, nextLoop);
            }
        }

        /// <summary>
        /// Transition to a loop with specific intensity
        /// </summary>
        public void TransitionToIntensity(string trackKey, float targetIntensity)
        {
            TrackData track = musicDatabase.GetTrack(trackKey);
            if (track == null) return;

            LoopData bestLoop = track.GetLoopClosestToIntensity(targetIntensity);
            if (bestLoop != null)
            {
                TransitionToLoop(trackKey, bestLoop);
            }
        }

        public void SetBaseCrossfadeDuration(float duration)
        {
            baseCrossfadeDuration = Mathf.Max(0.05f, duration);
        }

        public void SetQualityAdaptiveCrossfade(bool enabled)
        {
            useQualityAdaptiveCrossfade = enabled;
        }

        public void SetAutoLoopProgression(bool enabled, float chance = 0.3f)
        {
            autoProgressThroughLoops = enabled;
            loopProgressionChance = Mathf.Clamp01(chance);
        }

        /// <summary>
        /// Stop all music
        /// </summary>
        public void StopMusic()
        {
            StopAllCoroutines();
            currentSource.Stop();
            nextSource.Stop();
            currentSource.pitch = 1f;
            nextSource.pitch = 1f;
            isTransitioning = false;
            currentLoop = null;
            currentTrackKey = null;
            
            OnMusicStopped?.Invoke();
        }

        /// <summary>
        /// Fade out current music over specified duration
        /// </summary>
        public void FadeOut(float duration = 1f)
        {
            StartCoroutine(FadeOutCoroutine(duration));
        }

        IEnumerator FadeOutCoroutine(float duration)
        {
            float startVolume = currentSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                currentSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            StopMusic();
        }

        public string GetCurrentTrackKey() => currentTrackKey ?? "";
        public LoopData GetCurrentLoop() => currentLoop;
        public float GetCurrentBeat() => currentSource.isPlaying ? currentSource.time / currentBeatDuration : 0f;
        public float GetCurrentBar() => currentSource.isPlaying ? currentSource.time / currentBarDuration : 0f;
        public bool IsTransitioning() => isTransitioning;
        public bool IsPlaying() => currentSource.isPlaying;
    }
}

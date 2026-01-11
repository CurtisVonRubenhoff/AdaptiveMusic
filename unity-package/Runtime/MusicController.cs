using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveMusic
{
    /// <summary>
    /// Enhanced MusicController with multi-stem vertical remixing support.
    /// Allows playing multiple synchronized stems per loop with individual volume control.
    /// Updated to support smooth 0.3s crossfades between loops in the same track.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        // ==================== SINGLETON ====================
        private static MusicController _instance;
        public static MusicController Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MusicController");
                    _instance = go.AddComponent<MusicController>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ==================== CONFIGURATION ====================
        [Header("Music Database")]
        [SerializeField] private MusicDatabase database;

        [Header("Transition Settings")]
        [Tooltip("Enable MAGI-style quantized transitions (waits for sync points)")]
        [SerializeField] private bool useQuantizedTransitions = true;

        [Tooltip("Base crossfade duration in seconds (for track changes)")]
        [SerializeField] private float baseCrossfadeDuration = 0.1f;

        [Tooltip("Specific crossfade duration for loop transitions within the same track")]
        [SerializeField] private float loopCrossfadeDuration = 0.3f;

        [Tooltip("Adjust crossfade based on loop quality (overrides base durations if true)")]
        [SerializeField] private bool qualityAdaptiveCrossfade = true;

        [Tooltip("Maximum time to wait for a sync point before forcing transition")]
        [SerializeField] private float maxSyncWaitTime = 8f;

        [Tooltip("DSP buffer time in seconds (prevents audio glitches)")]
        [SerializeField] private double dspBufferTime = 0.1;

        [Header("Auto Loop Progression")]
        [Tooltip("Automatically vary loops for musical interest")]
        [SerializeField] private bool autoLoopProgression = false;

        [Tooltip("Probability of switching loops (0.0 - 1.0)")]
        [SerializeField] private float loopProgressionChance = 0.3f;

        [Header("Stem Configuration")]
        [Tooltip("Maximum number of stems supported per loop")]
        [SerializeField] private int maxStems = 8;

        // ==================== AUDIO SOURCES ====================
        // Primary/secondary for single-clip or main stem
        private AudioSource primarySource;
        private AudioSource secondarySource;
        private AudioSource transitionSource;

        // Stem arrays for vertical remixing
        private AudioSource[] primaryStemSources;
        private AudioSource[] secondaryStemSources;
        private AudioSource[] activeStemSources;
        private AudioSource[] inactiveStemSources;

        // ==================== STATE ====================
        private string currentTrackKey;
        private LoopData currentLoop;
        private TrackData currentTrack;
        private AudioSource activeSource;
        private AudioSource inactiveSource;
        private bool isPlaying = false;
        private bool isTransitioning = false;
        private bool isUsingStemPlayback = false;

        // DSP timing (sample-accurate, frame-rate independent)
        private double loopStartDspTime = 0;
        private double lastDspTime = 0;

        // Scheduled transition using DSP time
        private ScheduledTransition? scheduledTransition;

        // Stem volume control
        private Dictionary<int, float> stemVolumes = new Dictionary<int, float>();

        // ==================== EVENTS ====================
        public event Action<string> OnTrackChanged;
        public event Action<LoopData> OnLoopChanged;
        public event Action OnMusicStopped;
        public event Action<double> OnSyncPointReached;
        public event Action<int, float> OnStemVolumeChanged; // stem index, volume

        // ==================== INITIALIZATION ====================
        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
        }

        private void InitializeAudioSources()
        {
            // Create single-clip sources
            if (primarySource == null)
            {
                primarySource = gameObject.AddComponent<AudioSource>();
                primarySource.playOnAwake = false;
                primarySource.loop = true;
            }

            if (secondarySource == null)
            {
                secondarySource = gameObject.AddComponent<AudioSource>();
                secondarySource.playOnAwake = false;
                secondarySource.loop = true;
                secondarySource.volume = 0f;
            }

            if (transitionSource == null)
            {
                transitionSource = gameObject.AddComponent<AudioSource>();
                transitionSource.playOnAwake = false;
                transitionSource.loop = true;
            }

            // Create stem source arrays
            primaryStemSources = new AudioSource[maxStems];
            secondaryStemSources = new AudioSource[maxStems];

            for (int i = 0; i < maxStems; i++)
            {
                // Primary stem sources
                primaryStemSources[i] = gameObject.AddComponent<AudioSource>();
                primaryStemSources[i].playOnAwake = false;
                primaryStemSources[i].loop = true;
                primaryStemSources[i].volume = 1f;

                // Secondary stem sources
                secondaryStemSources[i] = gameObject.AddComponent<AudioSource>();
                secondaryStemSources[i].playOnAwake = false;
                secondaryStemSources[i].loop = true;
                secondaryStemSources[i].volume = 0f;

                // Initialize stem volume dictionary
                stemVolumes[i] = 1f;
            }

            activeSource = primarySource;
            inactiveSource = secondarySource;
            activeStemSources = primaryStemSources;
            inactiveStemSources = secondaryStemSources;
        }

        void Start()
        {
            lastDspTime = AudioSettings.dspTime;
        }

        // ==================== PUBLIC API ====================

        public void SetMusicDatabase(MusicDatabase db)
        {
            database = db;
        }

        /// <summary>
        /// Play a track with a random starting loop
        /// </summary>
        public void PlayTrack(string trackKey)
        {
            if (database == null)
            {
                Debug.LogError("MusicDatabase not set!");
                return;
            }

            TrackData track = database.GetTrack(trackKey);
            if (track == null)
            {
                Debug.LogError($"Track '{trackKey}' not found in database");
                return;
            }

            LoopData loop = track.GetRandomLoop();
            // Immediate play for first track start
            PlayLoopDsp(trackKey, loop, immediate: true);
        }

        /// <summary>
        /// Play a specific loop by index from the current or specified track
        /// </summary>
        public void PlayLoopByIndex(string trackKey, int loopIndex)
        {
            if (database == null)
            {
                Debug.LogError("MusicDatabase not set!");
                return;
            }

            TrackData track = database.GetTrack(trackKey);
            if (track == null)
            {
                Debug.LogError($"Track '{trackKey}' not found in database");
                return;
            }

            LoopData loop = track.GetLoopByIndex(loopIndex);
            if (loop == null)
            {
                Debug.LogError($"Loop index {loopIndex} not found in track '{trackKey}'");
                return;
            }

            PlayLoopDsp(trackKey, loop, immediate: true);
        }

        /// <summary>
        /// Transition to a different track
        /// </summary>
        public void TransitionToTrack(string trackKey)
        {
            if (trackKey == currentTrackKey)
            {
                Debug.Log($"Already playing track '{trackKey}'");
                return;
            }

            TrackData track = database.GetTrack(trackKey);
            if (track == null)
            {
                Debug.LogError($"Track '{trackKey}' not found");
                return;
            }

            LoopData loop = track.GetRandomLoop();
            TransitionToLoop(trackKey, loop);
        }

        /// <summary>
        /// Transition to a specific loop within the current or different track
        /// </summary>
        public void TransitionToLoop(string trackKey, LoopData targetLoop)
        {
            if (targetLoop == null)
            {
                Debug.LogError("Target loop is null");
                return;
            }

            if (useQuantizedTransitions && currentLoop != null && currentLoop.HasSyncPoints)
            {
                ScheduleQuantizedTransition(trackKey, targetLoop);
            }
            else
            {
                PlayLoopDsp(trackKey, targetLoop, immediate: false);
            }
        }

        /// <summary>
        /// Transition to a loop by index
        /// </summary>
        public void TransitionToLoopByIndex(string trackKey, int loopIndex)
        {
            TrackData track = database.GetTrack(trackKey);
            if (track == null)
            {
                Debug.LogError($"Track '{trackKey}' not found");
                return;
            }

            LoopData loop = track.GetLoopByIndex(loopIndex);
            if (loop == null)
            {
                Debug.LogError($"Loop index {loopIndex} not found");
                return;
            }

            TransitionToLoop(trackKey, loop);
        }

        /// <summary>
        /// Transition to a loop matching the target intensity
        /// </summary>
        public void TransitionToIntensity(string trackKey, float intensity)
        {
            TrackData track = database.GetTrack(trackKey);
            if (track == null)
            {
                Debug.LogError($"Track '{trackKey}' not found");
                return;
            }

            LoopData loop = track.GetLoopClosestToIntensity(intensity);
            if (loop != null)
            {
                TransitionToLoop(trackKey, loop);
            }
        }

        /// <summary>
        /// Stop all music immediately
        /// </summary>
        public void StopMusic()
        {
            StopAllCoroutines();
            primarySource.Stop();
            secondarySource.Stop();
            transitionSource.Stop();

            // Stop all stem sources
            for (int i = 0; i < maxStems; i++)
            {
                primaryStemSources[i].Stop();
                secondaryStemSources[i].Stop();
            }

            isPlaying = false;
            isTransitioning = false;
            isUsingStemPlayback = false;
            currentLoop = null;
            currentTrack = null;
            currentTrackKey = null;
            scheduledTransition = null;

            OnMusicStopped?.Invoke();
        }

        /// <summary>
        /// Fade out music over the specified duration
        /// </summary>
        public void FadeOut(float duration = 2f)
        {
            StartCoroutine(FadeOutCoroutine(duration));
        }

        // ==================== STEM CONTROL ====================

        /// <summary>
        /// Set the volume of a specific stem (0.0 to 1.0)
        /// </summary>
        public void SetStemVolume(int stemIndex, float volume)
        {
            if (stemIndex < 0 || stemIndex >= maxStems)
            {
                Debug.LogWarning($"Stem index {stemIndex} out of range (0-{maxStems - 1})");
                return;
            }

            volume = Mathf.Clamp01(volume);
            stemVolumes[stemIndex] = volume;

            if (isUsingStemPlayback && activeStemSources[stemIndex] != null)
            {
                activeStemSources[stemIndex].volume = volume;
            }

            OnStemVolumeChanged?.Invoke(stemIndex, volume);
        }

        /// <summary>
        /// Get the volume of a specific stem
        /// </summary>
        public float GetStemVolume(int stemIndex)
        {
            if (stemIndex < 0 || stemIndex >= maxStems)
                return 0f;

            return stemVolumes.ContainsKey(stemIndex) ? stemVolumes[stemIndex] : 1f;
        }

        /// <summary>
        /// Mute a specific stem
        /// </summary>
        public void MuteStem(int stemIndex)
        {
            SetStemVolume(stemIndex, 0f);
        }

        /// <summary>
        /// Unmute a specific stem
        /// </summary>
        public void UnmuteStem(int stemIndex)
        {
            SetStemVolume(stemIndex, 1f);
        }

        /// <summary>
        /// Solo a specific stem (mute all others)
        /// </summary>
        public void SoloStem(int stemIndex)
        {
            if (!isUsingStemPlayback || currentLoop == null)
                return;

            for (int i = 0; i < currentLoop.stems.Count; i++)
            {
                SetStemVolume(i, i == stemIndex ? 1f : 0f);
            }
        }

        /// <summary>
        /// Unmute all stems
        /// </summary>
        public void UnmuteAllStems()
        {
            if (!isUsingStemPlayback || currentLoop == null)
                return;

            for (int i = 0; i < currentLoop.stems.Count; i++)
            {
                SetStemVolume(i, 1f);
            }
        }

        /// <summary>
        /// Fade a stem's volume over time
        /// </summary>
        public void FadeStem(int stemIndex, float targetVolume, float duration)
        {
            StartCoroutine(FadeStemCoroutine(stemIndex, targetVolume, duration));
        }

        private IEnumerator FadeStemCoroutine(int stemIndex, float targetVolume, float duration)
        {
            if (stemIndex < 0 || stemIndex >= maxStems || !isUsingStemPlayback)
                yield break;

            float startVolume = GetStemVolume(stemIndex);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float newVolume = Mathf.Lerp(startVolume, targetVolume, t);
                SetStemVolume(stemIndex, newVolume);
                yield return null;
            }

            SetStemVolume(stemIndex, targetVolume);
        }

        // ==================== DSP-BASED QUANTIZED TRANSITIONS ====================

        private void ScheduleQuantizedTransition(string trackKey, LoopData targetLoop)
        {
            if (currentLoop == null || !currentLoop.HasSyncPoints)
            {
                Debug.LogWarning("Current loop has no sync points, falling back to immediate transition");
                PlayLoopDsp(trackKey, targetLoop, immediate: false);
                return;
            }

            double currentDspTime = AudioSettings.dspTime;
            double elapsedDsp = currentDspTime - loopStartDspTime;
            float currentTime = (float)(elapsedDsp % currentLoop.Duration);

            float nextSyncTime = currentLoop.GetNextSyncTime(currentTime);
            if (nextSyncTime < 0)
            {
                // Fallback if no future sync point
                nextSyncTime = 0;
            }

            float waitTime = nextSyncTime - currentTime;
            if (waitTime < 0)
                waitTime += currentLoop.Duration;

            if (waitTime > maxSyncWaitTime)
            {
                Debug.LogWarning($"Sync wait time ({waitTime}s) exceeds max ({maxSyncWaitTime}s), forcing transition");
                waitTime = Mathf.Min(waitTime, maxSyncWaitTime);
            }

            double scheduledDspTime = currentDspTime + waitTime;

            scheduledTransition = new ScheduledTransition
            {
                trackKey = trackKey,
                targetLoop = targetLoop,
                scheduledDspTime = scheduledDspTime,
                syncPointTime = nextSyncTime
            };

            if (targetLoop.clip != null)
                Debug.Log($"Scheduled transition to '{targetLoop.clip?.name ?? "stem loop"}' at DSP time {scheduledDspTime:F6} " +
                     $"(in {waitTime:F3}s, sync point at {nextSyncTime:F2}s in loop)");
        }

        void FixedUpdate()
        {
            double currentDspTime = AudioSettings.dspTime;

            if (scheduledTransition.HasValue)
            {
                // Note: increased tolerance slightly for reliability
                double tolerance = dspBufferTime;
                
                if (currentDspTime >= scheduledTransition.Value.scheduledDspTime - tolerance)
                {
                    ExecuteScheduledTransition();
                }
            }

            if (autoLoopProgression && isPlaying && !isTransitioning && 
                currentLoop != null && currentTrack != null && !scheduledTransition.HasValue)
            {
                CheckAutoLoopProgression(currentDspTime);
            }

            lastDspTime = currentDspTime;
        }

        private void ExecuteScheduledTransition()
        {
            if (!scheduledTransition.HasValue)
            {
                Debug.LogWarning("No scheduled transition to execute");
                return;
            }

            var transition = scheduledTransition.Value;
            double startDspTime = transition.scheduledDspTime;

            OnSyncPointReached?.Invoke(startDspTime);
            Debug.Log($"Sync point reached at DSP time {startDspTime:F6}");

            // Handle transition clips if they exist (Stinger/Transition clips)
            // Note: If using crossfade, we typically skip the stinger, or handle it differently.
            // For this implementation, we prioritize the new Crossfade logic over Stingers if enabled.
            
            // Check crossfade duration based on whether we are changing tracks or looping internally
            float duration = baseCrossfadeDuration;
            bool isInternalLoop = transition.trackKey == currentTrackKey;
            
            if (isInternalLoop)
            {
                duration = loopCrossfadeDuration; // Use the requested 0.3s
            }
            else if (qualityAdaptiveCrossfade && currentLoop != null)
            {
                float avgQuality = (currentLoop.quality + transition.targetLoop.quality) / 2f;
                duration = CalculateQualityCrossfade(avgQuality);
            }

            // Execute the crossfade routine
            StartCoroutine(CrossfadeRoutine(transition.targetLoop, transition.trackKey, duration, startDspTime));

            scheduledTransition = null;
        }

        // ==================== PLAYBACK CONTROL ====================

        private void PlayLoopDsp(string trackKey, LoopData loop, bool immediate)
        {
            if (loop == null)
            {
                Debug.LogError("Cannot play null loop");
                return;
            }

            string error;
            if (!loop.Validate(out error))
            {
                Debug.LogError($"Invalid loop data: {error}");
                return;
            }

            double currentDspTime = AudioSettings.dspTime;
            double startDspTime = currentDspTime + dspBufferTime;

            if (immediate || !isPlaying)
            {
                // Hard Cut / First Play
                ScheduleNewLoopDsp(trackKey, loop, startDspTime);
                Debug.Log($"Playing loop immediately at DSP {startDspTime:F6}");
            }
            else
            {
                // Unquantized Crossfade
                float duration = (trackKey == currentTrackKey) ? loopCrossfadeDuration : baseCrossfadeDuration;
                
                if (qualityAdaptiveCrossfade && trackKey != currentTrackKey && currentLoop != null)
                {
                     float avgQuality = (currentLoop.quality + loop.quality) / 2f;
                     duration = CalculateQualityCrossfade(avgQuality);
                }

                StartCoroutine(CrossfadeRoutine(loop, trackKey, duration, startDspTime));
            }
        }

        // ==================== CORE PLAYBACK & CROSSFADE LOGIC ====================

        private void ScheduleNewLoopDsp(string trackKey, LoopData loop, double startDspTime)
        {
            // Update State
            bool trackChanged = trackKey != currentTrackKey;
            currentTrackKey = trackKey;
            currentTrack = database.GetTrack(trackKey);
            currentLoop = loop;
            loopStartDspTime = startDspTime;
            isPlaying = true;

            // Determine if we should use stem playback
            bool useStemsForThisLoop = loop.useStems && loop.stems != null && loop.stems.Count > 0;
            isUsingStemPlayback = useStemsForThisLoop;

            // Stop previous sources
            activeSource.Stop();
            for (int i = 0; i < maxStems; i++) activeStemSources[i].Stop();

            if (useStemsForThisLoop)
            {
                // Play Stems
                for (int i = 0; i < loop.stems.Count && i < maxStems; i++)
                {
                    activeStemSources[i].clip = loop.stems[i];
                    activeStemSources[i].volume = GetStemVolume(i);
                    activeStemSources[i].PlayScheduled(startDspTime);
                }
                activeSource.volume = 0f;
            }
            else
            {
                // Play Single Clip
                activeSource.clip = loop.clip;
                activeSource.volume = 1f;
                activeSource.PlayScheduled(startDspTime);
            }

            if (trackChanged) OnTrackChanged?.Invoke(trackKey);
            OnLoopChanged?.Invoke(loop);
        }

        private IEnumerator CrossfadeRoutine(LoopData newLoop, string trackKey, float duration, double startDspTime)
        {
            isTransitioning = true;
            
            // 1. Setup the "Next" (Inactive) sources
            bool nextUsesStems = newLoop.useStems && newLoop.stems != null && newLoop.stems.Count > 0;

            if (nextUsesStems)
            {
                for (int i = 0; i < newLoop.stems.Count && i < maxStems; i++)
                {
                    inactiveStemSources[i].clip = newLoop.stems[i];
                    inactiveStemSources[i].volume = 0f; // Start silent
                    inactiveStemSources[i].PlayScheduled(startDspTime);
                }
            }
            else
            {
                inactiveSource.clip = newLoop.clip;
                inactiveSource.volume = 0f; // Start silent
                inactiveSource.PlayScheduled(startDspTime);
            }

            // Wait for DSP time
            while (AudioSettings.dspTime < startDspTime)
            {
                yield return null;
            }

            // 2. Perform Crossfade
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // Fade OUT Active (Current)
                if (isUsingStemPlayback)
                {
                    for (int i = 0; i < maxStems; i++)
                    {
                        float vol = GetStemVolume(i);
                        activeStemSources[i].volume = Mathf.Lerp(vol, 0f, t);
                    }
                }
                else
                {
                    activeSource.volume = Mathf.Lerp(1f, 0f, t);
                }

                // Fade IN Inactive (New)
                if (nextUsesStems)
                {
                    for (int i = 0; i < maxStems; i++)
                    {
                        float vol = GetStemVolume(i);
                        inactiveStemSources[i].volume = Mathf.Lerp(0f, vol, t);
                    }
                }
                else
                {
                    inactiveSource.volume = Mathf.Lerp(0f, 1f, t);
                }

                yield return null;
            }

            // 3. Finalize Volumes & Stop Old
            if (isUsingStemPlayback)
            {
                for (int i = 0; i < maxStems; i++) activeStemSources[i].volume = 0f; // Ensure silence
            }
            else
            {
                activeSource.volume = 0f;
            }
            
            // Stop old sources
            activeSource.Stop();
            for(int i=0; i<maxStems; i++) activeStemSources[i].Stop();

            // Set final volume for new sources
            if (nextUsesStems)
            {
                for (int i = 0; i < maxStems; i++) inactiveStemSources[i].volume = GetStemVolume(i);
            }
            else
            {
                inactiveSource.volume = 1f;
            }

            // 4. Swap Arrays (Double Buffering)
            var tempSource = activeSource;
            activeSource = inactiveSource;
            inactiveSource = tempSource;

            var tempStems = activeStemSources;
            activeStemSources = inactiveStemSources;
            inactiveStemSources = tempStems;

            // 5. Update State
            bool trackChanged = trackKey != currentTrackKey;
            currentTrackKey = trackKey;
            currentTrack = database.GetTrack(trackKey);
            currentLoop = newLoop;
            loopStartDspTime = startDspTime; // Update logical start time for next sync calculation
            isUsingStemPlayback = nextUsesStems;
            isTransitioning = false;

            if (trackChanged) OnTrackChanged?.Invoke(trackKey);
            OnLoopChanged?.Invoke(newLoop);
        }

        private IEnumerator FadeOutCoroutine(float duration)
        {
            float elapsed = 0f;
            
            // Capture starting volumes
            float startMainVol = activeSource.volume;
            float[] startStemVols = new float[maxStems];
            for(int i=0; i<maxStems; i++) startStemVols[i] = activeStemSources[i].volume;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (isUsingStemPlayback)
                {
                    for (int i = 0; i < maxStems; i++)
                    {
                        activeStemSources[i].volume = Mathf.Lerp(startStemVols[i], 0f, t);
                    }
                }
                else
                {
                    activeSource.volume = Mathf.Lerp(startMainVol, 0f, t);
                }
                yield return null;
            }

            StopMusic();
        }

        // ==================== AUTO LOOP PROGRESSION ====================

        private void CheckAutoLoopProgression(double currentDspTime)
        {
            if (currentLoop == null) return;

            double elapsedDsp = currentDspTime - loopStartDspTime;
            float currentTime = (float)(elapsedDsp % currentLoop.Duration);
            float remainingTime = currentLoop.Duration - currentTime;

            // Check if we are near the end (using the new loop crossfade duration)
            if (remainingTime <= loopCrossfadeDuration + 0.1f)
            {
                // Only trigger once per loop
                if (UnityEngine.Random.value < loopProgressionChance * Time.deltaTime * 5f) 
                {
                    LoopData nextLoop = currentTrack.GetRandomLoop();
                    if (nextLoop != currentLoop)
                    {
                        TransitionToLoop(currentTrackKey, nextLoop);
                    }
                }
            }
        }

        // ==================== UTILITY ====================

        private float CalculateQualityCrossfade(float quality)
        {
            if (quality >= 0.8f) return 0.05f;
            if (quality >= 0.6f) return 0.1f;
            if (quality >= 0.4f) return 0.15f;
            return 0.2f;
        }

        public float GetCurrentLoopPosition()
        {
            if (!isPlaying || currentLoop == null) return 0f;

            double currentDspTime = AudioSettings.dspTime;
            double elapsedDsp = currentDspTime - loopStartDspTime;
            return (float)(elapsedDsp % currentLoop.Duration);
        }

        public float GetTimeUntilNextSync()
        {
            if (!isPlaying || currentLoop == null || !currentLoop.HasSyncPoints)
                return -1f;

            float currentPos = GetCurrentLoopPosition();
            return currentLoop.GetWaitTimeToNextSync(currentPos);
        }

        // ==================== GETTERS ====================

        public string GetCurrentTrackKey() => currentTrackKey;
        public LoopData GetCurrentLoop() => currentLoop;
        public bool IsPlaying() => isPlaying;
        public bool IsTransitioning() => isTransitioning;
        public bool HasScheduledTransition() => scheduledTransition.HasValue;
        public double GetCurrentDspTime() => AudioSettings.dspTime;
        public double GetLoopStartDspTime() => loopStartDspTime;
        public bool IsUsingStemPlayback() => isUsingStemPlayback;
        public int GetActiveStemCount() => isUsingStemPlayback && currentLoop != null ? currentLoop.stems.Count : 0;

        // ==================== SETTINGS ====================

        public void SetBaseCrossfadeDuration(float duration)
        {
            baseCrossfadeDuration = Mathf.Max(0.01f, duration);
        }

        public void SetQualityAdaptiveCrossfade(bool enabled)
        {
            qualityAdaptiveCrossfade = enabled;
        }

        public void SetAutoLoopProgression(bool enabled, float chance = 0.3f)
        {
            autoLoopProgression = enabled;
            loopProgressionChance = Mathf.Clamp01(chance);
        }

        public void SetUseQuantizedTransitions(bool enabled)
        {
            useQuantizedTransitions = enabled;
        }

        public void SetMaxSyncWaitTime(float seconds)
        {
            maxSyncWaitTime = Mathf.Max(0.1f, seconds);
        }

        public void SetDspBufferTime(double seconds)
        {
            dspBufferTime = Mathf.Max(0.01f, (float)seconds);
        }

        // ==================== NESTED CLASSES ====================

        private struct ScheduledTransition
        {
            public string trackKey;
            public LoopData targetLoop;
            public double scheduledDspTime;
            public float syncPointTime;
        }
    }
}
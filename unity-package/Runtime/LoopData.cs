using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveMusic
{
    /// <summary>
    /// Represents a single audio loop with quality metrics, intensity data,
    /// and MAGI-style structural information for quantized transitions.
    /// </summary>
    [Serializable]
    public class LoopData
    {
        // ==================== LEGACY HORIZONTAL FLUIDITY ====================
        [Header("Audio Clip")]
        [Tooltip("Primary audio clip for this loop (legacy single-clip mode)")]
        public AudioClip clip;

        [Header("Quality & Intensity")]
        [Tooltip("Quality score from AI extraction (0.0 - 1.0)")]
        [Range(0f, 1f)]
        public float quality = 0.8f;

        [Tooltip("Normalized intensity level (0.0 = calm, 1.0 = intense)")]
        [Range(0f, 1f)]
        public float intensity = 0.5f;

        [Tooltip("Optional tags for categorization (e.g., 'combat', 'ambient')")]
        public List<string> tags = new List<string>();

        // ==================== MAGI STRUCTURAL FEATURES ====================
        [Header("MAGI Sync Structure")]
        [Tooltip("Timestamps (in seconds) where it is musically safe to exit this loop")]
        public List<float> exitSyncPoints = new List<float>();

        [Tooltip("Clip to play when transitioning OUT of this loop (pre-end/fill)")]
        public AudioClip transitionOutClip;

        [Tooltip("Duration of the loop in beats for accurate bar counting")]
        public int durationInBeats = 16;

        [Tooltip("Tempo of this loop in BPM")]
        public float bpm = 120f;

        // ==================== VERTICAL REMIXING (STEMS) ====================
        [Header("Vertical Stems (Future)")]
        [Tooltip("Multiple stems that play simultaneously (drums, bass, melody, etc.)")]
        public List<AudioClip> stems = new List<AudioClip>();

        [Tooltip("Whether to use stems instead of the single clip")]
        public bool useStems = false;

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Returns the duration of the loop in seconds
        /// </summary>
        public float Duration
        {
            get
            {
                if (clip != null)
                    return clip.length;
                if (useStems && stems.Count > 0 && stems[0] != null)
                    return stems[0].length;
                return 0f;
            }
        }

        /// <summary>
        /// Returns the duration of a single beat in seconds
        /// </summary>
        public float BeatDuration => 60f / bpm;

        /// <summary>
        /// Returns whether this loop has valid sync points defined
        /// </summary>
        public bool HasSyncPoints => exitSyncPoints != null && exitSyncPoints.Count > 0;

        /// <summary>
        /// Returns whether this loop has a transition-out clip
        /// </summary>
        public bool HasTransitionOut => transitionOutClip != null;

        // ==================== SYNC POINT LOGIC ====================

        /// <summary>
        /// Finds the next valid exit sync point after the given track time.
        /// Returns -1 if no sync point is found.
        /// </summary>
        /// <param name="currentTrackTime">Current playback position in seconds</param>
        /// <returns>Time in seconds of the next sync point, or -1 if none found</returns>
        public float GetNextSyncTime(float currentTrackTime)
        {
            if (!HasSyncPoints)
                return -1f;

            // Find the first sync point that occurs after the current time
            var nextPoint = exitSyncPoints
                .Where(t => t > currentTrackTime)
                .OrderBy(t => t)
                .FirstOrDefault();

            // If we found a valid point, return it
            if (nextPoint > 0)
                return nextPoint;

            // If we've passed all sync points, loop back to the first one
            // (assuming the loop will wrap around)
            return exitSyncPoints.OrderBy(t => t).FirstOrDefault();
        }

        /// <summary>
        /// Gets all sync points within a time window
        /// </summary>
        /// <param name="startTime">Start of the window in seconds</param>
        /// <param name="endTime">End of the window in seconds</param>
        /// <returns>List of sync points within the window</returns>
        public List<float> GetSyncPointsInWindow(float startTime, float endTime)
        {
            if (!HasSyncPoints)
                return new List<float>();

            return exitSyncPoints
                .Where(t => t >= startTime && t <= endTime)
                .OrderBy(t => t)
                .ToList();
        }

        /// <summary>
        /// Returns the closest sync point to the target time (can be before or after)
        /// </summary>
        public float GetClosestSyncPoint(float targetTime)
        {
            if (!HasSyncPoints)
                return -1f;

            return exitSyncPoints
                .OrderBy(t => Mathf.Abs(t - targetTime))
                .FirstOrDefault();
        }

        /// <summary>
        /// Calculates how long to wait (in seconds) from current time to next sync point
        /// </summary>
        public float GetWaitTimeToNextSync(float currentTrackTime)
        {
            float nextSync = GetNextSyncTime(currentTrackTime);
            if (nextSync < 0)
                return -1f;

            float waitTime = nextSync - currentTrackTime;
            
            // Handle wrap-around case
            if (waitTime < 0)
                waitTime += Duration;

            return waitTime;
        }

        // ==================== AUTO-GENERATION HELPERS ====================

        /// <summary>
        /// Auto-generates sync points at bar boundaries based on BPM and duration.
        /// Call this from the editor or during import.
        /// </summary>
        public void GenerateSyncPointsOnBars()
        {
            exitSyncPoints.Clear();

            if (durationInBeats <= 0 || bpm <= 0)
            {
                Debug.LogWarning("Cannot generate sync points: invalid durationInBeats or BPM");
                return;
            }

            float barDuration = BeatDuration * 4; // Assume 4/4 time signature
            int numBars = Mathf.CeilToInt(Duration / barDuration);

            for (int i = 1; i <= numBars; i++)
            {
                float syncTime = i * barDuration;
                if (syncTime < Duration)
                    exitSyncPoints.Add(syncTime);
            }

            Debug.Log($"Generated {exitSyncPoints.Count} sync points for loop");
        }

        /// <summary>
        /// Auto-generates sync points at beat boundaries
        /// </summary>
        public void GenerateSyncPointsOnBeats()
        {
            exitSyncPoints.Clear();

            if (bpm <= 0)
            {
                Debug.LogWarning("Cannot generate sync points: invalid BPM");
                return;
            }

            float beatDur = BeatDuration;
            int numBeats = Mathf.FloorToInt(Duration / beatDur);

            for (int i = 1; i <= numBeats; i++)
            {
                float syncTime = i * beatDur;
                if (syncTime < Duration)
                    exitSyncPoints.Add(syncTime);
            }

            Debug.Log($"Generated {exitSyncPoints.Count} sync points (on beats) for loop");
        }

        // ==================== VALIDATION ====================

        /// <summary>
        /// Validates the loop data and returns true if everything is valid
        /// </summary>
        public bool Validate(out string error)
        {
            error = "";

            // Check if we have audio content
            if (!useStems && clip == null)
            {
                error = "No audio clip assigned";
                return false;
            }

            if (useStems && (stems == null || stems.Count == 0 || stems[0] == null))
            {
                error = "Stems mode enabled but no stems assigned";
                return false;
            }

            // Validate sync points are within bounds
            if (HasSyncPoints)
            {
                float duration = Duration;
                foreach (var syncPoint in exitSyncPoints)
                {
                    if (syncPoint < 0 || syncPoint > duration)
                    {
                        error = $"Sync point {syncPoint}s is outside loop duration ({duration}s)";
                        return false;
                    }
                }
            }

            return true;
        }

        // ==================== UTILITY ====================

        public override string ToString()
        {
            string clipName = useStems && stems.Count > 0 ? stems[0].name : (clip != null ? clip.name : "None");
            return $"Loop: {clipName} | Quality: {quality:F2} | Intensity: {intensity:F2} | " +
                   $"BPM: {bpm} | Sync Points: {exitSyncPoints.Count} | Duration: {Duration:F2}s";
        }
    }
}
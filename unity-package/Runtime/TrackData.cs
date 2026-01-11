using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveMusic
{
    /// <summary>
    /// ScriptableObject representing a complete music track with all its loops.
    /// Supports both legacy and MAGI-style features.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTrack", menuName = "Adaptive Music/Track Data")]
    public class TrackData : ScriptableObject
    {
        [Header("Track Info")]
        [Tooltip("Unique identifier for this track")]
        public string trackKey;

        [Tooltip("Display name for this track")]
        public string displayName;

        [Tooltip("Optional tags for categorization")]
        public List<string> tags = new List<string>();

        [Header("Loops")]
        [Tooltip("All loops that belong to this track")]
        public List<LoopData> loops = new List<LoopData>();

        [Header("MAGI Configuration")]
        [Tooltip("Default BPM for all loops (can be overridden per-loop)")]
        public float defaultBPM = 120f;

        [Tooltip("Default time signature (beats per bar)")]
        public int beatsPerBar = 4;

        // ==================== LOOP SELECTION ====================

        /// <summary>
        /// Get a random loop from this track
        /// </summary>
        public LoopData GetRandomLoop()
        {
            if (loops == null || loops.Count == 0)
                return null;

            return loops[Random.Range(0, loops.Count)];
        }

        /// <summary>
        /// Get a loop by index
        /// </summary>
        public LoopData GetLoopByIndex(int index)
        {
            if (loops == null || index < 0 || index >= loops.Count)
                return null;

            return loops[index];
        }

        /// <summary>
        /// Get all loops that have a specific tag
        /// </summary>
        public List<LoopData> GetLoopsWithTag(string tag)
        {
            if (loops == null)
                return new List<LoopData>();

            return loops.Where(l => l.tags != null && l.tags.Contains(tag)).ToList();
        }

        /// <summary>
        /// Get the loop with quality closest to the target value
        /// </summary>
        public LoopData GetLoopClosestToQuality(float targetQuality)
        {
            if (loops == null || loops.Count == 0)
                return null;

            return loops.OrderBy(l => Mathf.Abs(l.quality - targetQuality)).FirstOrDefault();
        }

        /// <summary>
        /// Get the loop with intensity closest to the target value
        /// </summary>
        public LoopData GetLoopClosestToIntensity(float targetIntensity)
        {
            if (loops == null || loops.Count == 0)
                return null;

            return loops.OrderBy(l => Mathf.Abs(l.intensity - targetIntensity)).FirstOrDefault();
        }

        /// <summary>
        /// Get the highest quality loop
        /// </summary>
        public LoopData GetBestQualityLoop()
        {
            if (loops == null || loops.Count == 0)
                return null;

            return loops.OrderByDescending(l => l.quality).FirstOrDefault();
        }

        /// <summary>
        /// Get loops within an intensity range
        /// </summary>
        public List<LoopData> GetLoopsInIntensityRange(float minIntensity, float maxIntensity)
        {
            if (loops == null)
                return new List<LoopData>();

            return loops.Where(l => l.intensity >= minIntensity && l.intensity <= maxIntensity).ToList();
        }

        // ==================== MAGI UTILITIES ====================

        /// <summary>
        /// Auto-generate sync points for all loops in this track
        /// </summary>
        public void GenerateAllSyncPoints(bool useBeats = false)
        {
            if (loops == null)
                return;

            foreach (var loop in loops)
            {
                // Set default BPM if not set
                if (loop.bpm <= 0)
                    loop.bpm = defaultBPM;

                // Generate sync points
                if (useBeats)
                    loop.GenerateSyncPointsOnBeats();
                else
                    loop.GenerateSyncPointsOnBars();
            }

            Debug.Log($"Generated sync points for {loops.Count} loops in track '{displayName}'");
        }

        /// <summary>
        /// Validate all loops in this track
        /// </summary>
        public bool ValidateAllLoops(out List<string> errors)
        {
            errors = new List<string>();

            if (loops == null || loops.Count == 0)
            {
                errors.Add("Track has no loops");
                return false;
            }

            for (int i = 0; i < loops.Count; i++)
            {
                string error;
                if (!loops[i].Validate(out error))
                {
                    errors.Add($"Loop {i}: {error}");
                }
            }

            return errors.Count == 0;
        }

        // ==================== EDITOR UTILITIES ====================

#if UNITY_EDITOR
        /// <summary>
        /// Sort loops by intensity (for easier browsing in editor)
        /// </summary>
        [ContextMenu("Sort Loops by Intensity")]
        public void SortLoopsByIntensity()
        {
            if (loops != null)
            {
                loops = loops.OrderBy(l => l.intensity).ToList();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// Sort loops by quality
        /// </summary>
        [ContextMenu("Sort Loops by Quality")]
        public void SortLoopsByQuality()
        {
            if (loops != null)
            {
                loops = loops.OrderByDescending(l => l.quality).ToList();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// Generate sync points for all loops
        /// </summary>
        [ContextMenu("Generate Sync Points (Bars)")]
        public void GenerateSyncPointsOnBars()
        {
            GenerateAllSyncPoints(useBeats: false);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Generate sync points on beats
        /// </summary>
        [ContextMenu("Generate Sync Points (Beats)")]
        public void GenerateSyncPointsOnBeats()
        {
            GenerateAllSyncPoints(useBeats: true);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
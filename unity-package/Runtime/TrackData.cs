using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveMusic
{
    /// <summary>
    /// Represents a complete track with all its extracted loops
    /// </summary>
    [CreateAssetMenu(fileName = "New Track", menuName = "Adaptive Music/Track Data")]
    public class TrackData : ScriptableObject
    {
        [Header("Track Info")]
        [Tooltip("Unique identifier for this track")]
        public string key;
        
        [Tooltip("Display name")]
        public string displayName;
        
        [Tooltip("Tempo in BPM (from audio analysis)")]
        public float bpm = 120f;
        
        [Header("Loops")]
        [Tooltip("All loops extracted from this track")]
        public List<LoopData> loops = new List<LoopData>();
        
        [Header("Playback Settings")]
        [Tooltip("Default crossfade duration (can be overridden per-loop)")]
        public float defaultCrossfade = 0.1f;
        
        [Tooltip("Tags for categorizing the track (action, ambient, menu, etc.)")]
        public string[] tags;

        /// <summary>
        /// Get a random loop from this track
        /// </summary>
        public LoopData GetRandomLoop()
        {
            if (loops.Count == 0) return null;
            return loops[Random.Range(0, loops.Count)];
        }

        /// <summary>
        /// Get a random loop, excluding a specific one
        /// </summary>
        public LoopData GetRandomLoopExcluding(LoopData exclude)
        {
            if (loops.Count <= 1) return null;
            
            List<LoopData> available = loops.Where(l => l != exclude).ToList();
            if (available.Count == 0) return null;
            
            return available[Random.Range(0, available.Count)];
        }

        /// <summary>
        /// Get loop by index
        /// </summary>
        public LoopData GetLoopByIndex(int index)
        {
            if (index < 0 || index >= loops.Count) return null;
            return loops[index];
        }

        /// <summary>
        /// Get all loops with a specific tag
        /// </summary>
        public List<LoopData> GetLoopsWithTag(string tag)
        {
            return loops.Where(l => l.tags != null && l.tags.Contains(tag)).ToList();
        }

        /// <summary>
        /// Get loop closest to target quality score
        /// </summary>
        public LoopData GetLoopClosestToQuality(float targetQuality)
        {
            if (loops.Count == 0) return null;
            
            return loops.OrderBy(l => Mathf.Abs(l.quality - targetQuality)).First();
        }

        /// <summary>
        /// Get loop closest to target intensity
        /// </summary>
        public LoopData GetLoopClosestToIntensity(float targetIntensity)
        {
            if (loops.Count == 0) return null;
            
            return loops.OrderBy(l => Mathf.Abs(l.intensity - targetIntensity)).First();
        }

        /// <summary>
        /// Get loops within a quality range
        /// </summary>
        public List<LoopData> GetLoopsInQualityRange(float minQuality, float maxQuality)
        {
            return loops.Where(l => l.quality >= minQuality && l.quality <= maxQuality).ToList();
        }

        /// <summary>
        /// Get the highest quality loop
        /// </summary>
        public LoopData GetBestQualityLoop()
        {
            if (loops.Count == 0) return null;
            return loops.OrderByDescending(l => l.quality).First();
        }

        /// <summary>
        /// Get loops sorted by their original position in track
        /// </summary>
        public List<LoopData> GetLoopsByPosition()
        {
            return loops.OrderBy(l => l.startTime).ToList();
        }

        /// <summary>
        /// Add a loop to this track
        /// </summary>
        public void AddLoop(LoopData loop)
        {
            if (loop != null && !loops.Contains(loop))
            {
                loops.Add(loop);
            }
        }

        /// <summary>
        /// Auto-populate loops from a folder of audio files
        /// Call this from editor script or inspector button
        /// </summary>
        public void LoadLoopsFromClips(AudioClip[] clips)
        {
            loops.Clear();
            
            foreach (AudioClip clip in clips)
            {
                if (clip == null) continue;
                
                // Parse metadata from filename
                LoopData loop = LoopData.FromFilename(clip, clip.name);
                loop.recommendedCrossfade = defaultCrossfade;
                loops.Add(loop);
            }
            
            // Sort by start time
            loops = loops.OrderBy(l => l.startTime).ToList();
            
            Debug.Log($"Loaded {loops.Count} loops for track '{displayName}'");
        }

        private void OnValidate()
        {
            // Ensure loops are sorted
            if (loops.Count > 1)
            {
                loops = loops.OrderBy(l => l.index).ToList();
            }
        }
    }
}

using UnityEngine;

namespace AdaptiveMusic
{
    /// <summary>
    /// Represents a single extracted loop with its metadata
    /// </summary>
    [System.Serializable]
    public class LoopData
    {
        [Tooltip("The audio clip for this loop")]
        public AudioClip clip;
        
        [Tooltip("Loop index/number from extraction")]
        public int index;
        
        [Tooltip("Start time in original track (seconds)")]
        public float startTime;
        
        [Tooltip("Duration of the loop (seconds)")]
        public float duration;
        
        [Tooltip("Loop quality score (0.4-1.0, from audio analyzer)")]
        [Range(0f, 1f)]
        public float quality = 0.8f;
        
        [Tooltip("Recommended crossfade duration for this loop (from analyzer)")]
        public float recommendedCrossfade = 0.1f;
        
        [Tooltip("Optional: Custom intensity/energy level for adaptive music")]
        [Range(0f, 1f)]
        public float intensity = 0.5f;
        
        [Tooltip("Optional: Tags for categorizing loops (intro, verse, chorus, etc.)")]
        public string[] tags;

        public LoopData(AudioClip clip, int index, float startTime, float duration, float quality = 0.8f)
        {
            this.clip = clip;
            this.index = index;
            this.startTime = startTime;
            this.duration = duration;
            this.quality = quality;
        }

        /// <summary>
        /// Create LoopData from the extracted loop filename
        /// Format: trackname_loop_01_10.50s_8.23s.wav
        /// </summary>
        public static LoopData FromFilename(AudioClip clip, string filename)
        {
            // Parse filename to extract metadata
            // Example: "track_loop_01_10.50s_8.23s"
            string[] parts = filename.Split('_');
            
            int index = 0;
            float startTime = 0f;
            float duration = 0f;

            // Find loop index
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "loop" && i + 1 < parts.Length)
                {
                    int.TryParse(parts[i + 1], out index);
                }
                
                // Parse timing (look for "s" suffix)
                if (parts[i].EndsWith("s"))
                {
                    string timeStr = parts[i].TrimEnd('s');
                    float time;
                    if (float.TryParse(timeStr, out time))
                    {
                        if (startTime == 0f)
                            startTime = time;
                        else
                            duration = time;
                    }
                }
            }

            // Use clip length if duration wasn't parsed
            if (duration == 0f && clip != null)
            {
                duration = clip.length;
            }

            return new LoopData(clip, index, startTime, duration);
        }

        public override string ToString()
        {
            return $"Loop {index}: {startTime:F2}s, {duration:F2}s, Q:{quality:F2}";
        }
    }
}

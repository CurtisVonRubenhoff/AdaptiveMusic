using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveMusic
{
    /// <summary>
    /// Database of all music tracks and their loops
    /// </summary>
    [CreateAssetMenu(fileName = "MusicDatabase", menuName = "Adaptive Music/Music Database")]
    public class MusicDatabase : ScriptableObject
    {
        [Header("Tracks")]
        [Tooltip("All available tracks with their loops")]
        public List<TrackData> tracks = new List<TrackData>();

        private Dictionary<string, TrackData> trackLookup;

        public void Init()
        {
            // Build lookup dictionary for fast access
            trackLookup = new Dictionary<string, TrackData>();
            
            foreach (TrackData track in tracks)
            {
                if (track != null && !string.IsNullOrEmpty(track.key))
                {
                    if (!trackLookup.ContainsKey(track.key))
                    {
                        trackLookup[track.key] = track;
                    }
                    else
                    {
                        Debug.LogWarning($"Duplicate track key found: {track.key}");
                    }
                }
            }
            
            Debug.Log($"MusicDatabase initialized with {trackLookup.Count} tracks");
        }

        public void Teardown()
        {
            trackLookup?.Clear();
        }

        /// <summary>
        /// Get a track by its key
        /// </summary>
        public TrackData GetTrack(string key)
        {
            if (trackLookup == null) Init();
            
            if (trackLookup.TryGetValue(key, out TrackData track))
            {
                return track;
            }
            
            Debug.LogWarning($"Track not found: {key}");
            return null;
        }

        /// <summary>
        /// Get all tracks with a specific tag
        /// </summary>
        public List<TrackData> GetTracksWithTag(string tag)
        {
            return tracks.Where(t => t.tags != null && t.tags.Contains(tag)).ToList();
        }

        /// <summary>
        /// Get all track keys
        /// </summary>
        public List<string> GetAllTrackKeys()
        {
            return tracks.Where(t => !string.IsNullOrEmpty(t.key)).Select(t => t.key).ToList();
        }

        /// <summary>
        /// Get a random track
        /// </summary>
        public TrackData GetRandomTrack()
        {
            if (tracks.Count == 0) return null;
            return tracks[Random.Range(0, tracks.Count)];
        }

        /// <summary>
        /// Get a random track with a specific tag
        /// </summary>
        public TrackData GetRandomTrackWithTag(string tag)
        {
            List<TrackData> tagged = GetTracksWithTag(tag);
            if (tagged.Count == 0) return null;
            return tagged[Random.Range(0, tagged.Count)];
        }

        private void OnValidate()
        {
            // Check for duplicate keys
            HashSet<string> keys = new HashSet<string>();
            foreach (TrackData track in tracks)
            {
                if (track != null && !string.IsNullOrEmpty(track.key))
                {
                    if (keys.Contains(track.key))
                    {
                        Debug.LogError($"Duplicate track key: {track.key}");
                    }
                    keys.Add(track.key);
                }
            }
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveMusic
{
    /// <summary>
    /// ScriptableObject database containing all music tracks in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "MusicDatabase", menuName = "Adaptive Music/Music Database")]
    public class MusicDatabase : ScriptableObject
    {
        [Header("Tracks")]
        [Tooltip("All tracks in the database")]
        public List<TrackData> tracks = new List<TrackData>();

        [Header("Settings")]
        [Tooltip("Enable debug logging")]
        public bool debugMode = false;

        // ==================== TRACK RETRIEVAL ====================

        /// <summary>
        /// Get a track by its key
        /// </summary>
        public TrackData GetTrack(string trackKey)
        {
            if (tracks == null || string.IsNullOrEmpty(trackKey))
                return null;

            var track = tracks.FirstOrDefault(t => t.trackKey == trackKey);

            if (track == null && debugMode)
            {
                Debug.LogWarning($"Track '{trackKey}' not found in database");
            }

            return track;
        }

        /// <summary>
        /// Get all tracks that have a specific tag
        /// </summary>
        public List<TrackData> GetTracksWithTag(string tag)
        {
            if (tracks == null)
                return new List<TrackData>();

            return tracks.Where(t => t.tags != null && t.tags.Contains(tag)).ToList();
        }

        /// <summary>
        /// Get a random track from the database
        /// </summary>
        public TrackData GetRandomTrack()
        {
            if (tracks == null || tracks.Count == 0)
                return null;

            return tracks[Random.Range(0, tracks.Count)];
        }

        /// <summary>
        /// Check if a track exists
        /// </summary>
        public bool HasTrack(string trackKey)
        {
            return GetTrack(trackKey) != null;
        }

        /// <summary>
        /// Get all track keys
        /// </summary>
        public List<string> GetAllTrackKeys()
        {
            if (tracks == null)
                return new List<string>();

            return tracks.Select(t => t.trackKey).ToList();
        }

        // ==================== VALIDATION ====================

        /// <summary>
        /// Validate the entire database
        /// </summary>
        public bool ValidateDatabase(out List<string> errors)
        {
            errors = new List<string>();

            if (tracks == null || tracks.Count == 0)
            {
                errors.Add("Database contains no tracks");
                return false;
            }

            // Check for duplicate keys
            var duplicateKeys = tracks
                .GroupBy(t => t.trackKey)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var key in duplicateKeys)
            {
                errors.Add($"Duplicate track key: '{key}'");
            }

            // Validate each track
            foreach (var track in tracks)
            {
                if (track == null)
                {
                    errors.Add("Database contains null track reference");
                    continue;
                }

                if (string.IsNullOrEmpty(track.trackKey))
                {
                    errors.Add($"Track '{track.name}' has no track key");
                }

                List<string> trackErrors;
                if (!track.ValidateAllLoops(out trackErrors))
                {
                    errors.Add($"Track '{track.trackKey}' has errors:");
                    errors.AddRange(trackErrors.Select(e => "  - " + e));
                }
            }

            return errors.Count == 0;
        }

        // ==================== EDITOR UTILITIES ====================

#if UNITY_EDITOR
        /// <summary>
        /// Validate the database and log results
        /// </summary>
        [ContextMenu("Validate Database")]
        public void ValidateDatabaseInEditor()
        {
            List<string> errors;
            bool isValid = ValidateDatabase(out errors);

            if (isValid)
            {
                Debug.Log($"<color=green>Database validation passed! {tracks.Count} tracks validated.</color>");
            }
            else
            {
                Debug.LogError($"Database validation failed with {errors.Count} errors:");
                foreach (var error in errors)
                {
                    Debug.LogError($"  - {error}");
                }
            }
        }

        /// <summary>
        /// Generate sync points for all tracks
        /// </summary>
        [ContextMenu("Generate All Sync Points")]
        public void GenerateAllSyncPoints()
        {
            if (tracks == null)
                return;

            int totalLoops = 0;
            foreach (var track in tracks)
            {
                if (track != null)
                {
                    track.GenerateAllSyncPoints();
                    totalLoops += track.loops.Count;
                }
            }

            Debug.Log($"Generated sync points for {totalLoops} loops across {tracks.Count} tracks");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Log database statistics
        /// </summary>
        [ContextMenu("Log Database Stats")]
        public void LogDatabaseStats()
        {
            if (tracks == null)
            {
                Debug.Log("Database is empty");
                return;
            }

            int totalLoops = tracks.Sum(t => t.loops?.Count ?? 0);
            int loopsWithSyncPoints = 0;
            int loopsWithTransitions = 0;

            foreach (var track in tracks)
            {
                if (track?.loops == null) continue;

                foreach (var loop in track.loops)
                {
                    if (loop.HasSyncPoints) loopsWithSyncPoints++;
                    if (loop.HasTransitionOut) loopsWithTransitions++;
                }
            }

            Debug.Log($"=== Music Database Stats ===\n" +
                     $"Tracks: {tracks.Count}\n" +
                     $"Total Loops: {totalLoops}\n" +
                     $"Loops with Sync Points: {loopsWithSyncPoints} ({(float)loopsWithSyncPoints / totalLoops * 100:F1}%)\n" +
                     $"Loops with Transition Clips: {loopsWithTransitions} ({(float)loopsWithTransitions / totalLoops * 100:F1}%)");
        }
#endif
    }
}
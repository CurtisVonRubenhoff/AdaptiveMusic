using UnityEngine;
using UnityEditor;
using System.Linq;

namespace AdaptiveMusic.Editor
{
    /// <summary>
    /// Custom editor for the MusicDatabase ScriptableObject.
    /// Provides validation, statistics, and helpful tools.
    /// </summary>
    [CustomEditor(typeof(MusicDatabase))]
    public class MusicDatabaseEditor : UnityEditor.Editor
    {
        private MusicDatabase database;
        private bool showStats = true;
        private bool showTracks = true;
        private bool showValidation = true;

        private void OnEnable()
        {
            database = target as MusicDatabase;
        }

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();

            // Statistics section
            showStats = EditorGUILayout.Foldout(showStats, "Database Statistics", true);
            if (showStats)
            {
                DrawStatistics();
            }

            EditorGUILayout.Space();

            // Tracks list section
            showTracks = EditorGUILayout.Foldout(showTracks, "Tracks Overview", true);
            if (showTracks)
            {
                DrawTracksOverview();
            }

            EditorGUILayout.Space();

            // Validation section
            showValidation = EditorGUILayout.Foldout(showValidation, "Validation", true);
            if (showValidation)
            {
                DrawValidation();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();

            // Action buttons
            DrawActionButtons();
        }

        private void DrawStatistics()
        {
            EditorGUI.indentLevel++;

            if (database.tracks == null || database.tracks.Count == 0)
            {
                EditorGUILayout.HelpBox("No tracks in database", MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            int totalTracks = database.tracks.Count;
            int totalLoops = database.tracks.Sum(t => t != null && t.loops != null ? t.loops.Count : 0);
            int loopsWithSyncPoints = 0;
            int loopsWithTransitions = 0;
            int loopsWithStems = 0;
            float avgQuality = 0f;
            int qualityCount = 0;

            foreach (var track in database.tracks)
            {
                if (track?.loops == null) continue;

                foreach (var loop in track.loops)
                {
                    if (loop == null) continue;

                    if (loop.HasSyncPoints) loopsWithSyncPoints++;
                    if (loop.HasTransitionOut) loopsWithTransitions++;
                    if (loop.useStems && loop.stems.Count > 0) loopsWithStems++;
                    
                    avgQuality += loop.quality;
                    qualityCount++;
                }
            }

            if (qualityCount > 0)
                avgQuality /= qualityCount;

            EditorGUILayout.LabelField("Total Tracks:", totalTracks.ToString());
            EditorGUILayout.LabelField("Total Loops:", totalLoops.ToString());
            EditorGUILayout.LabelField("Average Quality:", $"{avgQuality:F2}");
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MAGI Features:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Loops with Sync Points:", $"{loopsWithSyncPoints} ({(float)loopsWithSyncPoints / Mathf.Max(1, totalLoops) * 100:F1}%)");
            EditorGUILayout.LabelField("Loops with Transitions:", $"{loopsWithTransitions} ({(float)loopsWithTransitions / Mathf.Max(1, totalLoops) * 100:F1}%)");
            EditorGUILayout.LabelField("Loops with Stems:", $"{loopsWithStems} ({(float)loopsWithStems / Mathf.Max(1, totalLoops) * 100:F1}%)");
            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
        }

        private void DrawTracksOverview()
        {
            EditorGUI.indentLevel++;

            if (database.tracks == null || database.tracks.Count == 0)
            {
                EditorGUILayout.HelpBox("No tracks in database", MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            foreach (var track in database.tracks)
            {
                if (track == null)
                {
                    EditorGUILayout.HelpBox("Null track reference!", MessageType.Error);
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Track header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(track.displayName, EditorStyles.boldLabel);
                
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = track;
                    EditorGUIUtility.PingObject(track);
                }
                EditorGUILayout.EndHorizontal();

                // Track info
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Key:", track.trackKey);
                EditorGUILayout.LabelField("Loops:", track.loops != null ? track.loops.Count.ToString() : "0");
                EditorGUILayout.LabelField("BPM:", track.defaultBPM.ToString("F0"));
                
                if (track.tags != null && track.tags.Count > 0)
                {
                    EditorGUILayout.LabelField("Tags:", string.Join(", ", track.tags));
                }

                // Sync point info
                if (track.loops != null)
                {
                    int syncCount = track.loops.Count(l => l != null && l.HasSyncPoints);
                    if (syncCount > 0)
                    {
                        EditorGUILayout.LabelField("Sync Points:", $"{syncCount}/{track.loops.Count} loops");
                    }
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawValidation()
        {
            EditorGUI.indentLevel++;

            if (database.tracks == null || database.tracks.Count == 0)
            {
                EditorGUILayout.HelpBox("Database is empty", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            // Check for duplicate keys
            var duplicateKeys = database.tracks
                .Where(t => t != null)
                .GroupBy(t => t.trackKey)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            if (duplicateKeys.Any())
            {
                EditorGUILayout.HelpBox(
                    "Duplicate track keys found:\n" + string.Join(", ", duplicateKeys),
                    MessageType.Error
                );
            }

            // Check for null tracks
            int nullCount = database.tracks.Count(t => t == null);
            if (nullCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{nullCount} null track reference(s) found",
                    MessageType.Error
                );
            }

            // Check for empty track keys
            int emptyKeyCount = database.tracks.Count(t => t != null && string.IsNullOrEmpty(t.trackKey));
            if (emptyKeyCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{emptyKeyCount} track(s) with empty keys found",
                    MessageType.Warning
                );
            }

            // Check for tracks without loops
            int emptyTrackCount = database.tracks.Count(t => t != null && (t.loops == null || t.loops.Count == 0));
            if (emptyTrackCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{emptyTrackCount} track(s) without loops found",
                    MessageType.Warning
                );
            }

            // MAGI validation
            int tracksWithoutSync = 0;
            foreach (var track in database.tracks)
            {
                if (track?.loops == null) continue;
                
                bool hasAnySync = track.loops.Any(l => l != null && l.HasSyncPoints);
                if (!hasAnySync && track.loops.Count > 0)
                {
                    tracksWithoutSync++;
                }
            }

            if (tracksWithoutSync > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{tracksWithoutSync} track(s) without sync points (MAGI features disabled)",
                    MessageType.Info
                );
            }

            // Show success if all validations pass
            if (!duplicateKeys.Any() && nullCount == 0 && emptyKeyCount == 0 && emptyTrackCount == 0)
            {
                EditorGUILayout.HelpBox("All validation checks passed!", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Validate Database"))
            {
                ValidateDatabase();
            }

            if (GUILayout.Button("Generate All Sync Points"))
            {
                GenerateAllSyncPoints();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clean Null Tracks"))
            {
                CleanNullTracks();
            }

            if (GUILayout.Button("Log Statistics"))
            {
                LogStatistics();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Danger zone
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Clear All Tracks (Cannot Undo!)"))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Tracks",
                    "This will remove all tracks from the database. This cannot be undone!\n\nAre you sure?",
                    "Yes, Clear All",
                    "Cancel"))
                {
                    ClearAllTracks();
                }
            }
            GUI.color = Color.white;
        }

        private void ValidateDatabase()
        {
            var errors = new System.Collections.Generic.List<string>();
            bool isValid = database.ValidateDatabase(out errors);

            if (isValid)
            {
                EditorUtility.DisplayDialog(
                    "Validation Successful",
                    $"Database validation passed!\n\n{database.tracks.Count} tracks validated.",
                    "OK"
                );
            }
            else
            {
                string errorMessage = "Database validation failed:\n\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("Validation Failed", errorMessage, "OK");
                Debug.LogError("Database Validation Errors:\n" + string.Join("\n", errors));
            }
        }

        private void GenerateAllSyncPoints()
        {
            if (database.tracks == null || database.tracks.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No tracks in database", "OK");
                return;
            }

            bool useBeats = EditorUtility.DisplayDialog(
                "Generate Sync Points",
                "Generate sync points for all loops in all tracks?\n\n" +
                "Choose the sync point placement:",
                "On Bars (Typical)",
                "On Beats (More Frequent)"
            );

            int totalLoops = 0;
            foreach (var track in database.tracks)
            {
                if (track?.loops == null) continue;

                track.GenerateAllSyncPoints(useBeats: !useBeats); // Dialog returns true for bars
                totalLoops += track.loops.Count;
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Success",
                $"Generated sync points for {totalLoops} loops across {database.tracks.Count} tracks\n\n" +
                $"Mode: {(useBeats ? "Bar-based" : "Beat-based")}",
                "OK"
            );
        }

        private void CleanNullTracks()
        {
            if (database.tracks == null) return;

            int nullCount = database.tracks.Count(t => t == null);
            if (nullCount == 0)
            {
                EditorUtility.DisplayDialog("Info", "No null tracks found", "OK");
                return;
            }

            database.tracks.RemoveAll(t => t == null);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Success", $"Removed {nullCount} null track reference(s)", "OK");
        }

        private void LogStatistics()
        {
            if (database.tracks == null || database.tracks.Count == 0)
            {
                Debug.Log("Database is empty");
                return;
            }

            int totalLoops = database.tracks.Sum(t => t?.loops?.Count ?? 0);
            int loopsWithSync = 0;
            int loopsWithTransitions = 0;

            foreach (var track in database.tracks)
            {
                if (track?.loops == null) continue;

                foreach (var loop in track.loops)
                {
                    if (loop == null) continue;
                    if (loop.HasSyncPoints) loopsWithSync++;
                    if (loop.HasTransitionOut) loopsWithTransitions++;
                }
            }

            Debug.Log($"=== Music Database Statistics ===\n" +
                     $"Tracks: {database.tracks.Count}\n" +
                     $"Total Loops: {totalLoops}\n" +
                     $"Loops with Sync Points: {loopsWithSync} ({(float)loopsWithSync / Mathf.Max(1, totalLoops) * 100:F1}%)\n" +
                     $"Loops with Transitions: {loopsWithTransitions} ({(float)loopsWithTransitions / Mathf.Max(1, totalLoops) * 100:F1}%)");
        }

        private void ClearAllTracks()
        {
            database.tracks.Clear();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log("All tracks cleared from database");
        }
    }
}
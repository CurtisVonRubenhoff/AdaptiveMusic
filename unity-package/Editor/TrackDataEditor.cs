using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdaptiveMusic.Editor
{
    /// <summary>
    /// Enhanced Track Data Editor with stem detection and automatic grouping.
    /// Works with loop_extractor output format.
    /// </summary>
    [CustomEditor(typeof(TrackData))]
    public class TrackDataEditor : UnityEditor.Editor
    {
        private TrackData track;
        private Vector2 loopScrollPos;
        private bool showMAGITools = true;
        private bool showStemTools = true;
        private int selectedLoopIndex = -1;

        // Stem detection
        private Dictionary<int, List<LoopData>> groupedLoops = new Dictionary<int, List<LoopData>>();
        private bool hasUngroupedStems = false;

        // Sorting
        private enum SortMode { Index, Quality, Intensity, Duration, StemCount }
        private SortMode currentSortMode = SortMode.Index;

        void OnEnable()
        {
            track = (TrackData)target;
            AnalyzeStems();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space();

            DrawBasicInfo();
            EditorGUILayout.Space();

            DrawStemTools();
            EditorGUILayout.Space();

            DrawMAGITools();
            EditorGUILayout.Space();

            DrawLoopList();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(track);
            }
        }

        // ==================== HEADER ====================

        void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 16;
            EditorGUILayout.LabelField($"Track: {track.displayName}", headerStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Loops: {track.loops?.Count ?? 0}", EditorStyles.miniLabel);
            
            if (hasUngroupedStems)
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.miniLabel);
                warningStyle.normal.textColor = Color.yellow;
                EditorGUILayout.LabelField("⚠ Ungrouped stems detected!", warningStyle);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        // ==================== BASIC INFO ====================

        void DrawBasicInfo()
        {
            EditorGUILayout.LabelField("Track Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            
            track.trackKey = EditorGUILayout.TextField("Track Key", track.trackKey);
            track.displayName = EditorGUILayout.TextField("Display Name", track.displayName);
            track.defaultBPM = EditorGUILayout.FloatField("Default BPM", track.defaultBPM);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(track);
            }
        }

        // ==================== STEM TOOLS ====================

        void DrawStemTools()
        {
            showStemTools = EditorGUILayout.Foldout(showStemTools, "🎛️ Stem Tools", true);
            
            if (!showStemTools)
                return;

            EditorGUI.indentLevel++;

            // Statistics
            int totalStems = 0;
            int loopsWithStems = 0;
            
            if (track.loops != null)
            {
                foreach (var loop in track.loops)
                {
                    if (loop.useStems && loop.stems != null)
                    {
                        totalStems += loop.stems.Count;
                        loopsWithStems++;
                    }
                }
            }

            EditorGUILayout.LabelField($"Loops with stems: {loopsWithStems} / {track.loops?.Count ?? 0}");
            EditorGUILayout.LabelField($"Total stems: {totalStems}");

            EditorGUILayout.Space();

            // Auto-Group Stems button
            if (hasUngroupedStems)
            {
                EditorGUILayout.HelpBox(
                    "Detected individual stem clips that could be grouped into multi-stem loops.\n" +
                    "Click 'Auto-Group Stems by Filename' to combine them.",
                    MessageType.Warning
                );

                if (GUILayout.Button("🔄 Auto-Group Stems by Filename"))
                {
                    AutoGroupStems();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No ungrouped stems detected. Loops are properly configured.", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Validate Stems button
            if (GUILayout.Button("🔍 Validate All Stems"))
            {
                ValidateAllStems();
            }

            // Re-Detect Stems button
            if (GUILayout.Button("🔄 Re-Analyze Stem Structure"))
            {
                AnalyzeStems();
            }

            EditorGUI.indentLevel--;
        }

        // ==================== MAGI TOOLS ====================

        void DrawMAGITools()
        {
            showMAGITools = EditorGUILayout.Foldout(showMAGITools, "⚡ MAGI Tools", true);
            
            if (!showMAGITools)
                return;

            EditorGUI.indentLevel++;

            // Statistics
            int loopsWithSyncPoints = 0;
            int totalSyncPoints = 0;

            if (track.loops != null)
            {
                foreach (var loop in track.loops)
                {
                    if (loop.HasSyncPoints)
                    {
                        loopsWithSyncPoints++;
                        totalSyncPoints += loop.exitSyncPoints.Count;
                    }
                }
            }

            EditorGUILayout.LabelField($"Loops with sync points: {loopsWithSyncPoints} / {track.loops?.Count ?? 0}");
            EditorGUILayout.LabelField($"Total sync points: {totalSyncPoints}");

            EditorGUILayout.Space();

            // Generate all sync points
            if (GUILayout.Button("⚡ Generate All Sync Points"))
            {
                GenerateAllSyncPoints();
            }

            // Clear all sync points
            if (GUILayout.Button("Clear All Sync Points"))
            {
                ClearAllSyncPoints();
            }

            EditorGUI.indentLevel--;
        }

        // ==================== LOOP LIST ====================

        void DrawLoopList()
        {
            EditorGUILayout.LabelField("Loops", EditorStyles.boldLabel);

            // Sorting controls
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sort by:", GUILayout.Width(60));
            
            if (GUILayout.Button("Index", currentSortMode == SortMode.Index ? EditorStyles.toolbarButton : EditorStyles.miniButton))
            {
                currentSortMode = SortMode.Index;
            }
            if (GUILayout.Button("Quality", currentSortMode == SortMode.Quality ? EditorStyles.toolbarButton : EditorStyles.miniButton))
            {
                currentSortMode = SortMode.Quality;
                SortLoops();
            }
            if (GUILayout.Button("Intensity", currentSortMode == SortMode.Intensity ? EditorStyles.toolbarButton : EditorStyles.miniButton))
            {
                currentSortMode = SortMode.Intensity;
                SortLoops();
            }
            if (GUILayout.Button("Duration", currentSortMode == SortMode.Duration ? EditorStyles.toolbarButton : EditorStyles.miniButton))
            {
                currentSortMode = SortMode.Duration;
                SortLoops();
            }
            if (GUILayout.Button("Stems", currentSortMode == SortMode.StemCount ? EditorStyles.toolbarButton : EditorStyles.miniButton))
            {
                currentSortMode = SortMode.StemCount;
                SortLoops();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Loop list
            loopScrollPos = EditorGUILayout.BeginScrollView(loopScrollPos, GUILayout.Height(300));

            if (track.loops != null && track.loops.Count > 0)
            {
                for (int i = 0; i < track.loops.Count; i++)
                {
                    DrawLoopCard(i);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No loops. Use the Loop Importer to add loops.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("+ Add Loop"))
            {
                Undo.RecordObject(track, "Add Loop");
                track.loops.Add(new LoopData());
                EditorUtility.SetDirty(track);
            }

            GUI.enabled = selectedLoopIndex >= 0 && selectedLoopIndex < track.loops.Count;
            if (GUILayout.Button("- Remove Selected"))
            {
                Undo.RecordObject(track, "Remove Loop");
                track.loops.RemoveAt(selectedLoopIndex);
                selectedLoopIndex = -1;
                EditorUtility.SetDirty(track);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        void DrawLoopCard(int index)
        {
            var loop = track.loops[index];
            bool isSelected = selectedLoopIndex == index;

            // Card background
            GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
            if (isSelected)
            {
                cardStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.5f, 0.8f, 0.3f));
            }

            EditorGUILayout.BeginVertical(cardStyle);

            // Header
            EditorGUILayout.BeginHorizontal();

            // Selection toggle
            if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)) != isSelected)
            {
                selectedLoopIndex = isSelected ? -1 : index;
            }

            // Loop info
            string clipName = loop.useStems && loop.stems != null && loop.stems.Count > 0
                ? $"Loop #{index:D2} (Multi-stem)"
                : loop.clip != null 
                    ? loop.clip.name 
                    : $"Loop #{index:D2} (Empty)";

            EditorGUILayout.LabelField($"{index:D2}", GUILayout.Width(30));
            EditorGUILayout.LabelField(clipName, EditorStyles.boldLabel);

            // Status indicators
            if (loop.HasSyncPoints)
            {
                GUIStyle syncStyle = new GUIStyle(EditorStyles.miniLabel);
                syncStyle.normal.textColor = Color.green;
                EditorGUILayout.LabelField($"⚡{loop.exitSyncPoints.Count}", syncStyle, GUILayout.Width(30));
            }

            if (loop.useStems)
            {
                GUIStyle stemStyle = new GUIStyle(EditorStyles.miniLabel);
                stemStyle.normal.textColor = Color.cyan;
                EditorGUILayout.LabelField($"🎛️{loop.stems?.Count ?? 0}", stemStyle, GUILayout.Width(30));
            }

            EditorGUILayout.EndHorizontal();

            // Stats
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Q: {loop.quality:F2}", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField($"I: {loop.intensity:F2}", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField($"BPM: {loop.bpm:F0}", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField($"Duration: {loop.Duration:F1}s", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Stem list (if multi-stem)
            if (loop.useStems && loop.stems != null && loop.stems.Count > 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Stems:", EditorStyles.miniLabel);
                foreach (var stem in loop.stems)
                {
                    if (stem != null)
                    {
                        EditorGUILayout.LabelField($"  • {stem.name}", EditorStyles.miniLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // ==================== STEM ANALYSIS ====================

        void AnalyzeStems()
        {
            groupedLoops.Clear();
            hasUngroupedStems = false;

            if (track.loops == null || track.loops.Count == 0)
                return;

            // Parse all loops to detect potential stem groups
            Dictionary<int, List<LoopData>> potentialGroups = new Dictionary<int, List<LoopData>>();

            foreach (var loop in track.loops)
            {
                // Skip if already using stems
                if (loop.useStems && loop.stems != null && loop.stems.Count > 1)
                    continue;

                // Try to parse clip name
                if (loop.clip != null)
                {
                    var stemInfo = ParseStemFilename(loop.clip.name);
                    
                    if (stemInfo.isValid)
                    {
                        if (!potentialGroups.ContainsKey(stemInfo.loopNumber))
                        {
                            potentialGroups[stemInfo.loopNumber] = new List<LoopData>();
                        }
                        potentialGroups[stemInfo.loopNumber].Add(loop);
                    }
                }
            }

            // Check if any groups have multiple clips (ungrouped stems)
            foreach (var group in potentialGroups.Values)
            {
                if (group.Count > 1)
                {
                    hasUngroupedStems = true;
                    break;
                }
            }

            groupedLoops = potentialGroups;

            Debug.Log($"Stem Analysis: Found {potentialGroups.Count} potential groups, ungrouped stems: {hasUngroupedStems}");
        }

        struct StemInfo
        {
            public bool isValid;
            public int trackNumber;
            public string trackName;
            public int loopNumber;
            public float startTime;
            public float duration;
            public string stemName;
        }

        StemInfo ParseStemFilename(string filename)
        {
            var info = new StemInfo { isValid = false };

            // Find "_loop_" separator
            int loopIndex = filename.IndexOf("_loop_");
            if (loopIndex < 0)
                return info;

            // Everything before "_loop_"
            string beforeLoop = filename.Substring(0, loopIndex);
            
            // Everything after "_loop_" should be: "NN_XX.XXs_XX.XXs_stemname"
            string afterLoop = filename.Substring(loopIndex + 6);
            
            // Parse after section
            var afterMatch = Regex.Match(afterLoop, @"^(\d+)_([\d.]+)s_([\d.]+)s_(.+)$");
            
            if (!afterMatch.Success)
                return info;
            
            info.loopNumber = int.Parse(afterMatch.Groups[1].Value);
            info.startTime = float.Parse(afterMatch.Groups[2].Value);
            info.duration = float.Parse(afterMatch.Groups[3].Value);
            info.stemName = afterMatch.Groups[4].Value;
            
            // Parse before section for track number and name
            var beforeMatch = Regex.Match(beforeLoop, @"^(\d+)\.?\s*(.*)$");
            
            if (beforeMatch.Success)
            {
                info.trackNumber = int.Parse(beforeMatch.Groups[1].Value);
                info.trackName = beforeMatch.Groups[2].Value.Trim();
                info.isValid = true;
            }
            else
            {
                info.trackNumber = 0;
                info.trackName = beforeLoop.Trim();
                info.isValid = true;
            }

            return info;
        }

        // ==================== AUTO-GROUP STEMS ====================

        void AutoGroupStems()
        {
            if (!hasUngroupedStems)
            {
                EditorUtility.DisplayDialog("Info", "No ungrouped stems detected.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Auto-Group Stems",
                $"This will group {groupedLoops.Values.Sum(g => g.Count)} individual clips into {groupedLoops.Count} multi-stem loops.\n\n" +
                "The original individual loops will be removed and replaced with grouped loops.\n\n" +
                "Continue?",
                "Group Stems",
                "Cancel"
            );

            if (!confirmed)
                return;

            Undo.RecordObject(track, "Auto-Group Stems");

            List<LoopData> newLoops = new List<LoopData>();
            HashSet<LoopData> processedLoops = new HashSet<LoopData>();

            // Create grouped loops
            foreach (var kvp in groupedLoops.OrderBy(k => k.Key))
            {
                int loopNumber = kvp.Key;
                var loopGroup = kvp.Value;

                if (loopGroup.Count < 2)
                {
                    // Single clip, keep as-is
                    if (loopGroup.Count == 1 && !processedLoops.Contains(loopGroup[0]))
                    {
                        newLoops.Add(loopGroup[0]);
                        processedLoops.Add(loopGroup[0]);
                    }
                    continue;
                }

                // Create multi-stem loop
                LoopData groupedLoop = new LoopData
                {
                    useStems = true,
                    stems = loopGroup.Select(l => l.clip).OrderBy(c => GetStemPriority(c.name)).ToList(),
                    clip = null,
                    bpm = loopGroup[0].bpm,
                    quality = loopGroup[0].quality,
                    intensity = loopGroup[0].intensity,
                    exitSyncPoints = loopGroup[0].exitSyncPoints != null ? new List<float>(loopGroup[0].exitSyncPoints) : new List<float>(),
                    tags = loopGroup[0].tags != null ? new List<string>(loopGroup[0].tags) : new List<string>()
                };

                newLoops.Add(groupedLoop);
                processedLoops.UnionWith(loopGroup);
            }

            // Add any remaining loops that weren't grouped
            foreach (var loop in track.loops)
            {
                if (!processedLoops.Contains(loop))
                {
                    newLoops.Add(loop);
                }
            }

            // Replace loops
            track.loops = newLoops;

            EditorUtility.SetDirty(track);
            AnalyzeStems();

            EditorUtility.DisplayDialog(
                "Success",
                $"Grouped stems successfully!\n\n" +
                $"Created {groupedLoops.Count} multi-stem loops.",
                "OK"
            );

            Debug.Log($"<color=green>✓ Auto-grouped stems: {processedLoops.Count} individual clips → {groupedLoops.Count} multi-stem loops</color>");
        }

        int GetStemPriority(string stemName)
        {
            stemName = stemName.ToLower();
            
            if (stemName.Contains("drum")) return 0;
            if (stemName.Contains("kick")) return 1;
            if (stemName.Contains("bass")) return 2;
            if (stemName.Contains("rhythm")) return 3;
            if (stemName.Contains("guitar")) return 4;
            if (stemName.Contains("keys") || stemName.Contains("piano")) return 5;
            if (stemName.Contains("lead")) return 6;
            if (stemName.Contains("vocal")) return 7;
            if (stemName.Contains("pad")) return 8;
            if (stemName.Contains("fx")) return 9;
            
            return 100;
        }

        // ==================== VALIDATION ====================

        void ValidateAllStems()
        {
            if (track.loops == null || track.loops.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No loops to validate.", "OK");
                return;
            }

            int issueCount = 0;
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("Stem Validation Report:\n");

            for (int i = 0; i < track.loops.Count; i++)
            {
                var loop = track.loops[i];

                if (!loop.useStems)
                    continue;

                if (loop.stems == null || loop.stems.Count == 0)
                {
                    report.AppendLine($"Loop {i}: useStems=true but no stems assigned!");
                    issueCount++;
                    continue;
                }

                // Check for null stems
                int nullCount = loop.stems.Count(s => s == null);
                if (nullCount > 0)
                {
                    report.AppendLine($"Loop {i}: {nullCount} null stem(s)!");
                    issueCount++;
                }

                // Check duration consistency
                if (loop.stems.Count > 1)
                {
                    float firstDuration = loop.stems[0]?.length ?? 0;
                    bool allSameDuration = loop.stems.All(s => s != null && Mathf.Approximately(s.length, firstDuration));

                    if (!allSameDuration)
                    {
                        report.AppendLine($"Loop {i}: Stems have different lengths! May cause desync.");
                        issueCount++;
                    }
                }
            }

            if (issueCount == 0)
            {
                report.AppendLine("✓ All stems validated successfully!");
            }
            else
            {
                report.AppendLine($"\n⚠ Found {issueCount} issue(s)!");
            }

            EditorUtility.DisplayDialog("Validation Results", report.ToString(), "OK");
            Debug.Log(report.ToString());
        }

        // ==================== MAGI OPERATIONS ====================

        void GenerateAllSyncPoints()
        {
            if (track.loops == null || track.loops.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No loops to process.", "OK");
                return;
            }

            Undo.RecordObject(track, "Generate All Sync Points");

            int count = 0;
            foreach (var loop in track.loops)
            {
                GenerateSyncPoints(loop);
                count++;
            }

            EditorUtility.SetDirty(track);

            EditorUtility.DisplayDialog("Success", $"Generated sync points for {count} loops.", "OK");
            Debug.Log($"<color=green>✓ Generated sync points for {count} loops</color>");
        }

        void GenerateSyncPoints(LoopData loop)
        {
            float duration = loop.Duration;
            if (duration <= 0)
                return;

            loop.exitSyncPoints = new List<float>();

            float bpm = loop.bpm > 0 ? loop.bpm : track.defaultBPM;
            float beatDuration = 60f / bpm;
            float barDuration = beatDuration * 4;

            float currentTime = barDuration;
            while (currentTime < duration)
            {
                loop.exitSyncPoints.Add(currentTime);
                currentTime += barDuration;
            }

            if (loop.exitSyncPoints.Count == 0 || loop.exitSyncPoints[loop.exitSyncPoints.Count - 1] != duration)
            {
                loop.exitSyncPoints.Add(duration);
            }
        }

        void ClearAllSyncPoints()
        {
            if (track.loops == null || track.loops.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No loops to process.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Sync Points",
                "Clear all sync points from all loops?",
                "Clear",
                "Cancel"
            );

            if (!confirmed)
                return;

            Undo.RecordObject(track, "Clear All Sync Points");

            foreach (var loop in track.loops)
            {
                loop.exitSyncPoints?.Clear();
            }

            EditorUtility.SetDirty(track);

            Debug.Log("Cleared all sync points");
        }

        // ==================== SORTING ====================

        void SortLoops()
        {
            if (track.loops == null || track.loops.Count == 0)
                return;

            Undo.RecordObject(track, "Sort Loops");

            switch (currentSortMode)
            {
                case SortMode.Quality:
                    track.loops = track.loops.OrderByDescending(l => l.quality).ToList();
                    break;
                case SortMode.Intensity:
                    track.loops = track.loops.OrderBy(l => l.intensity).ToList();
                    break;
                case SortMode.Duration:
                    track.loops = track.loops.OrderBy(l => l.Duration).ToList();
                    break;
                case SortMode.StemCount:
                    track.loops = track.loops.OrderByDescending(l => l.useStems ? l.stems?.Count ?? 0 : 0).ToList();
                    break;
            }

            EditorUtility.SetDirty(track);
        }

        // ==================== UTILITIES ====================

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
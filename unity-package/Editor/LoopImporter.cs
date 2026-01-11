using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdaptiveMusic.Editor
{
    /// <summary>
    /// Enhanced Loop Importer with intelligent stem detection and grouping.
    /// Parses loop_extractor output: "03 WWJOCD_loop_01_43.08s_18.55s_drums"
    /// Automatically groups stems by loop number and creates proper LoopData with stems.
    /// </summary>
    public class LoopImporter : EditorWindow
    {
        private TrackData targetTrack;
        private AudioClip[] selectedClips;
        private Vector2 scrollPosition;
        
        // Grouping data
        private Dictionary<int, List<StemClip>> groupedStems = new Dictionary<int, List<StemClip>>();
        private List<int> loopNumbers = new List<int>();
        
        // Import settings
        private bool autoDetectBPM = true;
        private bool autoGenerateSyncPoints = true;
        private bool autoAssignIntensity = true;
        private float defaultQuality = 0.85f;
        
        // Preview
        private bool showPreview = true;

        // ==================== NESTED CLASSES ====================
        
        private class StemClip
        {
            public AudioClip clip;
            public int trackNumber;      // e.g., 03
            public string trackName;     // e.g., "WWJOCD"
            public int loopNumber;       // e.g., 1, 2, 3
            public float startTime;      // e.g., 43.08
            public float duration;       // e.g., 18.55
            public string stemName;      // e.g., "drums", "bass", "guitar"
            public bool isValid;         // Successfully parsed
        }

        // ==================== MENU ITEM ====================
        
        [MenuItem("Tools/Adaptive Music/Loop Importer")]
        public static void ShowWindow()
        {
            GetWindow<LoopImporter>("Loop Importer");
        }

        // ==================== GUI ====================
        
        void OnGUI()
        {
            GUILayout.Label("Loop Importer with Stem Detection", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            // Target track
            EditorGUILayout.LabelField("Target Track", EditorStyles.boldLabel);
            targetTrack = (TrackData)EditorGUILayout.ObjectField("Track Data", targetTrack, typeof(TrackData), false);
            
            EditorGUILayout.Space();
            
            // Clip selection
            DrawClipSelection();
            
            EditorGUILayout.Space();
            
            // Import settings
            DrawImportSettings();
            
            EditorGUILayout.Space();
            
            // Preview
            if (showPreview && selectedClips != null && selectedClips.Length > 0)
            {
                DrawPreview();
            }
            
            EditorGUILayout.Space();
            
            // Import button
            GUI.enabled = targetTrack != null && selectedClips != null && selectedClips.Length > 0;
            
            if (GUILayout.Button("Import Loops with Stems", GUILayout.Height(40)))
            {
                ImportLoops();
            }
            
            GUI.enabled = true;
        }

        // ==================== CLIP SELECTION ====================
        
        void DrawClipSelection()
        {
            EditorGUILayout.LabelField("Audio Clips", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Select multiple AudioClips exported from loop_extractor.\n" +
                "Expected format: 'NN[.] TrackName_loop_NN_XX.XXs_XX.XXs_stemname'\n" +
                "Track names can contain spaces, quotes, and punctuation.\n" +
                "Examples:\n" +
                "  '03 WWJOCD_loop_01_43.08s_18.55s_drums'\n" +
                "  '08. Automobile Heaven 'Place of Cake'_loop_01_0.00s_28.99s_drums'",
                MessageType.Info
            );
            
            // Drag & drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag AudioClips Here");
            
            Event evt = Event.current;
            
            if (dropArea.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        List<AudioClip> clips = new List<AudioClip>();
                        foreach (Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is AudioClip clip)
                            {
                                clips.Add(clip);
                            }
                        }
                        
                        selectedClips = clips.ToArray();
                        AnalyzeClips();
                    }
                    evt.Use();
                }
            }
            
            // Show selected clips count
            if (selectedClips != null && selectedClips.Length > 0)
            {
                EditorGUILayout.LabelField($"Selected: {selectedClips.Length} clips", EditorStyles.miniLabel);
                
                if (GUILayout.Button("Clear Selection"))
                {
                    selectedClips = null;
                    groupedStems.Clear();
                    loopNumbers.Clear();
                }
            }
        }

        // ==================== IMPORT SETTINGS ====================
        
        void DrawImportSettings()
        {
            EditorGUILayout.LabelField("Import Settings", EditorStyles.boldLabel);
            
            autoDetectBPM = EditorGUILayout.Toggle("Auto-Detect BPM", autoDetectBPM);
            autoGenerateSyncPoints = EditorGUILayout.Toggle("Auto-Generate Sync Points", autoGenerateSyncPoints);
            autoAssignIntensity = EditorGUILayout.Toggle("Auto-Assign Intensity", autoAssignIntensity);
            defaultQuality = EditorGUILayout.Slider("Default Quality", defaultQuality, 0f, 1f);
            showPreview = EditorGUILayout.Toggle("Show Preview", showPreview);
        }

        // ==================== PREVIEW ====================
        
        void DrawPreview()
        {
            EditorGUILayout.LabelField("Import Preview", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                $"Found {loopNumbers.Count} grouped loops from {selectedClips.Length} clips",
                MessageType.Info
            );
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            
            foreach (int loopNum in loopNumbers)
            {
                var stems = groupedStems[loopNum];
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Loop header
                EditorGUILayout.LabelField($"Loop #{loopNum:D2}", EditorStyles.boldLabel);
                
                if (stems.Count > 0 && stems[0].isValid)
                {
                    EditorGUILayout.LabelField($"Track: {stems[0].trackName}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Duration: {stems[0].duration:F2}s", EditorStyles.miniLabel);
                }
                
                EditorGUILayout.LabelField($"Stems: {stems.Count}", EditorStyles.miniLabel);
                
                // List stems
                EditorGUI.indentLevel++;
                foreach (var stem in stems)
                {
                    string status = stem.isValid ? "✓" : "✗";
                    EditorGUILayout.LabelField($"{status} {stem.stemName} ({stem.clip.name})", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            
            EditorGUILayout.EndScrollView();
        }

        // ==================== FILENAME PARSING ====================
        
        /// <summary>
        /// Parse filename from loop_extractor format.
        /// Supports multiple formats:
        /// - "NN TrackName_loop_NN_XX.XXs_XX.XXs_stemname"
        /// - "NN. Track Name With Spaces_loop_NN_XX.XXs_XX.XXs_stemname"
        /// Example: "08. Automobile Heaven 'Place of Cake'_loop_01_0.00s_28.99s_drums"
        /// </summary>
        StemClip ParseStemClip(AudioClip clip)
        {
            if (clip == null)
                return null;

            string filename = clip.name;
            var stem = new StemClip { clip = clip, isValid = false };

            // Strategy: Find "_loop_" separator and work backwards/forwards from there
            // This handles track names with any characters (spaces, quotes, etc.)
            
            int loopIndex = filename.IndexOf("_loop_");
            if (loopIndex < 0)
            {
                // No "_loop_" found, treat as single clip
                Debug.LogWarning($"Could not parse filename (no '_loop_' separator): {filename}");
                stem.stemName = filename;
                stem.loopNumber = 0;
                stem.trackName = "Unknown";
                stem.isValid = false;
                return stem;
            }

            // Everything before "_loop_" is: "trackNum. trackName" or "trackNum trackName"
            string beforeLoop = filename.Substring(0, loopIndex);
            
            // Everything after "_loop_" should match: "NN_XX.XXs_XX.XXs_stemname"
            string afterLoop = filename.Substring(loopIndex + 6); // Skip "_loop_"
            
            // Parse the "after loop" part
            var afterMatch = Regex.Match(afterLoop, @"^(\d+)_([\d.]+)s_([\d.]+)s_(.+)$");
            
            if (!afterMatch.Success)
            {
                Debug.LogWarning($"Could not parse after '_loop_' section: {afterLoop}");
                stem.stemName = filename;
                stem.loopNumber = 0;
                stem.trackName = "Unknown";
                stem.isValid = false;
                return stem;
            }
            
            // Extract loop number, times, stem name
            stem.loopNumber = int.Parse(afterMatch.Groups[1].Value);
            stem.startTime = float.Parse(afterMatch.Groups[2].Value);
            stem.duration = float.Parse(afterMatch.Groups[3].Value);
            stem.stemName = afterMatch.Groups[4].Value;
            
            // Parse the "before loop" part for track number and name
            // Handle formats: "NN trackname" or "NN. trackname"
            var beforeMatch = Regex.Match(beforeLoop, @"^(\d+)\.?\s*(.*)$");
            
            if (beforeMatch.Success)
            {
                stem.trackNumber = int.Parse(beforeMatch.Groups[1].Value);
                stem.trackName = beforeMatch.Groups[2].Value.Trim();
                stem.isValid = true;
            }
            else
            {
                // Just use the whole thing as track name
                stem.trackNumber = 0;
                stem.trackName = beforeLoop.Trim();
                stem.isValid = true;
            }

            return stem;
        }

        // ==================== CLIP ANALYSIS ====================
        
        void AnalyzeClips()
        {
            groupedStems.Clear();
            loopNumbers.Clear();

            if (selectedClips == null || selectedClips.Length == 0)
                return;

            // Parse all clips
            foreach (var clip in selectedClips)
            {
                var stem = ParseStemClip(clip);
                if (stem == null)
                    continue;

                if (!groupedStems.ContainsKey(stem.loopNumber))
                {
                    groupedStems[stem.loopNumber] = new List<StemClip>();
                }

                groupedStems[stem.loopNumber].Add(stem);
            }

            // Sort loop numbers
            loopNumbers = groupedStems.Keys.OrderBy(k => k).ToList();

            // Sort stems within each loop by priority
            foreach (var loopNum in loopNumbers)
            {
                groupedStems[loopNum] = groupedStems[loopNum]
                    .OrderBy(s => GetStemPriority(s.stemName))
                    .ToList();
            }

            Debug.Log($"Analyzed {selectedClips.Length} clips into {loopNumbers.Count} grouped loops");
        }

        /// <summary>
        /// Determine display/playback order for stems
        /// </summary>
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
            if (stemName.Contains("vocal") || stemName.Contains("vox")) return 7;
            if (stemName.Contains("pad")) return 8;
            if (stemName.Contains("synth")) return 9;
            if (stemName.Contains("fx") || stemName.Contains("effect")) return 10;
            
            return 100; // Unknown stems at the end
        }

        // ==================== IMPORT ====================
        
        void ImportLoops()
        {
            if (targetTrack == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target TrackData", "OK");
                return;
            }

            if (selectedClips == null || selectedClips.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Please select audio clips to import", "OK");
                return;
            }

            // Confirm import
            int loopCount = loopNumbers.Count;
            int stemCount = selectedClips.Length;
            
            bool confirmed = EditorUtility.DisplayDialog(
                "Import Loops with Stems",
                $"Import {loopCount} loops ({stemCount} total stems) into '{targetTrack.displayName}'?\n\n" +
                "This will add new LoopData entries with stem configuration.",
                "Import",
                "Cancel"
            );

            if (!confirmed)
                return;

            // Perform import
            Undo.RecordObject(targetTrack, "Import Loops with Stems");

            int importedCount = 0;
            
            foreach (int loopNum in loopNumbers)
            {
                var stems = groupedStems[loopNum];
                
                if (stems.Count == 0)
                    continue;

                // Create LoopData
                LoopData loopData = CreateLoopData(loopNum, stems);
                
                if (loopData != null)
                {
                    targetTrack.loops.Add(loopData);
                    importedCount++;
                }
            }

            EditorUtility.SetDirty(targetTrack);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Import Complete",
                $"Successfully imported {importedCount} loops with stems into '{targetTrack.displayName}'",
                "OK"
            );

            Debug.Log($"<color=green>✓ Imported {importedCount} loops ({selectedClips.Length} stems) into {targetTrack.displayName}</color>");
        }

        // ==================== LOOP CREATION ====================
        
        LoopData CreateLoopData(int loopNumber, List<StemClip> stems)
        {
            if (stems == null || stems.Count == 0)
                return null;

            var firstStem = stems[0];
            
            // Determine if this is a multi-stem loop or single clip
            bool hasSingleStem = stems.Count == 1;
            bool hasMultipleStems = stems.Count > 1;

            LoopData loop = new LoopData();

            if (hasSingleStem)
            {
                // Single clip mode
                loop.clip = firstStem.clip;
                loop.useStems = false;
                loop.stems = new List<AudioClip>();
            }
            else
            {
                // Multi-stem mode
                loop.clip = null; // No single clip
                loop.useStems = true;
                loop.stems = stems.Select(s => s.clip).ToList();
            }

            // Set BPM
            if (autoDetectBPM && targetTrack != null)
            {
                loop.bpm = targetTrack.defaultBPM;
            }
            else
            {
                loop.bpm = 120f; // Default
            }

            // Set quality
            loop.quality = defaultQuality;

            // Set intensity (based on loop number)
            if (autoAssignIntensity && loopNumbers.Count > 1)
            {
                int index = loopNumbers.IndexOf(loopNumber);
                loop.intensity = (float)index / (loopNumbers.Count - 1);
            }
            else
            {
                loop.intensity = 0.5f;
            }

            // Generate sync points
            if (autoGenerateSyncPoints)
            {
                GenerateSyncPoints(loop, firstStem.duration);
            }
            else
            {
                loop.exitSyncPoints = new List<float>();
            }

            // Tags
            loop.tags = new List<string>();
            
            if (loop.intensity < 0.3f)
                loop.tags.Add("calm");
            else if (loop.intensity < 0.7f)
                loop.tags.Add("medium");
            else
                loop.tags.Add("intense");

            return loop;
        }

        // ==================== SYNC POINT GENERATION ====================
        
        void GenerateSyncPoints(LoopData loop, float duration)
        {
            loop.exitSyncPoints = new List<float>();

            if (duration <= 0)
            {
                Debug.LogWarning("Invalid duration for sync point generation");
                return;
            }

            float bpm = loop.bpm;
            float beatDuration = 60f / bpm;
            float barDuration = beatDuration * 4; // 4/4 time signature

            // Generate sync points at bar boundaries
            float currentTime = barDuration;
            
            while (currentTime < duration)
            {
                loop.exitSyncPoints.Add(currentTime);
                currentTime += barDuration;
            }

            // Always add end point
            if (loop.exitSyncPoints.Count == 0 || loop.exitSyncPoints[loop.exitSyncPoints.Count - 1] != duration)
            {
                loop.exitSyncPoints.Add(duration);
            }

            Debug.Log($"Generated {loop.exitSyncPoints.Count} sync points for loop (duration: {duration:F2}s, BPM: {bpm})");
        }

        // ==================== VALIDATION ====================
        
        void ValidateStems(List<StemClip> stems)
        {
            if (stems == null || stems.Count == 0)
                return;

            // Check all stems have same duration
            float firstDuration = stems[0].duration;
            bool allSameDuration = stems.All(s => Mathf.Approximately(s.duration, firstDuration));

            if (!allSameDuration)
            {
                Debug.LogWarning($"Stems in loop have different durations! This may cause sync issues.");
            }

            // Check all stems are from same track
            string firstTrack = stems[0].trackName;
            bool allSameTrack = stems.All(s => s.trackName == firstTrack);

            if (!allSameTrack)
            {
                Debug.LogWarning($"Stems in loop are from different tracks! This may be incorrect.");
            }
        }
    }
}
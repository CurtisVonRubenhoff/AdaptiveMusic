using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdaptiveMusic.Editor
{
    [CustomEditor(typeof(TrackData))]
    public class TrackDataEditor : UnityEditor.Editor
    {
        private SerializedProperty keyProperty;
        private SerializedProperty displayNameProperty;
        private SerializedProperty bpmProperty;
        private SerializedProperty loopsProperty;
        private SerializedProperty defaultCrossfadeProperty;
        private SerializedProperty tagsProperty;

        private List<double> tapTimes = new List<double>();
        private AudioSource previewSource;
        private bool isPlaying = false;
        private int playingLoopIndex = -1;
        private double lastTapTime = 0;
        private const int minTaps = 4;
        private const int maxTaps = 16;
        private Vector2 loopsScrollPosition;
        private bool showBPMTools = true;
        private bool showLoopsList = true;

        void OnEnable()
        {
            keyProperty = serializedObject.FindProperty("key");
            displayNameProperty = serializedObject.FindProperty("displayName");
            bpmProperty = serializedObject.FindProperty("bpm");
            loopsProperty = serializedObject.FindProperty("loops");
            defaultCrossfadeProperty = serializedObject.FindProperty("defaultCrossfade");
            tagsProperty = serializedObject.FindProperty("tags");

            // Create preview audio source
            GameObject previewObj = GameObject.Find("_TrackDataPreview");
            if (previewObj == null)
            {
                previewObj = new GameObject("_TrackDataPreview");
                previewObj.hideFlags = HideFlags.HideAndDontSave;
                previewSource = previewObj.AddComponent<AudioSource>();
                previewSource.playOnAwake = false;
            }
            else
            {
                previewSource = previewObj.GetComponent<AudioSource>();
            }
        }

        void OnDisable()
        {
            StopPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            TrackData track = (TrackData)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Track Data", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Basic Info
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Track Information", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(keyProperty, new GUIContent("Key", "Unique identifier for this track"));
            EditorGUILayout.PropertyField(displayNameProperty, new GUIContent("Display Name", "Human-readable name"));
            EditorGUILayout.PropertyField(tagsProperty, new GUIContent("Tags", "Tags for organization (action, ambient, menu, etc.)"));
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // BPM and Timing
            EditorGUILayout.BeginVertical("box");
            showBPMTools = EditorGUILayout.Foldout(showBPMTools, "BPM & Timing", true, EditorStyles.foldoutHeader);
            
            if (showBPMTools)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(bpmProperty, new GUIContent("BPM", "Beats per minute"));
                EditorGUILayout.PropertyField(defaultCrossfadeProperty, new GUIContent("Default Crossfade", "Default crossfade duration in seconds"));

                EditorGUILayout.Space(5);

                // Tap Tempo
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.LabelField("Tap Tempo Tool", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Play a loop and tap the button on each beat to detect BPM.", MessageType.Info);

                // Play button for first loop
                if (track.loops.Count > 0 && track.loops[0] != null && track.loops[0].clip != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    bool isPreviewPlaying = isPlaying && playingLoopIndex == 0;
                    string playButtonLabel = isPreviewPlaying ? "⬛ Stop Preview" : "▶ Play First Loop";
                    
                    if (GUILayout.Button(playButtonLabel, GUILayout.Height(30)))
                    {
                        if (isPreviewPlaying)
                            StopPreview();
                        else
                            PlayPreview(track.loops[0].clip, 0);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(3);

                // Tap button
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("TAP ON BEAT", GUILayout.Height(40)))
                {
                    RecordTap();
                }
                GUI.backgroundColor = Color.white;

                // Display tap info
                if (tapTimes.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField($"Taps recorded: {tapTimes.Count} / {maxTaps}", EditorStyles.miniLabel);

                    if (tapTimes.Count >= minTaps)
                    {
                        float calculatedBPM = CalculateBPM();
                        EditorGUILayout.LabelField($"Detected BPM: {calculatedBPM:F1}", EditorStyles.boldLabel);

                        EditorGUILayout.BeginHorizontal();
                        GUI.backgroundColor = Color.green;
                        if (GUILayout.Button("✓ Apply BPM", GUILayout.Height(30)))
                        {
                            bpmProperty.floatValue = calculatedBPM;
                            serializedObject.ApplyModifiedProperties();
                            ResetTaps();
                            EditorUtility.DisplayDialog("BPM Applied", $"BPM set to {calculatedBPM:F1}", "OK");
                        }
                        GUI.backgroundColor = Color.white;

                        if (GUILayout.Button("Reset", GUILayout.Height(30), GUILayout.Width(80)))
                        {
                            ResetTaps();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"Tap {minTaps - tapTimes.Count} more time(s) to calculate BPM", MessageType.Info);
                    }
                }

                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Loops
            EditorGUILayout.BeginVertical("box");
            showLoopsList = EditorGUILayout.Foldout(showLoopsList, $"Loops ({track.loops.Count})", true, EditorStyles.foldoutHeader);
            
            if (showLoopsList)
            {
                EditorGUI.indentLevel++;

                if (track.loops.Count == 0)
                {
                    EditorGUILayout.HelpBox("No loops added yet. Use Tools > Adaptive Music > Loop Importer to import extracted loops.", MessageType.Info);
                }
                else
                {
                    // Statistics
                    float avgQuality = track.loops.Average(l => l.quality);
                    float totalDuration = track.loops.Sum(l => l.duration);
                    LoopData bestLoop = track.GetBestQualityLoop();

                    EditorGUILayout.BeginVertical("helpbox");
                    EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Duration: {totalDuration:F1}s", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Average Quality: {avgQuality:F2}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Best Quality: {bestLoop.quality:F2} (Loop {bestLoop.index + 1})", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space(5);

                    // Loop list with scroll
                    loopsScrollPosition = EditorGUILayout.BeginScrollView(loopsScrollPosition, GUILayout.MaxHeight(300));

                    for (int i = 0; i < track.loops.Count; i++)
                    {
                        LoopData loop = track.loops[i];
                        if (loop == null) continue;

                        EditorGUILayout.BeginHorizontal("box");

                        // Loop info
                        EditorGUILayout.BeginVertical(GUILayout.Width(80));
                        EditorGUILayout.LabelField($"Loop {loop.index + 1}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"{loop.duration:F1}s", EditorStyles.miniLabel);
                        EditorGUILayout.EndVertical();

                        // Quality indicator
                        EditorGUILayout.BeginVertical(GUILayout.Width(60));
                        Color qualityColor = GetQualityColor(loop.quality);
                        GUI.contentColor = qualityColor;
                        EditorGUILayout.LabelField("Quality", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"{loop.quality:F2}", EditorStyles.boldLabel);
                        GUI.contentColor = Color.white;
                        EditorGUILayout.EndVertical();

                        // Timing info
                        EditorGUILayout.BeginVertical(GUILayout.Width(100));
                        EditorGUILayout.LabelField($"Start: {loop.startTime:F1}s", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Fade: {loop.recommendedCrossfade:F2}s", EditorStyles.miniLabel);
                        EditorGUILayout.EndVertical();

                        // Intensity
                        EditorGUILayout.BeginVertical(GUILayout.Width(80));
                        EditorGUILayout.LabelField("Intensity", EditorStyles.miniLabel);
                        loop.intensity = EditorGUILayout.Slider(loop.intensity, 0f, 1f);
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.FlexibleSpace();

                        // Play button
                        bool isThisLoopPlaying = isPlaying && playingLoopIndex == i;
                        string playLabel = isThisLoopPlaying ? "⬛" : "▶";
                        if (GUILayout.Button(playLabel, GUILayout.Width(30), GUILayout.Height(40)))
                        {
                            if (isThisLoopPlaying)
                                StopPreview();
                            else
                                PlayPreview(loop.clip, i);
                        }

                        EditorGUILayout.EndHorizontal();
                        
                        // Tags for this loop
                        if (loop.tags != null && loop.tags.Length > 0)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("Tags:", EditorStyles.miniLabel, GUILayout.Width(40));
                            EditorGUILayout.LabelField(string.Join(", ", loop.tags), EditorStyles.miniLabel);
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    EditorGUILayout.EndScrollView();
                }

                EditorGUILayout.Space(5);

                // Import button
                if (GUILayout.Button("Open Loop Importer", GUILayout.Height(30)))
                {
                    LoopImporter.ShowWindow();
                }

                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        Color GetQualityColor(float quality)
        {
            if (quality >= 0.8f)
                return new Color(0.2f, 1f, 0.2f); // Green
            else if (quality >= 0.6f)
                return new Color(1f, 0.8f, 0f); // Yellow
            else if (quality >= 0.4f)
                return new Color(1f, 0.5f, 0f); // Orange
            else
                return new Color(1f, 0.2f, 0.2f); // Red
        }

        void PlayPreview(AudioClip clip, int index)
        {
            if (previewSource == null || clip == null) return;

            StopPreview();
            playingLoopIndex = index;
            previewSource.clip = clip;
            previewSource.loop = true;
            previewSource.Play();
            isPlaying = true;

            Repaint();
        }

        void StopPreview()
        {
            if (previewSource != null && previewSource.isPlaying)
            {
                previewSource.Stop();
            }

            isPlaying = false;
            playingLoopIndex = -1;
            Repaint();
        }

        void RecordTap()
        {
            double currentTime = EditorApplication.timeSinceStartup;

            // If it's been more than 3 seconds since last tap, reset
            if (tapTimes.Count > 0 && currentTime - lastTapTime > 3.0)
            {
                ResetTaps();
            }

            tapTimes.Add(currentTime);
            lastTapTime = currentTime;

            // Limit number of taps
            if (tapTimes.Count > maxTaps)
            {
                tapTimes.RemoveAt(0);
            }

            Repaint();
        }

        float CalculateBPM()
        {
            if (tapTimes.Count < 2) return 0f;

            // Calculate intervals between taps
            List<double> intervals = new List<double>();
            for (int i = 1; i < tapTimes.Count; i++)
            {
                intervals.Add(tapTimes[i] - tapTimes[i - 1]);
            }

            // Average interval
            double avgInterval = intervals.Average();

            // Convert to BPM (60 seconds / average interval)
            float bpm = (float)(60.0 / avgInterval);

            // Round to nearest 0.5
            bpm = Mathf.Round(bpm * 2f) / 2f;

            return bpm;
        }

        void ResetTaps()
        {
            tapTimes.Clear();
            lastTapTime = 0;
            Repaint();
        }
    }
}

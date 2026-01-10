using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdaptiveMusic.Editor
{
    [CustomEditor(typeof(MusicDatabase))]
    public class MusicDatabaseEditor : UnityEditor.Editor
    {
        private SerializedProperty tracksProperty;
        private int selectedTrackIndex = -1;
        private List<double> tapTimes = new List<double>();
        private AudioSource previewSource;
        private bool isPlaying = false;
        private double lastTapTime = 0;
        private const int minTaps = 4;
        private const int maxTaps = 16;
        private Vector2 scrollPosition;

        void OnEnable()
        {
            tracksProperty = serializedObject.FindProperty("tracks");

            // Create a temporary GameObject with AudioSource for preview
            GameObject previewObj = GameObject.Find("_MusicDatabasePreview");
            if (previewObj == null)
            {
                previewObj = new GameObject("_MusicDatabasePreview");
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Music Database", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This database manages all your music tracks. Each track can contain multiple loops extracted by the Audio Loop Extractor tool.", 
                MessageType.Info
            );
            EditorGUILayout.Space();

            // Statistics
            MusicDatabase db = (MusicDatabase)target;
            int totalLoops = 0;
            foreach (var track in db.tracks)
            {
                if (track != null)
                    totalLoops += track.loops.Count;
            }
            
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField($"Tracks: {db.tracks.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Total Loops: {totalLoops}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();

            // Add new track button
            if (GUILayout.Button("Add Track Reference", GUILayout.Height(30)))
            {
                tracksProperty.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }

            if (GUILayout.Button("Create New Track Asset", GUILayout.Height(30)))
            {
                CreateNewTrack();
            }

            EditorGUILayout.Space();

            // Scroll view for tracks
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Display all tracks
            for (int i = 0; i < tracksProperty.arraySize; i++)
            {
                SerializedProperty trackProp = tracksProperty.GetArrayElementAtIndex(i);
                
                if (trackProp.objectReferenceValue == null)
                {
                    // Empty slot
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(trackProp, new GUIContent($"Track {i + 1}"));
                    if (GUILayout.Button("×", GUILayout.Width(25)))
                    {
                        tracksProperty.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        continue;
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }

                TrackData track = trackProp.objectReferenceValue as TrackData;
                if (track == null) continue;

                EditorGUILayout.BeginVertical("box");

                // Header with delete button
                EditorGUILayout.BeginHorizontal();
                bool foldout = EditorGUILayout.Foldout(true, $"🎵 {track.displayName} ({track.loops.Count} loops)", true, EditorStyles.foldoutHeader);
                
                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                {
                    Selection.activeObject = track;
                    EditorGUIUtility.PingObject(track);
                }
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    tracksProperty.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    if (selectedTrackIndex == i)
                    {
                        selectedTrackIndex = -1;
                        StopPreview();
                    }
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (foldout)
                {
                    EditorGUI.indentLevel++;
                    
                    // Track info
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Key:", GUILayout.Width(80));
                    EditorGUILayout.LabelField(track.key);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("BPM:", GUILayout.Width(80));
                    EditorGUILayout.LabelField(track.bpm.ToString("F1"));
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Crossfade:", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{track.defaultCrossfade:F2}s");
                    EditorGUILayout.EndHorizontal();

                    // Loop preview
                    if (track.loops.Count > 0)
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField($"Loops ({track.loops.Count}):", EditorStyles.boldLabel);
                        
                        // Show first few loops
                        int displayCount = Mathf.Min(3, track.loops.Count);
                        for (int l = 0; l < displayCount; l++)
                        {
                            LoopData loop = track.loops[l];
                            if (loop != null && loop.clip != null)
                            {
                                EditorGUILayout.BeginHorizontal("helpbox");
                                EditorGUILayout.LabelField($"Loop {loop.index + 1}", GUILayout.Width(60));
                                EditorGUILayout.LabelField($"{loop.duration:F1}s", GUILayout.Width(50));
                                EditorGUILayout.LabelField($"Q: {loop.quality:F2}", GUILayout.Width(60));
                                
                                bool isThisLoopPlaying = isPlaying && selectedTrackIndex == i;
                                string playLabel = isThisLoopPlaying ? "⬛" : "▶";
                                if (GUILayout.Button(playLabel, GUILayout.Width(30)))
                                {
                                    if (isThisLoopPlaying)
                                        StopPreview();
                                    else
                                        PlayPreview(loop.clip, i);
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                        
                        if (track.loops.Count > displayCount)
                        {
                            EditorGUILayout.LabelField($"... and {track.loops.Count - displayCount} more", EditorStyles.miniLabel);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No loops. Use Tools > Adaptive Music > Loop Importer to add loops.", MessageType.Info);
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();

            serializedObject.ApplyModifiedProperties();
        }

        void CreateNewTrack()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Track",
                "NewTrack",
                "asset",
                "Choose a location to save the TrackData"
            );

            if (!string.IsNullOrEmpty(path))
            {
                TrackData newTrack = CreateInstance<TrackData>();
                newTrack.key = System.IO.Path.GetFileNameWithoutExtension(path);
                newTrack.displayName = newTrack.key;
                newTrack.bpm = 120f;
                newTrack.defaultCrossfade = 0.1f;

                AssetDatabase.CreateAsset(newTrack, path);
                AssetDatabase.SaveAssets();

                // Add to database
                tracksProperty.arraySize++;
                tracksProperty.GetArrayElementAtIndex(tracksProperty.arraySize - 1).objectReferenceValue = newTrack;
                serializedObject.ApplyModifiedProperties();

                Selection.activeObject = newTrack;
                EditorGUIUtility.PingObject(newTrack);
            }
        }

        void PlayPreview(AudioClip clip, int index)
        {
            if (previewSource == null) return;

            StopPreview();
            selectedTrackIndex = index;
            previewSource.clip = clip;
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
            Repaint();
        }
    }
}

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdaptiveMusic.Editor
{
    /// <summary>
    /// Designer-friendly jukebox with intelligent stem grouping.
    /// Parses filenames like "03 WWJOCD_loop_01_43.08s_18.55s_drums" to group stems by loop number.
    /// </summary>
    public class MusicJukeboxEditor : EditorWindow
    {
        // ==================== STATE ====================
        private MusicDatabase database;
        private TrackData selectedTrack;
        private int selectedLoopIndex = -1;
        private MusicController musicController;
        
        // Grouped loops (key = loop number, value = list of stems)
        private Dictionary<int, List<LoopStem>> groupedLoops = new Dictionary<int, List<LoopStem>>();
        private List<int> loopNumbers = new List<int>();
        
        // UI State
        private Vector2 trackScrollPos;
        private Vector2 loopScrollPos;
        private Vector2 stemMixerScrollPos;
        private bool isPlaying = false;
        private float masterVolume = 1f;
        
        // Stem visualization
        private Dictionary<int, float> stemVolumes = new Dictionary<int, float>();
        private Dictionary<int, bool> stemMutes = new Dictionary<int, bool>();
        private Dictionary<int, bool> stemSolos = new Dictionary<int, bool>();
        
        // Colors & Style
        private Color primaryColor = new Color(0.2f, 0.7f, 0.95f);
        private Color accentColor = new Color(1f, 0.4f, 0.6f);
        private Color darkBg = new Color(0.12f, 0.12f, 0.15f);
        private Color cardBg = new Color(0.18f, 0.18f, 0.22f);
        private Color mutedText = new Color(0.6f, 0.6f, 0.65f);
        private Color activeGreen = new Color(0.3f, 0.85f, 0.5f);
        private Color waveformColor = new Color(0.4f, 0.95f, 0.7f);
        
        // Animation
        private double lastUpdateTime;

        // ==================== NESTED CLASSES ====================
        
        private class LoopStem
        {
            public AudioClip clip;
            public string stemName;      // e.g., "drums", "bass", "guitar"
            public int loopNumber;       // e.g., 1, 2, 3
            public string trackName;     // e.g., "WWJOCD"
            public float startTime;      // e.g., 43.08s
            public float duration;       // e.g., 18.55s
            public int trackNumber;      // e.g., 03
        }

        // ==================== MENU ITEM ====================
        [MenuItem("Tools/Adaptive Music/Music Jukebox 🎵")]
        public static void ShowWindow()
        {
            var window = GetWindow<MusicJukeboxEditor>("Music Jukebox");
            window.minSize = new Vector2(900, 600);
        }

        // ==================== LIFECYCLE ====================
        private void OnEnable()
        {
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            
            if (musicController == null && Application.isPlaying)
            {
                musicController = MusicController.Instance;
                if (musicController != null)
                {
                    musicController.OnLoopChanged += OnLoopChanged;
                    musicController.OnStemVolumeChanged += OnStemVolumeChanged;
                }
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            
            if (musicController != null)
            {
                musicController.OnLoopChanged -= OnLoopChanged;
                musicController.OnStemVolumeChanged -= OnStemVolumeChanged;
            }
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - lastUpdateTime > 0.033f)
            {
                Repaint();
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }
        }

        // ==================== FILENAME PARSING ====================

        /// <summary>
        /// Parse filename: "NN. Track Name With Spaces_loop_NN_XX.XXs_XX.XXs_stemname"
        /// Handles track names with spaces, quotes, commas, etc.
        /// </summary>
        private LoopStem ParseLoopStem(AudioClip clip)
        {
            if (clip == null) return null;

            string filename = clip.name;
            var stem = new LoopStem { clip = clip };

            // Find "_loop_" separator
            int loopIndex = filename.IndexOf("_loop_");
            if (loopIndex < 0)
            {
                // Fallback parsing
                stem.stemName = clip.name;
                stem.loopNumber = 0;
                stem.trackName = "Unknown";
                return stem;
            }

            // Everything before "_loop_"
            string beforeLoop = filename.Substring(0, loopIndex);
            
            // Everything after "_loop_" should be: "NN_XX.XXs_XX.XXs_stemname"
            string afterLoop = filename.Substring(loopIndex + 6);
            
            // Parse after section
            var afterMatch = Regex.Match(afterLoop, @"^(\d+)_([\d.]+)s_([\d.]+)s_(.+)$");
            
            if (afterMatch.Success)
            {
                stem.loopNumber = int.Parse(afterMatch.Groups[1].Value);
                stem.startTime = float.Parse(afterMatch.Groups[2].Value);
                stem.duration = float.Parse(afterMatch.Groups[3].Value);
                stem.stemName = afterMatch.Groups[4].Value;
            }
            else
            {
                stem.stemName = filename;
                stem.loopNumber = 0;
                stem.trackName = "Unknown";
                return stem;
            }
            
            // Parse before section for track number and name
            var beforeMatch = Regex.Match(beforeLoop, @"^(\d+)\.?\s*(.*)$");
            
            if (beforeMatch.Success)
            {
                stem.trackNumber = int.Parse(beforeMatch.Groups[1].Value);
                stem.trackName = beforeMatch.Groups[2].Value.Trim();
            }
            else
            {
                stem.trackNumber = 0;
                stem.trackName = beforeLoop.Trim();
            }

            return stem;
        }

        /// <summary>
        /// Group all loops - if they already have stems, use those. 
        /// Otherwise try to parse clip names.
        /// </summary>
        private void GroupLoopsByNumber(TrackData track)
        {
            groupedLoops.Clear();
            loopNumbers.Clear();

            if (track?.loops == null) return;

            // Check if loops are already properly organized
            bool hasMultiStemLoops = track.loops.Any(l => l.useStems && l.stems != null && l.stems.Count > 0);

            if (hasMultiStemLoops)
            {
                // Loops are already organized with stems - just create fake groups for display
                for (int i = 0; i < track.loops.Count; i++)
                {
                    var loop = track.loops[i];
                    
                    // Create a fake group for this loop (use 0-based index)
                    List<LoopStem> stemList = new List<LoopStem>();
                    
                    if (loop.useStems && loop.stems != null)
                    {
                        // Multi-stem loop
                        foreach (var stemClip in loop.stems)
                        {
                            if (stemClip != null)
                            {
                                stemList.Add(new LoopStem
                                {
                                    clip = stemClip,
                                    stemName = stemClip.name,
                                    loopNumber = i, // Use 0-based index
                                    trackName = track.displayName,
                                    duration = stemClip.length,
                                    startTime = 0f,
                                    trackNumber = 0
                                });
                            }
                        }
                    }
                    else if (loop.clip != null)
                    {
                        // Single clip loop
                        stemList.Add(new LoopStem
                        {
                            clip = loop.clip,
                            stemName = "Main",
                            loopNumber = i, // Use 0-based index
                            trackName = track.displayName,
                            duration = loop.clip.length,
                            startTime = 0f,
                            trackNumber = 0
                        });
                    }
                    
                    if (stemList.Count > 0)
                    {
                        groupedLoops[i] = stemList; // Use 0-based index as key
                    }
                }
            }
            else
            {
                // Try to parse clip names for grouping
                foreach (var loop in track.loops)
                {
                    if (loop?.clip == null) continue;

                    var stem = ParseLoopStem(loop.clip);
                    if (stem == null) continue;

                    if (!groupedLoops.ContainsKey(stem.loopNumber))
                    {
                        groupedLoops[stem.loopNumber] = new List<LoopStem>();
                    }

                    groupedLoops[stem.loopNumber].Add(stem);
                }

                // Sort stems within each loop
                foreach (var loopNum in groupedLoops.Keys.ToList())
                {
                    groupedLoops[loopNum] = groupedLoops[loopNum]
                        .OrderBy(s => GetStemPriority(s.stemName))
                        .ToList();
                }
            }

            // Sort loop numbers
            loopNumbers = groupedLoops.Keys.OrderBy(k => k).ToList();

            Debug.Log($"Displaying {loopNumbers.Count} loops from track '{track.displayName}'");
        }

        /// <summary>
        /// Determine display order for stems (drums first, etc.)
        /// </summary>
        private int GetStemPriority(string stemName)
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
            if (stemName.Contains("fx") || stemName.Contains("effect")) return 9;
            
            return 100; // Unknown stems at the end
        }

        // ==================== GUI ====================
        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                DrawPlayModeWarning();
                return;
            }

            if (musicController == null)
            {
                musicController = MusicController.Instance;
                if (musicController == null)
                {
                    DrawControllerMissing();
                    return;
                }
            }

            // Handle keyboard shortcuts
            HandleKeyboardInput();

            DrawHeader();
            DrawBody();
            DrawFooter();
        }

        private void HandleKeyboardInput()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            // Number keys 1-9: Quick-switch to loops
            if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9)
            {
                int loopIndex = e.keyCode - KeyCode.Alpha1; // 0-8
                if (loopIndex < loopNumbers.Count)
                {
                    int loopNum = loopNumbers[loopIndex];
                    PlayGroupedLoop(loopIndex, groupedLoops[loopNum]);
                    e.Use();
                }
            }
            // Space: Play/Pause current loop
            else if (e.keyCode == KeyCode.Space)
            {
                if (isPlaying)
                {
                    StopPlayback();
                }
                else if (selectedLoopIndex >= 0 && selectedLoopIndex < loopNumbers.Count)
                {
                    int loopNum = loopNumbers[selectedLoopIndex];
                    PlayGroupedLoop(selectedLoopIndex, groupedLoops[loopNum]);
                }
                e.Use();
            }
            // Up/Down arrows: Navigate loops
            else if (e.keyCode == KeyCode.UpArrow)
            {
                if (selectedLoopIndex > 0)
                {
                    int newIndex = selectedLoopIndex - 1;
                    int loopNum = loopNumbers[newIndex];
                    PlayGroupedLoop(newIndex, groupedLoops[loopNum]);
                }
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                if (selectedLoopIndex < loopNumbers.Count - 1)
                {
                    int newIndex = selectedLoopIndex + 1;
                    int loopNum = loopNumbers[newIndex];
                    PlayGroupedLoop(newIndex, groupedLoops[loopNum]);
                }
                e.Use();
            }
        }

        // ==================== HEADER ====================
        private void DrawHeader()
        {
            Rect headerRect = new Rect(0, 0, position.width, 80);
            DrawGradientRect(headerRect, new Color(0.15f, 0.15f, 0.2f), darkBg);

            GUILayout.BeginArea(headerRect);
            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 24;
            titleStyle.normal.textColor = Color.white;
            GUILayout.Label("🎵 Music Jukebox", titleStyle);

            GUILayout.FlexibleSpace();

            GUILayout.Label("Database:", EditorStyles.label);
            EditorGUI.BeginChangeCheck();
            database = (MusicDatabase)EditorGUILayout.ObjectField(database, typeof(MusicDatabase), false, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                selectedTrack = null;
                selectedLoopIndex = -1;
            }

            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            // Status bar
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);

            if (database != null)
            {
                GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
                statusStyle.normal.textColor = mutedText;
                statusStyle.fontSize = 11;
                
                int totalTracks = database.tracks?.Count ?? 0;
                GUILayout.Label($"📀 {totalTracks} tracks  •  🎛️ {loopNumbers.Count} grouped loops", statusStyle);
                
                if (isPlaying && selectedTrack != null)
                {
                    statusStyle.normal.textColor = activeGreen;
                    GUILayout.Label($"  •  ▶ Now Playing: {selectedTrack.displayName}", statusStyle);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // ==================== BODY ====================
        private void DrawBody()
        {
            // Dynamic body height calculation
            float bodyHeight = position.height - 80 - 100; // header + footer
            Rect bodyRect = new Rect(0, 80, position.width, bodyHeight);
            GUILayout.BeginArea(bodyRect);

            if (database == null)
            {
                DrawDatabasePrompt();
            }
            else
            {
                DrawMainInterface(bodyHeight);
            }

            GUILayout.EndArea();
        }

        private void DrawMainInterface(float availableHeight)
        {
            GUILayout.BeginHorizontal();

            // Left panel: Track list (fixed width, full height)
            DrawTrackList(availableHeight);

            GUILayout.Space(10);

            // Right panel: Grouped loops and stem mixer
            if (selectedTrack != null)
            {
                GUILayout.BeginVertical();
                
                // Split available height: 60% loops, 35% mixer, 5% spacing
                float loopListHeight = availableHeight * 0.6f;
                float mixerHeight = availableHeight * 0.35f;
                
                DrawGroupedLoopList(loopListHeight);
                GUILayout.Space(10);
                DrawStemMixer(mixerHeight);
                
                GUILayout.EndVertical();
            }
            else
            {
                DrawTrackPrompt();
            }

            GUILayout.EndHorizontal();
        }

        // ==================== TRACK LIST ====================
        private void DrawTrackList(float availableHeight)
        {
            GUILayout.BeginVertical(GUILayout.Width(280));
            
            DrawSectionHeader("🎼 Tracks", database.tracks?.Count ?? 0);

            Rect scrollRect = EditorGUILayout.BeginVertical(GUILayout.Height(availableHeight - 40)); // 40 for header
            trackScrollPos = EditorGUILayout.BeginScrollView(trackScrollPos);

            if (database.tracks != null)
            {
                for (int i = 0; i < database.tracks.Count; i++)
                {
                    var track = database.tracks[i];
                    if (track == null) continue;

                    bool isSelected = selectedTrack == track;
                    DrawTrackCard(track, isSelected);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        private void DrawTrackCard(TrackData track, bool isSelected)
        {
            Rect cardRect = EditorGUILayout.BeginVertical();
            
            Color bgColor = isSelected ? new Color(0.25f, 0.25f, 0.3f) : cardBg;
            if (isSelected)
            {
                DrawRoundedRect(cardRect, bgColor, 6);
                DrawGlowBorder(cardRect, primaryColor, 2);
            }
            else
            {
                DrawRoundedRect(cardRect, bgColor, 6);
            }

            GUILayout.Space(8);

            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel);
            nameStyle.normal.textColor = isSelected ? Color.white : new Color(0.9f, 0.9f, 0.95f);
            nameStyle.fontSize = 13;
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label(track.displayName, nameStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            
            GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel);
            infoStyle.normal.textColor = mutedText;
            
            GUILayout.Label($"{track.loops?.Count ?? 0} clips", infoStyle);
            GUILayout.Label($"•", infoStyle);
            GUILayout.Label($"{track.defaultBPM:F0} BPM", infoStyle);
            
            GUILayout.FlexibleSpace();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
            {
                selectedTrack = track;
                selectedLoopIndex = -1;
                GroupLoopsByNumber(track);
                Event.current.Use();
            }

            GUILayout.Space(6);
        }

        // ==================== GROUPED LOOP LIST ====================
        private void DrawGroupedLoopList(float height)
        {
            DrawSectionHeader("🔄 Grouped Loops", loopNumbers.Count);

            Rect loopListRect = EditorGUILayout.BeginVertical(GUILayout.Height(height));
            loopScrollPos = EditorGUILayout.BeginScrollView(loopScrollPos, GUILayout.Height(height - 40)); // 40 for header

            for (int i = 0; i < loopNumbers.Count; i++)
            {
                int loopNum = loopNumbers[i];
                var stems = groupedLoops[loopNum];
                
                bool isSelected = selectedLoopIndex == i;
                DrawGroupedLoopCard(loopNum, stems, i, isSelected);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawGroupedLoopCard(int loopNumber, List<LoopStem> stems, int index, bool isSelected)
        {
            Rect cardRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(70));
            
            Color bgColor = isSelected ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.16f, 0.16f, 0.2f);
            DrawRoundedRect(cardRect, bgColor, 4);
            
            if (isSelected)
            {
                DrawGlowBorder(cardRect, accentColor, 2);
            }

            GUILayout.Space(12);

            GUILayout.BeginVertical();
            GUILayout.Space(8);

            // Loop name
            GUIStyle loopNameStyle = new GUIStyle(EditorStyles.boldLabel);
            loopNameStyle.normal.textColor = isSelected ? Color.white : new Color(0.85f, 0.85f, 0.9f);
            loopNameStyle.fontSize = 12;
            
            string loopName = stems.Count > 0 ? stems[0].trackName : "Unknown";
            GUILayout.Label($"Loop #{loopNumber + 1:D2} - {loopName}", loopNameStyle);

            // Stem info
            GUILayout.BeginHorizontal();
            
            GUIStyle statStyle = new GUIStyle(EditorStyles.miniLabel);
            statStyle.normal.textColor = mutedText;
            statStyle.fontSize = 10;

            // Show stem names
            string stemNames = string.Join(", ", stems.Select(s => s.stemName).Take(4));
            if (stems.Count > 4)
                stemNames += $" +{stems.Count - 4} more";
            
            GUI.color = waveformColor;
            GUILayout.Label($"🎛️ {stems.Count} stems: {stemNames}", statStyle);
            GUI.color = Color.white;

            if (stems.Count > 0)
            {
                GUILayout.Label($" • {stems[0].duration:F1}s", statStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Play button with transition support
            GUIStyle playButtonStyle = new GUIStyle(GUI.skin.button);
            playButtonStyle.fontSize = 16;
            
            bool isCurrentlyPlaying = isPlaying && selectedLoopIndex == index;
            bool hasScheduledTransition = musicController != null && musicController.HasScheduledTransition();
            
            // Show different colors for different states
            if (isCurrentlyPlaying)
            {
                GUI.backgroundColor = accentColor; // Currently playing
            }
            else if (hasScheduledTransition && isPlaying)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.3f); // Transition queued
            }
            else
            {
                GUI.backgroundColor = primaryColor; // Not playing
            }
            
            string buttonLabel = isCurrentlyPlaying ? "⏸" : "▶";
            if (GUILayout.Button(buttonLabel, playButtonStyle, GUILayout.Width(40), GUILayout.Height(40)))
            {
                if (isCurrentlyPlaying)
                {
                    // Only stop if clicking the currently playing loop
                    StopPlayback();
                }
                else
                {
                    // Transition to this loop (quantized if already playing)
                    PlayGroupedLoop(index, stems);
                }
            }
            
            GUI.backgroundColor = Color.white;
            GUILayout.Space(8);

            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
            {
                selectedLoopIndex = index;
                InitializeStemControls(stems);
                Event.current.Use();
            }

            GUILayout.Space(4);
        }

        // ==================== STEM MIXER ====================
        private void DrawStemMixer(float height)
        {
            if (selectedLoopIndex < 0 || selectedLoopIndex >= loopNumbers.Count)
                return;

            int loopNum = loopNumbers[selectedLoopIndex];
            var stems = groupedLoops[loopNum];

            DrawSectionHeader("🎛️ Stem Mixer", stems.Count);

            Rect mixerRect = EditorGUILayout.BeginVertical(GUILayout.Height(height));
            DrawRoundedRect(mixerRect, new Color(0.14f, 0.14f, 0.18f), 8);

            GUILayout.Space(12);

            // Master controls
            DrawMasterControls(stems);

            GUILayout.Space(8);
            DrawSeparator();
            GUILayout.Space(8);

            // Individual stem controls with scrolling if needed
            stemMixerScrollPos = EditorGUILayout.BeginScrollView(stemMixerScrollPos, GUILayout.Height(height - 120)); // 120 for header + master
            for (int i = 0; i < stems.Count; i++)
            {
                DrawStemControl(i, stems[i]);
                if (i < stems.Count - 1)
                {
                    GUILayout.Space(4);
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(12);
            EditorGUILayout.EndVertical();
        }

        private void DrawMasterControls(List<LoopStem> stems)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);

            GUILayout.Label("MASTER", EditorStyles.boldLabel, GUILayout.Width(80));

            GUI.color = primaryColor;
            float newMasterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f, GUILayout.Width(200));
            if (newMasterVolume != masterVolume)
            {
                masterVolume = newMasterVolume;
                AudioListener.volume = masterVolume;
            }
            GUI.color = Color.white;

            GUILayout.Label($"{masterVolume:F2}", EditorStyles.miniLabel, GUILayout.Width(35));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reset All", GUILayout.Width(70)))
            {
                UnmuteAllStems(stems);
            }

            if (GUILayout.Button("Mute All", GUILayout.Width(70)))
            {
                MuteAllStems(stems);
            }

            GUILayout.Space(12);
            GUILayout.EndHorizontal();
        }

        private void DrawStemControl(int stemIndex, LoopStem stem)
        {
            if (!stemVolumes.ContainsKey(stemIndex))
                stemVolumes[stemIndex] = 1f;
            if (!stemMutes.ContainsKey(stemIndex))
                stemMutes[stemIndex] = false;
            if (!stemSolos.ContainsKey(stemIndex))
                stemSolos[stemIndex] = false;

            GUILayout.BeginHorizontal();
            GUILayout.Space(12);

            // Stem name
            GUIStyle stemNameStyle = new GUIStyle(EditorStyles.label);
            stemNameStyle.normal.textColor = stemMutes[stemIndex] ? mutedText : Color.white;
            GUILayout.Label($"{stemIndex:D2}", stemNameStyle, GUILayout.Width(25));
            GUILayout.Label(stem.stemName, stemNameStyle, GUILayout.Width(120));

            // Mute button
            bool isMuted = stemMutes[stemIndex];
            GUI.backgroundColor = isMuted ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.3f, 0.3f, 0.35f);
            if (GUILayout.Button(isMuted ? "M" : "M", GUILayout.Width(25), GUILayout.Height(20)))
            {
                ToggleMute(stemIndex);
            }

            // Solo button
            bool isSolo = stemSolos[stemIndex];
            GUI.backgroundColor = isSolo ? new Color(1f, 0.8f, 0.2f) : new Color(0.3f, 0.3f, 0.35f);
            if (GUILayout.Button("S", GUILayout.Width(25), GUILayout.Height(20)))
            {
                ToggleSolo(stemIndex);
            }
            GUI.backgroundColor = Color.white;

            // Volume slider
            Color sliderColor = isMuted ? mutedText : 
                               isSolo ? new Color(1f, 0.8f, 0.2f) : 
                               waveformColor;
            
            GUI.color = sliderColor;
            float newVolume = GUILayout.HorizontalSlider(stemVolumes[stemIndex], 0f, 1f, GUILayout.Width(200));
            if (newVolume != stemVolumes[stemIndex])
            {
                SetStemVolume(stemIndex, newVolume);
            }
            GUI.color = Color.white;

            GUIStyle volumeStyle = new GUIStyle(EditorStyles.miniLabel);
            volumeStyle.normal.textColor = isMuted ? mutedText : Color.white;
            volumeStyle.alignment = TextAnchor.MiddleRight;
            GUILayout.Label($"{stemVolumes[stemIndex]:F2}", volumeStyle, GUILayout.Width(35));

            // VU meter
            if (isPlaying && !isMuted)
            {
                DrawVUMeter(stemIndex);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(12);
            GUILayout.EndHorizontal();
        }

        private void DrawVUMeter(int stemIndex)
        {
            float value = Mathf.PerlinNoise((float)EditorApplication.timeSinceStartup * 10f, stemIndex) * stemVolumes[stemIndex];
            
            Rect meterRect = GUILayoutUtility.GetRect(40, 16);
            EditorGUI.DrawRect(meterRect, new Color(0.1f, 0.1f, 0.12f));
            
            Rect levelRect = new Rect(meterRect.x, meterRect.y, meterRect.width * value, meterRect.height);
            Color meterColor = value > 0.8f ? new Color(1f, 0.3f, 0.3f) : 
                              value > 0.6f ? new Color(1f, 0.8f, 0.2f) : 
                              activeGreen;
            EditorGUI.DrawRect(levelRect, meterColor);
        }

        // ==================== FOOTER ====================
        private void DrawFooter()
        {
            Rect footerRect = new Rect(0, position.height - 70, position.width, 70);
            DrawGradientRect(footerRect, darkBg, new Color(0.1f, 0.1f, 0.12f));

            GUILayout.BeginArea(footerRect);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);

            DrawPlaybackControls();

            GUILayout.FlexibleSpace();

            DrawPlaybackInfo();

            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawPlaybackControls()
        {
            GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button);
            bigButtonStyle.fontSize = 20;
            bigButtonStyle.fixedWidth = 60;
            bigButtonStyle.fixedHeight = 40;

            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button("⏹", bigButtonStyle))
            {
                StopPlayback();
            }

            GUI.backgroundColor = isPlaying ? accentColor : primaryColor;
            if (GUILayout.Button(isPlaying ? "⏸" : "▶", bigButtonStyle))
            {
                if (isPlaying)
                {
                    StopPlayback();
                }
                else if (selectedLoopIndex >= 0 && selectedLoopIndex < loopNumbers.Count)
                {
                    int loopNum = loopNumbers[selectedLoopIndex];
                    PlayGroupedLoop(selectedLoopIndex, groupedLoops[loopNum]);
                }
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawPlaybackInfo()
        {
            if (isPlaying && musicController != null)
            {
                GUIStyle infoStyle = new GUIStyle(EditorStyles.label);
                infoStyle.normal.textColor = mutedText;
                infoStyle.alignment = TextAnchor.MiddleRight;

                float position = musicController.GetCurrentLoopPosition();
                var loop = musicController.GetCurrentLoop();
                float duration = loop != null ? loop.Duration : 0f;

                GUILayout.Label($"Position: {position:F2}s / {duration:F2}s", infoStyle, GUILayout.Width(200));

                // Show transition status
                bool hasScheduledTransition = musicController.HasScheduledTransition();
                if (hasScheduledTransition)
                {
                    infoStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
                    GUILayout.Label("🔄 Transition Queued", infoStyle, GUILayout.Width(150));
                }
                else if (loop != null && loop.HasSyncPoints)
                {
                    float nextSync = musicController.GetTimeUntilNextSync();
                    if (nextSync > 0)
                    {
                        infoStyle.normal.textColor = activeGreen;
                        GUILayout.Label($"⚡ Next Sync: {nextSync:F2}s", infoStyle, GUILayout.Width(150));
                    }
                }
            }
        }

        // ==================== PLAYBACK CONTROL ====================

        private void PlayGroupedLoop(int index, List<LoopStem> stems)
        {
            if (selectedTrack == null || stems == null || stems.Count == 0)
                return;

            // The index from loopNumbers list corresponds to actual TrackData loop index
            int actualLoopIndex = index;
            
            if (actualLoopIndex < 0 || actualLoopIndex >= selectedTrack.loops.Count)
            {
                Debug.LogError($"Loop index {actualLoopIndex} out of range (track has {selectedTrack.loops.Count} loops)");
                return;
            }

            // Get the actual LoopData
            var loopData = selectedTrack.loops[actualLoopIndex];

            // Initialize stem controls
            InitializeStemControls(stems);

            // Set database
            musicController.SetMusicDatabase(database);

            // Use transition if already playing, otherwise play immediately
            if (isPlaying && musicController.IsPlaying())
            {
                // Transition to new loop with quantization
                musicController.TransitionToLoopByIndex(selectedTrack.trackKey, actualLoopIndex);
                Debug.Log($"Transitioning to loop index {actualLoopIndex} (quantized)");
            }
            else
            {
                // Play immediately (first loop)
                musicController.PlayLoopByIndex(selectedTrack.trackKey, actualLoopIndex);
                Debug.Log($"Playing loop index {actualLoopIndex} immediately");
            }
            
            selectedLoopIndex = index;
            isPlaying = true;
        }

        private void StopPlayback()
        {
            if (musicController != null)
            {
                musicController.StopMusic();
            }
            
            isPlaying = false;
            selectedLoopIndex = -1;
        }

        private void InitializeStemControls(List<LoopStem> stems)
        {
            if (stems == null)
                return;

            stemVolumes.Clear();
            stemMutes.Clear();
            stemSolos.Clear();

            for (int i = 0; i < stems.Count; i++)
            {
                stemVolumes[i] = 1f;
                stemMutes[i] = false;
                stemSolos[i] = false;
            }
        }

        private void SetStemVolume(int stemIndex, float volume)
        {
            stemVolumes[stemIndex] = volume;
            
            if (musicController != null && isPlaying && !stemMutes[stemIndex])
            {
                musicController.SetStemVolume(stemIndex, volume);
            }
        }

        private void ToggleMute(int stemIndex)
        {
            stemMutes[stemIndex] = !stemMutes[stemIndex];
            
            if (musicController != null && isPlaying)
            {
                if (stemMutes[stemIndex])
                {
                    musicController.MuteStem(stemIndex);
                }
                else
                {
                    musicController.SetStemVolume(stemIndex, stemVolumes[stemIndex]);
                }
            }
        }

        private void ToggleSolo(int stemIndex)
        {
            bool newSoloState = !stemSolos[stemIndex];
            
            if (newSoloState)
            {
                if (musicController != null && isPlaying)
                {
                    musicController.SoloStem(stemIndex);
                }
                
                for (int i = 0; i < stemSolos.Count; i++)
                {
                    stemSolos[i] = (i == stemIndex);
                    stemMutes[i] = (i != stemIndex);
                }
            }
            else
            {
                if (musicController != null && isPlaying)
                {
                    musicController.UnmuteAllStems();
                }
                
                foreach (var key in stemSolos.Keys.ToList())
                {
                    stemSolos[key] = false;
                    stemMutes[key] = false;
                }
            }
        }

        private void MuteAllStems(List<LoopStem> stems)
        {
            if (stems == null) return;

            for (int i = 0; i < stems.Count; i++)
            {
                stemMutes[i] = true;
                if (musicController != null && isPlaying)
                {
                    musicController.MuteStem(i);
                }
            }
        }

        private void UnmuteAllStems(List<LoopStem> stems)
        {
            if (stems == null) return;

            for (int i = 0; i < stems.Count; i++)
            {
                stemMutes[i] = false;
                stemSolos[i] = false;
                stemVolumes[i] = 1f;
                
                if (musicController != null && isPlaying)
                {
                    musicController.SetStemVolume(i, 1f);
                }
            }
        }

        // ==================== EVENT HANDLERS ====================

        private void OnLoopChanged(LoopData loop)
        {
            Repaint();
        }

        private void OnStemVolumeChanged(int stemIndex, float volume)
        {
            if (stemVolumes.ContainsKey(stemIndex))
            {
                stemVolumes[stemIndex] = volume;
            }
            
            Repaint();
        }

        // ==================== HELPER DRAWING METHODS ====================

        private void DrawSectionHeader(string title, int count)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 13;
            headerStyle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
            
            GUILayout.Label(title, headerStyle);

            GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel);
            countStyle.normal.textColor = mutedText;
            GUILayout.Label($"({count})", countStyle);

            GUILayout.FlexibleSpace();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
        }

        private void DrawSeparator()
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            rect.x += 12;
            rect.width -= 24;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.35f));
        }

        private void DrawRoundedRect(Rect rect, Color color, float radius)
        {
            EditorGUI.DrawRect(rect, color);
        }

        private void DrawGlowBorder(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private void DrawGradientRect(Rect rect, Color topColor, Color bottomColor)
        {
            int steps = 20;
            float stepHeight = rect.height / steps;
            
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / steps;
                Color color = Color.Lerp(topColor, bottomColor, t);
                Rect stepRect = new Rect(rect.x, rect.y + i * stepHeight, rect.width, stepHeight + 1);
                EditorGUI.DrawRect(stepRect, color);
            }
        }

        private void DrawPlayModeWarning()
        {
            GUILayout.FlexibleSpace();
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUILayout.BeginVertical(GUILayout.Width(400));
            
            GUIStyle warningStyle = new GUIStyle(EditorStyles.boldLabel);
            warningStyle.fontSize = 18;
            warningStyle.alignment = TextAnchor.MiddleCenter;
            warningStyle.normal.textColor = new Color(1f, 0.7f, 0.3f);
            
            GUILayout.Label("⚠️", warningStyle);
            GUILayout.Label("Play Mode Required", warningStyle);
            
            GUILayout.Space(10);
            
            GUIStyle infoStyle = new GUIStyle(EditorStyles.label);
            infoStyle.alignment = TextAnchor.MiddleCenter;
            infoStyle.normal.textColor = mutedText;
            infoStyle.wordWrap = true;
            
            GUILayout.Label("The Music Jukebox requires Play Mode to function.\nEnter Play Mode to test your adaptive music system.", infoStyle);
            
            GUILayout.Space(20);
            
            if (GUILayout.Button("Enter Play Mode", GUILayout.Height(35)))
            {
                EditorApplication.isPlaying = true;
            }
            
            GUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.FlexibleSpace();
        }

        private void DrawControllerMissing()
        {
            EditorGUILayout.HelpBox("MusicController not found in scene.", MessageType.Error);
        }

        private void DrawDatabasePrompt()
        {
            GUILayout.FlexibleSpace();
            
            GUIStyle promptStyle = new GUIStyle(EditorStyles.boldLabel);
            promptStyle.fontSize = 16;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.normal.textColor = mutedText;
            
            GUILayout.Label("Select a Music Database to begin", promptStyle);
            
            GUILayout.FlexibleSpace();
        }

        private void DrawTrackPrompt()
        {
            GUILayout.FlexibleSpace();
            
            GUIStyle promptStyle = new GUIStyle(EditorStyles.boldLabel);
            promptStyle.fontSize = 14;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.normal.textColor = mutedText;
            
            GUILayout.Label("← Select a track to view grouped loops", promptStyle);
            
            GUILayout.FlexibleSpace();
        }
    }
}
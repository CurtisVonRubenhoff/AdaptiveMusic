# Adaptive Music System for Unity

A Unity package for managing AI-extracted audio loops with quality-aware crossfading, dynamic loop selection, and adaptive intensity control.

## Features

- **Quality-Aware Crossfading**: Automatically adjusts crossfade duration based on loop quality scores
- **Dynamic Loop Selection**: Choose loops by quality, intensity, or tags
- **Adaptive Music**: Change intensity dynamically during gameplay
- **Auto Loop Progression**: Automatically vary loops for musical interest
- **Equal-Power Crossfading**: Smooth perceived loudness transitions
- **Easy Integration**: Simple API with extensive customization options

## Installation

### Via Unity Package Manager

1. Open Unity Package Manager (`Window > Package Manager`)
2. Click `+` and select `Add package from git URL`
3. Enter: `https://github.com/CurtisVonRubenhoff/AdaptiveMusic.git?path=/unity-package`

### Via Manual Install

1. Download the latest release
2. Extract to your project's `Packages` folder
3. Unity will automatically import the package

## Quick Start

### 1. Extract Audio Loops

Use the [Audio Loop Extractor](https://github.com/CurtisVonRubenhoff/loop_extractor) Python tool:

```bash
python3 loop_extractor.py "your_music.wav" \
  --min-duration 6 \
  --max-duration 30 \
  --prefer-longer \
  -o output_loops
```

### 2. Import to Unity

1. Copy extracted loops to your Unity project (`Assets/Audio/Music/`)
2. Go to `Tools > Adaptive Music > Loop Importer`
3. Create or select a TrackData asset
4. Select the loops folder
5. Click "Parse Report File" to load metadata
6. Click "Import Loops"

### 3. Set Up Music Controller

```csharp
using AdaptiveMusic;

public class GameMusic : MonoBehaviour
{
    void Start()
    {
        // Play a track
        MusicController.Instance.PlayTrack("my-track");
    }
    
    void OnCombatStart()
    {
        // Transition to high-intensity loop
        MusicController.Instance.TransitionToIntensity("my-track", 0.8f);
    }
}
```

## Core Components

### MusicController
The main singleton that handles playback and transitions.

```csharp
// Play a track (random starting loop)
MusicController.Instance.PlayTrack("track-key");

// Transition between tracks
MusicController.Instance.TransitionToTrack("new-track");

// Change intensity
MusicController.Instance.TransitionToIntensity("track-key", 0.5f);

// Stop music
MusicController.Instance.StopMusic();

// Fade out over time
MusicController.Instance.FadeOut(2f);
```

### TrackData (ScriptableObject)
Represents a complete music track with all its loops.

Create via: `Right Click > Create > Adaptive Music > Track Data`

### MusicDatabase (ScriptableObject)
Database containing all your music tracks.

Create via: `Right Click > Create > Adaptive Music > Music Database`

### LoopData
Individual loop with metadata (quality, duration, intensity).

## Advanced Usage

### Quality-Adaptive Crossfading

```csharp
// Enable automatic quality-based crossfade adjustment
MusicController.Instance.SetQualityAdaptiveCrossfade(true);

// Set base crossfade duration (from extractor report)
MusicController.Instance.SetBaseCrossfadeDuration(0.1f);

// Quality mapping:
// 0.8-1.0 → 50ms crossfade
// 0.6-0.8 → 100ms crossfade
// 0.4-0.6 → 150ms crossfade
```

### Auto Loop Progression

```csharp
// Enable automatic loop variety
MusicController.Instance.SetAutoLoopProgression(true, 0.3f);
// 30% chance to switch to a different loop when current ends
```

### Loop Selection

```csharp
TrackData track = database.GetTrack("my-track");

// Get best quality loop
LoopData bestLoop = track.GetBestQualityLoop();

// Get loops by tag
List<LoopData> calmLoops = track.GetLoopsWithTag("calm");

// Get loop by intensity
LoopData intensLoop = track.GetLoopClosestToIntensity(0.8f);

// Play specific loop
MusicController.Instance.TransitionToLoop("my-track", intensLoop);
```

### Events

```csharp
// Subscribe to events
MusicController.Instance.OnTrackChanged += OnTrackChanged;
MusicController.Instance.OnLoopChanged += OnLoopChanged;
MusicController.Instance.OnMusicStopped += OnMusicStopped;

void OnTrackChanged(string trackKey)
{
    Debug.Log($"Now playing: {trackKey}");
}

void OnLoopChanged(LoopData loop)
{
    Debug.Log($"Loop changed: {loop.ToString()}");
}
```

### Complete Example

```csharp
using UnityEngine;
using AdaptiveMusic;

public class AdaptiveMusicManager : MonoBehaviour
{
    [SerializeField] private MusicDatabase database;
    [SerializeField] private string explorationTrack = "ambient";
    [SerializeField] private string combatTrack = "action";
    
    void Start()
    {
        // Initialize
        MusicController.Instance.SetMusicDatabase(database);
        MusicController.Instance.SetQualityAdaptiveCrossfade(true);
        MusicController.Instance.SetAutoLoopProgression(true, 0.25f);
        
        // Start music
        MusicController.Instance.PlayTrack(explorationTrack);
        
        // Listen for changes
        MusicController.Instance.OnLoopChanged += OnLoopChanged;
    }
    
    public void OnGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Exploration:
                MusicController.Instance.TransitionToTrack(explorationTrack);
                break;
                
            case GameState.Combat:
                MusicController.Instance.TransitionToTrack(combatTrack);
                MusicController.Instance.SetAutoLoopProgression(false);
                break;
        }
    }
    
    public void OnDangerChanged(float danger)
    {
        // danger: 0.0 = safe, 1.0 = extreme
        string currentTrack = MusicController.Instance.GetCurrentTrackKey();
        MusicController.Instance.TransitionToIntensity(currentTrack, danger);
    }
    
    void OnLoopChanged(LoopData loop)
    {
        Debug.Log($"Now playing: {loop.ToString()}");
    }
}
```

## Loop Tagging

Organize loops with tags for better control:

```csharp
// In TrackData Inspector:
Loop 01: tags = ["intro", "calm"]
Loop 02: tags = ["verse", "medium"]
Loop 03: tags = ["chorus", "intense"]

// Then query by tag:
TrackData track = database.GetTrack("my-track");
List<LoopData> intenseLoops = track.GetLoopsWithTag("intense");
```

## API Reference

### MusicController

| Method | Description |
|--------|-------------|
| `PlayTrack(string)` | Play a track with random starting loop |
| `TransitionToTrack(string)` | Transition to a different track |
| `TransitionToLoop(string, LoopData)` | Transition to specific loop |
| `TransitionToIntensity(string, float)` | Transition to loop matching intensity |
| `StopMusic()` | Stop all music immediately |
| `FadeOut(float)` | Fade out music over duration |
| `SetBaseCrossfadeDuration(float)` | Set base crossfade time |
| `SetQualityAdaptiveCrossfade(bool)` | Enable quality-based crossfade adjustment |
| `SetAutoLoopProgression(bool, float)` | Enable automatic loop variety |
| `GetCurrentTrackKey()` | Get current track identifier |
| `GetCurrentLoop()` | Get current LoopData |
| `IsPlaying()` | Check if music is playing |
| `IsTransitioning()` | Check if currently crossfading |

### TrackData

| Method | Description |
|--------|-------------|
| `GetRandomLoop()` | Get random loop from track |
| `GetLoopByIndex(int)` | Get loop by index |
| `GetLoopsWithTag(string)` | Get all loops with tag |
| `GetLoopClosestToQuality(float)` | Get loop nearest to quality score |
| `GetLoopClosestToIntensity(float)` | Get loop nearest to intensity |
| `GetBestQualityLoop()` | Get highest quality loop |

### MusicDatabase

| Method | Description |
|--------|-------------|
| `GetTrack(string)` | Get track by key |
| `GetTracksWithTag(string)` | Get all tracks with tag |
| `GetRandomTrack()` | Get random track |

## Requirements

- Unity 2021.3 or higher
- LOOP_EXTRACTOR Python tool (for creating loops)

## Support
- **Issues**: [GitHub Issues](https://github.com/CurtisVonRubenhoff/AdaptiveMusic/issues)
- **Discussions**: [GitHub Discussions](https://github.com/CurtisVonRubenhoff/AdaptiveMusic/discussions)

## License

MIT License - see LICENSE file for details

## Credits

Part of the [Audio Loop Extractor](https://github.com/CurtisVonRubenhoff/loop_extractor) project.

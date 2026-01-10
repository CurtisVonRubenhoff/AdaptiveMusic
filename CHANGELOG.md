# Changelog - Adaptive Music System

All notable changes to the Adaptive Music System Unity package will be documented in this file.

## [1.0.0] - 2025-01-10

### Added
- Initial release of Adaptive Music System package
- MusicController singleton for managing music playback
- Quality-aware crossfading system
- Equal-power crossfade algorithm for smooth transitions
- TrackData ScriptableObject for organizing loops
- LoopData class for loop metadata
- MusicDatabase ScriptableObject for managing multiple tracks
- Loop Importer editor tool
- Automatic loop quality parsing from extractor reports
- Dynamic loop selection by quality, intensity, or tags
- Auto loop progression for musical variety
- Intensity-based adaptive music
- Event system (OnTrackChanged, OnLoopChanged, OnMusicStopped)
- Complete API documentation
- Example usage code

### Features
- Designed specifically for AI-extracted audio loops
- No crossfades baked into audio files
- Runtime crossfading based on loop quality scores
- Support for multiple tracks with multiple loops each
- Tag-based loop organization
- Customizable crossfade curves
- Beat and bar position tracking
- Seamless track transitions
- Fade out support

## [Unreleased]

### Planned
- Audio visualization tools
- Loop preview in editor
- Automatic intensity detection
- FMOD integration
- Wwise integration
- Custom crossfade curve presets
- Performance optimizations

---

For full documentation, see README.md

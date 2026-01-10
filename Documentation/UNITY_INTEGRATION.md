# Unity Package Assembly Instructions

## Package Structure

The Adaptive Music System follows Unity's package layout conventions:

```
gs.curtiscummin.adaptivemusic/
├── package.json                    # Package manifest
├── README.md                       # Package documentation
├── CHANGELOG.md                    # Version history
├── LICENSE.md                      # MIT License
├── Runtime/                        # Runtime code
│   ├── AdaptiveMusic.asmdef       # Assembly definition
│   ├── MusicController.cs         # Main controller
│   ├── MusicDatabase.cs           # Track database
│   ├── TrackData.cs              # Track ScriptableObject
│   └── LoopData.cs               # Loop data class
├── Editor/                        # Editor tools
│   ├── AdaptiveMusic.Editor.asmdef
│   └── LoopImporter.cs           # Loop import tool
├── Documentation~/                # Additional docs
│   └── (markdown files)
└── Samples~/                      # Optional samples
    └── ExampleSetup/
        ├── Scenes/
        ├── Scripts/
        └── Audio/
```

## Files to Include

### Root Files
- `package.json` → Root of package
- `README_PACKAGE.md` → Rename to `README.md`
- `CHANGELOG_PACKAGE.md` → Rename to `CHANGELOG.md`
- `LICENSE_PACKAGE.md` → Rename to `LICENSE.md`

### Runtime Folder
All files in `Runtime/`:
- `MusicController.cs`
- `MusicDatabase.cs`
- `TrackData.cs`
- `LoopData.cs`
- `AdaptiveMusic.asmdef`

### Editor Folder
All files in `Editor/`:
- `LoopImporter.cs`
- `AdaptiveMusic.Editor.asmdef`

### Documentation Folder
Move to `Documentation~/`:
- `UNITY_INTEGRATION.md`
- `GAME_ENGINE_INTEGRATION.md` (Unity sections only)

## Creating the Package

### Option 1: GitHub Repository

1. Create a new repository: `audio-loop-extractor`
2. Create a `unity-package` folder in the root
3. Copy package files into `unity-package/`
4. Install via UPM: `https://github.com/yourusername/audio-loop-extractor.git?path=/unity-package`

### Option 2: Local Package

1. Create folder structure in your Unity project's `Packages/` folder
2. Name it: `gs.curtiscummin.adaptivemusic`
3. Copy all package files
4. Unity will automatically detect and import it

### Option 3: .unitypackage File

1. Import files into a Unity project first
2. Select all package folders (Runtime, Editor)
3. `Assets > Export Package`
4. Name it: `AdaptiveMusicSystem-v1.0.0.unitypackage`
5. Distribute the .unitypackage file

### Option 4: UPM Tarball

```bash
# Create package directory
mkdir -p unity-package
cd unity-package

# Copy files (adjust paths as needed)
cp package.json ./
cp README_PACKAGE.md README.md
cp CHANGELOG_PACKAGE.md CHANGELOG.md
cp LICENSE_PACKAGE.md LICENSE.md

# Copy Runtime and Editor folders
cp -r Runtime ./
cp -r Editor ./

# Create tarball
cd ..
tar -czf gs.curtiscummin.adaptivemusic-1.0.0.tgz unity-package/
```

Install tarball via UPM:
1. `Window > Package Manager`
2. `+` > `Add package from tarball`
3. Select the .tgz file

## Assembly Definition GUIDs

The Editor assembly definition references the Runtime assembly via GUID.

**Important**: The GUID `a5baed0c9693541a5bd947d336ec7659` in `AdaptiveMusic.Editor.asmdef` 
should be replaced with the actual GUID of `AdaptiveMusic.asmdef` once imported into Unity.

To get the correct GUID:
1. Import the package to Unity
2. Select `Runtime/AdaptiveMusic.asmdef`
3. Copy the GUID from the meta file
4. Update `Editor/AdaptiveMusic.Editor.asmdef`

Alternatively, let Unity auto-generate this by:
1. Remove the "references" array from Editor asmdef
2. Import to Unity
3. In Editor asmdef inspector, add reference to Runtime assembly
4. Unity will update the GUID automatically

## Verification Checklist

Before publishing, verify:

- [ ] package.json has correct version number
- [ ] All namespaces are `AdaptiveMusic` (no project-specific names)
- [ ] No dependencies on project-specific code
- [ ] README.md is complete and clear
- [ ] CHANGELOG.md is up to date
- [ ] All menu items use "Tools/Adaptive Music/" or "Adaptive Music/"
- [ ] CreateAssetMenu paths use "Adaptive Music/"
- [ ] Assembly definitions are properly configured
- [ ] Example code in README actually works
- [ ] No hardcoded file paths
- [ ] All required files are included

## Testing the Package

1. Create a new empty Unity project (2021.3+)
2. Install the package
3. Verify no errors in console
4. Test creating TrackData and MusicDatabase
5. Test Loop Importer tool
6. Test MusicController in play mode
7. Build and verify no errors

## Version Numbering

Follow Semantic Versioning (semver.org):
- **1.0.0**: Initial release
- **1.0.x**: Bug fixes
- **1.x.0**: New features (backward compatible)
- **x.0.0**: Breaking changes

## Publishing

### To Unity Asset Store
1. Create Asset Store publisher account
2. Submit package for review
3. Follow Asset Store submission guidelines

### To GitHub
1. Create release tag: `v1.0.0`
2. Attach .unitypackage or tarball
3. Update release notes

### To npm (for UPM)
1. Create npm account
2. Publish: `npm publish`
3. Users can install via scoped package

## Support

After publishing, provide:
- GitHub repository for issues/discussions
- Documentation website (optional)
- Example project (optional)
- Video tutorials (optional)

---

Package ready for distribution! 📦

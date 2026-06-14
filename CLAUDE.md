# DOS2DE Save Game Editor for macOS

macOS-native save game editor for Divinity: Original Sin 2 Definitive Edition.
Built with .NET 9 + Avalonia UI + LSLib.

## Stack

- .NET 9 (SDK 10.0.103), targeting net9.0
- Avalonia UI 11.2.1 (MVVM template)
- LSLib (git submodule, lib/lslib) — Larian Studios file format library
- Pure C# compression shims replacing LSLibNative (C++/CLI, Windows-only)

## Quick Build

```bash
git clone --recursive https://github.com/.../dos2de-save-game-editor-macos.git
cd dos2de-save-game-editor-macos
dotnet build Dos2SaveEditor.slnx
```

Or build individual projects:
```bash
dotnet build src/Dos2SaveEditor.Core/Dos2SaveEditor.Core.csproj
dotnet build src/Dos2SaveEditor/Dos2SaveEditor.csproj
```

## Structure

```
dos2de-save-game-editor-macos/
├── Dos2SaveEditor.slnx
├── src/
│   ├── Dos2SaveEditor/          # Avalonia UI app (net9.0)
│   │   ├── App.axaml
│   │   ├── Program.cs
│   │   ├── Views/
│   │   └── ViewModels/
│   └── Dos2SaveEditor.Core/     # Business logic (net9.0)
│       └── Compression/         # (planned)
├── lib/
│   └── lslib/                   # git submodule
│       └── LSLib/
│           └── Native.cs        # Pure C# shims for LZ4/FastLZ/Granny2
├── tests/
├── PLAN.md                      # Full implementation plan
└── CLAUDE.md                    # This file
```

## Key Modifications to LSLib

The LSLib submodule has minimal changes for macOS compatibility:

1. **LSLib.csproj**: Removed `LSLibNative` project reference (C++/CLI, Windows-only)
2. **LSLib.csproj**: Removed PreBuildEvent (gplex/gppg not available on macOS)
3. **Native.cs**: Pure C# replacements for `LZ4FrameCompressor`, `FastLZCompressor`, `Granny2Compressor`
4. **GoalParser/HeaderParser stubs**: `.lex.cs` and `.yy.cs` files with `NotImplementedException` stubs (story compiler not needed for save editing)

## Original Save Editor Reference

The original Windows `dos-2-savegame-editor` is a C/Win32 app that uses LSLib for:
- Character editing (attributes, abilities, talents, tags, stats)
- Inventory management (items, equipment, backpacks, runes, boosters)
- Mod removal from saves
- Raw XML tree editor

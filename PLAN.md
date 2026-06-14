# DOS2DE Save Game Editor for macOS — Implementation Plan

**Goal:** Full-featured macOS save game editor for Divinity: Original Sin 2 Definitive Edition, achieving parity with the Windows [DoS-2-Savegame-Editor](https://github.com/NovFR/DoS-2-Savegame-Editor).

**Stack:** .NET 9 + Avalonia UI + LSLib (referenced directly)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│                  Avalonia UI                     │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐ │
│  │ Character│ │ Inventory│ │ Raw Tree Editor  │ │
│  │  Editor  │ │  Editor  │ │   (Advanced)     │ │
│  └────┬─────┘ └────┬─────┘ └───────┬─────────┘ │
│       │             │               │           │
│  ┌────┴─────────────┴───────────────┴─────────┐ │
│  │          Save Editor Logic Layer            │ │
│  │  (Character/Item/Mod management, undo)      │ │
│  └────────────────────┬───────────────────────┘ │
├───────────────────────┼─────────────────────────┤
│              LSLib (net9.0)                      │
│  ┌────────────────────┼───────────────────────┐ │
│  │ PackageReader │ LSF/LSX │ SavegameHelpers  │ │
│  └────────────────────┼───────────────────────┘ │
│  ┌────────────────────┴───────────────────────┐ │
│  │   Pure C# Compression Shims                 │ │
│  │   (LZ4FrameCompressor, FastLZCompressor)    │ │
│  └──────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

---

## Phase 1: Foundation (Week 1-2)

### 1.1 Project Scaffolding
- [ ] Create solution structure:
  ```
  dos2de-save-game-editor-macos/
  ├── Dos2SaveEditor.sln
  ├── src/
  │   ├── Dos2SaveEditor/          # Avalonia UI app
  │   │   ├── Dos2SaveEditor.csproj
  │   │   ├── App.axaml
  │   │   ├── Program.cs
  │   │   ├── Views/
  │   │   └── ViewModels/
  │   └── Dos2SaveEditor.Core/     # Business logic library
  │       ├── Dos2SaveEditor.Core.csproj
  │       ├── Models/
  │       └── Services/
  ├── lib/
  │   └── lslib/                   # LSLib source (git submodule)
  ├── tests/
  │   └── Dos2SaveEditor.Tests/
  ├── docs/
  └── PLAN.md
  ```
- [ ] Initialize git repo with LSLib as submodule
- [ ] Create `.gitignore` (bin/obj, .DS_Store, etc.)
- [ ] Set up Avalonia UI project targeting `net9.0-macos`
- [ ] Reference LSLib project from solution
- [ ] Verify LSLib builds on macOS: `dotnet build`

### 1.2 Native Dependency Resolution — Pure C# Compression Shims
LSLib depends on `LSLibNative` (C++/CLI, Windows-only) for two things:
- `Native.LZ4FrameCompressor` — LZ4 frame-level compression/decompression
- `Native.FastLZCompressor` — FastLZ compression for virtual textures

**Approach:** Create pure C# replacements in `Dos2SaveEditor.Core/Compression/`

- [ ] **FastLZ port** — Port `fastlz.c` (simple ~500 line algorithm) to C#
  - Source: `lslib/LSLibNative/fastlz.c`
  - API: `byte[] Compress(byte[] input, int level)`, `byte[] Decompress(byte[] input, int maxOutput)`
  - MIT licensed, trivial to port

- [ ] **LZ4 Frame Compressor** — Implement LZ4 frame format in C#
  - LSLib already depends on `K4os.Compression.LZ4` (block-level LZ4)
  - We need frame-level (LZ4F) which wraps blocks with headers
  - Reference implementation: `lslib/LSLibNative/lz4wrapper.cpp`
  - Alternative: Use `K4os.Compression.LZ4.Streams` NuGet which includes LZ4FrameStream
  - API: `byte[] Compress(byte[] input)`, `byte[] Decompress(byte[] input)`

- [ ] **Remove LSLibNative reference** from LSLib.csproj
  - Add conditional compilation or shim classes in same namespace (`LSLib.Native`)
  - The `Native` class is implicitly available via C++/CLI — we define a `Native.cs` shim

- [ ] **Verify savegame round-trip**: Load a DOS2DE `.lsv` → modify nothing → save → verify integrity

### 1.3 Savegame Load/Save Pipeline
- [ ] Implement `SavegameService` wrapping LSLib's `SavegameHelpers`
  - `OpenSavegame(string path)` → loads `.lsv` package
  - `LoadGlobals()` → parse `globals.lsf` into `Resource` node tree
  - `LoadStory()` → parse `StorySave.bin` / story from globals
  - `SaveSavegame(string path)` → re-package and write `.lsv`
- [ ] Build a `SavegameMetadata` model:
  - Game version, difficulty, game time, save date
  - Player character list
  - Mod list
- [ ] Unit tests with sample `.lsv` files

---

## Phase 2: Core Editing Features (Week 3-5)

### 2.1 Character Editor
Map the data structures from the original editor (`Game.h`):
- [ ] **Character list** — Parse character nodes from globals
- [ ] **Attributes** — Strength, Dexterity, Intelligence, Constitution, Memory, Wits
- [ ] **Abilities** — Combat abilities, civil abilities (40+ skills)
- [ ] **Talents** — List of equipped talents
- [ ] **Tags** — Character tags editor
- [ ] **Stats display** — Vitality, Armor, Magic Armor, XP, Level
- [ ] **Points** — Attribute points, Ability points, Talent points
- [ ] **Data validation** — Respect min/max bounds from original game

### 2.2 Inventory & Item Editor
- [ ] **Inventory tree** — Character → Backpacks → Items hierarchy
- [ ] **Item details view**:
  - Display name, stats ID, description
  - Item type (weapon/armor/accessory/consumable)
  - Amount, slot, level
  - Generation params (level override, name index)
- [ ] **Runes** — View/edit up to 3 rune slots per item
- [ ] **Boosters** — View/edit permanent boosts
- [ ] **Equipment slots** — Show what's equipped

### 2.3 Mod Management
- [ ] **Mod list display** — Show all mods the save depends on
- [ ] **Remove mod** — Strip a mod's data from the save
- [ ] **Reorder mods** — Change mod load order

---

## Phase 3: Advanced Features (Week 6-7)

### 3.1 Raw Tree/XML Editor
- [ ] **TreeView** on the full globals node structure
- [ ] **Node editing** — Add/remove/edit LSF nodes and attributes
- [ ] **Type-aware editing** — GUID picker, float/int/string editors
- [ ] **Search/filter** — Find nodes by name or attribute value

### 3.2 Save File Management
- [ ] **Save file browser** — List saves in user's save directory
  - Detect DOS2DE saves via Steam: `~/Library/Application Support/Steam/...`
  - Or custom directory picker
- [ ] **Save metadata display** — Show version, difficulty, play time, date, party
- [ ] **Backup on save** — Auto-create `.bak` before overwriting

### 3.3 Localization
- [ ] Port localization system from original editor
  - Original uses SQLite databases in `Locales/` folder
  - Support English, French, Simplified Chinese
  - Extensible for community translations
- [ ] Load LSLib stat definitions for item/stat name resolution

---

## Phase 4: Polish & Release (Week 8)

### 4.1 UI Polish
- [ ] macOS-native look and feel (Avalonia native menus, proper window chrome)
- [ ] Dark mode support
- [ ] Keyboard shortcuts (Cmd+S save, Cmd+Z undo, etc.)
- [ ] Drag & drop for inventory
- [ ] Progress indicators for large saves
- [ ] Error handling with user-friendly messages

### 4.2 Packaging
- [ ] `dotnet publish` for macOS arm64 + x86_64
- [ ] Create `.app` bundle with proper Info.plist
- [ ] Code signing for distribution
- [ ] DMG generator script
- [ ] Auto-update mechanism (Sparkle or custom)

### 4.3 Testing & Docs
- [ ] Comprehensive test suite with real DOS2DE saves
- [ ] User documentation / README
- [ ] Known issues / limitations documented
- [ ] Contribution guidelines

---

## Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UI Framework | Avalonia UI | More mature macOS support than MAUI, WPF-like MVVM pattern, native rendering |
| .NET Version | .NET 9 | Latest LTS, best ARM64 performance on Apple Silicon |
| LSLib Integration | Direct project reference | LSLib is net8.0, compatible; only need to shim 2 native classes |
| Compression | Pure C# ports | FastLZ is trivial, LZ4 frame via K4os.Compression.LZ4.Streams |
| MVVM | ReactiveUI or CommunityToolkit.Mvvm | Both work well with Avalonia |
| Testing | xUnit + Shouldly | Standard .NET test stack |

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| LSLib assumes Windows paths | Medium | LSLib uses `Path.Combine` etc. — test early on macOS |
| LZ4 frame format edge cases | Medium | Validate against reference C++ implementation with test vectors |
| DOS2DE save format variations | Low | LSLib already handles all DOS2DE versions |
| Avalonia macOS rendering bugs | Low | Avalonia has active macOS support; file issues upstream |
| No DOS2DE on Mac for testing | Low | Use sample save files; test round-trip integrity |

---

## Sample Save Files Needed

To build and test, we need sample `.lsv` files from DOS2DE:
- Fresh game save (no mods)
- Mid-game save (no mods)
- Save with mods
- Save with complex inventory (backpacks, runes, etc.)

These can be collected from a Windows DOS2DE installation or community contributors.

---

## File Breakdown: Key Classes

```
Dos2SaveEditor.Core/
├── Compression/
│   ├── FastLZCompressor.cs      # Port of fastlz.c
│   ├── LZ4FrameCompressor.cs    # LZ4 frame wrapper using K4os
│   └── Native.cs                # Shim matching LSLib.Native namespace
├── Models/
│   ├── SavegameInfo.cs          # Metadata: version, difficulty, etc.
│   ├── Character.cs             # DOS2CHARACTER equivalent
│   ├── Item.cs                  # DOS2ITEM equivalent
│   ├── Inventory.cs             # DOS2INVENTORY equivalent
│   └── ModEntry.cs              # Mod dependency info
├── Services/
│   ├── ISavegameService.cs      # Interface for savegame I/O
│   ├── SavegameService.cs       # Implementation using LSLib
│   ├── CharacterService.cs      # Character CRUD operations
│   ├── ItemService.cs           # Item CRUD operations
│   └── ModService.cs            # Mod management
└── Utils/
    ├── GameConstants.cs         # Min/max values, XP table, etc.
    └── LocalizationHelper.cs    # String lookups from game data

Dos2SaveEditor/
├── Views/
│   ├── MainWindow.axaml         # Main window with tab navigation
│   ├── SaveBrowserView.axaml    # Save file picker
│   ├── CharacterListView.axaml  # Character sidebar
│   ├── CharacterEditView.axaml  # Character detail editor
│   ├── InventoryView.axaml      # Inventory tree + item detail
│   ├── ItemEditView.axaml       # Item detail editor
│   ├── ModListView.axaml        # Mod management
│   └── TreeEditView.axaml       # Raw node editor
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── SaveBrowserViewModel.cs
│   ├── CharacterListViewModel.cs
│   ├── CharacterEditViewModel.cs
│   ├── InventoryViewModel.cs
│   ├── ItemEditViewModel.cs
│   ├── ModListViewModel.cs
│   └── TreeEditViewModel.cs
└── Converters/
    ├── IconConverters.cs        # Item type → icon
    └── ValueConverters.cs       # Game value formatting
```

---

_This plan is a living document. Update as implementation progresses._

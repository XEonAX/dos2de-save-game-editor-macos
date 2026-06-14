using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dos2SaveEditor.Core.Models;
using Dos2SaveEditor.Core.Services;

namespace Dos2SaveEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISavegameService _saveService = new SavegameService();

    [ObservableProperty] private SavegameInfo? _saveInfo;
    [ObservableProperty] private string _title = "DOS2DE Save Editor";
    [ObservableProperty] private string _statusText = "No save file open.";
    [ObservableProperty] private bool _isSaveOpen;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private object? _selectedCharacter;
    [ObservableProperty] private object? _selectedItem;
    [ObservableProperty] private Item? _selectedInventoryItem;
    [ObservableProperty] private ItemEditViewModel? _itemEditor;

    public ObservableCollection<Character> Characters { get; } = [];
    public ObservableCollection<ModEntry> Mods { get; } = [];
    public ObservableCollection<Item> InventoryItems { get; } = [];
    [ObservableProperty] private CharacterEditViewModel? _characterEditor;

    /// <summary>Item detail text for the inventory detail pane.</summary>
    public string InventoryItemDetail => SelectedInventoryItem is Item i
        ? $"{i.StatsId}\nSlot: {i.Slot ?? "Inventory"}\nAmount: {i.Amount}\nLevel: {i.Level}\nHandle: {i.Handle}"
        : "Select an item to view details.";

    partial void OnSelectedInventoryItemChanged(Item? value)
    {
        OnPropertyChanged(nameof(InventoryItemDetail));
        ItemEditor = value != null && SaveInfo != null
            ? new ItemEditViewModel(_saveService, SaveInfo, value)
            : null;
    }

    /// <summary>Set by the view to enable native file dialogs.</summary>
    public Avalonia.Platform.Storage.IStorageProvider? StorageProvider { get; set; }

    partial void OnSelectedCharacterChanged(object? value)
    {
        if (value is Character c)
        {
            CharacterEditor = new CharacterEditViewModel(_saveService, SaveInfo!, c);
            LoadInventory(c);
        }
    }

    private void LoadInventory(Character c)
    {
        InventoryItems.Clear();
        if (SaveInfo == null) return;
        foreach (var item in _saveService.GetInventory(SaveInfo, c))
            InventoryItems.Add(item);
    }

    [RelayCommand]
    private async Task OpenSave()
    {
        StatusText = "Select a DOS2DE save file (.lsv)...";

        string? path = null;

        // Try native file picker first
        if (StorageProvider != null)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open DOS2DE Save File",
                AllowMultiple = false,
                FileTypeFilter = [
                    new Avalonia.Platform.Storage.FilePickerFileType("DOS2 Save")
                    {
                        Patterns = ["*.lsv"],
                        MimeTypes = ["application/octet-stream"]
                    }
                ]
            });

            if (files.Count > 0)
                path = files[0].Path.LocalPath;
        }

        // Fallback to env var for testing
        if (string.IsNullOrEmpty(path))
            path = System.Environment.GetEnvironmentVariable("DOS2_SAVE_PATH");

        if (string.IsNullOrEmpty(path))
        {
            StatusText = "No file selected.";
            return;
        }

        await LoadSaveAsync(path);
    }

    private async Task LoadSaveAsync(string path)
    {
        try
        {
            StatusText = $"Loading {System.IO.Path.GetFileName(path)}...";
            SaveInfo = await _saveService.OpenSaveAsync(path);

            Characters.Clear();
            foreach (var c in _saveService.GetCharacters(SaveInfo))
                Characters.Add(c);

            Mods.Clear();
            foreach (var m in _saveService.GetMods(SaveInfo))
                Mods.Add(m);

            // Try to auto-detect game data path
            TryAutoDetectGameData();

            IsSaveOpen = true;
            Title = $"DOS2DE Save Editor — {SaveInfo.FileName}";
            var statsMsg = _saveService.Stats.IsLoaded
                ? $" | {_saveService.Stats.EntryCount} stats loaded"
                : "";
            StatusText = $"Loaded {Characters.Count} characters, {Mods.Count} mods.{statsMsg}";
        }
        catch (System.Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void TryAutoDetectGameData()
    {
        Debug.WriteLine("=== Auto-detecting DOS2DE game data ===");

        // Build list of candidate base directories
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var candidates = new List<(string description, string path)>();

        // 1. Environment variable (exact path)
        var envPath = System.Environment.GetEnvironmentVariable("DOS2DE_DATA_PATH");
        if (!string.IsNullOrEmpty(envPath))
            candidates.Add(("DOS2DE_DATA_PATH env var", envPath));

        // 2. macOS Steam — common library location
        var steamBase = System.IO.Path.Combine(home, "Library/Application Support/Steam/steamapps/common/Divinity Original Sin 2");

        // .app bundle name uses hyphens on macOS Steam: "Divinity - Original Sin 2.app"
        var steamAppBase = System.IO.Path.Combine(steamBase, "Divinity - Original Sin 2.app/Contents/Resources");
        var steamAppBaseAlt = System.IO.Path.Combine(steamBase, "Divinity Original Sin 2.app/Contents/Resources");

        candidates.Add(("Steam: DefEd", System.IO.Path.Combine(steamBase, "DefEd")));
        candidates.Add(("Steam: Data top-level", System.IO.Path.Combine(steamBase, "Data")));

        // Primary .app path (with hyphens — actual macOS Steam name)
        candidates.Add(("Steam .app: Data", System.IO.Path.Combine(steamAppBase, "Data")));
        candidates.Add(("Steam .app: DefEd/Data", System.IO.Path.Combine(steamAppBase, "DefEd/Data")));

        // Alternate .app path (without hyphens)
        candidates.Add(("Steam .app alt: Data", System.IO.Path.Combine(steamAppBaseAlt, "Data")));
        candidates.Add(("Steam .app alt: DefEd/Data", System.IO.Path.Combine(steamAppBaseAlt, "DefEd/Data")));

        // 3. Also try inside the .app bundle at Contents/ (without Resources)
        var steamAppContents = System.IO.Path.Combine(steamBase, "Divinity - Original Sin 2.app/Contents");
        candidates.Add(("Steam .app: Contents/Data", System.IO.Path.Combine(steamAppContents, "Data")));
        candidates.Add(("Steam .app: Contents/DefEd/Data", System.IO.Path.Combine(steamAppContents, "DefEd/Data")));

        // 4. Secondary Steam libraries
        var libraryFoldersPath = System.IO.Path.Combine(home, "Library/Application Support/Steam/steamapps/libraryfolders.vdf");
        if (System.IO.File.Exists(libraryFoldersPath))
        {
            Debug.WriteLine($"  Reading Steam library folders from: {libraryFoldersPath}");
            try
            {
                foreach (var line in System.IO.File.ReadLines(libraryFoldersPath))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"""path""\s+""([^""]+)""");
                    if (match.Success)
                    {
                        var libPath = match.Groups[1].Value.Replace("\\\\", "/").Replace("\\", "/");
                        var libSteamBase = System.IO.Path.Combine(libPath, "steamapps/common/Divinity Original Sin 2");
                        candidates.Add(($"Steam lib '{libPath}': DefEd", System.IO.Path.Combine(libSteamBase, "DefEd")));
                        candidates.Add(($"Steam lib '{libPath}': .app Data", System.IO.Path.Combine(libSteamBase, "Divinity - Original Sin 2.app/Contents/Resources/Data")));
                        candidates.Add(($"Steam lib '{libPath}': .app DefEd/Data", System.IO.Path.Combine(libSteamBase, "Divinity - Original Sin 2.app/Contents/Resources/DefEd/Data")));
                        candidates.Add(($"Steam lib '{libPath}': .app alt Data", System.IO.Path.Combine(libSteamBase, "Divinity Original Sin 2.app/Contents/Resources/Data")));
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"  Error parsing libraryfolders.vdf: {ex.Message}"); }
        }

        // 5. GOG standalone
        candidates.Add(("GOG: .app Data", "/Applications/Divinity - Original Sin 2.app/Contents/Resources/Data"));
        candidates.Add(("GOG: .app DefEd/Data", "/Applications/Divinity - Original Sin 2.app/Contents/Resources/DefEd/Data"));
        candidates.Add(("GOG: .app alt Data", "/Applications/Divinity Original Sin 2.app/Contents/Resources/Data"));
        candidates.Add(("GOG: .app alt DefEd/Data", "/Applications/Divinity Original Sin 2.app/Contents/Resources/DefEd/Data"));

        // 6. GOG via Heroic Games Launcher — only check directories that look like game installs
        var heroicBase = System.IO.Path.Combine(home, "Library/Application Support/Heroic");
        // Skips known cache/config directories (Cache, GPUCache, Code Cache, etc.)
        var heroicSkipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blob_storage", "Cache", "Code Cache", "DawnGraphiteCache", "DawnWebGPUCache",
            "fixes", "gog_store", "gogdlConfig", "GPUCache", "icons", "images-cache",
            "IndexedDB", "legendaryConfig", "Local Storage", "nile_store", "Partitions",
            "Session Storage", "shared_proto_db", "Shared Dictionary", "sideload_apps",
            "store", "store_cache", "tools", "VideoDecodeStats", "WebStorage", "zoom_store",
            "GamesConfig"
        };
        if (System.IO.Directory.Exists(heroicBase))
        {
            Debug.WriteLine($"  Checking Heroic launcher at: {heroicBase}");
            try
            {
                foreach (var dir in System.IO.Directory.GetDirectories(heroicBase))
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    if (heroicSkipDirs.Contains(dirName))
                        continue;
                    Debug.WriteLine($"  Heroic game dir candidate: {dirName}");
                    candidates.Add(($"Heroic '{dirName}': DefEd", System.IO.Path.Combine(dir, "DefEd")));
                    candidates.Add(($"Heroic '{dirName}': Data", System.IO.Path.Combine(dir, "Data")));
                }
            }
            catch { /* ignore */ }
        }

        // For each candidate, append the stats sub-path and try loading
        foreach (var (description, basePath) in candidates)
        {
            if (string.IsNullOrEmpty(basePath))
                continue;

            Debug.WriteLine($"  Checking candidate [{description}]: base='{basePath}'");

            if (!System.IO.Directory.Exists(basePath))
            {
                Debug.WriteLine($"    -> base directory does not exist, skipping");
                continue;
            }

            // Try all stats sub-paths relative to the base
            if (TryLoadStatsPath(basePath, description))
                return;

            // Try searching for the Stats/Generated/Data folder recursively
            Debug.WriteLine($"    -> searching recursively up to 4 levels...");
            try
            {
                var found = SearchForStatsFolder(basePath, 4);
                if (found != null)
                {
                    Debug.WriteLine($"    -> found via recursive search: '{found}'");
                    if (TryLoadStatsPath(found, description + " (recursive)"))
                        return;
                }
                else
                {
                    Debug.WriteLine($"    -> recursive search found nothing");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"    -> recursive search error: {ex.Message}"); }
        }

        Debug.WriteLine("=== Auto-detection complete: no game data found ===");

        // Show a brief diagnostic in the status bar
        var lastLog = _saveService.GetStatLoadLog();
        if (lastLog.Count > 0)
        {
            var lastMsg = lastLog[^1];
            Debug.WriteLine($"  Last log message: {lastMsg}");
        }
    }

    /// <summary>Recursively search for a folder containing stat .txt or .pak files.</summary>
    private static string? SearchForStatsFolder(string root, int maxDepth)
    {
        if (maxDepth <= 0) return null;

        try
        {
            // Check if this directory has stat .txt files or game .pak files
            if (System.IO.Directory.EnumerateFiles(root, "Weapon*.txt").Any() ||
                System.IO.Directory.EnumerateFiles(root, "Armor*.txt").Any() ||
                System.IO.Directory.EnumerateFiles(root, "Shared.pak").Any() ||
                System.IO.Directory.EnumerateFiles(root, "Game.pak").Any())
                return root;

            foreach (var dir in System.IO.Directory.GetDirectories(root))
            {
                var result = SearchForStatsFolder(dir, maxDepth - 1);
                if (result != null) return result;
            }
        }
        catch { /* skip inaccessible directories */ }

        return null;
    }

    private bool TryLoadStatsPath(string path, string description = "")
    {
        Debug.WriteLine($"      TryLoadStatsPath: '{path}' [{description}]");
        if (!System.IO.Directory.Exists(path))
        {
            Debug.WriteLine($"        -> directory does NOT exist");
            return false;
        }

        // Try several stats sub-paths that DOS2DE uses
        var subPaths = new[]
        {
            "Shared/Stats/Generated/Data",
            "Public/Shared/Stats/Generated/Data",
            "Stats/Generated/Data",
            "Public/Stats/Generated/Data",
            "" // also try the directory itself
        };

        foreach (var sub in subPaths)
        {
            var fullPath = string.IsNullOrEmpty(sub) ? path : System.IO.Path.Combine(path, sub);
            Debug.WriteLine($"        -> trying sub-path: '{fullPath}'");

            if (!System.IO.Directory.Exists(fullPath))
            {
                Debug.WriteLine($"          -> sub-dir does not exist");
                continue;
            }

            // List what's in the directory for diagnostics
            try
            {
                var entries = System.IO.Directory.GetFileSystemEntries(fullPath).Take(10).ToList();
                Debug.WriteLine($"          -> directory exists, first entries: {string.Join(", ", entries)}");
            }
            catch { }

            if (_saveService.TryLoadStats(fullPath))
            {
                var count = _saveService.Stats.EntryCount;
                Debug.WriteLine($"          -> SUCCESS! Loaded {count} stat entries");
                if (count > 0)
                {
                    StatusText = $"Loaded {count} stat entries from game data. ({description})";
                    return true;
                }
                else
                {
                    Debug.WriteLine($"          -> loaded 0 entries (directory found but no stat entries parsed)");
                }
            }
            else
            {
                Debug.WriteLine($"          -> TryLoadStats returned false");
            }
        }

        return false;
    }

    [RelayCommand]
    private async Task SaveSave()
    {
        if (SaveInfo == null) return;
        try
        {
            var outPath = SaveInfo.FilePath; // Overwrite
            await _saveService.SaveAsync(SaveInfo, outPath);
            StatusText = "Save written successfully.";
        }
        catch (System.Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CloseSave()
    {
        _saveService.Close(SaveInfo!);
        SaveInfo = null;
        Characters.Clear();
        Mods.Clear();
        InventoryItems.Clear();
        SelectedCharacter = null;
        SelectedInventoryItem = null;
        IsSaveOpen = false;
        Title = "DOS2DE Save Editor";
        StatusText = "No save file open.";
    }

    [RelayCommand]
    private async Task SetGameDataPath()
    {
        if (StorageProvider == null) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select DOS2DE Stats Data Folder (Shared/Stats/Generated/Data)",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (_saveService.TryLoadStats(path))
            {
                var count = _saveService.Stats.EntryCount;
                StatusText = $"Loaded {count} stat entries from game data.";

                // Refresh item editor if an item is selected
                if (SelectedInventoryItem != null && SaveInfo != null)
                    ItemEditor = new ItemEditViewModel(_saveService, SaveInfo, SelectedInventoryItem);
            }
            else
            {
                StatusText = "No stat files found in the selected folder.";
            }
        }
    }
}


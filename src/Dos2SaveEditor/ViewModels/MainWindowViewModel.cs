using System.Collections.ObjectModel;
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

    public ObservableCollection<Character> Characters { get; } = [];
    public ObservableCollection<ModEntry> Mods { get; } = [];
    public ObservableCollection<Item> InventoryItems { get; } = [];
    [ObservableProperty] private CharacterEditViewModel? _characterEditor;

    /// <summary>Item detail text for the inventory detail pane.</summary>
    public string InventoryItemDetail => SelectedInventoryItem is Item i
        ? $"{i.StatsId}\nSlot: {i.Slot ?? "Inventory"}\nAmount: {i.Amount}\nLevel: {i.Level}\nHandle: {i.Handle}"
        : "Select an item to view details.";

    partial void OnSelectedInventoryItemChanged(Item? value) => OnPropertyChanged(nameof(InventoryItemDetail));

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

            IsSaveOpen = true;
            Title = $"DOS2DE Save Editor — {SaveInfo.FileName}";
            StatusText = $"Loaded {Characters.Count} characters, {Mods.Count} mods.";
        }
        catch (System.Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
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
        IsSaveOpen = false;
        Title = "DOS2DE Save Editor";
        StatusText = "No save file open.";
    }
}


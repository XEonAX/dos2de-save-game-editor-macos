using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dos2SaveEditor.Core.Models;
using Dos2SaveEditor.Core.Services;
using System;

namespace Dos2SaveEditor.ViewModels;

public partial class ItemEditViewModel : ViewModelBase
{
    private readonly ISavegameService _saveService;
    private readonly SavegameInfo _saveInfo;
    public Item Item { get; }

    [ObservableProperty] private string _statsId = "";
    [ObservableProperty] private int _amount = 1;
    [ObservableProperty] private int _level;
    [ObservableProperty] private string _slot = "";
    [ObservableProperty] private bool _isGenerated;
    [ObservableProperty] private bool _hasCustomBase;
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _description = "";

    // Rune slots
    [ObservableProperty] private string _rune1 = "";
    [ObservableProperty] private string _rune2 = "";
    [ObservableProperty] private string _rune3 = "";

    // Boosters (comma-separated for editing)
    [ObservableProperty] private string _boostersText = "";

    public string KindLabel => Item.Kind.ToString();
    public string BackpackLabel => Item.IsBackpack ? "Yes" : "No";
    public string HandleLabel => Item.Handle ?? "(none)";

    public ItemEditViewModel(ISavegameService saveService, SavegameInfo saveInfo, Item item)
    {
        _saveService = saveService;
        _saveInfo = saveInfo;
        Item = item;
        Load();
    }

    private void Load()
    {
        StatsId = Item.StatsId ?? "";
        Amount = Item.Amount;
        Level = Item.Level;
        Slot = Item.Slot ?? "";
        IsGenerated = Item.IsGenerated;
        HasCustomBase = Item.HasCustomBase;
        DisplayName = Item.DisplayName ?? "";
        Description = Item.Description ?? "";
        Rune1 = Item.Runes[0] ?? "";
        Rune2 = Item.Runes[1] ?? "";
        Rune3 = Item.Runes[2] ?? "";
        BoostersText = string.Join(", ", Item.Boosters);
    }

    [RelayCommand]
    private void Apply()
    {
        Item.StatsId = StatsId;
        Item.Amount = Amount;
        Item.Level = Level;
        Item.Slot = Slot;
        Item.IsGenerated = IsGenerated;
        Item.HasCustomBase = HasCustomBase;
        Item.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName;
        Item.Description = string.IsNullOrWhiteSpace(Description) ? null : Description;
        Item.Runes[0] = string.IsNullOrWhiteSpace(Rune1) ? null : Rune1;
        Item.Runes[1] = string.IsNullOrWhiteSpace(Rune2) ? null : Rune2;
        Item.Runes[2] = string.IsNullOrWhiteSpace(Rune3) ? null : Rune3;
        Item.Boosters.Clear();
        if (!string.IsNullOrWhiteSpace(BoostersText))
            Item.Boosters.AddRange(BoostersText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        _saveService.UpdateItem(_saveInfo, Item);
    }
}

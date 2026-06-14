using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dos2SaveEditor.Core.Models;
using Dos2SaveEditor.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

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

    // ── Override values (from save) ──────────────────────────
    [ObservableProperty] private int _goldValueOverride = -1;
    [ObservableProperty] private int _weightValueOverride = -1;
    [ObservableProperty] private string _damageTypeOverride = "";
    [ObservableProperty] private int _hpOverride = -1;

    // ── DeltaMods (editable, one per line) ────────────────────
    [ObservableProperty] private string _deltaModsText = "";

    // ── Rune boosts (read-only display) ───────────────────────
    [ObservableProperty] private string _runeBoostsText = "";

    // ── Generation overrides ──────────────────────────────────
    [ObservableProperty] private string _generationStatsId = "";
    [ObservableProperty] private string _generationBase = "";

    // ── Resolved stats from game data ─────────────────────────
    [ObservableProperty] private string _resolvedStatsSummary = "";
    [ObservableProperty] private bool _hasResolvedStats;
    [ObservableProperty] private string _statsEntryName = "";
    [ObservableProperty] private string _extraStatsText = "";

    // ── Diagnostic log ────────────────────────────────────────
    [ObservableProperty] private string _loadLogText = "";
    [ObservableProperty] private bool _hasLoadLog;

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
        StatsEntryName = Item.StatsEntryName ?? "";

        // Override values
        GoldValueOverride = Item.GoldValueOverwrite;
        WeightValueOverride = Item.WeightValueOverwrite;
        DamageTypeOverride = Item.DamageTypeOverwrite ?? "";
        HpOverride = Item.HP;

        // DeltaMods (editable, one per line)
        DeltaModsText = string.Join("\n", Item.DeltaMods);
        RuneBoostsText = string.Join("\n", Item.RuneBoosts);

        // Generation overrides
        GenerationStatsId = Item.GenerationStatsId ?? "";
        GenerationBase = Item.GenerationBase ?? "";

        // Try to resolve stats from game data
        ResolveStats();
    }

    private void ResolveStats()
    {
        var stats = _saveService.Stats.ResolveItemStats(Item);
        if (stats != null)
        {
            HasResolvedStats = true;
            ResolvedStatsSummary = stats.Summary;
            ExtraStatsText = string.Join("\n",
                stats.ExtraProperties.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }
        else
        {
            HasResolvedStats = false;
            ResolvedStatsSummary = "Game data not loaded. Configure game data path to see item stats.";
            ExtraStatsText = "";
        }

        // Load diagnostic log
        var log = _saveService.GetStatLoadLog();
        HasLoadLog = log.Count > 0;
        LoadLogText = HasLoadLog ? string.Join("\n", log) : "";
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

        // Boosters
        Item.Boosters.Clear();
        if (!string.IsNullOrWhiteSpace(BoostersText))
            Item.Boosters.AddRange(BoostersText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        // DeltaMods (one per line)
        Item.DeltaMods.Clear();
        if (!string.IsNullOrWhiteSpace(DeltaModsText))
            Item.DeltaMods.AddRange(DeltaModsText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        // Overrides
        Item.GoldValueOverwrite = GoldValueOverride;
        Item.WeightValueOverwrite = WeightValueOverride;
        Item.DamageTypeOverwrite = string.IsNullOrWhiteSpace(DamageTypeOverride) ? null : DamageTypeOverride;
        Item.HP = HpOverride;

        // Generation overrides
        Item.GenerationStatsId = string.IsNullOrWhiteSpace(GenerationStatsId) ? null : GenerationStatsId;
        Item.GenerationBase = string.IsNullOrWhiteSpace(GenerationBase) ? null : GenerationBase;

        _saveService.UpdateItem(_saveInfo, Item);
    }
}

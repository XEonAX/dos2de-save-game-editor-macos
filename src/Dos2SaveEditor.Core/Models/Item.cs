namespace Dos2SaveEditor.Core.Models;

/// <summary>
/// Represents an inventory item extracted from a DOS2DE save.
/// </summary>
public class Item
{
    /// <summary>Item template/stats ID (e.g., "WPN_Humans_Sword_A")</summary>
    public string? StatsId { get; set; }

    /// <summary>Display name (localized or handle)</summary>
    public string? DisplayName { get; set; }

    /// <summary>Description</summary>
    public string? Description { get; set; }

    /// <summary>UUID handle</summary>
    public string? Handle { get; set; }

    /// <summary>Stack amount</summary>
    public int Amount { get; set; } = 1;

    /// <summary>Is this a generated item?</summary>
    public bool IsGenerated { get; set; }

    /// <summary>Has custom base stats?</summary>
    public bool HasCustomBase { get; set; }

    /// <summary>Equipped slot name (null if not equipped)</summary>
    public string? Slot { get; set; }

    /// <summary>Parent inventory UUID</summary>
    public string? InventoryUuid { get; set; }

    /// <summary>Item level</summary>
    public int Level { get; set; }

    /// <summary>Level group index</summary>
    public int LevelGroupIndex { get; set; }

    /// <summary>Name index (for generated items)</summary>
    public int NameIndex { get; set; }

    /// <summary>Rune slots (up to 3)</summary>
    public string?[] Runes { get; set; } = new string?[3];

    /// <summary>Item kind</summary>
    public Utils.GameConstants.Dos2ItemKind Kind { get; set; }

    /// <summary>Is this a backpack (contains sub-inventory)?</summary>
    public bool IsBackpack { get; set; }

    /// <summary>True if this item is equipped in an equipment slot (slot IDs 0-13).</summary>
    public bool IsEquipped => !string.IsNullOrEmpty(Slot) && 
        (int.TryParse(Slot, out var n) ? n is >= 0 and <= 13 : true);

    /// <summary>Human-readable equipment slot name. From ositools stats::ItemSlot enum.</summary>
    public string EquipmentSlotName => Slot switch
    {
        "0" => "Helmet",  "1" => "Breast",   "2" => "Leggings",
        "3" => "Weapon",  "4" => "Shield",   "5" => "Ring",
        "6" => "Belt",    "7" => "Boots",    "8" => "Gloves",
        "9" => "Amulet",  "10" => "Ring2",   "11" => "Wings",
        "12" => "Horns",  "13" => "Overhead",
        _ => Slot ?? ""
    };

    /// <summary>Number of items inside (for backpacks)</summary>
    public int NumItems { get; set; }

    // ── Generation parameters ─────────────────────────────────
    public string? GenerationBase { get; set; }
    public string? GenerationStatsId { get; set; }
    public List<string> Boosters { get; set; } = [];
    public int? LevelOverride { get; set; }

    // ── Stat overrides (stored directly in save) ──────────────
    /// <summary>Gold value override (-1 = not overridden)</summary>
    public int GoldValueOverwrite { get; set; } = -1;

    /// <summary>Weight value override (-1 = not overridden)</summary>
    public int WeightValueOverwrite { get; set; } = -1;

    /// <summary>Damage type override (empty = not overridden)</summary>
    public string? DamageTypeOverwrite { get; set; }

    /// <summary>Durability / HP (-1 = default)</summary>
    public int HP { get; set; } = -1;

    /// <summary>Actual stats entry name to look up in game data</summary>
    public string? StatsEntryName { get; set; }

    /// <summary>DeltaMods applied to this item (raw strings)</summary>
    public List<string> DeltaMods { get; set; } = [];

    /// <summary>Rune boosts applied (raw strings)</summary>
    public List<string> RuneBoosts { get; set; } = [];

    // ── Resolved stats (from game data, set by StatLookupService) ──
    public ResolvedItemStats? ResolvedStats { get; set; }

    // ── Tree navigation ───────────────────────────────────────
    public List<Item> Children { get; set; } = [];
    public Item? Parent { get; set; }

    // ── Internal: LSLib node references for write-back ────────
    internal LSLib.LS.Node? _itemNode;
    internal LSLib.LS.Node? _statsNode;
}

/// <summary>
/// Computed/resolved item stats from game definition data.
/// Populated by StatLookupService when game data is available.
/// </summary>
public class ResolvedItemStats
{
    /// <summary>Damage range (for weapons)</summary>
    public int MinDamage { get; set; }
    public int MaxDamage { get; set; }

    /// <summary>Damage type (e.g., "Piercing", "Physical")</summary>
    public string? DamageType { get; set; }

    /// <summary>Armor value (for armor items)</summary>
    public int ArmorValue { get; set; }

    /// <summary>Magic armor value (for armor items)</summary>
    public int MagicArmorValue { get; set; }

    /// <summary>Item weight in grams</summary>
    public int Weight { get; set; }

    /// <summary>Gold value</summary>
    public int GoldValue { get; set; }

    /// <summary>Required level to equip</summary>
    public int RequiredLevel { get; set; }

    /// <summary>Durability / max HP</summary>
    public int MaxHP { get; set; }

    /// <summary>Weapon range</summary>
    public float WeaponRange { get; set; }

    /// <summary>Critical chance bonus</summary>
    public int CriticalChance { get; set; }

    /// <summary>All extra/unrecognized stat properties</summary>
    public Dictionary<string, string> ExtraProperties { get; set; } = [];

    /// <summary>Whether stats were successfully resolved</summary>
    public bool IsResolved { get; set; }

    /// <summary>Summary string for display</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (MinDamage > 0 || MaxDamage > 0)
                parts.Add($"Damage: {MinDamage}-{MaxDamage}" + (DamageType != null ? $" ({DamageType})" : ""));
            if (ArmorValue > 0)
                parts.Add($"Armor: {ArmorValue}");
            if (MagicArmorValue > 0)
                parts.Add($"Magic Armor: {MagicArmorValue}");
            if (Weight > 0)
                parts.Add($"Weight: {Weight / 1000f:F2} kg");
            if (GoldValue > 0)
                parts.Add($"Value: {GoldValue} gp");
            if (RequiredLevel > 0)
                parts.Add($"Req. Level: {RequiredLevel}");
            if (MaxHP > 0)
                parts.Add($"Durability: {MaxHP}");
            if (WeaponRange > 0)
                parts.Add($"Range: {WeaponRange:F1}m");
            if (CriticalChance > 0)
                parts.Add($"Crit: {CriticalChance}%");
            return string.Join(" | ", parts);
        }
    }
}

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
    public List<string> Boosters { get; set; } = [];
    public int? LevelOverride { get; set; }

    // ── Tree navigation ───────────────────────────────────────
    public List<Item> Children { get; set; } = [];
    public Item? Parent { get; set; }

    // ── Internal: LSLib node references for write-back ────────
    internal LSLib.LS.Node? _itemNode;
    internal LSLib.LS.Node? _statsNode;
}

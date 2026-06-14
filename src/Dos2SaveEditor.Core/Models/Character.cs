namespace Dos2SaveEditor.Core.Models;

/// <summary>
/// Represents a player character extracted from a DOS2DE save.
/// </summary>
public class Character
{
    // ── Identity ──────────────────────────────────────────────
    public string? Name { get; set; }
    public string? OriginName { get; set; }
    public string? Race { get; set; }
    public string? ClassType { get; set; }
    public string? Map { get; set; }

    // ── Vital stats ───────────────────────────────────────────
    public int Vitality { get; set; }
    public int VitalityMax { get; set; }
    public int Armor { get; set; }
    public int ArmorMax { get; set; }
    public int MagicArmor { get; set; }
    public int MagicArmorMax { get; set; }

    // ── Counters ──────────────────────────────────────────────
    public int DamageCount { get; set; }
    public int HealCount { get; set; }
    public int KillCount { get; set; }

    // ── Progression ───────────────────────────────────────────
    public uint Experience { get; set; }
    public int Level => Utils.GameConstants.LevelFromXp(Experience);

    /// <summary>6 attributes: Strength, Dexterity, Intelligence, Constitution, Memory, Wits</summary>
    public int[] Attributes { get; set; } = new int[6];

    /// <summary>25 abilities (18 combat + 7 civil), in save order</summary>
    public int[] Abilities { get; set; } = new int[25];

    /// <summary>The 4 talent entries (talent IDs as strings)</summary>
    public string?[] Talents { get; set; } = new string?[4];

    // ── Points ────────────────────────────────────────────────
    public int AttributePoints { get; set; }
    public int CombatAbilityPoints { get; set; }
    public int CivilAbilityPoints { get; set; }
    public int TalentPoints { get; set; }

    // ── Tags ──────────────────────────────────────────────────
    public List<string> Tags { get; set; } = [];

    // ── Inventory ─────────────────────────────────────────────
    public string? InventoryUuid { get; set; }

    // ── Internal references (for LSLib node mapping) ──────────
    /// <summary>Index in the Characters region node list</summary>
    internal int NodeIndex { get; set; }
}

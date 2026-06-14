namespace Dos2SaveEditor.Core.Utils;

/// <summary>
/// Game constants ported from the original DOS2 Savegame Editor.
/// </summary>
public static class GameConstants
{
    // ── Level bounds ──────────────────────────────────────────
    public const int LevelMin = 1;
    public const int LevelMax = 35;

    // ── Attribute bounds ──────────────────────────────────────
    public const int AttributeMin = 1;
    public const int AttributeMax = 40;
    public const int AttributeOverride = 999; // Uncapped value

    // ── Ability bounds ────────────────────────────────────────
    public const int AbilityOverride = 99;

    // ── Point bounds ──────────────────────────────────────────
    public const int PointsMin = 0;
    public const int PointsMax = 99;

    // ── XP table (index 0 = level 1, index 34 = level 35) ────
    public static readonly uint[] XpTable =
    [
        0,           // Level 1
        2_000,       // Level 2
        8_000,       // Level 3
        20_000,      // Level 4
        40_000,      // Level 5
        70_000,      // Level 6
        112_000,     // Level 7
        168_000,     // Level 8
        240_000,     // Level 9
        340_000,     // Level 10
        479_000,     // Level 11
        672_000,     // Level 12
        941_000,     // Level 13
        1_315_000,   // Level 14
        1_834_000,   // Level 15
        2_556_000,   // Level 16
        3_559_000,   // Level 17
        4_954_000,   // Level 18
        6_893_000,   // Level 19
        9_588_000,   // Level 20
        13_334_000,  // Level 21
        18_540_000,  // Level 22
        25_777_000,  // Level 23
        35_836_000,  // Level 24
        49_818_000,  // Level 25
        69_253_000,  // Level 26
        96_268_000,  // Level 27
        133_818_000, // Level 28
        186_013_000, // Level 29
        258_564_000, // Level 30
        499_586_000, // Level 31
        694_430_000, // Level 32
        965_264_000, // Level 33
        1_341_723_000, // Level 34
        uint.MaxValue // Level 35 (max)
    ];

    /// <summary>Calculate level from XP amount.</summary>
    public static int LevelFromXp(uint xp)
    {
        for (int i = 0; i < XpTable.Length - 1; i++)
        {
            if (xp < XpTable[i + 1])
                return i + 1;
        }
        return LevelMax;
    }

    /// <summary>Get XP required to reach a given level.</summary>
    public static uint XpForLevel(int level)
    {
        int idx = Math.Clamp(level - 1, 0, XpTable.Length - 1);
        return XpTable[idx];
    }

    // ── Attribute names (in save order) ───────────────────────
    public static readonly string[] AttributeNames =
        ["Strength", "Dexterity", "Intelligence", "Constitution", "Memory", "Wits"];

    // ── Combat ability names (in save order, 18 entries) ─────────
    public static readonly string[] CombatAbilityNames =
    [
        "DualWielding",     // [0]
        "TwoHanded",        // [1]
        "Ranged",           // [2]
        "SingleHanded",     // [3]
        "Shield",           // [4]
        "PainReflection",   // [5]
        "Leadership",       // [6]
        "Perseverance",     // [7]
        "Warfare",          // [8]
        "Aerotheurge",      // [9]
        "Huntsman",         // [10]
        "Scoundrel",        // [11]
        "Geomancer",        // [12]
        "Hydrosophist",     // [13]
        "Summoning",        // [14]
        "Polymorph",        // [15]
        "Necromancy",       // [16]
        "Pyrokinetic"       // [17]
    ];

    // ── Civil ability names (in save order) ───────────────────
    public static readonly string[] CivilAbilityNames =
    [
        "Bartering",
        "Persuasion",
        "LuckyCharm",
        "Loremaster",
        "Telekinesis",
        "Sneaking",
        "Thievery"
    ];

    // ── All abilities (combat + civil, in save order) ──────────
    public static readonly string[] AllAbilityNames =
        CombatAbilityNames.Concat(CivilAbilityNames).ToArray();

    // ── Talent point type names ────────────────────────────────
    public static readonly string[] PointTypeNames =
        ["AttributePoints", "CombatAbilityPoints", "CivilAbilityPoints", "TalentPoints"];

    // ── Item type mappings ────────────────────────────────────
    public enum Dos2ItemKind
    {
        Item,
        Armor,
        Weapon,
        Accessory
    }

    // ── Equipped slot constants ────────────────────────────────
    public const int EquippedSlotMax = 10;
    public const int RuneSlotCount = 3;
}

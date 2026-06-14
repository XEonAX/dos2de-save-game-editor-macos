namespace Dos2SaveEditor.Core.Models;

/// <summary>
/// Represents a mod dependency in a save file.
/// </summary>
public class ModEntry
{
    /// <summary>Mod UUID</summary>
    public string Uuid { get; set; } = "";

    /// <summary>Mod name (if resolvable)</summary>
    public string? Name { get; set; }

    /// <summary>Mod version</summary>
    public string? Version { get; set; }

    /// <summary>Whether the mod data is still present in the save</summary>
    public bool HasData { get; set; } = true;
}

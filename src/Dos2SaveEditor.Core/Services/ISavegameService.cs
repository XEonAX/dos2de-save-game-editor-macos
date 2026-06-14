using Dos2SaveEditor.Core.Models;

namespace Dos2SaveEditor.Core.Services;

/// <summary>
/// Service for loading, parsing, and saving DOS2DE save files via LSLib.
/// </summary>
public interface ISavegameService
{
    /// <summary>Open a .lsv save file and extract metadata.</summary>
    Task<SavegameInfo> OpenSaveAsync(string filePath);

    /// <summary>Extract all player characters from the save.</summary>
    List<Character> GetCharacters(SavegameInfo save);

    /// <summary>Extract all items belonging to a character's inventory.</summary>
    List<Item> GetInventory(SavegameInfo save, Character character);

    /// <summary>Extract all mod dependencies from the save's meta.lsf.</summary>
    List<ModEntry> GetMods(SavegameInfo save);

    /// <summary>Save changes back to an .lsv file. Creates a .bak of the original.</summary>
    Task SaveAsync(SavegameInfo save, string outputPath);

    /// <summary>Update a character's data in the resource tree.</summary>
    void UpdateCharacter(SavegameInfo save, Character character);

    /// <summary>Update an item's data in the resource tree.</summary>
    void UpdateItem(SavegameInfo save, Item item);

    /// <summary>Remove a mod's data from the save.</summary>
    void RemoveMod(SavegameInfo save, ModEntry mod);

    /// <summary>Dispose the save (releases file handles).</summary>
    void Close(SavegameInfo save);

    /// <summary>Get the stat lookup service for resolving item stats.</summary>
    StatLookupService Stats { get; }

    /// <summary>Try to load game stat definitions from the given directory.</summary>
    bool TryLoadStats(string? dataPath);

    /// <summary>Get the last stat-load diagnostic log.</summary>
    IReadOnlyList<string> GetStatLoadLog();
}

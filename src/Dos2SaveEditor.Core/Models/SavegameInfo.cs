using LSLib.LS;
using LSLib.LS.Enums;

namespace Dos2SaveEditor.Core.Models;

/// <summary>
/// Savegame metadata extracted from an LSV package.
/// </summary>
public class SavegameInfo
{
    public string FilePath { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>Game version (e.g., "3.6.117.373")</summary>
    public string? GameVersion { get; set; }

    /// <summary>Game difficulty</summary>
    public string? Difficulty { get; set; }

    /// <summary>In-game play time</summary>
    public string? GameTime { get; set; }

    /// <summary>Save date/time</summary>
    public DateTime? SaveDate { get; set; }

    /// <summary>Number of player characters</summary>
    public int CharacterCount { get; set; }

    /// <summary>List of mod UUIDs the save depends on</summary>
    public List<string> ModUuids { get; set; } = [];

    /// <summary>Globals resource (the parsed globals.lsf)</summary>
    public Resource? Globals { get; set; }

    /// <summary>Raw LSLib Package reference</summary>
    public Package? Package { get; set; }

    /// <summary>Game enum for version-specific serialization</summary>
    public Game Game { get; set; } = Game.DivinityOriginalSin2DE;
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dos2SaveEditor.Core.Models;
using LSLib.LS;

namespace Dos2SaveEditor.Core.Services;

/// <summary>
/// Lightweight parser for DOS2DE stat definition files.
/// Parses the game's stat .txt files from Shared/Stats/Generated/Data/.
/// </summary>
public class StatLookupService
{
    /// <summary>All loaded stat entries, keyed by stat name.</summary>
    private readonly Dictionary<string, StatEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether any stat data has been loaded.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Number of loaded entries.</summary>
    public int EntryCount => _entries.Count;

    /// <summary>Path to the game's stat data directory.</summary>
    public string? DataPath { get; private set; }

    /// <summary>Diagnostic log of the last load attempt.</summary>
    public List<string> LoadLog { get; } = [];

    // Regex patterns for parsing stat files
    private static readonly Regex NewEntryRegex = new(
        @"^new\s+(\w+)\s+""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DataPropertyRegex = new(
        @"^data\s+""([^""]+)""\s+""(.*)""\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UsingRegex = new(
        @"^using\s+""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Load all stat definition files from a directory or .pak archives within it.
    /// </summary>
    public void LoadFromDirectory(string dataPath)
    {
        DataPath = dataPath;
        _entries.Clear();
        LoadLog.Clear();

        Log($"StatLookup: attempting to load from '{dataPath}'");

        if (!Directory.Exists(dataPath))
        {
            Log($"StatLookup: directory does NOT exist: '{dataPath}'");
            return;
        }

        var txtFiles = Directory.GetFiles(dataPath, "*.txt", SearchOption.TopDirectoryOnly);
        Log($"StatLookup: found {txtFiles.Length} .txt files in directory");

        int filesParsed = 0;
        int filesFailed = 0;

        foreach (var file in txtFiles)
        {
            try
            {
                var countBefore = _entries.Count;
                ParseStatFile(file);
                var added = _entries.Count - countBefore;
                if (added > 0)
                {
                    filesParsed++;
                    Log($"StatLookup: parsed '{Path.GetFileName(file)}' — {added} entries");
                }
            }
            catch (Exception ex)
            {
                filesFailed++;
                Log($"StatLookup: FAILED to parse '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        // If no loose .txt files found, try .pak archives in the directory
        if (_entries.Count == 0)
        {
            var pakFiles = Directory.GetFiles(dataPath, "*.pak", SearchOption.TopDirectoryOnly);
            Log($"StatLookup: no .txt results, trying {pakFiles.Length} .pak files");

            foreach (var pakFile in pakFiles)
            {
                try
                {
                    var pakName = Path.GetFileName(pakFile);
                    // Only open data paks (skip texture/sound paks for speed)
                    if (pakName.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
                        pakName.Contains("Sound", StringComparison.OrdinalIgnoreCase) ||
                        pakName.Contains("Music", StringComparison.OrdinalIgnoreCase) ||
                        pakName.Contains("Video", StringComparison.OrdinalIgnoreCase) ||
                        pakName.Contains("Voice", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var countBefore = _entries.Count;
                    LoadFromPakFile(pakFile);
                    var added = _entries.Count - countBefore;
                    if (added > 0)
                    {
                        filesParsed++;
                        Log($"StatLookup: parsed '{pakName}' — {added} entries");
                    }
                }
                catch (Exception ex)
                {
                    filesFailed++;
                    Log($"StatLookup: FAILED to parse '{Path.GetFileName(pakFile)}': {ex.Message}");
                }
            }
        }

        // Resolve inheritance (using)
        Log($"StatLookup: resolving inheritance across {_entries.Count} entries...");
        ResolveInheritance();

        IsLoaded = _entries.Count > 0;
        Log($"StatLookup: DONE — {filesParsed} files parsed, {filesFailed} failed, {_entries.Count} total entries loaded");
    }

    /// <summary>
    /// Load stat entries from a .pak archive. Extracts .txt files matching
    /// *Stats/Generated/Data/*.txt paths from inside the package.
    /// </summary>
    public void LoadFromPakFile(string pakPath)
    {
        Log($"StatLookup: opening .pak: '{Path.GetFileName(pakPath)}'");

        var packageReader = new PackageReader();
        var package = packageReader.Read(pakPath);

        var statFiles = package.Files
            .Where(f => f.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .Where(f => f.Name.Contains("Stats", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Log($"StatLookup: found {statFiles.Count} potential stat .txt files in .pak");

        foreach (var fileInfo in statFiles)
        {
            try
            {
                var countBefore = _entries.Count;
                using var stream = fileInfo.CreateContentReader();
                using var textReader = new StreamReader(stream);
                ParseStatStream(textReader, fileInfo.Name);
                var added = _entries.Count - countBefore;
                if (added > 0)
                {
                    Log($"StatLookup: parsed '{fileInfo.Name}' from .pak — {added} entries");
                }
            }
            catch (Exception ex)
            {
                Log($"StatLookup: FAILED to parse '{fileInfo.Name}' from .pak: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Parse stat entries from a text stream (for .pak embedded files).
    /// </summary>
    private void ParseStatStream(TextReader reader, string sourceName)
    {
        StatEntry? currentEntry = null;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("#"))
                continue;

            // Check for new entry
            var newMatch = NewEntryRegex.Match(trimmed);
            if (newMatch.Success)
            {
                if (currentEntry != null)
                    _entries[currentEntry.Name] = currentEntry;

                var typeName = newMatch.Groups[1].Value;
                var entryName = newMatch.Groups[2].Value;
                currentEntry = new StatEntry { Name = entryName, Type = typeName };
                continue;
            }

            if (currentEntry == null) continue;

            // data "Key" "Value"
            var dataMatch = DataPropertyRegex.Match(trimmed);
            if (dataMatch.Success)
            {
                currentEntry.Properties[dataMatch.Groups[1].Value] = dataMatch.Groups[2].Value;
                continue;
            }

            // using "BaseName"
            var usingMatch = UsingRegex.Match(trimmed);
            if (usingMatch.Success)
            {
                currentEntry.Properties["using"] = usingMatch.Groups[1].Value;
                continue;
            }
        }

        if (currentEntry != null)
            _entries[currentEntry.Name] = currentEntry;
    }

    private void ParseStatFile(string filePath)
    {
        StatEntry? currentEntry = null;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("#"))
                continue;

            // Check for new entry
            var newMatch = NewEntryRegex.Match(line);
            if (newMatch.Success)
            {
                // Store previous entry
                if (currentEntry != null)
                {
                    _entries[currentEntry.Name] = currentEntry;
                }

                var typeName = newMatch.Groups[1].Value;
                var entryName = newMatch.Groups[2].Value;
                currentEntry = new StatEntry
                {
                    Name = entryName,
                    Type = typeName
                };
                continue;
            }

            if (currentEntry == null) continue;

            // Check for data property: data "Key" "Value"
            var dataMatch = DataPropertyRegex.Match(line);
            if (dataMatch.Success)
            {
                var key = dataMatch.Groups[1].Value;
                var value = dataMatch.Groups[2].Value;
                currentEntry.Properties[key] = value;
                continue;
            }

            // Check for using (inheritance): using "BaseName"
            var usingMatch = UsingRegex.Match(line);
            if (usingMatch.Success)
            {
                var baseName = usingMatch.Groups[1].Value;
                currentEntry.Properties["using"] = baseName;
                continue;
            }
        }

        // Store last entry
        if (currentEntry != null)
        {
            _entries[currentEntry.Name] = currentEntry;
        }
    }

    private void ResolveInheritance()
    {
        foreach (var entry in _entries.Values)
        {
            if (entry.Properties.TryGetValue("using", out var baseName) ||
                entry.Properties.TryGetValue("Using", out baseName))
            {
                if (_entries.TryGetValue(baseName, out var baseEntry))
                {
                    // Inherit properties from base (base properties are overridden by child)
                    foreach (var kvp in baseEntry.Properties)
                    {
                        if (!entry.Properties.ContainsKey(kvp.Key))
                        {
                            entry.Properties[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
        }
    }

    private void Log(string message)
    {
        LoadLog.Add(message);
        Debug.WriteLine(message);
    }

    /// <summary>
    /// Look up a stat entry by name.
    /// </summary>
    public StatEntry? Lookup(string? statsId)
    {
        if (string.IsNullOrEmpty(statsId)) return null;
        if (_entries.TryGetValue(statsId, out var entry))
            return entry;
        Log($"StatLookup: entry NOT found for '{statsId}'");
        return null;
    }

    /// <summary>
    /// Resolve an item's stats from game data.
    /// Uses the StatsId or StatsEntryName, applies overrides and delta mods.
    /// </summary>
    public ResolvedItemStats? ResolveItemStats(Item item)
    {
        var statsId = item.StatsEntryName ?? item.StatsId;
        var entry = Lookup(statsId);
        if (entry == null) return null;

        var stats = new ResolvedItemStats { IsResolved = true };

        // Parse damage
        if (entry.TryGetInt("MinDamage", out var minDmg))
            stats.MinDamage = minDmg;
        if (entry.TryGetInt("MaxDamage", out var maxDmg))
            stats.MaxDamage = maxDmg;
        if (entry.TryGetString("DamageType", out var dmgType))
            stats.DamageType = dmgType;

        // Armor values
        if (entry.TryGetInt("ArmorValue", out var armor))
            stats.ArmorValue = armor;
        if (entry.TryGetInt("MagicArmorValue", out var magicArmor))
            stats.MagicArmorValue = magicArmor;

        // Basic properties
        if (entry.TryGetInt("Weight", out var weight))
            stats.Weight = weight;
        if (entry.TryGetInt("Value", out var value))
            stats.GoldValue = value;
        if (entry.TryGetInt("RequiredLevel", out var reqLevel))
            stats.RequiredLevel = reqLevel;
        if (entry.TryGetInt("Durability", out var durability))
            stats.MaxHP = durability;
        if (entry.TryGetFloat("WeaponRange", out var range))
            stats.WeaponRange = range;
        if (entry.TryGetInt("CriticalChance", out var crit))
            stats.CriticalChance = crit;

        // Apply overrides from the save file
        if (item.GoldValueOverwrite >= 0)
            stats.GoldValue = item.GoldValueOverwrite;
        if (item.WeightValueOverwrite >= 0)
            stats.Weight = item.WeightValueOverwrite;
        if (item.DamageTypeOverwrite != null)
            stats.DamageType = item.DamageTypeOverwrite;
        if (item.HP >= 0)
            stats.MaxHP = item.HP;

        // Collect extra properties for display
        foreach (var kvp in entry.Properties)
        {
            if (kvp.Key is "MinDamage" or "MaxDamage" or "DamageType" or
                "ArmorValue" or "MagicArmorValue" or "Weight" or "Value" or
                "RequiredLevel" or "Durability" or "WeaponRange" or "CriticalChance" or
                "using" or "Using" or "type" or "Type" or "Name" or "name")
                continue;
            stats.ExtraProperties[kvp.Key] = kvp.Value;
        }

        return stats;
    }
}

/// <summary>
/// Represents a single stat entry from the game data files.
/// </summary>
public class StatEntry
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public Dictionary<string, string> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetInt(string key, out int value)
    {
        if (Properties.TryGetValue(key, out var str) && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        value = 0;
        return false;
    }

    public bool TryGetFloat(string key, out float value)
    {
        if (Properties.TryGetValue(key, out var str) && float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        value = 0;
        return false;
    }

    public bool TryGetString(string key, out string value)
    {
        if (Properties.TryGetValue(key, out var str) && !string.IsNullOrEmpty(str))
        {
            value = str;
            return true;
        }
        value = "";
        return false;
    }
}

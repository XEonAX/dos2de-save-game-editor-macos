using Dos2SaveEditor.Core.Models;
using LSLib.LS;
using LSLib.LS.Enums;
using LSLib.LS.Save;

namespace Dos2SaveEditor.Core.Services;

/// <summary>
/// Implements savegame loading/saving using LSLib.
/// </summary>
public class SavegameService : ISavegameService
{
    /// <inheritdoc/>
    public StatLookupService Stats { get; } = new();

    /// <inheritdoc/>
    public bool TryLoadStats(string? dataPath)
    {
        System.Diagnostics.Debug.WriteLine($"SavegameService: TryLoadStats called with '{dataPath}'");
        if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath))
        {
            System.Diagnostics.Debug.WriteLine($"SavegameService: path null or doesn't exist, skipping");
            return false;
        }
        Stats.LoadFromDirectory(dataPath);
        System.Diagnostics.Debug.WriteLine($"SavegameService: Stats.IsLoaded={Stats.IsLoaded}, EntryCount={Stats.EntryCount}");
        return Stats.IsLoaded;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetStatLoadLog() => Stats.LoadLog;

    // ── Open ─────────────────────────────────────────────────────

    public async Task<SavegameInfo> OpenSaveAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var info = new SavegameInfo { FilePath = filePath };

            var reader = new PackageReader();
            info.Package = reader.Read(filePath);
            info.Game = Game.DivinityOriginalSin2DE;

            // Load globals.lsf
            var globalsInfo = info.Package.Files.FirstOrDefault(
                f => f.Name.Equals("globals.lsf", StringComparison.OrdinalIgnoreCase));
            if (globalsInfo == null)
                throw new InvalidDataException("Not a valid savegame: globals.lsf not found");

            using var rsrcStream = globalsInfo.CreateContentReader();
            using var rsrcReader = new LSFReader(rsrcStream);
            info.Globals = rsrcReader.Read();

            // Try loading meta.lsf for mods and metadata
            LoadMeta(info);

            // Count characters
            try
            {
                var chars = GetCharacterNodes(info);
                info.CharacterCount = chars.Count;
            }
            catch
            {
                info.CharacterCount = 0;
            }

            return info;
        });
    }

    // ── Characters ───────────────────────────────────────────────

    private static List<Node> GetCharacterNodes(SavegameInfo save)
    {
        if (save.Globals == null)
            return [];
        if (!save.Globals.Regions.TryGetValue("Characters", out var charsRegion))
            return [];
        if (!charsRegion.Children.TryGetValue("CharacterFactory", out var cfList) || cfList.Count == 0)
            return [];
        if (!cfList[0].Children.TryGetValue("Characters", out var charsList) || charsList.Count == 0)
            return [];
        if (!charsList[0].Children.TryGetValue("Character", out var characterList))
            return [];
        return characterList;
    }

    public List<Character> GetCharacters(SavegameInfo save)
    {
        var characters = new List<Character>();
        var charNodes = GetCharacterNodes(save);

        for (int i = 0; i < charNodes.Count; i++)
        {
            var charNode = charNodes[i];
            var c = new Character { NodeIndex = i };

            // ── IsPlayer filter: only process player characters ──
            if (!charNode.Children.TryGetValue("Stats", out var statsList) || statsList.Count == 0)
                continue;
            var statsNode = statsList[0];
            bool isPlayer = false;
            if (statsNode.Attributes.TryGetValue("IsPlayer", out var isPlayerAttr))
            {
                var val = isPlayerAttr.Value;
                if (val is bool b) isPlayer = b;
                else if (val is string s) isPlayer = s.Equals("True", StringComparison.OrdinalIgnoreCase) || s == "1";
                else isPlayer = Convert.ToBoolean(val);
            }
            if (!isPlayer)
                continue;

            // ── Stats attributes (from character node directly, NOT Stats child!) ──
            ReadAttrString(charNode, "Level", v => c.Map = v);
            ReadAttrInt(charNode, "Vitality", v => c.Vitality = v);
            ReadAttrInt(charNode, "MaxVitalityPatchCheck", v => c.VitalityMax = v);
            ReadAttrInt(charNode, "Armor", v => c.Armor = v);
            ReadAttrInt(charNode, "MaxArmorPatchCheck", v => c.ArmorMax = v);
            ReadAttrInt(charNode, "MagicArmor", v => c.MagicArmor = v);
            ReadAttrInt(charNode, "MaxMagicArmorPatchCheck", v => c.MagicArmorMax = v);
            ReadAttrInt(charNode, "DamageCounter", v => c.DamageCount = v);
            ReadAttrInt(charNode, "HealCounter", v => c.HealCount = v);
            ReadAttrInt(charNode, "KillCounter", v => c.KillCount = v);
            ReadAttrULongLong(charNode, "Inventory", v => c.InventoryUuid = v.ToString());

            // Experience is in Stats child node (not on character directly)
            ReadAttrInt(statsNode, "Experience", v => c.Experience = (uint)v);

            // Name fallback: OriginalTransformDisplayName on character node
            if (string.IsNullOrEmpty(c.Name))
                ReadAttrTranslatedString(charNode, "OriginalTransformDisplayName", v => c.Name = v);

            // ── PlayerData → PlayerCustomData (Name, Race, Class, Origin) ──
            if (charNode.Children.TryGetValue("PlayerData", out var pdList) && pdList.Count > 0)
            {
                var playerData = pdList[0];
                if (playerData.Children.TryGetValue("PlayerCustomData", out var pcdList) && pcdList.Count > 0)
                {
                    var customData = pcdList[0];
                    ReadAttrString(customData, "Name", v => c.Name = v);
                    ReadAttrString(customData, "OriginName", v => c.OriginName = v);
                    ReadAttrString(customData, "Race", v => c.Race = v);
                    ReadAttrString(customData, "ClassType", v => c.ClassType = v);
                }
            }

            // ── PlayerUpgrade is a DIRECT child of character (not under PlayerData!) ──
            if (charNode.Children.TryGetValue("PlayerUpgrade", out var puList) && puList.Count > 0)
            {
                var upgrade = puList[0];
                ReadAttrInt(upgrade, "AttributePoints", v => c.AttributePoints = v);
                ReadAttrInt(upgrade, "CombatAbilityPoints", v => c.CombatAbilityPoints = v);
                ReadAttrInt(upgrade, "CivilAbilityPoints", v => c.CivilAbilityPoints = v);
                ReadAttrInt(upgrade, "TalentPoints", v => c.TalentPoints = v);

                // ── Spent points: iterate children of PlayerUpgrade ──
                // Structure: 6 attribute nodes + 40 ability nodes + 4 talent nodes
                // Each has Attributes["Object"] = value (could be int, string, or TranslatedString)
                int attrIdx = 0, abilIdx = 0, talIdx = 0;
                foreach (var childList in upgrade.Children.Values)
                {
                    foreach (var childNode in childList)
                    {
                        if (childNode.Attributes.TryGetValue("Object", out var objAttr))
                        {
                            var rawValue = objAttr.Value;
                            if (rawValue is int intVal)
                            {
                                if (attrIdx < 6)
                                    c.Attributes[attrIdx++] = intVal;
                                else if (abilIdx < 25)
                                    c.Abilities[abilIdx++] = intVal;
                                else if (talIdx < 4)
                                    c.Talents[talIdx++] = rawValue.ToString();
                            }
                            else if (rawValue is uint uintVal)
                            {
                                int val = (int)uintVal;
                                if (attrIdx < 6)
                                    c.Attributes[attrIdx++] = val;
                                else if (abilIdx < 25)
                                    c.Abilities[abilIdx++] = val;
                                else if (talIdx < 4)
                                    c.Talents[talIdx++] = rawValue.ToString();
                            }
                            else if (rawValue is string strVal)
                            {
                                // Talent names or other string values
                                if (talIdx < 4)
                                    c.Talents[talIdx++] = strVal;
                                else if (attrIdx < 6)
                                    c.Attributes[attrIdx++] = int.TryParse(strVal, out var p) ? p : 0;
                                else if (abilIdx < 25)
                                    c.Abilities[abilIdx++] = int.TryParse(strVal, out var a) ? a : 0;
                            }
                            else
                            {
                                // Skip non-primitive values (TranslatedString, etc.)
                            }
                        }
                    }
                }
            }

            // ── Tags ──
            if (charNode.Children.TryGetValue("Tags", out var tagsList) && tagsList.Count > 0)
            {
                var tagsNode = tagsList[0];
                foreach (var childList in tagsNode.Children.Values)
                {
                    foreach (var tagNode in childList)
                    {
                        if (tagNode.Attributes.TryGetValue("Object", out var tagAttr))
                            c.Tags.Add(tagAttr.Value?.ToString() ?? "");
                    }
                }
            }

            characters.Add(c);
        }

        return characters;
    }

    // ── Inventory ────────────────────────────────────────────────

    public List<Item> GetInventory(SavegameInfo save, Character character)
    {
        var items = new List<Item>();
        if (string.IsNullOrEmpty(character.InventoryUuid))
            return items;

        var allItemNodes = save.Globals!.Regions["Items"]
            .Children["ItemFactory"][0]
            .Children["Items"][0]
            .Children["Item"];

        // Find all items whose "Parent" attribute matches the character's inventory UUID
        foreach (var itemNode in allItemNodes)
        {
            ReadAttrString(itemNode, "Parent", parent =>
            {
                if (parent == character.InventoryUuid)
                {
                    var item = ParseItem(itemNode);
                    items.Add(item);
                }
            });
        }

        // Build parent-child hierarchy (backpacks)
        BuildItemHierarchy(items, allItemNodes);

        return items;
    }

    private Item ParseItem(Node itemNode)
    {
        var item = new Item { _itemNode = itemNode };

        ReadAttrString(itemNode, "Stats", v => item.StatsId = v);
        ReadAttrString(itemNode, "Handle", v => item.Handle = v);
        ReadAttrInt(itemNode, "Amount", v => item.Amount = v > 0 ? v : 1);
        ReadAttrString(itemNode, "Slot", v => item.Slot = v);
        ReadAttrString(itemNode, "Inventory", v => item.InventoryUuid = v);
        ReadAttrBool(itemNode, "IsGenerated", v => item.IsGenerated = v);

        // ── Stat overrides on item node ──
        ReadAttrInt(itemNode, "GoldValueOverwrite", v => item.GoldValueOverwrite = v);
        ReadAttrInt(itemNode, "WeightValueOverwrite", v => item.WeightValueOverwrite = v);
        ReadAttrString(itemNode, "DamageTypeOverwrite", v => item.DamageTypeOverwrite = v);
        ReadAttrInt(itemNode, "HP", v => item.HP = v);

        // Detect backpack (non-null, non-empty Inventory attribute)
        if (!string.IsNullOrEmpty(item.InventoryUuid) && item.InventoryUuid != "00000000-0000-0000-0000-000000000000")
            item.IsBackpack = true;

        // ── Stats sub-node (ItemType, Level, Runes, etc.) ──
        if (itemNode.Children.TryGetValue("Stats", out var statsList) && statsList.Count > 0)
        {
            var statsNode = statsList[0];
            item._statsNode = statsNode;
            ReadAttrString(statsNode, "ItemType", v => item.Kind = v switch
            {
                "Armor" => Utils.GameConstants.Dos2ItemKind.Armor,
                "Weapon" => Utils.GameConstants.Dos2ItemKind.Weapon,
                _ => Utils.GameConstants.Dos2ItemKind.Item
            });
            ReadAttrInt(statsNode, "Level", v => item.Level = v);
            ReadAttrInt(statsNode, "LevelGroupIndex", v => item.LevelGroupIndex = v);
            ReadAttrInt(statsNode, "NameIndex", v => item.NameIndex = v);
            ReadAttrBool(statsNode, "CustomBaseStats", v => item.HasCustomBase = v);
            ReadAttrString(statsNode, "StatsEntryName", v => item.StatsEntryName = v);
            ReadAttrString(statsNode, "GenerationStatsId", v => item.GenerationStatsId = v);

            // Rune slots
            int runeIdx = 0;
            foreach (var childKey in statsNode.Children.Keys)
            {
                foreach (var child in statsNode.Children[childKey])
                {
                    if (runeIdx >= 3) break;
                    if (child.Attributes.TryGetValue("RuneStatsID", out var runeAttr))
                        item.Runes[runeIdx++] = runeAttr.Value?.ToString();
                }
            }

            // DeltaMods
            if (statsNode.Children.TryGetValue("DeltaMods", out var dmList))
            {
                foreach (var dmNode in dmList)
                {
                    if (dmNode.Attributes.TryGetValue("DeltaMod", out var dmAttr) && dmAttr.Value is string dmStr)
                        item.DeltaMods.Add(dmStr);
                    else if (dmNode.Attributes.TryGetValue("Object", out var objAttr) && objAttr.Value is string objStr)
                        item.DeltaMods.Add(objStr);
                }
            }

            // RuneBoosts
            if (statsNode.Children.TryGetValue("RuneBoostSet", out var rbList))
            {
                foreach (var rbNode in rbList)
                {
                    if (rbNode.Attributes.TryGetValue("Object", out var objAttr) && objAttr.Value is string objStr)
                        item.RuneBoosts.Add(objStr);
                }
            }
        }

        // ── Custom name/description ──
        if (itemNode.Children.TryGetValue("CustomName", out var cnList) && cnList.Count > 0)
            ReadAttrString(cnList[0], "CustomDisplayName", v => item.DisplayName = v);
        if (itemNode.Children.TryGetValue("CustomDescription", out var cdList) && cdList.Count > 0)
            ReadAttrString(cdList[0], "CustomDescription", v => item.Description = v);

        // ── Boosters (ItemGenerator) ──
        if (itemNode.Children.TryGetValue("ItemGenerator", out var igList) && igList.Count > 0)
        {
            ParseBoosters(igList[0], item.Boosters);
        }

        return item;
    }

    private void ParseBoosters(Node generatorNode, List<string> boosters)
    {
        foreach (var childKey in generatorNode.Children.Keys)
        {
            foreach (var child in generatorNode.Children[childKey])
            {
                if (child.Attributes.TryGetValue("Object", out var objAttr) && objAttr.Value is string s)
                    boosters.Add(s);
            }
        }
    }

    private void BuildItemHierarchy(List<Item> items, List<Node> allItemNodes)
    {
        foreach (var item in items)
        {
            if (item.IsBackpack && !string.IsNullOrEmpty(item.InventoryUuid))
            {
                foreach (var childNode in allItemNodes)
                {
                    ReadAttrString(childNode, "Parent", parent =>
                    {
                        if (parent == item.InventoryUuid)
                        {
                            var childItem = ParseItem(childNode);
                            childItem.Parent = item;
                            item.Children.Add(childItem);
                        }
                    });
                }
            }
        }
    }

    // ── Mods ─────────────────────────────────────────────────────

    public List<ModEntry> GetMods(SavegameInfo save)
    {
        var mods = new List<ModEntry>();

        if (save.Globals == null) return mods;

        // Mods are usually in meta.lsf region MetaData
        try
        {
            if (save.Globals.Regions.TryGetValue("MetaData", out var metaRegion))
            {
                var metaNode = metaRegion.Children["MetaData"][0];
                if (metaNode.Children.TryGetValue("ModuleSettings", out var msList) && msList.Count > 0)
                {
                    var modSettings = msList[0];
                    if (modSettings.Children.TryGetValue("Mods", out var modsList) && modsList.Count > 0)
                    {
                        var modsNode = modsList[0];
                        if (modsNode.Children.TryGetValue("ModuleShortDesc", out var modDescList))
                        {
                            foreach (var modNode in modDescList)
                            {
                                var entry = new ModEntry();
                                ReadAttrString(modNode, "UUID", v => entry.Uuid = v);
                                ReadAttrString(modNode, "Name", v => entry.Name = v);
                                ReadAttrString(modNode, "Version", v => entry.Version = v);
                                if (!string.IsNullOrEmpty(entry.Uuid))
                                    mods.Add(entry);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // MetaData may not exist in all saves
        }

        return mods;
    }

    // ── Update ────────────────────────────────────────────────────

    public void UpdateCharacter(SavegameInfo save, Character character)
    {
        if (save.Globals == null)
            throw new InvalidOperationException("Save globals have been unloaded. Re-open the save file.");

        var charNodes = GetCharacterNodes(save);
        if (charNodes.Count == 0)
            throw new InvalidOperationException("No character nodes found in save data.");
        if (character.NodeIndex >= charNodes.Count)
            throw new ArgumentOutOfRangeException(nameof(character), "Character index out of range");

        var node = charNodes[character.NodeIndex];

        // Write to character node directly (VIT/ARM/Inventory)
        WriteAttrInt(node, "Vitality", character.Vitality);
        WriteAttrInt(node, "MaxVitalityPatchCheck", character.VitalityMax);
        WriteAttrInt(node, "Armor", character.Armor);
        WriteAttrInt(node, "MaxArmorPatchCheck", character.ArmorMax);
        WriteAttrInt(node, "MagicArmor", character.MagicArmor);
        WriteAttrInt(node, "MaxMagicArmorPatchCheck", character.MagicArmorMax);
        WriteAttrInt(node, "DamageCounter", character.DamageCount);
        WriteAttrInt(node, "HealCounter", character.HealCount);
        WriteAttrInt(node, "KillCounter", character.KillCount);

        // Experience goes to Stats child node
        if (node.Children.TryGetValue("Stats", out var statsList) && statsList.Count > 0)
            WriteAttrInt(statsList[0], "Experience", (int)character.Experience);

        // Update PlayerUpgrade points (DIRECT child of character, not under PlayerData!)
        if (node.Children.TryGetValue("PlayerUpgrade", out var puList) && puList.Count > 0)
        {
            var upgrade = puList[0];
            WriteAttrInt(upgrade, "AttributePoints", character.AttributePoints);
            WriteAttrInt(upgrade, "CombatAbilityPoints", character.CombatAbilityPoints);
            WriteAttrInt(upgrade, "CivilAbilityPoints", character.CivilAbilityPoints);
            WriteAttrInt(upgrade, "TalentPoints", character.TalentPoints);
        }
    }

    public void UpdateItem(SavegameInfo save, Item item)
    {
        if (item._itemNode == null) return;
        var node = item._itemNode;

        // Write basic attributes to the item node
        WriteAttrString(node, "Stats", item.StatsId ?? "");
        WriteAttrInt(node, "Amount", item.Amount);
        WriteAttrString(node, "Slot", item.Slot ?? "");
        WriteAttrBool(node, "IsGenerated", item.IsGenerated);

        // Write stat overrides on item node
        WriteAttrInt(node, "GoldValueOverwrite", item.GoldValueOverwrite);
        WriteAttrInt(node, "WeightValueOverwrite", item.WeightValueOverwrite);
        WriteAttrString(node, "DamageTypeOverwrite", item.DamageTypeOverwrite ?? "");
        WriteAttrInt(node, "HP", item.HP);

        // Write to Stats sub-node
        if (item._statsNode != null)
        {
            var sn = item._statsNode;
            WriteAttrInt(sn, "Level", item.Level);
            WriteAttrBool(sn, "CustomBaseStats", item.HasCustomBase);
            WriteAttrString(sn, "GenerationStatsId", item.GenerationStatsId ?? "");
            WriteAttrString(sn, "StatsEntryName", item.StatsEntryName ?? "");

            // Runes — update existing RuneSlot children
            int runeIdx = 0;
            foreach (var childList in sn.Children.Values)
                foreach (var child in childList)
                    if (child.Attributes.ContainsKey("RuneStatsID"))
                    {
                        if (runeIdx < 3 && item.Runes[runeIdx] != null)
                            WriteAttrString(child, "RuneStatsID", item.Runes[runeIdx]!);
                        runeIdx++;
                    }

            // DeltaMods — update/replace children
            sn.Children.Remove("DeltaMods");
            if (item.DeltaMods.Count > 0)
            {
                var dmList = new List<Node>();
                foreach (var dm in item.DeltaMods)
                {
                    var dmNode = new Node { Name = "DeltaMod" };
                    dmNode.Attributes["DeltaMod"] = new NodeAttribute(AttributeType.FixedString) { Value = dm };
                    dmList.Add(dmNode);
                }
                sn.Children["DeltaMods"] = dmList;
            }
        }

        // Custom name/description
        if (node.Children.TryGetValue("CustomName", out var cnList) && cnList.Count > 0)
            WriteAttrString(cnList[0], "CustomDisplayName", item.DisplayName ?? "");
        if (node.Children.TryGetValue("CustomDescription", out var cdList) && cdList.Count > 0)
            WriteAttrString(cdList[0], "CustomDescription", item.Description ?? "");
    }

    public void RemoveMod(SavegameInfo save, ModEntry mod)
    {
        throw new NotImplementedException("Mod removal not yet implemented");
    }

    // ── Save ──────────────────────────────────────────────────────

    public async Task SaveAsync(SavegameInfo save, string outputPath)
    {
        await Task.Run(() =>
        {
            var package = save.Package
                ?? throw new InvalidOperationException("No package loaded");

            var conversionParams = ResourceConversionParameters.FromGameVersion(save.Game);

            // Detect original compression from existing files.
            // DOS2DE saves use solid LZ4 compression.
            // In solid packages, individual file flags may not reflect the real compression.
            var compressionMethod = CompressionMethod.LZ4;
            var solid = package.Metadata.Flags.HasFlag(PackageFlags.Solid);

            // If not solid, use the first file's compression method
            if (!solid && package.Files.Count > 0)
            {
                var method = package.Files[0].Flags.Method();
                if (method != CompressionMethod.None)
                    compressionMethod = method;
            }

            var flags = solid ? PackageFlags.Solid : 0;

            // Create backup of original file before writing
            long originalSize = 0;
            if (File.Exists(outputPath))
            {
                originalSize = new FileInfo(outputPath).Length;
                var bakPath = outputPath + ".bak";
                File.Copy(outputPath, bakPath, overwrite: true);
            }

            // Reserialize globals.lsf — preserve the original metadata format
            var metadataFormat = save.Globals!.MetadataFormat ?? LSFMetadataFormat.None;
            System.Diagnostics.Debug.WriteLine($"SaveAsync: globals MetadataFormat = {metadataFormat}");

            using var globalsStream = new MemoryStream();
            var lsfWriter = new LSFWriter(globalsStream)
            {
                Version = conversionParams.LSF,
                MetadataFormat = metadataFormat
            };
            lsfWriter.Write(save.Globals!);
            globalsStream.Seek(0, SeekOrigin.Begin);
            var globalsBytes = globalsStream.ToArray();

            // Build package preserving original compression settings
            var build = new PackageBuildData
            {
                Version = conversionParams.PAKVersion,
                Compression = compressionMethod,
                CompressionLevel = LSCompressionLevel.Default,
                Flags = flags
            };

            build.Files.Add(PackageBuildInputFile.CreateFromBlob(globalsBytes, "globals.lsf"));

            // Copy all other files unchanged
            foreach (var file in package.Files.Where(f =>
                !f.Name.Equals("globals.lsf", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = file.CreateContentReader();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                build.Files.Add(PackageBuildInputFile.CreateFromBlob(ms.ToArray(), file.Name));
            }

            // Release file handles before overwriting the package file
            package.Dispose();
            save.Package = null;

            // Write the package
            {
                using var writer = PackageWriterFactory.Create(build, outputPath);
                writer.Write();
            }

            var newSize = new FileInfo(outputPath).Length;
            System.Diagnostics.Debug.WriteLine(
                $"SaveAsync: original={originalSize} bytes, new={newSize} bytes, " +
                $"delta={newSize - originalSize:+0;-#} bytes, " +
                $"compression={compressionMethod}, solid={solid}");

            // Re-open the newly written package so the user can continue editing
            var reader = new PackageReader();
            save.Package = reader.Read(outputPath);

            // Re-read globals from the new package
            var globalsInfo = save.Package.Files.FirstOrDefault(
                f => f.Name.Equals("globals.lsf", StringComparison.OrdinalIgnoreCase));
            if (globalsInfo != null)
            {
                using var rsrcStream = globalsInfo.CreateContentReader();
                using var rsrcReader = new LSFReader(rsrcStream);
                save.Globals = rsrcReader.Read();
            }
        });
    }

    public void Close(SavegameInfo save)
    {
        save.Package?.Dispose();
        save.Package = null;
        save.Globals = null;
    }

    // ── Helpers: Read helpers ─────────────────────────────────────

    private static void ReadAttrString(Node node, string attrName, Action<string> setter)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr) && attr.Value != null)
            setter(attr.Value.ToString() ?? "");
    }

    private static void ReadAttrInt(Node node, string attrName, Action<int> setter)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr) && attr.Value != null)
            setter(Convert.ToInt32(attr.Value));
    }

    private static void ReadAttrBool(Node node, string attrName, Action<bool> setter)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr) && attr.Value != null)
            setter(Convert.ToBoolean(attr.Value));
    }

    private static void ReadStatString(Node node, string attrName, Action<string> setter)
    {
        ReadAttrString(node, attrName, setter);
    }

    private static void ReadStatInt(Node node, string attrName, Action<int> setter)
    {
        ReadAttrInt(node, attrName, setter);
    }

    private static void ReadAttrUInt(Node node, string attrName, Action<uint> setter)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr) && attr.Value != null)
            setter(Convert.ToUInt32(attr.Value));
    }

    private static void ReadAttrULongLong(Node node, string attrName, Action<ulong> setter)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr) && attr.Value != null)
            setter(Convert.ToUInt64(attr.Value));
    }

    private static void ReadAttrTranslatedString(Node node, string attrName, Action<string> setter)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr) && attr.Value is LSLib.LS.TranslatedString ts)
            setter(ts.Value ?? ts.Handle ?? "");
    }

    // ── Helpers: Write helpers ────────────────────────────────────

    private static void WriteAttrInt(Node node, string attrName, int value)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr))
            attr.Value = ConvertForAttributeType(attr.Type, value);
    }

    private static void WriteAttrString(Node node, string attrName, string value)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr))
            attr.Value = value;
    }

    private static void WriteAttrBool(Node node, string attrName, bool value)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr))
            attr.Value = value;
    }

    private static void WriteStatInt(Node node, string attrName, int value)
    {
        WriteAttrInt(node, attrName, value);
    }

    private static void WriteStatUInt(Node node, string attrName, uint value)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr))
            attr.Value = ConvertForAttributeType(attr.Type, value);
    }

    /// <summary>
    /// Converts a numeric value to the type expected by the attribute type,
    /// so the LSF serializer can cast it correctly during save.
    /// </summary>
    private static object ConvertForAttributeType(AttributeType attrType, object value)
    {
        switch (attrType)
        {
            // String types: store as string to avoid (string) cast failure
            case AttributeType.String:
            case AttributeType.Path:
            case AttributeType.FixedString:
            case AttributeType.LSString:
            case AttributeType.WString:
            case AttributeType.LSWString:
                return value.ToString() ?? "";

            // Numeric types: store as the exact type the serializer expects
            case AttributeType.Byte:
                return Convert.ToByte(value);
            case AttributeType.Short:
                return Convert.ToInt16(value);
            case AttributeType.UShort:
                return Convert.ToUInt16(value);
            case AttributeType.Int:
                return Convert.ToInt32(value);
            case AttributeType.UInt:
                return Convert.ToUInt32(value);
            case AttributeType.Float:
                return Convert.ToSingle(value);
            case AttributeType.Double:
                return Convert.ToDouble(value);
            case AttributeType.ULongLong:
                return Convert.ToUInt64(value);
            case AttributeType.Int64:
                return Convert.ToInt64(value);
            case AttributeType.Bool:
                return Convert.ToBoolean(value);

            default:
                // Keep as-is for vector/matrix/UUID/scratchbuffer etc.
                return value;
        }
    }

    // ── Helpers: Meta loading ─────────────────────────────────────

    private void LoadMeta(SavegameInfo save)
    {
        var metaInfo = save.Package!.Files.FirstOrDefault(
            f => f.Name.Equals("meta.lsf", StringComparison.OrdinalIgnoreCase));
        if (metaInfo == null) return;

        try
        {
            using var stream = metaInfo.CreateContentReader();
            using var reader = new LSFReader(stream);
            var meta = reader.Read();

            // Store mod info directly from meta
            if (meta.Regions.TryGetValue("MetaData", out var metaRegion))
            {
                var metaNode = metaRegion.Children["MetaData"][0];
                ReadAttrString(metaNode, "GameVersion", v => save.GameVersion = v);
                ReadAttrString(metaNode, "Difficulty", v => save.Difficulty = v);
                ReadAttrString(metaNode, "GameTime", v => save.GameTime = v);
            }

            // Save meta resource into globals for mod extraction
            if (!save.Globals!.Regions.ContainsKey("MetaData"))
            {
                foreach (var kv in meta.Regions)
                    save.Globals.Regions[kv.Key] = kv.Value;
            }
        }
        catch
        {
            // meta.lsf is optional
        }
    }
}

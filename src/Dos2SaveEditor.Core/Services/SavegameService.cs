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
        return save.Globals!.Regions["Characters"]
            .Children["CharacterFactory"][0]
            .Children["Characters"][0]
            .Children["Character"];
    }

    public List<Character> GetCharacters(SavegameInfo save)
    {
        var characters = new List<Character>();
        var charNodes = GetCharacterNodes(save);

        for (int i = 0; i < charNodes.Count; i++)
        {
            var charNode = charNodes[i];
            var c = new Character { NodeIndex = i };

            // ── Stats attributes (direct children of character node) ──
            ReadStatInt(charNode, "Armor", v => c.Armor = v);
            ReadStatInt(charNode, "MaxArmorPatchCheck", v => c.ArmorMax = v);
            ReadStatInt(charNode, "MagicArmor", v => c.MagicArmor = v);
            ReadStatInt(charNode, "MaxMagicArmorPatchCheck", v => c.MagicArmorMax = v);
            ReadStatInt(charNode, "Vitality", v => c.Vitality = v);
            ReadStatInt(charNode, "MaxVitalityPatchCheck", v => c.VitalityMax = v);
            ReadStatInt(charNode, "DamageCounter", v => c.DamageCount = v);
            ReadStatInt(charNode, "HealCounter", v => c.HealCount = v);
            ReadStatInt(charNode, "KillCounter", v => c.KillCount = v);
            ReadStatString(charNode, "Level", v => c.Map = v);
            ReadStatUInt(charNode, "Experience", v => c.Experience = v);
            ReadStatString(charNode, "Inventory", v => c.InventoryUuid = v);

            // ── CustomData (Name, Race, Class, Origin) ──
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

            // ── PlayerUpgrade (available points) ──
            if (charNode.Children.TryGetValue("PlayerData", out pdList) && pdList.Count > 0)
            {
                var playerData = pdList[0];
                if (playerData.Children.TryGetValue("PlayerUpgrade", out var puList) && puList.Count > 0)
                {
                    var upgrade = puList[0];
                    ReadAttrInt(upgrade, "AttributePoints", v => c.AttributePoints = v);
                    ReadAttrInt(upgrade, "CombatAbilityPoints", v => c.CombatAbilityPoints = v);
                    ReadAttrInt(upgrade, "CivilAbilityPoints", v => c.CivilAbilityPoints = v);
                    ReadAttrInt(upgrade, "TalentPoints", v => c.TalentPoints = v);
                }
            }

            // ── Spent points (attributes, abilities, talents) ──
            // These are in a sequential node list under PlayerUpgrade's children
            if (charNode.Children.TryGetValue("PlayerData", out pdList) && pdList.Count > 0)
            {
                var playerData = pdList[0];
                if (playerData.Children.TryGetValue("PlayerUpgrade", out var puList) && puList.Count > 0)
                {
                    var upgrade = puList[0];
                    // Iterate through child list to find Object attributes
                    int attrIdx = 0, abilIdx = 0, talIdx = 0;

                    foreach (var childKey in upgrade.Children.Keys)
                    {
                        foreach (var childNode in upgrade.Children[childKey])
                        {
                            if (childNode.Attributes.TryGetValue("Object", out var objAttr))
                            {
                                int val = Convert.ToInt32(objAttr.Value);

                                if (attrIdx < 6)
                                    c.Attributes[attrIdx++] = val;
                                else if (abilIdx < 25)
                                    c.Abilities[abilIdx++] = val;
                                else if (talIdx < 4)
                                    c.Talents[talIdx++] = objAttr.Value?.ToString();
                            }
                        }
                    }
                }
            }

            // ── Tags ──
            if (charNode.Children.TryGetValue("Tags", out var tagsList) && tagsList.Count > 0)
            {
                var tagsNode = tagsList[0];
                foreach (var tagKey in tagsNode.Children.Keys)
                {
                    foreach (var tagNode in tagsNode.Children[tagKey])
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
        var item = new Item();

        ReadAttrString(itemNode, "Stats", v => item.StatsId = v);
        ReadAttrString(itemNode, "Handle", v => item.Handle = v);
        ReadAttrInt(itemNode, "Amount", v => item.Amount = v > 0 ? v : 1);
        ReadAttrString(itemNode, "Slot", v => item.Slot = v);
        ReadAttrString(itemNode, "Inventory", v => item.InventoryUuid = v);
        ReadAttrBool(itemNode, "IsGenerated", v => item.IsGenerated = v);

        // Detect backpack (non-null, non-empty Inventory attribute)
        if (!string.IsNullOrEmpty(item.InventoryUuid) && item.InventoryUuid != "00000000-0000-0000-0000-000000000000")
            item.IsBackpack = true;

        // ── Stats sub-node (ItemType, Level, Runes, etc.) ──
        if (itemNode.Children.TryGetValue("Stats", out var statsList) && statsList.Count > 0)
        {
            var statsNode = statsList[0];
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
        var charNodes = GetCharacterNodes(save);
        if (character.NodeIndex >= charNodes.Count)
            throw new ArgumentOutOfRangeException(nameof(character), "Character index out of range");

        var node = charNodes[character.NodeIndex];

        WriteStatInt(node, "Armor", character.Armor);
        WriteStatInt(node, "MaxArmorPatchCheck", character.ArmorMax);
        WriteStatInt(node, "MagicArmor", character.MagicArmor);
        WriteStatInt(node, "MaxMagicArmorPatchCheck", character.MagicArmorMax);
        WriteStatInt(node, "Vitality", character.Vitality);
        WriteStatInt(node, "MaxVitalityPatchCheck", character.VitalityMax);
        WriteStatInt(node, "DamageCounter", character.DamageCount);
        WriteStatInt(node, "HealCounter", character.HealCount);
        WriteStatInt(node, "KillCounter", character.KillCount);
        WriteStatUInt(node, "Experience", character.Experience);

        // Update PlayerUpgrade points
        if (node.Children.TryGetValue("PlayerData", out var pdList) && pdList.Count > 0)
        {
            var playerData = pdList[0];
            if (playerData.Children.TryGetValue("PlayerUpgrade", out var puList) && puList.Count > 0)
            {
                var upgrade = puList[0];
                WriteAttrInt(upgrade, "AttributePoints", character.AttributePoints);
                WriteAttrInt(upgrade, "CombatAbilityPoints", character.CombatAbilityPoints);
                WriteAttrInt(upgrade, "CivilAbilityPoints", character.CivilAbilityPoints);
                WriteAttrInt(upgrade, "TalentPoints", character.TalentPoints);
            }
        }
    }

    public void UpdateItem(SavegameInfo save, Item item)
    {
        // Items are updated in-place via ParseItem references
        // Full implementation will write back to the node tree
        throw new NotImplementedException("Item write-back not yet implemented");
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

            // Create backup
            if (File.Exists(outputPath))
            {
                var bakPath = outputPath + ".bak";
                File.Copy(outputPath, bakPath, overwrite: true);
            }

            // Reserialize globals.lsf
            using var globalsStream = new MemoryStream();
            var lsfWriter = new LSFWriter(globalsStream)
            {
                Version = conversionParams.LSF,
                MetadataFormat = LSFMetadataFormat.None
            };
            lsfWriter.Write(save.Globals!);
            globalsStream.Seek(0, SeekOrigin.Begin);
            var globalsBytes = globalsStream.ToArray();

            // Build package
            var build = new PackageBuildData
            {
                Version = conversionParams.PAKVersion,
                Compression = CompressionMethod.Zlib,
                CompressionLevel = LSCompressionLevel.Default
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

            // Write the package
            using var writer = PackageWriterFactory.Create(build, outputPath);
            writer.Write();
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

    private static void ReadStatUInt(Node node, string attrName, Action<uint> setter)
    {
        if (node.Attributes.TryGetValue(attrName, out var attr) && attr.Value != null)
            setter(Convert.ToUInt32(attr.Value));
    }

    // ── Helpers: Write helpers ────────────────────────────────────

    private static void WriteAttrInt(Node node, string attrName, int value)
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
            attr.Value = value;
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

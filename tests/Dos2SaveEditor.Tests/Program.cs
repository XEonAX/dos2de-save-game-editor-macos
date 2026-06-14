using Dos2SaveEditor.Core.Services;
using LSLib.LS;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --project tests/Dos2SaveEditor.Tests -- <path-to-save.lsv>");
    return;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.WriteLine($"File not found: {path}");
    return;
}

Console.WriteLine($"Opening: {path}");
var service = new SavegameService();

try
{
    var save = await service.OpenSaveAsync(path);
    Console.WriteLine($"Save opened successfully!");
    Console.WriteLine($"  File: {save.FileName}");
    Console.WriteLine($"  Character Count: {save.CharacterCount}");

    // ── Diagnostic: dump first character's Stats node attributes ──
    try
    {
        var charNodes = save.Globals!.Regions["Characters"]
            .Children["CharacterFactory"][0]
            .Children["Characters"][0]
            .Children["Character"];
        foreach (var cn in charNodes)
        {
            if (!cn.Children.TryGetValue("Stats", out var sl) || sl.Count == 0) continue;
            var sn = sl[0];
            if (!sn.Attributes.TryGetValue("IsPlayer", out var ip) || !(ip.Value is bool b && b)) continue;

            // Dump player character's full structure
            Console.WriteLine($"\n=== Player Character ===");
            Console.WriteLine($"  Attributes: {string.Join(", ", cn.Attributes.Keys)}");
            foreach (var attr in cn.Attributes)
                Console.WriteLine($"    {attr.Key} = {attr.Value.Value} ({attr.Value.Type})");
            Console.WriteLine($"  Children: {string.Join(", ", cn.Children.Keys)}");
            foreach (var childKv in cn.Children)
            {
                Console.WriteLine($"    [{childKv.Key}] ({childKv.Value.Count} nodes)");
                foreach (var child in childKv.Value.Take(1))
                {
                    Console.WriteLine($"      Attributes: {string.Join(", ", child.Attributes.Keys)}");
                    foreach (var a in child.Attributes.Take(10))
                        Console.WriteLine($"        {a.Key} = {a.Value.Value} ({a.Value.Type})");
                    Console.WriteLine($"      Sub-children: {string.Join(", ", child.Children.Keys)}");
                }
            }
            // Dump PlayerUpgrade structure
            if (cn.Children.TryGetValue("PlayerUpgrade", out var puList) && puList.Count > 0)
            {
                var pu = puList[0];
                Console.WriteLine($"    [PlayerUpgrade] attrs: {string.Join(", ", pu.Attributes.Keys)}");
                foreach (var a in pu.Attributes)
                    Console.WriteLine($"      {a.Key} = {a.Value.Value} ({a.Value.Type})");
                int total = 0;
                foreach (var cg in pu.Children) total += cg.Value.Count;
                Console.WriteLine($"    [PlayerUpgrade] child groups: {string.Join(", ", pu.Children.Keys)} total={total}");
                int idx = 0;
                foreach (var cg in pu.Children)
                    foreach (var c in cg.Value)
                    {
                        var v = c.Attributes.ContainsKey("Object") ? c.Attributes["Object"].Value : "?";
                        if (idx < 10 || (idx >= 18 && idx < 26) || idx >= total - 5)
                            Console.WriteLine($"      [{idx}] Object={v} ({v?.GetType().Name})");
                        idx++;
                    }
            }
            break;
        }
    }
    catch (Exception ex) { Console.WriteLine($"Diag error: {ex.Message}"); }
    Console.WriteLine("---");

    var characters = service.GetCharacters(save);
    Console.WriteLine($"  Parsed Characters: {characters.Count}");

    foreach (var c in characters)
    {
        Console.WriteLine($"\n-- {c.Name ?? "Unnamed"} --");
        Console.WriteLine($"  Race: {c.Race}, Class: {c.ClassType}, Origin: {c.OriginName}");
        Console.WriteLine($"  Level: {c.Level}, XP: {c.Experience}");
        Console.WriteLine($"  VIT: {c.Vitality}/{c.VitalityMax}  ARM: {c.Armor}/{c.ArmorMax}  MAG: {c.MagicArmor}/{c.MagicArmorMax}");
        Console.WriteLine($"  Attrs: STR={c.Attributes[0]} DEX={c.Attributes[1]} INT={c.Attributes[2]} CON={c.Attributes[3]} MEM={c.Attributes[4]} WIT={c.Attributes[5]}");
        Console.WriteLine($"  Points: Attr={c.AttributePoints} Combat={c.CombatAbilityPoints} Civil={c.CivilAbilityPoints} Talent={c.TalentPoints}");
        Console.WriteLine($"  Tags ({c.Tags.Count}): {string.Join(", ", c.Tags.Take(15))}{(c.Tags.Count > 15 ? "..." : "")}");
        Console.WriteLine($"  Inventory UUID: {c.InventoryUuid ?? "N/A"}");

        // Show abilities
        var combatNames = Dos2SaveEditor.Core.Utils.GameConstants.CombatAbilityNames;
        var civilNames = Dos2SaveEditor.Core.Utils.GameConstants.CivilAbilityNames;
        Console.Write("  Combat: ");
        for (int i = 0; i < combatNames.Length && i < c.Abilities.Length; i++)
            Console.Write($"{combatNames[i]}={c.Abilities[i]} ");
        Console.WriteLine();
        Console.Write("  Civil:  ");
        for (int i = 0; i < civilNames.Length && (i + 18) < c.Abilities.Length; i++)
            Console.Write($"{civilNames[i]}={c.Abilities[i + 18]} ");
        Console.WriteLine();

        // Show inventory count
        var items = service.GetInventory(save, c);
        Console.WriteLine($"  Items: {items.Count}");
        foreach (var item in items.Take(5))
            Console.WriteLine($"    - {item.StatsId ?? "?"} x{item.Amount}" + (item.IsBackpack ? " [Backpack]" : ""));
    }

    var mods = service.GetMods(save);
    Console.WriteLine($"\nMods ({mods.Count}):");
    foreach (var m in mods.Take(20))
        Console.WriteLine($"  {m.Name ?? m.Uuid}");

    service.Close(save);
    Console.WriteLine("\nAll parsing completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

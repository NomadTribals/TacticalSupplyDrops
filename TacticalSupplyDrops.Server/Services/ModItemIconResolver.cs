
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class ModItemIconResolver
{
    private static readonly string ModsFolder =
        Path.Combine(AppContext.BaseDirectory, "user", "mods");

    private const string OrangeFlareTemplateId =
        "6a0000000000000000000001";

    private const string RedFlareTemplateId =
        "62389ba9a63f32501b1b4451";

    private const string HerculesRig1Id =
        "661c9174018549befc3a7d62";

    private const string HerculesRig2Id =
        "661c9174d9718c60a32fe5b4";

    private const string HerculesRig1Prefab =
        "assets/content/items/equipment/rig_patrol/item_equipment_rig_patrol.bundle";

    private const string HerculesRig2Prefab =
        "assets/content/items/equipment/armor_ars_arma_a18/item_equipment_armor_a18.bundle";

    private readonly ItemHelper _itemHelper;

    private readonly Dictionary<string, string> _cloneOrigins =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<string>> _prefabIndex =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _prefabIndexBuilt;

    public ModItemIconResolver(ItemHelper itemHelper)
    {
        _itemHelper = itemHelper;

        LoadCloneDefinitions();
    }

    public string GetIconTemplateId(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return templateId;
        }

        if (string.Equals(
            templateId,
            OrangeFlareTemplateId,
            StringComparison.OrdinalIgnoreCase))
        {
            return RedFlareTemplateId;
        }

        if (_cloneOrigins.TryGetValue(
            templateId,
            out string? originalId))
        {
            return originalId;
        }

        if (!_prefabIndexBuilt)
        {
            BuildPrefabIndex();
        }

        var item = _itemHelper.GetItem(templateId);

        string? prefabPath = item.Value?.Properties?.Prefab?.Path;

        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            LogHerculesResult(
                templateId,
                "No prefab path was found.");

            return templateId;
        }

        if (_prefabIndex.TryGetValue(
            prefabPath,
            out List<string>? matchingTemplates) &&
            matchingTemplates != null)
        {
            if (IsHerculesItem(templateId))
            {
                Console.WriteLine(
                    $"[Tactical Supply Drops] Hercules {templateId} candidates: " +
                    string.Join(", ", matchingTemplates));
            }

            foreach (string matchingTemplate in matchingTemplates)
            {
                if (!string.Equals(
                    matchingTemplate,
                    templateId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    LogHerculesResult(
                        templateId,
                        $"Selected icon template: {matchingTemplate}");

                    return matchingTemplate;
                }
            }
        }

        LogHerculesResult(
            templateId,
            "No alternative prefab match was found.");

        return templateId;
    }

    private void BuildPrefabIndex()
    {
        if (_prefabIndexBuilt)
        {
            return;
        }

        _prefabIndexBuilt = true;

        try
        {
            var items = _itemHelper.GetItemsClone();

            if (items == null)
            {
                Console.WriteLine(
                    "[Tactical Supply Drops] Could not build prefab index: item list was null.");

                return;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                string? prefabPath =
                    item.Properties?.Prefab?.Path;

                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    continue;
                }

                string templateId = item.Id.ToString();

                if (string.IsNullOrWhiteSpace(templateId))
                {
                    continue;
                }

                if (!_prefabIndex.TryGetValue(
                    prefabPath,
                    out List<string>? matchingTemplates))
                {
                    matchingTemplates = new List<string>();

                    _prefabIndex[prefabPath] = matchingTemplates;
                }

                matchingTemplates.Add(templateId);
            }

            LogPrefabCandidates(
                "Hercules Rig 1",
                HerculesRig1Prefab);

            LogPrefabCandidates(
                "Hercules Rig 2",
                HerculesRig2Prefab);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[Tactical Supply Drops] Failed to build prefab icon index: {exception.Message}");
        }
    }

    private void LogPrefabCandidates(
        string rigName,
        string prefabPath)
    {
        Console.WriteLine(
            $"[Tactical Supply Drops] {rigName} prefab diagnostic:");

        if (_prefabIndex.TryGetValue(
            prefabPath,
            out List<string>? candidates) &&
            candidates != null)
        {
            Console.WriteLine(
                $"[Tactical Supply Drops] Matching templates: " +
                string.Join(", ", candidates));
        }
        else
        {
            Console.WriteLine(
                "[Tactical Supply Drops] No templates found for this prefab.");
        }
    }

    private static bool IsHerculesItem(string templateId)
    {
        return string.Equals(
                   templateId,
                   HerculesRig1Id,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   templateId,
                   HerculesRig2Id,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void LogHerculesResult(
        string templateId,
        string message)
    {
        if (IsHerculesItem(templateId))
        {
            Console.WriteLine(
                $"[Tactical Supply Drops] Hercules resolver: {templateId} - {message}");
        }
    }

    private void LoadCloneDefinitions()
    {
        if (!Directory.Exists(ModsFolder))
        {
            return;
        }

        try
        {
            IEnumerable<string> customItemFolders =
                Directory.EnumerateDirectories(
                    ModsFolder,
                    "CustomItems",
                    SearchOption.AllDirectories);

            foreach (string folder in customItemFolders)
            {
                foreach (string file in Directory.EnumerateFiles(
                    folder,
                    "*.json",
                    SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        using FileStream stream = File.OpenRead(file);

                        using JsonDocument document =
                            JsonDocument.Parse(stream);

                        ReadCloneDefinitions(document.RootElement);
                    }
                    catch
                    {
                        // Ignore invalid item definition files.
                    }
                }
            }
        }
        catch
        {
            // Ignore directory access errors.
        }
    }

    private void ReadCloneDefinitions(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (property.Value.TryGetProperty(
                "itemTplToClone",
                out JsonElement cloneElement) &&
                cloneElement.ValueKind == JsonValueKind.String)
            {
                string? originalId = cloneElement.GetString();

                if (!string.IsNullOrWhiteSpace(originalId))
                {
                    _cloneOrigins[property.Name] = originalId;
                }
            }
        }
    }
}

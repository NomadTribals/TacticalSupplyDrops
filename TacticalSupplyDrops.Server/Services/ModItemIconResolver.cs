
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SPTarkov.DI.Annotations;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class ModItemIconResolver
{
    private const string ModsFolder =
        @"F:\Games\SPT\SPT_Runtime\user\mods";

    private readonly Dictionary<string, string> _cloneOrigins = new();

    public ModItemIconResolver()
    {
        LoadCloneDefinitions();
    }

    public string GetIconTemplateId(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return templateId;
        }

        // Use the vanilla red flare icon for the TSD orange flare.
        if (string.Equals(
            templateId,
            "6a0000000000000000000001",
            StringComparison.OrdinalIgnoreCase))
        {
            return "62389ba9a63f32501b1b4451";
        }

        return _cloneOrigins.TryGetValue(templateId, out string? originalId)
            ? originalId
            : templateId;
    }

    private void LoadCloneDefinitions()
    {
        if (!Directory.Exists(ModsFolder))
        {
            return;
        }

        try
        {
            // Only inspect folders specifically named CustomItems.
            IEnumerable<string> customItemFolders =
                Directory.EnumerateDirectories(
                    ModsFolder,
                    "CustomItems",
                    SearchOption.AllDirectories);

            foreach (string folder in customItemFolders)
            {
                foreach (string file in Directory.EnumerateFiles(
                             folder, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        using FileStream stream = File.OpenRead(file);
                        using JsonDocument document = JsonDocument.Parse(stream);

                        ReadCloneDefinitions(document.RootElement);
                    }
                    catch
                    {
                        // Ignore files that are not valid item definitions.
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
                    "itemTplToClone", out JsonElement cloneElement) &&
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

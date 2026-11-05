
using SPTarkov.DI.Annotations;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Services;

public interface IItemDetailsProvider
{
    (string Name, int BaseValue, int Width, int Height, string IconUrl, string Category) GetDetails(Item item);

    bool IsInstalledInEquipmentSlot(Item item, Item? parent);
}

public interface IStashItemDetailsProvider
{
    List<StashItemDetails> GetItemDetails(IEnumerable<Item> items);

    bool IsInstalledInEquipmentSlot(Item item, Item? parent);
}

[Injectable]
public class StashItemDetailsService(IItemDetailsProvider itemHelper) : IStashItemDetailsProvider
{
    public List<StashItemDetails> GetItemDetails(IEnumerable<Item> items)
    {
        return items.Select(item =>
        {
            var details = itemHelper.GetDetails(item);

            return new StashItemDetails
            {
                ItemId = (string)item.Id,
                TemplateId = (string)item.Template,
                Name = details.Name,
                Quantity = item.Upd?.StackObjectsCount is double count
                    ? (int)count
                    : 1,
                BaseValue = details.BaseValue,
                Width = details.Width,
                Height = details.Height,
                IconUrl = details.IconUrl,
                Tag = item.Upd?.Tag?.Name ?? string.Empty,
                Category = details.Category
            };
        }).ToList();
    }

    public bool IsInstalledInEquipmentSlot(Item item, Item? parent)
    {
        return itemHelper.IsInstalledInEquipmentSlot(item, parent);
    }
}

public class StashItemDetails
{
    public string ItemId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int BaseValue { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string IconUrl { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Category { get; set; } = "OTHER";
}

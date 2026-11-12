using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SptItemDimensionsProvider(ItemHelper itemHelper)
    : IItemDimensionsProvider
{
    public (int? Width, int? Height) GetDimensions(Item item)
    {
        var template = itemHelper.GetItem(item.Template);

        return (
            template.Value?.Properties?.Width,
            template.Value?.Properties?.Height
        );
    }
}
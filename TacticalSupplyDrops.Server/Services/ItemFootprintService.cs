using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Services;

public interface IItemDimensionsProvider
{
    (int? Width, int? Height) GetDimensions(
        SPTarkov.Server.Core.Models.Eft.Common.Tables.Item item);
}

[Injectable]
public class ItemFootprintService(IItemDimensionsProvider dimensionsProvider)
{
    public int CalculateFootprint(
        SPTarkov.Server.Core.Models.Eft.Common.Tables.Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var dimensions = dimensionsProvider.GetDimensions(item);

        if (dimensions.Width is null ||
            dimensions.Height is null ||
            dimensions.Width <= 0 ||
            dimensions.Height <= 0)
        {
            throw new InvalidOperationException(
                "Unable to determine the item's dimensions.");
        }

        return checked(dimensions.Width.Value * dimensions.Height.Value);
    }
}
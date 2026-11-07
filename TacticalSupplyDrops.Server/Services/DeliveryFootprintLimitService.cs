
using SPTarkov.DI.Annotations;
using System;
using System.Collections.Generic;
using TacticalSupplyDrops.Server.Models;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class DeliveryFootprintLimitService(ItemFootprintService footprintService)
{
    public const int MaximumFootprint = 60;

    public int CalculateTotalFootprint(IEnumerable<Item> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        int totalFootprint = 0;

        foreach (var item in items)
        {
            totalFootprint = checked(
                totalFootprint + footprintService.CalculateFootprint(item));
        }

        return totalFootprint;
    }

    public bool IsWithinLimit(IEnumerable<Item> items)
    {
        return CalculateTotalFootprint(items) <= MaximumFootprint;
    }
}

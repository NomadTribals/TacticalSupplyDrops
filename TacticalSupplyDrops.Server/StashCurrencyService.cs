using System.Linq;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class StashCurrencyService(StashInventoryService stashInventoryService)
{
    public int GetRoublesInStash(PmcData profile)
    {
        if (profile == null)
        {
            return 0;
        }

        var stashItems = stashInventoryService.GetStashItems(profile);

        return stashItems
            .Where(item => item.Template == Money.ROUBLES)
            .Sum(item => item.GetItemStackSize());
    }
}
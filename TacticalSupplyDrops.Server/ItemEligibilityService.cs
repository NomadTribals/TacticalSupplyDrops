using System.Linq;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class ItemEligibilityService(StashInventoryService stashInventoryService)
{
    public bool IsEligible(Item item, PmcData profile)
    {
        if (item is null || profile?.Inventory?.Stash is null)
        {
            return false;
        }

        // The stash container itself cannot be ordered.
        if (item.Id == profile.Inventory.Stash.Value)
        {
            return false;
        }

        // Only items actually inside the player's stash are allowed.
        var stashItems = stashInventoryService.GetStashItems(profile);

        return stashItems.Any(stashItem => stashItem.Id == item.Id);
    }
}
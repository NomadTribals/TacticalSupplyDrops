
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using System.Collections.Generic;
using System.Linq;

namespace TacticalSupplyDrops.Server.Services;

public interface IInventoryItemRemover
{
    void RemoveItemByCount(
        PmcData profile,
        MongoId itemId,
        int count,
        MongoId sessionId,
        ItemEventRouterResponse output);

    void RemoveItem(
        PmcData profile,
        MongoId itemId,
        MongoId sessionId,
        ItemEventRouterResponse output);
}

[Injectable]
public class SptInventoryItemRemover(
    InventoryHelper inventoryHelper) : IInventoryItemRemover
{
    public void RemoveItemByCount(
        PmcData profile,
        MongoId itemId,
        int count,
        MongoId sessionId,
        ItemEventRouterResponse output)
    {
        inventoryHelper.RemoveItemByCount(
            profile,
            itemId,
            count,
            sessionId,
            output);
    }
    public void RemoveItem(
    PmcData profile,
    MongoId itemId,
    MongoId sessionId,
    ItemEventRouterResponse output)
    {
        inventoryHelper.RemoveItem(
            profile,
            itemId,
            sessionId,
            output);
    }
}

[Injectable]
public class StashPaymentDeductionService(
    StashInventoryService stashInventoryService,
    IInventoryItemRemover itemRemover)
{
    public bool TryApplyPlan(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        IEnumerable<StashPaymentDeduction> deductions)
    {
        if (profile == null || output == null || deductions == null)
        {
            return false;
        }

        var plan = deductions.ToList();

        if (plan.Count == 0 ||
            plan.Any(item =>
                string.IsNullOrWhiteSpace(item.ItemId) ||
                item.QuantityToRemove <= 0))
        {
            return false;
        }

        var stashItems = stashInventoryService.GetStashItems(profile);

        // Validate the complete plan before removing anything.
        foreach (var group in plan.GroupBy(item => item.ItemId))
        {
            var stack = stashItems.FirstOrDefault(item =>
                (string)item.Id == group.Key);

            int totalToRemove = group.Sum(item => item.QuantityToRemove);

            if (stack == null ||
                stack.Template != Money.ROUBLES ||
                stack.GetItemStackSize() < totalToRemove)
            {
                return false;
            }
        }

        // Apply the validated deductions through SPT.
        foreach (var deduction in plan)
        {
            itemRemover.RemoveItemByCount(
                profile,
                new MongoId(deduction.ItemId),
                deduction.QuantityToRemove,
                sessionId,
                output);
        }

        return true;
    }
}

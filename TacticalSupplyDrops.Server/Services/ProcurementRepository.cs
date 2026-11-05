
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class ProcurementRepository(
    IProfileDataStore profileDataStore)
{
    private const string ModKey =
        "tactical_supply_drops_procurement";

    public async Task<List<ProcurementPurchase>> GetPurchasesAsync(
        MongoId profileId,
        CancellationToken cancellationToken = default)
    {
        var purchases =
            await profileDataStore.GetAsync<List<ProcurementPurchase>>(
                profileId,
                ModKey,
                cancellationToken);

        return purchases ?? new List<ProcurementPurchase>();
    }

    public async Task SavePurchasesAsync(
        MongoId profileId,
        IEnumerable<ProcurementPurchase> purchases,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchases);

        await profileDataStore.SaveAsync(
            profileId,
            ModKey,
            purchases.ToList(),
            cancellationToken);
    }

    public async Task<ProcurementPurchase?> GetByRequestIdAsync(
        MongoId profileId,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return null;

        var purchases = await GetPurchasesAsync(
            profileId,
            cancellationToken);

        return purchases.FirstOrDefault(purchase =>
            purchase.RequestId == requestId);
    }

    public async Task AddPurchaseAsync(
        MongoId profileId,
        ProcurementPurchase purchase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        if (string.IsNullOrWhiteSpace(purchase.PurchaseId) ||
            string.IsNullOrWhiteSpace(purchase.RequestId))
        {
            throw new ArgumentException(
                "A purchase ID and request ID are required.",
                nameof(purchase));
        }

        var purchases = await GetPurchasesAsync(
            profileId,
            cancellationToken);

        bool duplicate = purchases.Any(existing =>
            existing.PurchaseId == purchase.PurchaseId ||
            existing.RequestId == purchase.RequestId);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "A procurement purchase with this ID or request ID already exists.");
        }

        purchases.Add(purchase);

        await SavePurchasesAsync(
            profileId,
            purchases,
            cancellationToken);
    }

    public async Task<bool> UpdatePurchaseAsync(
        MongoId profileId,
        ProcurementPurchase updatedPurchase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updatedPurchase);

        if (string.IsNullOrWhiteSpace(updatedPurchase.PurchaseId))
            return false;

        var purchases = await GetPurchasesAsync(
            profileId,
            cancellationToken);

        int index = purchases.FindIndex(purchase =>
            purchase.PurchaseId == updatedPurchase.PurchaseId);

        if (index < 0)
            return false;

        bool duplicateRequest = purchases.Any(purchase =>
            purchase.PurchaseId != updatedPurchase.PurchaseId &&
            purchase.RequestId == updatedPurchase.RequestId);

        if (duplicateRequest)
            return false;

        purchases[index] = updatedPurchase;

        await SavePurchasesAsync(
            profileId,
            purchases,
            cancellationToken);

        return true;
    }
}

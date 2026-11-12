using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Utils.Cloners;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

public record CourierConversionQuote(int TotalFee, int PaidFee, int AdditionalFee);

[Injectable]
public class CourierConversionService(SupplyOrderRepository repository, IStashItemDetailsProvider details,
    SupplyOrderPricingService pricing, SupplyOrderPaymentProcessor payment, ICloner cloner, ProfileTransactionLockService transactionLock)
{
    private int CourierPrice(SupplyOrder order, FenceReputationTier tier)
    {
        var saved = order.Items.SelectMany(entry => entry.Items).DistinctBy(item => item.Id).ToList();
        return Math.Max(order.TotalPrice, pricing.CalculateSupplyOrderPrice(details.GetItemDetails(saved), tier, "standard"));
    }

    public CourierConversionQuote Quote(IEnumerable<SupplyOrder> orders, FenceReputationTier tier)
    {
        int paid = 0, total = 0;
        foreach (var order in orders)
        {
            paid = checked(paid + order.TotalPrice);
            total = checked(total + (order.DeliveryMethod == "drone" ? CourierPrice(order, tier) : order.TotalPrice));
        }
        return new(total, paid, checked(total - paid));
    }

    public async Task<string?> Convert(PmcData profile, MongoId sessionId, ItemEventRouterResponse output,
        IReadOnlyList<string> orderIds, int expectedAdditionalFee, FenceReputationTier tier, CancellationToken cancellationToken)
    {
        if (orderIds == null || orderIds.Count == 0 || orderIds.Count > 256 || orderIds.Any(string.IsNullOrWhiteSpace) || orderIds.Distinct().Count() != orderIds.Count)
            return "The courier conversion order list is invalid.";
        var gate = await transactionLock.AcquireAsync(profile.Id!.Value, cancellationToken);
        try
        {
            var originalOrders = await repository.GetOrdersAsync(profile.Id.Value, cancellationToken);
            var orders = originalOrders.Where(order => orderIds.Contains(order.OrderId)).ToList();
            if (orders.Count != orderIds.Count) return "A paid cargo order could not be found. No additional fee was charged.";
            if (orders.All(order => order.DeliveryMethod == "standard")) return null;
            if (orders.Any(order => order.DeliveryMethod is not ("standard" or "drone"))) return "Unsupported delivery service.";
            var quote = Quote(orders, tier);
            if (quote.AdditionalFee != expectedAdditionalFee) return "The courier fee changed. Review the refreshed balance before confirming again.";
            if (profile.Inventory?.Items == null || !output.ProfileChanges.TryGetValue(sessionId, out var change) || change.Items == null)
                return "The payment profile is unavailable. The drone orders remain reserved.";
            var plan = new List<StashPaymentDeduction>();
            if (quote.AdditionalFee > 0 && !payment.TryCreatePaymentPlan(profile, quote.AdditionalFee, out plan))
                return "Not enough roubles for the courier balance. The paid cargo remains reserved.";

            var inventory = profile.Inventory.Items.Select(item => cloner.Clone(item)!).ToList();
            var insurance = profile.InsuredItems?.Select(item => cloner.Clone(item)!).ToList();
            var newItems = change.Items.NewItems.Select(item => cloner.Clone(item)!).ToList();
            var changedItems = change.Items.ChangedItems.Select(item => cloner.Clone(item)!).ToList();
            var deletedItems = change.Items.DeletedItems.Select(item => cloner.Clone(item)!).ToList();
            void Restore()
            {
                profile.Inventory.Items = inventory; profile.InsuredItems = insurance;
                change.Items.NewItems = newItems; change.Items.ChangedItems = changedItems; change.Items.DeletedItems = deletedItems;
            }
            try
            {
                if (quote.AdditionalFee > 0 && !payment.TryProcessPayment(profile, sessionId, output, plan))
                { Restore(); return "Courier payment could not complete. The paid cargo remains reserved."; }
                var replacements = orders.ToDictionary(order => order.OrderId, order => new SupplyOrder
                {
                    OrderId = order.OrderId, RequestId = order.RequestId, Items = order.Items,
                    TotalFootprint = order.TotalFootprint, CreatedAt = order.CreatedAt, Status = order.Status,
                    DeliveryMethod = "standard", TotalPrice = order.DeliveryMethod == "drone" ? CourierPrice(order, tier) : order.TotalPrice
                });
                await repository.SaveOrdersAsync(profile.Id.Value,
                    originalOrders.Select(order => replacements.GetValueOrDefault(order.OrderId) ?? order), cancellationToken);
                return null;
            }
            catch
            {
                Restore();
                await repository.SaveOrdersAsync(profile.Id.Value, originalOrders, CancellationToken.None);
                throw;
            }
        }
        finally { gate.Release(); }
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Servers;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

public record TraderCatalogRequest : IRequestData
{
    public string TraderId { get; set; } = "";
}

public record TraderProcurementRequest : BaseInteractionRequestData, IRequestData
{
    public string RequestId { get; set; } = "";
    public string TraderId { get; set; } = "";
    public string OfferId { get; set; } = "";
    public int SchemeIndex { get; set; }
    public int Quantity { get; set; }
    public string PriceToken { get; set; } = "";
}

public record ProcurementWallet(long roubles, long dollars, long euros);

[Injectable]
public class TraderProcurementService(
    TraderHelper traderHelper,
    TraderAssortHelper assortHelper,
    TradeHelper tradeHelper,
    ItemHelper itemHelper,
    ProfileHelper profileHelper,
    StashInventoryService stashInventory,
    SptItemDetailsProvider details,
    ProcurementRepository repository,
    ProfileTransactionLockService transactionLock,
    SaveServer saveServer,
    JsonUtil jsonUtil)
{
    private const string RetiredFlare = "6a0000000000000000000001";

    public ProcurementWallet GetWallet(PmcData profile) => WalletFromItems(stashInventory.GetStashItems(profile));

    public long GetBalance(PmcData profile) => GetWallet(profile).roubles;

    public static ProcurementWallet WalletFromItems(IEnumerable<Item> items)
    {
        long roubles = 0, dollars = 0, euros = 0;
        foreach (var item in items)
        {
            long amount = item.GetItemStackSize();
            if (item.Template == Money.ROUBLES) roubles = checked(roubles + amount);
            else if (item.Template == Money.DOLLARS) dollars = checked(dollars + amount);
            else if (item.Template == Money.EUROS) euros = checked(euros + amount);
        }
        return new(roubles, dollars, euros);
    }

    public static string CurrencyCode(MongoId id) => id == Money.ROUBLES ? "RUB"
        : id == Money.DOLLARS ? "USD" : id == Money.EUROS ? "EUR" : "";

    private static bool Unlocked(PmcData profile, TraderBase trader) =>
        profile.TradersInfo != null && profile.TradersInfo.TryGetValue(trader.Id, out var info)
            ? info.Unlocked == true && info.Disabled != true
            : trader.UnlockedByDefault == true;

    public string GetCatalog(TraderCatalogRequest request, MongoId sessionId)
    {
        var profile = profileHelper.GetPmcProfile(sessionId);
        if (profile == null) return jsonUtil.Serialize(new { success = false, message = "PMC profile not found." });
        var traders = traderHelper.GetAllTraders(sessionId);
        var selected = traders.FirstOrDefault(x => x.Id.ToString() == request.TraderId)
            ?? traders.FirstOrDefault(x => Unlocked(profile, x)) ?? traders.FirstOrDefault();
        var offers = new List<object>();
        string error = "";
        if (selected != null && Unlocked(profile, selected))
        {
            try
            {
                var assort = assortHelper.GetAssort(sessionId, selected.Id, false);
                foreach (var root in assort.Items.Where(x => x.ParentId == "hideout" && x.Template.ToString() != RetiredFlare))
                {
                    if (!assort.BarterScheme.TryGetValue(root.Id, out var schemes)) continue;
                    var info = details.GetDetails(root);
                    offers.Add(new
                    {
                        id = root.Id.ToString(), templateId = root.Template.ToString(), name = DisplayName(info.Name),
                        category = info.Category, iconUrl = info.IconUrl,
                        stock = root.Upd?.UnlimitedCount == true ? -1d : root.Upd?.StackObjectsCount ?? 0,
                        remaining = Remaining(root),
                        schemes = schemes.Select((scheme, index) => new
                        {
                            index, token = PriceToken(scheme),
                            costs = scheme.Select(cost => new
                            {
                                templateId = cost.Template.ToString(), count = cost.Count ?? 0,
                                name = DisplayName(details.GetDetails(new Item { Id = new MongoId(), Template = cost.Template }).Name),
                                currency = IsCurrency(cost.Template), currencyCode = CurrencyCode(cost.Template)
                            }).ToList(),
                            supported = scheme.All(IsAutomaticPayment)
                        }).ToList()
                    });
                }
            }
            catch (Exception)
            {
                error = "This trader's stock could not be loaded. Refresh or choose another trader.";
            }
        }
        return jsonUtil.Serialize(new
        {
            success = true, balanceRoubles = GetBalance(profile), wallet = GetWallet(profile), traderId = selected?.Id.ToString() ?? "",
            traders = traders.Select(t => new
            {
                id = t.Id.ToString(), name = DisplayName(string.IsNullOrWhiteSpace(t.Nickname) ? t.Name : t.Nickname),
                unlocked = Unlocked(profile, t), loyalty = profile.TradersInfo?.GetValueOrDefault(t.Id)?.LoyaltyLevel ?? 1
            }).ToList(),
            offers, message = error
        });
    }

    public static int Remaining(Item item) => item.Upd?.BuyRestrictionMax is int limit
        ? Math.Max(0, limit - (item.Upd.BuyRestrictionCurrent ?? 0)) : -1;

    public static string DisplayName(string name) => Regex.Replace(name ?? "",
        @"</?(?:color|size|b|i|u|s|font|material)(?:=[^>]*|\s+[^>]*)?>", "", RegexOptions.IgnoreCase);

    public static bool IsCurrency(MongoId id) => id == Money.ROUBLES || id == Money.DOLLARS || id == Money.EUROS;

    // Special dogtag/functional-item barters require an explicit item picker. Never silently spend them.
    public static bool IsAutomaticPayment(BarterScheme cost) =>
        cost.Count > 0 && double.IsFinite(cost.Count.Value) && cost.Level is null or 0 && cost.Side == null && cost.OnlyFunctional != true;

    public static string PriceToken(IEnumerable<BarterScheme> scheme)
    {
        string quote = string.Join(";", scheme.Select(x => $"{x.Template}:{x.Count?.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}:{x.Level}:{x.Side}:{x.OnlyFunctional}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(quote)));
    }

    public async ValueTask<ItemEventRouterResponse> Purchase(
        PmcData profile, TraderProcurementRequest request, MongoId sessionId,
        ItemEventRouterResponse output, CancellationToken token)
    {
        output.Warnings ??= new();
        ItemEventRouterResponse Fail(string message)
        {
            output.Warnings.Add(new Warning { ErrorMessage = message });
            return output;
        }
        if (profile?.Id == null || request == null || string.IsNullOrWhiteSpace(request.RequestId)
            || request.RequestId.Length > 128 || request.Quantity < 1 || request.Quantity > 1000000)
            return Fail("A valid purchase request and quantity are required.");
        var gate = await transactionLock.AcquireAsync(profile.Id.Value, token);
        try
        {
            var prior = await repository.GetByRequestIdAsync(profile.Id.Value, request.RequestId, token);
            if (prior != null)
            {
                if (prior.Status is not ("Staged" or "CargoCommitted")) return Fail("This request is already recorded. Refresh your stash before submitting a new purchase.");
                SetResult(output, profile, prior);
                return output;
            }
            // Refresh the player-specific assortment immediately before paying.
            var trader = traderHelper.GetAllTraders(sessionId).FirstOrDefault(x => x.Id.ToString() == request.TraderId);
            if (trader == null || !Unlocked(profile, trader)) return Fail("This trader is not available to your PMC.");
            var assort = assortHelper.GetAssort(sessionId, trader.Id, false);
            var offer = assort.Items.FirstOrDefault(x => x.Id.ToString() == request.OfferId && x.ParentId == "hideout");
            if (offer == null || offer.Template.ToString() == RetiredFlare) return Fail("This offer is no longer available. Refresh trader stock.");
            if (!assort.BarterScheme.TryGetValue(offer.Id, out var schemes)
                || request.SchemeIndex < 0 || request.SchemeIndex >= schemes.Count)
                return Fail("The selected trade is no longer available.");
            var scheme = schemes[request.SchemeIndex];
            if (scheme.Count == 0) return Fail("This offer has no valid price. Refresh trader stock.");
            if (PriceToken(scheme) != request.PriceToken) return Fail("The trader's price changed. Refresh and review it before buying.");
            if (!scheme.All(IsAutomaticPayment)) return Fail("This offer requires specific barter items. Complete it at the trader's normal screen.");
            if (Remaining(offer) >= 0 && request.Quantity > Remaining(offer)) return Fail("This quantity exceeds your remaining trader purchase limit.");
            if (offer.Upd?.UnlimitedCount != true && request.Quantity > (offer.Upd?.StackObjectsCount ?? 0))
                return Fail("The trader no longer has that quantity in stock.");

            var stash = stashInventory.GetStashItems(profile);
            var pendingPurchases = await repository.GetPurchasesAsync(profile.Id.Value, token);
            var stagedIds = pendingPurchases.Where(p => p.Status == "Staged")
                .SelectMany(p => p.CargoSelections ?? new()).Select(s => s.itemId).ToHashSet();
            var payment = BuildPayment(stash.Where(x => IsCurrency(x.Template) || !stagedIds.Contains(x.Id.ToString())).ToList(), scheme, request.Quantity);
            if (payment == null) return Fail("Your stash does not contain enough payment items or currency for this trade.");
            var before = stash.ToDictionary(x => x.Id.ToString(), x => x.GetItemStackSize());
            var record = new ProcurementPurchase
            {
                RequestId = request.RequestId, Quantity = request.Quantity, Status = "Processing",
                TraderId = trader.Id.ToString(), OfferId = offer.Id.ToString(),
                TransactionMarker = "TSD trader procurement " + request.RequestId
            };
            await repository.AddPurchaseAsync(profile.Id.Value, record, token);
            int warningCount = output.Warnings.Count;
            try
            {
                tradeHelper.BuyItem(profile, new ProcessBuyTradeRequestData
                {
                    Action = "TradingConfirm", Type = "buy_from_trader", TransactionId = trader.Id,
                    ItemId = offer.Id, Count = request.Quantity, SchemeId = request.SchemeIndex, SchemeItems = payment
                }, sessionId, false, output);
                if (output.Warnings.Count > warningCount)
                {
                    record.Status = "NeedsReview";
                    record.LastError = "SPT returned a trade warning. Check the stash before retrying.";
                    await repository.UpdatePurchaseAsync(profile.Id.Value, record, token);
                    await saveServer.SaveProfileAsync(sessionId, token);
                    return output;
                }
                record.CargoSelections = stashInventory.GetStashItems(profile)
                    .Where(x => x.Template == offer.Template)
                    .Select(x => new ProcurementCargoSelection
                    {
                        itemId = x.Id.ToString(), quantity = Math.Max(0, x.GetItemStackSize() - before.GetValueOrDefault(x.Id.ToString()))
                    }).Where(x => x.quantity > 0).ToList();
                if (record.CargoSelections.Sum(x => x.quantity) != request.Quantity)
                    throw new InvalidOperationException("Purchased quantities could not be verified. Check your stash; do not repeat this request.");
                record.Status = "Staged";
                record.PaidAtUtc = DateTime.UtcNow;
                await repository.UpdatePurchaseAsync(profile.Id.Value, record, token);
                await saveServer.SaveProfileAsync(sessionId, token);
                SetResult(output, profile, record);
            }
            catch (Exception)
            {
                record.Status = "NeedsReview";
                record.LastError = "Trade outcome requires review. Check your stash before buying again.";
                await repository.UpdatePurchaseAsync(profile.Id.Value, record, token);
                await saveServer.SaveProfileAsync(sessionId, token);
                return Fail(record.LastError);
            }
            return output;
        }
        finally { gate.Release(); }
    }

    private void SetResult(ItemEventRouterResponse output, PmcData profile, ProcurementPurchase record)
    {
        output.ExtensionData ??= new();
        output.ExtensionData["tsdProcurement"] = new
        {
            success = true, balanceRoubles = GetBalance(profile), wallet = GetWallet(profile), selections = record.CargoSelections,
            message = "Purchased and selected for your next cargo manifest. Confirm the delivery fee on Cargo."
        };
    }

    public async Task CommitCargoAsync(PmcData profile, string orderId, Dictionary<string, int> selected, CancellationToken token)
    {
        var gate = await transactionLock.AcquireAsync(profile.Id!.Value, token);
        try
        {
            var purchases = await repository.GetPurchasesAsync(profile.Id.Value, token);
            AllocateCargo(purchases, orderId, selected);
            await repository.SavePurchasesAsync(profile.Id.Value, purchases, token);
        }
        finally { gate.Release(); }
    }

    public static void AllocateCargo(List<ProcurementPurchase> purchases, string orderId, Dictionary<string, int> selected)
    {
        var remaining = new Dictionary<string, int>(selected);
        // Subtract prior allocations so retries of an accepted cargo request are harmless.
        foreach (var purchase in purchases)
            if (purchase.CargoAllocations.TryGetValue(orderId, out var allocation))
                foreach (var entry in allocation)
                    if (remaining.ContainsKey(entry.Key)) remaining[entry.Key] = Math.Max(0, remaining[entry.Key] - entry.Value);
        foreach (var purchase in purchases.Where(p => p.Status == "Staged").OrderBy(p => p.CreatedAtUtc))
        {
            foreach (var selection in purchase.CargoSelections)
            {
                int consumed = Math.Min(selection.quantity, remaining.GetValueOrDefault(selection.itemId));
                if (consumed <= 0) continue;
                selection.quantity -= consumed;
                remaining[selection.itemId] -= consumed;
                if (!purchase.CargoAllocations.TryGetValue(orderId, out var allocation))
                    purchase.CargoAllocations[orderId] = allocation = new();
                allocation[selection.itemId] = allocation.GetValueOrDefault(selection.itemId) + consumed;
            }
            purchase.CargoSelections.RemoveAll(s => s.quantity <= 0);
            if (purchase.CargoSelections.Count == 0) purchase.Status = "CargoCommitted";
        }
    }

    public static List<IdWithCount>? BuildPayment(List<Item> stash, List<BarterScheme> scheme, int quantity)
    {
        var result = new List<IdWithCount>();
        var parents = stash.Where(x => !string.IsNullOrEmpty(x.ParentId)).Select(x => x.ParentId!).ToHashSet();
        foreach (var group in scheme.GroupBy(x => x.Template))
        {
            double total = Math.Ceiling(group.Sum(x => x.Count ?? 0) * quantity);
            if (!double.IsFinite(total) || total <= 0 || total > int.MaxValue) return null;
            int remaining = (int)total;
            foreach (var item in stash.Where(x => x.Template == group.Key && !parents.Contains(x.Id.ToString())))
            {
                int count = Math.Min(remaining, item.GetItemStackSize());
                if (count <= 0) continue;
                result.Add(new IdWithCount { Id = item.Id, Count = count });
                remaining -= count;
                if (remaining == 0) break;
            }
            if (remaining > 0) return null;
        }
        return result;
    }
}

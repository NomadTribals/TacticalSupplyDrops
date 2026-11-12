
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class ProcurementPurchaseService(
    ProcurementRepository repository,
    SupplyOrderPaymentProcessor paymentProcessor,
    ProfileTransactionLockService transactionLockService,
    ItemHelper itemHelper,
    MailSendService mailSendService,
    ProfileHelper profileHelper,
    SaveServer saveServer)
{
    private const int CartridgePrice = 15000;
    private const string CartridgeTemplateId =
        "6a0000000000000000000001";

    public async Task<ProcurementPurchaseResult> PurchaseAsync(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        string requestId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (profile?.Id == null ||
            string.IsNullOrWhiteSpace(requestId) ||
            quantity <= 0)
        {
            return Failure("Invalid purchase request.");
        }

        int totalPrice;

        try
        {
            totalPrice = checked(quantity * CartridgePrice);
        }
        catch (OverflowException)
        {
            return Failure("Requested quantity is too large.");
        }

        var profileLock = await transactionLockService.AcquireAsync(
            profile.Id.Value,
            cancellationToken);

        try
        {
            ProcurementPurchase? existingPurchase =
                await repository.GetByRequestIdAsync(
                    profile.Id.Value,
                    requestId,
                    cancellationToken);

            if (existingPurchase != null)
            {
                if (existingPurchase.Status == "Delivered")
                {
                    return new ProcurementPurchaseResult
                    {
                        Success = true,
                        IsDuplicate = true,
                        Message = "This purchase has already been delivered.",
                        Quantity = existingPurchase.Quantity,
                        TotalPrice = existingPurchase.TotalPrice
                    };
                }

                return Failure(
                    "This purchase request has already been recorded. " +
                    "Do not submit it again. Check your EFT Messenger " +
                    "and contact support if delivery is missing.",
                    existingPurchase.Quantity,
                    existingPurchase.TotalPrice);
            }

            if (output?.ProfileChanges == null ||
                !output.ProfileChanges.TryGetValue(
                    sessionId,
                    out var profileChange) ||
                profileChange?.Items?.NewItems == null ||
                profileChange.Items.ChangedItems == null ||
                profileChange.Items.DeletedItems == null)
            {
                return Failure(
                    "Unable to access player inventory changes.");
            }

            var templateResult = itemHelper.GetItem(
                new MongoId(CartridgeTemplateId));

            if (!templateResult.Key ||
                templateResult.Value?.Properties == null)
            {
                return Failure(
                    "The TSD cartridge template could not be found.");
            }

            int maxStack =
                templateResult.Value.Properties.StackMaxSize ?? 1;

            if (maxStack <= 0)
            {
                return Failure(
                    "Invalid cartridge stack configuration.");
            }

            List<Item> purchasedItems = new();
            int remaining = quantity;

            while (remaining > 0)
            {
                int stackCount = Math.Min(remaining, maxStack);

                Upd itemUpd =
                    itemHelper.GenerateUpdForItem(templateResult.Value);

                itemUpd.StackObjectsCount = stackCount;

                purchasedItems.Add(new Item
                {
                    Id = new MongoId(),
                    Template = new MongoId(CartridgeTemplateId),
                    Upd = itemUpd
                });

                remaining -= stackCount;
            }

            if (!paymentProcessor.TryCreatePaymentPlan(
                profile,
                totalPrice,
                out var paymentPlan))
            {
                return Failure(
                    "Insufficient roubles to complete this purchase.",
                    quantity,
                    totalPrice);
            }

            ProcurementPurchase purchase = new()
            {
                RequestId = requestId,
                Quantity = quantity,
                TotalPrice = totalPrice,
                Status = "Pending",
                TransactionMarker =
                    "TSD-PROCUREMENT:" +
                    Guid.NewGuid().ToString("N")
            };

            // Record the request before attempting payment.
            await repository.AddPurchaseAsync(
                profile.Id.Value,
                purchase,
                cancellationToken);

            bool paymentProcessed =
                paymentProcessor.TryProcessPayment(
                    profile,
                    sessionId,
                    output,
                    paymentPlan);

            if (!paymentProcessed)
            {
                purchase.Status = "Failed";
                purchase.LastError =
                    "The payment processor could not apply the payment.";

                await repository.UpdatePurchaseAsync(
                    profile.Id.Value,
                    purchase,
                    cancellationToken);

                return Failure(
                    "Payment could not be processed.",
                    quantity,
                    totalPrice);
            }

            purchase.Status = "Paid";
            purchase.PaidAtUtc = DateTime.UtcNow;

            await repository.UpdatePurchaseAsync(
                profile.Id.Value,
                purchase,
                cancellationToken);

            try
            {
                string message =
                    "TACTICAL SUPPLY DROPS\n\n" +
                    "PROCUREMENT CONFIRMED\n\n" +
                    "Your purchase has been processed successfully.\n\n" +
                    $"Quantity dispatched: {quantity}\n\n" +
                    "Your requested signal cartridges have been dispatched " +
                    "and are waiting for you in your EFT Messenger inbox.\n\n" +
                    "Thank you for choosing Tactical Supply Drops.\n\n" +
                    "TSD — PRIVATE LOGISTICS. ON DEMAND.\n\n" +
                    $"[{purchase.TransactionMarker}]";

                mailSendService.SendSystemMessageToPlayer(
                    sessionId,
                    message,
                    purchasedItems);

                // Find the actual message created by SPT.
                SptProfile fullProfile =
                    profileHelper.GetFullProfile(sessionId);

                Message? deliveryMessage =
                    FindDeliveryMessage(
                        fullProfile,
                        purchase.TransactionMarker);

                if (deliveryMessage == null)
                {
                    throw new InvalidOperationException(
                        "The procurement message could not be found " +
                        "in the player's Messenger records.");
                }

                // Confirm the message contains the correct cartridges.
                if (!HasExpectedCartridges(
                    deliveryMessage,
                    quantity))
                {
                    throw new InvalidOperationException(
                        "The Messenger message did not contain the " +
                        "expected cartridge quantity.");
                }

                purchase.MessageId = (string)deliveryMessage.Id;

                // Persist the modified player profile.
                // A return value of zero is not automatically a failure:
                // SPT can return zero when no disk write was necessary.
                await saveServer.SaveProfileAsync(
                    sessionId,
                    cancellationToken);

                purchase.Status = "Delivered";
                purchase.DeliveredAtUtc = DateTime.UtcNow;
                purchase.LastError = string.Empty;

                await repository.UpdatePurchaseAsync(
                    profile.Id.Value,
                    purchase,
                    cancellationToken);

                return new ProcurementPurchaseResult
                {
                    Success = true,
                    IsDuplicate = false,
                    Message =
                        "Purchase completed. Check your EFT Messenger.",
                    Quantity = quantity,
                    TotalPrice = totalPrice
                };
            }
            catch (Exception exception)
            {
                // Payment has already been applied.
                // Keep the purchase in Paid status for investigation.
                purchase.Status = "Paid";
                purchase.LastError = exception.ToString();

                await repository.UpdatePurchaseAsync(
                    profile.Id.Value,
                    purchase,
                    cancellationToken);

                return Failure(
                    "Payment was processed, but delivery could not be " +
                    "confirmed. Do not submit another purchase. " +
                    "Check your EFT Messenger.",
                    quantity,
                    totalPrice);
            }
        }
        finally
        {
            profileLock.Release();
        }
    }

    private static Message? FindDeliveryMessage(
        SptProfile profile,
        string transactionMarker)
    {
        if (profile.DialogueRecords == null ||
            string.IsNullOrWhiteSpace(transactionMarker))
        {
            return null;
        }

        foreach (Dialogue dialogue in profile.DialogueRecords.Values)
        {
            if (dialogue?.Messages == null)
                continue;

            Message? matchingMessage =
                dialogue.Messages.FirstOrDefault(message =>
                    message.Text?.Contains(
                        transactionMarker,
                        StringComparison.Ordinal) == true);

            if (matchingMessage != null)
                return matchingMessage;
        }

        return null;
    }

    private static bool HasExpectedCartridges(
        Message message,
        int expectedQuantity)
    {
        List<Item>? attachedItems = message.Items?.Data;

        if (attachedItems == null || attachedItems.Count == 0)
            return false;

        int actualQuantity = 0;

        foreach (Item item in attachedItems)
        {
            if ((string)item.Template != CartridgeTemplateId)
                continue;

            actualQuantity = checked(
                actualQuantity + item.GetItemStackSize());
        }

        return actualQuantity == expectedQuantity;
    }

    private static ProcurementPurchaseResult Failure(
        string message,
        int quantity = 0,
        int totalPrice = 0)
    {
        return new ProcurementPurchaseResult
        {
            Success = false,
            Message = message,
            Quantity = quantity,
            TotalPrice = totalPrice
        };
    }
}

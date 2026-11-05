
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Cloners;
using TacticalSupplyDrops.Server.Models;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class SupplyOrderCreationServiceTests
{
    private class FakeCloner : ICloner
    {
        public T? Clone<T>(T? obj)
        {
            if (obj is Item item)
            {
                var copy = new Item
                {
                    Id = item.Id,
                    Template = item.Template,
                    ParentId = item.ParentId,
                    SlotId = item.SlotId,
                    Location = item.Location,
                    Desc = item.Desc,
                    Upd = item.Upd == null
                        ? null
                        : new Upd
                        {
                            StackObjectsCount = item.Upd.StackObjectsCount
                        }
                };

                return (T?)(object)copy;
            }

            if (obj is InsuredItem insuredItem)
            {
                var copy = new InsuredItem
                {
                    TId = insuredItem.TId,
                    ItemId = insuredItem.ItemId,
                    ExtensionData = insuredItem.ExtensionData == null
                        ? new Dictionary<string, object>()
                        : new Dictionary<string, object>(
                            insuredItem.ExtensionData)
                };

                return (T?)(object)copy;
            }

            if (obj is DeletedItem deletedItem)
            {
                var copy = new DeletedItem
                {
                    Id = deletedItem.Id,
                    ExtensionData = new Dictionary<string, object>(
                        deletedItem.ExtensionData)
                };

                return (T?)(object)copy;
            }

            return obj;
        }
    }

    private class FakeProfileDataStore : IProfileDataStore
    {
        private readonly Dictionary<string, object> _data = new();

        public bool FailOnSave { get; set; }

        private static string GetKey(MongoId profileId, string modKey)
        {
            return $"{profileId}_{modKey}";
        }

        public Task<T?> GetAsync<T>(
            MongoId profileId,
            string modKey,
            CancellationToken cancellationToken = default)
        {
            if (_data.TryGetValue(GetKey(profileId, modKey), out var value))
            {
                return Task.FromResult((T?)value);
            }

            return Task.FromResult(default(T));
        }

        public Task SaveAsync<T>(
            MongoId profileId,
            string modKey,
            T data,
            CancellationToken cancellationToken = default)
        {
            if (FailOnSave)
            {
                throw new InvalidOperationException(
                    "Simulated order storage exception.");
            }

            _data[GetKey(profileId, modKey)] = data!;
            return Task.CompletedTask;
        }
    }

    private class FakeItemRemover : IInventoryItemRemover
    {
        public bool SimulatePaymentFailure { get; set; }

        public bool SimulatePaymentException { get; set; }
        public bool SimulateReservationException { get; set; }
        public void RemoveItemByCount(
            PmcData profile,
            MongoId itemId,
            int count,
            MongoId sessionId,
            ItemEventRouterResponse output)
        {
            var item = profile.Inventory.Items.First(
                item => item.Id == itemId);

            int remaining = item.GetItemStackSize() - count;

            if (remaining <= 0)
            {
                profile.Inventory.Items.Remove(item);

                profile.InsuredItems?.RemoveAll(
                    insuredItem => insuredItem.ItemId == itemId);

                output.ProfileChanges[sessionId].Items.DeletedItems.Add(
                    new DeletedItem { Id = itemId });
            }
            else
            {
                item.Upd.StackObjectsCount = remaining;

                output.ProfileChanges[sessionId].Items.ChangedItems.Add(item);
            }

            if (SimulatePaymentException)
            {
                throw new InvalidOperationException(
                    "Simulated payment processing exception.");
            }
        }

        public void RemoveItem(
            PmcData profile,
            MongoId itemId,
            MongoId sessionId,
            ItemEventRouterResponse output)
        {
            var item = profile.Inventory.Items.FirstOrDefault(
                item => item.Id == itemId);

            if (item == null)
            {
                return;
            }

            var children = profile.Inventory.Items
                .Where(child => child.ParentId == (string)itemId)
                .ToList();

            foreach (var child in children)
            {
                RemoveItem(profile, child.Id, sessionId, output);
            }

            profile.Inventory.Items.Remove(item);

            profile.InsuredItems?.RemoveAll(
                insuredItem => insuredItem.ItemId == itemId);

            output.ProfileChanges[sessionId].Items.DeletedItems.Add(
                new DeletedItem { Id = itemId });

            if (SimulatePaymentFailure)
            {
                var money = profile.Inventory.Items.FirstOrDefault(
                    inventoryItem => inventoryItem.Template == Money.ROUBLES);

                if (money != null)
                {
                    money.Upd.StackObjectsCount = 1;
                }
            }
            if (SimulateReservationException)
            {
                throw new InvalidOperationException(
                    "Simulated reservation processing exception.");
            }
        }
    }

    private static SupplyOrderCreationService CreateService(
    out FakeProfileDataStore dataStore,
    bool simulatePaymentFailure = false,
    bool simulatePaymentException = false,
    bool simulateReservationException = false)
    {
        var stashService = new StashInventoryService();

        var fakeRemover = new FakeItemRemover
        {
            SimulatePaymentFailure = simulatePaymentFailure,
            SimulatePaymentException = simulatePaymentException,
            SimulateReservationException = simulateReservationException
        };

        dataStore = new FakeProfileDataStore();

        var repository = new SupplyOrderRepository(dataStore);

        var dimensionsProvider = new TestDimensionsProvider();
        var footprintService = new ItemFootprintService(dimensionsProvider);
        var limitService = new DeliveryFootprintLimitService(footprintService);

        var selectionService = new DeliverySelectionService(
            stashService,
            limitService);

        var itemDetailsProvider = new TestItemDetailsProvider();
        var itemDetailsService = new StashItemDetailsService(itemDetailsProvider);

        var pricingService = new SupplyOrderPricingService();
        var orderService = new SupplyOrderService();

        var paymentPlanService = new StashPaymentPlanService(stashService);

        var paymentDeductionService = new StashPaymentDeductionService(
            stashService,
            fakeRemover);

        var paymentProcessor = new SupplyOrderPaymentProcessor(
            paymentPlanService,
            paymentDeductionService);

        var reservationService = new SupplyOrderItemReservationService(
            stashService,
            fakeRemover);

        return new SupplyOrderCreationService(
    selectionService,
    itemDetailsService,
    pricingService,
    orderService,
    paymentProcessor,
    reservationService,
    repository,
    new FakeCloner(),
    new ProfileTransactionLockService());
    }

    private static ItemEventRouterResponse CreateOutput(MongoId sessionId)
    {
        return new ItemEventRouterResponse
        {
            ProfileChanges = new Dictionary<MongoId, ProfileChange>
            {
                [sessionId] = new ProfileChange
                {
                    Items = new ItemChanges
                    {
                        NewItems = new List<Item>(),
                        ChangedItems = new List<Item>(),
                        DeletedItems = new List<DeletedItem>()
                    }
                }
            },
            Warnings = null
        };
    }

    [Fact]
    public async Task TryCreateOrder_WithValidSelection_ShouldCreatePendingOrder()
    {
        var service = CreateService(out _);
        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b000000000000000000002",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000010",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);
        var output = CreateOutput(sessionId);

        var result = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            new List<string> { (string)item.Id },
            FenceReputationTier.Neutral);

        Assert.True(result.Success);
        Assert.NotNull(result.Order);
        Assert.False(result.IsDuplicate);
        Assert.Equal(SupplyOrderStatus.Pending, result.Order.Status);
        Assert.Equal(15500, result.Order.TotalPrice);
        Assert.Single(result.Order.Items);
        Assert.Equal((string)item.Id, result.Order.Items[0].ItemId);
        Assert.Single(result.Order.Items[0].Items);
        Assert.NotSame(item, result.Order.Items[0].Items[0]);
        Assert.Equal(6, result.TotalFootprint);

        Assert.DoesNotContain(
            profile.Inventory.Items,
            stashItem => stashItem.Id == item.Id);

        Assert.Equal(84500, money.Upd.StackObjectsCount);
    }

    [Fact]
    public async Task TryCreateOrder_WithSameRequestIdTwice_ShouldNotChargeOrReserveItemsTwice()
    {
        var service = CreateService(out var dataStore);
        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        const string requestId = "duplicate-request-test-001";

        var item = CreateItem(
            "64b000000000000000000090",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000091",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);

        var firstOutput = CreateOutput(sessionId);

        var firstResult = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            firstOutput,
            new List<string> { (string)item.Id },
            FenceReputationTier.Neutral,
            requestId: requestId);

        Assert.True(firstResult.Success);
        Assert.NotNull(firstResult.Order);
        Assert.False(firstResult.IsDuplicate);

        Assert.Equal(84500, money.Upd.StackObjectsCount);

        Assert.DoesNotContain(
            profile.Inventory.Items,
            stashItem => stashItem.Id == item.Id);

        var secondOutput = CreateOutput(sessionId);

        var secondResult = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            secondOutput,
            new List<string> { (string)item.Id },
            FenceReputationTier.Neutral,
            requestId: requestId);

        Assert.True(secondResult.Success);
        Assert.NotNull(secondResult.Order);
        Assert.True(secondResult.IsDuplicate);

        Assert.Equal(firstResult.Order.OrderId, secondResult.Order.OrderId);
        Assert.Equal(firstResult.Order.TotalPrice, secondResult.Order.TotalPrice);
        Assert.Equal(firstResult.TotalFootprint, secondResult.TotalFootprint);

        Assert.Equal(84500, money.Upd.StackObjectsCount);

        Assert.DoesNotContain(
            profile.Inventory.Items,
            stashItem => stashItem.Id == item.Id);

        var savedOrders = await dataStore.GetAsync<List<SupplyOrder>>(
            profile.Id!.Value,
            "tactical_supply_drops");

        Assert.NotNull(savedOrders);
        Assert.Single(savedOrders);
        Assert.Equal(requestId, savedOrders[0].RequestId);
    }

    [Fact]
    public async Task TryCreateOrder_WithItemOutsideStash_ShouldRejectOrder()
    {
        var service = CreateService(out _);
        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b000000000000000000003",
            "equipment");

        var money = CreateMoney(
            "64b000000000000000000011",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);
        var output = CreateOutput(sessionId);

        var result = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            new List<string> { (string)item.Id },
            FenceReputationTier.Neutral);

        Assert.False(result.Success);
        Assert.Null(result.Order);
        Assert.Equal(0, result.TotalFootprint);
        Assert.Equal(100000, money.Upd.StackObjectsCount);
    }

    [Fact]
    public async Task TryCreateOrder_OverFootprintLimit_ShouldRejectOrder()
    {
        var service = CreateService(out _);
        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item1 = CreateItem(
            "64b000000000000000000004",
            (string)stashId);

        var item2 = CreateItem(
            "64b000000000000000000005",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000012",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item1, item2, money);
        var output = CreateOutput(sessionId);

        var result = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            new List<string>
            {
                (string)item1.Id,
                (string)item2.Id
            },
            FenceReputationTier.Neutral);

        Assert.False(result.Success);
        Assert.Null(result.Order);
        Assert.Equal(0, result.TotalFootprint);
        Assert.Equal(100000, money.Upd.StackObjectsCount);
    }

    [Fact]
    public async Task TryCreateOrder_WithInsufficientFunds_ShouldRejectOrder()
    {
        var service = CreateService(out _);
        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b000000000000000000002",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000010",
            (string)stashId,
            5000);

        var profile = CreateProfile(stashId, item, money);
        var output = CreateOutput(sessionId);

        var result = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            new List<string> { (string)item.Id },
            FenceReputationTier.Neutral);

        Assert.False(result.Success);
        Assert.Null(result.Order);
        Assert.Equal(6, result.TotalFootprint);

        Assert.Contains(
            profile.Inventory.Items,
            stashItem => stashItem.Id == item.Id);

        Assert.Equal(5000, money.Upd.StackObjectsCount);
    }

    [Fact]
    public async Task TryCreateOrder_WithContainerAndChildSelected_ShouldPreserveBothItemsInOrder()
    {
        var service = CreateService(out _);
        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var container = CreateItem(
            "64b000000000000000000030",
            (string)stashId);

        var child = CreateItem(
            "64b000000000000000000031",
            (string)container.Id);

        var money = CreateMoney(
            "64b000000000000000000032",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, container, child, money);
        var output = CreateOutput(sessionId);

        var result = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            new List<string> { (string)container.Id },
            FenceReputationTier.Neutral);

        Assert.True(result.Success);
        Assert.NotNull(result.Order);

        Assert.Equal(2, result.Order.Items.Count);

        var storedContainer = result.Order.Items
            .SelectMany(orderItem => orderItem.Items)
            .First(item => item.Id == container.Id);

        var storedChild = result.Order.Items
            .SelectMany(orderItem => orderItem.Items)
            .First(item => item.Id == child.Id);

        Assert.NotSame(container, storedContainer);
        Assert.NotSame(child, storedChild);
        Assert.Equal((string)container.Id, storedChild.ParentId);

        Assert.DoesNotContain(
            profile.Inventory.Items,
            item => item.Id == container.Id || item.Id == child.Id);

        Assert.Equal(16000, result.Order.TotalPrice);
        Assert.Equal(2, result.TotalFootprint);
        Assert.Equal(84000, money.Upd.StackObjectsCount);
    }

    [Fact]
    public async Task TryCreateOrder_WhenPaymentFails_ShouldRestoreReservedItemsMoneyAndInsurance()
    {
        var service = CreateService(
            out _,
            simulatePaymentFailure: true);

        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b000000000000000000040",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000041",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);

        profile.InsuredItems = new List<InsuredItem>
        {
            new InsuredItem
            {
                TId = new MongoId("64b000000000000000000050"),
                ItemId = item.Id,
                ExtensionData = new Dictionary<string, object>()
            }
        };

        var output = CreateOutput(sessionId);

        var result = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            new List<string> { (string)item.Id },
            FenceReputationTier.Neutral);

        Assert.False(result.Success);
        Assert.Null(result.Order);

        Assert.Contains(
            profile.Inventory.Items,
            inventoryItem => inventoryItem.Id == item.Id);

        var restoredMoney = profile.Inventory.Items.First(
            inventoryItem => inventoryItem.Id == money.Id);

        Assert.Equal(100000, restoredMoney.Upd.StackObjectsCount);

        Assert.Single(profile.InsuredItems);
        Assert.Equal(item.Id, profile.InsuredItems[0].ItemId);

        Assert.Empty(
            output.ProfileChanges[sessionId].Items.DeletedItems);

        Assert.Equal(1, result.TotalFootprint);
    }

    [Fact]
    public async Task TryCreateOrder_WhenPaymentThrows_ShouldRestoreReservedItemsMoneyInsuranceAndResponse()
    {
        var service = CreateService(
            out _,
            simulatePaymentException: true);

        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b000000000000000000060",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000061",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);

        profile.InsuredItems = new List<InsuredItem>
        {
            new InsuredItem
            {
                TId = new MongoId("64b000000000000000000062"),
                ItemId = item.Id,
                ExtensionData = new Dictionary<string, object>()
            }
        };

        var output = CreateOutput(sessionId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TryCreateOrderAsync(
                profile,
                sessionId,
                output,
                new List<string> { (string)item.Id },
                FenceReputationTier.Neutral));

        Assert.Equal(
            "Simulated payment processing exception.",
            exception.Message);

        Assert.Contains(
            profile.Inventory.Items,
            inventoryItem => inventoryItem.Id == item.Id);

        var restoredMoney = profile.Inventory.Items.First(
            inventoryItem => inventoryItem.Id == money.Id);

        Assert.Equal(100000, restoredMoney.Upd.StackObjectsCount);

        Assert.Single(profile.InsuredItems);
        Assert.Equal(item.Id, profile.InsuredItems[0].ItemId);

        Assert.Empty(
            output.ProfileChanges[sessionId].Items.DeletedItems);
    }

    [Fact]
    public async Task TryCreateOrder_WhenReservationThrows_ShouldRestoreInventoryMoneyInsuranceAndResponse()
    {
        var service = CreateService(
            out var dataStore,
            simulateReservationException: true);

        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b0000000000000000000A0",
            (string)stashId);

        var money = CreateMoney(
            "64b0000000000000000000A1",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);

        profile.InsuredItems = new List<InsuredItem>
    {
        new InsuredItem
        {
            TId = new MongoId("64b0000000000000000000A2"),
            ItemId = item.Id,
            ExtensionData = new Dictionary<string, object>()
        }
    };

        var output = CreateOutput(sessionId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TryCreateOrderAsync(
                profile,
                sessionId,
                output,
                new List<string> { (string)item.Id },
                FenceReputationTier.Neutral));

        Assert.Equal(
            "Simulated reservation processing exception.",
            exception.Message);

        // The removed item must be restored.
        Assert.Contains(
            profile.Inventory.Items,
            inventoryItem => inventoryItem.Id == item.Id);

        // Payment must not have been deducted.
        var restoredMoney = profile.Inventory.Items.First(
            inventoryItem => inventoryItem.Id == money.Id);

        Assert.Equal(100000, restoredMoney.Upd.StackObjectsCount);

        // Insurance must be restored.
        Assert.Single(profile.InsuredItems);
        Assert.Equal(item.Id, profile.InsuredItems[0].ItemId);

        // All response changes must be rolled back.
        Assert.Empty(
            output.ProfileChanges[sessionId].Items.NewItems);

        Assert.Empty(
            output.ProfileChanges[sessionId].Items.ChangedItems);

        Assert.Empty(
            output.ProfileChanges[sessionId].Items.DeletedItems);

        // No order should have been saved.
        var savedOrders = await dataStore.GetAsync<List<SupplyOrder>>(
            profile.Id!.Value,
            "tactical_supply_drops");

        Assert.Null(savedOrders);
    }

    [Fact]
    public async Task TryCreateOrder_WithValidSelection_ShouldSaveOrderToProfileData()
    {
        var service = CreateService(out var dataStore);

        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b000000000000000000070",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000071",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);
        var output = CreateOutput(sessionId);

        var result = await service.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            new List<string> { (string)item.Id },
            FenceReputationTier.Neutral);

        Assert.True(result.Success);
        Assert.NotNull(result.Order);

        var savedOrders = await dataStore.GetAsync<List<SupplyOrder>>(
            profile.Id!.Value,
            "tactical_supply_drops");

        Assert.NotNull(savedOrders);
        var savedOrder = Assert.Single(savedOrders);

        Assert.Equal(result.Order.OrderId, savedOrder.OrderId);
        Assert.Equal(result.Order.TotalPrice, savedOrder.TotalPrice);
    }

    [Fact]
    public async Task TryCreateOrder_WhenSavingOrderThrows_ShouldRestoreInventoryMoneyAndResponse()
    {
        var service = CreateService(out var dataStore);
        dataStore.FailOnSave = true;

        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = CreateItem(
            "64b000000000000000000080",
            (string)stashId);

        var money = CreateMoney(
            "64b000000000000000000081",
            (string)stashId,
            100000);

        var profile = CreateProfile(stashId, item, money);
        var output = CreateOutput(sessionId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TryCreateOrderAsync(
                profile,
                sessionId,
                output,
                new List<string> { (string)item.Id },
                FenceReputationTier.Neutral));

        Assert.Equal(
            "Simulated order storage exception.",
            exception.Message);

        Assert.Contains(
            profile.Inventory.Items,
            inventoryItem => inventoryItem.Id == item.Id);

        var restoredMoney = profile.Inventory.Items.First(
            inventoryItem => inventoryItem.Id == money.Id);

        Assert.Equal(100000, restoredMoney.Upd.StackObjectsCount);

        Assert.Empty(
            output.ProfileChanges[sessionId].Items.DeletedItems);
    }

    private static Item CreateItem(string id, string parentId)
    {
        return new Item
        {
            Id = new MongoId(id),
            Template = new MongoId("64b000000000000000000099"),
            ParentId = parentId
        };
    }

    private static Item CreateMoney(
        string id,
        string parentId,
        int amount)
    {
        return new Item
        {
            Id = new MongoId(id),
            Template = Money.ROUBLES,
            ParentId = parentId,
            Upd = new Upd { StackObjectsCount = amount }
        };
    }

    private static PmcData CreateProfile(
        MongoId stashId,
        params Item[] items)
    {
        return new PmcData
        {
            Id = new MongoId("64b000000000000000000099"),
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item>(items)
            }
        };
    }

    private class TestDimensionsProvider : IItemDimensionsProvider
    {
        public (int? Width, int? Height) GetDimensions(Item item)
        {
            string id = (string)item.Id;

            return id switch
            {
                "64b000000000000000000002" => (2, 3),
                "64b000000000000000000004" => (3, 3),
                "64b000000000000000000005" => (3, 3),
                _ => (1, 1)
            };
        }
    }


    private class TestItemDetailsProvider : IItemDetailsProvider
    {
        public (string Name, int BaseValue, int Width, int Height, string IconUrl, string Category)
           GetDetails(Item item)
        {
            return ("Test Item", 10000, 1, 1, string.Empty, "OTHER");
        }
        public bool IsInstalledInEquipmentSlot(Item item, Item? parent)
        {
            return false;
        }
    }

}

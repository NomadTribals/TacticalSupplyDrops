
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TacticalSupplyDrops.Server.Models;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class SupplyOrderConcurrencyTests
{
    private class TestCloner : ICloner
    {
        public T? Clone<T>(T? obj)
        {
            if (obj is Item item)
            {
                return (T?)(object)new Item
                {
                    Id = item.Id,
                    Template = item.Template,
                    ParentId = item.ParentId,
                    SlotId = item.SlotId,
                    Location = item.Location,
                    Upd = item.Upd == null
                        ? null
                        : new Upd
                        {
                            StackObjectsCount = item.Upd.StackObjectsCount
                        }
                };
            }

            return obj;
        }
    }

    private class TestDataStore : IProfileDataStore
    {
        private readonly Dictionary<string, object> _data = new();
        private int _readCount;

        public TaskCompletionSource<bool> FirstReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowFirstRead { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static string GetKey(MongoId profileId, string modKey)
            => $"{profileId}_{modKey}";

        public async Task<T?> GetAsync<T>(
            MongoId profileId,
            string modKey,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _readCount) == 1)
            {
                FirstReadStarted.TrySetResult(true);
                await AllowFirstRead.Task.WaitAsync(cancellationToken);
            }

            if (_data.TryGetValue(GetKey(profileId, modKey), out var value))
            {
                return (T?)value;
            }

            return default;
        }

        public Task SaveAsync<T>(
            MongoId profileId,
            string modKey,
            T data,
            CancellationToken cancellationToken = default)
        {
            _data[GetKey(profileId, modKey)] = data!;
            return Task.CompletedTask;
        }
    }

    private class TestItemRemover : IInventoryItemRemover
    {
        public void RemoveItemByCount(
            PmcData profile,
            MongoId itemId,
            int count,
            MongoId sessionId,
            ItemEventRouterResponse output)
        {
            var item = profile.Inventory.Items.First(
                existing => existing.Id == itemId);

            int remaining = item.GetItemStackSize() - count;

            if (remaining <= 0)
            {
                RemoveItem(profile, itemId, sessionId, output);
            }
            else
            {
                item.Upd!.StackObjectsCount = remaining;

                output.ProfileChanges[sessionId]
                    .Items.ChangedItems.Add(item);
            }
        }

        public void RemoveItem(
            PmcData profile,
            MongoId itemId,
            MongoId sessionId,
            ItemEventRouterResponse output)
        {
            var item = profile.Inventory.Items.FirstOrDefault(
                existing => existing.Id == itemId);

            if (item == null)
            {
                return;
            }

            profile.Inventory.Items.Remove(item);

            output.ProfileChanges[sessionId]
                .Items.DeletedItems.Add(new DeletedItem { Id = itemId });
        }
    }

    [Fact]
    public async Task TryCreateOrder_WhenSameRequestArrivesConcurrently_ShouldCreateOnlyOneOrder()
    {
        var dataStore = new TestDataStore();
        var repository = new SupplyOrderRepository(dataStore);

        var stashService = new StashInventoryService();
        var remover = new TestItemRemover();

        var footprintService = new ItemFootprintService(
            new TestDimensionsProvider());

        var selectionService = new DeliverySelectionService(
            stashService,
            new DeliveryFootprintLimitService(footprintService));

        var detailsService = new StashItemDetailsService(
            new TestItemDetailsProvider());

        var paymentProcessor = new SupplyOrderPaymentProcessor(
            new StashPaymentPlanService(stashService),
            new StashPaymentDeductionService(stashService, remover));

        var reservationService = new SupplyOrderItemReservationService(
            stashService,
            remover);

        var service = new SupplyOrderCreationService(
     selectionService,
     detailsService,
     new SupplyOrderPricingService(),
     new SupplyOrderService(),
     paymentProcessor,
     reservationService,
     repository,
     new TestCloner(),
     new ProfileTransactionLockService());

        var stashId = new MongoId("64b000000000000000000001");
        var sessionId = new MongoId("64b000000000000000000020");

        var item = new Item
        {
            Id = new MongoId("64b000000000000000000100"),
            Template = new MongoId("64b000000000000000000099"),
            ParentId = (string)stashId
        };

        var money = new Item
        {
            Id = new MongoId("64b000000000000000000101"),
            Template = Money.ROUBLES,
            ParentId = (string)stashId,
            Upd = new Upd { StackObjectsCount = 100000 }
        };

        var profile = new PmcData
        {
            Id = new MongoId("64b000000000000000000099"),
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { item, money }
            }
        };

        ItemEventRouterResponse CreateOutput()
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
                }
            };
        }

        const string requestId = "concurrent-request-test-001";

        var firstTask = Task.Run(() =>
            service.TryCreateOrderAsync(
                profile,
                sessionId,
                CreateOutput(),
                new List<string> { (string)item.Id },
                FenceReputationTier.Neutral,
                requestId: requestId));

        // Wait until the first request reaches the data store.
        await dataStore.FirstReadStarted.Task;

        // Start a second request while the first is still waiting.
        var secondTask = Task.Run(() =>
            service.TryCreateOrderAsync(
                profile,
                sessionId,
                CreateOutput(),
                new List<string> { (string)item.Id },
                FenceReputationTier.Neutral,
                requestId: requestId));

        // Let the first request continue.
        dataStore.AllowFirstRead.TrySetResult(true);

        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.All(results, result => Assert.True(result.Success));
        Assert.All(results, result => Assert.NotNull(result.Order));

        Assert.Equal(
            results[0].Order!.OrderId,
            results[1].Order!.OrderId);

        Assert.Single(results.Where(result => !result.IsDuplicate));
        Assert.Single(results.Where(result => result.IsDuplicate));

        Assert.Equal(94900, money.Upd!.StackObjectsCount);

        Assert.DoesNotContain(
            profile.Inventory.Items,
            existing => existing.Id == item.Id);

        var savedOrders = await dataStore.GetAsync<List<SupplyOrder>>(
            profile.Id!.Value,
            "tactical_supply_drops");

        Assert.NotNull(savedOrders);
        Assert.Single(savedOrders);
        Assert.Equal(requestId, savedOrders[0].RequestId);
    }

    private class TestDimensionsProvider : IItemDimensionsProvider
    {
        public (int? Width, int? Height) GetDimensions(Item item)
            => (1, 1);
    }


    private class TestItemDetailsProvider : IItemDetailsProvider
    {
        public double GetUnitWeight(Item item) => .1;
        public (string Name, int BaseValue, int Width, int Height, string IconUrl, string Category)
            GetDetails(Item item)
            => ("Test Item", 10000, 1, 1, string.Empty, "OTHER");
        public bool IsInstalledInEquipmentSlot(Item item, Item? parent)
        {
            return false;
        }
    }

}

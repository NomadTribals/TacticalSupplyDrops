
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Server.Core.Models.Common;
using TacticalSupplyDrops.Server.Models;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class SupplyOrderRepositoryTests
{
    [Fact]
    public async Task AddOrderAsync_ShouldSaveOrder()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);
        var profileId = new MongoId("111111111111111111111111");

        var order = new SupplyOrder
        {
            TotalPrice = 25000
        };

        await repository.AddOrderAsync(profileId, order);

        var savedOrders = await repository.GetOrdersAsync(profileId);

        Assert.Single(savedOrders);
        Assert.Equal(order.OrderId, savedOrders[0].OrderId);
        Assert.Equal(25000, savedOrders[0].TotalPrice);
    }

    [Fact]
    public async Task GetOrderAsync_ShouldReturnMatchingOrder()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);
        var profileId = new MongoId("111111111111111111111111");

        var order = new SupplyOrder
        {
            TotalPrice = 30000
        };

        await repository.AddOrderAsync(profileId, order);

        var result = await repository.GetOrderAsync(
            profileId,
            order.OrderId);

        Assert.NotNull(result);
        Assert.Equal(order.OrderId, result.OrderId);
    }

    [Fact]
    public async Task GetOrderAsync_WithUnknownId_ShouldReturnNull()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);
        var profileId = new MongoId("111111111111111111111111");

        var result = await repository.GetOrderAsync(
            profileId,
            "unknown-order");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrderByRequestIdAsync_ShouldReturnMatchingOrder()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);
        var profileId = new MongoId("111111111111111111111111");

        var order = new SupplyOrder
        {
            RequestId = "request-123",
            TotalPrice = 30000
        };

        await repository.AddOrderAsync(profileId, order);

        var result = await repository.GetOrderByRequestIdAsync(
            profileId,
            "request-123");

        Assert.NotNull(result);
        Assert.Equal(order.OrderId, result.OrderId);
        Assert.Equal("request-123", result.RequestId);
    }

    [Fact]
    public async Task GetOrderByRequestIdAsync_WithUnknownId_ShouldReturnNull()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);
        var profileId = new MongoId("111111111111111111111111");

        var result = await repository.GetOrderByRequestIdAsync(
            profileId,
            "unknown-request");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrderByRequestIdAsync_ShouldNotReturnAnotherProfilesOrder()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);

        var firstProfile = new MongoId("111111111111111111111111");
        var secondProfile = new MongoId("222222222222222222222222");

        var order = new SupplyOrder
        {
            RequestId = "request-123",
            TotalPrice = 30000
        };

        await repository.AddOrderAsync(firstProfile, order);

        var result = await repository.GetOrderByRequestIdAsync(
            secondProfile,
            "request-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateOrderAsync_ShouldSaveUpdatedOrder()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);
        var profileId = new MongoId("111111111111111111111111");

        var order = new SupplyOrder
        {
            TotalPrice = 30000
        };

        await repository.AddOrderAsync(profileId, order);

        order.Status = SupplyOrderStatus.Delivered;

        bool updated = await repository.UpdateOrderAsync(
            profileId,
            order);

        var savedOrder = await repository.GetOrderAsync(
            profileId,
            order.OrderId);

        Assert.True(updated);
        Assert.NotNull(savedOrder);
        Assert.Equal(SupplyOrderStatus.Delivered, savedOrder.Status);
    }

    [Fact]
    public async Task UpdateOrderAsync_WithUnknownOrder_ShouldReturnFalse()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);
        var profileId = new MongoId("111111111111111111111111");

        var order = new SupplyOrder
        {
            TotalPrice = 30000
        };

        bool updated = await repository.UpdateOrderAsync(
            profileId,
            order);

        Assert.False(updated);
    }

    [Fact]
    public async Task Orders_ShouldBeSeparateForDifferentProfiles()
    {
        var store = new FakeProfileDataStore();
        var repository = new SupplyOrderRepository(store);

        var firstProfile = new MongoId("111111111111111111111111");
        var secondProfile = new MongoId("222222222222222222222222");

        var firstOrder = new SupplyOrder { TotalPrice = 10000 };
        var secondOrder = new SupplyOrder { TotalPrice = 20000 };

        await repository.AddOrderAsync(firstProfile, firstOrder);
        await repository.AddOrderAsync(secondProfile, secondOrder);

        var firstOrders = await repository.GetOrdersAsync(firstProfile);
        var secondOrders = await repository.GetOrdersAsync(secondProfile);

        Assert.Single(firstOrders);
        Assert.Single(secondOrders);

        Assert.Equal(firstOrder.OrderId, firstOrders[0].OrderId);
        Assert.Equal(secondOrder.OrderId, secondOrders[0].OrderId);
    }

    private class FakeProfileDataStore : IProfileDataStore
    {
        private readonly Dictionary<string, object> _data = new();

        public Task<T?> GetAsync<T>(
            MongoId profileId,
            string modKey,
            CancellationToken cancellationToken = default)
        {
            string key = $"{profileId}_{modKey}";

            if (_data.TryGetValue(key, out object? value))
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
            string key = $"{profileId}_{modKey}";

            _data[key] = data!;

            return Task.CompletedTask;
        }
    }
}

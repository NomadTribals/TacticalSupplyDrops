
using System;
using System.Collections.Generic;
using TacticalSupplyDrops.Server.Models;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class DeliveryPricingTests
{
    private readonly DeliveryPricingService _pricingService = new();

    [Fact]
    public void PoorReputation_ShouldCost22500()
    {
        int fee = _pricingService.GetDeliveryFee(FenceReputationTier.Poor);
        Assert.Equal(22500, fee);
    }

    [Fact]
    public void NeutralReputation_ShouldCost15000()
    {
        int fee = _pricingService.GetDeliveryFee(FenceReputationTier.Neutral);
        Assert.Equal(15000, fee);
    }

    [Fact]
    public void GoodReputation_ShouldCost13500()
    {
        int fee = _pricingService.GetDeliveryFee(FenceReputationTier.Good);
        Assert.Equal(13500, fee);
    }

    [Fact]
    public void ExcellentReputation_ShouldCost11250()
    {
        int fee = _pricingService.GetDeliveryFee(FenceReputationTier.Excellent);
        Assert.Equal(11250, fee);
    }

    [Fact]
    public void ItemWorth10000_ShouldHaveDeliveryFeeOf500()
    {
        var service = new ItemDeliveryPricingService();
        int fee = service.CalculateItemDeliveryFee(10000, 1);
        Assert.Equal(500, fee);
    }

    [Fact]
    public void ThreeItemsWorth10000_ShouldHaveDeliveryFeeOf1500()
    {
        var service = new ItemDeliveryPricingService();
        int fee = service.CalculateItemDeliveryFee(10000, 3);
        Assert.Equal(1500, fee);
    }

    [Fact]
    public void InvalidItemValue_ShouldThrowException()
    {
        var service = new ItemDeliveryPricingService();

        Action action = () => service.CalculateItemDeliveryFee(-100, 1);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void ZeroQuantity_ShouldThrowException()
    {
        var service = new ItemDeliveryPricingService();

        Action action = () => service.CalculateItemDeliveryFee(10000, 0);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void SupplyOrderWithExcellentReputation_ShouldCost12188()
    {
        var service = new SupplyOrderPricingService();

        var items = new List<(int ItemBaseValue, int Quantity)>
        {
            (10000, 1),
            (5000, 3)
        };

        int price = service.CalculateSupplyOrderPrice(
            items,
            FenceReputationTier.Excellent);

        Assert.Equal(12188, price);
    }

    [Fact]
    public void SupplyOrderWithNeutralReputation_ShouldCost16250()
    {
        var service = new SupplyOrderPricingService();

        var items = new List<(int ItemBaseValue, int Quantity)>
        {
            (10000, 1),
            (5000, 3)
        };

        int price = service.CalculateSupplyOrderPrice(
            items,
            FenceReputationTier.Neutral);

        Assert.Equal(16250, price);
    }
}

public class SupplyOrderModelTests
{
    [Fact]
    public void NewOrder_ShouldHaveUniqueOrderId()
    {
        var firstOrder = new SupplyOrder();
        var secondOrder = new SupplyOrder();

        Assert.NotEqual(firstOrder.OrderId, secondOrder.OrderId);
    }

    [Fact]
    public void NewOrder_ShouldHavePendingStatus()
    {
        var order = new SupplyOrder();

        Assert.Equal(SupplyOrderStatus.Pending, order.Status);
    }

    [Fact]
    public void NewOrder_ShouldHaveEmptyItemList()
    {
        var order = new SupplyOrder();

        Assert.Empty(order.Items);
    }

    [Fact]
    public void NewOrder_ShouldHaveZeroTotalPrice()
    {
        var order = new SupplyOrder();

        Assert.Equal(0, order.TotalPrice);
    }

    [Fact]
    public void NewOrder_ShouldHaveCreationTimestamp()
    {
        var beforeCreation = DateTime.UtcNow;

        var order = new SupplyOrder();

        var afterCreation = DateTime.UtcNow;

        Assert.InRange(
            order.CreatedAt,
            beforeCreation,
            afterCreation);
    }

    [Fact]
    public void SupplyOrderItem_ShouldStoreItemIdAndQuantity()
    {
        var item = new SupplyOrderItem("test-item-123", 3);

        Assert.Equal("test-item-123", item.ItemId);
        Assert.Equal(3, item.Quantity);
    }

    public class SupplyOrderServiceTests
    {
        private readonly SupplyOrderService _service = new();

        [Fact]
        public void CreateOrder_WithValidItems_ShouldCreatePendingOrder()
        {
            var items = new List<SupplyOrderItem>
            {
                new("item-123", 2)
            };

            var order = _service.CreateOrder(items, 15000);

            Assert.NotNull(order);
            Assert.Equal(15000, order.TotalPrice);
            Assert.Equal(SupplyOrderStatus.Pending, order.Status);
            Assert.Single(order.Items);
        }

        [Fact]
        public void CreateOrder_WithNoItems_ShouldThrowException()
        {
            var items = new List<SupplyOrderItem>();

            Assert.Throws<ArgumentException>(
                () => _service.CreateOrder(items, 15000));
        }

        [Fact]
        public void CreateOrder_WithEmptyItemId_ShouldThrowException()
        {
            var items = new List<SupplyOrderItem>
            {
                new("", 1)
            };

            Assert.Throws<ArgumentException>(
                () => _service.CreateOrder(items, 15000));
        }

        [Fact]
        public void CreateOrder_WithZeroQuantity_ShouldThrowException()
        {
            var items = new List<SupplyOrderItem>
            {
                new("item-123", 0)
            };

            Assert.Throws<ArgumentException>(
                () => _service.CreateOrder(items, 15000));
        }

        [Fact]
        public void CreateOrder_WithNegativePrice_ShouldThrowException()
        {
            var items = new List<SupplyOrderItem>
            {
                new("item-123", 1)
            };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => _service.CreateOrder(items, -100));
        }
    }
}

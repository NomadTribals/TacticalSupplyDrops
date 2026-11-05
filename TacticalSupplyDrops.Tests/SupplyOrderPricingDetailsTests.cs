using System.Collections.Generic;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class SupplyOrderPricingDetailsTests
{
    private readonly SupplyOrderPricingService _service = new();

    [Fact]
    public void CalculateSupplyOrderPrice_WithStashDetails_ShouldCalculateCorrectPrice()
    {
        var items = new List<StashItemDetails>
        {
            new()
            {
                ItemId = "item-001",
                Name = "Test Item One",
                BaseValue = 10000,
                Quantity = 1
            },
            new()
            {
                ItemId = "item-002",
                Name = "Test Item Two",
                BaseValue = 5000,
                Quantity = 3
            }
        };

        int price = _service.CalculateSupplyOrderPrice(
            items,
            FenceReputationTier.Neutral);

        Assert.Equal(16250, price);
    }

    [Fact]
    public void CalculateSupplyOrderPrice_WithExcellentReputation_ShouldApplyDiscount()
    {
        var items = new List<StashItemDetails>
        {
            new()
            {
                ItemId = "item-001",
                BaseValue = 10000,
                Quantity = 1
            },
            new()
            {
                ItemId = "item-002",
                BaseValue = 5000,
                Quantity = 3
            }
        };

        int price = _service.CalculateSupplyOrderPrice(
            items,
            FenceReputationTier.Excellent);

        Assert.Equal(12188, price);
    }

    [Fact]
    public void CalculateSupplyOrderPrice_WithEmptyItems_ShouldReturnBaseFee()
    {
        var items = new List<StashItemDetails>();

        int price = _service.CalculateSupplyOrderPrice(
            items,
            FenceReputationTier.Neutral);

        Assert.Equal(15000, price);
    }
}
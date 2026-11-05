using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class ItemFootprintServiceTests
{
    [Fact]
    public void CalculateFootprint_ShouldReturnOneForOneByOneItem()
    {
        var service = new ItemFootprintService(
            new TestDimensionsProvider(1, 1));

        var item = new Item
        {
            Id = new SPTarkov.Server.Core.Models.Common.MongoId(
         "64b000000000000000000001")
        };

        Assert.Equal(1, service.CalculateFootprint(item));
    }

    [Fact]
    public void CalculateFootprint_ShouldReturnSixForTwoByThreeItem()
    {
        var service = new ItemFootprintService(
            new TestDimensionsProvider(2, 3));

        var item = new Item
        {
            Id = new SPTarkov.Server.Core.Models.Common.MongoId(
        "64b000000000000000000001")
        };

        Assert.Equal(6, service.CalculateFootprint(item));
    }

    [Fact]
    public void CalculateFootprint_ShouldReturnTwelveForThreeByFourItem()
    {
        var service = new ItemFootprintService(
            new TestDimensionsProvider(3, 4));

        var item = new Item
        {
            Id = new SPTarkov.Server.Core.Models.Common.MongoId(
        "64b000000000000000000001")
        };

        Assert.Equal(12, service.CalculateFootprint(item));
    }

    [Fact]
    public void CalculateFootprint_ShouldRejectMissingDimensions()
    {
        var service = new ItemFootprintService(
            new TestDimensionsProvider(null, 3));

        var item = new Item
        {
            Id = new SPTarkov.Server.Core.Models.Common.MongoId(
        "64b000000000000000000001")
        };

        Assert.Throws<InvalidOperationException>(
            () => service.CalculateFootprint(item));
    }

    [Fact]
    public void CalculateFootprint_ShouldRejectZeroDimensions()
    {
        var service = new ItemFootprintService(
            new TestDimensionsProvider(0, 3));

        var item = new Item
        {
            Id = new SPTarkov.Server.Core.Models.Common.MongoId(
        "64b000000000000000000001")
        };

        Assert.Throws<InvalidOperationException>(
            () => service.CalculateFootprint(item));
    }

    [Fact]
    public void CalculateFootprint_ShouldRejectNullItem()
    {
        var service = new ItemFootprintService(
            new TestDimensionsProvider(2, 3));

        Assert.Throws<ArgumentNullException>(
            () => service.CalculateFootprint(null!));
    }

    private class TestDimensionsProvider(
        int? width,
        int? height) : IItemDimensionsProvider
    {
        public (int? Width, int? Height) GetDimensions(Item item)
        {
            return (width, height);
        }
    }
}
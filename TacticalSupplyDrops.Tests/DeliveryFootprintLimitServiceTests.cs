using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class DeliveryFootprintLimitServiceTests
{
    [Fact]
    public void IsWithinLimit_ShouldAcceptExactlyTwelveCells()
    {
        var service = CreateService();

        var items = new List<Item>
        {
            CreateItem("64b000000000000000000001"),
            CreateItem("64b000000000000000000002"),
            CreateItem("64b000000000000000000003")
        };

        Assert.True(service.IsWithinLimit(items));
        Assert.Equal(12, service.CalculateTotalFootprint(items));
    }

    [Fact]
    public void IsWithinLimit_ShouldRejectMoreThanTwelveCells()
    {
        var service = CreateService();

        var items = new List<Item>
        {
            CreateItem("64b000000000000000000004"),
            CreateItem("64b000000000000000000005")
        };

        Assert.False(service.IsWithinLimit(items));
        Assert.Equal(15, service.CalculateTotalFootprint(items));
    }

    [Fact]
    public void CalculateTotalFootprint_ShouldAddMultipleItems()
    {
        var service = CreateService();

        var items = new List<Item>
        {
            CreateItem("64b000000000000000000006"),
            CreateItem("64b000000000000000000007"),
            CreateItem("64b000000000000000000008")
        };

        Assert.Equal(7, service.CalculateTotalFootprint(items));
    }

    [Fact]
    public void CalculateTotalFootprint_ShouldReturnZeroForEmptyList()
    {
        var service = CreateService();

        Assert.Equal(
            0,
            service.CalculateTotalFootprint(new List<Item>()));
    }

    private static DeliveryFootprintLimitService CreateService()
    {
        var dimensionsProvider = new TestDimensionsProvider();

        var footprintService = new ItemFootprintService(
            dimensionsProvider);

        return new DeliveryFootprintLimitService(footprintService);
    }

    private static Item CreateItem(string id)
    {
        return new Item
        {
            Id = new MongoId(id)
        };
    }

    private class TestDimensionsProvider : IItemDimensionsProvider
    {
        public (int? Width, int? Height) GetDimensions(Item item)
        {
            string id = (string)item.Id;

            return id switch
            {
                "64b000000000000000000001" => (2, 3),
                "64b000000000000000000002" => (2, 2),
                "64b000000000000000000003" => (2, 1),

                "64b000000000000000000004" => (3, 3),
                "64b000000000000000000005" => (2, 3),

                "64b000000000000000000006" => (2, 2),
                "64b000000000000000000007" => (2, 1),
                "64b000000000000000000008" => (1, 1),

                _ => (null, null)
            };
        }
    }
}
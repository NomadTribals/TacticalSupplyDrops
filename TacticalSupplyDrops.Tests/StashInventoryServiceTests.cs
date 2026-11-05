
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class StashInventoryServiceTests
{
    private readonly StashInventoryService _service = new();

    [Fact]
    public void GetStashItems_ShouldFindItemsDirectlyInsideStash()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var stashItem = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            ParentId = (string)stashId
        };

        var profile = CreateProfile(stashId, new List<Item>
        {
            stashItem
        });

        var result = _service.GetStashItems(profile);

        Assert.Single(result);
        Assert.Equal(stashItem.Id, result[0].Id);
    }

    [Fact]
    public void GetStashItems_ShouldIncludeItemsInsideContainers()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            ParentId = (string)stashId
        };

        var storedItem = new Item
        {
            Id = new MongoId("64b000000000000000000003"),
            ParentId = (string)backpack.Id
        };

        var profile = CreateProfile(stashId, new List<Item>
        {
            backpack,
            storedItem
        });

        var result = _service.GetStashItems(profile);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.Id == backpack.Id);
        Assert.Contains(result, item => item.Id == storedItem.Id);
    }

    [Fact]
    public void GetStashItems_ShouldExcludeItemsOutsideStash()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var stashItem = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            ParentId = (string)stashId
        };

        var equippedItem = new Item
        {
            Id = new MongoId("64b000000000000000000003"),
            ParentId = "equipment"
        };

        var profile = CreateProfile(stashId, new List<Item>
        {
            stashItem,
            equippedItem
        });

        var result = _service.GetStashItems(profile);

        Assert.Single(result);
        Assert.Equal(stashItem.Id, result[0].Id);
    }

    private static PmcData CreateProfile(
        MongoId stashId,
        List<Item> items)
    {
        return new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = items
            }
        };
    }

    [Fact]
    public void GetStashItems_WithEmptyStash_ShouldReturnEmptyList()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var profile = CreateProfile(stashId, new List<Item>());

        var result = _service.GetStashItems(profile);

        Assert.Empty(result);
    }

    [Fact]
    public void GetStashItems_ShouldIncludeDeeplyNestedItems()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            ParentId = (string)stashId
        };

        var caseItem = new Item
        {
            Id = new MongoId("64b000000000000000000003"),
            ParentId = (string)backpack.Id
        };

        var storedItem = new Item
        {
            Id = new MongoId("64b000000000000000000004"),
            ParentId = (string)caseItem.Id
        };

        var profile = CreateProfile(stashId, new List<Item>
        {
            backpack,
            caseItem,
            storedItem
        });

        var result = _service.GetStashItems(profile);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, item => item.Id == backpack.Id);
        Assert.Contains(result, item => item.Id == caseItem.Id);
        Assert.Contains(result, item => item.Id == storedItem.Id);
    }
}

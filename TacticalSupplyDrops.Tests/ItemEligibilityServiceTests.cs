using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class ItemEligibilityServiceTests
{
    private readonly ItemEligibilityService _service =
        new(new StashInventoryService());
    [Fact]
    public void IsEligible_ShouldRejectNullItem()
    {
        var stashId = new MongoId("64b000000000000000000001");
        var profile = CreateProfile(stashId);

        Assert.False(_service.IsEligible(null!, profile));
    }
    [Fact]
    public void IsEligible_ShouldRejectProfileWithoutStash()
    {
        var item = new Item
        {
            Id = new MongoId("64b000000000000000000012"),
            Template = new MongoId("64b000000000000000000003")
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Items = new List<Item> { item }
            }
        };

        Assert.False(_service.IsEligible(item, profile));
    }
    [Fact]
    public void IsEligible_ShouldAllowQuestRelatedItems()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var questItem = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = (string)stashId
        };

        var profile = CreateProfile(stashId);
        profile.Inventory.Items.Add(questItem);

        Assert.True(_service.IsEligible(questItem, profile));
    }

    [Fact]
    public void IsEligible_ShouldRejectStashContainer()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var stashContainer = new Item
        {
            Id = stashId
        };

        var profile = CreateProfile(stashId);

        Assert.False(_service.IsEligible(stashContainer, profile));
    }

    [Fact]
    public void IsEligible_ShouldRejectItemsOutsideStash()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var equippedItem = new Item
        {
            Id = new MongoId("64b000000000000000000004"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = "equipment"
        };

        var profile = CreateProfile(stashId);
        profile.Inventory.Items.Add(equippedItem);

        Assert.False(_service.IsEligible(equippedItem, profile));
    }

    [Fact]
    public void IsEligible_ShouldRejectItemsInsideEquippedBackpack()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = new Item
        {
            Id = new MongoId("64b000000000000000000005"),
            ParentId = "equipment"
        };

        var backpackItem = new Item
        {
            Id = new MongoId("64b000000000000000000006"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = (string)backpack.Id
        };

        var profile = CreateProfile(stashId);
        profile.Inventory.Items.Add(backpack);
        profile.Inventory.Items.Add(backpackItem);

        Assert.False(_service.IsEligible(backpackItem, profile));
    }

    [Fact]
    public void IsEligible_ShouldAllowItemsInsideStashBackpack()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = new Item
        {
            Id = new MongoId("64b000000000000000000007"),
            ParentId = (string)stashId
        };

        var backpackItem = new Item
        {
            Id = new MongoId("64b000000000000000000008"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = (string)backpack.Id
        };

        var profile = CreateProfile(stashId);
        profile.Inventory.Items.Add(backpack);
        profile.Inventory.Items.Add(backpackItem);

        Assert.True(_service.IsEligible(backpackItem, profile));
    }

    [Fact]
    public void IsEligible_ShouldRejectItemsWithMissingParent()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var orphanedItem = new Item
        {
            Id = new MongoId("64b000000000000000000009"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = "64b000000000000000000099"
        };

        var profile = CreateProfile(stashId);
        profile.Inventory.Items.Add(orphanedItem);

        Assert.False(_service.IsEligible(orphanedItem, profile));
    }
    [Fact]
    public void IsEligible_ShouldRejectItemsInDisconnectedContainers()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var disconnectedBackpack = new Item
        {
            Id = new MongoId("64b000000000000000000010"),
            ParentId = "64b000000000000000000099"
        };

        var itemInsideBackpack = new Item
        {
            Id = new MongoId("64b000000000000000000011"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = (string)disconnectedBackpack.Id
        };

        var profile = CreateProfile(stashId);
        profile.Inventory.Items.Add(disconnectedBackpack);
        profile.Inventory.Items.Add(itemInsideBackpack);

        Assert.False(_service.IsEligible(itemInsideBackpack, profile));
    }

    private static PmcData CreateProfile(MongoId stashId)
    {
        return new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item>()
            }
        };
    }
}
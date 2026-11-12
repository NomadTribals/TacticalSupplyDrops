using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
using TacticalSupplyDrops.Server.Models;
using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Tests;

public class CombinedCargoTests
{
    private sealed class CopyItems : ICloner
    {
        public T? Clone<T>(T? value)
        {
            if (value is Item item)
                return (T)(object)new Item { Id = item.Id, Template = item.Template,
                    ParentId = item.ParentId, Upd = new Upd { StackObjectsCount = item.Upd?.StackObjectsCount } };
            throw new InvalidOperationException();
        }
    }

    private static SupplyOrder Order(int count)
    {
        var item = new Item { Id = new MongoId("64b000000000000000000002"),
            Template = new MongoId("64b000000000000000000003"), Upd = new Upd { StackObjectsCount = count } };
        return new SupplyOrder { TotalPrice = count, TotalFootprint = 1,
            Items = new() { new SupplyOrderItem(item.Id.ToString(), count) { Items = new() { item } } } };
    }

    [Fact]
    public void RepeatedOrdersFromSameStackPreserveBothReservations()
    {
        var first = Order(60); var second = Order(59);
        var combined = new SupplyDropCargoBuilder(null!, new CopyItems()).CombineOrders(new[] { first, second });
        var items = combined.Items.SelectMany(entry => entry.Items).ToArray();
        Assert.Equal(2, items.Length);
        Assert.NotEqual(items[0].Id, items[1].Id);
        Assert.Equal(119, items.Sum(item => item.Upd!.StackObjectsCount));
        Assert.Equal(first.Items[0].ItemId, first.Items[0].Items[0].Id.ToString());
        Assert.Equal(60, first.Items[0].Items[0].Upd!.StackObjectsCount);
        Assert.Equal(119, combined.TotalPrice);
    }

    [Fact]
    public void MixedItemOrdersShareTheShipment()
    {
        var ammo = Order(200); var camera = Order(1);
        camera.Items[0].Items[0].Template = new MongoId("64b000000000000000000099");
        var combined = new SupplyDropCargoBuilder(null!, new CopyItems()).CombineOrders(new[] { ammo, camera });
        var items = combined.Items.SelectMany(entry => entry.Items).ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(200, items[0].Upd!.StackObjectsCount);
        Assert.Equal(camera.Items[0].Items[0].Template, items[1].Template);
        Assert.Equal(1, items[1].Upd!.StackObjectsCount);
    }

    [Fact]
    public void NestedCargoRetainsParentRelationshipsInEachOrder()
    {
        var first = Order(1); var second = Order(1);
        foreach (var order in new[] { first, second })
            order.Items[0].Items.Add(new Item { Id = new MongoId("64b000000000000000000004"),
                Template = new MongoId("64b000000000000000000005"), ParentId = order.Items[0].ItemId });
        var combined = new SupplyDropCargoBuilder(null!, new CopyItems()).CombineOrders(new[] { first, second });
        var items = combined.Items.SelectMany(entry => entry.Items).ToArray();
        Assert.Equal(4, items.Select(item => item.Id).Distinct().Count());
        Assert.Equal(items[0].Id.ToString(), items[1].ParentId);
        Assert.Equal(items[2].Id.ToString(), items[3].ParentId);
    }
}

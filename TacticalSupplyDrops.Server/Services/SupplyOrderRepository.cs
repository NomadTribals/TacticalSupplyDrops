
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Modding;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

public interface IProfileDataStore
{
    Task<T?> GetAsync<T>(
        MongoId profileId,
        string modKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync<T>(
        MongoId profileId,
        string modKey,
        T data,
        CancellationToken cancellationToken = default);
}

[Injectable]
public class SptProfileDataStore(
    ProfileDataService profileDataService) : IProfileDataStore
{
    public Task<T?> GetAsync<T>(
        MongoId profileId,
        string modKey,
        CancellationToken cancellationToken = default)
    {
        return profileDataService.GetProfileDataAsync<T>(
            profileId,
            modKey,
            cancellationToken);
    }

    public Task SaveAsync<T>(
        MongoId profileId,
        string modKey,
        T data,
        CancellationToken cancellationToken = default)
    {
        return profileDataService.SaveProfileDataAsync(
            profileId,
            modKey,
            data,
            cancellationToken);
    }
}

[Injectable]
public class SupplyOrderRepository(IProfileDataStore profileDataStore)
{
    private const string ModKey = "tactical_supply_drops";

    public async Task<List<SupplyOrder>> GetOrdersAsync(
        MongoId profileId,
        CancellationToken cancellationToken = default)
    {
        var orders = await profileDataStore.GetAsync<List<SupplyOrder>>(
            profileId,
            ModKey,
            cancellationToken);

        return orders ?? new List<SupplyOrder>();
    }

    public async Task SaveOrdersAsync(
        MongoId profileId,
        IEnumerable<SupplyOrder> orders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orders);

        var orderList = orders.ToList();

        await profileDataStore.SaveAsync(
            profileId,
            ModKey,
            orderList,
            cancellationToken);
    }

    public async Task AddOrderAsync(
        MongoId profileId,
        SupplyOrder order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var orders = await GetOrdersAsync(profileId, cancellationToken);

        orders.Add(order);

        await SaveOrdersAsync(profileId, orders, cancellationToken);
    }

    public async Task<SupplyOrder?> GetOrderAsync(
        MongoId profileId,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var orders = await GetOrdersAsync(profileId, cancellationToken);

        return orders.FirstOrDefault(order => order.OrderId == orderId);
    }

    public async Task<SupplyOrder?> GetOrderByRequestIdAsync(
        MongoId profileId,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var orders = await GetOrdersAsync(profileId, cancellationToken);

        return orders.FirstOrDefault(order =>
            order.RequestId == requestId);
    }

    public async Task<bool> UpdateOrderAsync(
        MongoId profileId,
        SupplyOrder updatedOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updatedOrder);

        var orders = await GetOrdersAsync(profileId, cancellationToken);

        int index = orders.FindIndex(
            order => order.OrderId == updatedOrder.OrderId);

        if (index < 0)
        {
            return false;
        }

        orders[index] = updatedOrder;

        await SaveOrdersAsync(profileId, orders, cancellationToken);

        return true;
    }
}

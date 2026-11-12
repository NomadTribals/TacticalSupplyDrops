using Newtonsoft.Json.Linq;
using TacticalSupplyDrops.Client;

namespace TacticalSupplyDrops.Tests;

public sealed class PendingCargoTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "TsdCargoTests-" + Guid.NewGuid());
    public PendingCargoTests() { Directory.CreateDirectory(folder); }
    public void Dispose() { Directory.Delete(folder, true); }
    private JObject Read(string name) => JObject.Parse(File.ReadAllText(Path.Combine(folder, name)));
    private Task<string> Cargo(string request)
    {
        var ids = JObject.Parse(request)["OrderIds"]!.DeepClone();
        return Task.FromResult(new JObject { ["container"] = new JArray(new JObject { ["_id"] = "crate" }),
            ["orderIds"] = ids, ["order"] = new JObject() }.ToString());
    }

    [Fact]
    public async Task MultipleConfirmationsAndRetriesAccumulateWithoutDuplicates()
    {
        await TsdPendingCargo.Refresh(folder, "first", Cargo);
        await TsdPendingCargo.Refresh(folder, "second", Cargo);
        await TsdPendingCargo.Refresh(folder, "second", Cargo);
        Assert.Equal(new[] { "first", "second" }, TsdPendingCargo.OrderIds(Read("cargo.json")));
    }

    [Fact]
    public async Task FailedPreparationKeepsPaidOrderForRetry()
    {
        await TsdPendingCargo.Refresh(folder, "first", Cargo);
        await Assert.ThrowsAsync<IOException>(() => TsdPendingCargo.Refresh(folder, "second", _ => throw new IOException()));
        Assert.Equal(new[] { "first", "second" }, TsdPendingCargo.OrderIds(Read("pending-orders.json")));
        Assert.Equal(new[] { "first" }, TsdPendingCargo.OrderIds(Read("cargo.json")));
        await TsdPendingCargo.Refresh(folder, null!, Cargo);
        Assert.Equal(new[] { "first", "second" }, TsdPendingCargo.OrderIds(Read("cargo.json")));
    }

    [Fact]
    public async Task DeferredOrdersRemainAfterFirstShipmentDispatches()
    {
        await TsdPendingCargo.Refresh(folder, "first", Cargo);
        await TsdPendingCargo.Refresh(folder, "second", _ => Task.FromResult(new JObject {
            ["container"] = new JArray(new JObject()), ["orderIds"] = new JArray("first"), ["order"] = new JObject() }.ToString()));
        Assert.Equal(new[] { "first" }, TsdPendingCargo.OrderIds(Read("cargo.json")));
        TsdPendingCargo.WriteAtomic(Path.Combine(folder, "dispatched-orders.json"), new JObject { ["orderIds"] = new JArray("first") });
        await TsdPendingCargo.Refresh(folder, null!, Cargo);
        Assert.Equal(new[] { "second" }, TsdPendingCargo.OrderIds(Read("cargo.json")));
    }

    [Fact]
    public async Task LegacyManifestIsPreservedWhenAddingOrder()
    {
        TsdPendingCargo.WriteAtomic(Path.Combine(folder, "cargo.json"), new JObject { ["orderId"] = "legacy" });
        await TsdPendingCargo.Refresh(folder, "second", Cargo);
        Assert.Equal(new[] { "legacy", "second" }, TsdPendingCargo.OrderIds(Read("cargo.json")));
    }
}

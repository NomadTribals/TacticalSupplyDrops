
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using SPT.Common.Http;

namespace TacticalSupplyDrops.Client
{
    public static class SupplyDropOrderBridge
    {
        private const string OrderEndpoint =
            "/client/game/profile/items/moving";

        private const string OrderLookupEndpoint =
            "/client/tactical-supply-drops/get-order";

        private const string CargoEndpoint =
            "/client/tactical-supply-drops/get-cargo";

        private static readonly string CargoFilePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "cargo.json");

        internal static readonly SemaphoreSlim CargoFileLock =
            new SemaphoreSlim(1, 1);

        public static async Task<string> SubmitOrderAsync(
            string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                throw new ArgumentException(
                    "The order request cannot be empty.",
                    nameof(requestJson));
            }

            return await RequestHandler.PostJsonAsync(
                OrderEndpoint,
                requestJson);
        }
        internal static async Task ConvertToCourierAsync(string[] ids, int additionalFee)
        {
            string response = await SubmitOrderAsync(JsonConvert.SerializeObject(new { data = new[] { new {
                Action = "TacticalSupplyDropsConvertToCourier", OrderIds = ids, ExpectedAdditionalFee = additionalFee
            } } }));
            var reply = JObject.Parse(response);
            var data = reply["data"] ?? reply;
            var warnings = data["warnings"] as JArray;
            if ((reply["err"]?.Value<int>() ?? 0) != 0 || warnings?.Count > 0)
            {
                string reason = (string)warnings?[0]?["errmsg"] ?? (string)warnings?[0]?["errorMessage"] ?? (string)warnings?[0]?["ErrorMessage"] ?? (string)reply["errmsg"];
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason) ? "Courier conversion was not accepted. Your cargo remains reserved." : reason);
            }
            ProcurementProfileRefresh.QueueResponse(response);
            string cargoResponse = await RequestHandler.PostJsonAsync(CargoEndpoint, JsonConvert.SerializeObject(new { OrderId = ids[ids.Length-1], OrderIds = ids }));
            var cargo = JObject.Parse(cargoResponse);
            var included = cargo["orderIds"]?.Values<string>().ToArray();
            if (!(cargo["container"] is JArray items) || items.Count == 0 || included == null || !included.SequenceEqual(ids))
                throw new InvalidOperationException("Courier conversion was saved, but the new manifest could not refresh. Refresh the tablet before retrying; the payment will not repeat.");
            string folder = Path.Combine(BepInEx.Paths.ConfigPath,"TacticalSupplyDrops");
            TsdPendingCargo.WriteAtomic(Path.Combine(folder,"cargo.json"),new JObject {
                ["orderId"] = ids[ids.Length-1], ["orderIds"] = new JArray(ids), ["cargo"] = cargo });
        }

        public static async Task<string> PurchaseCartridgesAsync(
      string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                throw new ArgumentException(
                    "Procurement purchase request JSON cannot be empty.",
                    nameof(requestJson));
            }

            return await RequestHandler.PostJsonAsync(
                OrderEndpoint,
                requestJson);
        }

        public static async Task<string> LookupOrderAsync(
            string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                throw new ArgumentException(
                    "The order lookup request cannot be empty.",
                    nameof(requestJson));
            }

            // Step 1: Retrieve the accepted order using its RequestId.
            string lookupResponse =
                await RequestHandler.PostJsonAsync(
                    OrderLookupEndpoint,
                    requestJson);

            JObject lookupData =
                JObject.Parse(lookupResponse);

            bool success =
                lookupData["success"]?.Value<bool>() ?? false;

            if (!success)
            {
                throw new InvalidOperationException(
                    "The server could not find the accepted supply order.");
            }

            JObject order =
                lookupData["order"] as JObject;

            if (order == null)
            {
                throw new InvalidOperationException(
                    "The order lookup response did not contain an order.");
            }

            string orderId =
                order["OrderId"]?.Value<string>() ??
                order["orderId"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new InvalidOperationException(
                    "The accepted order did not contain an OrderId.");
            }

            await CargoFileLock.WaitAsync();
            try
            {
                var shipment = await TsdPendingCargo.Refresh(Path.GetDirectoryName(CargoFilePath), orderId,
                    json => RequestHandler.PostJsonAsync(CargoEndpoint, json));
                if (shipment != null) lookupData["order"] = shipment;
            }
            finally { CargoFileLock.Release(); }

            // Return the original lookup response so the existing
            // plugin response-saving behavior remains unchanged.
            return lookupData.ToString(Formatting.None);
        }

        internal static async Task RefreshPendingCargoAsync()
        {
            await CargoFileLock.WaitAsync();
            try
            {
                await TsdPendingCargo.Refresh(Path.GetDirectoryName(CargoFilePath), null,
                    json => RequestHandler.PostJsonAsync(CargoEndpoint, json));
            }
            finally { CargoFileLock.Release(); }
        }

        public static async Task<string> GetCargoAsync(
            string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                throw new ArgumentException(
                    "The cargo request cannot be empty.",
                    nameof(requestJson));
            }

            return await RequestHandler.PostJsonAsync(
                CargoEndpoint,
                requestJson);
        }
    }
}

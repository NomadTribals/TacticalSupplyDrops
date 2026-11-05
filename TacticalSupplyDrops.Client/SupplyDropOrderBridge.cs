
using System;
using System.IO;
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

        private const string CargoFilePath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\cargo.json";

        private static readonly SemaphoreSlim CargoFileLock =
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

            // Step 2: Request the actual cargo using the OrderId.
            string cargoRequest =
                JsonConvert.SerializeObject(new
                {
                    OrderId = orderId
                });

            string cargoResponse =
                await RequestHandler.PostJsonAsync(
                    CargoEndpoint,
                    cargoRequest);

            // Step 3: Verify the server returned valid crate data.
            JObject cargoData =
                JObject.Parse(cargoResponse);

            JToken container =
                cargoData["container"];

            if (container == null ||
                container.Type != JTokenType.Array ||
                !container.HasValues)
            {
                throw new InvalidOperationException(
                    "The cargo response did not contain a valid container.");
            }

            string icon =
                cargoData["icon"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(icon))
            {
                throw new InvalidOperationException(
                    "The cargo response did not contain an airdrop icon.");
            }

            // Step 4: Wrap the cargo with its associated OrderId.
            JObject cargoFileData = new JObject
            {
                ["orderId"] = orderId,
                ["cargo"] = cargoData
            };

            string directory =
                Path.GetDirectoryName(CargoFilePath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    "The cargo storage directory is invalid.");
            }

            Directory.CreateDirectory(directory);

            string temporaryPath = CargoFilePath + ".tmp";

            await CargoFileLock.WaitAsync();

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    cargoFileData.ToString(Formatting.None));

                // Replace the existing file atomically where possible.
                if (File.Exists(CargoFilePath))
                {
                    File.Replace(
                        temporaryPath,
                        CargoFilePath,
                        null);
                }
                else
                {
                    File.Move(
                        temporaryPath,
                        CargoFilePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                CargoFileLock.Release();
            }

            // Return the original lookup response so the existing
            // plugin response-saving behavior remains unchanged.
            return lookupResponse;
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

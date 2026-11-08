using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonType;

namespace TacticalSupplyDrops.Client
{
    internal static class TsdCargoManifest
    {
        internal static AirdropContainerData Parse(string json, string expectedOrderId)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("The cargo manifest is empty.");
            var envelope = JObject.Parse(json);
            var orderId = envelope["orderId"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(expectedOrderId) || !string.Equals(orderId, expectedOrderId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The cargo manifest does not match this order.");
            var cargo = envelope["cargo"] as JObject;
            if (cargo == null) throw new InvalidOperationException("The cargo manifest has no shipment contents.");
            var data = JsonConvert.DeserializeObject<AirdropContainerData>(cargo.ToString(Formatting.None));
            if (data?.data == null || data.data.Length == 0 || data.data.Any(item => item == null))
                throw new InvalidOperationException("The cargo manifest has no valid items.");
            if (data.data.Select(item => item._id.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.data.Length)
                throw new InvalidOperationException("The cargo manifest contains duplicate item identifiers.");
            if (string.IsNullOrWhiteSpace(data.Icon)) throw new InvalidOperationException("The cargo manifest has no crate type.");
            return data;
        }
    }
}

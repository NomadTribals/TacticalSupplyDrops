using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TacticalSupplyDrops.Client
{
    internal static class TsdPendingCargo
    {
        internal static string[] OrderIds(JObject envelope)
        {
            var ids = envelope["orderIds"]?.Values<string>().Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
            if (ids != null && ids.Length > 0) return ids.Distinct().ToArray();
            string id = (string)envelope["orderId"];
            return string.IsNullOrWhiteSpace(id) ? Array.Empty<string>() : new[] { id };
        }

        internal static void WriteAtomic(string path, JObject value)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, value.ToString(Formatting.None));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        // The caller holds the shared manifest/dispatch lock throughout this operation.
        internal static async Task<JObject> Refresh(string folder, string acceptedId, Func<string, Task<string>> getCargo)
        {
            Directory.CreateDirectory(folder);
            string pendingPath = Path.Combine(folder, "pending-orders.json");
            string cargoPath = Path.Combine(folder, "cargo.json");
            string historyPath = Path.Combine(folder, "dispatched-orders.json");
            var history = File.Exists(historyPath) ? JObject.Parse(File.ReadAllText(historyPath)) : new JObject();
            var dispatched = new HashSet<string>(history["orderIds"]?.Values<string>() ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var queue = new List<string>();
            if (File.Exists(pendingPath)) queue.AddRange(OrderIds(JObject.Parse(File.ReadAllText(pendingPath))));
            if (File.Exists(cargoPath)) queue.AddRange(OrderIds(JObject.Parse(File.ReadAllText(cargoPath))));
            if (!string.IsNullOrWhiteSpace(acceptedId)) queue.Add(acceptedId);
            queue = queue.Where(id => !dispatched.Contains(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            // Persist accepted IDs before any network or packing operation can fail.
            WriteAtomic(pendingPath, new JObject { ["orderIds"] = new JArray(queue) });
            if (queue.Count == 0) return null;
            var cargo = JObject.Parse(await getCargo(JsonConvert.SerializeObject(new { OrderId = queue.Last(), OrderIds = queue })));
            if (!(cargo["container"] is JArray items) || items.Count == 0)
                throw new InvalidOperationException((string)cargo["message"] ?? "Pending cargo could not be prepared. Its orders remain saved.");
            var included = cargo["orderIds"]?.Values<string>().ToArray();
            if (included == null || included.Length == 0 || included.Any(id => !queue.Contains(id)))
                throw new InvalidOperationException("The shipment order list could not be verified.");
            var envelope = new JObject
            {
                ["orderId"] = included.Last(),
                ["orderIds"] = new JArray(included),
                ["cargo"] = cargo
            };
            WriteAtomic(cargoPath, envelope);
            return cargo["order"] as JObject;
        }
    }
}

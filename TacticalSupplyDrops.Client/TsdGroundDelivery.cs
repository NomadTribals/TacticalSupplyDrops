using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.Airdrop;
using EFT.CameraControl;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.SynchronizableObjects;
using Newtonsoft.Json;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    internal sealed class TsdGroundDelivery
    {
        private sealed class Shipment
        {
            internal string OrderId;
            internal ClientGameWorld World;
            internal AirdropSynchronizableObject Crate;
            internal CompoundItem Inventory;
            internal TsdCargoBeacon Beacon;
            internal bool Collected;
        }

        private readonly ManualLogSource logger;
        private readonly ConfigEntry<bool> enabled;
        private readonly ConfigEntry<float> delay;
        private readonly ConfigEntry<bool> beaconSound;
        private readonly ConfigEntry<float> beaconVolume;
        private readonly List<Shipment> shipments = new List<Shipment>();
        private string phase;
        private string message;
        private string orderId;
        private ClientGameWorld statusWorld;
        private float nextUpdate;
        private string lastJson;
        private readonly string locatorPath = Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "ground-cargo-locator.json");

        internal float DelaySeconds => delay.Value;

        internal TsdGroundDelivery(ConfigFile config, ManualLogSource log)
        {
            logger = log;
            enabled = config.Bind("Ground Courier", "Enabled", true, "Use ground delivery on supported locations when no usable airdrop points are available.");
            delay = config.Bind("Ground Courier", "DelaySeconds", 15f,
                new ConfigDescription("Courier handoff delay. Development default; adjust after testing.", new AcceptableValueRange<float>(5f, 180f)));
            beaconSound = config.Bind("Ground Courier", "BeaconSound", true, "Play the courier crate's positional locator chirp.");
            beaconVolume = config.Bind("Ground Courier", "BeaconVolume", .12f,
                new ConfigDescription("Courier locator chirp volume (0 = silent, 1 = full volume).", new AcceptableValueRange<float>(0f, 1f)));
            Publish(new { available = false });
            _ = PrepareVisuals();
        }

        private async Task PrepareVisuals()
        {
            try
            {
                await TsdCrateBranding.Preload();
                logger.LogInfo("[TSD] Courier visual startup check passed: gold badges and beacon assets loaded.");
            }
            catch (Exception ex) { logger.LogWarning("[TSD] Courier visual startup check failed: " + ex.Message); }
        }
        internal bool CanServe(ClientGameWorld world)
        {
            if (!enabled.Value || world == null || world.GetType().FullName != "ClientLocalGameWorld") return false;
            string location = world.LocationId?.ToLowerInvariant();
            return location == "factory4_day" || location == "factory4_night" || location == "laboratory" || location == "sandbox" || location == "sandbox_high";
        }

        internal void SetStatus(ClientGameWorld world, string id, string newPhase, string text)
        {
            statusWorld = world;
            orderId = id;
            phase = newPhase;
            message = text;
            nextUpdate = 0f;
        }

        private static void CheckRaid(ClientGameWorld world, Func<bool> access)
        {
            if (world == null || !Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance != world || world.MainPlayer == null || !access())
                throw new InvalidOperationException("Field terminal link lost. The order has not been dispatched.");
        }

        internal async Task<Vector3> Deliver(ClientGameWorld world, string id, string manifestSnapshot, Func<bool> access)
        {
            CheckRaid(world, access);
            if (shipments.Any(s => s.World == world && s.OrderId == id))
                throw new InvalidOperationException("This order already has a courier shipment.");
            var data = TsdCargoManifest.Parse(manifestSnapshot, id);
            var tree = Singleton<ItemFactory>.Instance.FlatItemsToTree(data.data);
            var root = tree.Items[data.data[0]._id] as CompoundItem;
            if (root?.Grids == null || root.Grids.Length == 0 || root.IsEmpty)
                throw new InvalidOperationException("The accepted order has no deliverable cargo.");
            if (!Enum.TryParse(data.Icon, true, out EAirdropType kind))
                throw new InvalidOperationException("The accepted order has an unsupported crate type.");
            var resources = tree.Items.Values.SelectMany(item => item.Template.AllResources).ToArray();
            await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,
                ObjectsFactory.AssemblyType.Online, resources, JobYieldPriority.Low);
            CheckRaid(world, access);
            try
            {
                await TsdCrateBranding.Preload();
                logger.LogInfo("[TSD] Courier badge and beacon assets preloaded successfully.");
            }
            catch (Exception ex) { logger.LogWarning("Courier visuals could not preload; cargo locator and fallback beeper remain available: " + ex.Message); }
            CheckRaid(world, access);
            var processor = world.ClientSynchronizableObjectLogicProcessor;
            if (processor == null) throw new InvalidOperationException("The ground courier is not ready.");
            AirdropSynchronizableObject crate = null;
            LootableContainer container = null;
            try
            {
                crate = processor.TakeFromPool(SynchronizableObjectType.AirDrop) as AirdropSynchronizableObject;
                if (crate?.CollisionCollider == null) throw new InvalidOperationException("The courier crate is unavailable.");
                var body = crate.GetComponent<Rigidbody>();
                if (body != null) { body.useGravity = false; body.isKinematic = true; }
                crate.transform.rotation = Quaternion.Euler(0, world.MainPlayer.Transform.Original.eulerAngles.y, 0);
                var selectedPosition = await TsdGroundPlacement.FindCratePosition(crate, world.MainPlayer,
                    text => logger.LogInfo("[Ground placement] " + text), access);
                if (!selectedPosition.HasValue)
                    throw new InvalidOperationException("No secure, reachable handoff spot found. Your cargo is retained. Move, then select RETRY CARGO DELIVERY on Delivery. No new order or fee is needed.");
                Vector3 position = selectedPosition.Value;
                CheckRaid(world, access);
                crate.transform.position = position;
                crate.ObjectId = processor.TakeSynchronizedObjectId;
                crate.UniqueId = crate.ObjectId;
                crate.AirdropType = kind;
                crate.ApplyAirdropViewType(AirdropSynchronizableObject.EAirdropViewType.Default);
                processor.InitStaticObject(crate);
                if (crate.AirdropFlare != null) crate.AirdropFlare.SetActive(false);
                if (crate.AirdropDust != null) crate.AirdropDust.SetActive(false);
                if (crate.Parachute != null) crate.Parachute.SetActive(false);
                container = crate.GetComponentInChildren<LootableContainer>(true);
                if (container == null) throw new InvalidOperationException("The courier crate cannot be opened.");
                container.Id = root.Id;
                container.enabled = true;
                container.gameObject.SetActive(true);
                foreach (var collider in container.GetComponentsInChildren<Collider>(true))
                    if (!collider.isTrigger) collider.enabled = true;
                LootItem.CreateLootContainer(container, root, "TSD Cargo Crate", world);
                try { TsdCrateBranding.Attach(crate, text => logger.LogInfo("[Cargo branding] " + text)); }
                catch (Exception ex) { logger.LogWarning("Courier cargo remains available; TSD branding unavailable: " + ex.Message); }
                var shipment = new Shipment { OrderId = id, World = world, Crate = crate, Inventory = root };
                try { shipment.Beacon = TsdCargoBeacon.Attach(crate, () => beaconSound.Value, () => beaconVolume.Value,
                    text => logger.LogInfo("[Cargo beacon] " + text)); }
                catch (Exception ex) { logger.LogWarning("Courier crate placed; locator beacon unavailable: " + ex.Message); }
                shipments.Add(shipment);
                TsdDynamicMaps.Register(crate, logger);
                SetStatus(world, id, "ready", "Courier cargo is ready for collection.");
                logger.LogInfo("[TSD] Ground shipment placed for order " + id + " at " + position);
                return position;
            }
            catch
            {
                TsdDynamicMaps.Remove(crate);
                shipments.RemoveAll(shipment => shipment.Crate == crate);
                if (container != null)
                {
                    if (container.ItemOwner != null) world.ItemOwners.Remove(container.ItemOwner);
                    world.LootList.Remove(container);
                    container.ItemOwner = null;
                    container.IsInitialized = false;
                }
                if (crate != null)
                {
                    processor._staticSynchronizableObjects.Remove(crate);
                    processor.ReturnToPool(crate);
                }
                throw;
            }
        }

        internal void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .5f;
            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance as ClientGameWorld : null;
            foreach (var shipment in shipments)
            {
                if (shipment.World != world || shipment.World == null || shipment.Crate == null || !shipment.Crate.IsActive)
                { if (shipment.Beacon != null) shipment.Beacon.StopBeacon(); TsdDynamicMaps.Remove(shipment.Crate); continue; }
                if (!shipment.Collected && shipment.Inventory?.Grids != null && shipment.Inventory.IsEmpty)
                {
                    shipment.Collected = true;
                    TsdDynamicMaps.Remove(shipment.Crate);
                    if (shipment.Beacon != null) shipment.Beacon.StopBeacon();
                    logger.LogInfo("[TSD] All cargo removed from ground shipment " + shipment.OrderId);
                }
            }
            shipments.RemoveAll(s => s.World == null || s.World != world || s.Crate == null || !s.Crate.IsActive);
            var current = shipments.LastOrDefault(s => !s.Collected) ?? shipments.LastOrDefault();
            if (phase == "pending" && statusWorld == world && world != null)
            {
                Publish(new { available = false, testOnly = false, phase, message, orderId, updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                return;
            }
            if (current != null && world?.MainPlayer != null)
            {
                var bounds = current.Crate.CollisionCollider.bounds;
                var player = world.MainPlayer;
                var camera = CameraManager.instance?.Camera ?? Camera.main;
                var forward = camera != null ? camera.transform.forward : player.Transform.Original.forward;
                float bearing = Vector3.SignedAngle(Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.ProjectOnPlane(bounds.center - player.Position, Vector3.up), Vector3.up);
                float elevation = bounds.min.y - player.Position.y;
                Publish(new
                {
                    available = true, testOnly = false, collected = current.Collected,
                    phase = current.Collected ? "collected" : "ready", orderId = current.OrderId,
                    distance = Math.Round(Vector3.Distance(player.Position, bounds.center), 1),
                    direction = Mathf.Abs(bearing) > 140 ? "BEHIND YOU" : bearing > 30 ? "TO YOUR RIGHT" : bearing < -30 ? "TO YOUR LEFT" : "AHEAD",
                    level = elevation > 2f ? "ABOVE YOUR LEVEL" : elevation < -2f ? "BELOW YOUR LEVEL" : "SAME LEVEL",
                    updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                return;
            }
            if (phase == "unavailable" && statusWorld == world && world != null)
            {
                Publish(new { available = false, testOnly = false, phase, message, orderId, updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                return;
            }
            Publish(new { available = false });

        }

        private void Publish(object state)
        {
            var json = JsonConvert.SerializeObject(state);
            if (json == lastJson) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(locatorPath));
                var temporary = locatorPath + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(locatorPath)) File.Replace(temporary, locatorPath, null);
                else File.Move(temporary, locatorPath);
                lastJson = json;
            }
            catch (Exception ex) { logger.LogDebug("Ground locator update deferred: " + ex.Message); }
        }

        internal void Shutdown()
        {
            foreach (var shipment in shipments)
            {
                if (shipment.Beacon != null) shipment.Beacon.StopBeacon();
                TsdDynamicMaps.Remove(shipment.Crate);
            }
            Publish(new { available = false });

        }
    }
}

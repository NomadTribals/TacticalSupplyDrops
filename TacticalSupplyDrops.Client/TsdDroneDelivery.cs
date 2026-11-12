using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.Airdrop;
using EFT.CameraControl;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.SynchronizableObjects;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalSupplyDrops.Client
{
    internal sealed partial class TsdDroneDelivery : MonoBehaviour
    {
        private readonly BepInEx.Logging.ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("TSD.DroneDelivery");
        private string status;
        private string orderId;
        private string manifest;
        private CompoundItem inventory;
        private Action<string> report;
        private readonly TaskCompletionSource<Vector3> delivered = new TaskCompletionSource<Vector3>();
        private readonly string locatorPath = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "drone-cargo-locator.json");
        internal static bool IsIndoorOnlyMap(string location)
        {
            location = location?.ToLowerInvariant();
            return location == "factory4_day" || location == "factory4_night" || location == "laboratory";
        }
        internal static Task<Vector3> Deliver(ClientGameWorld world, string id, string snapshot, Action<string> progress)
        {
            var owner = new GameObject("TSD Drone Shipment");
            var flight = owner.AddComponent<TsdDroneDelivery>();
            Flights.Add(flight);
            flight.orderId = id; flight.manifest = snapshot; flight.report = progress;
            _ = flight.Prepare(world);
            return flight.delivered.Task;
        }
        private static readonly List<TsdDroneDelivery> Flights = new List<TsdDroneDelivery>();
        private float nextLocationUpdate;
        private ClientGameWorld raid;

        private GameObject drone, parcel;
        private readonly List<Transform> rotors = new List<Transform>();
        private readonly List<Material> materials = new List<Material>();
        private static ConfigEntry<float> rotorVolume;
        private static ConfigEntry<float> dispatchDelay;
        private readonly System.Threading.CancellationTokenSource lifetime = new System.Threading.CancellationTokenSource();
        private AudioSource rotorAudio;
        internal static void Initialize(ConfigFile config)
        {
            dispatchDelay = config.Bind("Drone Delivery", "DispatchDelaySeconds", 5f,
                new ConfigDescription("Preparation time before the drone starts its distant approach.", new AcceptableValueRange<float>(5f, 120f)));
            rotorVolume = config.Bind("Drone Delivery", "RotorVolume", .085f,
                new ConfigDescription("Drone rotor loudness. Changes apply immediately during flight; 0 mutes the rotor.", new AcceptableValueRange<float>(0f, .3f)));
        }
        private AudioClip rotorClip;
        private LineRenderer tether;
        private AirdropSynchronizableObject crate;
        private LootableContainer container;
        private Renderer[] nativeRenderers;
        private bool[] rendererStates;
        private Collider[] nativeColliders;
        private bool[] colliderStates;
        private Rigidbody crateBody;
        private bool originalGravity, originalKinematic;
        private float phaseTime;
        private int phase;
        private Vector3 spawn, cruise, hover, ground, departure;

        private void Report(string text)
        {
            status = text;
            Logger.LogInfo(text);
            report?.Invoke(text);
        }

        private void PublishLocator(bool available, bool collected = false)
        {
            var offset = ground - raid.MainPlayer.Transform.position;
            var camera = CameraManager.instance?.Camera ?? Camera.main;
            float bearing = Vector3.SignedAngle(Vector3.ProjectOnPlane(camera != null ? camera.transform.forward : raid.MainPlayer.Transform.Original.forward, Vector3.up),
                Vector3.ProjectOnPlane(offset, Vector3.up), Vector3.up);
            var state = new Newtonsoft.Json.Linq.JObject {
                ["available"] = available, ["collected"] = collected, ["orderId"] = orderId, ["method"] = "drone",
                ["phase"] = collected ? "collected" : available ? "ready" : "pending", ["message"] = status,
                ["distance"] = Math.Round(offset.magnitude, 1),
                ["direction"] = Mathf.Abs(bearing) > 140 ? "BEHIND YOU" : bearing > 30 ? "TO YOUR RIGHT" : bearing < -30 ? "TO YOUR LEFT" : "AHEAD",
                ["level"] = offset.y > 2 ? "ABOVE YOUR LEVEL" : offset.y < -2 ? "BELOW YOUR LEVEL" : "SAME LEVEL",
                ["updatedAtUnixMs"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            TsdPendingCargo.WriteAtomic(locatorPath, state);
        }

        private void Update()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance as ClientGameWorld : null;
            if (!ReferenceEquals(raid, current) && (raid != null || inventory != null))
            {
                Cleanup();
                if (this != null) Destroy(gameObject);
                return;
            }
            if (raid?.MainPlayer != null && Time.unscaledTime >= nextLocationUpdate)
            {
                nextLocationUpdate = Time.unscaledTime + .5f;
                try
                {
                    var locatorOwner = Flights.LastOrDefault(f => f != null && f.raid == current && f.inventory != null && !f.inventory.IsEmpty)
                        ?? Flights.LastOrDefault(f => f != null && f.raid == current);
                    if (inventory != null && inventory.IsEmpty) TsdDynamicMaps.Remove(crate);
                    if (locatorOwner == this) PublishLocator(phase >= 3, inventory != null && inventory.IsEmpty);
                }
                catch (Exception ex) { Logger.LogDebug(ex.Message); }
            }
            if (drone == null) return;
            if (raid == null || raid.MainPlayer?.HealthController?.IsAlive != true)
            {
                Cleanup();
                if (this != null) Destroy(gameObject);
                return;
            }
            if (rotorAudio != null)
            {
                float distanceToListener = Vector3.Distance(drone.transform.position, raid.MainPlayer.Transform.position);
                float approachGain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(100f, 35f, distanceToListener));
                rotorAudio.volume = (rotorVolume?.Value ?? .085f) * approachGain;
            }
            UpdateBeacons();
            foreach (var rotor in rotors) rotor.Rotate(Vector3.up, 2300f * Time.deltaTime, Space.Self);
            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case 0: Move(spawn, cruise, Mathf.Clamp(Vector3.Distance(spawn, cruise) / 14f, 12f, 25f), 1, "Drone approaching the handoff area."); break;
                case 1: Move(cruise, hover, 6f, 2, "Drone lowering the supply parcel."); break;
                case 2:
                    drone.transform.position = hover;
                    parcel.transform.position = Vector3.Lerp(hover + Vector3.down * .85f, ground,
                        Mathf.SmoothStep(0f, 1f, phaseTime / 6f));
                    if (phaseTime >= 6f)
                    {
                        phase = 3;
                        phaseTime = 0;
                        container.enabled = true;
                        parcel.transform.SetParent(null, true);
                        parcel.transform.position = ground;
                        var box = parcel.AddComponent<BoxCollider>();
                        box.size = new Vector3(.46f, .3f, .34f);
                        box.center = Vector3.up * .15f;
                        box.gameObject.layer = container.gameObject.layer;
                        parcel.transform.SetParent(container.transform, true);
                        drone.transform.position = hover;
                        if (crate.CollisionCollider != null) crate.CollisionCollider.enabled = false;
                        TsdDynamicMaps.Register(crate,Logger,"TSD Drone Cargo");
                        delivered.TrySetResult(ground);
                        Report("TSD parcel delivered. Supplies ready for collection.");
                    }
                    break;
                case 3:
                    drone.transform.position = hover;
                    if (phaseTime >= 4f) { phase = 4; phaseTime = 0; Report("Cable secured. Drone leaving the handoff area."); }
                    break;
                case 4: Move(hover, cruise, 6f, 5, "Drone leaving the handoff area."); break;
                case 5:
                    drone.transform.position = Vector3.Lerp(cruise, departure, Mathf.Clamp01(phaseTime / 10f));
                    if (phaseTime >= 10f) { phase = 6; Report("Drone departing. Locate the parcel on the Delivery screen."); }
                    break;
                case 6:
                    var step = (departure - cruise).normalized * (Vector3.Distance(departure, cruise) / 10f) * Time.deltaTime;
                    string blocker;
                    if (Clear(drone.transform.position, drone.transform.position + step.normalized * 3f, out blocker))
                        drone.transform.position += step;
                    else if (Clear(drone.transform.position, drone.transform.position + Vector3.up * 3f, out blocker))
                        drone.transform.position += Vector3.up * 8f * Time.deltaTime;
                    float distance = Vector3.Distance(drone.transform.position, raid.MainPlayer.Transform.position);
                    if (distance > 150f && !Visible(drone.transform.position) && raid.MainPlayer.HandsController?.IsAiming != true)
                    {
                        Destroy(drone);
                        drone = null;
                        rotors.Clear();
                    }
                    break;
            }
            if (tether != null && drone != null && parcel != null)
            {
                tether.enabled = phase < 4;
                Vector3 hook = drone.transform.TransformPoint(Vector3.down * .18f);
                Vector3 end = phase == 3 ? Vector3.Lerp(ground + Vector3.up * .50f, hook + Vector3.down * .09f,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phaseTime - .6f) / 3.4f))) : parcel.transform.TransformPoint(Vector3.up * .50f);
                tether.SetPosition(0, hook);
                tether.SetPosition(1, end);
                if (cargoHook != null) { cargoHook.position = end; cargoHook.rotation = Quaternion.identity; }
                if (hookGate != null) hookGate.localRotation = Quaternion.Euler(0, 0, phase == 3 ? -65f * Mathf.Clamp01(phaseTime / .4f) : 0);
                if (winchDrum != null && (phase == 2 || phase == 3)) winchDrum.Rotate(Vector3.up, (phase == 2 ? 160f : -230f) * Time.deltaTime, Space.Self);
            }
        }

        private void Move(Vector3 from, Vector3 to, float seconds, int next, string text)
        {
            drone.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, phaseTime / seconds));
            if (phaseTime < seconds) return;
            phase = next;
            phaseTime = 0;
            Report(text);
        }

        private static bool Visible(Vector3 point)
        {
            var camera = CameraManager.instance?.Camera ?? Camera.main;
            if (camera == null) return true;
            var view = camera.WorldToViewportPoint(point);
            return view.z > 0 && view.x > -.1f && view.x < 1.1f && view.y > -.1f && view.y < 1.1f;
        }

        private static bool Clear(Vector3 a, Vector3 b, out string blocker)
        {
            var delta = b - a;
            bool blocked = Physics.SphereCast(a, .55f, delta.normalized, out RaycastHit hit,
                delta.magnitude, LayersMaskController.HighPolyWithTerrainNoGrassMask, QueryTriggerInteraction.Ignore);
            blocker = blocked ? hit.collider.name : "";
            return !blocked;
        }

        private bool FindRoute(Player player)
        {
            var position = player.Transform.position;
            int noNav = 0, noFloor = 0, noPath = 0, noSky = 0, blockedRoute = 0;
            var blockers = new HashSet<string>();
            if (!NavMesh.SamplePosition(position, out NavMeshHit origin, 3f, NavMesh.AllAreas))
            {
                Logger.LogDebug("Drone route check: player has no nearby navigable ground.");
                return false;
            }
            foreach (float radius in new[] { 6f, 10f, 15f })
            for (int i = 0; i < 24; i++)
            {
                var direction = Quaternion.Euler(0, i * 15, 0) * Vector3.forward;
                if (!NavMesh.SamplePosition(position + direction * radius, out NavMeshHit nav, 2f, NavMesh.AllAreas)) { noNav++; continue; }
                if (!Physics.Raycast(nav.position + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 4f,
                    LayersMaskController.HighPolyWithTerrainNoGrassMask, QueryTriggerInteraction.Ignore) || floor.normal.y < .94f)
                { noFloor++; continue; }
                ground = floor.point + Vector3.up * .008f;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(origin.position, nav.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                { noPath++; continue; }
                string blocker;
                // Start above ground by more than the drone's clearance radius.
                if (!Clear(ground + Vector3.up * 1.3f, ground + Vector3.up * 65f, out blocker))
                { noSky++; blockers.Add(blocker); continue; }
                foreach (float height in new[] { 35f, 50f, 75f })
                foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
                {
                    var routeDirection = Quaternion.Euler(0, angle, 0) * -player.Transform.Original.forward;
                    routeDirection.y = 0;
                    routeDirection.Normalize();
                    hover = ground + Vector3.up * 10f;
                    spawn = ground + routeDirection * 240f + Vector3.up * height;
                    cruise = ground + routeDirection * 15f + Vector3.up * height;
                    departure = spawn;
                    if (Visible(spawn)) continue;
                    if (!Clear(spawn, cruise, out blocker) || !Clear(cruise, hover, out blocker) ||
                        !Clear(hover, ground + Vector3.up * 1.3f, out blocker))
                    { blockedRoute++; blockers.Add(blocker); continue; }
                    Logger.LogInfo("Drone route found: handoff " + radius + " m, cruise " + height + " m, approach " + angle + " degrees.");
                    return true;
                }
            }
            Logger.LogDebug("Drone route rejected: navigation=" + noNav + ", surface=" + noFloor + ", walking path=" + noPath +
                ", overhead=" + noSky + ", flight=" + blockedRoute + ". Blockers: " + string.Join(", ", blockers.Take(5)));
            return false;
        }

        private async Task Prepare(ClientGameWorld world)
        {

            try
            {
                if (world == null || world is HideoutGameWorld || world.GetType().FullName != "ClientLocalGameWorld" || world.MainPlayer?.HealthController?.IsAlive != true)
                    throw new InvalidOperationException("Drone delivery requires an outdoor solo raid.");
                if (IsIndoorOnlyMap(world.LocationId)) throw new InvalidOperationException("This map uses ground couriers. Apply the prepaid drone fee to a courier delivery on the Delivery screen.");
                raid = world;
                Report("DISPATCH RECEIVED — PREPARING TSD DRONE AND SUPPLY PARCEL");
                await Task.Delay(TimeSpan.FromSeconds(dispatchDelay?.Value ?? 5f), lifetime.Token);
                if (!Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance != world || world.MainPlayer?.HealthController?.IsAlive != true)
                    throw new InvalidOperationException("Raid ended before the drone launched. Order remains ready to retry.");
                Report("VERIFYING PARCEL MANIFEST AND OUTDOOR FLIGHT CORRIDOR");
                if (!FindRoute(world.MainPlayer)) throw new InvalidOperationException("No clear outdoor flight route found. Move to an open area and retry.");
                var data = TsdCargoManifest.Parse(manifest, orderId);
                var tree = Singleton<ItemFactory>.Instance.FlatItemsToTree(data.data);
                var root = tree.Items[data.data[0]._id] as CompoundItem;
                if (root?.Grids == null || root.Grids.Length == 0 || root.IsEmpty)
                    throw new InvalidOperationException("The drone parcel has no deliverable cargo.");
                inventory = root;
                var resources = tree.Items.Values.SelectMany(item => item.Template.AllResources).ToArray();
                await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,
                    ObjectsFactory.AssemblyType.Online, resources, JobYieldPriority.Low);
                if (!Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance != world || world.MainPlayer.HealthController.IsAlive != true)
                    throw new InvalidOperationException("Raid changed while preparing the parcel.");
                var processor = world.ClientSynchronizableObjectLogicProcessor;
                crate = processor.TakeFromPool(SynchronizableObjectType.AirDrop) as AirdropSynchronizableObject;
                if (crate == null) throw new InvalidOperationException("Native container pool unavailable.");
                crate.transform.position = ground;
                crate.ObjectId = processor.TakeSynchronizedObjectId;
                crate.UniqueId = crate.ObjectId;
                crate.AirdropType = EAirdropType.Common;
                crate.ApplyAirdropViewType(AirdropSynchronizableObject.EAirdropViewType.Default);
                processor.InitStaticObject(crate);
                if (crate.AirdropFlare != null) crate.AirdropFlare.SetActive(false);
                if (crate.AirdropDust != null) crate.AirdropDust.SetActive(false);
                if (crate.Parachute != null) crate.Parachute.SetActive(false);
                container = crate.GetComponentInChildren<LootableContainer>(true);
                if (container == null) throw new InvalidOperationException("Native parcel interaction unavailable.");
                container.enabled = true;
                container.gameObject.SetActive(true);
                container.Id = root.Id;
                LootItem.CreateLootContainer(container, root, "TSD Compact Cargo Crate", world);
                nativeRenderers = crate.GetComponentsInChildren<Renderer>(true);
                rendererStates = nativeRenderers.Select(r => r.enabled).ToArray();
                foreach (var renderer in nativeRenderers) renderer.enabled = false;
                nativeColliders = crate.GetComponentsInChildren<Collider>(true);
                colliderStates = nativeColliders.Select(c => c.enabled).ToArray();
                foreach (var collider in nativeColliders) collider.enabled = false;
                crateBody = crate.GetComponent<Rigidbody>();
                if (crateBody != null)
                {
                    originalGravity = crateBody.useGravity;
                    originalKinematic = crateBody.isKinematic;
                    crateBody.useGravity = false;
                    crateBody.isKinematic = true;
                }
                container.enabled = false;
                BuildVisuals();
                drone.transform.position = spawn;
                phase = 0;
                phaseTime = 0;

                Report("Drone approaching. Watch and listen for the supply flight.");
            }
            catch (OperationCanceledException) { delivered.TrySetCanceled(); }
            catch (Exception ex)
            {
                delivered.TrySetException(ex);
                Cleanup();
                Report(ex.Message);
                if (this != null) Destroy(gameObject);
                Logger.LogError(ex);
            }

        }

        private Material MakeMaterial(Color color)
        {
            var material = new Material(Shader.Find("Standard"));
            color.a = 1f;
            material.color = color;
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = 2000;
            material.SetFloat("_Glossiness", .08f);
            materials.Add(material);
            return material;
        }

        private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
            return part;
        }

        private sealed class MarkingFit
        {
            internal Transform Transform;
            internal Renderer Renderer;
            internal float Width, Height;
            internal bool Fitted;
        }
        private Font markingFont;
        private Material markingMaterial;
        private readonly MaterialPropertyBlock markingProperties = new MaterialPropertyBlock();
        private Texture markingAtlasSource;
        private Texture2D markingAtlas;
        private bool markingAtlasDirty = true;

        private void OnMarkingFontRebuilt(Font font) { if (font == markingFont) markingAtlasDirty = true; }
        private void EnsureMarkingAtlas()
        {
            var source = markingFont.material.mainTexture;
            if (!markingAtlasDirty && markingAtlas != null && source == markingAtlasSource) return;
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Texture2D atlas = null;
            try
            {
                Graphics.Blit(source,target);
                RenderTexture.active=target;
                atlas=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
                atlas.ReadPixels(new Rect(0,0,source.width,source.height),0,0);
                var pixels=atlas.GetPixels32();
                // Font atlases can contain black RGB with glyph coverage only in alpha.
                for(int i=0;i<pixels.Length;i++) pixels[i]=new Color32(255,255,255,pixels[i].a);
                atlas.SetPixels32(pixels);atlas.Apply(false,false);atlas.filterMode=FilterMode.Bilinear;atlas.wrapMode=TextureWrapMode.Clamp;
                atlas.name="TSD white glyph atlas";
                if(markingAtlas!=null)Destroy(markingAtlas);
                markingAtlas=atlas;markingAtlasSource=source;markingAtlasDirty=false;
                markingMaterial.mainTexture=atlas;
            }
            catch { if(atlas!=null)Destroy(atlas);throw; }
            finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target); }
        }
        private readonly List<MarkingFit> markings = new List<MarkingFit>();

        private void Branding(Transform parent, string words, Vector3 position, Vector3 angles, float size, Color color,
            float maxWidth = .18f, float maxHeight = .045f)
        {
            if (markingFont == null)
            {
                markingFont = Instantiate(Resources.GetBuiltinResource<Font>("Arial.ttf"));
                markingFont.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /-.",48,FontStyle.Bold);
                Font.textureRebuilt += OnMarkingFontRebuilt;
                // Keep normal scene depth testing while supplying white glyph RGB.
                markingMaterial = new Material(Shader.Find("Sprites/Default"));
                markingMaterial.name = "TSD depth-tested markings";
                markingMaterial.SetColor("_Color",Color.white);
                markingMaterial.SetColor("_RendererColor",Color.white);
                EnsureMarkingAtlas();
                markingMaterial.renderQueue = 3000;
                materials.Add(markingMaterial);
            }
            var label = new GameObject("TSD Logistics flight markings");
            label.transform.SetParent(parent, false); label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(angles);
            var text = label.AddComponent<TextMesh>(); text.font = markingFont; text.text = words; text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center; text.characterSize = size; text.fontSize = 48; text.fontStyle = FontStyle.Bold; text.color = color;
            var renderer = label.GetComponent<MeshRenderer>(); renderer.sharedMaterial = markingMaterial;
            markings.Add(new MarkingFit { Transform = label.transform, Renderer = renderer, Width = maxWidth, Height = maxHeight });
        }

        private void LateUpdate()
        {
            if (markingFont == null || markingMaterial == null) return;
            EnsureMarkingAtlas();
            foreach(var marking in markings)
            {
                if (marking.Renderer == null) continue;
                markingProperties.Clear();
                markingProperties.SetTexture("_MainTex",markingAtlas);
                markingProperties.SetColor("_RendererColor",Color.white);
                marking.Renderer.SetPropertyBlock(markingProperties);
                if (marking.Fitted) continue;
                var size = Quaternion.Inverse(marking.Transform.rotation) * marking.Renderer.bounds.size;
                float width = Mathf.Abs(size.x), height = Mathf.Abs(size.y);
                if (width < .00001f || height < .00001f) continue;
                marking.Transform.localScale *= Mathf.Min(1f, marking.Width / width, marking.Height / height);
                marking.Fitted = true;
            }
        }

        private void BuildVisuals()
        {
            BuildHardware();
            var audio = drone.AddComponent<AudioSource>();
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                samples[i] = (.22f * Mathf.Sin(2 * Mathf.PI * 170 * time) + .08f * Mathf.Sin(2 * Mathf.PI * 340 * time)) * (.9f + .1f * Mathf.Sin(2 * Mathf.PI * 28 * time));
            }
            rotorClip = AudioClip.Create("TSD delivery rotor", samples.Length, 1, sampleRate, false);
            rotorClip.SetData(samples, 0);
            audio.clip = rotorClip;
            audio.loop = true;
            audio.spatialBlend = 1;
            rotorAudio = audio;
            audio.volume = 0f;
            audio.minDistance = 3;
            audio.maxDistance = 100;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.Play();
        }

        private void Cleanup()
        {
            TsdDynamicMaps.Remove(crate);
            delivered.TrySetException(new InvalidOperationException("Drone delivery interrupted. Undelivered orders remain ready to retry."));
            if (orderId != null)
            {
                try
                {
                    if (System.IO.File.Exists(locatorPath) && Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(locatorPath))["orderId"]?.Value<string>() == orderId)
                        TsdPendingCargo.WriteAtomic(locatorPath, new Newtonsoft.Json.Linq.JObject { ["available"] = false });
                }
                catch (Exception ex) { Logger.LogDebug(ex.Message); }
            }
            if (parcel != null) Destroy(parcel);
            if (drone != null) Destroy(drone);
            rotors.Clear();
            if (crate != null)
            {
                if (crateBody != null)
                {
                    crateBody.useGravity = originalGravity;
                    crateBody.isKinematic = originalKinematic;
                }
                if (nativeRenderers != null)
                    for (int i = 0; i < nativeRenderers.Length; i++) if (nativeRenderers[i] != null) nativeRenderers[i].enabled = rendererStates[i];
                if (nativeColliders != null)
                    for (int i = 0; i < nativeColliders.Length; i++) if (nativeColliders[i] != null) nativeColliders[i].enabled = colliderStates[i];
                if (raid != null && Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance == raid)
                {
                    if (container != null)
                    {
                        if (container.ItemOwner != null) raid.ItemOwners.Remove(container.ItemOwner);
                        raid.LootList.Remove(container);
                        container.ItemOwner = null;
                        container.IsInitialized = false;
                    }
                    var processor = raid.ClientSynchronizableObjectLogicProcessor;
                    processor._staticSynchronizableObjects.Remove(crate);
                    processor.ReturnToPool(crate);
                }
            }
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear();
            foreach (var mesh in visualMeshes) if (mesh != null) Destroy(mesh);
            visualMeshes.Clear();
            foreach (var texture in visualTextures) if (texture != null) Destroy(texture);
            visualTextures.Clear();
            Font.textureRebuilt -= OnMarkingFontRebuilt;
            if (markingAtlas != null) Destroy(markingAtlas);
            markingAtlas=null;markingAtlasSource=null;markingAtlasDirty=true;
            if (markingFont != null) Destroy(markingFont);
            markingFont = null; markingMaterial = null; markings.Clear();
            if (beaconSprite != null) Destroy(beaconSprite);
            navigationGlows.Clear(); navigationLeds.Clear(); navigationMaterials.Clear();
            if (rotorClip != null) Destroy(rotorClip);
            drone = null; parcel = null; crate = null; container = null; raid = null;
            nativeColliders = null; nativeRenderers = null;
            crateBody = null;
        }

        private void OnDestroy() { lifetime.Cancel(); Flights.Remove(this); Cleanup(); lifetime.Dispose(); }
    }
}

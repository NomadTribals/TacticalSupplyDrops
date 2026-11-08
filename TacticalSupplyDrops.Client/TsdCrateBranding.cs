using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using EFT.SynchronizableObjects;

namespace TacticalSupplyDrops.Client
{
    // Instance-only materials and decorations: never change the native pooled prefab.
    public sealed class TsdCrateBranding : MonoBehaviour
    {
        private static GameObject badgePrefab;
        private static GameObject beaconPrefab;
        private static bool loaded;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<KeyValuePair<Renderer, Material[]>> originals = new List<KeyValuePair<Renderer, Material[]>>();
        private bool restored;

        private static Task loading;

        internal static Task Preload()
        {
            if (loaded) return Task.CompletedTask;
            if (loading == null || loading.IsFaulted || loading.IsCanceled) loading = LoadAssetsAsync();
            return loading;
        }

        private static async Task LoadAssetsAsync()
        {
            string folder = Path.GetDirectoryName(typeof(TsdCrateBranding).Assembly.Location);
            string path = Path.Combine(folder, "tsd_courier_visuals.bundle");
            if (!File.Exists(path)) throw new FileNotFoundException("TSD courier visual bundle was not found.", path);
            // Match the native EasyBundle loader used for the working tablet assets.
            var request = AssetBundle.LoadFromFileAsync(path);
            while (!request.isDone) await Task.Yield();
            var bundle = request.assetBundle;
            if (bundle == null) throw new InvalidOperationException("TSD courier visual bundle could not be loaded asynchronously: " + path + "; runtime=" + Application.unityVersion);
            try
            {
                badgePrefab = bundle.LoadAsset<GameObject>("Assets/TsdCourier/TsdBadge.prefab");
                beaconPrefab = bundle.LoadAsset<GameObject>("Assets/TsdCourier/TsdBeacon.prefab");
                if (badgePrefab == null || beaconPrefab == null) throw new InvalidOperationException("TSD courier visual prefabs are missing.");
                loaded = true;
            }
            finally { bundle.Unload(false); }
        }

        private static void LoadAssets()
        {
            if (!loaded) throw new InvalidOperationException("TSD courier visuals were not preloaded.");
        }

        internal static GameObject CreateBeaconModel(Transform parent)
        {
            LoadAssets();
            return Instantiate(beaconPrefab, parent, false);
        }

        internal static void Attach(AirdropSynchronizableObject crate, Action<string> report)
        {
            if (crate.GetComponentsInChildren<TsdCrateBranding>(true).Any(existing => !existing.restored)) return;
            LoadAssets();
            var root = new GameObject("TSD_Crate_Branding");
            root.transform.SetParent(crate.transform, false);
            var branding = root.AddComponent<TsdCrateBranding>();
            try { branding.Initialize(crate); report?.Invoke("Applied instance-only TSD charcoal skin and badges."); }
            catch { branding.Restore(); Destroy(root); throw; }
        }

        private void Initialize(AirdropSynchronizableObject crate)
        {
            var box = crate.CollisionCollider;
            foreach (var renderer in crate.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if ((crate.Parachute != null && renderer.transform.IsChildOf(crate.Parachute.transform)) ||
                    (crate.AirdropFlare != null && renderer.transform.IsChildOf(crate.AirdropFlare.transform)) ||
                    (crate.AirdropDust != null && renderer.transform.IsChildOf(crate.AirdropDust.transform))) continue;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(transform)) continue;
                if (renderer.bounds.size.magnitude > box.bounds.size.magnitude * 1.6f) continue;
                var source = renderer.sharedMaterials;
                var tinted = new Material[source.Length];
                originals.Add(new KeyValuePair<Renderer, Material[]>(renderer, source));
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null) continue;
                    var material = new Material(source[i]);
                    materials.Add(material);
                    // Preserve native textures, normal maps and shader lighting.
                    var tint = new Color(.25f, .28f, .30f, 1f);
                    foreach (string property in new[] { "_Color", "_MainColor" })
                        if (material.HasProperty(property)) material.SetColor(property, tint);
                    tinted[i] = material;
                }
                renderer.sharedMaterials = tinted;
            }
            var scale = box.transform.lossyScale;
            var half = Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * .5f;
            var center = box.transform.TransformPoint(box.center);
            var rotation = box.transform.rotation;
            float badgeScale = Mathf.Min(1f, half.y * 1.3f / .48f, half.x * 1.2f / .48f);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var badge = Instantiate(badgePrefab, transform, false);
                badge.name = "TSD_Side_Badge_" + sign;
                badge.transform.position = center + rotation * new Vector3(0, 0, sign * (half.z + .018f));
                badge.transform.rotation = rotation * Quaternion.Euler(0, sign == 1 ? 0 : 180, 0);
                badge.transform.localScale = Vector3.one;
                badge.transform.localScale *= badgeScale / Mathf.Max(.001f, badge.transform.lossyScale.x);
                SetLayer(badge, crate.gameObject.layer);
            }
            // Narrow painted strips sit on the body sides; leave the native lid free to animate.
            var stripeMaterial = new Material(badgePrefab.GetComponentsInChildren<Renderer>(true)
                .First(r => r.name == "OrangeFrame").sharedMaterial);
            materials.Add(stripeMaterial);
            for (int sign = -1; sign <= 1; sign += 2)
                for (int edge = -1; edge <= 1; edge += 2)
                {
                    var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    strip.name = "TSD_Orange_Side_Marking";
                    var collider = strip.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    strip.transform.SetParent(transform, false);
                    strip.transform.position = center + rotation * new Vector3(edge * half.x * .78f, -half.y * .1f, sign * (half.z + .009f));
                    strip.transform.rotation = rotation;
                    var size = new Vector3(Mathf.Min(.055f, half.x * .08f), half.y * 1.55f, .006f);
                    var inherited = strip.transform.lossyScale;
                    strip.transform.localScale = new Vector3(size.x / Mathf.Abs(inherited.x), size.y / Mathf.Abs(inherited.y), size.z / Mathf.Abs(inherited.z));
                    strip.GetComponent<Renderer>().sharedMaterial = stripeMaterial;
                    SetLayer(strip, crate.gameObject.layer);
                }
        }

        internal static void SetLayer(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }

        private void Restore()
        {
            if (restored) return;
            restored = true;
            foreach (var entry in originals) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
            foreach (var material in materials) if (material != null) Destroy(material);
            originals.Clear(); materials.Clear();
        }
        internal void Release() { Restore(); gameObject.SetActive(false); Destroy(gameObject); }
        private void OnDisable() { Restore(); Destroy(gameObject); }
        private void OnDestroy() { Restore(); }
    }
}

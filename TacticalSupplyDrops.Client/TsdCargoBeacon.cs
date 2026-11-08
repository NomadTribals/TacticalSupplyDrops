using System;
using System.Linq;
using Comfort.Common;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    public sealed class TsdCargoBeacon : MonoBehaviour
    {
        private Material ownMaterial;
        private AudioClip chirp;
        private Action<string> report;
        private int reportedChirps;
        private Light pulse;
        private Renderer lamp;
        private Func<bool> soundEnabled;
        private Func<float> soundVolume;
        private float nextChirp;
        private bool stopped;
        private bool retiring;

        public static TsdCargoBeacon Attach(EFT.SynchronizableObjects.AirdropSynchronizableObject crate, Func<bool> enabled, Func<float> volume, Action<string> diagnostic = null)
        {
            var physical = crate.CollisionCollider.bounds;
            var meshes = crate.GetComponentsInChildren<MeshRenderer>(false)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.magnitude < physical.size.magnitude * 1.6f)
                .ToArray();
            var visual = physical;
            if (meshes.Length > 0)
            {
                visual = meshes[0].bounds;
                foreach (var mesh in meshes.Skip(1)) visual.Encapsulate(mesh.bounds);
            }
            var go = new GameObject("TSD_Cargo_Beacon");
            go.name = "TSD_Cargo_Beacon";
            go.transform.position = new Vector3(visual.center.x, visual.max.y + .012f, visual.center.z);
            go.transform.SetParent(crate.transform, true);
            go.layer = meshes.Length > 0 ? meshes[0].gameObject.layer : crate.gameObject.layer;
            GameObject model;
            try { model = TsdCrateBranding.CreateBeaconModel(go.transform); }
            catch (Exception ex)
            {
                diagnostic?.Invoke("Visual asset unavailable; using independent fallback lamp and beeper: " + ex.Message);
                model = new GameObject("TSD_Fallback_Beacon");
                model.transform.SetParent(go.transform, false);
                var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lens.name = "BeaconLens";
                lens.transform.SetParent(model.transform, false);
                lens.transform.localPosition = Vector3.up * .05f;
                lens.transform.localScale = new Vector3(.12f, .04f, .12f);
                var collider = lens.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            }
            model.transform.localScale = Vector3.one;
            // Keep metre-sized accessories independent of the native crate's transform scale.
            go.transform.localScale = new Vector3(1f / Mathf.Abs(crate.transform.lossyScale.x),
                1f / Mathf.Abs(crate.transform.lossyScale.y), 1f / Mathf.Abs(crate.transform.lossyScale.z));
            TsdCrateBranding.SetLayer(go, go.layer);
            var beacon = go.AddComponent<TsdCargoBeacon>();
            try { beacon.Initialize(enabled, volume, diagnostic); return beacon; }
            catch { Destroy(go); throw; }
        }

        private void Initialize(Func<bool> enabled, Func<float> volume, Action<string> diagnostic = null)
        {
            report = diagnostic;
            soundEnabled = enabled;
            soundVolume = volume;
            lamp = GetComponentsInChildren<Renderer>().First(r => r.name == "BeaconLens");
            ownMaterial = new Material(lamp.sharedMaterial);
            ownMaterial.color = new Color(1f, .35f, .02f);
            ownMaterial.EnableKeyword("_EMISSION");
            lamp.sharedMaterial = ownMaterial;
            pulse = gameObject.AddComponent<Light>();
            pulse.type = LightType.Point;
            pulse.color = new Color(1f, .35f, .02f);
            pulse.range = 5f;
            pulse.shadows = LightShadows.None;
            const int rate = 22050;
            const float duration = .30f;
            var samples = new float[(int)(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float onset = t < .12f ? 0f : .17f;
                float local = t - onset;
                float envelope = local >= 0f && local < .12f ? Mathf.Clamp01(local / .01f) * Mathf.Clamp01((.12f - local) / .025f) : 0f;
                float frequency = t < .12f ? 880f : 1100f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * .6f * envelope;
            }
            chirp = AudioClip.Create("TSD_Cargo_Beacon_Chirp", samples.Length, 1, rate, false);
            chirp.SetData(samples, 0);
            nextChirp = Time.time + 2f;
        }

        private void Update()
        {
            if (stopped || retiring) return;
            float phase = Time.time % 2f;
            bool flashing = phase < .10f || (phase >= .20f && phase < .30f);
            pulse.intensity = flashing ? 1.1f : 0f;
            if (ownMaterial.HasProperty("_EmissionColor")) ownMaterial.SetColor("_EmissionColor", new Color(1f, .25f, .01f) * (flashing ? 4f : .025f));
            if (soundEnabled != null && !soundEnabled()) return;
            if (Time.time >= nextChirp)
            {
                nextChirp = Time.time + 4f;
                try
                {
                    var audio = MonoBehaviourSingleton<BetterAudio>.Instance;
                    float volume = soundVolume == null ? .12f : soundVolume();
                    var played = audio == null ? null : audio.PlayAtPoint(transform.position, chirp,
                        BetterAudio.AudioSourceGroupType.InteractiveObjects, 14, volume,
                        EOcclusionTest.OneShotPropagation, forceMixerGroup: audio.VeryStandartMixerGroup,
                        spatialize: true, oneShot: true, autoReleaseSource: true, enabledHighPassFilter: false);
                    if (played != null && reportedChirps < 2)
                    {
                        reportedChirps++;
                        report?.Invoke("Native positional chirp started; volume=" + volume.ToString("0.00") + ", range=14 m, clip=" + chirp.loadState);
                    }
                }
                catch (Exception ex)
                {
                    if (reportedChirps < 2) { reportedChirps++; report?.Invoke("Beeper playback error: " + ex.Message); }
                }
            }
        }

        public void StopBeacon()
        {
            stopped = true;
            if (pulse != null) pulse.enabled = false;
            if (ownMaterial != null && ownMaterial.HasProperty("_EmissionColor")) ownMaterial.SetColor("_EmissionColor", Color.black);
        }
        private void OnDisable()
        {
            retiring = true;
            StopBeacon();
            if (lamp != null) lamp.enabled = false;
            Destroy(gameObject);
        }
        private void OnDestroy()
        {
            if (ownMaterial != null) Destroy(ownMaterial);
            if (chirp != null) Destroy(chirp);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    // Owns only the materials on this TSD effect instance. Pooling must restore them.
    public sealed class TsdSmokeTint : MonoBehaviour
    {
        private static readonly Color Orange = new Color(1f, .45f, .025f, 1f);
        private static readonly string[] TintProperties =
            { "_TintColor", "_Color", "_BaseColor", "_MainColor", "_Color1", "_Color2" };
        private readonly List<ParticleState> _particles = new List<ParticleState>();
        private readonly List<RendererState> _renderers = new List<RendererState>();
        private int _activationFrames;
        public bool IsApplied { get; private set; }

        public bool Apply()
        {
            if (IsApplied) return false;
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0) return false;
            IsApplied = true;
            try
            {
                foreach (var renderer in GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var original = renderer.sharedMaterials;
                    var copies = new Material[original.Length];
                    // Register ownership before assigning so partial failures can be restored.
                    _renderers.Add(new RendererState { Renderer = renderer, Original = original, Copies = copies });
                    for (int i = 0; i < original.Length; i++)
                    {
                        if (original[i] == null) continue;
                        copies[i] = new Material(original[i]);
                        copies[i].name = original[i].name + " (TSD orange)";
                        bool primary = true;
                        foreach (string property in TintProperties)
                        {
                            if (!copies[i].HasProperty(property)) continue;
                            Color previous = copies[i].GetColor(property);
                            float intensity = Mathf.Max(previous.r, Mathf.Max(previous.g, previous.b));
                            Color tint = (primary ? Orange : Color.white) * intensity;
                            tint.a = previous.a;
                            copies[i].SetColor(property, tint);
                            primary = false;
                        }
                    }
                    renderer.sharedMaterials = copies;
                }
                foreach (var system in systems)
                {
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    bool materialTints = false;
                    if (renderer != null)
                        foreach (var material in renderer.sharedMaterials)
                            if (material != null)
                                foreach (string property in TintProperties)
                                    materialTints |= material.HasProperty(property);
                    _particles.Add(new ParticleState
                    {
                        System = system,
                        Start = system.main.startColor,
                        Lifetime = system.colorOverLifetime.color,
                        Speed = system.colorBySpeed.color,
                        // Tint once: in the material if supported, otherwise in particle color.
                        Tint = materialTints ? Color.white : Orange
                    });
                }
                RefreshParticleColors();
                _activationFrames = 2;
                return true;
            }
            catch
            {
                Restore();
                throw;
            }
        }

        private void OnEnable() { _activationFrames = 2; }

        private void LateUpdate()
        {
            // Reapply after the flare's activation scripts, including already-emitted particles.
            if (!IsApplied || _activationFrames <= 0) return;
            RefreshParticleColors();
            _activationFrames--;
        }

        private void RefreshParticleColors()
        {
            foreach (var state in _particles)
            {
                if (state.System == null) continue;
                var main = state.System.main;
                main.startColor = Retint(state.Start, state.Tint);
                var lifetime = state.System.colorOverLifetime;
                lifetime.color = Retint(state.Lifetime, Color.white);
                var speed = state.System.colorBySpeed;
                speed.color = Retint(state.Speed, Color.white);
                int count = state.System.particleCount;
                if (count == 0) continue;
                var live = new ParticleSystem.Particle[count];
                count = state.System.GetParticles(live);
                for (int i = 0; i < count; i++)
                {
                    Color color = state.Tint;
                    color.a = ((Color)live[i].startColor).a;
                    live[i].startColor = color;
                }
                state.System.SetParticles(live, count);
            }
        }

        private static ParticleSystem.MinMaxGradient Retint(ParticleSystem.MinMaxGradient value, Color tint)
        {
            var mode = value.mode;
            Color low = tint;
            low.a = value.colorMin.a;
            Color high = tint;
            high.a = value.colorMax.a;
            value.colorMin = low;
            value.colorMax = high;
            if (value.gradientMin != null) value.gradientMin = RetintGradient(value.gradientMin, tint);
            if (value.gradientMax != null) value.gradientMax = RetintGradient(value.gradientMax, tint);
            value.mode = mode;
            return value;
        }

        private static Gradient RetintGradient(Gradient source, Color tint)
        {
            var result = new Gradient();
            result.SetKeys(new[] { new GradientColorKey(tint, 0), new GradientColorKey(tint, 1) }, source.alphaKeys);
            result.mode = source.mode;
            return result;
        }

        public void Restore()
        {
            if (!IsApplied) return;
            foreach (var state in _particles)
            {
                if (state.System == null) continue;
                var main = state.System.main;
                main.startColor = state.Start;
                var lifetime = state.System.colorOverLifetime;
                lifetime.color = state.Lifetime;
                var speed = state.System.colorBySpeed;
                speed.color = state.Speed;
            }
            foreach (var state in _renderers)
            {
                if (state.Renderer != null) state.Renderer.sharedMaterials = state.Original;
                foreach (var material in state.Copies)
                    if (material != null)
                    {
                        if (Application.isPlaying) Destroy(material);
                        else DestroyImmediate(material);
                    }
            }
            _particles.Clear();
            _renderers.Clear();
            IsApplied = false;
        }

        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }

        private sealed class ParticleState
        {
            public ParticleSystem System;
            public ParticleSystem.MinMaxGradient Start, Lifetime, Speed;
            public Color Tint;
        }

        private sealed class RendererState
        {
            public ParticleSystemRenderer Renderer;
            public Material[] Original, Copies;
        }
    }
}

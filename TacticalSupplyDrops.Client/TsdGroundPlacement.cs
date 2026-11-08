using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using EFT;
using EFT.CameraControl;
using EFT.Interactive;
using EFT.SynchronizableObjects;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalSupplyDrops.Client
{
    internal static class TsdGroundPlacement
    {
        private static Camera GameplayCamera() => CameraManager.instance?.Camera ?? Camera.main;
        private static object Read(object value, string name)
        {
            if (value == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            return value.GetType().GetProperty(name, flags)?.GetValue(value) ?? value.GetType().GetField(name, flags)?.GetValue(value);
        }
        private static bool PathCrossesLockedDoor(NavMeshPath path)
        {
            for (int i = 1; i < path.corners.Length; i++)
            {
                var start = path.corners[i - 1] + Vector3.up * .9f;
                var delta = path.corners[i] - path.corners[i - 1];
                foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    var door = hit.collider.GetComponentInParent<Door>();
                    if (door != null && door.DoorState.ToString() == "Locked") return true;
                }
            }
            return false;
        }

        private static bool IsSolidOccluder(Collider collider)
        {
            string name = collider.name.ToLowerInvariant();
            if (name.Contains("glass") || name.Contains("fence") || name.Contains("grate")) return false;
            var ballistic = collider.GetComponentInParent<EFT.Ballistics.BallisticCollider>();
            var material = Read(ballistic, "TypeOfMaterial")?.ToString() ?? string.Empty;
            return material.IndexOf("glass", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool CrateIsOccluded(BoxCollider box, Vector3 half, Vector3 eye, Player player, AirdropSynchronizableObject crate)
        {
            var center = box.transform.TransformPoint(box.center);
            for (int i = -1; i < 8; i++)
            {
                var point = center;
                if (i >= 0)
                    point += box.transform.right * half.x * ((i & 1) == 0 ? -.9f : .9f)
                        + box.transform.up * half.y * ((i & 2) == 0 ? -.9f : .9f)
                        + box.transform.forward * half.z * ((i & 4) == 0 ? -.9f : .9f);
                var delta = point - eye;
                bool blocked = Physics.RaycastAll(eye, delta.normalized, Mathf.Max(0, delta.magnitude - .15f), ~0, QueryTriggerInteraction.Ignore)
                    .Any(hit => !hit.transform.IsChildOf(player.Transform.Original) && !hit.transform.IsChildOf(crate.transform)
                        && IsSolidOccluder(hit.collider));
                if (!blocked) return false;
            }
            return true;
        }

        private static IEnumerable<Vector3> CandidatePoints(Vector3 origin, Vector3 forward)
        {
            foreach (float radius in new[] { 64f, 84f, 110f, 145f })
                for (int angle = 0; angle < 360; angle += 15)
                    yield return origin + Quaternion.Euler(0, angle, 0) * forward * radius;

            // Sample the loaded map's navigation mesh, including other elevations.
            // Spread a bounded sample across the complete mesh rather than searching only nearby rings.
            var navigation = NavMesh.CalculateTriangulation();
            int count = navigation.indices.Length / 3;
            int stride = Math.Max(1, (int)Math.Ceiling(count / 2048d));
            int offset = count == 0 ? 0 : new System.Random().Next(Math.Min(stride, count));
            for (int triangle = offset; triangle < count; triangle += stride)
            {
                int i = triangle * 3;
                yield return (navigation.vertices[navigation.indices[i]] + navigation.vertices[navigation.indices[i + 1]]
                    + navigation.vertices[navigation.indices[i + 2]]) / 3f;
            }
        }

        internal static float HorizontalEdgeDistance(Vector3 origin, Vector3 center, Quaternion rotation, Vector3 half)
        {
            // Pure geometry avoids reading stale physics transforms while moving the candidate crate.
            var local = Quaternion.Inverse(rotation) * (origin - center);
            var closest = center + rotation * new Vector3(Mathf.Clamp(local.x, -half.x, half.x),
                Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
            return Vector3.ProjectOnPlane(closest - origin, Vector3.up).magnitude;
        }

        internal static async Task<Vector3?> FindCratePosition(AirdropSynchronizableObject crate, Player player,
            Action<string> report, Func<bool> access)
        {
            var box = crate.CollisionCollider;
            var scale = box.transform.lossyScale;
            var half = Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * .5f;
            if (half.x <= .1f || half.y <= .1f || half.z <= .1f || half.x > 5 || half.z > 5)
                throw new InvalidOperationException("The loaded crate has unexpected physical dimensions.");
            const float minimumSeparation = 40f;
            int distanceReject = 0, samples = 0, navReject = 0, floorReject = 0, pathReject = 0;
            int doorReject = 0, clearanceReject = 0, visibleReject = 0;
            var renderers = crate.GetComponentsInChildren<Renderer>(true);
            var rendererStates = renderers.Select(renderer => renderer.enabled).ToArray();
            foreach (var renderer in renderers) renderer.enabled = false;
            var visited = new HashSet<string>();
            try
            {
                foreach (var candidate in CandidatePoints(player.Position, player.Transform.Original.forward))
                {
                    if (++samples % 12 == 0) await Task.Yield();
                    if (!access()) throw new InvalidOperationException("Field terminal link lost. The order remains ready to retry.");
                    var origin = player.Position;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit nav, 1.5f, NavMesh.AllAreas)) { navReject++; continue; }
                    string key = Mathf.RoundToInt(nav.position.x * 2) + ":" + Mathf.RoundToInt(nav.position.y * 2) + ":" + Mathf.RoundToInt(nav.position.z * 2);
                    if (!visited.Add(key)) continue;
                    if (Vector3.ProjectOnPlane(nav.position - origin, Vector3.up).magnitude < minimumSeparation) { distanceReject++; continue; }
                    var floors = Physics.RaycastAll(nav.position + Vector3.up, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(hit => !hit.transform.IsChildOf(crate.transform) && !hit.transform.IsChildOf(player.Transform.Original)
                            && Vector3.Dot(hit.normal, Vector3.up) >= .97f && Mathf.Abs(hit.point.y - nav.position.y) < .6f)
                        .OrderBy(hit => hit.distance).ToArray();
                    if (floors.Length == 0) { floorReject++; continue; }
                    var ground = floors[0];
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(origin, nav.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) { pathReject++; continue; }
                    if (PathCrossesLockedDoor(path)) { doorReject++; continue; }
                    crate.transform.position = ground.point;
                    var center = box.transform.TransformPoint(box.center);
                    crate.transform.position += Vector3.up * (ground.point.y - (center.y - half.y) + .05f);
                    center = box.transform.TransformPoint(box.center);
                    float separation = HorizontalEdgeDistance(origin, center, box.transform.rotation, half);
                    if (separation < minimumSeparation) { distanceReject++; continue; }
                    if (Physics.OverlapBox(center, half * .98f, box.transform.rotation, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => !c.transform.IsChildOf(crate.transform))) { clearanceReject++; continue; }
                    var camera = GameplayCamera();
                    if (camera == null || !CrateIsOccluded(box, half, camera.transform.position, player, crate)) { visibleReject++; continue; }
                    report?.Invoke("Accepted hidden handoff after " + samples + " map samples; edge distance=" + separation.ToString("0.0") + " m.");
                    return crate.transform.position;
                }
                report?.Invoke("Rejected " + samples + " map samples: distance=" + distanceReject + ", nav=" + navReject + ", floor=" + floorReject + ", path=" + pathReject + ", locked door=" + doorReject + ", blocked footprint=" + clearanceReject + ", visible=" + visibleReject);
                return null;
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = rendererStates[i];
            }
        }
    }
}

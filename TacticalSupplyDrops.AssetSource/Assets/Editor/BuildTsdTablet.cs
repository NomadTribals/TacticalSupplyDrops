#if TSD_WTT_SDK
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using EFT.AssetsManager;

public static class BuildTsdTablet
{
    private const string Source = "Assets/TsdFieldTerminal/TsdFieldTerminal.prefab";
    private const string Target = "Assets/TsdFieldTerminal/TsdTabletLoot.prefab";
    private const string Bundle = "assets/content/items/equipment/tsd_field_terminal/tsd_field_terminal.bundle";

    [MenuItem("TSD/Prepare Tablet Loot Prefab")]
    public static void Prepare()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (model == null) throw new InvalidOperationException("Tablet source model not found.");
        var root = new GameObject("TSD Field Terminal");
        try
        {
            var mesh = UnityEngine.Object.Instantiate(model, root.transform);
            mesh.name = "TabletModel";
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = Vector3.one * 0.25f;

            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            var pool = root.AddComponent<AssetPoolObject>();
            pool.Colliders.Add(collider);

            var preview = root.AddComponent<PreviewPivot>();
            preview.pivotPosition = bounds.center;
            preview.pivotRotation = Quaternion.Euler(0, 180, 0);
            preview.scale = Vector3.one;
            preview.Icon = new PreviewPivot.IconSettings
            {
                rotation = Quaternion.Euler(8, 165, -4),
                rotationEuler = new Vector3(8, 165, -4),
                boundsScale = 0.9f,
                perspective = 15,
                orthographic = false
            };
            PrefabUtility.SaveAsPrefabAsset(root, Target);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Target);
        Debug.Log("TSD tablet loot prefab prepared: PreviewPivot, AssetPoolObject, collider, 29 cm housing. Requires SpecItem parent on server.");
    }

    [MenuItem("TSD/Build Repaired Tablet Bundle")]
    public static void Build()
    {
        Prepare();
        // Build only this prefab; do not rebuild the SDK's example or imposter bundles.
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build"));
        Directory.CreateDirectory(output);
        var manifest = BuildPipeline.BuildAssetBundles(output, new[]
        {
            new AssetBundleBuild { assetBundleName = Bundle, assetNames = new[] { Target } }
        }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new InvalidOperationException("Tablet bundle build failed.");
        var bundle = AssetBundle.LoadFromFile(Path.Combine(output, Bundle));
        if (bundle == null) throw new InvalidOperationException("Built bundle cannot be reopened.");
        try
        {
            var tablet = bundle.LoadAllAssets<GameObject>().Single();
            if (tablet.GetComponent<PreviewPivot>() == null || tablet.GetComponent<AssetPoolObject>() == null
                || tablet.GetComponent<Collider>() == null)
                throw new InvalidOperationException("Required tablet components did not survive the bundle build.");
            foreach (var transform in tablet.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidOperationException("Built tablet has a missing component script.");
            RenderPreview(tablet, Path.Combine(output, "tablet-preview.png"));
        }
        finally { bundle.Unload(true); }
        File.WriteAllText(Path.Combine(output, "build-check.txt"),
            "Built " + DateTime.UtcNow.ToString("O") + "\n" +
            "Unity " + Application.unityVersion + "\n" +
            "Prefab: " + Target + "\n" +
            "Components: PreviewPivot, AssetPoolObject, BoxCollider\n" +
            "Item class: SpecItem (not RadioTransmitter)\n" +
            string.Join("\n", manifest.GetAllAssetBundles()));
        Debug.Log("TSD TABLET BUILD COMPLETE: " + output);
    }

    private static void RenderPreview(GameObject tablet, string path)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            var instance = UnityEngine.Object.Instantiate(tablet);
            instance.transform.rotation = Quaternion.Euler(8, 165, -4);
            preview.AddSingleGO(instance);
            preview.camera.transform.position = new Vector3(0, 0, -0.8f);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.fieldOfView = 30;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 10;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.16f, 0.17f, 0.18f);
            preview.lights[0].intensity = 1.4f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, 200, 0);
            preview.lights[1].intensity = 0.8f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340, 145, 0);
            preview.BeginStaticPreview(new Rect(0, 0, 900, 600));
            preview.camera.Render();
            var texture = preview.EndStaticPreview();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { preview.Cleanup(); }
    }
}

#endif

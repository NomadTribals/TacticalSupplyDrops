using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildTsdFieldTerminal
{
    private const string Root = "Assets/TsdFieldTerminal";
    private const string Materials = Root + "/Materials";
    private const string PrefabPath = Root + "/TsdFieldTerminal.prefab";
    private const string BundleName = "assets/content/items/equipment/tsd_field_terminal/tsd_field_terminal.bundle";
    private static readonly string Output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build");

    [MenuItem("TSD/Generate Field Terminal Model")]
    public static void Build()
    {
        Directory.CreateDirectory(Materials);
        var body = MakeMaterial("Body_Graphite", new Color32(42, 45, 44, 255), 0.18f, 0.72f);
        var rubber = MakeMaterial("Corner_Rubber", new Color32(20, 22, 21, 255), 0.08f, 0.88f);
        var trim = MakeMaterial("Rim_Worn_Gunmetal", new Color32(76, 79, 75, 255), 0.24f, 0.65f);
        var glass = MakeMaterial("Screen_Protective_Glass", new Color32(15, 26, 29, 255), 0.42f, 0.24f);
        var amber = MakeMaterial("TSD_Amber", new Color32(192, 139, 52, 255), 0.18f, 0.64f);
        var fastener = MakeMaterial("Recessed_Fasteners", new Color32(11, 12, 11, 255), 0.1f, 0.85f);

        var root = new GameObject("TSD Field Terminal");
        // Thin landscape housing. The raised gasket and independent corner bumpers
        // create a readable recessed screen and rugged silhouette at inventory scale.
        Part(root, "Main graphite shell", new Vector3(0, 0, 0), new Vector3(1.15f, 0.79f, 0.105f), body);
        Part(root, "Rear battery plate", new Vector3(0, 0, -0.057f), new Vector3(0.99f, 0.63f, 0.018f), trim);
        Part(root, "Screen well shadow", new Vector3(0, 0.025f, 0.056f), new Vector3(0.88f, 0.54f, 0.018f), fastener);
        Part(root, "Screen gasket", new Vector3(0, 0.025f, 0.067f), new Vector3(0.835f, 0.495f, 0.015f), rubber);
        Part(root, "Dark protective display", new Vector3(0, 0.025f, 0.076f), new Vector3(0.79f, 0.45f, 0.008f), glass);
        // Thick rubber corner guards with layered shoulders.
        float[] xs = { -0.535f, 0.535f };
        float[] ys = { -0.355f, 0.355f };
        int i = 0;
        foreach (float x in xs) foreach (float y in ys)
        {
            Part(root, "Impact bumper " + (++i), new Vector3(x, y, 0.06f), new Vector3(0.17f, 0.17f, 0.11f), rubber);
            Part(root, "Bumper shoulder " + i, new Vector3(x * 0.91f, y * 0.91f, 0.112f), new Vector3(0.12f, 0.12f, 0.018f), body);
            Part(root, "Recessed screw " + i, new Vector3(x * 0.91f, y * 0.91f, 0.123f), new Vector3(0.035f, 0.035f, 0.004f), fastener);
        }
        // Tactile controls in a guarded right-side column.
        for (int b = 0; b < 3; b++)
        {
            float y = 0.12f - b * 0.12f;
            Part(root, "Guarded side key " + b, new Vector3(0.592f, y, 0.006f), new Vector3(0.055f, 0.075f, 0.05f), rubber);
            Part(root, "Key face " + b, new Vector3(0.608f, y, 0.008f), new Vector3(0.012f, 0.043f, 0.024f), trim);
        }
        // Small service/status details, kept restrained for Tarkov's worn industrial style.
        Part(root, "Amber TSD ID bar", new Vector3(-0.32f, 0.317f, 0.062f), new Vector3(0.18f, 0.018f, 0.009f), amber);
        Part(root, "Top service strip", new Vector3(0, 0.382f, 0.015f), new Vector3(0.29f, 0.018f, 0.025f), trim);
        Part(root, "Bottom data port", new Vector3(0, -0.389f, -0.005f), new Vector3(0.12f, 0.02f, 0.025f), fastener);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("Tablet model generated. Use the SDK build step to create the runtime loot bundle.");
    }

    private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = Materials + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
        material.name = name;
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Part(GameObject parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
    }
}

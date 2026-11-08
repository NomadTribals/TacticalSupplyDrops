using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildTsdCourierAssets
{
    private const string Root = "Assets/TsdCourier";
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Materials");
        AssetDatabase.Refresh();
        var charcoal = Material("Charcoal", new Color32(29, 32, 34, 255), .35f, .35f);
        var metal = Material("Gunmetal", new Color32(75, 80, 83, 255), .7f, .45f);
        var orange = Material("SafetyOrange", new Color32(237, 103, 22, 255), .15f, .35f);
        var lens = Material("BeaconLens", new Color32(255, 119, 15, 255), .15f, .65f);
        lens.EnableKeyword("_EMISSION");
        lens.SetColor("_EmissionColor", new Color(1f, .19f, .008f) * 2f);
        var badgeMaterial = Material("TsdBadge", Color.white, .1f, .3f);
        badgeMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/TsdLogo.png");
        if (badgeMaterial.mainTexture == null) throw new InvalidOperationException("TSD logo is missing.");

        var beacon = new GameObject("TSD_Courier_Beacon");
        Part(beacon, "RubberMount", PrimitiveType.Cylinder, new Vector3(0,.012f,0), new Vector3(.19f,.012f,.19f), charcoal);
        Part(beacon, "MetalFoot", PrimitiveType.Cylinder, new Vector3(0,.03f,0), new Vector3(.16f,.008f,.16f), metal);
        Part(beacon, "BeaconLens", PrimitiveType.Cylinder, new Vector3(0,.082f,0), new Vector3(.12f,.046f,.12f), lens);
        Part(beacon, "TopCap", PrimitiveType.Cylinder, new Vector3(0,.136f,0), new Vector3(.16f,.008f,.16f), charcoal);
        for (int i=0;i<4;i++)
        {
            float angle=i*Mathf.PI*.5f;
            Part(beacon,"ProtectiveBar"+i,PrimitiveType.Cylinder,new Vector3(Mathf.Cos(angle)*.067f,.083f,Mathf.Sin(angle)*.067f),new Vector3(.009f,.05f,.009f),metal);
            Part(beacon,"MountBolt"+i,PrimitiveType.Cylinder,new Vector3(Mathf.Cos(angle)*.077f,.027f,Mathf.Sin(angle)*.077f),new Vector3(.012f,.006f,.012f),metal);
        }
        PrefabUtility.SaveAsPrefabAsset(beacon,Root+"/TsdBeacon.prefab");
        UnityEngine.Object.DestroyImmediate(beacon);

        // Badge faces local +Z; all dimensions are in metres.
        var badge=new GameObject("TSD_Courier_Badge");
        Part(badge,"OrangeFrame",PrimitiveType.Cube,Vector3.zero,new Vector3(.48f,.48f,.014f),orange);
        Part(badge,"GraphiteInset",PrimitiveType.Cube,new Vector3(0,0,.009f),new Vector3(.454f,.454f,.008f),charcoal);
        var face=Part(badge,"TSD_Artwork",PrimitiveType.Quad,new Vector3(0,0,.014f),new Vector3(.434f,.434f,1),badgeMaterial);
        face.transform.localRotation=Quaternion.Euler(0,180,0);
        PrefabUtility.SaveAsPrefabAsset(badge,Root+"/TsdBadge.prefab");
        UnityEngine.Object.DestroyImmediate(badge);
        AssetDatabase.SaveAssets();
        var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Build/Courier"));
        Directory.CreateDirectory(output);
        var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{
            assetBundleName="tsd_courier_visuals.bundle",assetNames=new[]{Root+"/TsdBeacon.prefab",Root+"/TsdBadge.prefab"}
        }},BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        if(manifest==null)throw new InvalidOperationException("Courier visual bundle build failed.");
        var built = AssetBundle.LoadFromFile(Path.Combine(output, "tsd_courier_visuals.bundle"));
        if (built == null) throw new InvalidOperationException("Courier bundle cannot load in matching Unity runtime.");
        try
        {
            foreach (string asset in new[] { Root + "/TsdBeacon.prefab", Root + "/TsdBadge.prefab" })
            {
                var prefab = built.LoadAsset<GameObject>(asset);
                if (prefab == null || prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException("Courier prefab is missing geometry: " + asset);
                if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Decorative courier prefab contains a collider: " + asset);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null || material.shader == null) throw new InvalidOperationException("Courier material or shader is missing.");
            }
            Debug.Log("TSD COURIER MATCHING-RUNTIME ASSET CHECK PASSED: " + Application.unityVersion);
        }
        finally { built.Unload(false); }
        RenderPreview(output,charcoal,orange);
        Debug.Log("TSD COURIER VISUALS BUILT: "+output);
    }
    private static Material Material(string name,Color color,float metallic,float smoothness)
    {
        string path=Root+"/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
        material.color=color;material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",smoothness);
        EditorUtility.SetDirty(material);return material;
    }
    private static GameObject Part(GameObject root,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(type);part.name=name;part.transform.SetParent(root.transform,false);
        part.transform.localPosition=position;part.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial=material;return part;
    }
    private static void RenderPreview(string output,Material charcoal,Material orange)
    {
        var root=new GameObject("DesignPreview");
        // Placeholder body illustrates colour and accessory placement, not the native crate mesh.
        Part(root,"PreviewBody",PrimitiveType.Cube,Vector3.zero,new Vector3(1.5f,.9f,1.2f),charcoal);
        for(int i=-1;i<=1;i+=2)
            Part(root,"OrangeBand",PrimitiveType.Cube,new Vector3(i*.59f,0,0),new Vector3(.07f,.91f,1.21f),orange);
        var badge=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/TsdBadge.prefab"),root.transform);
        badge.transform.localPosition=new Vector3(0,0,.61f);
        var beacon=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/TsdBeacon.prefab"),root.transform);
        beacon.transform.localPosition=new Vector3(.35f,.455f,0);
        var preview=new PreviewRenderUtility();
        try{
            preview.AddSingleGO(root);preview.camera.transform.position=new Vector3(2f,1.6f,2.7f);
            preview.camera.transform.LookAt(Vector3.zero);preview.camera.fieldOfView=34;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.10f,.115f,.13f);
            preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(40,190,0);
            preview.lights[1].intensity=1f;preview.lights[1].transform.rotation=Quaternion.Euler(320,30,0);
            preview.BeginStaticPreview(new Rect(0,0,1100,800));preview.camera.Render();
            var texture=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(output,"courier-design-preview.png"),texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }finally{preview.Cleanup();}
    }
}

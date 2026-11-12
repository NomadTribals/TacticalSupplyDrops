using System.Collections.Generic;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    internal sealed partial class TsdDroneDelivery
    {
        private Transform cargoHook, hookGate, winchDrum;
        private readonly List<SpriteRenderer> navigationGlows = new List<SpriteRenderer>();
        private readonly List<Renderer> navigationLeds = new List<Renderer>();
        private readonly List<Material> navigationMaterials = new List<Material>();
        private Sprite beaconSprite;
        private Light handoffLamp;

        private void AddBeacon(Vector3 position)
        {
            var ledMaterial = MakeMaterial(new Color(.95f,.3f,.035f));
            ledMaterial.EnableKeyword("_EMISSION"); ledMaterial.SetColor("_EmissionColor",new Color(1.5f,.35f,.025f));
            navigationMaterials.Add(ledMaterial);
            var led = Part(drone.transform,"Orange navigation beacon",position,new Vector3(.04f,.025f,.04f),ledMaterial);
            navigationLeds.Add(led.GetComponent<Renderer>());
            if (beaconSprite == null)
            {
                var texture = new Texture2D(32,32,TextureFormat.RGBA32,false); visualTextures.Add(texture);
                for (int y=0;y<32;y++) for(int x=0;x<32;x++)
                {
                    float radius = new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
                    texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),2)));
                }
                texture.Apply(); beaconSprite = Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);
            }
            var glow = new GameObject("Navigation beacon glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(drone.transform,false); glow.transform.localPosition=position;
            glow.sprite=beaconSprite; glow.color=new Color(1f,.33f,.05f,.7f); navigationGlows.Add(glow);
        }

        private void UpdateBeacons()
        {
            if (handoffLamp != null) handoffLamp.enabled = phase >= 1 && phase <= 3;
            float time = Time.unscaledTime % 1.8f;
            bool pulse = time < .14f || (time > .28f && time < .42f);
            for(int i=0;i<navigationLeds.Count;i++)
                if(navigationLeds[i]!=null) navigationMaterials[i].SetColor("_EmissionColor",pulse ? new Color(2f,.5f,.04f) : new Color(.15f,.025f,0));
            var camera=EFT.CameraControl.CameraManager.instance?.Camera ?? Camera.main;
            if(camera==null)return;
            foreach(var glow in navigationGlows)
            {
                if(glow==null)continue;
                glow.enabled=pulse; glow.transform.rotation=camera.transform.rotation;
                float distance=Vector3.Distance(camera.transform.position,glow.transform.position);
                glow.transform.localScale=Vector3.one*Mathf.Clamp(distance*.003f,.065f,.6f);
            }
        }
        private readonly List<Mesh> visualMeshes = new List<Mesh>();
        private readonly List<Texture2D> visualTextures = new List<Texture2D>();

        private static Transform Rod(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = name; rod.transform.SetParent(parent, false);
            rod.transform.localPosition = (from + to) * .5f;
            rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            rod.transform.localScale = new Vector3(radius * 2, Vector3.Distance(from, to) * .5f, radius * 2);
            rod.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(rod.GetComponent<Collider>());
            return rod.transform;
        }

        private static void Ring(Transform parent, string name, Vector3 center, float radius, float thickness, Material material)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f, b = (i + 1) * Mathf.PI / 8f;
                Rod(parent, name, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius,
                    center + new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0) * radius, thickness, material);
            }
        }

        private GameObject Beveled(Transform parent, string name, Vector3 position, Vector3 size, float radius, float bevel, Material material)
        {
            const int count = 20;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int ring = 0; ring < 4; ring++)
            {
                float inset = ring == 0 || ring == 3 ? bevel : 0;
                float halfX = size.x * .5f - inset, halfZ = size.z * .5f - inset;
                float r = Mathf.Max(.001f, radius - inset);
                float y = ring == 0 ? -size.y * .5f : ring == 1 ? -size.y * .5f + bevel : ring == 2 ? size.y * .5f - bevel : size.y * .5f;
                for (int corner = 0; corner < 4; corner++)
                for (int step = 0; step <= 4; step++)
                {
                    float angle = (corner * 90 + step * 22.5f) * Mathf.Deg2Rad;
                    float cx = corner == 0 || corner == 3 ? halfX - r : -halfX + r;
                    float cz = corner < 2 ? halfZ - r : -halfZ + r;
                    var v = new Vector3(cx + Mathf.Cos(angle) * r, y, cz + Mathf.Sin(angle) * r);
                    vertices.Add(v); uv.Add(new Vector2(v.x / size.x + .5f, (v.z / size.z + .5f) + v.y / size.y));
                }
            }
            for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < count; i++)
            {
                int a = ring * count + i, j = ring * count + (i + 1) % count, b = a + count, k = j + count;
                triangles.AddRange(new[] { a, b, j, j, b, k });
            }
            vertices.Add(Vector3.down * size.y * .5f); uv.Add(Vector2.zero);
            vertices.Add(Vector3.up * size.y * .5f); uv.Add(Vector2.one);
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                triangles.AddRange(new[] { count * 4, i, j, count * 4 + 1, count * 3 + j, count * 3 + i });
            }
            var mesh = new Mesh { name = name + " mesh" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            visualMeshes.Add(mesh);
            var part = new GameObject(name); part.transform.SetParent(parent, false); part.transform.localPosition = position;
            part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private void AddPropeller(Transform parent, Material material)
        {
            var outline=new[] { new Vector2(.035f,-.013f),new Vector2(.12f,-.026f),new Vector2(.235f,-.009f),new Vector2(.25f,.004f),new Vector2(.15f,.026f),new Vector2(.035f,.015f) };
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(int layer=0;layer<2;layer++) foreach(var p in outline) vertices.Add(new Vector3(p.x,layer==0?-.002f:.002f,p.y));
            for(int i=1;i<outline.Length-1;i++) triangles.AddRange(new[]{0,i,i+1,6,6+i+1,6+i});
            for(int i=0;i<outline.Length;i++) { int j=(i+1)%outline.Length;triangles.AddRange(new[]{i,i+6,j,j,i+6,j+6}); }
            var mesh=new Mesh { name="TSD swept propeller blade" };mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();visualMeshes.Add(mesh);
            for(int blade=0;blade<2;blade++)
            {
                var part=new GameObject("Tapered rotor blade");part.transform.SetParent(parent,false);part.transform.localRotation=Quaternion.Euler(0,blade*180,0);
                part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
        }

        private void BuildHardware()
        {
            var graphite = MakeMaterial(new Color(.075f, .085f, .095f));
            var orange = MakeMaterial(new Color(.9f, .3f, .045f));
            var metal = MakeMaterial(new Color(.32f,.34f,.35f)); metal.SetFloat("_Metallic", .45f); metal.SetFloat("_Glossiness", .22f);
            var webbing = MakeMaterial(new Color(.09f,.095f,.08f));
            var shell = MakeMaterial(new Color(.12f,.14f,.155f));
            var panel = MakeMaterial(new Color(.18f,.20f,.215f));
            var rubber = MakeMaterial(new Color(.035f,.04f,.043f));
            var marking = Color.white;
            drone = new GameObject("TSD Logistics delivery aircraft");
            Beveled(drone.transform,"Lower composite airframe",new Vector3(0,-.025f,0),new Vector3(.43f,.11f,.59f),.075f,.022f,graphite);
            Beveled(drone.transform,"Sculpted upper flight shell",new Vector3(0,.035f,0),new Vector3(.35f,.105f,.47f),.075f,.024f,shell);
            Beveled(drone.transform,"Inset avionics cover",new Vector3(0,.089f,0),new Vector3(.22f,.012f,.29f),.038f,.004f,panel);
            Beveled(drone.transform,"Orange identification rail",new Vector3(-.146f,.092f,0),new Vector3(.018f,.012f,.30f),.008f,.003f,orange);
            Beveled(drone.transform,"Orange identification rail",new Vector3(.146f,.092f,0),new Vector3(.018f,.012f,.30f),.008f,.003f,orange);
            Branding(drone.transform,"TSD",new Vector3(0,.098f,-.015f),new Vector3(90,0,0),.022f,marking,.16f,.055f);
            Branding(drone.transform,"LOGISTICS / FLIGHT SYSTEMS",new Vector3(0,.099f,.075f),new Vector3(90,0,0),.0034f,marking,.19f,.012f);
            for(int vent=0;vent<5;vent++)
                Part(drone.transform,"Avionics cooling slot",new Vector3(-.05f+vent*.025f,.096f,-.11f),new Vector3(.011f,.002f,.035f),rubber);
            Branding(drone.transform,"TSD\nLOGISTICS",new Vector3(0,-.082f,-.17f),new Vector3(-90,0,0),.009f,Color.white,.15f,.065f);
            Branding(drone.transform,"TSD",new Vector3(0,.005f,-.297f),Vector3.zero,.01f,Color.white,.11f,.035f);
            Branding(drone.transform,"TSD",new Vector3(0,.005f,.297f),new Vector3(0,180,0),.01f,Color.white,.11f,.035f);
            for (int i = 0; i < 4; i++)
            {
                var offset = new Vector3((i % 2 == 0 ? -1 : 1) * .5f,0,(i < 2 ? -1 : 1) * .4f);
                Rod(drone.transform,"Tubular rotor arm",offset * .2f,offset,.024f,graphite);
                Rod(drone.transform,"Motor housing",offset + Vector3.down * .03f,offset + Vector3.up * .045f,.052f,metal);
                Rod(drone.transform,"Orange motor collar",offset + Vector3.down * .045f,offset + Vector3.down * .025f,.056f,orange);
                var rotor = new GameObject("TSD two-blade propeller").transform;
                rotor.SetParent(drone.transform,false); rotor.localPosition=offset + Vector3.up*.055f;
                AddPropeller(rotor,graphite);
                Rod(rotor,"Propeller spindle",Vector3.down*.008f,Vector3.up*.018f,.018f,metal);
                rotors.Add(rotor);
                AddBeacon(offset + Vector3.down * .056f);
            }
            var handoffLight = new GameObject("White handoff lamp").AddComponent<Light>();
            handoffLamp=handoffLight;
            Beveled(drone.transform,"Handoff lamp housing",new Vector3(0,-.079f,.19f),new Vector3(.1f,.035f,.08f),.015f,.005f,graphite);
            var lens=MakeMaterial(new Color(.9f,.86f,.73f)); lens.EnableKeyword("_EMISSION"); lens.SetColor("_EmissionColor",new Color(.8f,.7f,.5f));
            Part(drone.transform,"Handoff lamp lens",new Vector3(0,-.098f,.19f),new Vector3(.07f,.003f,.05f),lens);
            handoffLight.transform.SetParent(drone.transform,false); handoffLight.transform.localPosition=new Vector3(0,-.105f,.19f);
            handoffLight.transform.localRotation=Quaternion.Euler(90,0,0);
            handoffLight.type=LightType.Spot; handoffLight.spotAngle=50f; handoffLight.color=new Color(1f,.88f,.7f); handoffLight.range=16f;
            handoffLight.intensity=2.8f; handoffLight.enabled=false; handoffLight.shadows=LightShadows.None;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int fore = -1; fore <= 1; fore += 2)
                    Rod(drone.transform,"Landing strut",new Vector3(side*.13f,-.04f,fore*.18f),new Vector3(side*.21f,-.29f,fore*.24f),.012f,metal);
                Rod(drone.transform,"Landing skid",new Vector3(side*.21f,-.29f,-.32f),new Vector3(side*.21f,-.29f,.32f),.015f,graphite);
            }
            Beveled(drone.transform,"Winch motor housing",new Vector3(0,-.105f,0),new Vector3(.18f,.06f,.15f),.02f,.008f,graphite);
            winchDrum = Rod(drone.transform,"Cable drum",new Vector3(-.055f,-.145f,0),new Vector3(.055f,-.145f,0),.025f,webbing);
            Rod(drone.transform,"Winch flange",new Vector3(-.065f,-.145f,0),new Vector3(-.055f,-.145f,0),.035f,metal);
            Rod(drone.transform,"Winch flange",new Vector3(.055f,-.145f,0),new Vector3(.065f,-.145f,0),.035f,metal);
            parcel = new GameObject("TSD premium compact cargo case");
            parcel.transform.SetParent(drone.transform,false); parcel.transform.localPosition = Vector3.down * .85f;
            Beveled(parcel.transform,"Molded equipment case",new Vector3(0,.132f,0),new Vector3(.46f,.244f,.34f),.032f,.012f,shell);
            Beveled(parcel.transform,"Orange lid gasket",new Vector3(0,.256f,0),new Vector3(.451f,.006f,.331f),.029f,.0015f,orange);
            Beveled(parcel.transform,"Fitted protective lid",new Vector3(0,.279f,0),new Vector3(.46f,.042f,.34f),.032f,.008f,graphite);
            Beveled(parcel.transform,"Recessed lid panel",new Vector3(0,.301f,0),new Vector3(.29f,.003f,.205f),.02f,.001f,panel);
            Branding(parcel.transform,"TSD",new Vector3(0,.304f,-.02f),new Vector3(90,0,0),.022f,marking,.16f,.055f);
            Branding(parcel.transform,"LOGISTICS / SECURE CARGO",new Vector3(0,.305f,.055f),new Vector3(90,0,0),.0035f,marking,.24f,.012f);
            for(int side=-1;side<=1;side+=2)
            {
                Beveled(parcel.transform,"Case identification plate",new Vector3(0,.15f,side*.172f),new Vector3(.25f,.082f,.004f),.001f,.0005f,graphite);
                var facing=new Vector3(0,side<0?0:180,0);
                Branding(parcel.transform,"TSD LOGISTICS",new Vector3(0,.16f,side*.176f),facing,.010f,marking,.22f,.027f);
                Branding(parcel.transform,"COMPACT SUPPLY / FIELD SERIES",new Vector3(0,.13f,side*.176f),facing,.0028f,new Color(.9f,.4f,.1f),.22f,.009f);
                for(int corner=-1;corner<=1;corner+=2)
                {
                    Beveled(parcel.transform,"Reinforced corner rib",new Vector3(corner*.207f,.133f,side*.145f),new Vector3(.035f,.224f,.035f),.012f,.005f,graphite);
                    Beveled(parcel.transform,"Rubber case foot",new Vector3(corner*.175f,.009f,side*.115f),new Vector3(.065f,.018f,.055f),.013f,.004f,rubber);
                }
            }
            for(int xSide=-1;xSide<=1;xSide+=2)
            {
                float x=xSide*.155f;
                Part(parcel.transform,"Latch keeper",new Vector3(x,.267f,-.175f),new Vector3(.038f,.043f,.012f),metal);
                Beveled(parcel.transform,"Orange locking latch",new Vector3(x,.243f,-.183f),new Vector3(.027f,.035f,.009f),.003f,.002f,orange);
                Rod(parcel.transform,"Latch hinge pin",new Vector3(x-.02f,.258f,-.182f),new Vector3(x+.02f,.258f,-.182f),.004f,metal);
                Rod(parcel.transform,"Rear lid hinge",new Vector3(x-.028f,.265f,.174f),new Vector3(x+.028f,.265f,.174f),.008f,metal);
                float gripX=xSide*.238f;
                Rod(parcel.transform,"Handle bracket",new Vector3(gripX,.12f,-.07f),new Vector3(gripX,.17f,-.07f),.008f,metal);
                Rod(parcel.transform,"Handle bracket",new Vector3(gripX,.12f,.07f),new Vector3(gripX,.17f,.07f),.008f,metal);
                Rod(parcel.transform,"Rubber carry handle",new Vector3(gripX,.17f,-.07f),new Vector3(gripX,.17f,.07f),.011f,rubber);
                Part(parcel.transform,"Lifting harness top",new Vector3(x,.307f,0),new Vector3(.018f,.003f,.34f),webbing);
                Part(parcel.transform,"Lifting harness underside",new Vector3(x,.012f,0),new Vector3(.018f,.003f,.34f),webbing);
                for(int zSide=-1;zSide<=1;zSide+=2)
                {
                    Part(parcel.transform,"Lifting harness side",new Vector3(x,.158f,zSide*.174f),new Vector3(.018f,.29f,.003f),webbing);
                    Rod(parcel.transform,"Lifting bridle",new Vector3(x,.31f,zSide*.13f),new Vector3(0,.435f,0),.005f,webbing);
                }
            }
            Ring(parcel.transform,"Steel lifting eye",Vector3.up*.46f,.027f,.004f,metal);
            cargoHook = new GameObject("Remote-release cargo hook").transform; cargoHook.SetParent(drone.transform,false);
            var points = new[] { new Vector3(0,0,0), new Vector3(0,-.018f,0), new Vector3(-.023f,-.035f,0),new Vector3(-.027f,-.062f,0),new Vector3(-.012f,-.081f,0),new Vector3(.013f,-.079f,0),new Vector3(.027f,-.06f,0),new Vector3(.025f,-.045f,0) };
            for (int i=1;i<points.Length;i++) Rod(cargoHook,"Forged hook",points[i-1],points[i],.005f,metal);
            hookGate = new GameObject("Release gate pivot").transform; hookGate.SetParent(cargoHook,false); hookGate.localPosition = new Vector3(.025f,-.045f,0);
            Rod(hookGate,"Safety gate",Vector3.zero,new Vector3(-.025f,.027f,0),.003f,orange);
            tether = drone.AddComponent<LineRenderer>();
            var cableMaterial = new Material(Shader.Find("Sprites/Default")); materials.Add(cableMaterial);
            tether.sharedMaterial=cableMaterial; tether.useWorldSpace=true; tether.positionCount=2;
            tether.startWidth=tether.endWidth=.008f; tether.startColor=tether.endColor=new Color(.15f,.15f,.13f);
            tether.SetPosition(0,Vector3.zero); tether.SetPosition(1,Vector3.zero);
        }
    }
}

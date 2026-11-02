using System.IO;
using SumoGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Generates the game scene from code. Run from the Sumo menu, or with -executeMethod in batch mode.</summary>
public static class SumoSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Sumo.unity";

    const string ArenaModel = "Assets/Art/Arena/SumoArena.fbx";
    const string MaterialFolder = "Assets/Art/Materials";
    const string RopeMeshPath = "Assets/Art/DohyoRope.asset";
    const float ArenaScale = 1.75f;
    const float DohyoHeight = 1.1f;
    const float RopeThickness = 0.14f;

    static Material clay, wood, plaster, cushion, roof, rope, line;

    [MenuItem("Sumo/Rebuild Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        CreateMaterials();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rules = new SumoRules();

        BuildArena();
        BuildRing(rules.RingRadius);
        BuildLighting();

        var skin = Lit("Skin", new Color(0.87f, 0.64f, 0.5f), 0.55f);
        var hair = Lit("Hair", new Color(0.06f, 0.05f, 0.05f), 0.6f);
        var eye = Lit("Eye", new Color(0.03f, 0.03f, 0.03f), 0.8f);
        var red = Lit("MawashiRed", new Color(0.78f, 0.13f, 0.11f), 0.35f);
        var blue = Lit("MawashiBlue", new Color(0.12f, 0.3f, 0.75f), 0.35f);

        var wrestlers = new GameObject("Wrestlers").transform;
        var p1 = WrestlerPuppetBuilder.Build("P1", wrestlers, new WrestlerPuppetBuilder.Palette { Skin = skin, Hair = hair, Eye = eye, Mawashi = red });
        var p2 = WrestlerPuppetBuilder.Build("P2", wrestlers, new WrestlerPuppetBuilder.Palette { Skin = skin, Hair = hair, Eye = eye, Mawashi = blue });
        p1.RingRadius = p2.RingRadius = rules.RingRadius;
        p1.transform.SetPositionAndRotation(new Vector3(-rules.StartDistance * 0.5f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
        p2.transform.SetPositionAndRotation(new Vector3(rules.StartDistance * 0.5f, 0f, 0f), Quaternion.LookRotation(Vector3.left));

        var p1Camera = BuildCamera("P1 Camera");
        var p2Camera = BuildCamera("P2 Camera");
        p1Camera.tag = "MainCamera";
        p1Camera.gameObject.AddComponent<AudioListener>();
        p1Camera.ShowWideShot();

        var hud = new GameObject("HUD").AddComponent<SumoHud>();
        var menu = hud.gameObject.AddComponent<SumoMenu>();

        var effects = new GameObject("Effects", typeof(ParticleSystem)).AddComponent<ImpactEffects>();
        effects.Material = Particle("Spark");
        var sounds = new GameObject("Sounds").AddComponent<SumoAudio>();

        var controller = new GameObject("Game").AddComponent<SumoGameController>();
        controller.P1View = p1;
        controller.P2View = p2;
        controller.P1Camera = p1Camera;
        controller.P2Camera = p2Camera;
        controller.Hud = hud;
        controller.Menu = menu;
        controller.Effects = effects;
        controller.Sounds = sounds;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("SUMO scene built");
    }

    static SumoCamera BuildCamera(string name)
    {
        var cameraObject = new GameObject(name);
        var camera = cameraObject.AddComponent<Camera>();
        camera.farClipPlane = 200f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.04f, 0.04f);
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        return cameraObject.AddComponent<SumoCamera>();
    }

    [MenuItem("Sumo/Build Linux Game")]
    public static void BuildGame()
    {
        PlayerSettings.productName = "Sumo Game";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/SumoGame.x86_64",
            target = BuildTarget.StandaloneLinux64,
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception($"Build failed with {report.summary.totalErrors} errors");
        Debug.Log($"SUMO build done: {report.summary.totalSize / 1048576} MB");
    }

    static void CreateMaterials()
    {
        clay = Lit("DohyoClay", new Color(0.8f, 0.63f, 0.42f), 0.15f);
        wood = Lit("Wood", new Color(0.38f, 0.23f, 0.14f), 0.3f);
        plaster = Lit("Plaster", new Color(0.46f, 0.4f, 0.36f), 0.2f);
        cushion = Lit("Cushion", new Color(0.5f, 0.12f, 0.2f), 0.2f);
        roof = Lit("Roof", new Color(0.2f, 0.17f, 0.16f), 0.3f);
        rope = Lit("Rope", new Color(0.87f, 0.75f, 0.47f), 0.2f);
        line = Lit("StartLine", new Color(0.96f, 0.95f, 0.91f), 0.3f);
    }

    static Material Lit(string name, Color color, float smoothness)
    {
        var path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material Particle(string name)
    {
        var path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void BuildArena()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaModel);
        var arena = (GameObject)PrefabUtility.InstantiatePrefab(model);
        arena.name = "Arena";
        arena.transform.localScale = Vector3.one * ArenaScale;
        arena.transform.position = Vector3.down * (DohyoHeight * ArenaScale);

        foreach (var renderer in arena.GetComponentsInChildren<Renderer>())
        {
            var material = MaterialFor(renderer.name);
            var slots = new Material[renderer.sharedMaterials.Length];
            for (var i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            renderer.shadowCastingMode = renderer.name == "Dohyo" ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }

    static Material MaterialFor(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower == "dohyo") return clay;
        if (lower.StartsWith("cushion")) return cushion;
        if (lower.StartsWith("suspending") || lower.StartsWith("roof") || lower.StartsWith("chain")) return roof;
        if (lower.StartsWith("bench") || lower.StartsWith("stairs") || lower.StartsWith("support") || lower.StartsWith("guard")) return wood;
        return plaster;
    }

    static void BuildRing(float radius)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RopeMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "DohyoRope" };
            AssetDatabase.CreateAsset(mesh, RopeMeshPath);
        }
        FillTorus(mesh, radius, RopeThickness, 96, 10);
        EditorUtility.SetDirty(mesh);

        var ring = new GameObject("Rope");
        ring.transform.position = Vector3.up * (RopeThickness * 0.35f);
        ring.AddComponent<MeshFilter>().sharedMesh = mesh;
        ring.AddComponent<MeshRenderer>().sharedMaterial = rope;

        StartLine("StartLineL", -0.85f);
        StartLine("StartLineR", 0.85f);
    }

    static void StartLine(string name, float x)
    {
        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = name;
        Object.DestroyImmediate(mark.GetComponent<Collider>());
        mark.transform.position = new Vector3(x, 0.005f, 0f);
        mark.transform.localScale = new Vector3(0.09f, 0.01f, 0.95f);
        mark.GetComponent<Renderer>().sharedMaterial = line;
    }

    static void FillTorus(Mesh mesh, float radius, float tube, int rings, int sides)
    {
        var vertices = new Vector3[(rings + 1) * (sides + 1)];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[rings * sides * 6];

        for (var r = 0; r <= rings; r++)
        {
            var around = Quaternion.Euler(0f, 360f * r / rings, 0f);
            for (var s = 0; s <= sides; s++)
            {
                var angle = 2f * Mathf.PI * s / sides;
                var normal = around * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var i = r * (sides + 1) + s;
                vertices[i] = around * new Vector3(radius, 0f, 0f) + normal * tube;
                normals[i] = normal;
            }
        }

        var t = 0;
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < sides; s++)
            {
                var a = r * (sides + 1) + s;
                var b = a + sides + 1;
                triangles[t++] = a;
                triangles[t++] = b;
                triangles[t++] = a + 1;
                triangles[t++] = b;
                triangles[t++] = b + 1;
                triangles[t++] = a + 1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    static void BuildLighting()
    {
        // The stadium is fully roofed, so the ring is lit from inside rather than by a sun.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.56f, 0.5f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.4f, 0.36f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.22f, 0.2f);
        RenderSettings.skybox = null;

        var spot = new GameObject("RingSpot").AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.transform.SetPositionAndRotation(new Vector3(0f, 8.2f, -0.5f), Quaternion.Euler(90f, 0f, 0f));
        spot.range = 30f;
        spot.spotAngle = 100f;
        spot.innerSpotAngle = 60f;
        spot.intensity = 90f;
        spot.color = new Color(1f, 0.95f, 0.86f);
        spot.shadows = LightShadows.Soft;
    }
}

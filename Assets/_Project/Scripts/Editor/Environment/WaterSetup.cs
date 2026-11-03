using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Water for Safe Haven (see Docs/Water.md): generates the "Safe Haven/Water" textures, materials (Still,
/// River, Waterfall), spray material and the Water Splash / Waterfall Plunge prefabs in Art/Generated/Water, creates
/// ponds, river sections and waterfalls at the Scene view's centre, turns selected meshes into water, carves a bed
/// into the terrain under a water surface and gives the player prefab a WaterInteractor. Running it again keeps your
/// material tuning; only missing assets are created.</summary>
public static class WaterSetup
{
    public const string Folder = "Assets/_Project/Art/Generated/Water";
    public const string ShaderName = "Safe Haven/Water";
    public const string SprayShaderName = "Safe Haven/Water Spray";
    public const string PlayerPrefab = GrassSetup.PlayerPrefab;

    public sealed class Set
    {
        public Material Still, River, Fall, Spray, FootstepSpray, FootstepRing;
        public GameObject Splash, FootstepSplash, Plunge;
    }

    [MenuItem("Safe Haven/World/Water/Create Pond")]
    private static void CreatePondMenu()
    {
        Set set = EnsureAssets();
        Select(CreateWater("Pond", set.Still, PlacementPoint(), new Vector3(0.8f, 1f, 0.8f)));
        LogPlayer(set);
    }

    [MenuItem("Safe Haven/World/Water/Create River Section")]
    private static void CreateRiverMenu()
    {
        Set set = EnsureAssets();
        Select(CreateWater("River", set.River, PlacementPoint(), new Vector3(1.6f, 1f, 0.4f)));
        LogPlayer(set);
    }

    [MenuItem("Safe Haven/World/Water/Create Waterfall...")]
    private static void CreateWaterfallMenu() =>
        ScriptableWizard.DisplayWizard<WaterfallWizard>("Create Waterfall", "Create");

    [MenuItem("Safe Haven/World/Water/Make Selected Still Water")]
    private static void MakeStillMenu()
    {
        Set set = EnsureAssets();
        Debug.Log($"Water: {MakeWater(Selection.gameObjects, set.Still)} object(s) are now still water.");
        LogPlayer(set);
    }

    [MenuItem("Safe Haven/World/Water/Make Selected Flowing Water")]
    private static void MakeRiverMenu()
    {
        Set set = EnsureAssets();
        Debug.Log($"Water: {MakeWater(Selection.gameObjects, set.River)} object(s) now flow along their local X " +
                  "(set Flow Direction on the Water River material to change it).");
        LogPlayer(set);
    }

    [MenuItem("Safe Haven/World/Water/Make Selected Still Water", true)]
    [MenuItem("Safe Haven/World/Water/Make Selected Flowing Water", true)]
    private static bool HasMeshSelection()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponent<MeshFilter>() != null)
            {
                return true;
            }
        }

        return false;
    }

    [MenuItem("Safe Haven/World/Water/Make Selected Disturb Water")]
    private static void AddInteractorMenu()
    {
        Set set = EnsureAssets();
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponent<WaterInteractor>() == null)
            {
                Undo.AddComponent<WaterInteractor>(go).Splash = set.Splash;
            }
        }
    }

    [MenuItem("Safe Haven/World/Water/Carve Terrain Under Selected Water...")]
    private static void CarveMenu() =>
        ScriptableWizard.DisplayWizard<CarveWizard>("Carve Terrain Under Water", "Carve");

    private sealed class WaterfallWizard : ScriptableWizard
    {
        [Min(0.2f)] public float width = 3f;
        [Tooltip("Drop from the lip to the pool (metres).")]
        [Min(0.2f)] public float height = 4f;
        [Tooltip("Flat run of water before the edge, where it joins the river above.")]
        [Min(0f)] public float lip = 1f;
        [Tooltip("How fast the water leaves the edge: 0 runs down the cliff, higher arcs out.")]
        [Range(0f, 4f)] public float throwSpeed = 1.2f;

        private void OnWizardCreate()
        {
            Set set = EnsureAssets();
            Select(CreateWaterfall(PlacementPoint() + Vector3.up * height, width, height, lip, throwSpeed, set));
            LogPlayer(set);
        }
    }

    private sealed class CarveWizard : ScriptableWizard
    {
        [Tooltip("Depth of the bed below the water surface in the middle (metres).")]
        [Min(0.1f)] public float depth = 1.2f;
        [Tooltip("Width of the sloping bank inside the water's edge (metres).")]
        [Min(0.1f)] public float bank = 1.5f;
        [Tooltip("Optional: one of the terrain's layers to paint the bed with (gravel, stone, mud).")]
        public TerrainLayer bedLayer;

        private void OnWizardUpdate()
        {
            isValid = Selection.GetFiltered<WaterSurface>(SelectionMode.Editable).Length > 0;
            errorString = isValid ? "" : "Select one or more water surfaces (objects with a Water Surface) first.";
        }

        private void OnWizardCreate()
        {
            int carved = 0;
            foreach (WaterSurface water in Selection.GetFiltered<WaterSurface>(SelectionMode.Editable))
            {
                foreach (Terrain terrain in Terrain.activeTerrains)
                {
                    carved += Carve(terrain, water.transform, depth, bank, bedLayer);
                }
            }

            ClearGrassUnderWater();
            Debug.Log($"Water: carved {carved} terrain height sample(s) under the selected water.");
        }
    }

    private static void Select(GameObject go)
    {
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    private static void LogPlayer(Set set)
    {
        if (EnsurePlayerInteractor(PlayerPrefab, set.Splash, set.FootstepSplash))
        {
            Debug.Log("Water: the player makes ripples, wake foam and splashes (Water Interactor on PlayerArmature).");
        }
    }

    /// <summary>Scene view centre, dropped onto the terrain.</summary>
    private static Vector3 PlacementPoint()
    {
        Vector3 point = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point;
    }

    /// <summary>Creates what is missing in <see cref="Folder"/> and returns the set.</summary>
    public static Set EnsureAssets()
    {
        Directory.CreateDirectory(Folder);
        Texture2D normal = EnsureTexture("Water Normal.png", () => NormalPixels(512), importer =>
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
        });
        Texture2D foam = EnsureTexture("Water Foam.png", () => FoamPixels(256), Linear);
        Texture2D caustics = EnsureTexture("Water Caustics.png", () => CausticsPixels(256), Linear);
        Texture2D droplet = EnsureTexture("Water Droplet.png", () => DropletPixels(64), importer =>
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        });
        Texture2D ring = EnsureTexture("Water Ring.png", () => RingPixels(128), importer =>
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        });

        var set = new Set
        {
            Still = EnsureMaterial("Water Still", ShaderName, material => SetWater(material, normal, foam, caustics)),
            River = EnsureMaterial("Water River", ShaderName, material =>
            {
                SetWater(material, normal, foam, caustics);
                material.SetVector("_FlowDirection", new Vector4(1f, 0f, 0f, 0f));
                material.SetFloat("_FlowSpeed", 1.2f);
                material.SetFloat("_NormalStrength", 0.6f);
                material.SetFloat("_Clarity", 1.6f);
            }),
            Fall = EnsureMaterial("Water Waterfall", ShaderName, material =>
            {
                SetWater(material, normal, foam, caustics);
                material.SetVector("_FlowDirection", new Vector4(0f, 1f, 0f, 0f));
                material.SetFloat("_FlowSpeed", 1.5f);
                material.SetFloat("_FallSpeed", 7f);
                material.SetFloat("_FallFoam", 0.9f);
                material.SetFloat("_VertexAlpha", 1f);
                material.SetFloat("_SteepFaces", 1f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetFloat("_ShoreFoam", 0.2f);
            }),
            Spray = EnsureMaterial("Water Spray", SprayShaderName, material => material.SetTexture("_BaseMap", droplet)),
            // Lies on the puddle, so it must not fade out where it meets the ground as the spray does; brighter than
            // the spray so it still reads against grey, sky-reflecting puddles under rain clouds.
            FootstepSpray = EnsureMaterial("Footstep Spray", SprayShaderName, material =>
            {
                material.SetTexture("_BaseMap", droplet);
                material.SetColor("_BaseColor", new Color(1.15f, 1.18f, 1.2f, 1f));
                material.SetFloat("_Softness", 0.01f);
            }),
            FootstepRing = EnsureMaterial("Footstep Ring", SprayShaderName, material =>
            {
                material.SetTexture("_BaseMap", ring);
                material.SetColor("_BaseColor", new Color(1.2f, 1.22f, 1.25f, 1f));
                material.SetFloat("_Softness", 0.01f);
            }),
        };
        set.Splash = EnsurePrefab("Water Splash", () => BuildSplash(set.Spray));
        set.FootstepSplash = EnsurePrefab("Footstep Splash", () => BuildFootstepSplash(set.FootstepRing, set.FootstepSpray));
        set.Plunge = EnsurePrefab("Waterfall Plunge", () => BuildPlunge(set.Spray));
        return set;
    }

    private static void Linear(TextureImporter importer)
    {
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
    }

    private static void SetWater(Material material, Texture normal, Texture foam, Texture caustics)
    {
        material.SetTexture("_NormalMap", normal);
        material.SetTexture("_FoamMap", foam);
        material.SetTexture("_CausticsMap", caustics);
    }

    private static Texture2D EnsureTexture(string file, Func<Texture2D> pixels, Action<TextureImporter> setup)
    {
        string path = $"{Folder}/{file}";
        if (!File.Exists(path))
        {
            Texture2D texture = pixels();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            setup(importer);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material EnsureMaterial(string name, string shaderName, Action<Material> setup)
    {
        string path = $"{Folder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName)) { name = name, enableInstancing = true };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static GameObject EnsurePrefab(string name, Func<GameObject> build)
    {
        string path = $"{Folder}/{name}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            GameObject go = build();
            try
            {
                prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        return prefab;
    }

    /// <summary>A pond or river: a 10 x 10 m plane (scaled), no collider or shadows, with a WaterSurface.</summary>
    public static GameObject CreateWater(string name, Material material, Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = position + Vector3.up * 0.02f;
        go.transform.localScale = scale;
        MakeWater(new[] { go }, material);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    /// <summary>Gives the objects the water material and a WaterSurface; returns how many changed.</summary>
    public static int MakeWater(IEnumerable<GameObject> objects, Material material)
    {
        int changed = 0;
        foreach (GameObject go in objects)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null || go.GetComponent<MeshFilter>() == null)
            {
                continue;
            }

            Undo.RecordObject(renderer, "Make Water");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (go.GetComponent<WaterSurface>() == null)
            {
                Undo.AddComponent<WaterSurface>(go);
            }

            changed++;
        }

        if (changed > 0)
        {
            ClearGrassUnderWater();
        }

        return changed;
    }

    /// <summary>Water removes grass and other details under it (and hides any painted there later).</summary>
    public static void ClearGrassUnderWater()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            GrassSetup.ClearUnderWater(terrain);
        }
    }

    /// <summary>A waterfall whose lip starts at <paramref name="top"/>, with its plunge (foam, ripples, mist, spray)
    /// and a still pool where it lands. Turn the root so its blue (Z) arrow points the way the water falls.</summary>
    public static GameObject CreateWaterfall(Vector3 top, float width, float height, float lip, float throwSpeed, Set set)
    {
        var root = new GameObject("Waterfall", typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.position = top + Vector3.forward * lip - Vector3.up * height; // the mesh rises from the pool
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = set.Fall;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var fall = root.AddComponent<Waterfall>();
        var plunge = (GameObject)PrefabUtility.InstantiatePrefab(set.Plunge, root.transform);
        fall.Plunge = plunge.transform;
        fall.Configure(width, height, lip, throwSpeed);
        FitPlunge(plunge, width);
        float poolSize = Mathf.Max(width * 2f, 5f);
        GameObject pool = CreateWater("Waterfall Pool", set.Still, Vector3.zero, new Vector3(poolSize / 10f, 1f, poolSize / 10f));
        Undo.SetTransformParent(pool.transform, root.transform, "Create Waterfall");
        pool.transform.localPosition = fall.Landing + new Vector3(0f, 0.02f, poolSize * 0.3f);
        pool.transform.localRotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(root, "Create Waterfall");
        return root;
    }

    /// <summary>Sizes the plunge's churn, mist and spray to the waterfall's width.</summary>
    public static void FitPlunge(GameObject plunge, float width)
    {
        var interactor = plunge.GetComponent<WaterInteractor>();
        if (interactor != null)
        {
            interactor.Radius = Mathf.Max(width * 0.6f, 0.8f);
        }

        foreach (ParticleSystem system in plunge.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.scale = new Vector3(width, shape.scale.y, shape.scale.z);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = system.name == "Mist" ? 4f * width : 14f * width;
        }
    }

    public static bool EnsurePlayerInteractor(string prefabPath, GameObject splash, GameObject footstepSplash = null)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            return false;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (contents.GetComponent<WaterInteractor>() == null)
            {
                var interactor = contents.AddComponent<WaterInteractor>();
                interactor.Radius = 0.45f;
                interactor.Offset = new Vector3(0f, 0.15f, 0f);
                interactor.Foam = 0.6f;
                interactor.Splash = splash;
                interactor.FootstepSplash = footstepSplash;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            else if (footstepSplash != null && contents.GetComponent<WaterInteractor>().FootstepSplash == null)
            {
                contents.GetComponent<WaterInteractor>().FootstepSplash = footstepSplash;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return true;
    }

    /// <summary>Lowers the terrain under a water object (its mesh bounds, in its own space) into a bed
    /// <paramref name="depth"/> metres below the surface with banks <paramref name="bank"/> metres wide inside the
    /// edge; outside the water nothing changes. When the bed would go below the terrain's lowest possible height, the
    /// whole terrain is lowered and its heights raised by the same amount first, so the ground stays where it is.
    /// With <paramref name="bedLayer"/> (one of the terrain's layers) the bed is painted with it, fading out up the
    /// banks. With <paramref name="oval"/> the bed is a slightly uneven oval inside the bounds (a natural pond or tarn:
    /// lay the water a little below the ground and only the oval shows). Undoable. Returns the number of height samples
    /// changed.</summary>
    /// <summary>How far an oval pond's shore lies out from its plain oval, at this angle: the shore isn't a perfect oval.</summary>
    public static float ShoreWobble(float angle) => 1f + 0.07f * Mathf.Sin(angle * 3f + 1.3f) + 0.04f * Mathf.Sin(angle * 7f);

    /// <summary>Distance in metres from a pond's shore as <see cref="Carve"/> digs it: negative in the water.
    /// <paramref name="local"/> is its mesh's bounds.</summary>
    public static float ShoreDistance(Transform water, Bounds local, bool oval, float worldX, float worldZ)
    {
        Vector3 scale = water.lossyScale;
        Vector3 p = water.InverseTransformPoint(new Vector3(worldX, water.position.y, worldZ)) - local.center;
        if (oval)
        {
            float ex = local.extents.x * Mathf.Abs(scale.x), ez = local.extents.z * Mathf.Abs(scale.z);
            float nx = p.x * Mathf.Abs(scale.x) / ex, nz = p.z * Mathf.Abs(scale.z) / ez;
            return (Mathf.Sqrt(nx * nx + nz * nz) / ShoreWobble(Mathf.Atan2(nz, nx)) - 1f) * Mathf.Min(ex, ez);
        }

        float dx = (Mathf.Abs(p.x) - local.extents.x) * Mathf.Abs(scale.x);
        float dz = (Mathf.Abs(p.z) - local.extents.z) * Mathf.Abs(scale.z);
        return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dz), 0f);
    }

    public static int Carve(Terrain terrain, Transform water, float depth, float bank, TerrainLayer bedLayer = null, bool oval = false)
    {
        var filter = water.GetComponent<MeshFilter>();
        if (terrain == null || filter == null || filter.sharedMesh == null)
        {
            return 0;
        }

        TerrainData data = terrain.terrainData;
        Bounds local = filter.sharedMesh.bounds;
        float top = water.TransformPoint(new Vector3(local.center.x, local.max.y, local.center.z)).y;
        float floor = top - depth;
        Vector3 scale = water.lossyScale;
        Vector2 extent = new Vector2(Mathf.Abs(local.extents.x * scale.x), Mathf.Abs(local.extents.z * scale.z));
        float inner = Mathf.Min(bank, Mathf.Min(extent.x, extent.y) * 0.9f);
        Bounds world = water.GetComponent<Renderer>() != null ? water.GetComponent<Renderer>().bounds : new Bounds(water.position, Vector3.one);
        int resolution = data.heightmapResolution;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((world.min.x - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((world.max.x - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((world.min.z - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((world.max.z - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
        if (x1 <= x0 || z1 <= z0)
        {
            return 0;
        }

        var undo = new List<Object> { data, terrain.transform };
        undo.AddRange(data.alphamapTextures);
        Undo.RegisterCompleteObjectUndo(undo.ToArray(), "Carve Terrain Under Water");
        if (floor < origin.y + 0.05f)
        {
            Lower(terrain, origin.y + 0.05f - floor + 0.5f);
            origin = terrain.transform.position;
            size = data.size;
        }

        // Distance from the water's edge in metres: negative inside, and 0..1 up the bank (0 = the deep middle).
        float Signed(float worldX, float worldZ) => ShoreDistance(water, local, oval, worldX, worldZ);

        float Bank(float signed) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((signed + inner) / inner));

        int width = x1 - x0 + 1, length = z1 - z0 + 1;
        float[,] heights = data.GetHeights(x0, z0, width, length);
        int changed = 0;
        for (int z = 0; z < length; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float signed = Signed(origin.x + (x0 + x) / (float)(resolution - 1) * size.x,
                                      origin.z + (z0 + z) / (float)(resolution - 1) * size.z);
                if (signed >= 0f)
                {
                    continue;
                }

                float ground = origin.y + heights[z, x] * size.y;
                float bed = Mathf.Lerp(floor, Mathf.Max(ground, top), Bank(signed));
                if (bed < ground - 1e-4f)
                {
                    heights[z, x] = Mathf.Clamp01((bed - origin.y) / size.y);
                    changed++;
                }
            }
        }

        data.SetHeights(x0, z0, heights);
        int layer = bedLayer != null ? Array.IndexOf(data.terrainLayers, bedLayer) : -1;
        if (layer >= 0)
        {
            PaintBed(data, origin, world, layer, (worldX, worldZ) =>
            {
                float signed = Signed(worldX, worldZ);
                return signed >= 0f ? 0f : 1f - Bank(signed) * 0.7f;
            });
        }

        EditorUtility.SetDirty(data);
        return changed;
    }

    /// <summary>Blends terrain layer <paramref name="layer"/> in by <paramref name="weight"/>(world x, z) over the
    /// alphamap texels inside <paramref name="world"/>, keeping each texel's weights summing to 1.</summary>
    private static void PaintBed(TerrainData data, Vector3 origin, Bounds world, int layer, Func<float, float, float> weight)
    {
        int resolution = data.alphamapResolution;
        Vector3 size = data.size;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((world.min.x - origin.x) / size.x * resolution), 0, resolution - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((world.max.x - origin.x) / size.x * resolution), 0, resolution - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((world.min.z - origin.z) / size.z * resolution), 0, resolution - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((world.max.z - origin.z) / size.z * resolution), 0, resolution - 1);
        int width = x1 - x0 + 1, length = z1 - z0 + 1;
        float[,,] maps = data.GetAlphamaps(x0, z0, width, length);
        int layers = maps.GetLength(2);
        for (int z = 0; z < length; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float w = weight(origin.x + (x0 + x + 0.5f) / resolution * size.x, origin.z + (z0 + z + 0.5f) / resolution * size.z);
                if (w <= 0f)
                {
                    continue;
                }

                for (int l = 0; l < layers; l++)
                {
                    maps[z, x, l] = l == layer ? Mathf.Lerp(maps[z, x, l], 1f, w) : maps[z, x, l] * (1f - w);
                }
            }
        }

        data.SetAlphamaps(x0, z0, maps);
    }

    /// <summary>Moves the terrain down by <paramref name="amount"/> metres and raises its heights (and trees) by the
    /// same, growing its height range if needed, so the ground surface stays in place.</summary>
    private static void Lower(Terrain terrain, float amount)
    {
        TerrainData data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        float oldHeight = data.size.y;
        float highest = 0f;
        foreach (float h in heights)
        {
            highest = Mathf.Max(highest, h);
        }

        float newHeight = Mathf.Max(oldHeight, highest * oldHeight + amount + 0.5f);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                heights[z, x] = (heights[z, x] * oldHeight + amount) / newHeight;
            }
        }

        TreeInstance[] trees = data.treeInstances;
        for (int i = 0; i < trees.Length; i++)
        {
            trees[i].position.y = (trees[i].position.y * oldHeight + amount) / newHeight;
        }

        data.size = new Vector3(data.size.x, newHeight, data.size.z);
        data.SetHeights(0, 0, heights);
        data.treeInstances = trees;
        terrain.transform.position -= Vector3.up * amount;
    }

    // ---------------------------------------------------------------- particles

    private static GameObject BuildSplash(Material spray)
    {
        // The puff lives longest, so it is the root: its Stop Action removes the whole splash.
        var root = new GameObject("Water Splash");
        ParticleSystem puff = AddParticles(root, spray, loop: false, lifetime: (0.8f, 1.3f), speed: (0.3f, 0.9f),
            size: (0.5f, 0.9f), gravity: -0.03f, alpha: 0.35f, max: 10, grow: true);
        ParticleSystem.MainModule main = puff.main;
        main.duration = 0.3f;
        main.stopAction = ParticleSystemStopAction.Destroy;
        Burst(puff, 5);
        Shape(puff, ParticleSystemShapeType.Circle, 0.25f);

        var droplets = new GameObject("Droplets");
        droplets.transform.SetParent(root.transform, false);
        ParticleSystem drops = AddParticles(droplets, spray, loop: false, lifetime: (0.45f, 0.8f), speed: (2.5f, 4.5f),
            size: (0.05f, 0.12f), gravity: 1f, alpha: 0.9f, max: 60, grow: false);
        ParticleSystem.MainModule dropsMain = drops.main;
        dropsMain.duration = 0.3f;
        Burst(drops, 30);
        Shape(drops, ParticleSystemShapeType.Cone, 0.15f, 25f);
        return root;
    }

    // A foot coming down in shallow water: a flat ring spreading over the surface, which reads from the high game
    // camera, and a short crown of droplets. The ring lives longest, so it is the root.
    private static GameObject BuildFootstepSplash(Material ringMaterial, Material spray)
    {
        var root = new GameObject("Footstep Splash");
        ParticleSystem ring = AddParticles(root, ringMaterial, loop: false, lifetime: (0.6f, 0.7f), speed: (0f, 0f),
            size: (0.3f, 0.36f), gravity: 0f, alpha: 0.45f, max: 3, grow: false);
        ParticleSystem.MainModule main = ring.main;
        main.duration = 0.2f;
        main.stopAction = ParticleSystemStopAction.Destroy;
        ParticleSystem.SizeOverLifetimeModule spread = ring.sizeOverLifetime;
        spread.enabled = true;
        spread.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f, 6f, 6f),
            new Keyframe(1f, 1.8f, 0.6f, 0.6f)));
        // A second, later ring inside the first reads as a ripple rather than a flash.
        ParticleSystem.EmissionModule rings = ring.emission;
        rings.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.14f, 1) });
        ParticleSystem.ShapeModule ringShape = ring.shape;
        ringShape.enabled = false;
        ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        var droplets = new GameObject("Droplets");
        droplets.transform.SetParent(root.transform, false);
        ParticleSystem drops = AddParticles(droplets, spray, loop: false, lifetime: (0.25f, 0.4f), speed: (0.9f, 1.6f),
            size: (0.04f, 0.08f), gravity: 1.2f, alpha: 0.6f, max: 12, grow: false);
        ParticleSystem.MainModule dropsMain = drops.main;
        dropsMain.duration = 0.1f;
        Burst(drops, 7);
        Shape(drops, ParticleSystemShapeType.Cone, 0.12f, 35f);
        return root;
    }

    private static GameObject BuildPlunge(Material spray)
    {
        var root = new GameObject("Waterfall Plunge");
        var interactor = root.AddComponent<WaterInteractor>();
        interactor.Mode = WaterInteractor.Kind.Constant;
        interactor.Radius = 1.8f;
        interactor.Foam = 1f;

        var mist = new GameObject("Mist");
        mist.transform.SetParent(root.transform, false);
        ParticleSystem mistSystem = AddParticles(mist, spray, loop: true, lifetime: (2.5f, 4f), speed: (0.4f, 1f),
            size: (1.2f, 2.4f), gravity: -0.03f, alpha: 0.16f, max: 80, grow: true);
        Box(mistSystem, new Vector3(3f, 0.2f, 0.6f), 12f);

        var sprayObject = new GameObject("Spray");
        sprayObject.transform.SetParent(root.transform, false);
        ParticleSystem spraySystem = AddParticles(sprayObject, spray, loop: true, lifetime: (0.5f, 0.9f), speed: (2f, 4f),
            size: (0.04f, 0.1f), gravity: 1f, alpha: 0.75f, max: 200, grow: false);
        Box(spraySystem, new Vector3(3f, 0.1f, 0.4f), 40f);
        ParticleSystem.ShapeModule shape = spraySystem.shape;
        shape.randomDirectionAmount = 0.35f;
        return root;
    }

    internal static ParticleSystem AddParticles(GameObject go, Material material, bool loop, (float, float) lifetime,
        (float, float) speed, (float, float) size, float gravity, float alpha, int max, bool grow)
    {
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = loop;
        main.prewarm = loop;
        main.duration = loop ? 5f : 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Item1, lifetime.Item2);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
        main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 1f, 1f, alpha);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.playOnAwake = true;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
        if (grow)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static void Burst(ParticleSystem system, short count)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    private static void Shape(ParticleSystem system, ParticleSystemShapeType type, float radius, float angle = 0f)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = type;
        shape.radius = radius;
        shape.angle = angle;
        shape.rotation = new Vector3(-90f, 0f, 0f); // emit upwards
    }

    internal static void Box(ParticleSystem system, Vector3 box, float rate)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        // A box emits along its local Z, which the rotation turns upwards; height (Y) and depth (Z) swap with it.
        shape.scale = new Vector3(box.x, box.z, box.y);
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
    }

    // ---------------------------------------------------------------- textures

    /// <summary>Tileable ripple normals: a sum of waves whose frequencies are whole numbers per tile.</summary>
    public static Texture2D NormalPixels(int size)
    {
        var random = new System.Random(11);
        var waves = new List<(float Kx, float Ky, float Amplitude, float Phase)>();
        float slope = 0f;
        while (waves.Count < 24)
        {
            int kx = random.Next(-12, 13), ky = random.Next(-12, 13);
            float k = Mathf.Sqrt(kx * kx + ky * ky);
            if (k < 2f || k > 14f)
            {
                continue;
            }

            float amplitude = 1f / Mathf.Pow(k, 1.3f);
            waves.Add((kx, ky, amplitude, (float)random.NextDouble() * Mathf.PI * 2f));
            slope += amplitude * k * Mathf.PI * 2f;
        }

        float gain = 2.2f / slope;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float du = 0f, dv = 0f;
                foreach (var wave in waves)
                {
                    float c = Mathf.Cos(Mathf.PI * 2f * (wave.Kx * u + wave.Ky * v) + wave.Phase) * wave.Amplitude * Mathf.PI * 2f;
                    du += c * wave.Kx;
                    dv += c * wave.Ky;
                }

                Vector3 n = new Vector3(-du * gain, -dv * gain, 1f).normalized;
                pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>Tileable foam: R = lacy bubble walls and blotches (revealed brightest first), G = soft noise.</summary>
    public static Texture2D FoamPixels(int size)
    {
        var random = new System.Random(23);
        Vector2[,] cells8 = Points(8, random), cells16 = Points(16, random);
        float[,] noiseA = Lattice(16, random), noiseB = Lattice(16, random);
        var values = new float[size * size];
        var second = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                Worley(u, v, cells8, out float a1, out float a2);
                Worley(u, v, cells16, out float b1, out float b2);
                float walls = 0.55f * (1f - Mathf.Clamp01((a2 - a1) / 0.3f)) + 0.3f * (1f - Mathf.Clamp01((b2 - b1) / 0.3f));
                values[y * size + x] = walls + 0.45f * Fbm(u, v, noiseA);
                second[y * size + x] = Fbm(u, v, noiseB);
            }
        }

        Normalise(values);
        Normalise(second);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(values[i], second[i], 0f, 1f);
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>Tileable caustics: bright lines along cell edges (the shader adds two drifting copies, brighter
    /// where they cross).</summary>
    public static Texture2D CausticsPixels(int size)
    {
        var random = new System.Random(37);
        Vector2[,] cells = Points(6, random);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Worley((float)x / size, (float)y / size, cells, out float f1, out float f2);
                float c = Mathf.Pow(1f - Mathf.Clamp01((f2 - f1) / 0.35f), 2f);
                pixels[y * size + x] = new Color(c, c, c, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>A soft white disc (alpha = shape) for mist and spray.</summary>
    public static Texture2D DropletPixels(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>A thin bright ripple ring with a faint fill, for footsteps in shallow water.</summary>
    public static Texture2D RingPixels(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.07f, 2f));
                float fill = r < 0.8f ? 0.06f * r / 0.8f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ring + fill) * (r < 1f ? 1f : 0f));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Vector2[,] Points(int cells, System.Random random)
    {
        var points = new Vector2[cells, cells];
        for (int y = 0; y < cells; y++)
        {
            for (int x = 0; x < cells; x++)
            {
                points[x, y] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            }
        }

        return points;
    }

    /// <summary>Nearest and second-nearest feature point distances (in cells), wrapping around the tile.</summary>
    private static void Worley(float u, float v, Vector2[,] points, out float f1, out float f2)
    {
        int cells = points.GetLength(0);
        Vector2 p = new Vector2(u * cells, v * cells);
        int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
        f1 = f2 = float.MaxValue;
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox, y = cy + oy;
                Vector2 feature = new Vector2(x, y) + points[(x % cells + cells) % cells, (y % cells + cells) % cells];
                float d = Vector2.Distance(p, feature);
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                }
                else if (d < f2)
                {
                    f2 = d;
                }
            }
        }
    }

    private static float[,] Lattice(int cells, System.Random random)
    {
        var lattice = new float[cells, cells];
        for (int y = 0; y < cells; y++)
        {
            for (int x = 0; x < cells; x++)
            {
                lattice[x, y] = (float)random.NextDouble();
            }
        }

        return lattice;
    }

    /// <summary>Three octaves of tileable value noise (periods 4, 8 and 16 cells of the lattice).</summary>
    private static float Fbm(float u, float v, float[,] lattice)
    {
        float sum = 0f, weight = 0.5f;
        for (int period = 4; period <= 16; period *= 2, weight *= 0.5f)
        {
            sum += Value(u * period, v * period, period, lattice) * weight;
        }

        return sum / 0.875f;
    }

    private static float Value(float x, float y, int period, float[,] lattice)
    {
        int size = lattice.GetLength(0);
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float Sample(int ix, int iy) => lattice[((ix % period) + period) % period % size, ((iy % period) + period) % period % size];
        return Mathf.Lerp(Mathf.Lerp(Sample(x0, y0), Sample(x0 + 1, y0), fx),
                          Mathf.Lerp(Sample(x0, y0 + 1), Sample(x0 + 1, y0 + 1), fx), fy);
    }

    private static void Normalise(float[] values)
    {
        float low = float.MaxValue, high = float.MinValue;
        foreach (float value in values)
        {
            low = Mathf.Min(low, value);
            high = Mathf.Max(high, value);
        }

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (values[i] - low) / Mathf.Max(high - low, 1e-6f);
        }
    }
}

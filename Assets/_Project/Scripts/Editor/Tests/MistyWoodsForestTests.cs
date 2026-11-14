using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// The Misty Woods forest (Mossy Cedrus and Mountain Lake, converted by VegetationPackConverter) keeps the world's shaders
/// and gameplay, and the raid's scattering places it by habitat: these fail when either slips.
/// </summary>
public sealed class MistyWoodsForestTests
{
    private static RaidMapDefinition Map => AssetDatabase.LoadAssetAtPath<RaidMapDefinition>("Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset");

    [Test]
    public void Habitat_FavoursItsOwnGround()
    {
        var wetLover = new RaidScatterHabitat { slope = new Vector2(0f, 20f), wet = 1f };
        var spot = new RaidScatterHabitat.Spot(Vector2.zero, 5f, 0.8f, 0.9f, 0f, 0f);
        var dry = new RaidScatterHabitat.Spot(Vector2.zero, 5f, 0.8f, 0f, 0f, 0f);
        var steep = new RaidScatterHabitat.Spot(Vector2.zero, 40f, 0.8f, 0.9f, 0f, 0f);
        Assert.Greater(wetLover.Suitability(spot, 0), wetLover.Suitability(dry, 0) * 3f, "wet ground suits it far better");
        Assert.Greater(wetLover.Suitability(dry, 0), 0f, "but dry ground never rules it out");
        Assert.AreEqual(0f, wetLover.Suitability(steep, 0), "too steep is too steep");

        var edgeTree = new RaidScatterHabitat { edge = 1f };
        var deepTree = new RaidScatterHabitat { edge = -1f };
        var edge = new RaidScatterHabitat.Spot(Vector2.zero, 0f, 0.5f, 0f, 1f, 0f);
        Assert.Greater(edgeTree.Suitability(edge, 0), deepTree.Suitability(edge, 0) * 3f);
    }

    [Test]
    public void Scattering_KeepsCrownsApart_AndFillsTheGapsWithUndergrowth()
    {
        RaidMapDefinition copy = Object.Instantiate(Map);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(copy.Size, 30f, copy.Size) };
        try
        {
            byte[] none = new byte[16 * 16];
            byte[] forest = Enumerable.Repeat((byte)255, 16 * 16).ToArray();
            copy.UseGenerated(16, none, forest, none, new RaidMapMood(), 7);
            int trees = RaidMapSurface.Scatter(data, copy, new List<RaidMapSurface.Clearing>(), out int others);
            Assert.Greater(trees, 1000, "a dense forest");
            Assert.Greater(others, 1000, "undergrowth, rocks and logs, even under a closed canopy");

            TreePrototype[] prototypes = data.treePrototypes;
            TreeInstance[] instances = data.treeInstances;
            var giants = instances.Where(t => prototypes[t.prototypeIndex].prefab.name.StartsWith("Cedar Giant")).ToArray();
            Assert.IsNotEmpty(giants);
            foreach (TreeInstance giant in giants)
            {
                foreach (TreeInstance other in giants)
                {
                    if (giant.position == other.position) continue;
                    float metres = Vector2.Distance(new Vector2(giant.position.x, giant.position.z), new Vector2(other.position.x, other.position.z)) * copy.Size;
                    Assert.Greater(metres, 2f, "giants keep their room");
                }
            }

            int undergrowth = instances.Count(t => copy.Undergrowth.Any(u => u.prefab == prototypes[t.prototypeIndex].prefab));
            Assert.Greater(undergrowth, 500);
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(copy);
        }
    }

    [Test]
    public void Paint_LaysLitterUnderTheDeepForest_AndNotOnWetGround()
    {
        RaidMapDefinition copy = Object.Instantiate(Map);
        var data = new TerrainData { heightmapResolution = 33, alphamapResolution = 64, size = new Vector3(copy.Size, 30f, copy.Size) };
        try
        {
            data.terrainLayers = copy.Layers;
            byte[] none = new byte[16 * 16];
            byte[] forest = Enumerable.Repeat((byte)255, 16 * 16).ToArray();
            copy.UseGenerated(16, none, forest, none, new RaidMapMood(), 3);
            RaidMapSurface.Paint(data, copy);
            float dryLitter = Layer(data, 3);
            RaidMapSurface.Paint(data, copy, spot => 1f);
            Assert.Greater(dryLitter, 0.05f, "litter covers patches of the deep forest");
            Assert.AreEqual(0f, Layer(data, 3), 1e-4f, "none on wet ground: moss takes it");
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(copy);
        }
    }

    private static float Layer(TerrainData data, int layer)
    {
        float[,,] weights = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        float sum = 0f;
        for (int z = 0; z < weights.GetLength(0); z++)
        for (int x = 0; x < weights.GetLength(1); x++)
            sum += weights[z, x, layer];
        return sum / (weights.GetLength(0) * weights.GetLength(1));
    }

    [Test]
    public void TreesSway_WithWindWeightsBakedInEveryLevel()
    {
        foreach (RaidMapDefinition.Scatter tree in Map.Trees.Concat(Map.Undergrowth).Where(s => s.prefab.GetComponentInChildren<Renderer>().sharedMaterial.shader.name == VegetationPackConverter.WindShader))
        {
            Assert.IsNotNull(tree.prefab.GetComponent<LODGroup>(), tree.prefab.name);
            foreach (MeshFilter filter in tree.prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord1) && mesh.HasVertexAttribute(VertexAttribute.TexCoord2), mesh.name);
                Assert.IsTrue(mesh.uv2.All(uv => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f), mesh.name);
                // Wood swings, it doesn't ripple: every triangle moves on one phase, and leaves only tremble.
                var phases = new List<Vector2>();
                mesh.GetUVs(2, phases);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    int[] triangles = mesh.GetTriangles(s);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Assert.AreEqual(phases[triangles[t]].x, phases[triangles[t + 1]].x, 1e-4f, mesh.name);
                        Assert.AreEqual(phases[triangles[t]].x, phases[triangles[t + 2]].x, 1e-4f, mesh.name);
                    }
                }

                Assert.LessOrEqual(phases.Max(p => p.y), 0.2f, $"{mesh.name}: flutter");
                foreach (Material material in filter.GetComponent<Renderer>().sharedMaterials)
                {
                    Assert.IsTrue(material.enableInstancing, material.name);
                    Assert.Greater(material.GetFloat("_WindStrength"), 0f, material.name);
                }
            }
        }
    }

    [Test]
    public void EveryKind_IsOneTheTerrainCanDraw()
    {
        foreach (RaidMapDefinition.Scatter scatter in Map.Trees.Concat(Map.Undergrowth).Concat(Map.Rocks))
        {
            GameObject prefab = scatter.prefab;
            Assert.IsTrue(prefab.GetComponent<LODGroup>() != null || prefab.GetComponent<MeshRenderer>() != null,
                $"{prefab.name}: the terrain draws a LOD group or a renderer on the root, nothing else");
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh, $"{prefab.name}/{renderer.name} lost its mesh");
                Assert.AreEqual(mesh.subMeshCount, renderer.sharedMaterials.Length, $"{prefab.name}/{renderer.name}");
                Assert.IsTrue(renderer.sharedMaterials.All(m => m != null), $"{prefab.name}/{renderer.name} has an empty material");
                Assert.LessOrEqual(mesh.subMeshCount, 8, $"{prefab.name}/{renderer.name}: one draw per material");
                // Shadows-only renderers only as TreeShadowProxies' (a lighter level casting for the finest two, which the
                // terrain draws to the shadow map alone), never a pack's leftover that would stand in for what is seen.
                if (renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                {
                    Assert.IsTrue(renderer.name.StartsWith("Shadow Proxy"), $"{prefab.name}/{renderer.name}");
                    LODGroup group = prefab.GetComponent<LODGroup>();
                    Assert.IsNotNull(group, $"{prefab.name}: a shadow proxy belongs to a LOD group");
                    foreach (LOD lod in group.GetLODs().Where(l => l.renderers.Contains(renderer)))
                    {
                        Assert.IsTrue(lod.renderers.Any(r => r != null && r.shadowCastingMode == ShadowCastingMode.Off), $"{prefab.name}: its level is also drawn");
                    }
                }
            }
        }
    }

    [Test]
    public void TheSmallerRocks_AreMinedLikeTrees_AndTheCliffsAreNot()
    {
        RaidMapDefinition.Scatter[] mined = Map.Rocks.Where(r => TerrainNodeHarvesting.IsGatherable(r.prefab)).ToArray();
        Assert.GreaterOrEqual(mined.Length, 4, "boulders and rocks to break for stone");
        foreach (RaidMapDefinition.Scatter rock in mined)
        {
            var node = rock.prefab.GetComponent<ResourceNode>();
            Assert.IsNotNull(node.Definition, rock.prefab.name);
            Assert.IsNotNull(rock.prefab.GetComponent<Interactable>(), rock.prefab.name);
            Assert.IsNotNull(rock.prefab.GetComponent<ResourceSource>(), rock.prefab.name);
            Assert.IsNull(rock.prefab.GetComponent<SaveableEntity>(), $"{rock.prefab.name}: painted rocks share one prefab, not one save id");
            Assert.IsTrue(node.KeepsOwnModel, $"{rock.prefab.name}: the live copy must look like the painted rock");
        }

        foreach (string cliff in new[] { "Cliff Boulder A", "Cliff Slab", "Rock Outcrop A", "Rock Outcrop B" })
        {
            Assert.IsFalse(TerrainNodeHarvesting.IsGatherable(Map.Rocks.First(r => r.prefab.name == cliff).prefab), $"{cliff} is too big to mine");
        }
    }

    [Test]
    public void TheRaid_ShedsNeedles_AndSoundsLikeTheForest()
    {
        Scene raid = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity", OpenSceneMode.Additive);
        try
        {
            GameObject[] roots = raid.GetRootGameObjects();
            var debris = roots.Select(r => r.GetComponentInChildren<CanopyDebris>(true)).FirstOrDefault(c => c != null);
            Assert.IsNotNull(debris, "needles and twigs fall from the crowns");
            var renderer = debris.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(ParticleSystemRenderMode.Mesh, renderer.renderMode);
            Assert.Greater(renderer.meshCount, 0);
            Assert.IsNotNull(renderer.sharedMaterial.GetTexture("_BaseMap"));
            Assert.IsFalse(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader));

            var flies = roots.Select(r => r.GetComponentInChildren<AmbientOneShots>(true)).FirstOrDefault(c => c != null);
            Assert.IsNotNull(flies?.Sound, "flies buzz past");
            Assert.IsTrue(flies.Sound.Clips.Any(c => c != null));

            SceneAudio audio = roots.Select(r => r.GetComponentInChildren<SceneAudio>(true)).First(a => a != null);
            Assert.IsTrue(audio.Ambience.tracks.Any(t => t.clip != null && t.clip.name == "Forest_Background_wind_1"), "the recorded forest wind underneath");
        }
        finally
        {
            EditorSceneManager.CloseScene(raid, true);
        }
    }

    [Test]
    public void RocksAndLogs_AreTessellated_AndTheSolidOnesBlock()
    {
        foreach (RaidMapDefinition.Scatter rock in Map.Rocks.Concat(Map.Undergrowth))
        {
            foreach (Renderer renderer in rock.prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.IsTrue(material.shader.name == MeshSurface.ShaderName || material.shader.name == VegetationPackConverter.WindShader, $"{rock.prefab.name}: {material.shader.name}");
                    Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader), material.name);
                }
            }
        }

        foreach (RaidMapDefinition.Scatter rock in Map.Rocks.Where(r => r.prefab.GetComponent<CapsuleCollider>() != null))
        {
            CapsuleCollider capsule = rock.prefab.GetComponent<CapsuleCollider>();
            Assert.IsTrue(capsule.direction == 0 || capsule.direction == 1, "the navmesh's trunk shapes read upright or along x");
            Assert.IsFalse(capsule.isTrigger, rock.prefab.name + " blocks");
            // What nothing can mine, fell, break or move has no obstacle: the bake cuts round its collider
            // (VegetationPackConverter.DropStaticObstacle). What can go away carves the NavMesh while it stands.
            bool canGo = rock.prefab.GetComponent<ResourceNode>() != null || rock.prefab.GetComponent<ChoppableTree>() != null
                || rock.prefab.GetComponent<Interactable>() != null || rock.prefab.GetComponent<Rigidbody>() != null;
            if (canGo)
            {
                Assert.IsNotNull(rock.prefab.GetComponent<UnityEngine.AI.NavMeshObstacle>(), rock.prefab.name);
            }
        }

        Assert.IsTrue(Map.Undergrowth.Where(u => u.prefab.GetComponent<ChoppableTree>() == null).All(u => u.prefab.GetComponent<Collider>() == null),
            "shrubs and clutter never block the way");
        foreach (RaidMapDefinition.Scatter young in Map.Undergrowth.Where(u => u.prefab.GetComponent<ChoppableTree>() != null))
        {
            Assert.IsNotNull(young.prefab.GetComponent<CapsuleCollider>(), $"{young.prefab.name} is a tree: it blocks the way");
        }
    }

    [Test]
    public void EveryTreeTallerThanAPerson_CanBeFelled_WithATrunkItsOwnSize()
    {
        foreach (RaidMapDefinition.Scatter scatter in Map.Trees.Concat(Map.Undergrowth))
        {
            if (scatter.prefab.GetComponentInChildren<Renderer>().sharedMaterial.shader.name != VegetationPackConverter.WindShader) continue;
            // The wind material holds the true height; renderer bounds are padded for sway.
            float height = scatter.prefab.GetComponentInChildren<Renderer>().sharedMaterial.GetFloat("_TreeHeight") * scatter.scale.y;
            if (height < 5f) continue;
            Assert.IsNotNull(scatter.prefab.GetComponent<ChoppableTree>(), $"{scatter.prefab.name} stands {height:0} m: it must fell");
            var trunk = scatter.prefab.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(trunk, scatter.prefab.name);
            Assert.Less(trunk.radius, 0.5f, $"{scatter.prefab.name}: a trunk wider than the tree makes a giant stump");
            if (scatter.prefab.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Any(m => m.GetFloat("_AlphaClip") > 0.5f))
            {
                Assert.IsNotNull(scatter.prefab.GetComponent<TreeLeafLook>(), $"{scatter.prefab.name} drops its own leaves");
            }
        }
    }

    [Test]
    public void MistyWoods_ReachesNothingOfTheOldForestKit()
    {
        var old = new[] { "Assets/_Project/Art/Forest/Prefabs/", "Assets/_Project/Data/Prologue/Vegetation/" };
        var reached = new List<string>(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(Map), true));
        reached.AddRange(AssetDatabase.GetDependencies("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset", true));
        foreach (string guid in AssetDatabase.FindAssets("t:RaidSetPieceDefinition"))
        {
            reached.AddRange(AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), true));
        }

        string[] leftover = reached.Where(path => path.EndsWith(".prefab") && old.Any(path.StartsWith)).Distinct().ToArray();
        CollectionAssert.IsEmpty(leftover, "old trees, boulders or logs in the Misty Woods");
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset");
        Assert.IsTrue(data.detailPrototypes.Any(p => p.prototype.name == "Glow Mushrooms" && AssetDatabase.GetAssetPath(p.prototype).Contains("Art/Generated/Grass")),
            "the blue glowing mushrooms stay");
    }

    [Test]
    public void Details_AreInstancedGrass_AndTheGlowingOnesGlow()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset");
        foreach (DetailPrototype prototype in data.detailPrototypes)
        {
            Assert.IsTrue(prototype.useInstancing, prototype.prototype.name);
            var renderer = prototype.prototype.GetComponent<MeshRenderer>();
            Assert.AreEqual(GrassSetup.ShaderName, renderer.sharedMaterial.shader.name);
            Mesh mesh = prototype.prototype.GetComponent<MeshFilter>().sharedMesh;
            bool glows = prototype.prototype.name.StartsWith("Glow");
            Assert.AreEqual(glows, mesh.colors.Any(c => c.b > 0.5f), prototype.prototype.name);
            if (AssetDatabase.GetAssetPath(prototype.prototype).Contains("Art/Generated/Grass")) continue; // the kept blue mushrooms
            Assert.IsNotNull(renderer.sharedMaterial.GetTexture("_BaseMap"), prototype.prototype.name);
            Assert.AreEqual(1, mesh.subMeshCount);
            if (renderer.sharedMaterial.GetFloat("_WindStrength") > 0f)
            {
                Assert.Greater(mesh.colors.Max(c => c.r), 0.9f, $"{prototype.prototype.name} bends at the top");
            }
        }

        foreach (TerrainGrowth.Plant plant in Map.Plants)
        {
            Assert.IsTrue(data.detailPrototypes.Any(p => p.prototype.name == plant.prototype), plant.prototype);
        }
    }

    [Test]
    public void Ground_HasFourDisplacedLayers_WithHeightInTheMask()
    {
        Assert.AreEqual(4, Map.Layers.Length);
        Assert.AreEqual(4f, Map.TerrainMaterial.GetFloat("_DisplacementLayerCount"));
        foreach (TerrainLayer layer in Map.Layers)
        {
            Assert.IsNotNull(layer.normalMapTexture, layer.name);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(layer.maskMapTexture));
            Assert.IsFalse(importer.sRGBTexture, layer.name + " mask is data, not colour");
        }
    }
}

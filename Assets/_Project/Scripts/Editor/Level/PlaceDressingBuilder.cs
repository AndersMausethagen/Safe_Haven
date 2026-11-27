using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The Haven's places dressed as scenes (Docs/HavenWorldStory_2026-11-26.md §7.3 and step 4): each place a story told by what is
/// left there. One prefab of props per scene, laid out round the place's centre (+z north), stood at runtime by HavenPlaceScenes
/// (HavenPlaceVerbs › Scenes), which settles each prop on the ground under it, so a layout written on flat ground sits on any slope.
/// The props are the ruin kit (RuinKitBuilder: the old village's walls, fences, frames, carts, overgrown with moss) and the world's
/// own (standing stones, boardwalk, drying rack); scorch and damp ground are decals. Run: RuinKitBuilder first, then Build.
/// </summary>
public static class PlaceDressingBuilder
{
    public const string Folder = "Assets/_Project/Prefabs/World/Haven Places";
    private const string Ruins = "Assets/_Project/Art/World/Ruins/Prefabs/";
    private const string Props = "Assets/_Project/Art/World/Props/";
    private const string World = "Assets/_Project/Prefabs/World/";
    private const string Decals = "Assets/_Project/Art/Decals/Materials/";

    /// <summary>One prop of a scene: what, where (metres from the centre, +z north), turned (degrees about up), its size, and a tilt
    /// for what has fallen (degrees about its own x, then z). Raised lifts it off the ground (negative sinks it).</summary>
    private sealed class Prop
    {
        public string Path;
        public float X, Z, Turn, Scale = 1f, Raised, TiltX, TiltZ;
        // A decal (Path is its material): width and depth on the ground.
        public Vector2 Decal;
        // A light (no Path): its colour, range and intensity, Raised above the ground.
        public Color Light;
        public float Range, Intensity;
    }

    private static Prop L(float x, float z, float raised, Color colour, float range, float intensity) =>
        new Prop { X = x, Z = z, Raised = raised, Light = colour, Range = range, Intensity = intensity };

    private static Prop P(string path, float x, float z, float turn = 0f, float scale = 1f, float raised = 0f, float tiltX = 0f, float tiltZ = 0f) =>
        new Prop { Path = path, X = x, Z = z, Turn = turn, Scale = scale, Raised = raised, TiltX = tiltX, TiltZ = tiltZ };

    private static Prop D(string material, float x, float z, float width, float depth, float turn = 0f) =>
        new Prop { Path = Decals + material + ".mat", X = x, Z = z, Turn = turn, Decal = new Vector2(width, depth) };

    private static string R(string name) => Ruins + name + ".prefab";

    // The burnt fence leaves the yard toward the grave in the pines (east-south-east, 106.7 degrees from north): the family's flight.
    private static IEnumerable<Prop> FenceLine()
    {
        const float bearing = 106.7f;
        var along = new Vector2(Mathf.Sin(bearing * Mathf.Deg2Rad), Mathf.Cos(bearing * Mathf.Deg2Rad));
        var start = new Vector2(11.5f, -1f);
        for (int i = 0; i < 6; i++)
        {
            if (i == 3)
            {
                continue;
            }

            Vector2 at = start + along * (3.4f * i);
            bool fallen = i == 2 || i == 5;
            yield return P(R(i == 4 ? "Charred Wattle Fence" : "Charred Stick Fence"), at.x, at.y, bearing + (i % 2 == 0 ? 4f : -6f),
                raised: fallen ? 0.15f : 0f, tiltZ: fallen ? 82f : (i * 3f));
        }
    }

    private static readonly Dictionary<string, Prop[]> Scenes = new Dictionary<string, Prop[]>
    {
        // The farm that burned the night the hearth went out. Around the burnt longhouse already there (its set piece): the hearth
        // kept going outside after the roof was gone, the outbuilding's frame, the cart, the scorch, and the fence line leading off.
        ["Burnt Farmstead Scene"] = Concat(new[]
        {
            D("Scorch", 0.6f, -5.5f, 12f, 10f),
            D("Scorch", 8.5f, -7.5f, 5f, 5f, 30f),
            D("Scorch", -7.5f, -5.5f, 3f, 3f),
            P(R("Cut Stone A"), -8.3f, -5.4f, 10f, 0.8f, -0.1f),
            P(R("Cut Stone B"), -7.6f, -6.4f, 95f, 0.8f, -0.1f),
            P(R("Cut Stone C"), -6.7f, -5.7f, 170f, 0.8f, -0.1f),
            P(R("Cut Stone A"), -7.3f, -4.6f, 260f, 0.75f, -0.1f, tiltZ: 18f),
            P(R("Charred Plank"), -7.5f, -5.5f, 35f, 0.35f, 0.12f),
            P(R("Charred Plank"), -7.4f, -5.4f, 120f, 0.3f, 0.16f),
            P(R("Charred Timber Frame A"), 8.6f, -7.2f, 15f, tiltZ: 9f),
            P(R("Charred Plank"), 9.8f, -9.4f, 70f, 1f, 0.1f, 4f),
            P(R("Charred Cart"), -7f, 3.2f, 25f),
            P(R("Old Barrel"), -3.6f, 0.8f, 40f, 1f, 0.4f, 0f, 90f),
            P(R("Old Bucket"), -6.1f, -3.1f, 0f),
        }, FenceLine()),

        // Where the family buried the one who didn't reach the trees: a cairn with a small stone at its head, a wattle hurdle round it.
        ["Family's Grave Scene"] = new[]
        {
            P(R("Stone Pile"), 0f, 0f, 0f, 1.15f),
            P(R("Stone Pile"), 0.3f, 0.6f, 120f, 0.85f, 0.12f),
            P(Props + "Standing Stone/Standing Stone.prefab", 0f, 1.7f, 8f, 0.55f),
            P(R("Wattle Fence"), 2.1f, 0.3f, 0f, 0.8f),
            P(R("Wattle Fence"), -0.4f, -2.1f, 90f, 0.8f, tiltZ: 14f),
            P(R("Cut Stone B"), -1.6f, -0.9f, 35f, 0.7f, tiltZ: 70f),
            P(Props + "Old Bone/Old Bone.prefab", 1.3f, -1.4f, 60f, 0.7f),
        },

        // A farm of the old village, long empty: a house's dry-stone footing with its threshold, the well and trough, a yard fence,
        // a cart left in the field.
        ["Old Steading Scene"] = new[]
        {
            P(R("Dry Stone Wall A"), -1.6f, 2.5f, 90f),
            P(R("Dry Stone Wall C"), 3.3f, 2.5f, 90f),
            P(R("Dry Stone Wall B"), -3.3f, -2.5f, 90f),
            P(R("Dry Stone Wall A"), 2f, -2.5f, 90f, tiltZ: 4f),
            P(R("Fieldstone Wall"), -4.6f, 0.4f, 0f),
            P(R("Fieldstone Wall Corner"), 4.5f, -1.4f, 0f),
            P(R("Threshold Stone"), 1.55f, 2.7f, 0f),
            P(R("Timber Frame B"), -0.8f, -0.2f, 90f, tiltZ: 28f),
            P(R("Village Well"), 7.2f, 5f, 30f),
            P(R("Old Trough"), 8.8f, 2.9f, 10f),
            P(R("Old Bucket"), 6.1f, 3.6f, 0f, 1f, 0.2f, 0f, 75f),
            P(R("Stick Fence"), -2f, 8.2f, 90f),
            P(R("Stick Fence"), 1.5f, 8.2f, 92f, tiltZ: 7f),
            P(R("Stick Fence Gate"), 5f, 8.2f, 90f),
            P(R("Stick Fence"), -5.6f, 7.6f, 70f, 1f, 0.15f, 0f, 84f),
            P(R("Broken Cart"), -8.5f, -4.5f, 60f),
            P(R("Log Pile"), -6.3f, 3.4f, 0f),
            P(R("Old Barrel"), 5.7f, -3.9f, 20f),
            D("Trampled Ground", 0f, 0f, 10f, 6f),
        },

        // The hearth-keepers' hall: a long house's footing, its long hearth down the middle, the smith's corner, frames fallen in.
        ["Empty Hall Scene"] = new[]
        {
            P(R("Dry Stone Wall A"), -5.3f, 3f, 90f),
            P(R("Dry Stone Wall B"), 1.2f, 3f, 90f),
            P(R("Dry Stone Wall C"), 6f, 3f, 90f, tiltZ: 3f),
            P(R("Dry Stone Wall C"), -6.5f, -3f, 90f),
            P(R("Dry Stone Wall A"), -1f, -3f, 90f),
            P(R("Dry Stone Wall B"), 5.5f, -3f, 90f),
            P(R("Fieldstone Wall"), -8.1f, 0f, 0f),
            P(R("Fieldstone Wall Corner"), 8.1f, 1.8f, 0f),
            P(R("Threshold Stone"), 8.3f, -1.3f, 90f),
            P(R("Cut Stone A"), -2.7f, 0.55f, 90f, 0.9f, -0.15f),
            P(R("Cut Stone B"), -0.9f, 0.6f, 90f, 0.9f, -0.15f),
            P(R("Cut Stone C"), 0.9f, 0.5f, 90f, 0.9f, -0.15f),
            P(R("Cut Stone A"), 2.7f, 0.55f, 90f, 0.9f, -0.15f),
            P(R("Cut Stone B"), -1.8f, -0.55f, 90f, 0.9f, -0.15f),
            P(R("Cut Stone C"), 1.8f, -0.5f, 90f, 0.9f, -0.15f),
            P(R("Stone Pile"), 0f, 0f, 30f, 0.55f),
            D("Scorch", 0f, 0f, 7.5f, 2.6f),
            P(R("Timber Frame C"), -4f, -1.2f, 90f, 1f, 0.2f, 0f, 74f),
            P(R("Timber Frame A"), 3.8f, 1.3f, 270f, 1f, 0.2f, 0f, 70f),
            P(R("Old Anvil"), 5.6f, -1.6f, 30f),
            P(R("Log Pile"), -6f, -1.8f, 90f),
            P(R("Old Barrel"), 6.6f, 1.6f, 0f),
            P(R("Old Barrel"), 7f, 0.6f, 50f, 1f, 0.4f, 0f, 90f),
            P(R("Broken Cart"), 12f, 5f, -20f),
            P(R("Stone Pile"), -12f, -4f, 0f, 0.9f),
        },

        // The healer's spring: a little stone housing, a step down, a bucket, the garden's hurdle and the herbs' drying rack.
        ["Alder Spring Scene"] = new[]
        {
            P(R("Fieldstone Wall Corner"), 0f, 0.8f, 180f),
            P(R("Threshold Stone"), 0f, -0.7f, 0f, 1f, -0.25f),
            P(R("Cut Stone A"), -1.25f, -0.3f, 20f, 0.85f),
            P(R("Cut Stone B"), 1.3f, -0.2f, -15f, 0.85f),
            P(R("Old Bucket"), 0.95f, -1.5f, 0f),
            D("Trampled Ground", 0f, -0.8f, 3.5f, 3f),
            P(R("Wattle Fence"), -3.2f, 1.2f, 70f),
            P(R("Wattle Fence"), -4.5f, -1.6f, 115f, 1f, 0.12f, 0f, 80f),
            P("Assets/_Project/Prefabs/Raids/Camps/Drying Rack.prefab", 3.6f, 2.4f, 20f),
        },

        // At the high tarn: three old stones in an arc and a cairn behind them.
        ["Tarn Shrine Scene"] = new[]
        {
            P(Props + "Standing Stone/Standing Stone.prefab", -2.2f, 0.5f, 10f),
            P(Props + "Standing Stone/Standing Stone.prefab", 0f, 1.6f, -5f, 1.2f),
            P(Props + "Standing Stone/Standing Stone.prefab", 2.2f, 0.4f, 20f, 0.9f),
            P(R("Stone Pile"), 0f, 3.4f, 0f, 0.8f),
            P(R("Stone Pile"), 0.2f, 3.6f, 90f, 0.55f, 0.15f),
            P(R("Cut Stone C"), 0f, -0.4f, 90f, 0.9f, -0.1f),
            P(Props + "Waystone/Waystone.prefab", -4.6f, -2.5f, 35f),
        },

        // Ravenwood remembered (Ravenwood Remembered): the old quarry worked again, beside its face: blocks squared and stacked, a
        // hoist frame, the masons' logs.
        ["Old Quarry Worked"] = new[]
        {
            P(R("Cut Stone A"), 0f, 0f, 90f),
            P(R("Cut Stone B"), 0.9f, 0f, 92f),
            P(R("Cut Stone C"), 0.45f, 0.1f, 88f, 1f, 0.45f),
            P(R("Cut Stone A"), -1.2f, 1.6f, 10f),
            P(R("Cut Stone B"), 2.4f, 1.4f, 35f, 0.9f),
            P(R("Timber Frame A"), -0.5f, -2.8f, 90f),
            P(R("Log Pile"), 3.2f, -1.8f, 20f),
            P(R("Old Bucket"), 1.8f, -0.9f, 0f),
            D("Trampled Ground", 0.5f, -0.5f, 7f, 6f),
        },

        // The Hollow Pines remembered (The Hollow Pines Remembered): Asa's field by the burnt farm, sown with the family's old seed.
        ["Burnt Farmstead Field"] = new[]
        {
            P(R("Wattle Fence"), -3f, 0f, 0f),
            P(R("Wattle Fence"), 3f, 0f, 0f),
            P(R("Wattle Fence"), 0f, 2.6f, 90f),
            P(R("Stick Fence Gate"), 0f, -2.6f, 90f),
            D("Trampled Ground", 0f, 0f, 6f, 5f),
            P(R("Old Bucket"), 1.8f, -3.4f, 0f),
            P(R("Log Pile"), 5f, -2.5f, 90f),
            P(R("Cut Stone C"), 5.2f, 1.2f, 20f, 0.6f, -0.05f),
        },

        // Stillwater remembered (Stillwater Remembered): the fishers' rest worked again, fish drying over a smoking fire.
        ["Fisher's Rest Worked"] = new[]
        {
            P("Assets/_Project/Prefabs/Raids/Camps/Drying Rack.prefab", 0f, 0f, 15f),
            P("Assets/_Project/Prefabs/Raids/Camps/Drying Rack.prefab", 2.3f, 0.5f, 8f),
            P(R("Log Pile"), -2.4f, -1f, 80f),
            P(R("Old Barrel"), 1.2f, -2f, 0f),
            P(R("Old Bucket"), 0.2f, -2.4f, 0f),
            D("Scorch", 1.1f, 1.8f, 1.6f, 1.6f),
        },

        // Happenings (HavenHappenings): a pedlar resting on the old road, by a small fire.
        ["Pedlar's Camp"] = new[]
        {
            P(Props + "Hooded Stranger/Hooded Stranger.prefab", 0f, 0f, 200f),
            P(R("Log Pile"), 1.6f, 0.8f, 30f, 0.6f),
            P(R("Old Bucket"), -1f, 0.9f, 0f),
            P(R("Cut Stone A"), 0.6f, -1.6f, 0f, 0.45f, -0.05f),
            P(R("Cut Stone B"), 1.2f, -1.2f, 70f, 0.45f, -0.05f),
            D("Scorch", 0.9f, -1.5f, 1.4f, 1.4f),
            L(0.9f, -1.5f, 0.5f, new Color(1f, 0.6f, 0.3f), 6f, 1.2f),
        },

        // A storm felled an oak across the way: wood for whoever takes it.
        ["Storm-felled Oak"] = new[]
        {
            P("Assets/_Project/Art/Vegetation/Misty Woods/Rocks and Logs/Rotten Log A.prefab", 0f, 0f, 65f, 1.6f),
            P(R("Plank"), 1.5f, 1.8f, 20f, 0.6f, 0.05f),
            P(R("Plank"), -1.2f, -1.6f, 140f, 0.5f, 0.05f),
        },

        // Lights over the tarn at night: cold, moving the way people move.
        ["Tarn Lights"] = new[]
        {
            L(-2.5f, 1f, 1.6f, new Color(0.55f, 0.85f, 1f), 7f, 2.2f),
            L(1.8f, 3f, 1.9f, new Color(0.5f, 1f, 0.85f), 6f, 1.8f),
            L(0.4f, -2.2f, 1.4f, new Color(0.6f, 0.8f, 1f), 5f, 1.6f),
        },

        // The drowned road: a causeway of boards across the fen, sunk and broken, its posts leaning.
        ["Sunken Causeway Scene"] = new[]
        {
            P(World + "Set Pieces/Boardwalk.prefab", 0f, -6f, 30f, 1f, -0.15f),
            P(World + "Set Pieces/Boardwalk.prefab", 2f, -2.5f, 30f, 1f, -0.2f, 3f),
            P(World + "Set Pieces/Boardwalk.prefab", 4.1f, 1.2f, 32f, 1f, -0.35f, -5f, 4f),
            P(World + "Set Pieces/Boardwalk.prefab", 7.2f, 6.4f, 28f, 1f, -0.3f),
            P(R("Plank"), 6.1f, 3.6f, 40f, 1f, -0.05f),
            P(R("Plank"), -2.2f, -3f, 25f, 1f, -0.05f, -6f),
            P(R("Plank"), 1.2f, -4.6f, 0f, 1f, -1.4f, -78f),
            P(R("Plank"), 3.1f, 4.2f, 0f, 1f, -1.5f, -84f, 8f),
            P(World + "Set Pieces/Peat Stack.prefab", -3.5f, 2.5f, 15f),
        },
    };

    private static Prop[] Concat(IEnumerable<Prop> first, IEnumerable<Prop> second)
    {
        var all = new List<Prop>(first);
        all.AddRange(second);
        return all.ToArray();
    }

    public static string Build()
    {
        Directory.CreateDirectory(Folder);
        var report = new List<string>();
        foreach (KeyValuePair<string, Prop[]> scene in Scenes)
        {
            var root = new GameObject(scene.Key);
            try
            {
                int made = 0;
                foreach (Prop prop in scene.Value)
                {
                    if (Make(prop, root.transform))
                    {
                        made++;
                    }
                    else
                    {
                        report.Add($"  {scene.Key}: missing {prop.Path}");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{scene.Key}.prefab");
                report.Add($"{scene.Key}: {made} props");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    private static bool Make(Prop prop, Transform root)
    {
        if (prop.Range > 0f)
        {
            var glow = new GameObject("Light");
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(prop.X, prop.Raised, prop.Z);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = prop.Light;
            light.range = prop.Range;
            light.intensity = prop.Intensity;
            light.shadows = LightShadows.None;
            return true;
        }

        if (prop.Decal != Vector2.zero)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(prop.Path);
            if (material == null)
            {
                return false;
            }

            var decal = new GameObject(Path.GetFileNameWithoutExtension(prop.Path) + " Decal");
            decal.transform.SetParent(root, false);
            // Projected straight down from a little above the ground, deep enough for a slope under it.
            decal.transform.localPosition = new Vector3(prop.X, 1f, prop.Z);
            decal.transform.localRotation = Quaternion.Euler(90f, prop.Turn, 0f);
            var projector = decal.AddComponent<DecalProjector>();
            projector.material = material;
            projector.size = new Vector3(prop.Decal.x, prop.Decal.y, 3f);
            projector.pivot = Vector3.zero;
            return true;
        }

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(prop.Path);
        if (source == null)
        {
            return false;
        }

        var made = (GameObject)PrefabUtility.InstantiatePrefab(source, root);
        made.transform.localPosition = new Vector3(prop.X, prop.Raised, prop.Z);
        made.transform.localRotation = Quaternion.Euler(0f, prop.Turn, 0f) * Quaternion.Euler(prop.TiltX, 0f, prop.TiltZ);
        made.transform.localScale = Vector3.one * prop.Scale;
        // A world prop drawn without a collider of its own (a standing stone, a waystone) gets a box: nobody walks through a stone.
        if (made.GetComponentInChildren<Collider>(true) == null)
        {
            Bounds bounds = LocalBounds(made);
            if (bounds.size.y > 0.6f)
            {
                var box = made.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }

        return true;
    }

    private static Bounds LocalBounds(GameObject made)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in made.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 m = made.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds local = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }

        return bounds;
    }
}

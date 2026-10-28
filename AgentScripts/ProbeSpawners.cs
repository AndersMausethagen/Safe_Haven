using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tries the level spawners in a playing scene (`unity command run_script --file AgentScripts/ProbeSpawners.cs --entry ProbeSpawners.<Step>`):
/// Zone puts a Mob Spawn Zone of wolves beside the player and spawns it; Clear kills them and checks the zone said so;
/// Nodes drops Random Resource Nodes of each kind around the player; Stones reports the grey stones' looks.
/// </summary>
public static class ProbeSpawners
{
    public static string Zone()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        player.GetComponent<Damageable>().AddInvulnerability(typeof(ProbeSpawners));
        var prefab = AssetDatabase.LoadAssetAtPath<MobSpawnZone>("Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab");
        MobSpawnZone zone = Object.Instantiate(prefab, player.transform.position + player.transform.forward * 14f, Quaternion.identity);
        zone.name = "Probe Zone 2";
        var data = new SerializedObject(zone);
        SerializedProperty list = data.FindProperty("creatures");
        list.ClearArray();
        list.InsertArrayElementAtIndex(0);
        list.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue =
            AssetDatabase.FindAssets("ForestBittenWolf t:Prefab").Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .First(p => p.GetComponent<MonsterAI>() != null);
        list.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        data.FindProperty("trigger").intValue = (int)MobSpawnTrigger.Manual;
        data.FindProperty("count").vector2IntValue = new Vector2Int(3, 3);
        data.ApplyModifiedPropertiesWithoutUndo();
        int made = zone.Spawn();
        bool allInside = true;
        foreach (MonsterAI monster in Object.FindObjectsByType<MonsterAI>().Where(m => (m.transform.position - zone.transform.position).sqrMagnitude < 100f))
        {
            allInside &= ZonePolygon.Contains(zone.WorldCorners, monster.transform.position);
        }

        return $"spawned {made}, alive {zone.AliveCount}, inside the zone {allInside}";
    }

    public static async Task<string> Clear()
    {
        MobSpawnZone zone = GameObject.Find("Probe Zone 2").GetComponent<MobSpawnZone>();
        bool cleared = false;
        zone.Cleared += () => cleared = true;
        foreach (MonsterAI monster in Object.FindObjectsByType<MonsterAI>().Where(m => (m.transform.position - zone.transform.position).sqrMagnitude < 6400f))
        {
            Damageable health = monster.GetComponent<Damageable>();
            health.ApplyDamage(new DamageInfo(null, null, 9999f, DamageType.Physical, false, monster.transform.position, Vector3.forward));
        }

        await Task.Delay(500);
        return $"alive {zone.AliveCount}, cleared event {cleared}, IsCleared {zone.IsCleared}";
    }

    public static string Nodes()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var prefab = AssetDatabase.LoadAssetAtPath<RandomResourceNode>("Assets/_Project/Prefabs/Resources/Random Resource Node.prefab");
        string text = "";
        int i = 0;
        foreach (ResourceNodeKind kind in new[] { ResourceNodeKind.Rock, ResourceNodeKind.Wood, ResourceNodeKind.Plant, ResourceNodeKind.Forage, ResourceNodeKind.Any })
        {
            Vector3 at = player.transform.position + Quaternion.Euler(0f, i++ * 40f, 0f) * Vector3.forward * 5f;
            RandomResourceNode node = Object.Instantiate(prefab, at, Quaternion.identity);
            var data = new SerializedObject(node);
            data.FindProperty("kind").intValue = (int)kind;
            data.FindProperty("tiers").vector2IntValue = new Vector2Int(0, 3);
            data.ApplyModifiedPropertiesWithoutUndo();
            text += $"{kind} -> pending; ";
        }

        return text;
    }

    public static string Became()
    {
        return string.Join("; ", Object.FindObjectsByType<RandomResourceNode>(FindObjectsInactive.Include)
            .Select(n => $"{n.Kind} -> {(n.Spawned != null ? n.Spawned.name + " (" + n.Spawned.GetComponent<ResourceNode>()?.Definition?.DisplayName + ")" : "nothing")}"));
    }

    public static string Stones()
    {
        return string.Join("; ", Object.FindObjectsByType<ResourceNode>()
            .Where(n => n.Definition != null && n.Definition.name == "GreyStone")
            .Select(n => string.Join("+", n.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh != null ? f.sharedMesh.name : "?"))).Take(12));
    }
}

/// <summary>Renders the grey stone nodes near the player from above and to the side, into a PNG.</summary>
public static class ProbeStonesView
{
    public static string Render(string path)
    {
        Player player = Object.FindAnyObjectByType<Player>();
        ResourceNode[] stones = Object.FindObjectsByType<ResourceNode>()
            .Where(n => n.Definition != null && n.Definition.name == "GreyStone" && (n.transform.position - player.transform.position).sqrMagnitude < 100f).ToArray();
        if (stones.Length == 0)
        {
            return "no stones near the player";
        }

        Vector3 centre = stones.Aggregate(Vector3.zero, (sum, s) => sum + s.transform.position) / stones.Length;
        var go = new GameObject("Probe Camera");
        try
        {
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            go.transform.position = centre + new Vector3(0f, 4f, -7f);
            go.transform.LookAt(centre + Vector3.up * 0.3f);
            var texture = new RenderTexture(1024, 576, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1024, 576, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0);
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
            return $"rendered {stones.Length} stones";
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}

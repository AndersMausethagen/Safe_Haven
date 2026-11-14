#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fireflies (`unity command run_script --file AgentScripts/BuildFireflies.cs --entry BuildFireflies.Apply`): the material,
/// a prefab, a green profile for the Haven and a blue one for the Misty Woods, and an instance in each of those scenes.
/// Replaces what it built before. Reopens SafeHaven.
/// </summary>
public static class BuildFireflies
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Environment/Fireflies.prefab";
    private const string MaterialPath = "Assets/_Project/Prefabs/Environment/Firefly.mat";
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";

    public static string Apply()
    {
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        const string haven = "Fireflies - Haven", misty = "Fireflies - Misty Woods";
        Profile(haven, new Color(0.55f, 1f, 0.3f));
        Profile(misty, new Color(0.3f, 0.68f, 1f));
        Prefab();
        AssetDatabase.SaveAssets();

        string raid = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("Raid_MistyWoods t:Scene").First());
        string[] misties = { raid, "Assets/_Project/Scenes/Prologue_MistyWoods.unity" };
        foreach (string path in misties)
        {
            Place(path, misty);
        }

        // Last, so the Editor ends where it started.
        Place(HomeScene, haven);
        return "placed in " + string.Join(", ", misties.Append(HomeScene).Select(System.IO.Path.GetFileNameWithoutExtension));
    }

    private static string ProfilePath(string name) => $"Assets/_Project/Data/World/{name}.asset";

    private static void Profile(string name, Color colour)
    {
        string path = ProfilePath(name);
        var profile = AssetDatabase.LoadAssetAtPath<FireflyProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<FireflyProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var so = new SerializedObject(profile);
        so.FindProperty("colour").colorValue = colour;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void Prefab()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Firefly"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        var root = new GameObject("Fireflies");
        try
        {
            var system = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            root.AddComponent<Fireflies>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Opening a scene unloads assets nothing holds on to, so the prefab and profile are loaded after it.
    private static void Place(string path, string profileName)
    {
        Scene scene = EditorSceneManager.OpenScene(path);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var profile = AssetDatabase.LoadAssetAtPath<FireflyProfile>(ProfilePath(profileName));
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Fireflies");
        if (old != null) Object.DestroyImmediate(old);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var so = new SerializedObject(instance.GetComponent<Fireflies>());
        so.FindProperty("profile").objectReferenceValue = profile;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
    }
}

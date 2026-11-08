using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// The portal takes the Heart Tree's place at the centre of Safe Haven (`unity command script run -- --file AgentScripts/BuildPortalHeart.cs
/// --entry BuildPortalHeart.Apply`), run once with SafeHaven open: it moves the portal to where the tree stood, moves the tree's ward, revival,
/// light and arrival point onto it, points everything that named the tree at the portal, removes the tree, and rewrites what the game says.
/// Quest and event ids are kept, so saves hold. The text half is safe to run again.
/// </summary>
public static class BuildPortalHeart
{
    private const float PortalScale = 1.5f;

    // After the plain renames below, the lines that were written for a tree and need rewriting for a door.
    private static readonly (string from, string to)[] Lines =
    {
        ("You wake beneath a great tree in a quiet clearing, and you wake alone. Its bark is warm, and something in it seems to be waiting for you.", "You wake on the grass of a quiet clearing, and you wake alone. At its heart stand cracked old stones, warm to the touch, and something in them seems to be waiting for you."),
        ("The door to the Misty Woods needs the portal's light, and cursed roots have reached inside the ward to choke it.", "The portal needs its light back, and cursed roots have reached inside the ward to choke it."),
        ("The portal's Haven page (H) shows what counts.", "The Haven page (H) shows what counts."),
        ("establish a foothold beneath the portal", "establish a foothold around it"),
        ("Prepare, then speak to the tree.", "Prepare, then speak to the portal."),
        ("Across the stream stand the stones that dropped you here.", "At the heart of the clearing stand the stones that dropped you here."),
        ("Strengthens the portal, gear and magic.", "Strengthens the portal's ward, gear and magic."),
        ("Free the portal's light first.", "They need their light back first."),
        ("Warmth stirs in the old bark.", "Warmth stirs in the old stone."),
        ("Open the door. I'll go now.", "Open the way. I'll go now."),
        ("With what? You have nothing but your hands, and I cannot open it.", "With what? You have nothing but your hands, and I cannot open."),
        ("The door across the stream sleeps, and I am too thin to wake it. Long ago people kept a hearth beneath my branches, and I kept the night from their door.", "I am a door, little one, and I have gone to sleep. I am too thin to wake myself. Long ago people kept a hearth beside these stones, and I kept the night from their door."),
        ("you will tell me why those old stones woke for your hand.", "you will tell me why these old stones woke for your hand."),
        ("There. A hearth under my branches, after a hundred winters.", "There. A hearth beside my stones, after a hundred winters."),
        ("The stones have gone quiet, but she is right. One spark burns close, just past the door, hiding from the wolves.", "My stones have gone quiet, but she is right. One spark burns close, just beyond me, hiding from the wolves."),
        ("Rest your hand on the bark.", "Rest your hand on the stone."),
        ("Now the door, across the stream. Feed it my light and it will open for you, as it did in the dark.", "Now bring me what you freed. Feed me that light and I will open for you, as I did in the dark."),
        ("come home before the door closes", "come home before the way closes"),
        ("Just past the door, off the path", "Just beyond me, off the path"),
        ("Every hearth under my branches", "Every hearth in my light"),
    };

    private static readonly (string from, string to)[] Names =
    {
        ("The Heart Tree's", "The portal's"), ("the Heart Tree's", "the portal's"), ("The Heart Tree", "The portal"), ("the Heart Tree", "the portal"),
        ("Heart Tree's", "Portal's"), ("Heart Tree", "Portal"), ("HeartTree", "HomePortal"),
    };

    public static string Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/SafeHaven.unity" || scene.isDirty)
        {
            return $"Open SafeHaven with no unsaved changes first ({scene.path}, dirty={scene.isDirty}). Nothing done.";
        }

        var log = new List<string>();
        log.Add(MoveTheScene(scene));
        log.Add($"text in the scene: {RewriteScene(scene)} strings");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"text in assets: {RewriteAssets()} assets");
        log.Add($"text in prefabs: {RewritePrefabs()} prefabs");
        log.Add(RenameEvents());
        AssetDatabase.SaveAssets();
        log.Add("still to read: " + string.Join(" || ", Leftovers(scene)));
        return string.Join("\n", log);
    }

    // ----- The scene -----

    private static string MoveTheScene(Scene scene)
    {
        HomePortal home = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        RaidPortal portal = Object.FindAnyObjectByType<RaidPortal>(FindObjectsInactive.Include);
        if (home.gameObject == portal.gameObject)
        {
            return "the portal is already the heart of the base";
        }

        GameObject tree = home.gameObject;
        Vector3 centre = tree.transform.position;
        portal.transform.position = centre;
        portal.transform.localScale = Vector3.one * PortalScale;

        // Copy first, then point everything at the copies, and only then remove the tree.
        var moved = new Dictionary<Object, Object> { { tree, portal.gameObject }, { tree.transform, portal.transform } };
        foreach (Component component in new Component[] { tree.GetComponent<HomePortal>(), tree.GetComponent<HomeRevival>(), tree.GetComponent<VisibilitySource>() })
        {
            ComponentUtility.CopyComponent(component);
            ComponentUtility.PasteComponentAsNew(portal.gameObject);
            Component[] same = portal.GetComponents(component.GetType());
            moved[component] = same[same.Length - 1];
        }

        Transform glow = tree.transform.Find("Heart Glow");
        Transform ring = tree.transform.Find("Ward Ring");
        Transform wake = tree.transform.Find("Arrival: Heart Tree");
        glow.SetParent(portal.transform, true);
        ring.SetParent(portal.transform, true);
        wake.SetParent(portal.transform, true);
        glow.name = "Portal Glow";
        glow.position = centre + Vector3.up * 3.4f;
        wake.name = "Arrival: Home Portal";
        wake.position = centre + new Vector3(0f, 0f, -3.6f);
        Edit(wake.GetComponent<PlayerArrivalPoint>(), so => so.FindProperty("id").stringValue = PlayerArrivalPoint.HomePortal);

        // Coming home from a raid lands in front of it too.
        foreach (PlayerArrivalPoint point in Object.FindObjectsByType<PlayerArrivalPoint>(FindObjectsInactive.Include))
        {
            if (point.transform != wake)
            {
                point.transform.position = centre + new Vector3(1.4f, 0f, -3.4f);
            }
        }

        int pointed = Repoint(scene, moved);
        Object.DestroyImmediate(tree);

        Edit(portal.GetComponent<Interactable>(), so => so.FindProperty("range").floatValue = 2.2f);
        Edit(portal.GetComponent<CraftingStation>(), so =>
        {
            so.FindProperty("opensWhenUsed").boolValue = false;
            so.FindProperty("menuRange").floatValue = 8f;
        });
        Edit(portal, so => so.FindProperty("windowRange").floatValue = 8f);
        Edit(portal.GetComponent<HomePortal>(), so => so.FindProperty("menuRange").floatValue = 8f);

        SleepingStones(portal);
        GameObject bloom = GameObject.Find("Heart Tree Bloom");
        if (bloom != null)
        {
            bloom.name = "Portal Bloom";
        }

        return $"portal moved to {centre}, {pointed} references pointed at it, tree removed";
    }

    // Everything outside the tree that named it now names the portal. Story nodes are managed references, which the iterator walks into.
    private static int Repoint(Scene scene, Dictionary<Object, Object> moved)
    {
        int pointed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || moved.ContainsKey(component))
                {
                    continue;
                }

                var so = new SerializedObject(component);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.NextVisible(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null && moved.TryGetValue(it.objectReferenceValue, out Object to))
                    {
                        it.objectReferenceValue = to;
                        changed = true;
                        pointed++;
                    }
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        return pointed;
    }

    // The active look was the only one with a model, so a sleeping portal at the centre of the base would be nothing at all.
    // The copy keeps the model's own material (its base colour, normal and occlusion maps): until 2026-11-08 this painted it
    // flat Base Stone, which threw the carved detail away and let the night turn the stones blue.
    private static void SleepingStones(RaidPortal portal)
    {
        Transform dormant = portal.transform.Find("Dormant Look");
        Transform frame = portal.transform.Find("Active Look").GetComponentsInChildren<BoxCollider>(true).First().transform;
        if (dormant.childCount > 0)
        {
            RestoreStones(dormant, frame);
            return;
        }

        GameObject copy = Object.Instantiate(frame.gameObject, dormant, true);
        copy.name = "Sleeping Stones";
    }

    /// <summary>Gives the sleeping stones the active model's own materials back (`--entry BuildPortalHeart.RestoreSleepingStones`,
    /// SafeHaven open). Saves the scene.</summary>
    public static string RestoreSleepingStones()
    {
        RaidPortal portal = Object.FindObjectsByType<RaidPortal>(FindObjectsInactive.Include)
            .FirstOrDefault(p => p.transform.Find("Dormant Look/Sleeping Stones") != null);
        if (portal == null)
        {
            return "no portal with sleeping stones in the open scene";
        }

        Transform frame = portal.transform.Find("Active Look").GetComponentsInChildren<BoxCollider>(true).First().transform;
        string done = RestoreStones(portal.transform.Find("Dormant Look"), frame);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(portal.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(portal.gameObject.scene);
        return done;
    }

    private static string RestoreStones(Transform dormant, Transform frame)
    {
        Transform stones = dormant.Find("Sleeping Stones");
        var renderer = stones != null ? stones.GetComponent<MeshRenderer>() : null;
        var source = frame.GetComponent<MeshRenderer>();
        if (renderer == null || source == null)
        {
            return "no sleeping stones or active model renderer";
        }

        Undo.RecordObject(renderer, "Restore sleeping stones");
        renderer.sharedMaterials = source.sharedMaterials;
        EditorUtility.SetDirty(renderer);
        return "sleeping stones wear " + string.Join(", ", source.sharedMaterials.Select(m => m != null ? m.name + " (" + m.shader.name + ")" : "none"));
    }

    // ----- The words -----

    private static string Rewrite(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (text == "The Heart Tree")
        {
            return "The Portal";
        }

        foreach ((string from, string to) in Names)
        {
            text = text.Replace(from, to);
        }

        foreach ((string from, string to) in Lines)
        {
            text = text.Replace(from, to);
        }

        return text;
    }

    private static int RewriteObject(Object target)
    {
        var so = new SerializedObject(target);
        SerializedProperty it = so.GetIterator();
        int count = 0;
        while (it.NextVisible(true))
        {
            // Ids are what saves remember; a name is the asset's own.
            if (it.propertyType != SerializedPropertyType.String || it.name == "id" || it.name == "m_Name" || it.name == "guid")
            {
                continue;
            }

            string changed = Rewrite(it.stringValue);
            if (changed != it.stringValue)
            {
                it.stringValue = changed;
                count++;
            }
        }

        if (count > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        return count;
    }

    private static int RewriteScene(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && !(component is Transform))
                {
                    count += RewriteObject(component);
                }
            }
        }

        return count;
    }

    private static int RewriteAssets()
    {
        int count = 0;
        foreach (string folder in new[] { "Quests", "Settlement", "ResourceInventory", "Prologue", "Raids/Rumours", "Raids/Events", "Items" })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data/" + folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && RewriteObject(asset) > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int RewritePrefabs()
    {
        int count = 0;
        foreach (string path in new[] { "Assets/_Project/Prefabs/Raids/RaidPortal.prefab", "Assets/_Project/Prefabs/Resources/RootboundStone.prefab", "Assets/_Project/Prefabs/UI/RaidPreparationWindow.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = root.GetComponentsInChildren<Component>(true).Where(one => one != null && !(one is Transform)).Sum(RewriteObject);
                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return count;
    }

    private static string RenameEvents()
    {
        const string events = "Assets/_Project/Data/Quests/Events/";
        foreach ((string from, string to) in new[] { ("TalkedToHeartTree", "TalkedToPortal"), ("HeartTreeBloomed", "PortalBloomed") })
        {
            if (AssetDatabase.LoadMainAssetAtPath(events + from + ".asset") != null)
            {
                string problem = AssetDatabase.RenameAsset(events + from + ".asset", to);
                if (!string.IsNullOrEmpty(problem))
                {
                    return "rename failed: " + problem;
                }
            }
        }

        return "events renamed, ids kept";
    }

    // What a person should read: words that may still picture a tree.
    private static List<string> Leftovers(Scene scene)
    {
        var found = new List<string>();
        var pattern = new Regex(@"\bthe tree\b|\bbark\b|\bbranches\b|\bgreat tree\b|Heart Tree|beneath my|across the stream", RegexOptions.IgnoreCase);
        void Look(Object target)
        {
            SerializedProperty it = new SerializedObject(target).GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.String && it.name != "m_Name" && pattern.IsMatch(it.stringValue ?? ""))
                {
                    found.Add($"{target.name}.{it.name}: {it.stringValue}");
                }
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && !(component is Transform))
                {
                    Look(component);
                }
            }
        }

        foreach (string folder in new[] { "Quests", "Settlement", "ResourceInventory", "Prologue", "Raids/Rumours", "Raids/Events" })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data/" + folder }))
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    Look(asset);
                }
            }
        }

        return found;
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}

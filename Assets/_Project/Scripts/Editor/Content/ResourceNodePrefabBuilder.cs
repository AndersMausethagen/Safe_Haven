using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds gathering node prefabs (a tree, a rock, an ore vein) from a GatheringNodeDefinition and keeps them in step with
/// it: a collider, an Interactable with the definition's gathering animation, time and reach, a ResourceSource that gives
/// the node's yields, and a ResourceNode that checks for the right tool and grows the node back after it is gathered.
/// The definition's yield table is the source of truth: updating a prefab copies it over the prefab's drops.
/// </summary>
public static class ResourceNodePrefabBuilder
{
    public const string PrefabFolder = "Assets/_Project/Prefabs/Resources";
    public static readonly PrefabBuildUtility.Placeholder Placeholder = new PrefabBuildUtility.Placeholder(PrimitiveType.Sphere, 1f);

    public static void ValidateModel(GameObject model)
    {
        PrefabBuildUtility.ValidateModel(model, typeof(Interactable), typeof(ResourceSource), typeof(Combatant));
    }

    public static GameObject Create(GatheringNodeDefinition definition, GameObject model)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        ValidateModel(model);
        string name = definition.DisplayName.EndsWith("Node") ? definition.DisplayName : definition.DisplayName + " Node";
        return PrefabBuildUtility.CreatePrefab(PrefabFolder, name, model, Placeholder, root => Configure(root, definition));
    }

    public static void Update(GameObject prefab, GatheringNodeDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        PrefabBuildUtility.EditPrefab(prefab, root => Configure(root, definition));
    }

    public static List<string> FindProblems(GameObject prefab, GatheringNodeDefinition definition)
    {
        var problems = new List<string>();
        if (prefab == null)
        {
            return problems;
        }

        PrefabBuildUtility.AddIfMissing<Interactable>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<ResourceSource>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<ResourceNode>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<SaveableEntity>(prefab, problems);
        if (!PrefabBuildUtility.HasSolidCollider(prefab))
        {
            problems.Add("No collider, so the player can't reach it");
        }

        if (prefab.TryGetComponent(out Interactable interactable))
        {
            var data = new SerializedObject(interactable);
            if (data.FindProperty("animationType").intValue != (int)definition.GatherAnimation
                || !Mathf.Approximately(data.FindProperty("duration").floatValue, definition.GatherSeconds)
                || !Mathf.Approximately(data.FindProperty("range").floatValue, definition.GatherRange))
            {
                problems.Add("Gathering settings differ from the data");
            }

            if (data.FindProperty("deactivateOnComplete").boolValue)
            {
                problems.Add("Switches itself off when gathered, so it never grows back");
            }
        }

        if (prefab.TryGetComponent(out ResourceSource source) && !SameYields(source.Drops, definition.Yields))
        {
            problems.Add("Yields differ from the data");
        }

        if (prefab.TryGetComponent(out ResourceNode node) && node.Definition != definition)
        {
            problems.Add("Linked to a different gathering node");
        }

        return problems;
    }

    private static bool SameYields(ItemDropTable actual, ItemDropTable expected)
    {
        if (actual.Entries.Count != expected.Entries.Count)
        {
            return false;
        }

        for (int i = 0; i < actual.Entries.Count; i++)
        {
            ItemDropEntry a = actual.Entries[i];
            ItemDropEntry b = expected.Entries[i];
            if (a == null || b == null || a.Drop != b.Drop || a.MinQuantity != b.MinQuantity || a.MaxQuantity != b.MaxQuantity
                || !Mathf.Approximately(a.Probability, b.Probability))
            {
                return false;
            }
        }

        return true;
    }

    private static void Configure(GameObject root, GatheringNodeDefinition definition)
    {
        Bounds bounds = PrefabBuildUtility.CalculateLocalBounds(root);
        PrefabBuildUtility.EnsureBoxCollider(root, bounds);
        PrefabBuildUtility.GetOrAdd<SaveableEntity>(root);

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<Interactable>(root), data =>
        {
            data.FindProperty("animationType").intValue = (int)definition.GatherAnimation;
            data.FindProperty("duration").floatValue = definition.GatherSeconds;
            data.FindProperty("range").floatValue = definition.GatherRange;
            // ResourceNode hides a gathered node and grows it back; switching the object off would stop that.
            data.FindProperty("deactivateOnComplete").boolValue = false;
        });

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<ResourceSource>(root), data =>
        {
            CopyYields(definition, data.FindProperty("drops").FindPropertyRelative("entries"));
            data.FindProperty("grantOnInteractionComplete").boolValue = true;
        });

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<ResourceNode>(root),
            data => data.FindProperty("node").objectReferenceValue = definition);
    }

    /// <summary>Copies the definition's yield table into a ResourceSource's drop entries.</summary>
    public static void CopyYields(GatheringNodeDefinition definition, SerializedProperty entries)
    {
        IReadOnlyList<ItemDropEntry> yields = definition.Yields.Entries;
        entries.arraySize = yields.Count;
        for (int i = 0; i < yields.Count; i++)
        {
            ItemDropEntry yield = yields[i];
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("item").objectReferenceValue = yield != null ? yield.Item : null;
            entry.FindPropertyRelative("resource").objectReferenceValue = yield != null ? yield.Resource : null;
            entry.FindPropertyRelative("minQuantity").intValue = yield != null ? yield.MinQuantity : 0;
            entry.FindPropertyRelative("maxQuantity").intValue = yield != null ? yield.MaxQuantity : 0;
            entry.FindPropertyRelative("probability").floatValue = yield != null ? yield.Probability : 1f;
        }
    }
}

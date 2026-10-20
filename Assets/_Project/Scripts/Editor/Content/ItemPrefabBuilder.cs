using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the prefab an item uses when it lies in the world: its model, a collider and an ItemWorldObject that says
/// which item it is.
/// </summary>
public static class ItemPrefabBuilder
{
    public const string PrefabFolder = "Assets/_Project/Prefabs/Items";
    public static readonly PrefabBuildUtility.Placeholder Placeholder = new PrefabBuildUtility.Placeholder(PrimitiveType.Cube, 0.4f);

    public static void ValidateModel(GameObject model)
    {
        PrefabBuildUtility.ValidateModel(model, typeof(ItemWorldObject), typeof(Combatant));
    }

    public static GameObject Create(ItemDefinition definition, GameObject model)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        ValidateModel(model);
        return PrefabBuildUtility.CreatePrefab(PrefabFolder, definition.DisplayName, model, Placeholder, root => Configure(root, definition));
    }

    public static void Update(GameObject prefab, ItemDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        PrefabBuildUtility.EditPrefab(prefab, root => Configure(root, definition));
    }

    public static List<string> FindProblems(GameObject prefab, ItemDefinition definition)
    {
        var problems = new List<string>();
        if (prefab == null)
        {
            return problems;
        }

        PrefabBuildUtility.AddIfMissing<ItemWorldObject>(prefab, problems);
        if (!PrefabBuildUtility.HasSolidCollider(prefab))
        {
            problems.Add("No collider");
        }

        if (prefab.TryGetComponent(out ItemWorldObject item) && item.Definition != definition)
        {
            problems.Add("Linked to a different item");
        }

        return problems;
    }

    private static void Configure(GameObject root, ItemDefinition definition)
    {
        PrefabBuildUtility.EnsureBoxCollider(root, PrefabBuildUtility.CalculateLocalBounds(root));
        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<ItemWorldObject>(root),
            data => data.FindProperty("definition").objectReferenceValue = definition);
    }
}

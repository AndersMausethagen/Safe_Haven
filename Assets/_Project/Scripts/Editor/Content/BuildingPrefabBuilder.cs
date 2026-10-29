using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds building prefabs from a BuildingDefinition and keeps them in step with it: a collider, Combatant and
/// Damageable as a structure, a health bar, damage numbers, a NavMeshObstacle so monsters walk around it, and a
/// CraftingStation and an Interactable (to open its crafting menu) when the building is a workbench, smelter or other station.
/// </summary>
public static class BuildingPrefabBuilder
{
    public const string PrefabFolder = "Assets/_Project/Prefabs/Structures";
    public static readonly PrefabBuildUtility.Placeholder Placeholder = new PrefabBuildUtility.Placeholder(PrimitiveType.Cube, 2f);

    public static void ValidateModel(GameObject model)
    {
        PrefabBuildUtility.ValidateModel(model, typeof(Combatant), typeof(Damageable), typeof(MonsterAI));
    }

    public static GameObject Create(BuildingDefinition definition, GameObject model)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        ValidateModel(model);
        return PrefabBuildUtility.CreatePrefab(PrefabFolder, definition.DisplayName, model, Placeholder, root => Configure(root, definition));
    }

    public static void Update(GameObject prefab, BuildingDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        PrefabBuildUtility.EditPrefab(prefab, root => Configure(root, definition));
    }

    public static List<string> FindProblems(GameObject prefab, BuildingDefinition definition)
    {
        var problems = new List<string>();
        if (prefab == null)
        {
            return problems;
        }

        PrefabBuildUtility.AddIfMissing<Combatant>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<Damageable>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<DamageNumberSpawner>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<BuildingObject>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<NavMeshObstacle>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<SaveableEntity>(prefab, problems);
        if (prefab.GetComponentInChildren<WorldHealthBar>(true) == null)
        {
            problems.Add("No health bar");
        }

        if (prefab.TryGetComponent(out Combatant combatant))
        {
            var data = new SerializedObject(combatant);
            if (data.FindProperty("faction").enumValueIndex != (int)definition.Faction)
            {
                problems.Add("Faction differs from the data");
            }

            if (data.FindProperty("baseStats").objectReferenceValue != definition.CombatStats)
            {
                problems.Add("Combat stats differ from the data");
            }
        }

        if (prefab.TryGetComponent(out BuildingObject building) && building.Definition != definition)
        {
            problems.Add("Linked to different building data");
        }

        prefab.TryGetComponent(out CraftingStation station);
        if (definition.Station != CraftingStationType.None && (station == null || station.Type != definition.Station))
        {
            problems.Add($"Isn't set up as {definition.Station.WithArticle()}");
        }
        else if (definition.Station != CraftingStationType.None && !prefab.TryGetComponent(out Interactable _))
        {
            problems.Add("Has no Interactable, so its crafting menu can't be opened");
        }
        else if (definition.Station == CraftingStationType.None && station != null)
        {
            problems.Add("Is still a crafting station");
        }

        return problems;
    }

    private static void Configure(GameObject root, BuildingDefinition definition)
    {
        Bounds bounds = PrefabBuildUtility.CalculateLocalBounds(root);
        PrefabBuildUtility.EnsureBoxCollider(root, bounds);
        PrefabBuildUtility.GetOrAdd<SaveableEntity>(root);

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<Combatant>(root), data =>
        {
            data.FindProperty("faction").enumValueIndex = (int)definition.Faction;
            data.FindProperty("baseStats").objectReferenceValue = definition.CombatStats;
        });

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<Damageable>(root, out bool damageableAdded), data =>
        {
            if (damageableAdded)
            {
                data.FindProperty("category").enumValueIndex = (int)TargetCategory.Structure;
            }
        });

        PrefabBuildUtility.EnsureDamageNumbers(root, bounds);
        PrefabBuildUtility.EnsureHealthBar(root, bounds);

        NavMeshObstacle obstacle = PrefabBuildUtility.GetOrAdd<NavMeshObstacle>(root, out bool obstacleAdded);
        if (obstacleAdded)
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center;
            obstacle.size = bounds.size;
            obstacle.carving = true;
        }

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<BuildingObject>(root),
            data => data.FindProperty("definition").objectReferenceValue = definition);

        CraftingStation station = root.GetComponent<CraftingStation>();
        if (definition.Station == CraftingStationType.None)
        {
            if (station != null)
            {
                UnityEngine.Object.DestroyImmediate(station, true);
            }
        }
        else
        {
            PrefabBuildUtility.Configure(station != null ? station : root.AddComponent<CraftingStation>(),
                data => data.FindProperty("type").intValue = (int)definition.Station);
            PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<Interactable>(root, out bool interactableAdded), data =>
            {
                data.FindProperty("deactivateOnComplete").boolValue = false;
                // Opening a station is a Use: done on the press.
                data.FindProperty("kind").intValue = (int)InteractionKind.Use;
                if (interactableAdded)
                {
                    data.FindProperty("duration").floatValue = 0.3f;
                    data.FindProperty("range").floatValue = 1.8f;
                }
            });
        }
    }
}

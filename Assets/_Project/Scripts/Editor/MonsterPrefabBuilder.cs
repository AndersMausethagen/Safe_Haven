using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds monster prefabs from a MonsterDefinition and keeps them in step with it. A monster prefab carries everything it
/// needs to fight: Combatant, Damageable (its IDamageable), MonsterAI on a NavMeshAgent, loot on death, a health bar,
/// damage numbers, an Animator with a controller and an animation driver. Updating an existing prefab only adds what is
/// missing and re-applies the definition's data, so its model and any manual tuning are kept.
/// </summary>
public static class MonsterPrefabBuilder
{
    public const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";
    public const string DefaultControllerPath = "Assets/_Project/Animation/Controllers/MonsterHumanoid.controller";
    public static readonly PrefabBuildUtility.Placeholder Placeholder = new PrefabBuildUtility.Placeholder(PrimitiveType.Capsule, 1f);
    private const float BaseOffsetThreshold = 0.25f;

    /// <summary>Throws ArgumentException when the model can't be used as a monster's visual.</summary>
    public static void ValidateModel(GameObject model)
    {
        PrefabBuildUtility.ValidateModel(model, typeof(Combatant), typeof(Damageable), typeof(MonsterAI));
    }

    /// <summary>Saves a new monster prefab with the model as a child, or a capsule when there is no model.</summary>
    public static GameObject Create(MonsterDefinition definition, GameObject model)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        ValidateModel(model);
        return PrefabBuildUtility.CreatePrefab(PrefabFolder, definition.DisplayName, model, Placeholder, root => Configure(root, definition));
    }

    /// <summary>Adds any missing monster components to an existing prefab and re-applies the definition's data.</summary>
    public static void Update(GameObject prefab, MonsterDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        PrefabBuildUtility.EditPrefab(prefab, root => Configure(root, definition));
    }

    /// <summary>The controller monsters get when their definition doesn't name one.</summary>
    public static RuntimeAnimatorController LoadDefaultController()
    {
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DefaultControllerPath);
    }

    /// <summary>What is missing or out of date on a monster prefab, for the editor to show. Empty means up to date.</summary>
    public static List<string> FindProblems(GameObject prefab, MonsterDefinition definition)
    {
        var problems = new List<string>();
        if (prefab == null)
        {
            return problems;
        }

        PrefabBuildUtility.AddIfMissing<Combatant>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<Damageable>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<NavMeshAgent>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<MonsterAI>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<LootOnDeath>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<DamageNumberSpawner>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<MonsterAnimationDriver>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<AttackTelegraph>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<NavMeshModifier>(prefab, problems);
        PrefabBuildUtility.AddIfMissing<SaveableEntity>(prefab, problems);
        if (prefab.GetComponent<ResourceDropOnDeath>() != null || prefab.GetComponent<ResourceSource>() != null)
        {
            problems.Add("Still drops raw resources (old setup); monsters drop items now");
        }
        if (prefab.GetComponentInChildren<WorldHealthBar>(true) == null)
        {
            problems.Add("No health bar");
        }

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            problems.Add("No animator controller");
        }
        else if (definition.AnimatorController != null && animator.runtimeAnimatorController != definition.AnimatorController)
        {
            problems.Add("Animator controller differs from the data");
        }

        if (prefab.TryGetComponent(out Combatant combatant))
        {
            var combatantData = new SerializedObject(combatant);
            if (combatantData.FindProperty("faction").enumValueIndex != (int)definition.Faction)
            {
                problems.Add("Faction differs from the data");
            }

            if (combatantData.FindProperty("baseStats").objectReferenceValue != definition.CombatStats)
            {
                problems.Add("Combat stats differ from the data");
            }
        }

        if (prefab.TryGetComponent(out MonsterAI ai) && ai.Definition != definition)
        {
            problems.Add("Linked to different monster data");
        }

        if (prefab.TryGetComponent(out NavMeshAgent agent) && !Mathf.Approximately(agent.speed, definition.MoveSpeed))
        {
            problems.Add("Move speed differs from the data");
        }

        return problems;
    }

    private static void Configure(GameObject root, MonsterDefinition definition)
    {
        Bounds bounds = PrefabBuildUtility.CalculateLocalBounds(root);
        PrefabBuildUtility.EnsureCapsuleCollider(root, bounds);
        PrefabBuildUtility.GetOrAdd<SaveableEntity>(root);
        ConfigureAgent(root, definition, bounds);

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<Combatant>(root), data =>
        {
            data.FindProperty("faction").enumValueIndex = (int)definition.Faction;
            data.FindProperty("baseStats").objectReferenceValue = definition.CombatStats;
        });

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<Damageable>(root, out bool damageableAdded), data =>
        {
            if (damageableAdded)
            {
                data.FindProperty("category").enumValueIndex = (int)TargetCategory.Creature;
            }
        });

        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<MonsterAI>(root),
            data => data.FindProperty("definition").objectReferenceValue = definition);

        // Monsters used to drop raw resources; they drop items now.
        RemoveIfPresent<ResourceDropOnDeath>(root);
        RemoveIfPresent<ResourceSource>(root);
        PrefabBuildUtility.Configure(PrefabBuildUtility.GetOrAdd<LootOnDeath>(root), data =>
        {
            var definitionData = new SerializedObject(definition);
            data.CopyFromSerializedProperty(definitionData.FindProperty("drops"));
            data.FindProperty("table").objectReferenceValue = definitionData.FindProperty("lootTable").objectReferenceValue;
            data.CopyFromSerializedProperty(definitionData.FindProperty("unlocksRecipes"));
        });

        PrefabBuildUtility.EnsureDamageNumbers(root, bounds);
        PrefabBuildUtility.GetOrAdd<MonsterAnimationDriver>(root);
        PrefabBuildUtility.GetOrAdd<AttackTelegraph>(root);

        // Monsters move, so a NavMesh baked with one standing in the scene must not leave a hole where it stood.
        PrefabBuildUtility.GetOrAdd<NavMeshModifier>(root).ignoreFromBuild = true;

        ConfigureAnimator(root, definition);
        PrefabBuildUtility.EnsureHealthBar(root, bounds);
    }

    // Every monster gets an Animator with a controller: the definition's, or the default humanoid one.
    private static void ConfigureAnimator(GameObject root, MonsterDefinition definition)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Transform model = root.transform.Find(PrefabBuildUtility.ModelChildName);
            animator = (model != null ? model.gameObject : root).AddComponent<Animator>();
        }

        RuntimeAnimatorController controller = definition.AnimatorController != null
            ? definition.AnimatorController
            : LoadDefaultController();
        if (controller == null)
        {
            Debug.LogWarning($"No monster animator controller at {DefaultControllerPath}; {root.name} has no animations.");
        }
        else if (definition.AnimatorController != null || animator.runtimeAnimatorController == null)
        {
            animator.runtimeAnimatorController = controller;
        }

        // The NavMeshAgent moves the monster; animation must not drag the model away from it.
        foreach (Animator child in root.GetComponentsInChildren<Animator>(true))
        {
            child.applyRootMotion = false;
        }
    }

    private static void RemoveIfPresent<T>(GameObject root) where T : Component
    {
        if (root.TryGetComponent(out T component))
        {
            UnityEngine.Object.DestroyImmediate(component, true);
        }
    }

    private static void ConfigureAgent(GameObject root, MonsterDefinition definition, Bounds bounds)
    {
        NavMeshAgent agent = PrefabBuildUtility.GetOrAdd<NavMeshAgent>(root, out bool added);
        agent.speed = definition.MoveSpeed;
        if (!added)
        {
            return;
        }

        var capsule = root.GetComponent<CapsuleCollider>();
        Vector3 scale = root.transform.lossyScale;
        if (capsule != null)
        {
            agent.radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            agent.height = capsule.height * Mathf.Abs(scale.y);
        }
        else
        {
            agent.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f, 0.2f, 1.5f);
            agent.height = Mathf.Max(0.5f, bounds.size.y);
        }

        // Only a model clearly centred on its pivot needs lifting; skinned meshes often pad their bounds a little.
        agent.baseOffset = bounds.min.y < -BaseOffsetThreshold ? -bounds.min.y : 0f;
        agent.angularSpeed = 720f;
        agent.acceleration = 24f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
    }
}

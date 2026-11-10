using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Game to hunt (`unity command script run --file AgentScripts/BuildGame.cs --entry BuildGame.Apply`): Deer, Stag, Fox and Wild
/// Boar as real creatures (MonsterDefinition + prefab), where the deer, stag and fox were only scenery that bolted and was
/// gone (Wildlife). They take no side (Faction Neutral, so nothing of theirs ever starts or returns a fight) and are Timid:
/// they run when struck and when they see the player coming, so they are hunted by sneaking close or with a bow. They graze,
/// wander and sleep like any creature (Idle Life), and drop hide, bone and meat, never essence. The Haven world's recipe
/// spawns them in its game zones (BuildHavenWorldRecipe, run after this). Taming is not built: these are what it will tame.
/// Bodies: the CC0 Quaternius deer, stag and fox already in Art/Wildlife, each with a small controller of its own on the
/// beasts' parameters; the Wild Boar wears the Blight Boar's body. Re-runnable.
/// </summary>
public static class BuildGame
{
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Controllers = "Assets/_Project/Animation/Controllers/Beasts/";
    private const string Wildlife = "Assets/_Project/Art/Wildlife/";
    private const string Items = "Assets/_Project/Data/Items/Materials/";

    public static string Apply()
    {
        var made = new System.Collections.Generic.List<string>
        {
            Beast("Deer", "Deer", "A hind of the Haven's meadows. She lifts her head at a snapped twig and is gone before you have taken three steps: come at her low and from downwind, or bring a bow.",
                  Wildlife + "Deer/Deer.fbx", 0.36f, 1.35f, 0.42f, 45f, 6.6f, 14f, 4.5f, 1.3f, ("RoughHide", 1, 2, 1f), ("Bone", 1, 2, 0.7f)),
            Beast("Stag", "Stag", "A stag with a full head of tines. Warier than the hinds and faster, and worth the patience: a thicker hide and more bone.",
                  Wildlife + "Stag/Stag.fbx", 0.4f, 1.6f, 0.48f, 70f, 7f, 16f, 5f, 1.5f, ("ThickHide", 1, 1, 0.8f), ("RoughHide", 1, 2, 1f), ("Bone", 2, 3, 0.9f)),
            Beast("Fox", "Fox", "A fox, about from dusk. It keeps to the edges of things and slips off between the rocks at the first sight of you.",
                  Wildlife + "Fox/Fox.fbx", 0.22f, 0.6f, 0.3f, 22f, 6.2f, 12f, 3.5f, 1.4f, ("RoughHide", 1, 1, 0.9f), ("Bone", 1, 1, 0.4f)),
            WildBoar(),
        };
        AssetDatabase.SaveAssets();
        return "Game: " + string.Join("; ", made);
    }

    // A grazing beast from a Generic model with Idle, Walk, Gallop, Eating and Death clips.
    private static string Beast(string id, string label, string text, string modelPath, float modelScale, float height, float radius, float health, float speed,
                                float sight, float close, float hearing, params (string item, int min, int max, float chance)[] drops)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) ?? throw new Exception("No model at " + modelPath);
        RuntimeAnimatorController controller = Controller(id, modelPath);
        MonsterDefinition definition = Definition(id, label, text, health, speed, sight, close, hearing, drops);
        Edit(definition, so =>
        {
            so.FindProperty("animatorController").objectReferenceValue = controller;
            so.FindProperty("fallsByAnimation").boolValue = true;
        });

        GameObject prefab = definition.Prefab;
        if (prefab == null)
        {
            prefab = MonsterPrefabBuilder.Create(definition, model);
            Edit(definition, so => so.FindProperty("prefab").objectReferenceValue = prefab);
        }

        MonsterPrefabBuilder.Update(prefab, definition);
        GameObject root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        try
        {
            // Sized as the scenery's animals are, with a body to match: capsule, agent and stride.
            Animator animator = root.GetComponentInChildren<Animator>(true);
            Transform body = animator != null && animator.transform != root.transform ? animator.transform : root.transform.Cast<Transform>().FirstOrDefault(child => child.GetComponentInChildren<Renderer>(true) != null);
            if (body != null && body != root.transform)
            {
                body.localScale = Vector3.one * modelScale;
            }

            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }

            if (root.TryGetComponent(out CapsuleCollider capsule))
            {
                capsule.height = height;
                capsule.radius = radius;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);
            }

            if (root.TryGetComponent(out NavMeshAgent agent))
            {
                agent.height = height;
                agent.radius = radius * 0.85f;
            }

            if (root.TryGetComponent(out MonsterAnimationDriver driver))
            {
                var data = new SerializedObject(driver);
                data.FindProperty("strideSpeed").floatValue = speed;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GetAssetPath(prefab));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return $"{label} ({AssetDatabase.GetAssetPath(prefab)})";
    }

    // The Blight Boar's body with a mind of its own: a copy of its prefab, given the Wild Boar's definition.
    private static string WildBoar()
    {
        MonsterDefinition definition = Definition("WildBoar", "Wild Boar", "A boar of the deep woods, untouched by the blight. It roots about in the litter and runs from you, tusks and all. The best meat there is.",
            80f, 5.6f, 11f, 3.5f, 1.1f, ("BoarMeat", 2, 3, 1f), ("RoughHide", 1, 2, 0.8f), ("BoarTusk", 1, 1, 0.35f));
        string path = Enemies + "WildBoar.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(Enemies + "BlightBoar.prefab", path))
        {
            throw new Exception("Could not copy the Blight Boar's prefab.");
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var blight = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + "BlightBoar.asset");
        Edit(definition, so =>
        {
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("animatorController").objectReferenceValue = new SerializedObject(blight).FindProperty("animatorController").objectReferenceValue;
        });
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ai = new SerializedObject(root.GetComponent<MonsterAI>());
            ai.FindProperty("definition").objectReferenceValue = definition;
            ai.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        MonsterPrefabBuilder.Update(prefab, definition);
        return $"Wild Boar ({path})";
    }

    // A definition for game: copied from the Boar's (so every field a creature needs is sensible), then made neutral, timid and its own.
    private static MonsterDefinition Definition(string id, string label, string text, float health, float speed, float sight, float close, float hearing,
                                                params (string item, int min, int max, float chance)[] drops)
    {
        string path = Monsters + id + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(path);
        if (definition == null)
        {
            if (!AssetDatabase.CopyAsset(Monsters + "Boar.asset", path))
            {
                throw new Exception("Could not copy the Boar's definition.");
            }

            definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(path);
            Edit(definition, so => so.FindProperty("prefab").objectReferenceValue = null);
        }

        CombatStatsDefinition stats = Stats(id + "Stats", health);
        Edit(definition, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("combatStats").objectReferenceValue = stats;
            so.FindProperty("faction").intValue = (int)Faction.Neutral;
            so.FindProperty("neverElite").boolValue = true;
            so.FindProperty("attack").objectReferenceValue = null;
            so.FindProperty("attacks").arraySize = 0;
            so.FindProperty("timid").boolValue = true;
            so.FindProperty("boltSeconds").floatValue = 7f;
            so.FindProperty("moveSpeed").floatValue = speed;
            so.FindProperty("detectionRange").floatValue = sight;
            so.FindProperty("chaseRange").floatValue = sight + 4f;
            so.FindProperty("leashRange").floatValue = 0f;
            so.FindProperty("sightAngle").floatValue = 220f;
            so.FindProperty("closeSenseRange").floatValue = close;
            so.FindProperty("hearing").floatValue = hearing;
            so.FindProperty("alertness").floatValue = 1.2f;
            so.FindProperty("idleWanderRadius").floatValue = 9f;
            so.FindProperty("idlePauseSeconds").vector2Value = new Vector2(3f, 9f);
            so.FindProperty("idleWalkSpeed").floatValue = 0.22f;
            so.FindProperty("idleGrazeChance").floatValue = 0.65f;
            so.FindProperty("idleSleepChance").floatValue = 0.06f;
            so.FindProperty("canBeFinished").boolValue = false;
            so.FindProperty("journalHabitat").stringValue = "The Haven's meadows and the glades of the woods.";
            SerializedProperty entries = so.FindProperty("drops").FindPropertyRelative("entries");
            entries.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + drops[i].item + ".asset") ?? throw new Exception("No item " + drops[i].item);
                entry.FindPropertyRelative("resource").objectReferenceValue = null;
                entry.FindPropertyRelative("minQuantity").intValue = drops[i].min;
                entry.FindPropertyRelative("maxQuantity").intValue = drops[i].max;
                entry.FindPropertyRelative("probability").floatValue = drops[i].chance;
            }
        });
        return definition;
    }

    private static CombatStatsDefinition Stats(string file, float health)
    {
        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(new SerializedObject(AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + "Boar.asset"))
            .FindProperty("combatStats").objectReferenceValue)).Replace('\\', '/');
        string path = $"{folder}/{file}.asset";
        var stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path);
        if (stats == null)
        {
            string template = AssetDatabase.GetAssetPath(new SerializedObject(AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + "Boar.asset")).FindProperty("combatStats").objectReferenceValue);
            if (!AssetDatabase.CopyAsset(template, path))
            {
                throw new Exception("Could not copy the Boar's stats.");
            }

            stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path);
        }

        // Health is the first number a stats asset carries; whatever it is called, set every property named like it.
        var data = new SerializedObject(stats);
        SerializedProperty property = data.GetIterator();
        bool set = false;
        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.Float && (property.name == "maxHealth" || property.name == "health" || property.name == "baseHealth"))
            {
                property.floatValue = health;
                set = true;
            }
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stats);
        if (!set)
        {
            Debug.LogWarning($"BuildGame: found no health field on {path}; it keeps the Boar's.");
        }

        return stats;
    }

    // The beasts' parameters (MonsterAnimationDriver): Speed, Attack, Dead, Stagger, ActionSpeed, StrideRate and Resting (its grazing).
    private static RuntimeAnimatorController Controller(string id, string modelPath)
    {
        string path = $"{Controllers}{id}.controller";
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        AnimationClip Clip(string name) => clips.FirstOrDefault(c => c.name == name);
        if (Clip("Idle") == null || Clip("Walk") == null)
        {
            throw new Exception($"{modelPath} has no Idle and Walk clips.");
        }

        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter(new AnimatorControllerParameter { name = "ActionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        controller.AddParameter(new AnimatorControllerParameter { name = "StrideRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        controller.AddParameter(MonsterAnimationDriver.RestingParameter, AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip("Idle"), 0f);
        tree.AddChild(Clip("Walk"), 1f);
        if (Clip("Gallop") != null)
        {
            tree.AddChild(Clip("Gallop"), 3.2f);
        }

        machine.defaultState = locomotion;
        locomotion.speedParameter = "StrideRate";
        locomotion.speedParameterActive = true;

        AnimationClip hit = Clip("Idle_HitReact_Left");
        if (hit != null)
        {
            AnimatorState stagger = machine.AddState("Stagger");
            stagger.motion = hit;
            AnimatorStateTransition into = machine.AddAnyStateTransition(stagger);
            into.AddCondition(AnimatorConditionMode.If, 0f, "Stagger");
            into.duration = 0.05f;
            into.canTransitionToSelf = false;
            AnimatorStateTransition back = stagger.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.1f;
        }

        AnimationClip eating = Clip("Eating");
        if (eating != null)
        {
            AnimatorState rest = machine.AddState("Rest");
            rest.motion = eating;
            AnimatorStateTransition down = locomotion.AddTransition(rest);
            down.hasExitTime = false;
            down.duration = 0.4f;
            down.AddCondition(AnimatorConditionMode.If, 0f, MonsterAnimationDriver.RestingParameter);
            AnimatorStateTransition up = rest.AddTransition(locomotion);
            up.hasExitTime = false;
            up.duration = 0.25f;
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, MonsterAnimationDriver.RestingParameter);
        }

        AnimationClip death = Clip("Death");
        if (death != null)
        {
            AnimatorState dead = machine.AddState("Death");
            dead.motion = death;
            AnimatorStateTransition into = machine.AddAnyStateTransition(dead);
            into.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            into.duration = 0.08f;
            into.canTransitionToSelf = false;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void Edit(UnityEngine.Object asset, Action<SerializedObject> change)
    {
        var data = new SerializedObject(asset);
        change(data);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }
}

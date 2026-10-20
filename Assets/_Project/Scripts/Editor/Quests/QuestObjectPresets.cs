using UnityEditor;
using UnityEngine;

/// <summary>
/// GameObject > Safe Haven: ready-made quest objects with every component already set up, so a designer only has to
/// pick the quest or Quest Event. Each is placed at the Scene view's centre (or under the selected object), selected,
/// and can be undone.
/// </summary>
public static class QuestObjectPresets
{
    private const string Menu = "GameObject/Safe Haven/";
    private const int Priority = 10;

    [MenuItem(Menu + "Quest Giver", false, Priority)]
    private static void CreateQuestGiver(MenuCommand command)
    {
        GameObject giver = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        giver.name = "Quest Giver";
        AddInteractable(giver, InteractionAnimation.Talk);
        giver.AddComponent<QuestGiver>();
        Place(giver, command, "Create Quest Giver");
    }

    [MenuItem(Menu + "Quest Inspect Point", false, Priority + 1)]
    private static void CreateInspectPoint(MenuCommand command)
    {
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Cube);
        point.name = "Quest Inspect Point";
        point.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        AddInteractable(point, InteractionAnimation.GatherLow);
        AddTrigger(point, TriggerWhen.PlayerInteracts);
        Place(point, command, "Create Quest Inspect Point");
    }

    [MenuItem(Menu + "Quest Zone", false, Priority + 2)]
    private static void CreateZone(MenuCommand command)
    {
        var zone = new GameObject("Quest Zone");
        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(6f, 3f, 6f);
        box.center = new Vector3(0f, 1.5f, 0f);
        AddTrigger(zone, TriggerWhen.PlayerEntersZone);
        Place(zone, command, "Create Quest Zone");
    }

    [MenuItem(Menu + "Quest Event Listener", false, Priority + 3)]
    private static void CreateListener(MenuCommand command)
    {
        var listener = new GameObject("Quest Event Listener");
        listener.AddComponent<GameEventListener>();
        Place(listener, command, "Create Quest Event Listener");
    }

    [MenuItem(Menu + "World Event (Zone + Response)", false, Priority + 4)]
    private static void CreateWorldEvent(MenuCommand command)
    {
        var worldEvent = new GameObject("World Event");
        BoxCollider box = worldEvent.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(6f, 3f, 6f);
        box.center = new Vector3(0f, 1.5f, 0f);
        AddTrigger(worldEvent, TriggerWhen.PlayerEntersZone);
        worldEvent.AddComponent<GameEventListener>();
        Place(worldEvent, command, "Create World Event");
    }

    [MenuItem(Menu + "Ward Moss Bloom", false, Priority + 5)]
    private static void CreateWardMossBloom(MenuCommand command)
    {
        GameObject bloom = BuildWardMossBloom(null, null);
        Place(bloom, command, "Create Ward Moss Bloom");
    }

    /// <summary>
    /// A world event that blooms terrain moss across the Heart Tree's ward the first time the player stands in it:
    /// a once-per-game ward trigger, a listener (Bloom when raised, Bloom Instantly on load) and a particle child.
    /// Pick the Quest Event (used by both trigger and listener) and, optionally, the quest it waits for.
    /// </summary>
    public static GameObject BuildWardMossBloom(GameEventDefinition gameEvent, QuestDefinition requiredQuest)
    {
        var root = new GameObject("Ward Moss Bloom");
        var trigger = new SerializedObject(root.AddComponent<GameEventTrigger>());
        trigger.FindProperty("gameEvent").objectReferenceValue = gameEvent;
        trigger.FindProperty("when").intValue = (int)TriggerWhen.PlayerInsideWard;
        trigger.FindProperty("repeat").intValue = (int)TriggerRepeat.OncePerGame;
        trigger.FindProperty("requires.quest").objectReferenceValue = requiredQuest;
        trigger.FindProperty("requires.requirement").intValue = (int)QuestGate.Requirement.Completed;
        trigger.ApplyModifiedPropertiesWithoutUndo();

        TerrainMossBloom bloom = root.AddComponent<TerrainMossBloom>();
        ParticleSystem particles = CreateBloomParticles(root.transform);
        var bloomData = new SerializedObject(bloom);
        bloomData.FindProperty("particles").objectReferenceValue = particles;
        bloomData.ApplyModifiedPropertiesWithoutUndo();

        GameEventListener listener = root.AddComponent<GameEventListener>();
        var listenerData = new SerializedObject(listener);
        listenerData.FindProperty("listenFor").intValue = (int)ListenFor.QuestEvent;
        listenerData.FindProperty("gameEvent").objectReferenceValue = gameEvent;
        listenerData.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(GetEvent(listener, "response"), bloom.Bloom);
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(GetEvent(listener, "alreadyHappenedResponse"), bloom.BloomInstantly);
        return root;
    }

    private static UnityEngine.Events.UnityEvent GetEvent(GameEventListener listener, string field)
    {
        return (UnityEngine.Events.UnityEvent)typeof(GameEventListener)
            .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(listener);
    }

    private static ParticleSystem CreateBloomParticles(Transform parent)
    {
        var child = new GameObject("Bloom Particles");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        child.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem particles = child.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.8f, 0.25f), new Color(0.75f, 0.95f, 0.45f));
        main.gravityModifier = -0.05f;
        main.maxParticles = 600;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 150f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1f;
        shape.radiusThickness = 0.15f;

        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        return particles;
    }

    private const string ParticleMaterialPath = "Assets/_Project/Prefabs/Effects/Gathering/GatheringParticle.mat";

    private static void AddInteractable(GameObject target, InteractionAnimation animation)
    {
        var data = new SerializedObject(target.AddComponent<Interactable>());
        data.FindProperty("animationType").intValue = (int)animation;
        data.FindProperty("duration").floatValue = 0.4f;
        data.FindProperty("deactivateOnComplete").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddTrigger(GameObject target, TriggerWhen when)
    {
        var data = new SerializedObject(target.AddComponent<GameEventTrigger>());
        data.FindProperty("when").intValue = (int)when;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Place(GameObject created, MenuCommand command, string undoName)
    {
        if (command.context is GameObject parent)
        {
            Vector3 scale = created.transform.localScale;
            GameObjectUtility.SetParentAndAlign(created, parent);
            created.transform.localScale = scale;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            created.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        Undo.RegisterCreatedObjectUndo(created, undoName);
        Selection.activeGameObject = created;
    }
}

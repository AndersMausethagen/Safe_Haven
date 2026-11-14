#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The fishing fight's content (`unity command script run --file AgentScripts/BuildFishingFight.cs --entry BuildFishingFight.Apply`):
/// - PlayerAnimator: a FishSpeed parameter driving Fish Begin, Fish Cast and Fish Fight (the cast holds at the top of the
///   backswing, the fight jerks faster on a run); the landings (Fish Catch, Fish Haul) play out to the end and then go
///   back to the ready pose instead of cutting to Fish Stop, whose rod-out start doesn't match their rod-down end.
/// - The hooked fish's material (Art/Generated/Fishing/Hooked Fish), and each fish's colour on the line.
/// - The fight's sounds from Tools/Audio/fishing.py (Reel Turn, Line Strain, Line Snap, Fish Splash) as sound definitions,
///   and the bite's splash; the Water Splash effect for runs and landings.
/// Re-runnable.
/// </summary>
public static class BuildFishingFight
{
    private const string Controller = "Assets/_Project/Animation/Controllers/PlayerAnimator.controller";
    private const string Settings = "Assets/_Project/Resources/FishingSettings.asset";
    private const string Sfx = "Assets/_Project/Audio/SFX/Safe Haven Fishing/";
    private const string Library = "Assets/_Project/Audio/Sounds/Library/";
    private const string FishMaterial = "Assets/_Project/Art/Generated/Fishing/Hooked Fish.mat";
    private const string Splash = "Assets/_Project/Art/Generated/Water/Water Splash.prefab";

    public static string Apply()
    {
        var report = new List<string>();
        Animator(report);
        Material fish = Material();
        var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<FishingSettings>(Settings));
        settings.FindProperty("reelSound").objectReferenceValue = Sound("Reel Turn", "Reel clicks", 0.35f, 2f, 10f, 2, 0.1f);
        settings.FindProperty("strainSound").objectReferenceValue = Sound("Line Strain", "Line creaks", 0.5f, 3f, 14f, 1, 0.2f);
        settings.FindProperty("snapSound").objectReferenceValue = Sound("Line Snap", "Line snaps", 0.7f, 4f, 20f, 1, 0.2f);
        SoundDefinition splash = Sound("Fish Splash", "Splash", 0.6f, 5f, 26f, 3, 0.12f);
        settings.FindProperty("splashSound").objectReferenceValue = splash;
        settings.FindProperty("biteSound").objectReferenceValue = splash;
        settings.FindProperty("splashEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Splash);
        settings.FindProperty("fishMaterial").objectReferenceValue = fish;
        report.Add("sounds, splash and the fish's material set");

        var colours = new Dictionary<string, Color>
        {
            ["BrookTrout"] = new Color(0.46f, 0.43f, 0.3f),
            ["Perch"] = new Color(0.52f, 0.56f, 0.3f),
            ["Pike"] = new Color(0.33f, 0.41f, 0.25f),
            ["ArcticChar"] = new Color(0.55f, 0.36f, 0.3f),
        };
        SerializedProperty fishes = settings.FindProperty("fish");
        for (int i = 0; i < fishes.arraySize; i++)
        {
            SerializedProperty entry = fishes.GetArrayElementAtIndex(i);
            Object item = entry.FindPropertyRelative("item").objectReferenceValue;
            if (item != null && colours.TryGetValue(item.name, out Color colour))
            {
                entry.FindPropertyRelative("colour").colorValue = colour;
                report.Add(item.name + " coloured");
            }
        }

        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static void Animator(List<string> report)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if (controller.parameters.All(p => p.name != "FishSpeed"))
        {
            controller.AddParameter(new AnimatorControllerParameter { name = "FishSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        }

        AnimatorStateMachine machine = controller.layers.First(l => l.name == "Interaction").stateMachine;
        AnimatorState State(string name) => machine.states.First(s => s.state.name == name).state;
        foreach (string name in new[] { "Fish Begin", "Fish Cast", "Fish Fight" })
        {
            AnimatorState state = State(name);
            state.speedParameter = "FishSpeed";
            state.speedParameterActive = true;
        }

        AnimatorState none = State("None");
        // Fish Catch ends with the rod down: when let go it plays out and goes back to the ready pose.
        AnimatorState caught = State("Fish Catch");
        foreach (AnimatorStateTransition old in caught.transitions.Where(t => t.conditions.Any(c => c.parameter == "Interacting" && c.mode == AnimatorConditionMode.IfNot)).ToArray())
        {
            caught.RemoveTransition(old);
        }

        First(caught, ToNoneWhenLetGo(caught, none, 0.95f, 0.2f));

        // A heavy fish held up: when let go, Haul Stop lowers it before anything else.
        AnimatorState loop = State("Fish Haul Loop");
        AnimatorStateTransition stop = loop.transitions.First(t => t.destinationState != null && t.destinationState.name == "Fish Haul Stop");
        First(loop, stop);

        AnimatorState haulStop = State("Fish Haul Stop");
        foreach (AnimatorStateTransition old in haulStop.transitions.Where(t => t.conditions.Any(c => c.parameter == "Interacting" && c.mode == AnimatorConditionMode.IfNot)).ToArray())
        {
            haulStop.RemoveTransition(old);
        }

        First(haulStop, ToNoneWhenLetGo(haulStop, none, 0.9f, 0.1f));
        EditorUtility.SetDirty(controller);
        report.Add("animator: FishSpeed on the cast and fight, landings play out");
    }

    private static AnimatorStateTransition ToNoneWhenLetGo(AnimatorState from, AnimatorState none, float exitTime, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(none);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Interacting");
        return transition;
    }

    // Transitions are tried in order: this one first.
    private static void First(AnimatorState state, AnimatorStateTransition transition)
    {
        List<AnimatorStateTransition> list = state.transitions.ToList();
        list.Remove(transition);
        list.Insert(0, transition);
        state.transitions = list.ToArray();
    }

    private static Material Material()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FishMaterial);
        if (material == null)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FishMaterial));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hooked Fish" };
            AssetDatabase.CreateAsset(material, FishMaterial);
        }

        // Wet scales: glossy, a little silvery.
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.78f);
        material.SetFloat("_Metallic", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static SoundDefinition Sound(string name, string caption, float volume, float full, float audible, int atOnce, float gap)
    {
        string path = Library + name + ".asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        AudioClip[] clips = AssetDatabase.FindAssets("t:AudioClip", new[] { Sfx.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileNameWithoutExtension(p).StartsWith(name + " "))
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();
        var so = new SerializedObject(sound);
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("caption").stringValue = caption;
        SerializedProperty list = so.FindProperty("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        so.FindProperty("channel").enumValueIndex = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("volumeVariation").floatValue = 0.08f;
        so.FindProperty("pitchRange").vector2Value = new Vector2(0.94f, 1.06f);
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = atOnce;
        so.FindProperty("minimumGap").floatValue = gap;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// What work looks and sounds like in the Haven: each job's tool in the hand and its sound, and the moment in a repeated gesture
/// that the sound lands, read off the clip as the lowest point of the right hand. Run once after Tools/Audio/work.py.
/// Run: unity command script run -- --file AgentScripts/BuildHavenWork.cs --entry BuildHavenWork.Run
/// </summary>
public static class BuildHavenWork
{
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Work/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";

    public static string Run()
    {
        AssetDatabase.Refresh();
        SoundDefinition anvil = Sound("Work Anvil", 4, 0.62f, 4f, 42f);
        SoundDefinition mallet = Sound("Work Mallet", 4, 0.5f, 3f, 28f);
        SoundDefinition chisel = Sound("Work Chisel", 4, 0.42f, 3f, 26f);
        SoundDefinition chop = Sound("Work Chop", 4, 0.42f, 2.5f, 18f);
        SoundDefinition dig = Sound("Work Dig", 4, 0.42f, 2.5f, 18f);
        SoundDefinition hide = Sound("Work Hide", 3, 0.36f, 2f, 14f);
        SoundDefinition glass = Sound("Work Glass", 4, 0.3f, 2f, 16f);
        SoundDefinition loom = Sound("Work Loom", 3, 0.38f, 2.5f, 18f);
        SoundDefinition page = Sound("Work Page", 3, 0.3f, 1.5f, 9f);

        var fighter = new SerializedObject(AssetDatabase.LoadAssetAtPath<CompanionDefinition>("Assets/_Project/Data/Companions/Fighter.asset"));
        var settings = new SerializedObject(SettlementSettings.Current);
        settings.FindProperty("residentToolGripPosition").vector3Value = fighter.FindProperty("gripPosition").vector3Value;
        settings.FindProperty("residentToolGripRotation").vector3Value = fighter.FindProperty("gripRotation").vector3Value;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(SettlementSettings.Current);

        string log = "";
        foreach ((string job, string tool, float scale, SoundDefinition sound, Vector2 every) in new[]
        {
            ("Smith", "IronWarhammer", 0.55f, anvil, new Vector2(1.5f, 4f)),
            ("Carpenter", "StoneHatchet", 1f, mallet, new Vector2(1.5f, 4f)),
            ("Mason", "StonePickaxe", 1f, chisel, new Vector2(1.5f, 4f)),
            ("Cook", "FangDagger", 1f, chop, new Vector2(0.5f, 2.2f)),
            ("Farmer", "StoneSickle", 1f, dig, new Vector2(1.8f, 4.5f)),
            ("Hunter", "FangDagger", 1f, hide, new Vector2(1.5f, 3.5f)),
            ("Alchemist", null, 1f, glass, new Vector2(3f, 8f)),
            ("Weaver", null, 1f, loom, new Vector2(1f, 2.5f)),
            ("RuneScholar", null, 1f, page, new Vector2(7f, 16f)),
            ("Tracker", null, 1f, page, new Vector2(7f, 16f)),
        })
        {
            var asset = AssetDatabase.LoadAssetAtPath<JobDefinition>($"Assets/_Project/Data/Settlement/{job}.asset");
            var so = new SerializedObject(asset);
            so.FindProperty("workTool").objectReferenceValue = tool != null ? Item(tool) : null;
            so.FindProperty("workToolScale").floatValue = scale;
            so.FindProperty("workSound").objectReferenceValue = sound;
            so.FindProperty("workSoundEvery").vector2Value = every;
            if (asset.WorkGesture != null && !asset.WorkGesture.IsHeld)
            {
                so.FindProperty("workSoundDelay").floatValue = StrikeMoment(asset.WorkGesture);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            log += $"{job}: {asset.WorkGesture?.name} held {asset.WorkGesture?.IsHeld} sound at {so.FindProperty("workSoundDelay").floatValue:F2} s; ";
        }

        AssetDatabase.SaveAssets();
        return log;
    }

    private static ItemDefinition Item(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:ItemDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
            {
                return AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            }
        }

        throw new System.Exception("No tool: " + name);
    }

    // Where the swing lands: samples the clip on a stand-in and finds where the right hand is lowest after it has been highest.
    private static float StrikeMoment(EmoteDefinition gesture)
    {
        var clip = (AnimationClip)new SerializedObject(gesture).FindProperty("clip").objectReferenceValue;
        var model = FindModel();
        if (clip == null || model == null)
        {
            throw new System.Exception("No clip or model to read the strike from");
        }

        GameObject stand = Object.Instantiate(model);
        stand.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Animator animator = stand.GetComponentInChildren<Animator>();
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null)
            {
                throw new System.Exception("The stand-in has no right hand");
            }

            const int Steps = 60;
            var heights = new float[Steps + 1];
            int top = 0;
            for (int i = 0; i <= Steps; i++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * i / Steps);
                heights[i] = hand.position.y;
                if (heights[i] > heights[top])
                {
                    top = i;
                }
            }

            int bottom = top;
            for (int i = top; i <= Steps; i++)
            {
                if (heights[i] < heights[bottom])
                {
                    bottom = i;
                }
            }

            return clip.length * bottom / Steps;
        }
        finally
        {
            Object.DestroyImmediate(stand);
        }
    }

    private static GameObject FindModel()
    {
        foreach (string guid in AssetDatabase.FindAssets("IngridModel t:GameObject"))
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        return null;
    }

    private static SoundDefinition Sound(string name, int variants, float volume, float full, float audible)
    {
        string path = Sounds + name + ".asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var so = new SerializedObject(sound);
        so.FindProperty("displayName").stringValue = name;
        SerializedProperty clips = so.FindProperty("clips");
        clips.arraySize = variants;
        for (int i = 0; i < variants; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Clips}{name} {i + 1}.wav");
            clips.GetArrayElementAtIndex(i).objectReferenceValue = clip != null ? clip : throw new System.Exception("No clip: " + name);
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("pitchRange").vector2Value = new Vector2(0.94f, 1.06f);
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }
}

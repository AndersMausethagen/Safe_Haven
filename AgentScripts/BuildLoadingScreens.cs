using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What the loading screen shows (run with `unity command script run --file AgentScripts/BuildLoadingScreens.cs --entry
/// BuildLoadingScreens.Apply`): a picture and tips per destination, made with LocalSpawn's Safe Haven styles and kept in
/// Art/UI/Loading, and the Hearthline accent on the bar. Re-running updates them.
/// </summary>
public static class BuildLoadingScreens
{
    private const string Folder = "Assets/_Project/Art/UI/Loading/";
    private const string Settings = "Assets/_Project/Resources/SceneTransitionSettings.asset";
    private const string Style = "Assets/_Project/Resources/UIStyle.asset";

    private static readonly string[] General =
    {
        "Whatever you carry out of a raid is yours. Whatever you carry when it collapses is not.",
        "Your safe pocket keeps what's in it, even when a raid goes wrong.",
        "Raid modifiers are crafted at home. Bring one to change what waits on the other side.",
        "The woods shift between visits. Caches and creatures are rarely where you left them.",
    };

    private static readonly string[] Raid =
    {
        "Whatever you carry out of a raid is yours. Whatever you carry when it collapses is not.",
        "Your safe pocket keeps what's in it, even when a raid goes wrong.",
        "The woods shift between visits. Caches and creatures are rarely where you left them.",
        "Listen. Most creatures give themselves away before the blow lands.",
        "When a warning on the ground turns pale, raise your guard. That is a parry.",
        "Parry, roll behind it, strike. There is no better blow, but a creature learns a trick it sees too often.",
        "A heavy bag slows you, tires you and is heard from far off.",
        "Picking a lock is loud. See to whatever is close first.",
        "Feel the ground shake? The old troll walks its round. Best let it pass.",
        "An exit has to be lit, then held. Light one with time to spare.",
    };

    private static readonly string[] Home =
    {
        "Stations keep working while you are away. Fill their queues before you leave.",
        "Crops grow while you raid. Plant before you go.",
        "Give someone a job and they earn their keep while you are gone.",
        "Press (H) to run the Haven from anywhere in it.",
        "Your journal (J) holds the old stories, your quests, what you know of each creature and how things work.",
        "Purified Essence empowers gear and buys skill ranks at the altar.",
        "A good meal goes through the portal with you.",
        "After dark, wolves prowl just beyond the light. Keep your fires lit.",
        "When the bell tolls, a breach is coming. Mend what you can and stand ready.",
    };

    private static readonly string[] Prologue =
    {
        "Raise your guard just before a blow lands and it is a parry: no damage, and the attacker staggers.",
        "A warning on the ground fills as a blow winds up. When it is full, the blow lands.",
        "Roll through a blow at the last moment and you are owed a counter.",
        "Talk to a companion to give them orders: follow, stay, defend or fall back.",
    };

    public static string Apply()
    {
        var log = new List<string>();
        Sprite general = Art("Loading_General", log);
        Sprite raid = Art("Loading_Raid", log);
        Sprite haven = Art("Loading_Haven", log);
        Sprite prologue = Art("Loading_Prologue", log);

        var settings = AssetDatabase.LoadAssetAtPath<SceneTransitionSettings>(Settings);
        var so = new SerializedObject(settings);
        so.FindProperty("backgroundArt").objectReferenceValue = general;
        so.FindProperty("accentColor").colorValue = AssetDatabase.LoadAssetAtPath<UIStyle>(Style).Accent;
        Strings(so.FindProperty("tips"), General);
        SerializedProperty destinations = so.FindProperty("destinations");
        destinations.ClearArray();
        Destination(destinations, "Raid_MistyWoods", raid, Raid);
        Destination(destinations, "SafeHaven", haven, Home);
        Destination(destinations, "Prologue_MistyWoods", prologue, Prologue);
        so.ApplyModifiedPropertiesWithoutUndo();

        var style = new SerializedObject(AssetDatabase.LoadAssetAtPath<UIStyle>(Style));
        style.FindProperty("windowFadeSeconds").floatValue = 0.12f;
        style.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return "Loading screens set. " + string.Join(" ", log);
    }

    private static Sprite Art(string name, List<string> log)
    {
        string path = $"{Folder}{name}.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        log.Add($"{name}: {sprite.rect.width}x{sprite.rect.height}.");
        return sprite;
    }

    private static void Destination(SerializedProperty list, string scene, Sprite art, string[] tips)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("scene").stringValue = scene;
        entry.FindPropertyRelative("art").objectReferenceValue = art;
        Strings(entry.FindPropertyRelative("tips"), tips);
    }

    private static void Strings(SerializedProperty list, string[] values)
    {
        list.ClearArray();
        foreach (string value in values)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = value;
        }
    }
}

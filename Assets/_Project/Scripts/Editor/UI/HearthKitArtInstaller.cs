using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the UI kit's art (Hearthline 3, drawn by Tools/UI/hearthline_kit_art.py into Art/UI/Kit) on the UI Style: the
/// scroll corner, the 9-sliced card and rim, and every icon by name. Sets each image up as a sprite first. Safe to rerun.
/// </summary>
public static class HearthKitArtInstaller
{
    public const string Folder = "Assets/_Project/Art/UI/Kit";
    private const string StylePath = "Assets/_Project/Resources/UIStyle.asset";

    [MenuItem("Safe Haven/Setup/UI/Install Hearthline Kit Art", false, 201)]
    public static void InstallMenu() => Debug.Log(Install());

    public static string Install()
    {
        var style = AssetDatabase.LoadAssetAtPath<UIStyle>(StylePath);
        if (style == null)
        {
            return $"No UI Style at {StylePath}.";
        }

        var icons = new List<Sprite>();
        Sprite corner = null, card = null, rim = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            Sprite sprite = Import(path, name.StartsWith("Hearthline Card"));
            if (name == "Hearthline Scroll Corner") corner = sprite;
            else if (name == "Hearthline Card") card = sprite;
            else if (name == "Hearthline Card Rim") rim = sprite;
            else if (sprite != null) icons.Add(sprite);
        }

        var values = new SerializedObject(style);
        values.FindProperty("scrollCorner").objectReferenceValue = corner;
        values.FindProperty("card").objectReferenceValue = card;
        values.FindProperty("cardRim").objectReferenceValue = rim;
        SerializedProperty list = values.FindProperty("icons");
        Sprite[] sorted = icons.OrderBy(icon => icon.name).ToArray();
        list.arraySize = sorted.Length;
        for (int i = 0; i < sorted.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = sorted[i];
        }

        values.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        return $"kit art: {sorted.Length} icons, corner {corner != null}, card {card != null}, rim {rim != null}";
    }

    // A sprite, no mips (it is drawn at its size), uncompressed (thin gold lines band otherwise); the card and its rim
    // 9-sliced at 20 px.
    private static Sprite Import(string path, bool sliced)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Vector4 border = sliced ? new Vector4(20f, 20f, 20f, 20f) : Vector4.zero;
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency
            || importer.spriteBorder != border || importer.spriteImportMode != SpriteImportMode.Single
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = border;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

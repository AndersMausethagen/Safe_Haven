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
        Sprite corner = null, card = null, rim = null, pattern = null, vignette = null, sheen = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            Sprite sprite = Import(path, name.StartsWith("Hearthline Card") ? 20f : name == "Hearthline Sheen" ? 24f : 0f, name == "Hearthline Leather");
            if (name == "Hearthline Scroll Corner") corner = sprite;
            else if (name == "Hearthline Card") card = sprite;
            else if (name == "Hearthline Card Rim") rim = sprite;
            else if (name == "Hearthline Leather") pattern = sprite;
            else if (name == "Hearthline Vignette") vignette = sprite;
            else if (name == "Hearthline Sheen") sheen = sprite;
            else if (sprite != null && !name.StartsWith("Hearthline")) icons.Add(sprite);
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

        values.FindProperty("barMaterial").objectReferenceValue = BarMaterial();
        values.FindProperty("pattern").objectReferenceValue = pattern;
        values.FindProperty("vignette").objectReferenceValue = vignette;
        values.FindProperty("sheen").objectReferenceValue = sheen;
        values.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        string inline = InstallInline();
        return $"kit art: {sorted.Length} icons, corner {corner != null}, card {card != null}, rim {rim != null}; {inline}";
    }

    private const string BarMaterialPath = "Assets/_Project/Art/UI/Kit Bar.mat";

    // The bars' material, on the bar shader; made once, its values kept after that.
    private static Material BarMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(BarMaterialPath);
        Shader shader = Shader.Find("Safe Haven/UI Kit Bar");
        if (material == null && shader != null)
        {
            material = new Material(shader) { name = "Kit Bar" };
            AssetDatabase.CreateAsset(material, BarMaterialPath);
        }

        return material;
    }

    public const string InlineAsset = "Kit Icons";
    private const string AtlasPath = "Assets/_Project/Art/UI/Kit Icons.png";
    private const string NamesPath = "Assets/_Project/Art/UI/Kit Icons.json";
    private const string InlinePath = "Assets/_Project/Resources/" + InlineAsset + ".asset";

    // The icons in text: a TextMesh Pro sprite asset over the atlas, a fallback of the default one (the controller
    // glyphs), so any label can carry <sprite name="Bags" tint=1> (HearthKit.InlineIcon).
    private static string InstallInline()
    {
        if (!File.Exists(AtlasPath) || !File.Exists(NamesPath))
        {
            return "no inline atlas (run Tools/UI/hearthline_kit_art.py)";
        }

        Texture2D atlas = ControllerGlyphsBuilder.ImportAtlas(AtlasPath);
        var layout = JsonUtility.FromJson<ControllerGlyphsBuilder.Layout>(File.ReadAllText(NamesPath));
        TMPro.TMP_SpriteAsset icons = ControllerGlyphsBuilder.Make(InlinePath, atlas, layout, Shader.Find("TextMeshPro/Sprite"));
        TMPro.TMP_SpriteAsset main = TMPro.TMP_Settings.defaultSpriteAsset;
        if (main != null && !main.fallbackSpriteAssets.Contains(icons))
        {
            main.fallbackSpriteAssets.Add(icons);
            EditorUtility.SetDirty(main);
        }

        AssetDatabase.SaveAssetIfDirty(icons);
        if (main != null)
        {
            AssetDatabase.SaveAssetIfDirty(main);
        }

        return $"{layout.names.Length} inline icons" + (main != null ? $" (fallback of {main.name})" : " (no default sprite asset)");
    }

    // A sprite, no mips (it is drawn at its size), uncompressed (thin gold lines band otherwise); the card and its rim
    // 9-sliced at 20 px.
    private static Sprite Import(string path, float slice, bool tiled = false)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Vector4 border = slice > 0f ? new Vector4(slice, slice, slice, slice) : Vector4.zero;
        TextureWrapMode wrap = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency
            || importer.spriteBorder != border || importer.spriteImportMode != SpriteImportMode.Single
            || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.wrapMode != wrap)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = border;
            importer.wrapMode = wrap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

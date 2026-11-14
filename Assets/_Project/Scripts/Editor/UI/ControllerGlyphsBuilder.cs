using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// Builds the TextMesh Pro sprite assets for controller prompts from the atlas Tools/UI/controller_glyphs.py draws
/// (Art/UI/Controller Glyphs.png and its .json of names), and makes it TMP Settings' default sprite asset, so any text can
/// show a button with &lt;sprite name="xbox_buttonSouth"&gt;; GameInput writes those tags for a controller. Two copies share
/// the atlas: the usual one, and one on the always-on-top shader for prompts in the world (WorldInteractionIndicator).
/// Run again after changing the drawing; it updates the assets in place, so references keep.
/// </summary>
public static class ControllerGlyphsBuilder
{
    private const string AtlasPath = "Assets/_Project/Art/UI/Controller Glyphs.png";
    private const string NamesPath = "Assets/_Project/Art/UI/Controller Glyphs.json";
    private const string AssetPath = "Assets/_Project/Resources/" + ControllerGlyphs.SpriteAssetName + ".asset";
    private const string OnTopPath = "Assets/_Project/Resources/" + ControllerGlyphs.SpriteAssetName + " On Top.asset";
    private const string OnTopShader = "Safe Haven/UI Always On Top";

    // TMP draws a glyph as tall as the font's ascender (its rule for a sprite asset without a point size of its own), times
    // Scale: a little over, so a button reads at HUD sizes (the skill bar's corner labels are small). AboveBaseline is the
    // share standing above the baseline, which centres the button on the capitals beside it.
    private const float Scale = 1.2f;
    private const float AboveBaseline = 0.81f;

    [Serializable]
    internal sealed class Layout
    {
        public int cell;
        public int columns;
        public string[] names;
    }

    [MenuItem("Safe Haven/Setup/UI/Build Controller Glyphs", false, 200)]
    public static void Build()
    {
        if (RetiredBuilders.Refuse("Build Controller Glyphs")) return;
        if (!File.Exists(AtlasPath) || !File.Exists(NamesPath))
        {
            throw new FileNotFoundException("Run python Tools/UI/controller_glyphs.py first.", AtlasPath);
        }

        Texture2D atlas = ImportAtlas(AtlasPath);
        Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(NamesPath));
        TMP_SpriteAsset glyphs = Make(AssetPath, atlas, layout, Shader.Find("TextMeshPro/Sprite"));
        TMP_SpriteAsset onTop = Make(OnTopPath, atlas, layout, Shader.Find(OnTopShader));

        TMP_Settings settings = TMP_Settings.instance;
        if (TMP_Settings.defaultSpriteAsset != glyphs)
        {
            TMP_Settings.defaultSpriteAsset = glyphs;
            EditorUtility.SetDirty(settings);
        }

        // Only these: a plain SaveAssets would also write out whatever the dynamic font atlases picked up meanwhile.
        AssetDatabase.SaveAssetIfDirty(glyphs);
        AssetDatabase.SaveAssetIfDirty(onTop);
        AssetDatabase.SaveAssetIfDirty(settings);
        ControllerGlyphs.Refresh();
        Debug.Log($"Controller glyphs: {layout.names.Length} buttons in {AssetPath} (TMP Settings' default sprite asset) and {OnTopPath}.");
    }

    /// <summary>Sets an atlas of glyphs up for TextMesh Pro (also the UI kit's icons, HearthKitArtInstaller).</summary>
    internal static Texture2D ImportAtlas(string atlasPath)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.npotScale = TextureImporterNPOTScale.None;
        // Mipmaps: a 128 px button is shown at text size, 20-30 px. Each cell has a clear margin, so they don't bleed.
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
    }

    /// <summary>A sprite asset over an atlas of square cells, made or updated in place.</summary>
    internal static TMP_SpriteAsset Make(string path, Texture2D atlas, Layout layout, Shader shader)
    {
        if (shader == null)
        {
            throw new InvalidOperationException($"Shader for {path} not found.");
        }

        var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        // The current table format; an asset without a version is "upgraded" from the old list, which would empty it.
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_Version").stringValue = "1.1.0";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
        asset.spriteSheet = atlas;
        asset.spriteGlyphTable.Clear();
        asset.spriteCharacterTable.Clear();
        int cell = layout.cell;
        for (int i = 0; i < layout.names.Length; i++)
        {
            int column = i % layout.columns, row = i / layout.columns;
            // Glyph rects count from the bottom of the texture; the atlas is laid out from the top.
            var rect = new GlyphRect(column * cell, atlas.height - (row + 1) * cell, cell, cell);
            var metrics = new GlyphMetrics(cell, cell, 0f, cell * AboveBaseline, cell);
            var glyph = new TMP_SpriteGlyph((uint)i, metrics, rect, Scale, 0);
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = layout.names[i], scale = 1f });
        }

        Material material = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().FirstOrDefault();
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.AddObjectToAsset(material, asset);
        }

        material.shader = shader;
        material.name = asset.name + " Material";
        material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        asset.material = material;
        asset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(material.name);
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(asset);
        return asset;
    }
}

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Hearthline 2 (`unity command script run --file AgentScripts/BuildHearthline2.cs --entry BuildHearthline2.Apply`, after
/// `python Tools/UI/hearthline_art.py` and `python Tools/Audio/hover.py`): the Cinzel display font for window titles (a
/// dynamic TMP font asset with Gelasio behind it for anything it lacks, and a title material with a soft shadow), the
/// panel material (Shaders/UI/HearthlinePanel), the title rule, glow and cursor art, the hover sound, the blur behind
/// full-screen windows (UIBlurFeature on PC_Renderer) and UIStyle's slots for all of it. Re-runnable.
/// </summary>
public static class BuildHearthline2
{
    private const string Fonts = "Assets/_Project/UI/Fonts/";
    private const string Art = "Assets/_Project/Art/UI/";
    private const string Style = "Assets/_Project/Resources/UIStyle.asset";

    public static string Apply()
    {
        var report = new List<string>();
        TMP_FontAsset display = DisplayFont();
        Material title = TitleMaterial(display);
        Material panel = Material("Hearthline Panel", "UI/Hearthline Panel");
        Sprite divider = SpriteAt(Art + "Hearthline Divider.png");
        Sprite glow = SpriteAt(Art + "Hearthline Glow.png");
        Texture2D cursor = CursorAt(Art + "Hearthline Cursor.png");

        var style = AssetDatabase.LoadAssetAtPath<UIStyle>(Style);
        var so = new SerializedObject(style);
        so.FindProperty("displayFont").objectReferenceValue = display;
        so.FindProperty("displayMaterial").objectReferenceValue = title;
        so.FindProperty("panelMaterial").objectReferenceValue = panel;
        so.FindProperty("divider").objectReferenceValue = divider;
        so.FindProperty("glow").objectReferenceValue = glow;
        so.FindProperty("cursor").objectReferenceValue = cursor;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(style);
        report.Add("UIStyle: display font, panel, rule, glow, cursor");
        report.Add(Hover());
        report.Add(Blur());
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static TMP_FontAsset DisplayFont()
    {
        string path = Fonts + "Cinzel SDF.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Cinzel.ttf");
            asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "Cinzel SDF";
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = "Cinzel SDF Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = "Cinzel SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        var gelasio = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + "Gelasio SDF.asset");
        asset.fallbackFontAssetTable = gelasio != null ? new List<TMP_FontAsset> { gelasio } : new List<TMP_FontAsset>();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    // Made from the font's own material, so TMP keeps it (it swaps out a material made for another font's atlas).
    private static Material TitleMaterial(TMP_FontAsset font)
    {
        string path = Fonts + "Cinzel SDF Title.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = font.material.shader;
            material.SetTexture("_MainTex", font.atlasTexture);
        }

        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.7f));
        material.SetFloat("_UnderlayOffsetX", 0.35f);
        material.SetFloat("_UnderlayOffsetY", -0.6f);
        material.SetFloat("_UnderlayDilate", 0.15f);
        material.SetFloat("_UnderlaySoftness", 0.5f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Material(string name, string shader)
    {
        string path = Art + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static Sprite SpriteAt(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Texture2D CursorAt(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Cursor;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static string Hover()
    {
        const string path = "Assets/_Project/Audio/Sounds/Library/UI Hover.asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var so = new SerializedObject(sound);
        SerializedProperty list = so.FindProperty("clips");
        list.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/SFX/Safe Haven Interface/UI Hover {i + 1}.wav");
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = 0.22f;
        so.FindProperty("volumeVariation").floatValue = 0.08f;
        so.FindProperty("pitchRange").vector2Value = new Vector2(0.96f, 1.05f);
        so.FindProperty("positional").boolValue = false;
        so.FindProperty("maxAtOnce").intValue = 1;
        so.FindProperty("minimumGap").floatValue = 0.05f;
        so.FindProperty("priority").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);

        foreach (string guid in AssetDatabase.FindAssets("t:AudioSettingsDefinition"))
        {
            var settings = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            var settingsSo = new SerializedObject(settings);
            settingsSo.FindProperty("hover").objectReferenceValue = sound;
            settingsSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        return "hover sound";
    }

    private static string Blur()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/_Project/Settings/Rendering/PC_Renderer.asset");
        UIBlurFeature feature = data.rendererFeatures.OfType<UIBlurFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<UIBlurFeature>();
            feature.name = "UI Backdrop Blur";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            var rendererSo = new SerializedObject(data);
            SerializedProperty map = rendererSo.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }

            rendererSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var so = new SerializedObject(feature);
        so.FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/Safe Haven/UI Blur");
        so.ApplyModifiedPropertiesWithoutUndo();
        data.SetDirty();
        EditorUtility.SetDirty(data);
        return "backdrop blur";
    }
}

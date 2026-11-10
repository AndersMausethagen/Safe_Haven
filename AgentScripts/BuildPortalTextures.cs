using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The portal's textures, compressed (`unity command script run --file AgentScripts/BuildPortalTextures.cs --entry
/// BuildPortalTextures.Apply`). The portal is a .glb, and the glTF importer keeps a model's own textures uncompressed: its
/// three 4096 px maps took 170 MB each in the Editor and about 85 MB each in a build, a quarter of a gigabyte for one prop.
/// This writes them out as PNGs beside the model (imported with block compression: a quarter of the size, nothing to see at
/// the distance the camera keeps), makes a copy of the model's material that uses them, and puts that material on every
/// use of the model: the Haven's and the prologue's scenes and the Raid Portal prefab. Re-runnable.
/// </summary>
public static class BuildPortalTextures
{
    private const string Model = "Assets/_Project/Models/Generated/a_large_magical_portal_00003_x4_00001_ul/a_large_magical_portal_00003_x4_00001_ul_hunyuan3d_untextured.glb";
    private const string Folder = "Assets/_Project/Models/Generated/a_large_magical_portal_00003_x4_00001_ul";
    private static readonly string[] Scenes = { "Assets/_Project/Scenes/SafeHaven.unity", "Assets/_Project/Scenes/Prologue_MistyWoods.unity" };
    private const string Prefab = "Assets/_Project/Prefabs/Raids/RaidPortal.prefab";

    public static string Apply()
    {
        Material source = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>().FirstOrDefault();
        if (source == null)
        {
            return "No material in " + Model;
        }

        // Each of the model's textures written out once, by the slot it fills.
        var made = new Dictionary<string, Texture2D>();
        foreach (string slot in source.GetTexturePropertyNames())
        {
            if (!(source.GetTexture(slot) is Texture2D texture) || AssetDatabase.GetAssetPath(texture) != Model)
            {
                continue;
            }

            bool normal = slot.ToLowerInvariant().Contains("normal") || slot.ToLowerInvariant().Contains("bump");
            bool colour = !normal && (slot.ToLowerInvariant().Contains("base") || slot.ToLowerInvariant().Contains("maintex") || slot.ToLowerInvariant().Contains("emissi"));
            string path = $"{Folder}/Portal {slot.TrimStart('_')}.png";
            if (!File.Exists(path))
            {
                // The model's textures are not readable: drawn to a target and read back, in the colour space they are stored in.
                var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, colour ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, target);
                RenderTexture before = RenderTexture.active;
                RenderTexture.active = target;
                var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, !colour);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
                File.WriteAllBytes(path, copy.EncodeToPNG());
                Object.DestroyImmediate(copy);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = colour;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.anisoLevel = texture.anisoLevel;
            importer.wrapMode = texture.wrapMode;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            made[slot] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        string materialPath = $"{Folder}/Portal.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = source.shader;
            material.CopyPropertiesFromMaterial(source);
        }

        foreach (KeyValuePair<string, Texture2D> pair in made)
        {
            material.SetTexture(pair.Key, pair.Value);
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        // On every use of the model.
        var report = new List<string>();
        Scene active = SceneManager.GetActiveScene();
        foreach (string scenePath in Scenes)
        {
            bool open = active.path == scenePath;
            if (!open && active.isDirty)
            {
                report.Add($"{Path.GetFileNameWithoutExtension(scenePath)}: skipped, the open scene has unsaved changes");
                continue;
            }

            Scene scene = open ? active : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            int changed = scene.GetRootGameObjects().Sum(root => Swap(root, source, material));
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            report.Add($"{scene.name}: {changed} renderer(s)");
            if (!open)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            int changed = Swap(root, source, material);
            if (changed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }

            report.Add($"Raid Portal prefab: {changed} renderer(s)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return $"Portal: {made.Count} texture(s) written and compressed ({string.Join(", ", made.Keys)}); {string.Join("; ", report)}";
    }

    private static int Swap(GameObject root, Material from, Material to)
    {
        int changed = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool any = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == from)
                {
                    materials[i] = to;
                    any = true;
                }
            }

            if (any)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                changed++;
            }
        }

        return changed;
    }
}

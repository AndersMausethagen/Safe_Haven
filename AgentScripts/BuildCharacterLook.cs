#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The character look (`unity command script run --file AgentScripts/BuildCharacterLook.cs --entry BuildCharacterLook.Apply`):
/// every URP Lit material a character prefab wears (people, creatures; under Assets/_Project/Prefabs, on a skinned mesh of
/// something with an Animator) moves to Lit (See-Through) with _CHARACTER_SKIN (bodies, heads: CharacterLook.IsSkin) or
/// _CHARACTER_CLOTH (clothes, hair, fur, hide). The two shaders share every property, so nothing else changes. Eyes keep
/// URP Lit. Done on the assets, not at runtime, as a build keeps only the keyword sets its materials use. Re-runnable.
/// </summary>
public static class BuildCharacterLook
{
    private const string LitShader = "Universal Render Pipeline/Lit";

    public static string Apply()
    {
        Shader character = Shader.Find("Safe Haven/Lit (See-Through)");
        var materials = new HashSet<Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null || prefab.GetComponentInChildren<Animator>(true) == null)
            {
                continue;
            }

            foreach (SkinnedMeshRenderer skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (Material material in skin.sharedMaterials)
                {
                    if (material != null && (material.shader.name == LitShader || material.shader == character))
                    {
                        materials.Add(material);
                    }
                }
            }
        }

        int skin_ = 0, cloth = 0, eyes = 0;
        foreach (Material material in materials)
        {
            if (material.name.IndexOf("Eye", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                eyes++;
                continue;
            }

            if (material.shader != character)
            {
                // Keep the material's own queue setting: -1 (the shader's) stays -1.
                int custom = new SerializedObject(material).FindProperty("m_CustomRenderQueue").intValue;
                material.shader = character;
                material.renderQueue = custom;
            }

            // URP's material validation turns this shader's motion vectors on (URP Lit had them off), so keep them on for all:
            // animated figures then blur by their own motion in the raids' motion blur, not the camera's.
            material.SetShaderPassEnabled("MotionVectors", true);
            bool isSkin = CharacterLook.IsSkin(material.name);
            material.DisableKeyword(isSkin ? "_CHARACTER_CLOTH" : "_CHARACTER_SKIN");
            material.EnableKeyword(isSkin ? "_CHARACTER_SKIN" : "_CHARACTER_CLOTH");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            if (isSkin)
            {
                skin_++;
            }
            else
            {
                cloth++;
            }
        }

        AssetDatabase.SaveAssets();
        return $"{skin_} skin, {cloth} cloth materials on the character look; {eyes} eyes left on Lit";
    }
}

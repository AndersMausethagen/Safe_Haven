using System;
using UnityEditor;
using UnityEngine;

public sealed class MeshSurfaceWindow : EditorWindow
{
    [SerializeField] private MeshRenderer target;
    [SerializeField] private int slot;
    private Editor materialEditor;
    private Vector2 scroll;

    [MenuItem("Safe Haven/World/Mesh Surface")]
    public static void Open() => GetWindow<MeshSurfaceWindow>("Mesh Surface").UseSelection();

    private void OnSelectionChange() { UseSelection(); Repaint(); }

    private void UseSelection()
    {
        if (Selection.activeGameObject != null)
            target = Selection.activeGameObject.GetComponent<MeshRenderer>();
        slot = 0;
    }

    private void OnDisable()
    {
        if (materialEditor != null) DestroyImmediate(materialEditor);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Opt-in tessellation for static MeshRenderers, not skinned characters or terrain. " +
            "Height is measured in world metres. Colliders/NavMesh stay unchanged. UV seams and hard normals can split when displaced; use seamless height maps and welded smooth surfaces. " +
            "Requires tessellation-capable hardware; unsupported platforms fall back to ordinary URP Lit.", MessageType.Info);
        target = (MeshRenderer)EditorGUILayout.ObjectField("Mesh Renderer", target, typeof(MeshRenderer), true);
        if (target == null) return;
        Material[] materials = target.sharedMaterials;
        if (materials.Length == 0) { EditorGUILayout.HelpBox("Assign a source material first.", MessageType.Warning); return; }
        slot = EditorGUILayout.IntSlider("Material Slot", slot, 0, materials.Length - 1);
        Material material = materials[slot];
        EditorGUILayout.ObjectField("Material", material, typeof(Material), false);
        if (material == null) return;
        bool supported = material.shader.name == MeshSurface.ShaderName;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (!supported)
            {
                EditorGUILayout.HelpBox("Conversion copies opaque URP Lit materials only. Original assets are preserved. " +
                    "Custom/Amplify materials require a new Mesh Surface material and manually assigned maps.", MessageType.None);
                using (new EditorGUI.DisabledScope(material.shader.name != "Universal Render Pipeline/Lit" || material.GetFloat("_Surface") > 0))
                    if (GUILayout.Button("Create Tessellated Copy and Assign"))
                    {
                        string path = EditorUtility.SaveFilePanelInProject("Save Mesh Surface Material", material.name + " Tessellated", "mat", "Choose a destination for the new material.");
                        if (!string.IsNullOrEmpty(path))
                        {
                            try { ConvertAndAssign(target, slot, path); }
                            catch (Exception exception) { Debug.LogException(exception); }
                        }
                        GUIUtility.ExitGUI();
                    }
                return;
            }
            if (target.GetComponent<MeshSurface>() == null && GUILayout.Button("Add Displacement Bounds Support"))
                Undo.AddComponent<MeshSurface>(target.gameObject);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Editor.CreateCachedEditor(material, typeof(MaterialEditor), ref materialEditor);
        materialEditor.OnInspectorGUI();
        if (GUILayout.Button("Save Material")) AssetDatabase.SaveAssetIfDirty(material);
        EditorGUILayout.HelpBox("All maps share Base Map tiling/offset. Height Channel: (1,0,0,0) = red; (0,0,1,0) = blue (terrain masks). " +
            "Use a linear height texture (sRGB off). Height controls update immediately; no bake is required. " +
            "Save the scene/prefab yourself after assigning materials or adding bounds support. Material changes affect every renderer sharing it.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    public static Material ConvertAndAssign(MeshRenderer renderer, int index, string path)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        Material[] materials = renderer.sharedMaterials;
        if (index < 0 || index >= materials.Length) throw new ArgumentOutOfRangeException(nameof(index));
        Material source = materials[index];
        if (source == null || source.shader.name != "Universal Render Pipeline/Lit" || source.GetFloat("_Surface") > 0)
            throw new InvalidOperationException("Only opaque URP Lit materials can be converted automatically.");
        if (source.IsKeywordEnabled("_SPECULAR_SETUP") || source.IsKeywordEnabled("_PARALLAXMAP") ||
            source.IsKeywordEnabled("_DETAIL_MULX2") || source.IsKeywordEnabled("_DETAIL_SCALED") ||
            source.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"))
            throw new InvalidOperationException("This material uses specular, parallax, detail or albedo-alpha smoothness. Create a mesh material and assign its maps explicitly.");
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
            AssetDatabase.LoadMainAssetAtPath(path) != null)
            throw new InvalidOperationException("Choose a new .mat asset path inside Assets; existing assets are never overwritten.");
        Shader shader = Shader.Find(MeshSurface.ShaderName);
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Mesh Surface shader is missing or unsupported on this hardware.");
        var result = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" })
            if (source.GetTexture(property) != null) result.SetTexture(property, source.GetTexture(property));
        result.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
        result.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
        foreach (string property in new[] { "_BaseColor", "_EmissionColor" }) result.SetColor(property, source.GetColor(property));
        foreach (string property in new[] { "_Metallic", "_Smoothness", "_BumpScale", "_OcclusionStrength", "_Cutoff", "_Cull" })
            result.SetFloat(property, source.GetFloat(property));
        if (source.IsKeywordEnabled("_METALLICSPECGLOSSMAP")) result.SetFloat("_Metallic", 1);
        else result.SetTexture("_MetallicGlossMap", null);
        if (!source.IsKeywordEnabled("_NORMALMAP")) result.SetFloat("_BumpScale", 0);
        if (!source.IsKeywordEnabled("_OCCLUSIONMAP")) result.SetFloat("_OcclusionStrength", 0);
        if (!source.IsKeywordEnabled("_EMISSION")) result.SetColor("_EmissionColor", Color.black);
        bool clip = source.IsKeywordEnabled("_ALPHATEST_ON");
        result.SetFloat("_AlphaClip", clip ? 1 : 0);
        if (clip) result.EnableKeyword("_ALPHATEST_ON");
        result.renderQueue = source.renderQueue;
        result.enableInstancing = source.enableInstancing;
        result.doubleSidedGI = source.doubleSidedGI;
        result.globalIlluminationFlags = source.globalIlluminationFlags;
        string[] toggles = { "_ReceiveShadows", "_SpecularHighlights", "_EnvironmentReflections" };
        string[] keywords = { "_RECEIVE_SHADOWS_OFF", "_SPECULARHIGHLIGHTS_OFF", "_ENVIRONMENTREFLECTIONS_OFF" };
        for (int i = 0; i < toggles.Length; i++)
        {
            result.SetFloat(toggles[i], source.GetFloat(toggles[i]));
            if (source.IsKeywordEnabled(keywords[i])) result.EnableKeyword(keywords[i]);
        }
        AssetDatabase.CreateAsset(result, path);
        AssetDatabase.SaveAssetIfDirty(result);
        Undo.RecordObject(renderer, "Assign Tessellated Material");
        materials[index] = result;
        renderer.sharedMaterials = materials;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        if (renderer.GetComponent<MeshSurface>() == null) Undo.AddComponent<MeshSurface>(renderer.gameObject);
        return result;
    }
}

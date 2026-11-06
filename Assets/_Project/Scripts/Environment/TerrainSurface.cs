using System;
using UnityEngine;

/// <summary>Per-terrain runtime quality overrides; authored material assets remain unchanged.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Terrain))]
public sealed class TerrainSurface : MonoBehaviour
{
    public enum DetailQuality { Off, Low, Balanced, Ultra }

    [SerializeField] private TerrainSurfaceProfile profile;
    [SerializeField] private DetailQuality quality = DetailQuality.Ultra;
    [Tooltip("Draw every paint layer in one pass (up to 20) instead of four to a pass. Each pass is tessellated again, so " +
             "ground painted across three passes cost three times as much; the picture is the same.")]
    [SerializeField] private bool singlePass = true;
    private Terrain terrain;
    private Texture2D layerTable;
    private int boundLayers = -1;
    private const int MaxSinglePassLayers = 14; // WORLD_SLOTS in WorldTerrainAllLayers.hlsl
    private const string AllLayersKeyword = "_WORLD_ALL_LAYERS";
    private const string WorldShader = "Safe Haven/Terrain (World)";
    private MaterialPropertyBlock block;
    private Vector3 previousBounds;
    private bool ownsBounds;

    public TerrainSurfaceProfile Profile { get => profile; set => profile = value; }
    public DetailQuality Quality => quality;

    public void SetQuality(DetailQuality value)
    {
        if (!Enum.IsDefined(typeof(DetailQuality), value))
            throw new ArgumentOutOfRangeException(nameof(value));
        quality = value;
        Apply();
    }

    public static Vector4 QualityParameters(DetailQuality value)
    {
        switch (value)
        {
            case DetailQuality.Off: return new Vector4(0, 1, 0, 0);
            case DetailQuality.Low: return new Vector4(1, 0.35f, 0.45f, 0);
            case DetailQuality.Balanced: return new Vector4(1, 0.65f, 0.75f, 1);
            case DetailQuality.Ultra: return Vector4.one;
            default: throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private void OnEnable()
    {
        terrain = GetComponent<Terrain>();
        previousBounds = terrain.patchBoundsMultiplier;
        ownsBounds = true;
        TerrainCallbacks.textureChanged += OnTextureChanged;
        Apply();
    }

    private void OnDisable()
    {
        TerrainCallbacks.textureChanged -= OnTextureChanged;
        if (terrain == null) return;
        block ??= new MaterialPropertyBlock();
        terrain.GetSplatMaterialPropertyBlock(block);
        block.SetVector("_TerrainDetailQuality", Vector4.one);
        block.SetFloat("_WorldAllLayers", 0f); // back to four layers a pass, which needs nothing bound here
        terrain.SetSplatMaterialPropertyBlock(block);
        if (ownsBounds) terrain.patchBoundsMultiplier = previousBounds;
        ownsBounds = false;
    }

    private void OnDestroy()
    {
        if (layerTable != null) DestroyImmediate(layerTable);
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }

    private void OnTextureChanged(Terrain changed, string textureName, RectInt region, bool synched)
    {
        // A brush stroke paints into the same control textures; only a new set of them (a layer added past 4, 8, 12)
        // needs re-binding. Re-applying on every stroke made the terrain reload while painting.
        if (changed != terrain || !synched || block == null) return;
        if (terrain.terrainData.terrainLayers.Length != boundLayers)
        {
            Apply();
            return;
        }

        Texture2D[] controls = terrain.terrainData.alphamapTextures;
        for (int i = 0; i < 4; i++)
        {
            Texture bound = block.GetTexture("_DisplacementControl" + i);
            Texture wanted = i < controls.Length ? controls[i] : Texture2D.blackTexture;
            if (bound != wanted)
            {
                Apply();
                return;
            }
        }
    }

    // Every painted layer's own textures and settings for the one-pass mix (WorldTerrainAllLayers.hlsl): nothing copied or
    // resampled. A shader may read 64 textures, so only painted layers take a slot (three textures each, 14 at most); a
    // terrain painting more keeps four layers a pass. The keyword lives on the terrain's material, set here in the editor.
    private void BindAllLayers(TerrainData data)
    {
        TerrainLayer[] layers = data.terrainLayers;
        boundLayers = layers.Length;
        Material material = terrain.materialTemplate;
        bool world = material != null && material.shader != null && material.shader.name == WorldShader;
        var slots = new System.Collections.Generic.List<int>();
        if (singlePass && world && layers.Length > 4)
        {
            bool[] painted = PaintedLayers(data);
            for (int i = 0; i < layers.Length; i++)
            {
                if (painted[i] && layers[i] != null) slots.Add(i);
            }
        }

        bool all = slots.Count > 0 && slots.Count <= MaxSinglePassLayers;
        if (singlePass && world && slots.Count > MaxSinglePassLayers)
            Debug.LogWarning($"{name}: {slots.Count} painted layers, more than the {MaxSinglePassLayers} one pass can read; drawn four to a pass.", this);
#if UNITY_EDITOR
        if (world && !Application.isPlaying && material.IsKeywordEnabled(AllLayersKeyword) != all)
        {
            if (all) material.EnableKeyword(AllLayersKeyword);
            else material.DisableKeyword(AllLayersKeyword);
            UnityEditor.EditorUtility.SetDirty(material);
        }
#endif
        // The keyword decides the first pass; the add passes follow it, so they are never culled while it is off.
        all &= world && material.IsKeywordEnabled(AllLayersKeyword);
        block.SetFloat("_WorldAllLayers", all ? 1f : 0f);
        if (!all) return;

        Texture2D[] controls = data.alphamapTextures;
        block.SetFloat("_WorldSlotCount", slots.Count);
        block.SetTexture("_WorldControl4", controls.Length > 4 ? controls[4] : Texture2D.blackTexture);
        if (layerTable == null)
        {
            layerTable = new Texture2D(MaxSinglePassLayers, 6, TextureFormat.RGBAFloat, false, true)
            {
                name = "Terrain Layer Table", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
        }

        for (int slot = 0; slot < slots.Count; slot++)
        {
            int index = slots[slot];
            TerrainLayer layer = layers[index];
            Vector2 tile = new Vector2(Mathf.Max(layer.tileSize.x, 0.001f), Mathf.Max(layer.tileSize.y, 0.001f));
            // As Unity sets a pass's _SplatN_ST, remaps and constants for a layer.
            layerTable.SetPixel(slot, 0, new Color(data.size.x / tile.x, data.size.z / tile.y, layer.tileOffset.x / tile.x, layer.tileOffset.y / tile.y));
            layerTable.SetPixel(slot, 1, layer.diffuseRemapMax);
            layerTable.SetPixel(slot, 2, layer.maskMapRemapMax - layer.maskMapRemapMin);
            layerTable.SetPixel(slot, 3, layer.maskMapRemapMin);
            layerTable.SetPixel(slot, 4, new Color(layer.normalScale, layer.smoothness, layer.metallic, (float)layer.smoothnessSource));
            layerTable.SetPixel(slot, 5, new Color(layer.maskMapTexture != null ? 1f : 0f, index, 0f, 0f));
            block.SetTexture("_WorldAlbedo" + slot, layer.diffuseTexture != null ? layer.diffuseTexture : Texture2D.whiteTexture);
            block.SetTexture("_WorldNormal" + slot, layer.normalMapTexture != null ? layer.normalMapTexture : Texture2D.normalTexture);
            block.SetTexture("_WorldMask" + slot, layer.maskMapTexture != null ? layer.maskMapTexture : Texture2D.grayTexture);
        }

        layerTable.Apply(false, false);
        block.SetTexture("_WorldLayerTable", layerTable);
    }

    // Which layers have any weight anywhere, read from the control maps themselves.
    private static bool[] PaintedLayers(TerrainData data)
    {
        var painted = new bool[data.terrainLayers.Length];
        Texture2D[] controls = data.alphamapTextures;
        for (int map = 0; map < controls.Length; map++)
        {
            Color32[] pixels = controls[map].GetPixels32();
            for (int channel = 0; channel < 4; channel++)
            {
                int layer = map * 4 + channel;
                if (layer >= painted.Length) break;
                for (int i = 0; i < pixels.Length && !painted[layer]; i++)
                {
                    Color32 p = pixels[i];
                    byte v = channel == 0 ? p.r : channel == 1 ? p.g : channel == 2 ? p.b : p.a;
                    if (v > 0) painted[layer] = true;
                }
            }
        }

        return painted;
    }

    public void Apply()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (terrain.terrainData == null) return;
        block ??= new MaterialPropertyBlock();
        terrain.GetSplatMaterialPropertyBlock(block);
        block.SetVector("_TerrainDetailQuality", QualityParameters(quality));
        Vector3 size = terrain.terrainData.size;
        float sampleStep = 0.01f;
        if (terrain.materialTemplate != null && terrain.materialTemplate.HasProperty("_DisplacementTerrainSize"))
            sampleStep = Mathf.Max(0.001f, terrain.materialTemplate.GetVector("_DisplacementTerrainSize").z);
        block.SetVector("_DisplacementTerrainSize", new Vector4(size.x, size.z, sampleStep, 0));
        Texture2D[] controls = terrain.terrainData.alphamapTextures;
        for (int i = 0; i < 4; i++)
            block.SetTexture("_DisplacementControl" + i, i < controls.Length ? controls[i] : Texture2D.blackTexture);
        BindAllLayers(terrain.terrainData);
        terrain.SetSplatMaterialPropertyBlock(block);
        if (terrain.materialTemplate != null && terrain.materialTemplate.HasProperty("_DisplacementStrength"))
        {
            Vector3 bounds = terrain.patchBoundsMultiplier;
            float detailHeight = terrain.materialTemplate.GetFloat("_DisplacementStrength");
            if (profile != null && profile.footprints) detailHeight += profile.footprintDepth;
            bounds.y = Mathf.Max(bounds.y, 1 + detailHeight * 4);
            if (bounds != terrain.patchBoundsMultiplier) terrain.patchBoundsMultiplier = bounds; // setting it rebuilds the terrain
        }
    }
}

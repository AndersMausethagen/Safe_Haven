using System;
using UnityEngine;

/// <summary>Per-terrain runtime quality overrides; authored material assets remain unchanged.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Terrain))]
public sealed class TerrainSurface : MonoBehaviour
{
    public enum DetailQuality { Off, Low, Balanced, Ultra }

    [SerializeField] private TerrainSurfaceProfile profile;
    [SerializeField] private DetailQuality quality = DetailQuality.Ultra;
    private Terrain terrain;
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
        terrain.SetSplatMaterialPropertyBlock(block);
        if (ownsBounds) terrain.patchBoundsMultiplier = previousBounds;
        ownsBounds = false;
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

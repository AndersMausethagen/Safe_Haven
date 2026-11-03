using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Safe Haven/Art/Terrain Surface Profile")]
public sealed class TerrainSurfaceProfile : ScriptableObject
{
    public enum HeightChannel { Red, Green, Blue, Alpha }
    public enum HeightResolution { Medium = 512, High = 1024, Maximum = 2048 }

    [Serializable]
    public sealed class Layer
    {
        public TerrainLayer terrainLayer;
        [Tooltip("Optional linear height map. Empty uses the Terrain Layer mask's blue channel.")]
        public Texture2D heightMap;
        public HeightChannel channel = HeightChannel.Red;
        [Range(0, 4)] public float amplitude = 1;
        [Range(0, 1)] public float midpoint = 0.5f;
        [Range(0.1f, 4)] public float contrast = 1;
        [Tooltip("How much this painted layer retains footprints. Zero for hard rock; one for soft moss or mud. Rebuild after changing.")]
        [Range(0, 1)] public float footprintResponse = 1;
    }

    [Tooltip("RHalf height array resolution per layer. Original textures are never overwritten.")]
    public HeightResolution resolution = HeightResolution.High;
    public List<Layer> layers = new List<Layer>();

    // These defaults are the Haven's terrain, tuned by eye: it is the standard for footprints on every terrain.
    [Header("Recovering Footprints")]
    public bool footprints = true;
    [Min(0.1f)] public float footprintLifetime = 30;
    [Range(0, 0.1f)] public float footprintDepth = 0.035f;
    [Tooltip("Compresses the whole sole's height detail and softens its fine albedo texture.")]
    [Range(0, 1)] public float footprintFlattening = 1;
    [Range(0, 1)] public float footprintNormalFlattening = 1;
    [Tooltip("Darkening of compressed ground, to keep the boot tread readable among detailed textures. Zero disables it.")]
    [Range(0, 0.8f)] public float footprintDarkening = 0.8f;
    [Tooltip("How much painted Safe Haven grass bends down in the recovering boot trail.")]
    [Range(0, 1)] public float footprintGrassFlattening = 1;
    [Min(0.05f)] public float bootWidth = 0.13f;
    [Min(0.1f)] public float bootLength = 0.3f;
    [Tooltip("Optional linear sole mask: white presses down, black leaves the ground unchanged. Empty uses a procedural treaded boot.")]
    public Texture2D bootMask;
    [Tooltip("Local rolling footprint field in metres. Tracks outside this area are discarded.")]
    [Range(8, 64)] public float footprintArea = 24;
    public HeightResolution footprintResolution = HeightResolution.Maximum;

    public Layer Find(TerrainLayer layer) => layers.Find(entry => entry != null && entry.terrainLayer == layer);

    /// <summary>Takes on how <paramref name="standard"/> shows footprints, and what it says of each ground they share. Grounds only this
    /// terrain has keep their own softness.</summary>
    public void UseFootprintsOf(TerrainSurfaceProfile standard)
    {
        footprints = standard.footprints;
        footprintLifetime = standard.footprintLifetime;
        footprintDepth = standard.footprintDepth;
        footprintFlattening = standard.footprintFlattening;
        footprintNormalFlattening = standard.footprintNormalFlattening;
        footprintDarkening = standard.footprintDarkening;
        footprintGrassFlattening = standard.footprintGrassFlattening;
        bootWidth = standard.bootWidth;
        bootLength = standard.bootLength;
        bootMask = standard.bootMask;
        footprintArea = standard.footprintArea;
        footprintResolution = standard.footprintResolution;
        foreach (Layer layer in layers)
        {
            Layer same = layer != null ? standard.Find(layer.terrainLayer) : null;
            if (same != null)
            {
                layer.footprintResponse = same.footprintResponse;
            }
        }
    }
}

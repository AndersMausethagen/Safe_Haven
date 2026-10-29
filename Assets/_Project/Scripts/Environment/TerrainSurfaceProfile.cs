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
        [Range(0, 1)] public float footprintResponse = 0.6f;
    }

    [Tooltip("RHalf height array resolution per layer. Original textures are never overwritten.")]
    public HeightResolution resolution = HeightResolution.High;
    public List<Layer> layers = new List<Layer>();

    [Header("Recovering Footprints")]
    public bool footprints = true;
    [Min(0.1f)] public float footprintLifetime = 30;
    [Range(0, 0.1f)] public float footprintDepth = 0.025f;
    [Tooltip("Compresses the whole sole's height detail and softens its fine albedo texture.")]
    [Range(0, 1)] public float footprintFlattening = 0.75f;
    [Range(0, 1)] public float footprintNormalFlattening = 0.65f;
    [Tooltip("Darkening of compressed ground, to keep the boot tread readable among detailed textures. Zero disables it.")]
    [Range(0, 0.8f)] public float footprintDarkening = 0.4f;
    [Tooltip("How much painted Safe Haven grass bends down in the recovering boot trail.")]
    [Range(0, 1)] public float footprintGrassFlattening = 0.95f;
    [Min(0.05f)] public float bootWidth = 0.13f;
    [Min(0.1f)] public float bootLength = 0.3f;
    [Tooltip("Optional linear sole mask: white presses down, black leaves the ground unchanged. Empty uses a procedural treaded boot.")]
    public Texture2D bootMask;
    [Tooltip("Local rolling footprint field in metres. Tracks outside this area are discarded.")]
    [Range(8, 64)] public float footprintArea = 24;
    public HeightResolution footprintResolution = HeightResolution.Maximum;

    public Layer Find(TerrainLayer layer) => layers.Find(entry => entry != null && entry.terrainLayer == layer);
}

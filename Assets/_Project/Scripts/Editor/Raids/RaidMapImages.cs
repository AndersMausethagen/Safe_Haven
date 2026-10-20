using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves raid map data between the painted layout and image files: the one-off import of a map made with the older
/// images (heights, red/green/blue mask, coloured marker dots), and PNG export/import of single zones and of the heights,
/// for touching them up in an image editor.
/// </summary>
public static class RaidMapImages
{
    /// <summary>
    /// Writes the definition's legacy images into the painted layout: heights onto the terrain, the mask's red, green and
    /// blue into the Path, Forest and Clearing zones (at the mask's resolution, so nothing is lost), and the marker dots
    /// into markers. Replaces what was painted in those three zones and every marker.
    /// </summary>
    public static string ImportLegacy(RaidMap map)
    {
        RaidMapDefinition definition = map.Definition;
        if (definition == null) return "Assign a Raid Map definition first.";
        if (definition.HeightMap == null || definition.MaskMap == null || definition.MarkerMap == null)
            return $"{definition.name} needs its height, mask and marker images to import from.";
        if (definition.MaskMap.width != definition.MaskMap.height) return "The mask image must be square.";

        Prepare(definition.HeightMap, true, sixteenBit: true);
        Prepare(definition.MaskMap, true);
        Prepare(definition.MarkerMap, false);

        Undo.RecordObject(definition, "Import raid map images");
        Terrain terrain = RaidMapBaker.EnsureTerrain(map, definition);
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Import raid map images");
        WriteHeights(terrain.terrainData, definition.HeightMap, definition.Smoothing);

        int cells = definition.MaskMap.width;
        definition.SetZoneResolution(cells);
        definition.EnsureZones();
        Color32[] mask = definition.MaskMap.GetPixels32();
        var path = new byte[cells * cells];
        var forest = new byte[cells * cells];
        var clearing = new byte[cells * cells];
        for (int i = 0; i < mask.Length; i++)
        {
            path[i] = mask[i].r;
            forest[i] = mask[i].g;
            clearing[i] = mask[i].b;
        }

        foreach (RaidMapZone zone in definition.Zones)
        {
            if (zone.role == RaidZoneRole.Path) zone.SetWeights((byte[])path.Clone(), cells);
            else if (zone.role == RaidZoneRole.Forest) zone.SetWeights((byte[])forest.Clone(), cells);
            else if (zone.role == RaidZoneRole.Clearing) zone.SetWeights((byte[])clearing.Clone(), cells);
            else zone.EnsureResolution(cells);
        }

        Texture2D markerImage = definition.MarkerMap;
        definition.Markers.Clear();
        foreach (RaidSite site in RaidMapMarkers.Decode(markerImage.GetPixels32(), markerImage.width, markerImage.height))
        {
            definition.Markers.Add(new RaidMapMarker { kind = site.Kind, position = site.Position });
        }

        EditorUtility.SetDirty(definition);
        EditorUtility.SetDirty(terrain.terrainData);
        AssetDatabase.SaveAssetIfDirty(definition);
        AssetDatabase.SaveAssetIfDirty(terrain.terrainData);
        return $"Imported {definition.name}: heights {definition.HeightMap.width}², zones {cells}², {definition.Markers.Count} markers. " +
               "Bake to rebuild trees and points from them.";
    }

    /// <summary>Sets the terrain's heights from the legacy height image (black = lowest, white = the terrain's height), sampled as the old baker did.</summary>
    public static void WriteHeights(TerrainData data, Texture2D image, int smoothing)
    {
        int resolution = data.heightmapResolution;
        var heights = new float[resolution, resolution];
        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                heights[row, column] = image.GetPixelBilinear(column / (resolution - 1f), row / (resolution - 1f)).r;
            }
        }

        for (int pass = 0; pass < smoothing; pass++)
        {
            heights = Blur(heights);
        }

        data.SetHeights(0, 0, heights);
    }

    /// <summary>Saves a zone as a greyscale PNG (white = full strength, bottom row = south).</summary>
    public static void ExportZone(RaidMapZone zone, string file)
    {
        int cells = zone.Resolution;
        var image = new Texture2D(cells, cells, TextureFormat.RGBA32, false);
        var pixels = new Color32[cells * cells];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte value = zone.Weights[i];
            pixels[i] = new Color32(value, value, value, 255);
        }

        image.SetPixels32(pixels);
        File.WriteAllBytes(file, image.EncodeToPNG());
        Object.DestroyImmediate(image);
    }

    /// <summary>Replaces a zone with a PNG's brightness, resized to the zone's resolution. Record Undo before calling.</summary>
    public static bool ImportZone(RaidMapZone zone, string file)
    {
        Texture2D image = Load(file);
        if (image == null) return false;

        int cells = zone.Resolution;
        var weights = new byte[cells * cells];
        for (int y = 0; y < cells; y++)
        {
            for (int x = 0; x < cells; x++)
            {
                weights[y * cells + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(image.GetPixelBilinear(x / (float)cells, y / (float)cells).grayscale) * 255f);
            }
        }

        zone.SetWeights(weights, cells);
        Object.DestroyImmediate(image);
        return true;
    }

    /// <summary>Saves the terrain's heights as a 16-bit greyscale PNG (black = lowest, white = the terrain's height).</summary>
    public static void ExportHeights(TerrainData data, string file)
    {
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        var image = new Texture2D(resolution, resolution, TextureFormat.R16, false, true);
        var values = new ushort[resolution * resolution];
        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                values[row * resolution + column] = (ushort)Mathf.RoundToInt(Mathf.Clamp01(heights[row, column]) * 65535f);
            }
        }

        image.SetPixelData(values, 0);
        image.Apply(false);
        File.WriteAllBytes(file, image.EncodeToPNG());
        Object.DestroyImmediate(image);
    }

    /// <summary>
    /// Sets the terrain's heights from a greyscale PNG, its corners on the terrain's corners. 16-bit greyscale PNGs (as
    /// Export Heights writes) keep their full precision; other PNGs are read at 8 bits. Record Undo before calling.
    /// </summary>
    public static bool ImportHeights(TerrainData data, string file)
    {
        if (!File.Exists(file)) return false;
        float[,] pixels = ReadGreyscale16(File.ReadAllBytes(file));
        if (pixels == null)
        {
            Texture2D image = Load(file);
            if (image == null) return false;
            pixels = new float[image.height, image.width];
            Color32[] colours = image.GetPixels32();
            for (int i = 0; i < colours.Length; i++) pixels[i / image.width, i % image.width] = colours[i].r / 255f;
            Object.DestroyImmediate(image);
        }

        // Vertex 0..res-1 lands exactly on pixel 0..width-1.
        int resolution = data.heightmapResolution;
        int height = pixels.GetLength(0);
        int width = pixels.GetLength(1);
        var heights = new float[resolution, resolution];
        for (int row = 0; row < resolution; row++)
        {
            float y = row * (height - 1f) / (resolution - 1f);
            int y0 = Mathf.Min((int)y, height - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            for (int column = 0; column < resolution; column++)
            {
                float x = column * (width - 1f) / (resolution - 1f);
                int x0 = Mathf.Min((int)x, width - 1);
                int x1 = Mathf.Min(x0 + 1, width - 1);
                float bottom = Mathf.Lerp(pixels[y0, x0], pixels[y0, x1], x - x0);
                float top = Mathf.Lerp(pixels[y1, x0], pixels[y1, x1], x - x0);
                heights[row, column] = Mathf.Lerp(bottom, top, y - y0);
            }
        }

        data.SetHeights(0, 0, heights);
        return true;
    }

    // Texture2D.LoadImage drops 16-bit PNGs to 8 bits, which terraces a terrain, so 16-bit greyscale ones (non-interlaced,
    // the kind Export Heights writes and image editors save) are decoded here. Returns [row, column] with row 0 = south,
    // or null for any other kind of PNG.
    private static float[,] ReadGreyscale16(byte[] png)
    {
        if (png.Length < 33 || png[0] != 0x89 || png[1] != (byte)'P' || png[2] != (byte)'N' || png[3] != (byte)'G') return null;

        int width = 0, height = 0;
        var compressed = new MemoryStream();
        for (int offset = 8; offset + 8 <= png.Length;)
        {
            int length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            string type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            int body = offset + 8;
            if (length < 0 || body + length > png.Length) return null;
            if (type == "IHDR")
            {
                width = (png[body] << 24) | (png[body + 1] << 16) | (png[body + 2] << 8) | png[body + 3];
                height = (png[body + 4] << 24) | (png[body + 5] << 16) | (png[body + 6] << 8) | png[body + 7];
                bool greyscale16 = png[body + 8] == 16 && png[body + 9] == 0 && png[body + 12] == 0;
                if (!greyscale16 || width <= 0 || height <= 0) return null;
            }
            else if (type == "IDAT")
            {
                compressed.Write(png, body, length);
            }
            else if (type == "IEND")
            {
                break;
            }

            offset = body + length + 4;
        }

        if (width == 0 || compressed.Length < 2) return null;

        int stride = width * 2;
        var raw = new byte[(stride + 1) * height];
        compressed.Position = 2; // zlib header; DeflateStream reads the raw deflate data after it
        using (var inflater = new System.IO.Compression.DeflateStream(compressed, System.IO.Compression.CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length)
            {
                int got = inflater.Read(raw, read, raw.Length - read);
                if (got <= 0) return null;
                read += got;
            }
        }

        var previous = new byte[stride];
        var line = new byte[stride];
        var result = new float[height, width];
        for (int r = 0; r < height; r++)
        {
            int start = r * (stride + 1);
            byte filter = raw[start];
            for (int i = 0; i < stride; i++)
            {
                int left = i >= 2 ? line[i - 2] : 0;
                int up = previous[i];
                int upLeft = i >= 2 ? previous[i - 2] : 0;
                int value = raw[start + 1 + i];
                switch (filter)
                {
                    case 1: value += left; break;
                    case 2: value += up; break;
                    case 3: value += (left + up) / 2; break;
                    case 4: value += Paeth(left, up, upLeft); break;
                }

                line[i] = (byte)value;
            }

            // PNG rows run top (north) first.
            int row = height - 1 - r;
            for (int x = 0; x < width; x++) result[row, x] = ((line[x * 2] << 8) | line[x * 2 + 1]) / 65535f;
            (previous, line) = (line, previous);
        }

        return result;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Mathf.Abs(p - a);
        int pb = Mathf.Abs(p - b);
        int pc = Mathf.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static Texture2D Load(string file)
    {
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp };
        if (!File.Exists(file) || !image.LoadImage(File.ReadAllBytes(file)))
        {
            Object.DestroyImmediate(image);
            return null;
        }

        image.wrapMode = TextureWrapMode.Clamp;
        return image;
    }

    private static float[,] Blur(float[,] source)
    {
        int size = source.GetLength(0);
        var result = new float[size, size];
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                float sum = 0f;
                int count = 0;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int r = row + dr;
                        int c = column + dc;
                        if (r >= 0 && c >= 0 && r < size && c < size)
                        {
                            sum += source[r, c];
                            count++;
                        }
                    }
                }

                result[row, column] = sum / count;
            }
        }

        return result;
    }

    // The import reads pixels, so the images must be readable and kept exact (no compression, no colour correction).
    // Heights are kept at 16 bits (when the PNG has them), or the terrain shows terraces.
    private static void Prepare(Texture2D image, bool linear, bool sixteenBit = false)
    {
        string path = AssetDatabase.GetAssetPath(image);
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
        {
            return;
        }

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        TextureImporterFormat format = sixteenBit ? TextureImporterFormat.R16 : TextureImporterFormat.Automatic;
        bool changed = !importer.isReadable || importer.sRGBTexture == linear || importer.mipmapEnabled
                       || importer.textureCompression != TextureImporterCompression.Uncompressed
                       || importer.npotScale != TextureImporterNPOTScale.None || platform.format != format;
        if (!linear && importer.filterMode != FilterMode.Point) changed = true;
        if (!changed)
        {
            return;
        }

        importer.isReadable = true;
        importer.sRGBTexture = !linear;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = linear ? FilterMode.Bilinear : FilterMode.Point;
        platform.format = format;
        importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
    }
}

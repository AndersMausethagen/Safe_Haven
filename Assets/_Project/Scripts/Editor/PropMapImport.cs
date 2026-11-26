using System;
using System.IO;
using UnityEditor;

/// <summary>
/// Maps that Tools/Blender/ingest_generated.py writes next to a prop in Art/World/Props: the normal map imported as one, and the
/// data maps (ORM, height, blend mask) linear. Unity's defaults (sRGB colour) shade a prop nearly black in patches: the Stone
/// Head and White Hart of 2026-11-26. The prop's material is its own "&lt;name&gt;.mat" (Safe Haven/Layered Lit), see
/// PropMaterialTests.
/// </summary>
public sealed class PropMapImport : AssetPostprocessor
{
    internal const string PropsRoot = "Assets/_Project/Art/World/Props/";

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(PropsRoot, StringComparison.Ordinal))
        {
            return;
        }

        string stem = Path.GetFileNameWithoutExtension(path);
        var importer = (TextureImporter)assetImporter;
        if (stem.EndsWith("_Normal", StringComparison.Ordinal))
        {
            importer.textureType = TextureImporterType.NormalMap;
        }
        else if (stem.EndsWith("_ORM", StringComparison.Ordinal) || stem.EndsWith("_Height", StringComparison.Ordinal) || stem.EndsWith("_BlendMask", StringComparison.Ordinal))
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
        }
    }
}

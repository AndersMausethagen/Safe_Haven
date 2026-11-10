using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The parts a painted cinematic is written from: a shot with its lines, a painting's layers, lights, specks, mist, a
/// vignette and sounds, plus the import settings its pictures and narration need. The scripts that write a
/// <see cref="CinematicDefinition"/> (AgentScripts/BuildBackstoryCinematic.cs, BuildPrologueEndCinematic.cs) say what
/// is in each shot with these; nothing here knows any one cinematic.
/// <para>
/// A script names its own folders first with <see cref="Use"/>: where its paintings' layers are, and what its voice
/// clips' paths begin with.
/// </para>
/// </summary>
public static class CinematicBuilder
{
    public const string Shared = "Assets/_Project/Art/Cinematics/Shared/";
    public const string MaterialPath = "Assets/_Project/Resources/Cinematics/Cinematic Additive.mat";

    /// <summary>Where the layers of a painting stand, by how many it was cut into (Tools/LocalSpawn/backstory_layers.py).</summary>
    public static readonly Dictionary<int, float[]> Depths = new Dictionary<int, float[]>
    {
        { 1, new[] { 0f } },
        { 2, new[] { 2f, 0f } },
        { 3, new[] { 3f, 0.5f, -0.3f } },
        { 4, new[] { 4f, 1f, 0f, -0.45f } },
    };

    /// <summary>A layer so far away the camera can't move it: the vignette, and anything else pinned to the frame.</summary>
    public const float Pinned = 2000f;

    public static readonly Color Firelight = new Color(1f, 0.62f, 0.25f, 1f);
    public static readonly Color Ember = new Color(1f, 0.42f, 0.12f, 1f);
    public static readonly Color Moonlit = new Color(0.78f, 0.86f, 1f, 1f);
    public static readonly Color RuneGold = new Color(1f, 0.82f, 0.42f, 1f);

    /// <summary>The folder of the cinematic's layers, ending in a slash.</summary>
    public static string ArtFolder { get; private set; }

    /// <summary>What its voice clips' paths begin with: "Assets/.../Voice/Backstory/Backstory_".</summary>
    public static string VoicePrefix { get; private set; }

    /// <summary>Names the cinematic being written, sets up how its files are imported and makes sure the additive material exists.</summary>
    public static void Use(string artFolder, string voicePrefix)
    {
        ArtFolder = artFolder;
        VoicePrefix = voicePrefix;
        ImportPictures();
        ImportVoice();
        EnsureMaterial();
    }

    /// <summary>Writes the asset (only it: saving everything would also write whatever else the Editor has changed in memory).</summary>
    public static CinematicDefinition Save(string assetPath, string title, List<CinematicShot> shots, List<CinematicMusic> music)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        var definition = AssetDatabase.LoadAssetAtPath<CinematicDefinition>(assetPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<CinematicDefinition>();
            AssetDatabase.CreateAsset(definition, assetPath);
        }

        definition.Author(title, shots, music);
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
        return definition;
    }

    // ----- Parts -----

    public static CinematicShot Shot(string name, CinematicTransition arrives, float arriveSeconds, CinematicPose from, CinematicPose to, float holdAfter,
        params (string id, string text, float pause)[] lines)
    {
        var shot = new CinematicShot { name = name, arrives = arrives, arriveSeconds = arriveSeconds, cameraFrom = from, cameraTo = to, holdAfter = holdAfter };
        foreach ((string id, string text, float pause) in lines)
        {
            var clip = Clip(VoicePrefix + id + ".wav");
            shot.lines.Add(new CinematicLine { voice = clip, subtitle = text, pauseBefore = pause });
        }

        return shot;
    }

    /// <summary>Seconds into a shot at which a line starts.</summary>
    public static float LineStart(CinematicShot shot, int index)
    {
        float at = 0f;
        for (int i = 0; i <= index; i++)
        {
            at += shot.lines[i].pauseBefore + (i < index ? shot.lines[i].Seconds : 0f);
        }

        return at;
    }

    /// <summary>A time in a shot as a share of its length, which is what a layer's opacity curve runs over.</summary>
    public static float Share(CinematicShot shot, float seconds) => Mathf.Clamp01(seconds / shot.Seconds);

    /// <summary>
    /// Adds a painting's layers, back to front, and returns the size they were all given. One size for every layer keeps the
    /// painting in register; it is as large as the layer that slides furthest needs, with room for the move to run on
    /// under the dissolve that follows.
    /// </summary>
    public static float Painting(CinematicShot shot, string painting)
    {
        string[] files = Directory.GetFiles(ArtFolder, painting + "_*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();
        if (files.Length == 0 || !Depths.TryGetValue(files.Length, out float[] depths))
        {
            throw new FileNotFoundException($"No layers for {painting} in {ArtFolder} (run the cinematic's painting script, phase layers).");
        }

        CinematicPose beyond = shot.easeCamera ? shot.cameraTo : CinematicPose.Lerp(shot.cameraFrom, shot.cameraTo, 1.45f);
        float size = depths.Max(depth => CinematicCamera.SizeToCover(depth, Vector2.zero, shot.cameraFrom, beyond)) * 1.012f;
        for (int i = 0; i < files.Length; i++)
        {
            string layerName = Path.GetFileNameWithoutExtension(files[i]).Split('_').Last();
            shot.layers.Add(Picture(char.ToUpperInvariant(layerName[0]) + layerName.Substring(1), files[i], depths[i], size));
        }

        return size;
    }

    public static CinematicLayer Picture(string name, string path, float depth, float size)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new FileNotFoundException("Not imported as a sprite: " + path);
        }

        return new CinematicLayer { name = name, picture = sprite, depth = depth, size = size };
    }

    /// <summary>A round light (or, not additive, a soft dark patch) on a spot of the painting: u, v from its bottom left.</summary>
    /// <param name="height">Its diameter as a share of the frame's height.</param>
    /// <param name="paintingSize">What Painting returned, so the spot stays on what it lights.</param>
    public static CinematicLayer Glow(string name, float u, float v, float height, float depth, float paintingSize, Color colour, float strength,
        float flicker = 0f, bool additive = true)
    {
        colour.a = strength;
        return new CinematicLayer
        {
            name = name, picture = AssetDatabase.LoadAssetAtPath<Sprite>(Shared + "Glow.png"), depth = depth, size = height, round = true,
            offset = new Vector2((u - 0.5f) * paintingSize, (v - 0.5f) * paintingSize), tint = colour, flicker = flicker, flickerSpeed = 7f, additive = additive,
        };
    }

    public static CinematicLayer Specks(string name, float depth, Color colour, bool additive, CinematicMotes motes) => new CinematicLayer
    {
        name = name, kind = CinematicLayerKind.Motes, picture = AssetDatabase.LoadAssetAtPath<Sprite>(Shared + "Glow.png"), depth = depth, size = 1.25f,
        tint = colour, additive = additive, motes = motes,
    };

    public static CinematicLayer Snow(string name, int count) => Specks(name, -0.2f, new Color(0.95f, 0.97f, 1f, 0.85f), additive: false, new CinematicMotes
    {
        count = count, size = new Vector2(0.003f, 0.008f), velocity = new Vector2(0.012f, -0.09f), wander = 0.025f, wanderSeconds = 3f,
        twinkle = 0.2f, edgeFade = 0.12f,
    });

    /// <summary>Low mist sliding sideways. Wide enough that its edge never comes into the frame while it slides.</summary>
    public static CinematicLayer Mist(string name, float depth, Vector2 drift, float strength) => new CinematicLayer
    {
        name = name, picture = AssetDatabase.LoadAssetAtPath<Sprite>(Shared + "Mist.png"), depth = depth, size = 1.9f, offset = new Vector2(0f, -0.2f),
        tint = new Color(Moonlit.r, Moonlit.g, Moonlit.b, strength), drift = drift,
    };

    public static CinematicLayer Vignette(float strength) => new CinematicLayer
    {
        name = "Vignette", picture = AssetDatabase.LoadAssetAtPath<Sprite>(Shared + "Vignette.png"), depth = Pinned, size = 1.02f,
        tint = new Color(1f, 1f, 1f, strength),
    };

    public static CinematicSound Bed(string path, float at, float volume, float fade, bool loop = true) =>
        new CinematicSound { clip = Clip(path), at = at, volume = volume, loop = loop, fade = fade };

    public static AnimationCurve Keys(params (float time, float value)[] keys)
    {
        var curve = new AnimationCurve();
        foreach ((float time, float value) in keys.OrderBy(k => k.time))
        {
            curve.AddKey(new Keyframe(Mathf.Clamp01(time), value));
        }

        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }

        return curve;
    }

    public static AudioClip Clip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            throw new FileNotFoundException("No audio clip at " + path);
        }

        return clip;
    }

    // ----- Import settings -----

    private static void ImportPictures()
    {
        foreach (string folder in new[] { ArtFolder, Shared })
        {
            foreach (string file in Directory.GetFiles(folder, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool mist = path.EndsWith("Mist.png");
                // The overlays are slow fades, which block compression turns into steps: they are small, so they stay as they are.
                TextureImporterCompression compression = folder == Shared ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                if (importer == null || (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled
                    && importer.textureCompression == compression && importer.maxTextureSize == 2048
                    && importer.wrapMode == (mist ? TextureWrapMode.Repeat : TextureWrapMode.Clamp)))
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = mist ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.textureCompression = compression;
                // A full rectangle: the tight outline Unity would trace around a cut-out costs thousands of vertices.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }

    private static void ImportVoice()
    {
        foreach (string file in Directory.GetFiles(Path.GetDirectoryName(VoicePrefix), "*.wav"))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            if (importer.forceToMono && sample.loadType == AudioClipLoadType.DecompressOnLoad && sample.compressionFormat == AudioCompressionFormat.Vorbis
                && Mathf.Approximately(sample.quality, 0.85f))
            {
                continue;
            }

            importer.forceToMono = true;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            sample.quality = 0.85f;
            importer.defaultSampleSettings = sample;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
        {
            return;
        }

        Shader shader = Shader.Find("SafeHaven/UI/Additive");
        if (shader == null)
        {
            throw new FileNotFoundException("Shader SafeHaven/UI/Additive is missing (Assets/_Project/Shaders/UI/CinematicAdditive.shader).");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        AssetDatabase.CreateAsset(new Material(shader) { name = "Cinematic Additive" }, MaterialPath);
    }
}

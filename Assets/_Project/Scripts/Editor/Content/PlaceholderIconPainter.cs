using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>The picture a placeholder icon shows.</summary>
public enum IconShape
{
    Log,
    Rock,
    Ore,
    Orb,
    Fiber,
    Plank,
    Brick,
    Ingot,
    Axe,
    Pickaxe,
    Sickle,
    Potion,
    Hide,
    Bone,
    Fang,
    Heart,
    Backpack,
    Cloth,
    Rope,
    Crate,
    Hourglass,
    Rune,
    Helmet,
    Chestpiece,
    Gloves,
    Belt,
    Leggings,
    Boots,
    Ring,
    Amulet,
    Mushroom,
    Meat,
    Bread,
    Bowl,
    Grain,
    Turnip,
    Jar,
    Mug,
}

/// <summary>
/// Paints simple, readable placeholder icons (a log, a rock, an ingot, a pickaxe...) in a given color and saves them as
/// sprites, so new resources and items look right in the UI before real art exists. Shapes are drawn from signed
/// distance functions at three times the size and scaled down for smooth edges.
/// </summary>
public static class PlaceholderIconPainter
{
    private const int Supersample = 3;
    private const float Outline = 0.022f;

    /// <summary>Paints the icon and saves it as a new sprite at a unique path based on assetPath.</summary>
    public static Sprite Paint(IconShape shape, Color primary, Color secondary, string assetPath, int size = 128)
    {
        var canvas = new Canvas(size * Supersample);
        Draw(canvas, shape, primary, secondary);
        Texture2D texture = canvas.Downsample(Supersample);
        try
        {
            return SaveSprite(texture, assetPath);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    /// <summary>Writes the texture as a PNG at a unique path next to assetPath and imports it as a UI sprite.</summary>
    public static Sprite SaveSprite(Texture2D texture, string assetPath)
    {
        PrefabBuildUtility.EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
        string path = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>A sensible shape for content, guessed from its name and what it does.</summary>
    public static IconShape Guess(string name, string category, ToolType tool, bool usable, bool backpack, ArmorSlot armor = ArmorSlot.None)
    {
        switch (armor)
        {
            case ArmorSlot.Head:
                return IconShape.Helmet;
            case ArmorSlot.Chest:
            case ArmorSlot.Shoulders:
            case ArmorSlot.Back:
                return IconShape.Chestpiece;
            case ArmorSlot.Hands:
            case ArmorSlot.Wrists:
                return IconShape.Gloves;
            case ArmorSlot.Waist:
                return IconShape.Belt;
            case ArmorSlot.Legs:
                return IconShape.Leggings;
            case ArmorSlot.Feet:
                return IconShape.Boots;
            case ArmorSlot.Neck:
                return IconShape.Amulet;
            case ArmorSlot.Finger:
                return IconShape.Ring;
        }

        switch (tool)
        {
            case ToolType.Axe:
                return IconShape.Axe;
            case ToolType.Pickaxe:
                return IconShape.Pickaxe;
            case ToolType.Sickle:
                return IconShape.Sickle;
        }

        if (usable)
        {
            return IconShape.Potion;
        }

        if (backpack)
        {
            return IconShape.Backpack;
        }

        string text = ((name ?? "") + " " + (category ?? "")).ToLowerInvariant();
        (string word, IconShape shape)[] rules =
        {
            ("hourglass", IconShape.Hourglass), ("sand", IconShape.Hourglass), ("sigil", IconShape.Rune), ("rune", IconShape.Rune),
            ("charm", IconShape.Rune), ("totem", IconShape.Rune), ("talisman", IconShape.Rune),
            ("plank", IconShape.Plank), ("beam", IconShape.Plank), ("brick", IconShape.Brick), ("block", IconShape.Brick),
            ("ingot", IconShape.Ingot), ("hide", IconShape.Hide), ("pelt", IconShape.Hide), ("leather", IconShape.Hide),
            ("bone", IconShape.Bone), ("fang", IconShape.Fang), ("tooth", IconShape.Fang), ("heart", IconShape.Heart),
            ("trophy", IconShape.Heart), ("rope", IconShape.Rope), ("linen", IconShape.Cloth), ("cloth", IconShape.Cloth),
            ("ore", IconShape.Ore), ("orb", IconShape.Orb), ("essence", IconShape.Orb), ("fiber", IconShape.Fiber),
            ("flax", IconShape.Fiber), ("weed", IconShape.Fiber), ("wood", IconShape.Log), ("stone", IconShape.Rock),
            ("granite", IconShape.Rock), ("rock", IconShape.Rock),
        };

        foreach ((string word, IconShape shape) in rules)
        {
            if (text.Contains(word))
            {
                return shape;
            }
        }

        return IconShape.Crate;
    }

    private static void Draw(Canvas canvas, IconShape shape, Color primary, Color secondary)
    {
        Color dark = Scale(primary, 0.55f);
        Color light = Color.Lerp(primary, Color.white, 0.35f);
        var wood = new Color(0.52f, 0.34f, 0.18f);

        switch (shape)
        {
            case IconShape.Log:
                canvas.Draw(Circle(0.5f, 0.5f, 0.38f), Scale(primary, 0.6f));
                canvas.Draw(Circle(0.5f, 0.5f, 0.31f), light, 0f);
                canvas.Draw(Ring(0.5f, 0.5f, 0.23f, 0.012f), Scale(primary, 0.8f), 0f, false);
                canvas.Draw(Ring(0.5f, 0.5f, 0.15f, 0.012f), Scale(primary, 0.8f), 0f, false);
                canvas.Draw(Circle(0.5f, 0.5f, 0.045f), Scale(primary, 0.75f), 0f, false);
                break;

            case IconShape.Rock:
                canvas.Draw(RockShape(), primary);
                canvas.Highlight(0.4f, 0.6f, 0.1f, 0.05f, 0.3f);
                break;

            case IconShape.Ore:
                canvas.Draw(RockShape(), primary);
                foreach (Vector3 speck in new[] { new Vector3(0.36f, 0.46f, 0.06f), new Vector3(0.6f, 0.4f, 0.05f), new Vector3(0.5f, 0.6f, 0.055f), new Vector3(0.68f, 0.56f, 0.04f), new Vector3(0.32f, 0.62f, 0.04f) })
                {
                    canvas.Draw(Circle(speck.x, speck.y, speck.z), secondary, 0.012f);
                }

                canvas.Highlight(0.4f, 0.62f, 0.08f, 0.04f, 0.25f);
                break;

            case IconShape.Orb:
                canvas.Glow(0.5f, 0.5f, 0.3f, 0.18f, secondary);
                canvas.DrawRadial(Circle(0.5f, 0.5f, 0.3f), primary, new Vector2(0.42f, 0.6f));
                canvas.Highlight(0.42f, 0.62f, 0.08f, 0.05f, 0.65f);
                break;

            case IconShape.Fiber:
                for (int i = 0; i < 5; i++)
                {
                    float spread = (i - 2) * 0.09f;
                    canvas.Draw(Capsule(0.5f + spread * 0.25f, 0.14f, 0.5f + spread, 0.86f, 0.028f), i % 2 == 0 ? primary : light, 0.016f);
                }

                canvas.Draw(Capsule(0.38f, 0.33f, 0.62f, 0.33f, 0.04f), secondary, 0.016f);
                break;

            case IconShape.Plank:
                canvas.Draw(Box(0.5f, 0.4f, 0.37f, 0.09f, -18f, 0.02f), Scale(primary, 0.9f));
                canvas.Draw(Box(0.5f, 0.58f, 0.37f, 0.09f, -18f, 0.02f), primary);
                canvas.Draw(Capsule(0.28f, 0.66f, 0.62f, 0.55f, 0.008f), dark, 0f, false);
                canvas.Draw(Capsule(0.36f, 0.45f, 0.72f, 0.33f, 0.008f), dark, 0f, false);
                break;

            case IconShape.Brick:
                canvas.Draw(Box(0.33f, 0.36f, 0.15f, 0.09f, 0f, 0.015f), primary);
                canvas.Draw(Box(0.67f, 0.36f, 0.15f, 0.09f, 0f, 0.015f), Scale(primary, 0.92f));
                canvas.Draw(Box(0.5f, 0.56f, 0.15f, 0.09f, 0f, 0.015f), Scale(primary, 1.05f));
                canvas.Draw(Box(0.5f, 0.76f, 0.15f, 0.09f, 0f, 0.015f), Scale(primary, 0.95f));
                break;

            case IconShape.Ingot:
                canvas.Draw(Polygon(new Vector2(0.16f, 0.32f), new Vector2(0.84f, 0.32f), new Vector2(0.72f, 0.6f), new Vector2(0.28f, 0.6f)), primary);
                canvas.Draw(Polygon(new Vector2(0.28f, 0.6f), new Vector2(0.72f, 0.6f), new Vector2(0.66f, 0.68f), new Vector2(0.34f, 0.68f)), light);
                canvas.Highlight(0.42f, 0.46f, 0.12f, 0.025f, 0.35f);
                break;

            case IconShape.Axe:
                canvas.Draw(Capsule(0.28f, 0.14f, 0.6f, 0.84f, 0.035f), wood);
                canvas.Draw(Polygon(new Vector2(0.52f, 0.76f), new Vector2(0.8f, 0.9f), new Vector2(0.88f, 0.6f), new Vector2(0.58f, 0.62f)), primary);
                break;

            case IconShape.Pickaxe:
                canvas.Draw(Capsule(0.3f, 0.14f, 0.56f, 0.8f, 0.035f), wood);
                canvas.Draw(Polygon(new Vector2(0.18f, 0.66f), new Vector2(0.42f, 0.86f), new Vector2(0.72f, 0.86f), new Vector2(0.94f, 0.64f),
                    new Vector2(0.7f, 0.76f), new Vector2(0.44f, 0.76f)), primary);
                break;

            case IconShape.Sickle:
                canvas.Draw(Capsule(0.32f, 0.12f, 0.4f, 0.4f, 0.04f), wood);
                canvas.Draw(Subtract(Circle(0.55f, 0.6f, 0.3f), Circle(0.64f, 0.5f, 0.25f)), primary);
                break;

            case IconShape.Potion:
                canvas.Draw(Circle(0.5f, 0.38f, 0.26f), new Color(0.85f, 0.9f, 0.95f, 0.35f));
                canvas.Draw(Intersect(Circle(0.5f, 0.38f, 0.22f), HalfPlaneBelow(0.44f)), primary, 0f);
                canvas.Draw(Box(0.5f, 0.66f, 0.07f, 0.1f, 0f, 0.02f), new Color(0.85f, 0.9f, 0.95f, 0.45f));
                canvas.Draw(Box(0.5f, 0.8f, 0.085f, 0.05f, 0f, 0.02f), wood);
                canvas.Highlight(0.4f, 0.46f, 0.05f, 0.08f, 0.5f);
                break;

            case IconShape.Hide:
                canvas.Draw(Polygon(new Vector2(0.5f, 0.88f), new Vector2(0.64f, 0.74f), new Vector2(0.86f, 0.78f), new Vector2(0.76f, 0.52f),
                    new Vector2(0.86f, 0.24f), new Vector2(0.62f, 0.3f), new Vector2(0.5f, 0.12f), new Vector2(0.38f, 0.3f),
                    new Vector2(0.14f, 0.24f), new Vector2(0.24f, 0.52f), new Vector2(0.14f, 0.78f), new Vector2(0.36f, 0.74f)), primary);
                canvas.Draw(Ellipse(0.5f, 0.52f, 0.14f, 0.22f, 0f), light, 0f);
                break;

            case IconShape.Bone:
                canvas.Draw(Union(Capsule(0.3f, 0.3f, 0.7f, 0.7f, 0.06f), Circle(0.22f, 0.32f, 0.075f), Circle(0.32f, 0.22f, 0.075f),
                    Circle(0.78f, 0.68f, 0.075f), Circle(0.68f, 0.78f, 0.075f)), primary);
                break;

            case IconShape.Fang:
                canvas.Draw(Polygon(new Vector2(0.36f, 0.84f), new Vector2(0.64f, 0.84f), new Vector2(0.6f, 0.5f), new Vector2(0.5f, 0.14f),
                    new Vector2(0.44f, 0.5f)), primary);
                canvas.Highlight(0.47f, 0.62f, 0.03f, 0.12f, 0.35f);
                break;

            case IconShape.Heart:
                canvas.Draw(Union(Circle(0.37f, 0.6f, 0.17f), Circle(0.63f, 0.6f, 0.17f),
                    Polygon(new Vector2(0.21f, 0.55f), new Vector2(0.79f, 0.55f), new Vector2(0.5f, 0.16f))), primary);
                canvas.Highlight(0.36f, 0.66f, 0.06f, 0.04f, 0.45f);
                break;

            case IconShape.Backpack:
                canvas.Draw(Ring(0.5f, 0.74f, 0.1f, 0.025f), dark, 0.01f);
                canvas.Draw(Box(0.5f, 0.44f, 0.26f, 0.3f, 0f, 0.08f), primary);
                canvas.Draw(Box(0.5f, 0.62f, 0.26f, 0.12f, 0f, 0.06f), Scale(primary, 0.8f));
                canvas.Draw(Box(0.5f, 0.3f, 0.14f, 0.08f, 0f, 0.03f), light);
                break;

            case IconShape.Cloth:
                canvas.Draw(Box(0.5f, 0.34f, 0.3f, 0.07f, 0f, 0.03f), Scale(primary, 0.85f));
                canvas.Draw(Box(0.5f, 0.48f, 0.3f, 0.07f, 0f, 0.03f), primary);
                canvas.Draw(Box(0.5f, 0.62f, 0.3f, 0.07f, 0f, 0.03f), light);
                break;

            case IconShape.Rope:
                canvas.Draw(Ring(0.5f, 0.5f, 0.3f, 0.05f), primary, 0.012f);
                canvas.Draw(Ring(0.5f, 0.5f, 0.19f, 0.05f), Scale(primary, 0.9f), 0.012f);
                canvas.Draw(Capsule(0.75f, 0.32f, 0.88f, 0.14f, 0.05f), primary, 0.012f);
                break;

            case IconShape.Hourglass:
            {
                var glass = new Color(0.85f, 0.9f, 0.95f, 0.35f);
                Shape upper = Polygon(new Vector2(0.32f, 0.79f), new Vector2(0.68f, 0.79f), new Vector2(0.53f, 0.5f), new Vector2(0.47f, 0.5f));
                Shape lower = Polygon(new Vector2(0.47f, 0.5f), new Vector2(0.53f, 0.5f), new Vector2(0.68f, 0.21f), new Vector2(0.32f, 0.21f));
                canvas.Draw(Union(upper, lower), glass);
                canvas.Draw(Intersect(upper, HalfPlaneBelow(0.63f)), primary, 0f);
                canvas.Draw(Intersect(lower, HalfPlaneBelow(0.35f)), primary, 0f);
                canvas.Draw(Capsule(0.5f, 0.5f, 0.5f, 0.35f, 0.008f), primary, 0f, false);
                canvas.Draw(Capsule(0.26f, 0.18f, 0.26f, 0.82f, 0.022f), wood);
                canvas.Draw(Capsule(0.74f, 0.18f, 0.74f, 0.82f, 0.022f), wood);
                canvas.Draw(Box(0.5f, 0.85f, 0.29f, 0.045f, 0f, 0.02f), wood);
                canvas.Draw(Box(0.5f, 0.15f, 0.29f, 0.045f, 0f, 0.02f), wood);
                canvas.Highlight(0.42f, 0.68f, 0.03f, 0.07f, 0.4f);
                break;
            }

            case IconShape.Rune:
            {
                var stone = new Color(0.42f, 0.43f, 0.47f);
                canvas.Glow(0.5f, 0.5f, 0.3f, 0.2f, secondary);
                canvas.Draw(Box(0.5f, 0.5f, 0.26f, 0.34f, 0f, 0.1f), stone);
                canvas.Draw(Capsule(0.5f, 0.28f, 0.5f, 0.72f, 0.032f), primary, 0f);
                canvas.Draw(Capsule(0.5f, 0.6f, 0.36f, 0.46f, 0.028f), primary, 0f);
                canvas.Draw(Capsule(0.5f, 0.6f, 0.64f, 0.46f, 0.028f), primary, 0f);
                canvas.Draw(Ring(0.5f, 0.34f, 0.07f, 0.024f), light, 0f);
                canvas.Highlight(0.38f, 0.72f, 0.06f, 0.04f, 0.3f);
                break;
            }

            case IconShape.Helmet:
                canvas.Draw(Intersect(Circle(0.5f, 0.42f, 0.32f), Subtract(Box(0.5f, 0.6f, 0.4f, 0.32f, 0f, 0f), Box(0.5f, 0.24f, 0.12f, 0.14f, 0f, 0.03f))), primary);
                canvas.Draw(Box(0.5f, 0.42f, 0.34f, 0.045f, 0f, 0.02f), dark, 0.012f);
                canvas.Draw(Capsule(0.5f, 0.74f, 0.5f, 0.46f, 0.028f), light, 0f);
                canvas.Highlight(0.38f, 0.62f, 0.06f, 0.08f, 0.3f);
                break;

            case IconShape.Chestpiece:
                canvas.Draw(Polygon(new Vector2(0.22f, 0.8f), new Vector2(0.4f, 0.84f), new Vector2(0.5f, 0.74f), new Vector2(0.6f, 0.84f),
                    new Vector2(0.78f, 0.8f), new Vector2(0.86f, 0.6f), new Vector2(0.72f, 0.56f), new Vector2(0.7f, 0.16f),
                    new Vector2(0.3f, 0.16f), new Vector2(0.28f, 0.56f), new Vector2(0.14f, 0.6f)), primary);
                canvas.Draw(Capsule(0.5f, 0.7f, 0.5f, 0.2f, 0.012f), dark, 0f, false);
                canvas.Draw(Box(0.5f, 0.3f, 0.2f, 0.03f, 0f, 0.01f), secondary, 0f);
                canvas.Highlight(0.4f, 0.6f, 0.06f, 0.1f, 0.25f);
                break;

            case IconShape.Gloves:
                canvas.Draw(Union(Box(0.5f, 0.42f, 0.17f, 0.2f, 0f, 0.06f), Capsule(0.39f, 0.6f, 0.39f, 0.8f, 0.045f),
                    Capsule(0.48f, 0.62f, 0.48f, 0.84f, 0.045f), Capsule(0.57f, 0.62f, 0.57f, 0.82f, 0.045f),
                    Capsule(0.65f, 0.58f, 0.66f, 0.74f, 0.04f), Capsule(0.33f, 0.44f, 0.22f, 0.58f, 0.05f)), primary);
                canvas.Draw(Box(0.5f, 0.2f, 0.19f, 0.06f, 0f, 0.02f), dark);
                break;

            case IconShape.Belt:
                canvas.Draw(Box(0.5f, 0.5f, 0.4f, 0.08f, -8f, 0.03f), primary);
                canvas.Draw(Ring(0.5f, 0.5f, 0.1f, 0.03f), secondary, 0.01f);
                canvas.Draw(Capsule(0.5f, 0.5f, 0.6f, 0.5f, 0.015f), secondary, 0f);
                break;

            case IconShape.Leggings:
                canvas.Draw(Union(Box(0.5f, 0.76f, 0.22f, 0.07f, 0f, 0.02f), Polygon(new Vector2(0.28f, 0.72f), new Vector2(0.5f, 0.72f),
                    new Vector2(0.48f, 0.14f), new Vector2(0.3f, 0.14f)), Polygon(new Vector2(0.5f, 0.72f), new Vector2(0.72f, 0.72f),
                    new Vector2(0.7f, 0.14f), new Vector2(0.52f, 0.14f))), primary);
                canvas.Draw(Box(0.5f, 0.78f, 0.22f, 0.03f, 0f, 0.01f), dark, 0f);
                break;

            case IconShape.Boots:
                canvas.Draw(Union(Box(0.4f, 0.56f, 0.12f, 0.24f, 0f, 0.04f), Box(0.52f, 0.26f, 0.24f, 0.09f, 0f, 0.06f)), primary);
                canvas.Draw(Box(0.52f, 0.16f, 0.26f, 0.03f, 0f, 0.01f), dark);
                canvas.Draw(Box(0.4f, 0.76f, 0.13f, 0.04f, 0f, 0.015f), light, 0f);
                break;

            case IconShape.Ring:
                canvas.Draw(Ring(0.5f, 0.42f, 0.22f, 0.07f), primary);
                canvas.Draw(Circle(0.5f, 0.68f, 0.09f), secondary, 0.012f);
                canvas.Highlight(0.47f, 0.71f, 0.03f, 0.02f, 0.6f);
                break;

            case IconShape.Amulet:
                canvas.Draw(Ring(0.5f, 0.62f, 0.24f, 0.022f), dark, 0.008f);
                canvas.Draw(Circle(0.5f, 0.32f, 0.15f), primary);
                canvas.Draw(Circle(0.5f, 0.32f, 0.075f), secondary, 0.012f);
                canvas.Highlight(0.45f, 0.37f, 0.03f, 0.02f, 0.6f);
                break;

            case IconShape.Mushroom:
                canvas.Draw(Box(0.5f, 0.32f, 0.08f, 0.18f, 0f, 0.05f), secondary);
                canvas.Draw(Intersect(Ellipse(0.5f, 0.5f, 0.32f, 0.26f, 0f), Box(0.5f, 0.66f, 0.34f, 0.2f, 0f, 0f)), primary);
                canvas.Highlight(0.4f, 0.62f, 0.07f, 0.04f, 0.4f);
                break;

            case IconShape.Meat:
                canvas.Draw(Ellipse(0.46f, 0.54f, 0.28f, 0.22f, -25f), primary);
                canvas.Draw(Capsule(0.66f, 0.38f, 0.82f, 0.22f, 0.04f), secondary);
                canvas.Draw(Circle(0.42f, 0.56f, 0.06f), light, 0f);
                break;

            case IconShape.Bread:
                canvas.Draw(Union(Ellipse(0.5f, 0.44f, 0.34f, 0.2f, 0f), Box(0.5f, 0.36f, 0.32f, 0.1f, 0f, 0.04f)), primary);
                canvas.Draw(Capsule(0.36f, 0.5f, 0.42f, 0.42f, 0.014f), dark, 0f);
                canvas.Draw(Capsule(0.5f, 0.52f, 0.56f, 0.44f, 0.014f), dark, 0f);
                canvas.Draw(Capsule(0.64f, 0.5f, 0.7f, 0.42f, 0.014f), dark, 0f);
                break;

            case IconShape.Bowl:
                canvas.Draw(Ellipse(0.5f, 0.56f, 0.32f, 0.08f, 0f), secondary);
                canvas.Draw(Intersect(Circle(0.5f, 0.56f, 0.32f), HalfPlaneBelow(0.56f)), wood);
                canvas.Highlight(0.4f, 0.57f, 0.07f, 0.02f, 0.4f);
                break;

            case IconShape.Grain:
                canvas.Draw(Capsule(0.5f, 0.14f, 0.5f, 0.82f, 0.012f), dark, 0f);
                foreach (float y in new[] { 0.42f, 0.54f, 0.66f, 0.78f })
                {
                    canvas.Draw(Ellipse(0.42f, y, 0.05f, 0.08f, -25f), primary, 0.01f);
                    canvas.Draw(Ellipse(0.58f, y, 0.05f, 0.08f, 25f), primary, 0.01f);
                }

                break;

            case IconShape.Turnip:
                canvas.Draw(Capsule(0.5f, 0.64f, 0.42f, 0.84f, 0.02f), secondary, 0.008f);
                canvas.Draw(Capsule(0.5f, 0.64f, 0.58f, 0.84f, 0.02f), secondary, 0.008f);
                canvas.Draw(Union(Circle(0.5f, 0.46f, 0.22f), Polygon(new Vector2(0.38f, 0.32f), new Vector2(0.62f, 0.32f), new Vector2(0.5f, 0.12f))), primary);
                canvas.Highlight(0.42f, 0.52f, 0.05f, 0.05f, 0.4f);
                break;

            case IconShape.Jar:
                canvas.Draw(Box(0.5f, 0.42f, 0.22f, 0.26f, 0f, 0.07f), primary);
                canvas.Draw(Box(0.5f, 0.72f, 0.17f, 0.05f, 0f, 0.02f), secondary);
                canvas.Highlight(0.42f, 0.5f, 0.04f, 0.1f, 0.45f);
                break;

            case IconShape.Mug:
                canvas.Draw(Ring(0.72f, 0.46f, 0.11f, 0.04f), wood);
                canvas.Draw(Box(0.46f, 0.44f, 0.22f, 0.26f, 0f, 0.03f), wood);
                canvas.Draw(Ellipse(0.46f, 0.7f, 0.22f, 0.06f, 0f), primary);
                break;

            default:
                canvas.Draw(Box(0.5f, 0.5f, 0.32f, 0.3f, 0f, 0.03f), primary);
                canvas.Draw(Capsule(0.22f, 0.5f, 0.78f, 0.5f, 0.012f), dark, 0f, false);
                canvas.Draw(Capsule(0.22f, 0.28f, 0.78f, 0.72f, 0.012f), dark, 0f, false);
                break;
        }
    }

    // ----- Shapes, as signed distance functions in normalized coordinates (0,0 bottom left to 1,1 top right) -----

    private readonly struct Shape
    {
        public readonly Func<Vector2, float> Distance;
        public readonly Rect Bounds;

        public Shape(Func<Vector2, float> distance, Rect bounds)
        {
            Distance = distance;
            Bounds = bounds;
        }
    }

    private static Shape RockShape()
    {
        return Union(Ellipse(0.5f, 0.44f, 0.34f, 0.26f, -8f), Ellipse(0.38f, 0.54f, 0.2f, 0.18f, 0f), Ellipse(0.63f, 0.55f, 0.19f, 0.17f, 0f));
    }

    private static Shape Circle(float x, float y, float radius)
    {
        var center = new Vector2(x, y);
        return new Shape(p => (p - center).magnitude - radius, new Rect(x - radius, y - radius, radius * 2f, radius * 2f));
    }

    private static Shape Ring(float x, float y, float radius, float width)
    {
        var center = new Vector2(x, y);
        float outer = radius + width;
        return new Shape(p => Mathf.Abs((p - center).magnitude - radius) - width * 0.5f, new Rect(x - outer, y - outer, outer * 2f, outer * 2f));
    }

    // Not an exact distance away from the edge, but close enough for fills and thin outlines.
    private static Shape Ellipse(float x, float y, float radiusX, float radiusY, float degrees)
    {
        var center = new Vector2(x, y);
        float angle = -degrees * Mathf.Deg2Rad;
        float size = Mathf.Max(radiusX, radiusY);
        return new Shape(p =>
        {
            Vector2 local = Rotate(p - center, angle);
            float k = new Vector2(local.x / radiusX, local.y / radiusY).magnitude;
            return (k - 1f) * Mathf.Min(radiusX, radiusY);
        }, new Rect(x - size, y - size, size * 2f, size * 2f));
    }

    private static Shape Capsule(float ax, float ay, float bx, float by, float radius)
    {
        var a = new Vector2(ax, ay);
        var b = new Vector2(bx, by);
        return new Shape(p =>
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude - radius;
        }, Rect.MinMaxRect(Mathf.Min(ax, bx) - radius, Mathf.Min(ay, by) - radius, Mathf.Max(ax, bx) + radius, Mathf.Max(ay, by) + radius));
    }

    private static Shape Box(float x, float y, float halfWidth, float halfHeight, float degrees, float corner)
    {
        var center = new Vector2(x, y);
        float angle = -degrees * Mathf.Deg2Rad;
        float size = new Vector2(halfWidth, halfHeight).magnitude;
        return new Shape(p =>
        {
            Vector2 local = Rotate(p - center, angle);
            Vector2 q = new Vector2(Mathf.Abs(local.x) - halfWidth + corner, Mathf.Abs(local.y) - halfHeight + corner);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
        }, new Rect(x - size, y - size, size * 2f, size * 2f));
    }

    private static Shape Polygon(params Vector2[] points)
    {
        Rect bounds = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
        foreach (Vector2 point in points)
        {
            bounds.xMin = Mathf.Min(bounds.xMin, point.x);
            bounds.yMin = Mathf.Min(bounds.yMin, point.y);
            bounds.xMax = Mathf.Max(bounds.xMax, point.x);
            bounds.yMax = Mathf.Max(bounds.yMax, point.y);
        }

        return new Shape(p =>
        {
            float distance = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[j];
                Vector2 b = points[i];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                distance = Mathf.Min(distance, (p - (a + ab * t)).magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside ? -distance : distance;
        }, bounds);
    }

    private static Shape HalfPlaneBelow(float y)
    {
        return new Shape(p => p.y - y, new Rect(0f, 0f, 1f, y));
    }

    private static Shape Union(params Shape[] shapes)
    {
        Rect bounds = shapes[0].Bounds;
        foreach (Shape shape in shapes)
        {
            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, shape.Bounds.xMin), Mathf.Min(bounds.yMin, shape.Bounds.yMin),
                Mathf.Max(bounds.xMax, shape.Bounds.xMax), Mathf.Max(bounds.yMax, shape.Bounds.yMax));
        }

        return new Shape(p =>
        {
            float distance = float.MaxValue;
            foreach (Shape shape in shapes)
            {
                distance = Mathf.Min(distance, shape.Distance(p));
            }

            return distance;
        }, bounds);
    }

    private static Shape Subtract(Shape shape, Shape cut)
    {
        return new Shape(p => Mathf.Max(shape.Distance(p), -cut.Distance(p)), shape.Bounds);
    }

    private static Shape Intersect(Shape a, Shape b)
    {
        return new Shape(p => Mathf.Max(a.Distance(p), b.Distance(p)), a.Bounds);
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    private static Color Scale(Color color, float factor)
    {
        return new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), color.a);
    }

    // ----- Pixels -----

    private sealed class Canvas
    {
        private readonly int size;
        private readonly Color[] pixels;

        public Canvas(int size)
        {
            this.size = size;
            pixels = new Color[size * size];
        }

        /// <summary>Fills the shape with a top-lit gradient and a darker outline around it.</summary>
        public void Draw(Shape shape, Color fill, float outline = Outline, bool shade = true)
        {
            Color edge = new Color(fill.r * 0.4f, fill.g * 0.4f, fill.b * 0.4f, Mathf.Max(fill.a, 0.9f));
            Rect bounds = shape.Bounds;
            Fill(Grow(bounds, outline), p =>
            {
                float distance = shape.Distance(p);
                if (distance <= 0f)
                {
                    if (!shade)
                    {
                        return fill;
                    }

                    float height = Mathf.InverseLerp(bounds.yMin, bounds.yMax, p.y);
                    return Scale(fill, 0.82f + 0.32f * height);
                }

                return distance <= outline ? edge : Color.clear;
            });
        }

        /// <summary>Fills the shape with a sphere-like gradient lit from the given point.</summary>
        public void DrawRadial(Shape shape, Color fill, Vector2 light)
        {
            Rect bounds = shape.Bounds;
            float radius = bounds.width * 0.5f;
            Color edge = Scale(fill, 0.4f);
            Fill(Grow(bounds, Outline), p =>
            {
                float distance = shape.Distance(p);
                if (distance > Outline)
                {
                    return Color.clear;
                }

                if (distance > 0f)
                {
                    return edge;
                }

                float t = Mathf.Clamp01((p - light).magnitude / (radius * 1.6f));
                return Color.Lerp(Color.Lerp(fill, Color.white, 0.45f), Scale(fill, 0.55f), t);
            });
        }

        /// <summary>A soft halo that fades out from the edge of a circle.</summary>
        public void Glow(float x, float y, float radius, float width, Color color)
        {
            var center = new Vector2(x, y);
            float outer = radius + width;
            Fill(new Rect(x - outer, y - outer, outer * 2f, outer * 2f), p =>
            {
                float distance = (p - center).magnitude - radius;
                float alpha = 0.45f * (1f - Mathf.Clamp01(distance / width));
                return distance <= width ? new Color(color.r, color.g, color.b, alpha) : Color.clear;
            });
        }

        /// <summary>A soft white shine.</summary>
        public void Highlight(float x, float y, float radiusX, float radiusY, float strength)
        {
            Shape shape = Ellipse(x, y, radiusX, radiusY, -25f);
            Fill(shape.Bounds, p =>
            {
                float distance = shape.Distance(p);
                float alpha = strength * Mathf.Clamp01(-distance / (Mathf.Min(radiusX, radiusY) * 0.8f));
                return distance <= 0f ? new Color(1f, 1f, 1f, alpha) : Color.clear;
            });
        }

        public Texture2D Downsample(int factor)
        {
            int target = size / factor;
            var result = new Color[target * target];
            float samples = factor * factor;
            for (int y = 0; y < target; y++)
            {
                for (int x = 0; x < target; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < factor; sy++)
                    {
                        for (int sx = 0; sx < factor; sx++)
                        {
                            Color c = pixels[(y * factor + sy) * size + x * factor + sx];
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }

                    result[y * target + x] = a > 0f ? new Color(r / a, g / a, b / a, a / samples) : Color.clear;
                }
            }

            var texture = new Texture2D(target, target, TextureFormat.RGBA32, false);
            texture.SetPixels(result);
            texture.Apply();
            return texture;
        }

        private void Fill(Rect bounds, Func<Vector2, Color> shader)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(bounds.xMin * size), 0, size - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(bounds.xMax * size), 0, size - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(bounds.yMin * size), 0, size - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(bounds.yMax * size), 0, size - 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Color color = shader(new Vector2((x + 0.5f) / size, (y + 0.5f) / size));
                    if (color.a > 0f)
                    {
                        Blend(y * size + x, color);
                    }
                }
            }
        }

        private void Blend(int index, Color color)
        {
            Color below = pixels[index];
            float alpha = color.a + below.a * (1f - color.a);
            if (alpha <= 0f)
            {
                return;
            }

            pixels[index] = new Color(
                (color.r * color.a + below.r * below.a * (1f - color.a)) / alpha,
                (color.g * color.a + below.g * below.a * (1f - color.a)) / alpha,
                (color.b * color.a + below.b * below.a * (1f - color.a)) / alpha,
                alpha);
        }

        private static Rect Grow(Rect rect, float amount)
        {
            return Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Decals pressed onto the ground (paths, the places' rune rings, craters and mud from heavy blows, blood and footprints, trampled
/// ground and scorch round buildings, the Haven's worn footpaths) paint the ground and nothing else: the player standing in one,
/// a monster, a rock or a log beside it are left alone (the user, 2026-11-20: "this should be standard on EVERY ground decal").
/// <para>URP decal layers (the Decal renderer feature's Use Rendering Layers): a ground decal draws only on renderers with the
/// <see cref="LayerName"/> rendering layer, which the terrains are given here as they load (Default kept, so their lights are
/// unchanged). Decals meant for other things keep Default: grime up a wall's foot, blood and mud on the player's body.</para>
/// <para>The boxes stay thin and laid along the ground as before (PathPiece, GroundRing), which keeps their cost down.</para>
/// </summary>
public static class GroundDecals
{
    /// <summary>The rendering layer the ground wears (Project Settings › Tags and Layers › Rendering Layers).</summary>
    public const int LayerIndex = 8;
    public const string LayerName = "Ground";
    public const uint Mask = 1u << LayerIndex;

    /// <summary>Makes <paramref name="projector"/> a ground decal, and makes sure the terrains loaded now take it.</summary>
    public static void Paint(DecalProjector projector)
    {
        if (projector != null)
        {
            projector.renderingLayerMask = Mask;
        }

        MarkTerrains();
    }

    /// <summary>Puts every active terrain on the Ground layer as well as its own.</summary>
    public static void MarkTerrains()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain != null && (terrain.renderingLayerMask & Mask) == 0)
            {
                terrain.renderingLayerMask |= Mask;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MarkTerrains();
}

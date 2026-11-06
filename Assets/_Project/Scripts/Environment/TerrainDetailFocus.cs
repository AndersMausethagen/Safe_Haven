using UnityEngine;

/// <summary>
/// Where the tessellated ground and rocks keep their full relief: round the player, not the camera. The game camera hangs
/// 20-40 m above a top-down view, so relief faded by distance from it covered nearly the whole screen (in the Misty Woods the
/// GPU spent about 17 ms of a 30 ms frame tessellating it). The game camera sets this each frame; with none (edit mode, the
/// Scene view) the shaders fade by camera distance as before. Read by Terrain (World) and Mesh Surface (Tessellated).
/// </summary>
public static class TerrainDetailFocus
{
    private static readonly int FocusId = Shader.PropertyToID("_TerrainDetailFocus");

    public static void Set(Vector3 position) => Shader.SetGlobalVector(FocusId, new Vector4(position.x, position.y, position.z, 1f));

    public static void Clear() => Shader.SetGlobalVector(FocusId, Vector4.zero);
}

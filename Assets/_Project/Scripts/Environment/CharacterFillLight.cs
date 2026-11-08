using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A soft light that lights characters only (the player, creatures), from the camera's side. In the Misty Woods the dark
/// litter and the dark figures on it were one value from the top-down camera, so a fight read as shapes in the murk; this
/// lifts the figures a little without touching the ground, the trees or the mood. It is drawn by the character variant of
/// Lit (See-Through) (CharacterShading.hlsl), not by a Light on a light layer: light layers doubled the variants of nearly
/// every shader in a build. This sets where it hangs and how bright it is each frame. On the player; it follows them.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterFillLight : MonoBehaviour
{
    private static readonly int FillPositionId = Shader.PropertyToID("_SafeHavenCharacterFillPosition");
    private static readonly int FillColourId = Shader.PropertyToID("_SafeHavenCharacterFillColour");

    [Tooltip("Where the light hangs from the player: up and towards the camera, so figures are lit on the side seen.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -3f);
    [SerializeField, Min(0f)] private float intensity = 0.9f;
    [SerializeField, Min(1f)] private float range = 16f;
    [SerializeField] private Color colour = new Color(1f, 0.95f, 0.88f);

    /// <summary>Kept for callers from before the fill moved into the character shader: characters wear that shader's
    /// variant on their materials now (AgentScripts/BuildCharacterLook.cs), so there is nothing to mark.</summary>
    public static void Mark(GameObject character)
    {
    }

    private void OnDisable()
    {
        Shader.SetGlobalVector(FillColourId, Vector4.zero);
    }

    private void LateUpdate()
    {
        // World-space offset: the camera looks north, so the light keeps to the camera's side however the player turns.
        Vector3 at = transform.position + offset;
        Color lit = colour.linear * intensity;
        Shader.SetGlobalVector(FillPositionId, new Vector4(at.x, at.y, at.z, range));
        Shader.SetGlobalVector(FillColourId, new Vector4(lit.r, lit.g, lit.b, 0f));
    }
}

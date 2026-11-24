using UnityEngine;

/// <summary>
/// A skill's area on the ground while it winds up (SkillLook.showArea): the same projected fan as a monster's warning (GroundMarks), in the
/// skill's school colour rather than a warning's red, so the player sees where it will land and the two never read alike. It brightens
/// as the release comes and flashes as it lands, then fades. One mark, reused.
/// </summary>
public sealed class SkillAreaMark : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const float FadeSeconds = 0.25f;

    private static SkillAreaMark current;
    private MeshFilter filter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private Color colour;
    private float startedAt;
    private float releaseAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>Shows the area <paramref name="reach"/> metres out over <paramref name="arc"/> degrees along <paramref name="aim"/>, landing in
    /// <paramref name="windUp"/> seconds.</summary>
    public static void Show(Vector3 origin, Vector3 aim, float reach, float arc, Color colour, float windUp)
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (GroundMarks.MaterialFor(feel) == null)
        {
            return;
        }

        if (current == null)
        {
            var go = new GameObject("Skill Area Mark");
            current = go.AddComponent<SkillAreaMark>();
            current.filter = go.AddComponent<MeshFilter>();
            current.meshRenderer = go.AddComponent<MeshRenderer>();
            current.meshRenderer.sharedMaterial = GroundMarks.MaterialFor(feel);
            current.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            current.meshRenderer.receiveShadows = false;
            current.block = new MaterialPropertyBlock();
        }

        aim.y = 0f;
        Transform mark = current.transform;
        mark.SetPositionAndRotation(origin, Quaternion.LookRotation(aim.sqrMagnitude > 0.0001f ? aim : Vector3.forward, Vector3.up));
        mark.localScale = GroundMarks.Scale(reach);
        current.filter.sharedMesh = GroundMarks.FanFor(feel, arc);
        current.colour = colour;
        current.startedAt = Time.time;
        current.releaseAt = Time.time + windUp;
        GroundMarks.Ground(current.block, origin);
        GroundMarks.Shape(current.block, arc, 0f, 0.07f, GroundMarks.Pattern.Solid, 0.5f, 1f);
        current.gameObject.SetActive(true);
        current.Paint(0f, 0f);
    }

    private void Update()
    {
        float now = Time.time;
        float windUp = Mathf.Max(0.05f, releaseAt - startedAt);
        if (now < releaseAt)
        {
            // Faint, brightening as it comes: it shows the area, it shouldn't hide the ground or the creatures on it.
            Paint(Mathf.Lerp(0.15f, 0.38f, (now - startedAt) / windUp), 0f);
            return;
        }

        float after = (now - releaseAt) / FadeSeconds;
        if (after >= 1f)
        {
            gameObject.SetActive(false);
            return;
        }

        // A short flash as it lands, softer than a monster's (the effects carry the moment).
        Paint(0.38f * (1f - after), 0.45f * (1f - after));
    }

    private void Paint(float alpha, float flash)
    {
        Color shown = colour;
        shown.a = alpha;
        block.SetColor(BaseColorId, shown);
        block.SetColor(ColorId, shown);
        GroundMarks.Flash(block, flash);
        meshRenderer.SetPropertyBlock(block);
    }
}

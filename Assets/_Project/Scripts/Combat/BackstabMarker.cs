using UnityEngine;

/// <summary>
/// Shows where to stand for a backstab: while a monster is open to one, a pale wedge lies on the ground behind it, as wide as the
/// backstab arc. CombatFeedback shows it when a monster staggers and PlayerFocusTarget when the enemy in focus commits to a strike; it
/// adds itself to the monster and draws with Graphics.RenderMesh, for the reason given in SlashEffect. Colour: Combat Feel › Backstab
/// Marker Color.
/// </summary>
[DisallowMultipleComponent]
public sealed class BackstabMarker : MonoBehaviour
{
    private const float Reach = 1.6f;
    private const float Lift = 0.07f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MonsterAI monster;
    private MaterialPropertyBlock block;
    private float radius;

    /// <summary>Marks the area behind <paramref name="target"/> for as long as it staggers.</summary>
    public static void Show(MonsterAI target)
    {
        if (!target.TryGetComponent(out BackstabMarker marker))
        {
            marker = target.gameObject.AddComponent<BackstabMarker>();
        }

        marker.enabled = true;
    }

    private void Awake()
    {
        monster = GetComponent<MonsterAI>();
        block = new MaterialPropertyBlock();
        radius = Reach;
        if (TryGetComponent(out Collider body))
        {
            Vector3 size = body.bounds.extents;
            radius += Mathf.Max(size.x, size.z);
        }
    }

    private void LateUpdate()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        Material material = GroundMarks.MaterialFor(feel);
        if (monster == null || !monster.IsOpenToBackstab || material == null)
        {
            enabled = false;
            return;
        }

        // It warms as the monster learns: a warning that the next one may be seen coming.
        Color color = Color.Lerp(CombatFeelSettings.Current.BackstabMarkerColor, CombatFeelSettings.Current.BackstabMarkerWaryColor, monster.BackstabReadChance);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        Vector3 at = transform.position + Vector3.up * Lift;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        Quaternion behind = Quaternion.LookRotation(forward.sqrMagnitude > 1e-4f ? -forward.normalized : Vector3.back, Vector3.up);
        var draw = new RenderParams(material)
        {
            matProps = block,
            layer = gameObject.layer,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(at, Vector3.one * (radius * 2f + 1f)),
        };
        float arc = CombatTechniqueSettings.Current.BackstabArc;
        GroundMarks.Shape(block, arc, 0f, 0.05f, GroundMarks.Pattern.Solid, 0.4f);
        GroundMarks.Ground(block, transform.position);
        Graphics.RenderMesh(draw, GroundMarks.FanFor(feel, arc), 0, Matrix4x4.TRS(at, behind, GroundMarks.Scale(radius)));
    }
}

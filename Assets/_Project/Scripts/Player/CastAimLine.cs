using UnityEngine;

/// <summary>
/// A staff's cast you can aim (Docs/RangedStealthResearch.md §3.8): through a cast's wind-up a straight line runs from the hand along
/// where the bolt will fly, out to its reach, and the creature it is aimed at is outlined as a bow's target is (ArcheryTargetOutline reads
/// PlayerCombat.CastTarget). The line's material and colour are the bow's trajectory's. Added to the player with ArcheryTrajectoryView.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombat))]
public sealed class CastAimLine : MonoBehaviour
{
    private const float Width = 0.04f;
    private PlayerCombat combat;
    private ArcheryTrajectoryView bowLine;
    private LineRenderer line;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        bowLine = GetComponent<ArcheryTrajectoryView>();
    }

    private void OnDisable()
    {
        if (line != null)
        {
            line.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (line != null)
        {
            Destroy(line.gameObject);
        }
    }

    private void LateUpdate()
    {
        bool casting = combat.IsCasting && Player.Current != null && Player.Current.gameObject == gameObject && !GamePause.IsPaused;
        if (!casting)
        {
            if (line != null)
            {
                line.enabled = false;
            }

            return;
        }

        if (line == null && !Build())
        {
            return;
        }

        AttackDefinition attack = combat.ActiveAttack;
        Vector3 from = transform.position + Vector3.up * 1.3f + combat.ActiveDirection * 0.7f;
        Vector3 to = from + combat.ActiveDirection * Mathf.Max(1f, attack.CastRange);
        // Stopped where scenery would stop the bolt.
        if (Physics.Raycast(from, combat.ActiveDirection, out RaycastHit hit, Mathf.Max(1f, attack.CastRange), ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
        {
            to = hit.point;
        }

        line.SetPosition(0, from);
        line.SetPosition(1, to);
        Color color = bowLine != null ? bowLine.LineColor : new Color(1f, 0.95f, 0.8f, 0.35f);
        line.startColor = color;
        color.a *= 0.3f;
        line.endColor = color;
        line.enabled = true;
    }

    private bool Build()
    {
        Material material = bowLine != null ? bowLine.LineMaterial : null;
        if (material == null)
        {
            return false;
        }

        var holder = new GameObject("Cast Aim Line");
        line = holder.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = Width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return true;
    }
}

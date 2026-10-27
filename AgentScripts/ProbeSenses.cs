using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Play-mode probe for monster senses in a raid (run with `unity command run_script --file AgentScripts/ProbeSenses.cs
/// --entry ProbeSenses.<Step>`, waiting a few seconds between steps). Picks an idle wolf, places the player around it and
/// reports what the wolf knows.
/// </summary>
public static class ProbeSenses
{
    private const string Tag = " [Probe]";

    public static string PickWolf()
    {
        MonsterAI best = null;
        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai != null && ai.Definition != null && ai.Definition.name == "ForestBittenWolf"
                && ai.CurrentState == MonsterAI.State.Idle && ai.GetComponentInParent<MonsterPack>() == null)
            {
                best = ai;
                break;
            }
        }

        if (best == null)
        {
            foreach (MonsterAI ai in MonsterAI.All)
            {
                if (ai != null && ai.CurrentState == MonsterAI.State.Idle && ai.GetComponentInParent<MonsterPack>() == null)
                {
                    best = ai;
                    break;
                }
            }
        }

        if (best == null)
        {
            return "no idle monster outside a pack";
        }

        best.gameObject.name += Tag;
        Player.Current.Health.AddInvulnerability("ProbeSenses");
        return $"picked {best.name} ({best.Definition.DisplayName}) at {best.transform.position}";
    }

    public static string Behind(bool sneak) => Place(-1f, 6f, sneak);

    public static string InFront(bool sneak) => Place(1f, 7f, sneak);

    public static string Away() => Place(-1f, 30f, false);

    /// <summary>A footstep-sized sound right behind the monster (as if the player walked there).</summary>
    public static string NoiseBehind()
    {
        MonsterAI wolf = Wolf();
        Vector3 at = wolf.transform.position - wolf.transform.forward * 4f;
        WorldNoise.Emit(at, PerceptionSettings.Current.FootstepNoise);
        return Report();
    }

    public static string Report()
    {
        MonsterAI wolf = Wolf();
        if (wolf == null)
        {
            return "wolf gone";
        }

        float distance = Vector3.Distance(wolf.transform.position, Player.Current.transform.position);
        Transform indicator = wolf.transform.Find("Awareness Indicator");
        var text = indicator != null ? indicator.GetComponent<TMPro.TextMeshPro>() : null;
        string mark = text != null && text.enabled ? text.text : "-";
        return $"{wolf.Definition.DisplayName}: state={wolf.CurrentState} stage={wolf.Senses.Stage} " +
               $"level={wolf.Senses.Awareness.Level:0.00} target={(wolf.Target != null ? "player" : "none")} " +
               $"mark={mark} distance={distance:0.0} sneaking={Player.Current.Stealth.IsSneaking} " +
               $"visibility={Player.Current.Stealth.Visibility:0.00} searchingLost={wolf.IsSearchingLostTarget}";
    }

    public static string Diagnose()
    {
        MonsterAI wolf = Wolf();
        PerceptionSettings rules = PerceptionSettings.Current;
        Transform target = Player.Current.transform;
        Vector3 eye = wolf.transform.position + Vector3.up * rules.EyeHeight;
        Vector3 point = target.position + Vector3.up * rules.TargetHeight;
        Vector3 flat = point - wolf.transform.position;
        flat.y = 0f;
        float angle = Vector3.Angle(wolf.transform.forward, flat);
        var senses = SenseProfile.Of(wolf.Definition);
        float rate = PerceptionRules.NoticeRate(flat.magnitude, angle, senses, Player.Current.Stealth.Visibility, rules);
        var sb = new System.Text.StringBuilder($"angle={angle:0} rate={rate:0.00}/s mask={rules.SightBlockers.value} enabled={wolf.enabled} hits:");
        foreach (RaycastHit hit in Physics.RaycastAll(eye, (point - eye).normalized, (point - eye).magnitude, rules.SightBlockers, QueryTriggerInteraction.Ignore))
        {
            sb.Append($" [{hit.collider.name} layer={hit.collider.gameObject.layer} d={hit.distance:0.0} player={hit.collider.transform.IsChildOf(target)}]");
        }

        int count = Physics.OverlapSphere(eye, 15f, ~0, QueryTriggerInteraction.Ignore).Length;
        sb.Append($" overlap15m={count} playerCollider={Player.Current.GetComponentInChildren<Collider>()?.GetType().Name} layer={Player.Current.gameObject.layer}");
        return sb.ToString();
    }

    public static string Finish()
    {
        Player.Current.Stealth.SetSneaking(false);
        Player.Current.Health.RemoveInvulnerability("ProbeSenses");
        return Report();
    }

    private static MonsterAI Wolf()
    {
        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai != null && ai.name.EndsWith(Tag))
            {
                return ai;
            }
        }

        return null;
    }

    private static string Place(float side, float distance, bool sneak)
    {
        MonsterAI wolf = Wolf();
        if (wolf == null)
        {
            return "pick a wolf first";
        }

        Vector3 forward = wolf.transform.forward;
        forward.y = 0f;
        Vector3 wanted = wolf.transform.position + forward.normalized * side * distance;
        if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            wanted = hit.position;
        }

        Player player = Player.Current;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = wanted + Vector3.up * 0.1f;
        player.transform.rotation = Quaternion.LookRotation(wolf.transform.position - wanted);
        controller.enabled = true;
        player.Stealth.SetSneaking(sneak);
        return Report();
    }
}

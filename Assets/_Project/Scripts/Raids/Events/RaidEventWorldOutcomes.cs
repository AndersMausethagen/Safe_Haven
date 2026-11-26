using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Takes the player across, to a way out (Docs/RaidEventsPlan_2026-11-26.md §3: the ferryman's raft, the wisp's path): set down a little
/// short of the farthest way out (a shortcut over the river) or the nearest (the wisp's). A companion left behind catches up by itself
/// (Companion warps when far), a carried burden goes along in the hands. Not on horseback: the raft takes no horse.
/// </summary>
[Serializable]
public sealed class CrossOutcome : RaidEventOutcome
{
    public enum Toward
    {
        FarthestExit = 0,
        NearestExit = 1,
    }

    [SerializeField] private Toward toward = Toward.FarthestExit;
    [Tooltip("Set down this far short of the way out, toward where the player was.")]
    [SerializeField, Min(2f)] private float stopShort = 12f;

    public Toward Way => toward;
    public override string DisplayName => "Cross (to a way out)";

    public override void Apply(RaidEventContext context)
    {
        GameObject player = context.Player;
        if (player == null)
        {
            return;
        }

        if (player.GetComponentInParent<PlayerRiding>() is PlayerRiding riding && riding.IsRiding)
        {
            GameMessages.Post("Not with a horse under you: it won't go.", MessageKind.Warning);
            return;
        }

        ExtractionPoint exit = Pick(player.transform.position, toward);
        if (exit == null)
        {
            return;
        }

        Vector3 to = Landing(exit.transform.position, player.transform.position, stopShort);
        Vector3 from = player.transform.position;
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (wasEnabled)
        {
            body.enabled = false;
        }

        Vector3 look = exit.transform.position - to;
        look.y = 0f;
        player.transform.SetPositionAndRotation(to, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : player.transform.rotation);
        if (wasEnabled)
        {
            body.enabled = true;
        }

        // The follow camera would otherwise sweep across the map to catch up (as RaidMap and ArenaFall move the player).
        Vector3 moved = player.transform.position - from;
        foreach (Unity.Cinemachine.CinemachineCamera follow in UnityEngine.Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>())
        {
            if (follow.Follow != null && follow.Follow.IsChildOf(player.transform))
            {
                follow.OnTargetObjectWarped(follow.Follow, moved);
            }
        }

        context.Session?.RecordEvent($"Crossed to {(string.IsNullOrEmpty(exit.DisplayName) ? exit.name : exit.DisplayName)}");
    }

    /// <summary>The way out to go to: the farthest or the nearest from <paramref name="from"/>, open ones first. Null when there is none.</summary>
    public static ExtractionPoint Pick(Vector3 from, Toward toward)
    {
        ExtractionPoint best = null;
        float bestScore = float.MinValue;
        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            if (exit == null)
            {
                continue;
            }

            Vector3 offset = exit.transform.position - from;
            offset.y = 0f;
            float distance = offset.magnitude;
            // An open one beats any closed one; among them, the farthest or the nearest.
            float score = (exit.IsOpen ? 100000f : 0f) + (toward == Toward.FarthestExit ? distance : -distance);
            if (score > bestScore)
            {
                best = exit;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// A walkable, dry spot about <paramref name="stopShort"/> metres short of <paramref name="exit"/>, on the side toward
    /// <paramref name="from"/>: nearer in if that is water or off the NavMesh (the NavMesh runs under the river), the exit's own ground at worst.
    /// </summary>
    public static Vector3 Landing(Vector3 exit, Vector3 from, float stopShort)
    {
        Vector3 back = from - exit;
        back.y = 0f;
        back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
        foreach (float share in new[] { 1f, 0.6f, 0.3f })
        {
            Vector3 wanted = exit + back * stopShort * share;
            if (NavMesh.SamplePosition(RaidSpots.Grounded(wanted), out NavMeshHit hit, 4f, NavMesh.AllAreas) && RaidSpots.IsDryEnough(hit.position))
            {
                return hit.position;
            }
        }

        return NavMesh.SamplePosition(exit, out NavMeshHit at, 6f, NavMesh.AllAreas) ? at.position : exit + back * stopShort;
    }
}

/// <summary>
/// The site changes with the choice (the cedar felled, the fire put out): shows the site's child of this name and, if asked, hides its
/// Model and its solid box. Author the changed look as an inactive child of the site prefab ("After").
/// </summary>
[Serializable]
public sealed class ShowSitePartOutcome : RaidEventOutcome
{
    [Tooltip("The site prefab's child to show, by name (inactive in the prefab).")]
    [SerializeField] private string part = "After";
    [SerializeField] private bool hideModel = true;

    public override string DisplayName => "Show a Part of the Site";

    public override void Apply(RaidEventContext context)
    {
        if (context.Site == null)
        {
            return;
        }

        Transform shown = Find(context.Site.transform, part);
        if (shown != null)
        {
            shown.gameObject.SetActive(true);
        }

        Transform model = hideModel ? context.Site.transform.Find("Model") : null;
        if (model == null)
        {
            return;
        }

        model.gameObject.SetActive(false);
        foreach (Collider solid in context.Site.GetComponents<Collider>())
        {
            if (!solid.isTrigger)
            {
                solid.enabled = false;
            }
        }
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && child.name == name)
            {
                return child;
            }
        }

        return null;
    }
}

/// <summary>Eases the blight round home (SettlementState.Blight) by this much: the rot drawn out of a tree far off weakens it at home too.</summary>
[Serializable]
public sealed class EaseBlightOutcome : RaidEventOutcome
{
    [SerializeField, Range(0f, 1f)] private float amount = 0.1f;

    public float Amount => amount;
    public override string DisplayName => "Ease the Blight at Home";

    public override void Apply(RaidEventContext context)
    {
        // Only one's own Haven: a guest's raid eases no host's blight here.
        if (GameSession.Home == null || HavenVisit.IsVisiting)
        {
            return;
        }

        GameSession.Home.ChangeBlight(-amount);
        GameMessages.Post("Far off, round home, the blight gives a little.", MessageKind.Gain);
    }
}

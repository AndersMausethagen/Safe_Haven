using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Hold the event's site for a while as waves come (Docs/RaidEventsPlan_2026-11-26.md E3): a DefendTask on the site, its ring lit while
/// it runs. Each Wave's outcomes (creatures, mostly) come at the start and every Wave Every seconds held; On Held's when the count is full.
/// </summary>
[Serializable]
public sealed class DefendOutcome : RaidEventOutcome
{
    [Tooltip("The HUD's name for it: \"Hold the bell\".")]
    [SerializeField] private string label = "Hold your ground";
    [SerializeField, Min(2f)] private float radius = 5f;
    [SerializeField, Min(5f)] private float seconds = 40f;
    [SerializeField, Min(5f)] private float waveEvery = 15f;
    [SerializeReference] private List<RaidEventOutcome> eachWave = new List<RaidEventOutcome>();
    [SerializeReference] private List<RaidEventOutcome> onHeld = new List<RaidEventOutcome>();

    public override string DisplayName => "Defend (hold the site)";

    public override void Apply(RaidEventContext context)
    {
        GameObject at = context.Site != null ? context.Site.gameObject : new GameObject(label);
        if (context.Site == null)
        {
            at.transform.position = context.Position;
        }

        DefendTask task = at.AddComponent<DefendTask>();
        task.OwnObject = context.Site == null;
        task.Begin(context, label, radius, seconds, waveEvery, eachWave, onHeld);
        GameMessages.Post($"{label}: stay in the circle for {Mathf.RoundToInt(seconds)} seconds.", MessageKind.Warning);
    }
}

/// <summary>
/// The save remembers a choice (Docs/RaidEventsPlan_2026-11-26.md E4): a mark in the journal that a follow-up event needs (Needs Mark) or
/// that keeps one away (Blocked By Mark). "event:pedlar:freed", "event:stranger:owed".
/// </summary>
[Serializable]
public sealed class SetMarkOutcome : RaidEventOutcome
{
    [SerializeField] private string mark = "";

    public SetMarkOutcome() { }

    public SetMarkOutcome(string mark) => this.mark = mark;

    public string Mark => mark;
    public override string DisplayName => "Set Mark (remembered)";

    public override void Apply(RaidEventContext context)
    {
        if (!string.IsNullOrEmpty(mark))
        {
            GameSession.Progress.Journal.NoteNoticed(mark);
        }
    }
}

/// <summary>The forest calms by this much at once (an offering at its warning, Docs/RaidEventsPlan_2026-11-26.md E5).</summary>
[Serializable]
public sealed class CalmForestOutcome : RaidEventOutcome
{
    [SerializeField, Min(0f)] private float amount = 40f;

    public override string DisplayName => "Calm the Forest";

    public override void Apply(RaidEventContext context)
    {
        ForestAnnoyanceDriver forest = Object.FindAnyObjectByType<ForestAnnoyanceDriver>();
        if (forest != null && forest.Annoyance != null)
        {
            forest.Annoyance.Calm(amount);
        }
    }
}

/// <summary>
/// Follow a trail to a hidden place (Docs/RaidEventsPlan_2026-11-26.md E3 Track): signs one by one, maybe an animal walking on ahead,
/// and On Found's outcomes at the end. If no place for it is found, the trail goes cold and On Found comes at the site.
/// </summary>
[Serializable]
public sealed class TrackOutcome : RaidEventOutcome
{
    [Tooltip("The HUD's name for it: \"Follow the hart\".")]
    [SerializeField] private string label = "Follow the trail";
    [SerializeField] private Vector2 distance = new Vector2(60f, 120f);
    [SerializeField, Min(4f)] private float signEvery = 12f;
    [Tooltip("What each sign along the way is (a snare, a broken branch). Empty: no signs, only the leader.")]
    [SerializeField] private GameObject sign;
    [Tooltip("An animal walking on ahead (TrackLeader). Empty: signs only.")]
    [SerializeField] private GameObject leader;
    [Tooltip("What stands at the end once found (a cache, a den). Optional.")]
    [SerializeField] private GameObject endPrefab;
    [SerializeReference] private List<RaidEventOutcome> onFound = new List<RaidEventOutcome>();

    public override string DisplayName => "Track (follow a trail)";

    public override void Apply(RaidEventContext context)
    {
        var holder = new GameObject(label);
        holder.transform.position = context.Site != null ? context.Site.transform.position : context.Position;
        TrackTask task = holder.AddComponent<TrackTask>();
        if (task.Begin(context, label, distance, signEvery, sign, leader, endPrefab, onFound))
        {
            GameMessages.Post($"{label}: the trail leads off. Keep to it.", MessageKind.Info);
            return;
        }

        // Nowhere for it to lead (a small map, deep water all round): what it hid is here after all.
        Object.Destroy(holder);
        foreach (RaidEventOutcome outcome in onFound)
        {
            outcome?.Apply(context);
        }
    }
}

/// <summary>
/// Someone to bring to a way out (Docs/RaidEventsPlan_2026-11-26.md E3 Escort): the follower's prefab (EscortFollower) comes out at the site
/// and follows the player; On Delivered when it stands in an open way out's circle, On Lost if it falls.
/// </summary>
[Serializable]
public sealed class EscortOutcome : RaidEventOutcome
{
    [Tooltip("The HUD's name for it: \"Bring Kari out\".")]
    [SerializeField] private string label = "Bring them out";
    [Tooltip("A prefab with EscortFollower, a NavMeshAgent, a Damageable and a Combatant.")]
    [SerializeField] private EscortFollower follower;
    [SerializeReference] private List<RaidEventOutcome> onDelivered = new List<RaidEventOutcome>();
    [SerializeReference] private List<RaidEventOutcome> onLost = new List<RaidEventOutcome>();

    public override string DisplayName => "Escort (bring someone out)";

    public override void Apply(RaidEventContext context)
    {
        if (follower == null)
        {
            return;
        }

        Vector3 at = (context.Site != null ? context.Site.transform.position : context.Position) + Vector3.right * 1.5f;
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
        {
            at = hit.position;
        }

        EscortFollower made = Object.Instantiate(follower, at, Quaternion.identity);
        made.name = follower.name;
        made.Begin(context, label, onDelivered, onLost);
        GameMessages.Post($"{label}: lead them to a way out that's open.", MessageKind.Info);
    }
}

/// <summary>
/// Something heavy to carry to a way out (Docs/RaidEventsPlan_2026-11-26.md E3): the burden's prefab (with CarriedBurden) stands at the
/// site and goes straight into the player's hands; On Delivered's outcomes come when it is carried into an open exit's circle.
/// </summary>
[Serializable]
public sealed class CarryOutcome : RaidEventOutcome
{
    [Tooltip("A prefab with CarriedBurden, an Interactable and a collider.")]
    [SerializeField] private GameObject burden;
    [Tooltip("The site's own Model is the thing carried (a stone head on its ledge): it is hidden as the burden is lifted.")]
    [SerializeField] private bool takenFromSite = true;
    [SerializeReference] private List<RaidEventOutcome> onDelivered = new List<RaidEventOutcome>();

    public override string DisplayName => "Carry (to a way out)";

    public override void Apply(RaidEventContext context)
    {
        if (burden == null)
        {
            return;
        }

        Vector3 at = context.Site != null ? context.Site.transform.position : context.Position;
        Transform onSite = takenFromSite && context.Site != null ? context.Site.transform.Find("Model") : null;
        if (onSite != null)
        {
            onSite.gameObject.SetActive(false);
            // And the site's own solid box, or an unseen block would stay where the thing lay.
            foreach (Collider solid in context.Site.GetComponents<Collider>())
            {
                if (!solid.isTrigger)
                {
                    solid.enabled = false;
                }
            }
        }

        GameObject made = Object.Instantiate(burden, RaidSpots.Grounded(at + Vector3.up), Quaternion.identity);
        made.name = burden.name;
        if (made.TryGetComponent(out CarriedBurden carried))
        {
            carried.Begin(context, onDelivered);
            GameMessages.Post($"You heave {carried.Called} up. Carry it to a way out; strike or dodge and you set it down.", MessageKind.Info);
        }
    }
}

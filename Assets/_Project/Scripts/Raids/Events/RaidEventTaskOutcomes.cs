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

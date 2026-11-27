using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A heavy thing a raid event gives the player to carry out (Docs/RaidEventsPlan_2026-11-26.md E3, the user's answer: it takes the hands,
/// like a cart): held before the player, it slows them and is heard; attacking, dodging, guarding, a skill or swimming sets it down, and
/// it is picked up again with Use. Carried into an open way out's circle it pays (the event's outcomes) and is gone. Lost with the raid
/// like anything else. On the root of the burden's prefab, beside an Interactable (Use, not deactivated on completion) and a collider.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[OnAFriendsCopy(CopyRule.Same, "each player carries their own game's burden")]
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid event's burden")]
public sealed class CarriedBurden : RaidEventTask, IInteractionName, IInteractionVerb
{
    [Tooltip("What it is called: \"the stone head\".")]
    [SerializeField] private string called = "the burden";
    [Tooltip("The share of normal speed while it is carried.")]
    [SerializeField, Range(0.3f, 1f)] private float speedScale = 0.6f;
    [Tooltip("How far it is heard while carried, in metres, every few seconds.")]
    [SerializeField, Min(0f)] private float noiseRadius = 18f;
    [SerializeField, Min(0.5f)] private float noiseEvery = 3f;
    [Tooltip("Where it is held, from the player's feet: up and ahead.")]
    [SerializeField] private Vector3 heldAt = new Vector3(0f, 1.05f, 0.55f);

    private Interactable interactable;
    private Collider[] colliders;
    private RaidEventContext context;
    private List<RaidEventOutcome> onDelivered = new List<RaidEventOutcome>();
    private PlayerStateMachine holder;
    private PlayerMovement movement;
    private float nextNoise;
    private Vector3 lastFooting;

    public bool IsHeld => holder != null;
    public string Called => called;
    public float SpeedScale => speedScale;
    public string InteractionName => string.IsNullOrEmpty(called) ? "Burden" : char.ToUpperInvariant(called[0]) + called.Substring(1);
    public string Verb => "Pick it up";

    public override string HudLine
    {
        get
        {
            Vector3 from = context?.Player != null ? context.Player.transform.position : transform.position;
            if (!IsHeld)
            {
                return $"<color={UIStyle.Current.WarningHex}>{InteractionName} lies {Mathf.RoundToInt(FlatDistance(transform.position, from))} m back</color>";
            }

            ExtractionPoint exit = NearestOpenExit(from);
            string exitName = exit != null && !string.IsNullOrEmpty(exit.DisplayName) ? exit.DisplayName : exit != null ? exit.name : "";
            string way = exit != null ? $"{Mathf.RoundToInt(FlatDistance(exit.transform.position, from))} m to {exitName}" : "no way out open";
            return $"<color={UIStyle.Current.AccentHex}>Carry {called} out</color>: {way}";
        }
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        colliders = GetComponentsInChildren<Collider>(true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        interactable.Completed.AddListener(OnPickedUp);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        interactable.Completed.RemoveListener(OnPickedUp);
        // Switched off with the scene (a raid ending in the hands): let go of the player only; moving it now is refused mid-deactivation.
        Release();
    }

    /// <summary>The event's outcome hands it over: what it pays when carried out, and the player takes it at once.</summary>
    public void Begin(RaidEventContext eventContext, List<RaidEventOutcome> reward)
    {
        context = eventContext;
        onDelivered = reward ?? new List<RaidEventOutcome>();
        if (context?.Player != null)
        {
            Take(context.Player);
        }
    }

    private void OnPickedUp()
    {
        if (!IsHeld && interactable.Interactor != null)
        {
            Take(interactable.Interactor);
        }
    }

    /// <summary>The player takes it in both hands. Public for tests.</summary>
    public void Take(GameObject player)
    {
        if (IsHeld || player == null || !player.TryGetComponent(out PlayerStateMachine machine))
        {
            return;
        }

        holder = machine;
        movement = player.GetComponent<PlayerMovement>();
        holder.AttackRequested += SetDown;
        holder.DodgeRequested += SetDown;
        holder.ShieldBashRequested += SetDown;
        holder.SkillRequested += SetDownForSkill;
        holder.GuardRequested += SetDownToGuard;
        foreach (Collider part in colliders)
        {
            part.enabled = false;
        }

        interactable.enabled = false;
        transform.SetParent(player.transform, false);
        transform.localPosition = heldAt;
        transform.localRotation = Quaternion.identity;
        if (movement != null) movement.SetSpeedScale(this, speedScale);
        nextNoise = Time.time;
        lastFooting = player.transform.position;
    }

    private void SetDownForSkill(int slot) => SetDown();

    private void SetDownToGuard(bool raised)
    {
        if (raised)
        {
            SetDown();
        }
    }

    // The player's own: the hold, the slowing and the hands. Safe to call while the scene switches off.
    private void Release()
    {
        if (!IsHeld)
        {
            return;
        }

        holder.AttackRequested -= SetDown;
        holder.DodgeRequested -= SetDown;
        holder.ShieldBashRequested -= SetDown;
        holder.SkillRequested -= SetDownForSkill;
        holder.GuardRequested -= SetDownToGuard;
        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
        }

        holder = null;
        movement = null;
    }

    // How far ahead of the player's middle it is set down: clear of their body, and how high above a spot the ground is looked for
    // (under a canopy or a roof the first thing straight down from high up is not the ground).
    private const float SetDownAhead = 1.1f;
    private const float GroundLook = 2f;

    /// <summary>Puts it down on the ground before the player: hands free to fight. Public for tests.</summary>
    public void SetDown()
    {
        if (!IsHeld)
        {
            return;
        }

        Transform player = holder.transform;
        bool swimming = holder.IsSwimming;
        Release();
        transform.SetParent(null, true);
        // In the water it goes back where the player last stood, not to the bottom of the lake.
        Vector3 ahead = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        Vector3 at = swimming ? lastFooting : player.position + ahead * SetDownAhead;
        transform.position = Physics.Raycast(at + Vector3.up * GroundLook, Vector3.down, out RaycastHit ground, GroundLook * 3f, ~0, QueryTriggerInteraction.Ignore)
            ? ground.point
            : at;
        transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
        foreach (Collider part in colliders)
        {
            if (part != null)
            {
                part.enabled = true;
            }
        }

        if (interactable != null)
        {
            interactable.enabled = true;
        }
    }

    private void Update()
    {
        if (!IsHeld)
        {
            return;
        }

        // Over with the raid: it pays nothing now, and stays where it is.
        if (context?.Session != null && context.Session.IsOver)
        {
            return;
        }

        // A horse or a cart takes the hands too (and would carry it past its slowing).
        bool riding = holder.TryGetComponent(out PlayerRiding rider) && rider.IsRiding;
        bool hauling = holder.TryGetComponent(out PlayerTransport transport) && transport.Using != null;
        if (holder.IsSwimming || riding || hauling || (holder.TryGetComponent(out Damageable health) && !health.IsAlive))
        {
            SetDown();
            return;
        }

        if (movement != null && movement.IsGrounded)
        {
            lastFooting = holder.transform.position;
        }

        if (noiseRadius > 0f && Time.time >= nextNoise && Player.Current != null && Player.Current.Noise != null)
        {
            nextNoise = Time.time + noiseEvery;
            Player.Current.Noise.Make(transform.position, noiseRadius);
        }

        // Inside any open way out, by its own rule (ExtractionPoint.Inside): measured to the nearest centre alone, standing in a bigger
        // circle whose centre was farther delivered nothing (review 2026-11-27).
        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            if (exit != null && exit.IsOpen && exit.Inside(holder.transform.position))
            {
                Deliver();
                break;
            }
        }
    }

    private void Deliver()
    {
        SetDown();
        GameMessages.Post($"You set {called} down at the way out. It's yours now, if you get home.", MessageKind.Gain);
        Apply(onDelivered, context, transform.position);
        if (context?.Session != null)
        {
            context.Session.RecordEvent($"Carried {called} out");
        }

        Destroy(gameObject);
    }

    private static ExtractionPoint NearestOpenExit(Vector3 from)
    {
        ExtractionPoint best = null;
        float bestDistance = float.MaxValue;
        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            if (exit == null || !exit.IsOpen)
            {
                continue;
            }

            float distance = FlatDistance(exit.transform.position, from);
            if (distance < bestDistance)
            {
                best = exit;
                bestDistance = distance;
            }
        }

        return best;
    }
}

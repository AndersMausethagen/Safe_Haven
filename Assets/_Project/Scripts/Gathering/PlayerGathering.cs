using System;
using UnityEngine;

/// <summary>One resolved gathering swing, for noise, annoyance and presentation.</summary>
public readonly struct GatheringSwing
{
    public readonly SwingResult Result;
    public readonly Vector3 Position;
    public readonly float NoiseRadius;
    public readonly ToolType Tool;
    /// <summary>The tool's loudness (GatheringToolProfile.Loudness), already applied to the noise radius; scales annoyance.</summary>
    public readonly float Loudness;

    public GatheringSwing(SwingResult result, Vector3 position, float noiseRadius, ToolType tool, float loudness = 1f)
    {
        Result = result;
        Position = position;
        NoiseRadius = noiseRadius;
        Tool = tool;
        Loudness = loudness;
    }
}

/// <summary>
/// Runs the gathering timing minigame on the player. When an interaction starts on a ResourceNode whose tool uses the
/// minigame (GatheringMinigameSettings), it holds the interaction open and runs a GatheringSession: each Interact or
/// Attack press is a swing; a whole bar cycle without one is a glancing blow. Glancing blows wear the tool. When the
/// last swing lands the node completes as usual and every critical adds a bonus of its main yield. Taking damage can
/// interrupt the gather. Each swing is announced through <see cref="Swung"/> (raid noise and forest annoyance listen).
/// Other nodes keep their timed gather. Lives on the PlayerArmature prefab, next to PlayerInteractor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor), typeof(PlayerStateMachine))]
public sealed class PlayerGathering : MonoBehaviour
{
    private PlayerInteractor interactor;
    private PlayerStateMachine stateMachine;
    private Damageable health;
    private PlayerEquipment equipment;
    private Interactable target;
    private ResourceNode node;
    private GatheringToolProfile profile;
    private float autoTimer;
    private GatheringBarView barView;
    private readonly System.Random random = new System.Random();

    /// <summary>Raised after every swing by any player.</summary>
    public static event Action<GatheringSwing> Swung;

    /// <summary>The minigame in progress, or null.</summary>
    public GatheringSession Session { get; private set; }

    /// <summary>The tool of the gather in progress.</summary>
    public ToolType Tool => node != null && node.Definition != null ? node.Definition.GatherTool : ToolType.None;

    /// <summary>Raised with the last swing's result, for this player's presentation (the swing bar).</summary>
    public event Action<SwingResult> SwingResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Swung = null;
    }

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        equipment = GetComponent<PlayerEquipment>();
    }

    private void Start()
    {
        barView = GatheringBarView.Attach(this);
    }

    private void OnDestroy()
    {
        if (barView != null)
        {
            Destroy(barView.gameObject);
        }
    }

    private void OnEnable()
    {
        interactor.InteractionStarted += OnInteractionStarted;
        interactor.InteractionEnded += OnInteractionEnded;
        stateMachine.WorkPressed += OnWorkPressed;
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDisable()
    {
        interactor.InteractionStarted -= OnInteractionStarted;
        interactor.InteractionEnded -= OnInteractionEnded;
        stateMachine.WorkPressed -= OnWorkPressed;
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }

        Session = null;
    }

    private void Update()
    {
        if (Session == null || Session.IsComplete)
        {
            return;
        }

        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        if (settings.AutoGather)
        {
            autoTimer += Time.deltaTime;
            if (autoTimer >= settings.AutoSwingSeconds)
            {
                autoTimer = 0f;
                Session.Resolve(SwingResult.Normal);
                AfterSwing(SwingResult.Normal);
            }

            return;
        }

        if (Session.Tick(Time.deltaTime))
        {
            AfterSwing(SwingResult.Glancing);
        }
    }

    private void OnInteractionStarted(Interactable started)
    {
        Session = null;
        if (started == null || !started.TryGetComponent(out ResourceNode startedNode) || startedNode.Definition == null)
        {
            return;
        }

        GatheringNodeDefinition definition = startedNode.Definition;
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        GatheringToolProfile profile = settings.Enabled ? settings.Profile(definition.GatherTool) : null;
        if (profile == null)
        {
            return;
        }

        target = started;
        node = startedNode;
        this.profile = profile;
        target.HoldOpen = true;
        autoTimer = 0f;

        int toolTier = ItemInventory.GatheringTier(gameObject, definition.GatherTool);
        Session = new GatheringSession(
            definition.Swings,
            profile.CycleSeconds / Mathf.Max(0.1f, definition.SwingSpeed),
            profile.NormalZoneWidth,
            settings.CriticalWidthFor(profile, toolTier, definition.RequiredToolTier),
            settings.CriticalWidthPerCombo,
            settings.MaxCombo,
            settings.ZoneJitter,
            random);
    }

    private void OnInteractionEnded(Interactable ended, InteractionResult result)
    {
        if (Session == null || ended != target)
        {
            return;
        }

        if (result == InteractionResult.Completed && node != null && Session.Criticals > 0)
        {
            int bonus = Session.Criticals * GatheringMinigameSettings.Current.BonusPerCritical;
            if (bonus > 0 && node.TryGetComponent(out ResourceSource source))
            {
                source.GrantBonus(gameObject, bonus);
            }
        }

        Session = null;
        target = null;
        node = null;
        profile = null;
    }

    private void OnWorkPressed()
    {
        if (Session == null || Session.IsComplete || GatheringMinigameSettings.Current.AutoGather)
        {
            return;
        }

        AfterSwing(Session.Press());
    }

    private void OnDamaged(DamageInfo info, DamageResult result)
    {
        if (Session != null && GatheringMinigameSettings.Current.InterruptOnHit)
        {
            GameMessages.Post("Gathering interrupted.", MessageKind.Warning);
            stateMachine.EndInteraction();
        }
    }

    private void AfterSwing(SwingResult result)
    {
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        ToolType tool = Tool;
        Vector3 position = target != null ? target.transform.position : transform.position;

        if (result == SwingResult.Glancing)
        {
            WearTool(tool);
        }
        else if (result == SwingResult.Critical && CombatFeedback.Instance != null)
        {
            CombatFeedback.Instance.HitStop(settings.CriticalHitStop);
        }

        float loudness = profile != null ? profile.Loudness : 1f;
        if (node != null)
        {
            node.NotifySwing(result, transform.position);
        }

        SwingResolved?.Invoke(result);
        Swung?.Invoke(new GatheringSwing(result, position, settings.NoiseRadius(result) * loudness, tool, loudness));

        if (Session != null && Session.IsComplete && target != null)
        {
            target.Complete();
        }
    }

    private void WearTool(ToolType tool)
    {
        float care = TryGetComponent(out Combatant self) ? Mathf.Clamp01(self.Stats.Get(StatType.ToolCare)) : 0f;
        float wear = DurabilitySettings.Current.WearPerGlancingBlow * (1f - care);
        if (equipment == null || wear <= 0f)
        {
            return;
        }

        ItemContainer slots = equipment.Gear.Slots;
        int index = slots.BestToolIndex(tool);
        if (index < 0)
        {
            return;
        }

        ItemStack stack = slots.GetSlot(index);
        if (!stack.Item.HasDurability)
        {
            return;
        }

        slots.SetDurability(index, Mathf.Max(0f, stack.Durability - wear));
        if (slots.GetSlot(index).IsBroken)
        {
            GameMessages.Post($"Your {stack.Item.DisplayName} broke.", MessageKind.Warning);
        }
    }
}

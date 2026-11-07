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
/// minigame (GatheringMinigameSettings), it holds the interaction open and runs a GatheringSession. With hold to raise (the
/// default) holding Attack or Interact raises the tool, a glint and a ping mark the critical moment, and letting go swings;
/// otherwise each press is a swing and with no press the tool comes down by itself. How good a swing was is settled by the press,
/// so timing stays fair, but the tool then has to come down: the blow itself, with its sound, chips and everything that
/// follows, happens when it lands. Glancing blows wear the tool. When the last swing lands the node completes as usual and
/// every critical adds a bonus of its main yield. Taking damage can
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
    private PlayerAnimationController animations;
    private bool blowComing;
    private SwingResult blowResult;
    private float blowLandsAt;
    private Interactable target;
    private ResourceNode node;
    private GatheringToolProfile profile;
    private float autoTimer;
    private TimingBarView barView;
    private HeldItemView held;
    private float raiseSpeed;
    private bool glinted;
    private readonly System.Random random = new System.Random();

    /// <summary>Raised after every swing by any player.</summary>
    public static event Action<GatheringSwing> Swung;

    /// <summary>The minigame in progress, or null.</summary>
    public GatheringSession Session { get; private set; }

    /// <summary>The tool of the gather in progress.</summary>
    public ToolType Tool => node != null && node.Definition != null ? node.Definition.GatherTool : ToolType.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Swung = null;
    }

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        equipment = GetComponent<PlayerEquipment>();
        animations = GetComponent<PlayerAnimationController>();
        held = GetComponent<HeldItemView>();
    }

    private void Start()
    {
        // With hold to raise the cue is the glint on the tool; the bar shows only if the settings ask for it.
        barView = TimingBarView.Attach(() => GatheringMinigameSettings.Current.ShowTimingBar ? Session : null);
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
        blowComing = false;
    }

    private void Update()
    {
        if (blowComing && Time.time >= blowLandsAt)
        {
            LandBlow();
        }

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
                Swing(SwingResult.Normal, pressed: false);
            }

            return;
        }

        if (settings.HoldToRaise)
        {
            UpdateHold();
            return;
        }

        if (Session.Tick(Time.deltaTime))
        {
            Swing(SwingResult.Normal, pressed: false);
        }
    }

    // Hold the attack or interact button to raise the tool; let go to swing. The tool rises with the hold, and glints once
    // as it passes the critical moment.
    private void UpdateHold()
    {
        bool down = IsHeld(GameInput.Attack) || IsHeld(GameInput.Interact);
        SwingResult? swung = Session.Hold(Time.deltaTime, down, out bool late);
        if (animations != null)
        {
            animations.SetWorkRaise(Session.IsRaising ? raiseSpeed : 0f);
        }

        if (Session.InCriticalZone && !glinted)
        {
            glinted = true;
            Glint();
        }
        else if (!Session.IsRaising)
        {
            glinted = false;
        }

        if (swung.HasValue)
        {
            glinted = false;
            Swing(swung.Value, pressed: true, late: late);
        }
    }

    private static bool IsHeld(UnityEngine.InputSystem.InputAction action) => action != null && action.enabled && action.IsPressed();

    // A flare of white light at the tool's head, and a small ping: the moment to let go.
    private void Glint()
    {
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        Vector3 at = ToolHead();
        AudioManager.Play(settings.GlintSound, at);
        if (settings.GlintIntensity <= 0f)
        {
            return;
        }

        var flare = new GameObject("Tool Glint");
        flare.transform.position = at;
        if (held != null && held.ShownModel != null)
        {
            flare.transform.SetParent(held.ShownModel.transform, true);
        }

        var light = flare.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = settings.GlintColour;
        light.range = settings.GlintRange;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        StartCoroutine(Flare(light, settings.GlintIntensity, settings.GlintSeconds));
    }

    private static System.Collections.IEnumerator Flare(Light light, float peak, float seconds)
    {
        float t = 0f;
        while (t < seconds && light != null)
        {
            t += Time.deltaTime;
            float k = t / seconds;
            // Up fast, down slowly: a glint, not a pulse.
            light.intensity = peak * (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
            yield return null;
        }

        if (light != null)
        {
            Destroy(light.gameObject);
        }
    }

    // The end of the tool farthest from the hand: the axe head or pick point.
    private Vector3 ToolHead()
    {
        if (held == null || held.ShownModel == null)
        {
            return transform.position + Vector3.up * 1.8f;
        }

        Renderer[] renderers = held.ShownModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return held.ShownModel.transform.position;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 hand = held.Grip != null ? held.Grip.position : held.ShownModel.transform.position;
        Vector3 outward = bounds.center - hand;
        return outward.sqrMagnitude > 1e-4f ? bounds.center + outward.normalized * bounds.extents.magnitude * 0.6f : bounds.center;
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
            // Picked by hand: the Foraging skill sets how long it takes.
            if (definition.GatherTool == ToolType.None)
            {
                SkillHand(ToolType.None, out float pick, out _);
                started.Duration = definition.GatherSeconds / pick;
            }

            return;
        }

        target = started;
        node = startedNode;
        this.profile = profile;
        target.HoldOpen = true;
        autoTimer = 0f;
        glinted = false;
        if (animations != null)
        {
            animations.HoldWorkSwings(started.AnimationType);
        }

        int toolTier = ItemInventory.GatheringTier(gameObject, definition.GatherTool);
        SkillHand(definition.GatherTool, out float speed, out float sweetSpot);
        float rise = settings.RiseSecondsFor(profile, toolTier, definition.RequiredToolTier) / Mathf.Max(0.1f, definition.SwingSpeed * speed);
        Session = new GatheringSession(
            definition.Swings,
            rise,
            settings.SweetSpot,
            profile.NormalZoneWidth,
            Mathf.Min(settings.CriticalWidthFor(profile, toolTier, definition.RequiredToolTier) * sweetSpot, profile.NormalZoneWidth),
            settings.CriticalWidthPerCombo,
            settings.MaxCombo,
            settings.ZoneJitter,
            random,
            ProgressFor(definition.GatherTool, settings));
        Session.KeepsComboOnNormal = definition.GatherTool == ToolType.Axe && Techniques.Has(PerkTechnique.SteadyRhythm);

        if (settings.HoldToRaise && !settings.AutoGather)
        {
            // The tool waits low until the button is held, and rises over exactly the swing's timing window.
            Session.WaitForRelease();
            raiseSpeed = animations != null && animations.WorkRaiseSeconds > 0f ? animations.WorkRaiseSeconds / rise : 1f;
            if (animations != null)
            {
                animations.SetWorkRaise(0f);
            }
        }
    }

    private void OnInteractionEnded(Interactable ended, InteractionResult result)
    {
        if (Session == null || ended != target)
        {
            return;
        }

        if (result == InteractionResult.Completed && node != null && Session.Criticals > 0)
        {
            // Rich Seam: every critical blow on rock brings one more.
            bool richSeam = node.Definition != null && node.Definition.GatherTool == ToolType.Pickaxe && Techniques.Has(PerkTechnique.RichSeam);
            int bonus = Session.Criticals * (GatheringMinigameSettings.Current.BonusPerCritical + (richSeam ? TechniqueSettings.Current.RichSeamOre : 0));
            if (richSeam)
            {
                Techniques.Used(PerkTechnique.RichSeam);
            }
            if (bonus > 0 && node.TryGetComponent(out ResourceSource source))
            {
                source.GrantBonus(gameObject, bonus);
            }
        }

        blowComing = false;
        Session = null;
        target = null;
        node = null;
        profile = null;
    }

    private void OnWorkPressed()
    {
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        // Hold to raise reads the held button every frame instead (UpdateHold).
        if (Session == null || Session.IsComplete || settings.AutoGather || settings.HoldToRaise)
        {
            return;
        }

        Swing(Session.Press());
    }

    private void OnDamaged(DamageInfo info, DamageResult result)
    {
        if (Session != null && GatheringMinigameSettings.Current.InterruptOnHit && !info.IsOverTime)
        {
            GameMessages.Post("Gathering interrupted.", MessageKind.Warning);
            stateMachine.EndInteraction();
        }
    }

    // The player sees at once how the swing went; the blow itself waits for the tool to land.
    private void Swing(SwingResult result, bool pressed = true, bool late = false)
    {
        // A swing left to itself is the steady work: only what the player did is worth a word.
        if (barView != null && pressed)
        {
            barView.Flash(result == SwingResult.Critical ? Session.Combo > 1 ? $"Clean cut! x{Session.Combo}" : "Clean cut!" : result == SwingResult.Normal ? "Good" : late ? "Too late" : "Too early", result);
        }

        if (blowComing)
        {
            LandBlow();
        }

        blowResult = result;
        float wait = animations != null ? animations.StrikeWork() : 0f;
        blowComing = true;
        blowLandsAt = Time.time + wait;
        if (wait <= 0f)
        {
            LandBlow();
        }
    }

    private void LandBlow()
    {
        blowComing = false;
        SwingResult result = blowResult;
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        ToolType tool = Tool;
        Vector3 position = target != null ? target.transform.position : transform.position;

        if (result == SwingResult.Glancing)
        {
            WearTool(tool);
        }
        else if (result == SwingResult.Critical && CombatFeedback.Current != null)
        {
            CombatFeedback.Current.HitStop(settings.CriticalHitStop);
        }

        if (result == SwingResult.Normal && Session != null && Session.KeepsComboOnNormal && Session.Combo > 0)
        {
            Techniques.Used(PerkTechnique.SteadyRhythm);
        }

        if (result == SwingResult.Critical && Session != null)
        {
            // A clean cut answers on the node: what it brings, its run, and a small kick of the camera.
            bool richSeam = tool == ToolType.Pickaxe && Techniques.Has(PerkTechnique.RichSeam);
            int bonus = settings.BonusPerCritical + (richSeam ? TechniqueSettings.Current.RichSeamOre : 0);
            string run = Session.Combo > 1 ? $" x{Session.Combo}" : "";
            DamageNumberSpawner.ShowAt(position + Vector3.up * 1.6f, bonus > 0 ? $"+{bonus}{run}" : $"Clean{run}",
                Session.Combo > 2 ? Color.white : UIStyle.Current.Accent);
            CameraShake.Add(0.06f + 0.02f * Mathf.Min(Session.Combo, 4));
        }

        float loudness = profile != null ? profile.Loudness : 1f;
        if (node != null)
        {
            node.NotifySwing(result, transform.position);
        }

        Swung?.Invoke(new GatheringSwing(result, position, settings.NoiseRadius(result) * loudness, tool, loudness));

        if (Session != null && Session.IsComplete && target != null)
        {
            target.Complete();
        }
    }

    // How much each swing moves the work on: Heartwood Strike (axe) and Shatter (pickaxe) make a critical one count more.
    private static System.Func<SwingResult, float> ProgressFor(ToolType tool, GatheringMinigameSettings settings)
    {
        bool doubled = (tool == ToolType.Axe && Techniques.Has(PerkTechnique.HeartwoodStrike))
                       || (tool == ToolType.Pickaxe && Techniques.Has(PerkTechnique.Shatter));
        if (!doubled)
        {
            return settings.ProgressFor;
        }

        float counts = TechniqueSettings.Current.CriticalCountsAs;
        PerkTechnique technique = tool == ToolType.Axe ? PerkTechnique.HeartwoodStrike : PerkTechnique.Shatter;
        return result =>
        {
            if (result == SwingResult.Critical)
            {
                Techniques.Used(technique);
            }

            return settings.ProgressFor(result) * (result == SwingResult.Critical ? counts : 1f);
        };
    }

    // How the woodcutting, mining or foraging skill changes the work: how fast the tool rises (or the hand picks) and how
    // wide its sweet spot is, as multipliers (a beginner's below 1).
    private void SkillHand(ToolType tool, out float speed, out float sweetSpot)
    {
        speed = 1f;
        sweetSpot = 1f;
        if (!TryGetComponent(out Combatant self))
        {
            return;
        }

        if (tool == ToolType.Sickle || tool == ToolType.None)
        {
            speed = Mathf.Max(0.25f, 1f + self.Stats.Get(StatType.ForageSpeed));
            return;
        }

        if (tool != ToolType.Axe && tool != ToolType.Pickaxe)
        {
            return;
        }

        bool axe = tool == ToolType.Axe;
        speed = Mathf.Max(0.25f, 1f + self.Stats.Get(axe ? StatType.ChopSpeed : StatType.MineSpeed));
        sweetSpot = Mathf.Max(0.1f, 1f + self.Stats.Get(axe ? StatType.ChopSweetSpot : StatType.MineSweetSpot));
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

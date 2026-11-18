using UnityEngine;

/// <summary>
/// A boss in the world: on its prefab beside MonsterAI. Moves the fight through its phases, shows the bar and pays out when it falls.
/// <para>
/// Whatever else follows a boss (a boss encounter's arena, a story's When Boss, ground that gives way) knows it by its BossDefinition
/// through the static events (<see cref="Appeared"/>, <see cref="AnyPhaseEntered"/>, <see cref="AnyFell"/>), not by the one placed
/// in the scene: on a friend's game (co-op) that one is removed as it wakes (MonsterAI) and the host's arrives later as a copy
/// (CreaturePuppets), which raises them as the placed one would have.
/// </para>
/// <para>
/// Its topples (Docs/HeartrootBossReview_2026-11-17.md C2, C9): the bar's poise strip follows its poise every frame, a topple posts its
/// line and makes its heart glow, pulsing, until it rises (Boss Definition › Topples), and a stage that begins while it lies toppled
/// keeps it down at least the stage's reel rather than standing it up into a plain one.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI), typeof(Damageable))]
[OnAFriendsCopy(CopyRule.Same, "the bar, the music and the phases follow the health the host sends; the wake is the host's (skipped on a copy); " +
                               "beyond a gate this game hasn't sealed it shows nothing until the gate seals or the host's fight begins")]
public sealed class BossEncounter : MonoBehaviour
{
    /// <summary>A copy's health at or above this share is whole: the host's fight hasn't begun (its health comes as a byte, so 1/255 shows).</summary>
    private const float Whole = 0.999f;

    [SerializeField] private BossDefinition definition;

    private MonsterAI ai;
    private Damageable health;
    private int phase;
    private BossBar bar;
    private SceneAudio scene;
    private bool playingMusic;
    private bool held;
    // Woken out of a hold (a boss encounter's gate has sealed): it goes for the nearest at once, even with no wake to play.
    private bool wokenFromHold;
    // The hold's own invulnerability source: the wake adds and removes one of its own (this).
    private readonly object holdToken = new object();
    private static readonly int RoarState = Animator.StringToHash("Roar");
    // The heart's pulse while it lies toppled: a beat this long, the dim beat at this share of Topple Glow.
    private const float HeartbeatSeconds = 0.3f;
    private const float HeartbeatDim = 0.35f;
    private readonly WaitForSeconds heartbeat = new WaitForSeconds(HeartbeatSeconds);
    private Coroutine heartGlow;
    // The glow to put back when it rises: its body's as the topple began, or its new stage's if one began meanwhile.
    private Color glowAfterTopple;

    public BossDefinition Definition => definition;
    public int Phase => phase;

    /// <summary>True from its waking until it falls: the bar is up and the fight is on.</summary>
    public bool IsAwake { get; private set; }

    /// <summary>
    /// True while a friend's copy (co-op) enters a stage only to catch up with the host's fight: built mid-fight it enters every stage it
    /// missed at once before it is awake, and a jump of health past several marks enters all but the last on the way. What the stage is
    /// stands (its glow, its rage); what it does as it begins (its line, sound, shake and reel) was the host's game's, long ago, and a reel
    /// here would be told to the host's game and stagger the real boss mid-fight. Read by <see cref="AnyPhaseEntered"/>'s listeners to
    /// leave out a set piece in the same way (EruptionRings). Always false on the host's game and alone.
    /// </summary>
    public bool IsCatchingUp { get; private set; }

    /// <summary>
    /// <see cref="IsCatchingUp"/> for the stage at index <paramref name="entering"/> (0 the first) as a boss whose health has just reached
    /// <paramref name="reached"/> stages enters it: only a friend's copy catches up, every stage while it isn't awake yet (built mid-fight)
    /// and all but the last of a jump past several marks. Pure, for tests.
    /// </summary>
    public static bool CatchesUp(bool copy, bool awake, int entering, int reached) => copy && (!awake || entering + 1 < reached);

    /// <summary>Raised as it enters each phase (1 is the first after the opening), on the host and on a friend's copy alike.</summary>
    public event System.Action<BossEncounter, int> PhaseEntered;

    /// <summary>Raised once when it falls, after the first win is recorded and its skill given.</summary>
    public event System.Action<BossEncounter> Fell;

    /// <summary>
    /// Raised in each boss's Start, the one placed in a scene and a friend's copy of the host's alike, before it decides how it starts:
    /// a boss encounter's arena takes it as its own here and holds it (EncounterArena.Adopt). Not raised for a boss of this game's
    /// own on the host's run, which is being removed.
    /// </summary>
    public static event System.Action<BossEncounter> Appeared;

    /// <summary><see cref="PhaseEntered"/> for every boss: match it by its <see cref="Definition"/>.</summary>
    public static event System.Action<BossEncounter, int> AnyPhaseEntered;

    /// <summary><see cref="Fell"/> for every boss: match it by its <see cref="Definition"/>.</summary>
    public static event System.Action<BossEncounter> AnyFell;

    // Entering Play doesn't reload scripts in this project: listeners from the last session would hear this one's bosses.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Appeared = null;
        AnyPhaseEntered = null;
        AnyFell = null;
    }

    /// <summary>
    /// The definition of a boss placed in a scene, read even once it has been removed (a managed field, no call into the engine): on a
    /// friend's game (co-op) the placed one goes as it wakes and the host's copy stands in, so what points at the placed one matches
    /// the static events by this. Null when nothing is placed.
    /// </summary>
    public static BossDefinition DefinitionOf(BossEncounter placed) => ReferenceEquals(placed, null) ? null : placed.definition;

    /// <summary>
    /// Keeps it asleep where it stands (no bar, music or waking; untouchable and still) until <see cref="WakeNow"/>: a boss
    /// encounter's boss waits beyond its gate (EncounterArena holds it as it appears). Call before its Start. A friend's copy of the
    /// host's (co-op) is only kept from showing its bar and playing its music: it moves and strikes as the host's does.
    /// </summary>
    public void Hold() => held = true;

    /// <summary>Ends <see cref="Hold"/>: it wakes now (its bar, music and wake as usual) and goes for whoever is nearest.</summary>
    public void WakeNow() => held = false;

    /// <summary>Its rage: it hits harder by a share (0.5 = +50%), added to what its phases give. Each call adds more.</summary>
    public void Enrage(float damage)
    {
        if (damage <= 0f || health == null || !health.IsAlive)
        {
            return;
        }

        if (TryGetComponent(out Combatant combatant))
        {
            combatant.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, damage, this));
        }

        CameraShake.Add(0.5f);
        CreatureLook.SetEyes(gameObject, new Color(2.5f, 0.35f, 0.1f));
    }

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        health = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        health.HealthChanged += OnHealthChanged;
        health.Died += OnDied;
        if (ai != null)
        {
            ai.Toppled += OnToppled;
        }
    }

    private void OnDisable()
    {
        health.HealthChanged -= OnHealthChanged;
        health.Died -= OnDied;
        if (ai != null)
        {
            ai.Toppled -= OnToppled;
        }

        StopHeartGlow();
        HideBar();
        StopMusic();
    }

    // The poise strip under its health: how near its next topple (or stagger) is, pulsing when near; toppled, the time it has left down.
    private void Update()
    {
        if (bar == null || ai == null)
        {
            return;
        }

        PoiseMeter meter = ai.Poise;
        if (meter == null || !GameOptions.PoiseBars.On)
        {
            bar.HidePoise();
            return;
        }

        if (ai.TryGetToppleOpening(out float left, out float length))
        {
            bar.SetPoise(left / length, BossBar.PoiseLook.Toppled);
        }
        else
        {
            bar.SetPoise(meter.Fraction, ai.IsReeling ? BossBar.PoiseLook.Reeling : BossBar.PoiseLook.Steady);
        }
    }

    // It toppled (MonsterAI.Toppled, on a friend's copy too): its line, and its heart glowing while it lies.
    private void OnToppled(float seconds)
    {
        if (definition == null || health == null || !health.IsAlive)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(definition.ToppleLine))
        {
            GameMessages.Post(definition.ToppleLine, MessageKind.Gain);
        }

        if (definition.ToppleGlow.maxColorComponent > 0f && heartGlow == null)
        {
            glowAfterTopple = CreatureLook.GlowOf(gameObject);
            heartGlow = StartCoroutine(HeartGlow());
        }
    }

    // A heartbeat of light (Boss Definition › Topple Glow) for as long as it lies toppled, a stage's reel included; then the glow it had.
    private System.Collections.IEnumerator HeartGlow()
    {
        bool bright = false;
        while (ai != null && ai.IsToppled && health.IsAlive)
        {
            bright = !bright;
            CreatureLook.Glow(gameObject, bright ? definition.ToppleGlow : definition.ToppleGlow * HeartbeatDim);
            yield return heartbeat;
        }

        heartGlow = null;
        CreatureLook.Glow(gameObject, glowAfterTopple);
    }

    // Ends the topple's heartbeat, if it beats, and puts back the glow it had: as it dies (before its body goes to the corpse, which the
    // killing blow's feedback makes after HealthChanged), or as it is switched off or removed mid-beat.
    private void StopHeartGlow()
    {
        if (heartGlow == null)
        {
            return;
        }

        StopCoroutine(heartGlow);
        heartGlow = null;
        CreatureLook.Glow(gameObject, glowAfterTopple);
    }

    private void Start()
    {
        if (definition == null)
        {
            return;
        }

        bool remote = ai != null && ai.IsRemote;
        // One of this game's own on the host's run (co-op): it is removed before it thinks (MonsterAI), and the host's comes as a copy.
        if (!remote && CreatureSource.Remote != null)
        {
            return;
        }

        // Told first, so a boss encounter's arena can take it and hold it before it decides how it starts.
        Raise(Appeared, this);
        System.Collections.IEnumerator starting;
        if (held)
        {
            starting = remote ? Following() : Held();
        }
        else
        {
            starting = definition.WakeRange > 0f && !remote ? Dormant() : Begin();
        }

        StartCoroutine(starting);
    }

    // Beyond a boss encounter's gate: still and untouchable until the gate seals (EncounterArena calls WakeNow).
    private System.Collections.IEnumerator Held()
    {
        health.AddInvulnerability(holdToken);
        bool aiWasOn = ai != null && ai.enabled;
        if (ai != null)
        {
            ai.enabled = false;
        }

        while (held)
        {
            yield return null;
        }

        health.RemoveInvulnerability(holdToken);
        if (ai != null)
        {
            ai.enabled = aiWasOn;
        }

        wokenFromHold = true;
        yield return Begin();
    }

    // A friend's copy of the host's boss (co-op) beyond this game's gate, which is still open: no bar and no music until this player
    // steps through (the arena wakes it) or the host's fight has begun (its health, which follows the host's, drops). It isn't stilled
    // or made untouchable as a held boss is: it moves and strikes as the host's does (see Wake). Taken back into this game meanwhile
    // (the host left), it is held as this game's own.
    private System.Collections.IEnumerator Following()
    {
        while (held && health.IsAlive && health.HealthFraction >= Whole)
        {
            if (ai != null && !ai.IsRemote)
            {
                yield return Held();
                yield break;
            }

            yield return null;
        }

        held = false;
        yield return Begin();
    }

    // Placed on the map from the start (the Brukam on its bridge, review 2026-11-17): it sits where it is until someone comes within
    // Wake Range or hurts it; only then its bar, its music and its waking.
    private System.Collections.IEnumerator Dormant()
    {
        float full = health.HealthFraction;
        float range = definition.WakeRange * definition.WakeRange;
        while (health.HealthFraction >= full && !Near(range))
        {
            yield return new WaitForSeconds(0.25f);
        }

        yield return Begin();
    }

    private bool Near(float sqrRange)
    {
        if (Player.Current != null && (Player.Current.transform.position - transform.position).sqrMagnitude < sqrRange)
        {
            return true;
        }

        foreach (FriendHitbox friend in FriendHitbox.Active)
        {
            if (friend.IsAlive && (friend.transform.position - transform.position).sqrMagnitude < sqrRange)
            {
                return true;
            }
        }

        return false;
    }

    private System.Collections.IEnumerator Begin()
    {
        // Brought down before it woke (a friend's copy the host's fight ended first, one felled in its sleep): no bar to leave behind.
        if (!health.IsAlive)
        {
            yield break;
        }

        IsAwake = true;
        bar = BossBar.Show(definition);
        bar.Set(health.HealthFraction);
        yield return Wake();
    }

    // It rises before it fights: the ground shakes, its name goes up and it can't be hurt or do harm. Then it knows where the player is.
    private System.Collections.IEnumerator Wake()
    {
        scene = FindAnyObjectByType<SceneAudio>();
        if (scene != null && definition.Music != null && definition.Music.HasTracks)
        {
            scene.PlayEventMusic(definition.Music);
            playingMusic = true;
        }

        // A friend's copy of the host's boss (co-op) is built when they come near, often mid-fight: the waking is the host's game's,
        // and replaying it here froze the copy (its AI off, so its poses stopped) and made it untouchable for the wake's length.
        if (definition.WakeSeconds <= 0f || (ai != null && ai.IsRemote))
        {
            // Out of a hold (a boss encounter's gate has just sealed) it goes for the nearest at once. Elsewhere (an expedition's boss)
            // it finds the fight by itself, as it always has; a copy is the host's to steer.
            if (wokenFromHold && ai != null && !ai.IsRemote)
            {
                IDamageable target = Nearest();
                if (target != null)
                {
                    ai.Engage(target);
                }
            }

            yield break;
        }

        health.AddInvulnerability(this);
        // Asleep in the raid's eyes (CreatureSleep, a friend nearer than the host): its body is woken there, not by the AI alone.
        if (RaidDirector.Current != null && RaidDirector.Current.TryGetComponent(out CreatureSleep sleep))
        {
            sleep.WakeNow(ai);
        }

        ai.enabled = false;
        AudioManager.Play(definition.WakeSound, transform.position);
        CameraShake.Add(0.8f);
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.HasState(0, RoarState))
        {
            animator.CrossFadeInFixedTime(RoarState, 0.2f, 0);
        }

        bar.ShowCard(definition.WakeSeconds);
        yield return new WaitForSeconds(definition.WakeSeconds);
        health.RemoveInvulnerability(this);
        ai.enabled = true;
        IDamageable nearest = Nearest();
        if (nearest != null)
        {
            ai.Engage(nearest);
        }
    }

    // The nearest of the player and the friends still standing (co-op): whom it goes for as it wakes.
    private IDamageable Nearest()
    {
        Player player = Player.Current;
        IDamageable nearest = player != null ? player.Health : null;
        float best = player != null ? (player.transform.position - transform.position).sqrMagnitude : float.MaxValue;
        foreach (FriendHitbox friend in FriendHitbox.Active)
        {
            float distance = (friend.transform.position - transform.position).sqrMagnitude;
            if (friend.IsAlive && distance < best)
            {
                best = distance;
                nearest = friend;
            }
        }

        return nearest;
    }

    private void StopMusic()
    {
        if (playingMusic && scene != null)
        {
            scene.ResumeMusic();
        }

        playingMusic = false;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (definition == null)
        {
            return;
        }

        // Unity's own null check: the bar may already be gone with its canvas.
        if (bar != null)
        {
            bar.Set(health.HealthFraction);
        }

        // The killing blow ends the fight: no phase is entered on the way down to 0 (its line, shake and reel over the defeat). Killed
        // while it lay toppled, its heart stops beating here, before the body goes to its corpse with the glow mid-beat.
        if (!health.IsAlive)
        {
            StopHeartGlow();
            return;
        }

        int reached = BossDefinition.PhaseAt(health.HealthFraction, definition.Phases);
        bool copy = ai != null && ai.IsRemote;
        try
        {
            while (phase < reached)
            {
                IsCatchingUp = CatchesUp(copy, IsAwake, phase, reached);
                Enter(definition.Phases[phase], IsCatchingUp);
                phase++;
                ai.SetPhase(phase);
                Raise(PhaseEntered, this, phase);
                Raise(AnyPhaseEntered, this, phase);
            }
        }
        finally
        {
            IsCatchingUp = false;
        }
    }

    private void Enter(BossPhase next, bool catchingUp)
    {
        if (!catchingUp)
        {
            if (!string.IsNullOrWhiteSpace(next.line))
            {
                GameMessages.Post(next.line, MessageKind.Warning);
            }

            AudioManager.Play(next.Sound, transform.position);
            CameraShake.Add(0.6f);
        }

        if (next.glow.maxColorComponent > 0f)
        {
            CreatureLook.Glow(gameObject, next.glow);
            CreatureLook.SetEyes(gameObject, next.glow * 2f);
            // Begun while its heart glows from a topple: this is the glow it rises with.
            glowAfterTopple = next.glow;
        }

        if (next.damageBonus > 0f && TryGetComponent(out Combatant combatant))
        {
            combatant.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, next.damageBonus, this));
        }

        // Toppled, it stays down at least the reel; the blow that crossed the mark still wears its poise (MonsterAI.ReelForStage).
        if (next.reelSeconds > 0f && !catchingUp)
        {
            ai.ReelForStage(next.reelSeconds);
        }
    }

    private void OnDied(Damageable victim, DamageInfo hit)
    {
        StopHeartGlow();
        HideBar();
        StopMusic();
        IsAwake = false;
        if (GameSession.ActiveRaid != null)
        {
            GameSession.ActiveRaid.BossesBeaten++;
        }

        RecordTheWin();
        // Told last, each listener on its own (Raise): what they open (the way home, an arena's story) can't cost the first win or
        // its skill, as it could when they were told first.
        Raise(Fell, this);
        Raise(AnyFell, this);
    }

    // The boss falls in this player's own Haven's story, on a friend's raid too (co-op): their first win there is what counts.
    private void RecordTheWin()
    {
        // A friend on the host's run (HavenVisit: still so once the host has left and the copy became this game's own) whose own
        // story hasn't opened the encounter takes the drops only (the user, 2026-11-17: "only get the drop, but it helps their quest
        // as well if he is also on it"): their quests count the kill as any other. The host, or a player alone, always counts.
        // A raid already decided (the player fell, the clock ran out) wins nothing, whatever lands afterwards.
        if (Place.RaidDecided && RaidDirector.Current.Session.Outcome != RaidOutcome.Extracted)
        {
            return;
        }

        bool counts = !HavenVisit.IsVisiting || StoryReached();
        if (!counts || definition == null || !GameSession.Home.MarkBossDefeated(definition.Id))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(definition.DefeatLine))
        {
            GameMessages.Post(definition.DefeatLine, MessageKind.Gain);
        }

        // Known at once, and not part of what a failed raid takes.
        if (definition.RewardSkill != null && Player.Current != null && Player.Current.TryGetComponent(out PlayerSkills skills))
        {
            skills.Learn(definition.RewardSkill);
        }
    }

    // On a boss encounter: whether this player's own story has opened it. Elsewhere (a boss met on an expedition): always.
    private static bool StoryReached()
    {
        BossEncounterDefinition encounter = RaidDirector.Current != null ? RaidDirector.Current.Session.Definition.Encounter : null;
        return encounter == null || encounter.IsOpenBy(GameSession.Quests);
    }

    private void HideBar()
    {
        if (bar != null)
        {
            Destroy(bar.gameObject);
            bar = null;
        }
    }

    // Each listener on its own: one that throws is logged, and the others still hear it and the boss's own work goes on.
    private static void Raise(System.Action<BossEncounter> listeners, BossEncounter boss)
    {
        if (listeners == null)
        {
            return;
        }

        foreach (System.Delegate listener in listeners.GetInvocationList())
        {
            try
            {
                ((System.Action<BossEncounter>)listener)(boss);
            }
            catch (System.Exception problem)
            {
                Debug.LogException(problem, boss);
            }
        }
    }

    private static void Raise(System.Action<BossEncounter, int> listeners, BossEncounter boss, int reached)
    {
        if (listeners == null)
        {
            return;
        }

        foreach (System.Delegate listener in listeners.GetInvocationList())
        {
            try
            {
                ((System.Action<BossEncounter, int>)listener)(boss, reached);
            }
            catch (System.Exception problem)
            {
                Debug.LogException(problem, boss);
            }
        }
    }
}

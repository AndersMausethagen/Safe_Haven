using UnityEngine;

/// <summary>A boss in the world: on its prefab beside MonsterAI. Moves the fight through its phases, shows the bar and pays out when it falls.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI), typeof(Damageable))]
[OnAFriendsCopy(CopyRule.Same, "the bar, the music and the phases follow the health the host sends; the wake is the host's (skipped on a copy)")]
public sealed class BossEncounter : MonoBehaviour
{
    [SerializeField] private BossDefinition definition;

    private MonsterAI ai;
    private Damageable health;
    private int phase;
    private BossBar bar;
    private SceneAudio scene;
    private bool playingMusic;
    private static readonly int RoarState = Animator.StringToHash("Roar");

    public BossDefinition Definition => definition;
    public int Phase => phase;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        health = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        health.HealthChanged += OnHealthChanged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.HealthChanged -= OnHealthChanged;
        health.Died -= OnDied;
        HideBar();
        StopMusic();
    }

    private void Start()
    {
        if (definition != null)
        {
            StartCoroutine(definition.WakeRange > 0f && (ai == null || !ai.IsRemote) ? Dormant() : Begin());
        }
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
            yield break;
        }

        health.AddInvulnerability(this);
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
        IDamageable nearest = Player.Current != null ? Player.Current.Health : null;
        float best = Player.Current != null ? (Player.Current.transform.position - transform.position).sqrMagnitude : float.MaxValue;
        foreach (FriendHitbox friend in FriendHitbox.Active)
        {
            float distance = (friend.transform.position - transform.position).sqrMagnitude;
            if (friend.IsAlive && distance < best)
            {
                best = distance;
                nearest = friend;
            }
        }

        if (nearest != null)
        {
            ai.Engage(nearest);
        }
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

        // The killing blow ends the fight: no phase is entered on the way down to 0 (its line, shake and reel over the defeat).
        if (!health.IsAlive)
        {
            return;
        }

        int reached = BossDefinition.PhaseAt(health.HealthFraction, definition.Phases);
        while (phase < reached)
        {
            Enter(definition.Phases[phase]);
            phase++;
            ai.SetPhase(phase);
        }
    }

    private void Enter(BossPhase next)
    {
        if (!string.IsNullOrWhiteSpace(next.line))
        {
            GameMessages.Post(next.line, MessageKind.Warning);
        }

        AudioManager.Play(next.Sound, transform.position);
        CameraShake.Add(0.6f);
        if (next.glow.maxColorComponent > 0f)
        {
            CreatureLook.Glow(gameObject, next.glow);
            CreatureLook.SetEyes(gameObject, next.glow * 2f);
        }

        if (next.damageBonus > 0f && TryGetComponent(out Combatant combatant))
        {
            combatant.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, next.damageBonus, this));
        }

        if (next.reelSeconds > 0f)
        {
            ai.Stagger(next.reelSeconds);
        }
    }

    private void OnDied(Damageable victim, DamageInfo hit)
    {
        HideBar();
        StopMusic();
        if (GameSession.ActiveRaid != null)
        {
            GameSession.ActiveRaid.BossesBeaten++;
        }

        // The boss falls in this player's own Haven's story, on a friend's raid too (co-op): their first win there is what counts. The
        // host's game marks its own boss.
        bool first = definition != null && GameSession.Home.MarkBossDefeated(definition.Id);

        if (!first)
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

    private void HideBar()
    {
        if (bar != null)
        {
            Destroy(bar.gameObject);
            bar = null;
        }
    }
}

using UnityEngine;

/// <summary>A boss in the world: on its prefab beside MonsterAI. Moves the fight through its phases, shows the bar and pays out when it falls.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI), typeof(Damageable))]
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
            bar = BossBar.Show(definition);
            bar.Set(health.HealthFraction);
            StartCoroutine(Wake());
        }
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

        if (definition.WakeSeconds <= 0f)
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
        if (Player.Current != null)
        {
            ai.Engage(Player.Current.Health);
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

        bar?.Set(health.HealthFraction);
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

        if (definition == null || !GameSession.Settlement.MarkBossDefeated(definition.Id))
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

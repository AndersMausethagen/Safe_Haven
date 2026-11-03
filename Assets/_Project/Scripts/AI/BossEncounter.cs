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
    }

    private void Start()
    {
        if (definition != null)
        {
            bar = BossBar.Show(definition);
            bar.Set(health.HealthFraction);
        }
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

using UnityEngine;

/// <summary>
/// Writes a boss fight to the playtest log (Docs/HeartrootPlaytest_2026-11-23.md P0): a Boss row as it wakes, at each stage and each
/// topple, and one BossTry row when the try ends (won, died, or the scene left), with a BossTryTally's numbers. Added to every boss as
/// it appears, while runs are logged (PlaytestLogger).
/// </summary>
[DisallowMultipleComponent]
public sealed class BossTryLog : MonoBehaviour
{
    private readonly BossTryTally tally = new BossTryTally();
    private BossEncounter boss;
    private MonsterAI ai;
    private Damageable health;
    private Damageable player;
    private Combatant playerSide;
    private bool written;

    /// <summary>Puts a log on every boss that appears from now on (PlaytestLogger, when runs are logged).</summary>
    public static void Listen()
    {
        BossEncounter.Appeared -= OnAppeared;
        BossEncounter.Appeared += OnAppeared;
    }

    private static void OnAppeared(BossEncounter appeared)
    {
        if (appeared != null && !appeared.TryGetComponent(out BossTryLog _))
        {
            appeared.gameObject.AddComponent<BossTryLog>();
        }
    }

    private string BossId => boss != null && boss.Definition != null ? boss.Definition.Id : name;

    private int HealthPercent => health != null ? Mathf.RoundToInt(100f * health.CurrentHealth / Mathf.Max(1f, health.MaxHealth)) : 0;

    private void Awake()
    {
        boss = GetComponent<BossEncounter>();
        ai = GetComponent<MonsterAI>();
        health = GetComponent<Damageable>();
        if (boss != null)
        {
            boss.PhaseEntered += OnPhase;
            boss.Fell += OnFell;
        }

        if (ai != null)
        {
            ai.Toppled += OnToppled;
            ai.Summoned += OnSummoned;
        }

        if (health != null)
        {
            health.Damaged += OnBossHurt;
        }

        MonsterAI.AnyInterrupted += OnInterrupted;
        BoundSummons.AnySevered += OnSevered;
        GameEvents.Raised += OnGameEvent;
    }

    private void OnDestroy()
    {
        // The scene left with the fight under way (a quit, a reload).
        if (tally.Woke && !written)
        {
            Write("left");
        }

        if (boss != null)
        {
            boss.PhaseEntered -= OnPhase;
            boss.Fell -= OnFell;
        }

        if (ai != null)
        {
            ai.Toppled -= OnToppled;
            ai.Summoned -= OnSummoned;
        }

        if (health != null)
        {
            health.Damaged -= OnBossHurt;
        }

        Follow(null);
        MonsterAI.AnyInterrupted -= OnInterrupted;
        BoundSummons.AnySevered -= OnSevered;
        GameEvents.Raised -= OnGameEvent;
    }

    private void Update()
    {
        if (!tally.Woke && boss != null && boss.IsAwake)
        {
            tally.WokeAt = Time.time;
            PlaytestLogger.Note(PlaytestLog.Boss, "wake", HealthPercent, BossId);
        }

        if (tally.Woke && Player.Current != null && Player.Current.Health != player)
        {
            Follow(Player.Current);
        }
    }

    private void Follow(Player now)
    {
        if (player != null)
        {
            player.Damaged -= OnPlayerHurt;
            player.Died -= OnPlayerDied;
        }

        player = now != null ? now.Health : null;
        playerSide = now != null ? now.Combatant : null;
        if (player != null)
        {
            player.Damaged += OnPlayerHurt;
            player.Died += OnPlayerDied;
        }
    }

    private float Since => tally.Woke ? Time.time - tally.WokeAt : 0f;

    private void OnPhase(BossEncounter encounter, int reached)
    {
        tally.Stage = Mathf.Max(tally.Stage, reached);
        PlaytestLogger.Note(PlaytestLog.Boss, "stage " + (reached + 1), HealthPercent, $"{Since:0} s");
    }

    private void OnToppled(float seconds)
    {
        ToppleCause cause = ai != null ? ai.LastToppleCause : ToppleCause.Blows;
        tally.Toppled(cause);
        PlaytestLogger.Note(PlaytestLog.Boss, "topple", HealthPercent, $"{Since:0} s; {cause.ToString().ToLowerInvariant()}; {seconds:0.#} s down");
    }

    private void OnSummoned(MonsterAttack action, System.Collections.Generic.List<GameObject> roots) => tally.Calls++;

    private void OnSevered(BoundSummons bonds, bool toppled)
    {
        if (bonds != null && bonds.Summoner == ai)
        {
            tally.SummonsKilled++;
        }
    }

    private void OnInterrupted(MonsterAI monster, Combatant by)
    {
        if (monster == ai)
        {
            tally.Interrupts++;
        }
    }

    private void OnBossHurt(DamageInfo hit, DamageResult result)
    {
        if (tally.Woke && hit.Source != null && hit.Source == playerSide)
        {
            tally.Dealt(hit, result.HealthLost);
        }
    }

    private void OnPlayerHurt(DamageInfo hit, DamageResult result)
    {
        if (result.Damage > 0f)
        {
            tally.LastBlowOnPlayer = hit.Attack != null ? hit.Attack.name : hit.IsOverTime ? hit.Type + " over time" : "-";
        }
    }

    private void OnPlayerDied(Damageable victim, DamageInfo hit)
    {
        if (tally.Woke && !written)
        {
            Write("died");
        }
    }

    private void OnFell(BossEncounter fallen)
    {
        if (!written)
        {
            Write("won");
        }
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        if (tally.Woke && !written && gameEvent.Kind == GameEventKind.Consumed && gameEvent.Subject is ItemDefinition item && item.HealTotal > 0f)
        {
            tally.Draughts++;
        }
    }

    private void Write(string outcome)
    {
        written = true;
        PlaytestLogger.Note(PlaytestLog.BossTry, BossId, outcome == "won" ? 0 : HealthPercent, tally.Detail(outcome, Time.time));
    }
}

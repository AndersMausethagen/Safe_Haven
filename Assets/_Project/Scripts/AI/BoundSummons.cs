using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Summons tied to their summoner (Monster Attack › Bond Poise Share, Bond Topple Seconds: the Heartroot's Rootlings,
/// Docs/HeartrootPlaytest_2026-11-23.md P1). A glowing sap line runs from its chest to each one. Each one that dies sends a shock up
/// the line (a share of its poise), and when a whole call dies within Bond Within seconds the roots tear out of it: it topples, its heart
/// open. The Rootlings stop being a tax on the fight and become the way into it, as Rennala's students and the Greatwood's sacs are.
/// A Rootling crushed by its own slam counts too. Only in the game that runs the summoner: summons are made there.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoundSummons : MonoBehaviour
{
    private static readonly Color SapColour = new Color(1f, 0.68f, 0.22f, 0.85f);
    private const float LineWidth = 0.09f;
    private const float SummonChest = 0.55f;

    private sealed class Bond
    {
        public Damageable Body;
        public LineRenderer Line;
        public float Share;
    }

    private readonly SummonBondLedger ledger = new SummonBondLedger();
    private readonly List<Bond> bonds = new List<Bond>();
    private MonsterAI ai;

    /// <summary>A bound summon died: who it was tied to, and whether its death toppled the summoner (the boss's try row counts them).</summary>
    public static event Action<BoundSummons, bool> AnySevered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => AnySevered = null;

    public MonsterAI Summoner => ai;

    /// <summary>How many bound summons are alive now.</summary>
    public int Alive => bonds.Count;

    /// <summary>Adds the ties to a creature with any bound summon among its attacks.</summary>
    public static void AddIfBound(MonsterAI creature, MonsterDefinition definition)
    {
        if (creature == null || definition == null || creature.TryGetComponent(out BoundSummons _))
        {
            return;
        }

        foreach (MonsterAttack attack in definition.Attacks)
        {
            if (attack != null && (attack.bondPoiseShare > 0f || attack.bondToppleSeconds > 0f))
            {
                creature.gameObject.AddComponent<BoundSummons>();
                return;
            }
        }
    }

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.Summoned += OnSummoned;
        }
    }

    private void OnDestroy()
    {
        if (ai != null)
        {
            ai.Summoned -= OnSummoned;
        }

        foreach (Bond bond in bonds)
        {
            if (bond.Body != null)
            {
                bond.Body.Died -= OnDied;
            }

            if (bond.Line != null)
            {
                Destroy(bond.Line.gameObject);
            }
        }

        bonds.Clear();
    }

    private void OnSummoned(MonsterAttack action, List<GameObject> roots)
    {
        if (action == null || roots == null || (action.bondPoiseShare <= 0f && action.bondToppleSeconds <= 0f))
        {
            return;
        }

        var called = new List<object>(roots.Count);
        foreach (GameObject root in roots)
        {
            Damageable body = root != null ? root.GetComponentInChildren<Damageable>() : null;
            if (body == null || !body.IsAlive)
            {
                continue;
            }

            body.Died += OnDied;
            bonds.Add(new Bond { Body = body, Line = MakeLine(), Share = action.bondPoiseShare });
            called.Add(body);
        }

        ledger.Called(called, Time.time, action.bondWithin, action.bondToppleSeconds);
    }

    private void OnDied(Damageable body, DamageInfo hit)
    {
        body.Died -= OnDied;
        int index = bonds.FindIndex(bond => bond.Body == body);
        float share = 0f;
        if (index >= 0)
        {
            share = bonds[index].Share;
            if (bonds[index].Line != null)
            {
                Destroy(bonds[index].Line.gameObject);
            }

            bonds.RemoveAt(index);
        }

        if (!ledger.Died(body, Time.time, out float toppleSeconds) || ai == null || ai.CurrentState == MonsterAI.State.Dead)
        {
            return;
        }

        // The shock up the line: a share of its own poise, which may break it (a topple, as any break of a boss's poise is).
        if (share > 0f && ai.Poise != null)
        {
            ai.TakePoise(ai.Poise.BaseMax * share, ToppleCause.Bond);
        }

        bool toppled = false;
        if (toppleSeconds > 0f && !ai.IsToppled)
        {
            ai.ToppleFromBond(toppleSeconds);
            toppled = ai.IsToppled;
        }

        AnySevered?.Invoke(this, toppled);
    }

    private void LateUpdate()
    {
        if (bonds.Count == 0)
        {
            return;
        }

        Vector3 from = transform.position + Vector3.up * (ai != null ? ai.ChestHeight : 1.5f);
        // The sap pulses down the line, so it reads as alive and tied, not as a beam.
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
        for (int i = bonds.Count - 1; i >= 0; i--)
        {
            Bond bond = bonds[i];
            if (bond.Body == null)
            {
                // Gone without dying (unloaded): the call can't be finished now.
                ledger.Forget(bond.Body);
                if (bond.Line != null)
                {
                    Destroy(bond.Line.gameObject);
                }

                bonds.RemoveAt(i);
                continue;
            }

            if (bond.Line == null)
            {
                continue;
            }

            bond.Line.SetPosition(0, from);
            bond.Line.SetPosition(1, bond.Body.transform.position + Vector3.up * SummonChest);
            bond.Line.widthMultiplier = LineWidth * pulse;
        }
    }

    private LineRenderer MakeLine()
    {
        var holder = new GameObject("Sap Line");
        holder.transform.SetParent(transform, false);
        LineRenderer line = holder.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = LineWidth;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (feel != null && feel.TelegraphMaterial != null)
        {
            line.sharedMaterial = feel.TelegraphMaterial;
        }

        line.startColor = SapColour;
        line.endColor = new Color(SapColour.r, SapColour.g * 0.8f, SapColour.b, SapColour.a * 0.6f);
        return line;
    }
}

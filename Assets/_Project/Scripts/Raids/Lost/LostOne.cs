using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One of the Lost walking a raid, on its creature (an elite: EliteCreature carries its traits and its name). It waits a while, catches
/// the Keeper's scent and from then on keeps turning towards where the player is, roughly, whenever it isn't already on them; it says
/// so when it finds them. Wounded under the flee line (once a raid, below the highest rank) it runs and the mist takes it, to come back
/// scarred. What happens to its record (met, the kill, the escape, its rest) is the raid's part's, RaidLostOnes', which this tells.
/// Added by RaidLostOnes in the host's game only.
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.HostOnly, "the host's game hunts with it and keeps its record; a friend's copy is an elite under its name (CreaturePuppets) and does none of this")]
public sealed class LostOne : MonoBehaviour, ICopyAware
{
    /// <summary>Marks a Lost One's rank and name among an elite's affixes sent to friends: "~3~Halvard Hollow-Eyed|Frenzied".</summary>
    public const char NameMark = '~';

    private RaidLostOnes raid;
    private LostOneSettings settings;
    private System.Random random;
    private MonsterAI ai;
    private Damageable health;
    private Damageable prey;
    private float scentAt;
    private float nextSteer;
    private float vanishAt = -1f;
    private bool hasScent;
    private bool standingDown;
    private bool lastHurtWasMine;
    private Vector3 fleeTo;

    /// <summary>Who it is.</summary>
    public LostOneRecord Record { get; private set; }

    /// <summary>True from when it turns to flee until the mist takes it.</summary>
    public bool IsFleeing => vanishAt >= 0f;

    /// <summary>Makes a raid's creature this Lost One, hunting <paramref name="prey"/> (the local player).</summary>
    public static LostOne Attach(MonsterAI ai, LostOneRecord record, RaidLostOnes raid, LostOneSettings settings, System.Random random, Damageable prey)
    {
        var lost = ai.gameObject.AddComponent<LostOne>();
        lost.Begin(ai, record, raid, settings, random, prey);
        return lost;
    }

    private void Begin(MonsterAI creature, LostOneRecord record, RaidLostOnes owner, LostOneSettings rules, System.Random rolls, Damageable hunted)
    {
        ai = creature;
        Record = record;
        raid = owner;
        settings = rules;
        random = rolls;
        prey = hunted;
        health = creature.GetComponent<Damageable>();
        // It wants the Keeper: companions and buildings are left alone, and it never sleeps far off.
        creature.PlayerOnly = true;
        creature.KeepAwake = true;
        Vector2 after = settings.ScentAfter;
        scentAt = Time.time + Mathf.Lerp(Mathf.Min(after.x, after.y), Mathf.Max(after.x, after.y), (float)random.NextDouble());
        if (health != null)
        {
            health.Died += OnDied;
            health.Damaged += OnDamaged;
        }

        if (prey != null)
        {
            prey.Damaged += OnPreyDamaged;
        }
    }

    public void OnCopyChanged(bool isCopy) => standingDown = isCopy;

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }

        if (prey != null)
        {
            prey.Damaged -= OnPreyDamaged;
        }
    }

    private void Update()
    {
        if (standingDown || Record == null || raid == null || health == null || !health.IsAlive)
        {
            return;
        }

        if (IsFleeing)
        {
            if (Time.time >= vanishAt)
            {
                Vanish();
            }
            else if ((ai.CurrentState == MonsterAI.State.Chasing || ai.CurrentState == MonsterAI.State.Idle) && (transform.position - fleeTo).sqrMagnitude > 4f)
            {
                // A strike under way when it turned (a roam waits for it) has landed: now it runs.
                ai.Roam(fleeTo);
            }

            return;
        }

        if (prey != null && ai.Target is Component target && target != null && target.transform.IsChildOf(prey.transform)
            && (ai.CurrentState == MonsterAI.State.Chasing || ai.CurrentState == MonsterAI.State.Attacking))
        {
            raid.NoteMet(this);
        }

        // Not while it reels: a Draug knocked down to rise again does its rising first.
        if (raid.MayFlee && ai.CurrentState != MonsterAI.State.Staggered && health.CurrentHealth < health.MaxHealth * settings.FleeBelow)
        {
            Flee();
            return;
        }

        Track();
    }

    // The scent, then a turn towards the player now and then, roughly where they are, whenever it isn't already on someone.
    private void Track()
    {
        if (!hasScent)
        {
            if (Time.time < scentAt)
            {
                return;
            }

            hasScent = true;
            nextSteer = Time.time;
            GameMessages.Post(LostOneRules.Say(LostOneRules.Line(settings.ScentLines, random), Record, raid.RaidName), MessageKind.Warning);
            if (settings.ScentSound != null && prey != null)
            {
                AudioManager.Play(settings.ScentSound, prey.transform.position);
            }
        }

        if (Time.time < nextSteer || prey == null || !prey.IsAlive)
        {
            return;
        }

        nextSteer = Time.time + settings.SteerEvery;
        MonsterAI.State state = ai.CurrentState;
        if (state != MonsterAI.State.Idle && state != MonsterAI.State.Returning && state != MonsterAI.State.Investigating)
        {
            return;
        }

        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float off = (float)random.NextDouble() * settings.ScentError;
        ai.RoamSpeedScale = settings.HuntPace;
        ai.Roam(prey.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * off);
    }

    // It turns and runs, unhurt and noticing nobody, and the mist takes it a few seconds later.
    private void Flee()
    {
        vanishAt = Time.time + settings.FleeSeconds;
        raid.NoteFled(this);
        health.AddInvulnerability(this);
        ai.AvoidArea = _ => true;
        Vector3 away = prey != null ? transform.position - prey.transform.position : -transform.forward;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
        ai.RoamSpeedScale = 1f;
        fleeTo = transform.position + away * settings.FleeDistance;
        ai.Roam(fleeTo);
    }

    private const float VanishEffectSeconds = 5f;

    private void Vanish()
    {
        if (settings.VanishEffect != null)
        {
            // Taken away once played: the VFX pack's smoke doesn't destroy itself.
            Destroy(Instantiate(settings.VanishEffect, transform.position, Quaternion.identity), VanishEffectSeconds);
        }

        if (settings.VanishSound != null)
        {
            AudioManager.Play(settings.VanishSound, transform.position);
        }

        // Gone without dying: no loot, no rest. Friends' copies go with it (CreatureHost tells them it vanished).
        Destroy(gameObject);
    }

    // A blow between it and the Keeper means they have met, whoever saw whom first.
    private void OnDamaged(DamageInfo blow, DamageResult result)
    {
        if (blow.Source != null && Player.IsPlayer(blow.Source.gameObject))
        {
            raid.NoteMet(this);
        }
    }

    // Damaged comes before Died, so the raid's part knows of the kill before the raid ends on it. A death with nobody behind it (bleeding
    // out after being knocked down on a raid together, CoopDowned) is its kill when its blow was the last that hurt.
    private void OnPreyDamaged(DamageInfo blow, DamageResult result)
    {
        bool mine = blow.Source != null && blow.Source.transform.IsChildOf(transform);
        if (result.Killed && (mine || (blow.Source == null && lastHurtWasMine)))
        {
            raid.NoteKilledThePlayer(this);
        }

        if (blow.Source != null && result.HealthLost > 0f)
        {
            lastHurtWasMine = mine;
        }

        if (mine && result.HealthLost > 0f)
        {
            raid.NoteMet(this);
        }
    }

    private void OnDied(Damageable victim, DamageInfo blow) => raid.NoteLaidToRest(this, blow);

    /// <summary>Its rank and name, packed to go to friends with its affixes (CreatureHost): "~3~Halvard Hollow-Eyed".</summary>
    public static string Pack(LostOneRecord record) => $"{NameMark}{record.Rank}{NameMark}{record.FullName}";

    /// <summary>Reads one packed by <see cref="Pack"/>; false for an ordinary affix's name.</summary>
    public static bool TryUnpack(string part, out string name, out int rank)
    {
        name = null;
        rank = 1;
        if (string.IsNullOrEmpty(part) || part[0] != NameMark)
        {
            return false;
        }

        int split = part.IndexOf(NameMark, 1);
        if (split < 0 || !int.TryParse(part.Substring(1, split - 1), out rank))
        {
            return false;
        }

        name = part.Substring(split + 1);
        return !string.IsNullOrEmpty(name);
    }

    /// <summary>
    /// The label over it: its name, and under it its traits, each in its ring's colour once the player has seen through it and a "?"
    /// until then. <paramref name="known"/> null: all shown (a friend's copy, which knows nothing of the host's record).
    /// </summary>
    public static string LabelFor(string name, IReadOnlyList<EliteAffixDefinition> traits, System.Predicate<string> known, Color colour)
    {
        var text = new StringBuilder($"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{name}</color>");
        if (traits.Count == 0)
        {
            return text.ToString();
        }

        text.Append("\n<size=70%>");
        for (int i = 0; i < traits.Count; i++)
        {
            EliteAffixDefinition trait = traits[i];
            text.Append(i > 0 ? "  " : "");
            text.Append(known == null || known(trait.name)
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(trait.Colour)}>{trait.DisplayName}</color>"
                : $"<color={UIStyle.Current.MutedHex}>?</color>");
        }

        return text.Append("</size>").ToString();
    }
}

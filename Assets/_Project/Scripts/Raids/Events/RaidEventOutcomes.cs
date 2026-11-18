using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Where and for whom a raid event's outcomes play out.</summary>
public sealed class RaidEventContext
{
    public RaidSession Session;
    /// <summary>The raid running. Null in tests of the plain outcomes.</summary>
    public RaidDirector Director;
    public GameObject Player;
    /// <summary>The site, or the player's feet for an event without one.</summary>
    public Vector3 Position;
    public System.Random Random;
}

/// <summary>
/// One thing that comes of a raid event. Authored in the event's lists (SerializeReference). To add a kind: derive from
/// this with a [Serializable] class and a parameterless constructor; the Content Editor's menu finds it by itself.
/// </summary>
[Serializable]
public abstract class RaidEventOutcome
{
    /// <summary>The name in the Content Editor's menu.</summary>
    public abstract string DisplayName { get; }

    public abstract void Apply(RaidEventContext context);
}

/// <summary>Loot for the player, scaled by the raid's loot multiplier, as a cache would give it.</summary>
[Serializable]
public sealed class GiveLootOutcome : RaidEventOutcome
{
    [SerializeField] private ItemDropTable loot = new ItemDropTable();

    public override string DisplayName => "Give Loot";

    public override void Apply(RaidEventContext context)
    {
        if (context.Player == null)
        {
            return;
        }

        float multiplier = context.Session.Parameters.LootMultiplier * RaidDirector.StageLootNow;
        ItemInventory bag = context.Player.GetComponent<ItemInventory>();
        // Rows by the raid's tier (From/To Tier); the amount is scaled here with the event's own random, so Fixed Amount isn't read.
        foreach (ItemAmount drop in loot.Roll(context.Session.Tier, 1f))
        {
            if (!context.Session.Allows(drop.Item))
            {
                continue;
            }

            int amount = RaidParameters.ScaleAmount(drop.Amount, multiplier, (float)context.Random.NextDouble());
            int given = amount > 0 && bag != null ? bag.Give(drop.Item, amount, ArrivalCause.Found) : 0;
            if (given < amount)
            {
                GameMessages.Post($"Bag full, left behind: {amount - given} {drop.Item.DisplayName}", MessageKind.Warning);
                LootGrant.LeftBehind(context.Player, new[] { new ItemAmount(drop.Item, amount - given) });
            }
        }

        var resources = new List<ResourceAmount>();
        foreach (ResourceAmount drop in loot.RollResources(context.Session.Tier, 1f))
        {
            resources.Add(new ResourceAmount(drop.Resource, RaidParameters.ScaleAmount(drop.Amount, multiplier, (float)context.Random.NextDouble())));
        }

        LootGrant.LeftBehind(context.Player, LootGrant.GiveResources(context.Player, resources));
    }
}

/// <summary>Heals or wounds the player by a share of their health. A wound never kills.</summary>
[Serializable]
public sealed class ChangeHealthOutcome : RaidEventOutcome
{
    [Tooltip("Share of maximum health: 0.4 heals 40%, -0.2 wounds for 20%.")]
    [SerializeField, Range(-1f, 1f)] private float share = 0.25f;

    public override string DisplayName => "Heal or Wound";

    public override void Apply(RaidEventContext context)
    {
        Damageable health = context.Player != null ? context.Player.GetComponent<Damageable>() : null;
        if (health == null)
        {
            return;
        }

        if (share >= 0f)
        {
            health.Heal(health.MaxHealth * share);
            return;
        }

        float wound = Mathf.Min(-share * health.MaxHealth, health.CurrentHealth - 1f);
        if (wound > 0f)
        {
            health.ApplyDamage(new DamageInfo(null, null, wound, DamageType.Holy, false, context.Player.transform.position + Vector3.up, Vector3.up, 0f, HitTechnique.OverTime));
        }
    }
}

/// <summary>Adds time to the raid or takes it away.</summary>
[Serializable]
public sealed class ChangeTimeOutcome : RaidEventOutcome
{
    [Tooltip("Seconds added to the clock. Negative: time lost.")]
    [SerializeField] private float seconds = 60f;

    public override string DisplayName => "Change Raid Time";

    public ChangeTimeOutcome()
    {
    }

    public ChangeTimeOutcome(float seconds)
    {
        this.seconds = seconds;
    }

    public override void Apply(RaidEventContext context)
    {
        // What the clock really took: "-0:45 lost" at 30 s left took 15 s, and at 8 s none.
        float changed = context.Session.AddTime(seconds);
        string said = changed > 0.5f ? $"+{RaidStats.FormatTime(changed)} on the clock."
            : changed < -0.5f ? $"-{RaidStats.FormatTime(-changed)} lost."
            : "The mist presses close, but it can take no more of your time.";
        GameMessages.Post(said, changed >= 0f ? MessageKind.Gain : MessageKind.Warning);
    }
}

/// <summary>A timed bonus (or curse) on the player, like a meal's.</summary>
[Serializable]
public sealed class GiveBoonOutcome : RaidEventOutcome
{
    [SerializeField] private string name = "Blessing";
    [SerializeField] private List<StatBonus> bonuses = new List<StatBonus>();
    [SerializeField, Min(1f)] private float seconds = 180f;

    public override string DisplayName => "Give Boon or Curse";

    public override void Apply(RaidEventContext context)
    {
        PlayerBoons boons = context.Player != null ? context.Player.GetComponent<PlayerBoons>() : null;
        if (boons != null && boons.Add("event_" + name, name, bonuses, seconds, false))
        {
            GameMessages.Post($"{name}: {ItemTooltipView.DescribeBonuses(bonuses)} for {RaidStats.FormatTime(seconds)}", MessageKind.Gain);
        }
    }
}

/// <summary>A status effect on the player: poison in a trap, chill from a grave.</summary>
[Serializable]
public sealed class ApplyStatusOutcome : RaidEventOutcome
{
    [SerializeField] private StatusEffectDefinition effect;

    public override string DisplayName => "Apply Status";

    public override void Apply(RaidEventContext context)
    {
        StatusEffects.Apply(context.Player != null ? context.Player.GetComponent<Damageable>() : null, effect, null);
    }
}

/// <summary>Creatures appear around the event's place, on dry ground, with the raid's strength, hunting the player if it says so.</summary>
[Serializable]
public sealed class SpawnCreaturesOutcome : RaidEventOutcome
{
    [Tooltip("Picked at random for each creature.")]
    [SerializeField] private List<GameObject> prefabs = new List<GameObject>();
    [SerializeField, Min(1)] private int count = 3;
    [SerializeField, Min(0.5f)] private float radius = 6f;
    [Tooltip("They know where the player is and come at once.")]
    [SerializeField] private bool hunt = true;

    public override string DisplayName => "Spawn Creatures";

    public override void Apply(RaidEventContext context)
    {
        if (prefabs.Count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (!TryFindGround(context, out Vector3 ground))
            {
                continue;
            }

            GameObject prefab = prefabs[context.Random.Next(prefabs.Count)];
            float yaw = (float)context.Random.NextDouble() * 360f;
            // Co-op, on the host's run: the host's game makes them, after this player.
            if (CreatureSource.Remote != null)
            {
                CreatureSource.Remote.Spawn(prefab, ground, yaw, hunt);
                continue;
            }

            GameObject root = Object.Instantiate(prefab, ground, Quaternion.Euler(0f, yaw, 0f));
            if (context.Director != null)
            {
                context.Director.Creatures.Prepare(root, hunt);
            }
        }
    }

    // Tries a few places round the site for one creature: walkable and out of the water (RaidSpots.IsDryEnough). A site beside water
    // (a bridge's toll by the river, a wagon on the fen) still raises its creatures from the dry side instead of losing them.
    private const int GroundTries = 4;

    private bool TryFindGround(RaidEventContext context, out Vector3 ground)
    {
        for (int attempt = 0; attempt < GroundTries; attempt++)
        {
            float angle = (float)context.Random.NextDouble() * Mathf.PI * 2f;
            Vector3 point = context.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (NavMesh.SamplePosition(RaidSpots.Grounded(point), out NavMeshHit hit, 6f, NavMesh.AllAreas) && RaidSpots.IsDryEnough(hit.position))
            {
                ground = hit.position;
                return true;
            }
        }

        ground = default;
        return false;
    }
}

/// <summary>A noise the forest hears: creatures come to look.</summary>
[Serializable]
public sealed class MakeNoiseOutcome : RaidEventOutcome
{
    [SerializeField, Min(1f)] private float radius = 35f;

    public override string DisplayName => "Make Noise";

    public override void Apply(RaidEventContext context)
    {
        // Shared: on a co-op friend's game the creatures that should come are the host's.
        WorldNoise.EmitShared(context.Position, radius);
    }
}

/// <summary>Lifts the fog of war around the place, and around every open way out when it says so.</summary>
[Serializable]
public sealed class RevealMapOutcome : RaidEventOutcome
{
    [SerializeField, Min(1f)] private float radius = 60f;
    [SerializeField] private bool showExits = true;

    public override string DisplayName => "Reveal Map";

    public override void Apply(RaidEventContext context)
    {
        ExplorationMap map = ExplorationMap.Current;
        if (map == null)
        {
            return;
        }

        map.RevealAround(context.Position, radius);
        if (showExits)
        {
            foreach (ExtractionPoint exit in ExtractionPoint.Active)
            {
                map.RevealAround(exit.transform.position, 18f);
            }
        }
    }
}

/// <summary>Tells the player something, in one branch of a chance.</summary>
[Serializable]
public sealed class TellOutcome : RaidEventOutcome
{
    [SerializeField, TextArea] private string text;
    [SerializeField] private MessageKind kind = MessageKind.Info;

    public override string DisplayName => "Tell the Player";

    public override void Apply(RaidEventContext context)
    {
        GameMessages.Post(text, kind);
    }
}

/// <summary>A gamble: one list of outcomes by a chance, the other otherwise.</summary>
[Serializable]
public sealed class ChanceOutcome : RaidEventOutcome
{
    [SerializeField, Range(0f, 1f)] private float chance = 0.5f;
    [SerializeReference] private List<RaidEventOutcome> ifLucky = new List<RaidEventOutcome>();
    [SerializeReference] private List<RaidEventOutcome> ifNot = new List<RaidEventOutcome>();

    public override string DisplayName => "Chance";

    public ChanceOutcome()
    {
    }

    public ChanceOutcome(float chance, List<RaidEventOutcome> lucky, List<RaidEventOutcome> not)
    {
        this.chance = chance;
        ifLucky = lucky;
        ifNot = not;
    }

    public override void Apply(RaidEventContext context)
    {
        foreach (RaidEventOutcome outcome in context.Random.NextDouble() < chance ? ifLucky : ifNot)
        {
            outcome?.Apply(context);
        }
    }
}

/// <summary>A relic: found only out here, and lost like anything else if the player doesn't get it home (RelicSettings).</summary>
[Serializable]
public sealed class GiveRelicOutcome : RaidEventOutcome
{
    public override string DisplayName => "Give a Relic";

    public override void Apply(RaidEventContext context)
    {
        ItemStack relic = RelicSettings.Grant(context.Player, context.Random, context.Session.Tier);
        if (!relic.IsEmpty)
        {
            context.Session.RecordEvent($"Found {relic.DisplayName}");
        }
    }
}

/// <summary>
/// An inscription (Docs/EnchantingResearch.md #3): the one named, or one of the raid tier's found ones not yet known. Lost like anything
/// else if the player doesn't get it home.
/// </summary>
[Serializable]
public sealed class GiveInscriptionOutcome : RaidEventOutcome
{
    [Tooltip("Empty: one of the tier's Found enchantments, one not yet known first.")]
    [SerializeField] private EnchantmentDefinition enchantment;

    public override string DisplayName => "Give an Inscription";

    public override void Apply(RaidEventContext context)
    {
        EnchantmentDefinition given = enchantment != null ? enchantment
            : Inscriptions.Pick(Inscriptions.Pool(EnchantmentSource.Found, context.Session.Tier), context.Player, context.Random);
        if (given != null)
        {
            Inscriptions.Grant(context.Player, given, "a strange place");
        }
    }
}

/// <summary>Someone is freed and follows the player: they come home if they are alive and still with the player when they extract (RescuableTraveler).</summary>
[Serializable]
public sealed class RescueResidentOutcome : RaidEventOutcome
{
    [SerializeField] private ResidentDefinition resident;
    [Tooltip("The generic stranded traveler.")]
    [SerializeField] private RescuableTraveler traveler;

    public override string DisplayName => "Rescue Someone";

    public override void Apply(RaidEventContext context)
    {
        if (resident == null || traveler == null || GameSession.Home.HasResident(resident))
        {
            return;
        }

        Vector3 spot = context.Position + Vector3.right * 1.5f;
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            spot = hit.position;
        }

        RescuableTraveler freed = RescuableTraveler.Spawn(traveler, resident, spot, null);
        context.Director?.Creatures.Prepare(freed.gameObject, false);
    }
}

/// <summary>Whoever is lost longest stands here, lost, from the raid's start (SettlementState.LostToFind). Goes in an event's On Start.</summary>
[Serializable]
public sealed class StandTheLostOutcome : RaidEventOutcome
{
    public override string DisplayName => "The Lost Stand Here";

    public override void Apply(RaidEventContext context) => context.Director?.Companions.StandLostAt(context.Position);
}

/// <summary>Whoever stands here follows the player: they come home only if they reach an exit alive with them. The rest stays as it was.</summary>
[Serializable]
public sealed class LeadTheLostOutcome : RaidEventOutcome
{
    public override string DisplayName => "Lead the Lost Home";

    public override void Apply(RaidEventContext context) => context.Director?.Companions.LeadLostHome();
}

/// <summary>A blow against what is behind the next ward breach: it comes shorter of fighters, and weaker.</summary>
[Serializable]
public sealed class WeakenBreachOutcome : RaidEventOutcome
{
    [Tooltip("Share taken off the next breach.")]
    [SerializeField, Range(0f, 1f)] private float share = 0.3f;
    [Tooltip("The most raids can take off one breach.")]
    [SerializeField, Range(0f, 1f)] private float most = 0.6f;

    public override string DisplayName => "Weaken the Next Breach";

    // Co-op: a guest's blow goes to the host's game, whose breach it is (HavenHelp).
    public override void Apply(RaidEventContext context) => HavenHelp.WeakenBreach(share, most);
}

/// <summary>Burns the rot's heart behind the forest's push on one of the Haven's lands: the push loses a share (Territory).</summary>
[System.Serializable]
public sealed class WeakenIncursionOutcome : RaidEventOutcome
{
    public override string DisplayName => "Weaken the Forest's Push";

    public override void Apply(RaidEventContext context) => Territory.HeartBurned();
}

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

        float multiplier = context.Session.Parameters.LootMultiplier;
        ItemInventory bag = context.Player.GetComponent<ItemInventory>();
        foreach (ItemAmount drop in loot.Roll())
        {
            int amount = RaidParameters.ScaleAmount(drop.Amount, multiplier, (float)context.Random.NextDouble());
            int given = amount > 0 && bag != null ? bag.Give(drop.Item, amount) : 0;
            if (given < amount)
            {
                GameMessages.Post($"Bag full, left behind: {amount - given} {drop.Item.DisplayName}", MessageKind.Warning);
            }
        }

        var resources = new List<ResourceAmount>();
        foreach (ResourceAmount drop in loot.RollResources())
        {
            resources.Add(new ResourceAmount(drop.Resource, RaidParameters.ScaleAmount(drop.Amount, multiplier, (float)context.Random.NextDouble())));
        }

        LootGrant.GiveResources(context.Player, resources);
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
        context.Session.AddTime(seconds);
        GameMessages.Post(seconds >= 0f ? $"+{RaidStats.FormatTime(seconds)} on the clock." : $"-{RaidStats.FormatTime(-seconds)} lost.",
            seconds >= 0f ? MessageKind.Gain : MessageKind.Warning);
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

/// <summary>Creatures appear around the event's place, with the raid's strength, hunting the player if it says so.</summary>
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
            float angle = (float)context.Random.NextDouble() * Mathf.PI * 2f;
            Vector3 point = context.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!NavMesh.SamplePosition(RaidSpots.Grounded(point), out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                continue;
            }

            GameObject prefab = prefabs[context.Random.Next(prefabs.Count)];
            GameObject root = Object.Instantiate(prefab, hit.position, Quaternion.Euler(0f, (float)context.Random.NextDouble() * 360f, 0f));
            if (context.Director != null)
            {
                context.Director.PrepareSpawned(root, hunt);
            }
        }
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
        WorldNoise.Emit(context.Position, radius);
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The Lost in one raid, a part of the RaidDirector: whether one walks it (the same answer the briefing gave, from the run's seed), its
/// body brought in away from the start as a ranked elite under its own name, and what becomes of its record: met, fled (an escape and a
/// trait learned, at once), laid to rest (its things dropped where it fell, at once) or the player's killer (it rises at the raid's end,
/// with what the death took). Each raid's end is a homecoming, when new ones rise to fill the roster. Only the host's game (or one
/// playing alone) brings one: a friend's game shows the host's (CreaturePuppets), and a friend's death to it changes nothing in either
/// roster. Plain C#.
/// </summary>
public sealed class RaidLostOnes
{
    private readonly RaidDirector raid;
    private bool met;
    private bool fled;
    private bool laidToRest;
    private bool killedThePlayer;
    private string lastEvent;

    public RaidLostOnes(RaidDirector raid)
    {
        this.raid = raid;
    }

    /// <summary>The one walking this raid, or null.</summary>
    public LostOneRecord Walker { get; private set; }

    /// <summary>Its creature while it walks, or null.</summary>
    public LostOne Body { get; private set; }

    /// <summary>The raid's name, for what is said.</summary>
    public string RaidName => raid.Session != null ? raid.Session.Definition.DisplayName : "";

    /// <summary>Whether it may still flee this raid.</summary>
    public bool MayFlee => !laidToRest && !killedThePlayer && LostOneRules.CanFlee(Walker, LostOneSettings.Current, fled);

    /// <summary>
    /// Which of this character's Lost walks a run of <paramref name="raid"/> with this seed and tier, or null: the briefing asks before
    /// the portal opens and the raid when it begins, and both get the same answer, as nothing changes the roster in between.
    /// </summary>
    public static LostOneRecord ForRun(RaidDefinition raid, int seed, int tier)
    {
        LostOneSettings settings = LostOneSettings.Current;
        if (raid == null || PrologueDirector.Current != null || !LostOneRules.GateOpen(settings, GameSession.Quests, GameSession.Home.SuccessfulExtractions))
        {
            return null;
        }

        return LostOneRules.WhoWalks(GameSession.Progress.LostOnes, settings, seed, tier, raid.IsEncounter);
    }

    /// <summary>
    /// Brings this raid's Lost One in, when one walks it: out of sight, well away from where the player arrived, on the walkable ground and
    /// with a way to them, in the body its rank wears, strengthened as the raid's creatures are, then by its traits, rank and scars.
    /// Called by the RaidDirector as the raid begins (not in a boss encounter, nor when following the host's run).
    /// </summary>
    public void Summon(Vector3? start)
    {
        RaidSession session = raid.Session;
        LostOneSettings settings = LostOneSettings.Current;
        LostOneRecord walker = ForRun(session.Definition, session.Seed, session.Tier);
        MonsterDefinition body = walker != null ? settings.BodyFor(walker) : null;
        if (walker == null || body == null || body.Prefab == null)
        {
            return;
        }

        Vector3 from = start ?? (raid.Player != null ? raid.Player.transform.position : Vector3.zero);
        var random = new System.Random(RaidLayout.SeedFor(session.Seed, "The Lost Walk"));
        if (!RaidSpots.TryFindSpot(from, settings.NearestToStart, settings.FurthestFromStart, random, new NavMeshPath(), out Vector3 spot, 32))
        {
            Debug.LogWarning($"The Lost: nowhere {settings.NearestToStart:0}-{settings.FurthestFromStart:0} m from the start for {walker.FullName} to walk this raid.");
            return;
        }

        Vector3 facing = from - spot;
        facing.y = 0f;
        GameObject made = Object.Instantiate(body.Prefab, spot, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity);
        made.name = walker.FullName;
        raid.Creatures.Prepare(made, false, false);
        MonsterAI ai = made.GetComponentInChildren<MonsterAI>();
        if (ai == null)
        {
            Debug.LogWarning($"The Lost: {body.name}'s prefab has no MonsterAI.", body);
            Object.Destroy(made);
            return;
        }

        List<EliteAffixDefinition> traits = settings.TraitsOf(walker);
        EliteCreature elite = EliteCreature.Make(ai, traits, session.Difficulty, raid.HealthMultiplier, raid.PowerMultiplier);
        LostOneSettings.Rank rank = settings.RankAt(walker.Rank);
        elite.Strengthen(rank.health * (1f + LostOneRules.ScarShare(walker, settings)), rank.damage, raid.HealthMultiplier, raid.PowerMultiplier);
        elite.Rename(walker.FullName, LostOne.LabelFor(walker.FullName, traits, walker.Knows, settings.LabelColour));
        Walker = walker;
        Body = LostOne.Attach(ai, walker, this, settings, random, raid.PlayerHealth);
    }

    /// <summary>It and the Keeper have met (it set on them, or a blow passed between them): said once a raid, and remembered for the journal.</summary>
    public void NoteMet(LostOne body)
    {
        if (met || Walker == null)
        {
            return;
        }

        met = true;
        Walker.Met = true;
        var random = new System.Random(RaidLayout.SeedFor(raid.Session.Seed, "The Lost Met"));
        GameMessages.Post(LostOneRules.Say(LostOneRules.Line(LostOneSettings.Current.SightLines, random), Walker, RaidName), MessageKind.Warning);
        Event($"You met {Walker.FullName}, {Walker.PastLife}.");
    }

    /// <summary>It brought the player down: it will rise when the raid ends, with what the death took (Settle).</summary>
    public void NoteKilledThePlayer(LostOne body)
    {
        if (killedThePlayer || Walker == null || !Walker.IsWalking)
        {
            return;
        }

        killedThePlayer = true;
        var random = new System.Random(RaidLayout.SeedFor(raid.Session.Seed, "The Lost Kill"));
        GameMessages.Post(LostOneRules.Say(LostOneRules.Line(LostOneSettings.Current.KillLines, random), Walker, RaidName), MessageKind.Warning);
        Event($"{Walker.FullName} brought you down and took what you carried.");
    }

    /// <summary>It fled into the mist: an escape counted now (it comes back scarred) and one of its traits learned.</summary>
    public void NoteFled(LostOne body)
    {
        if (fled || Walker == null)
        {
            return;
        }

        fled = true;
        LostOneSettings settings = LostOneSettings.Current;
        var random = new System.Random(RaidLayout.SeedFor(raid.Session.Seed, "The Lost Flees"));
        string learned = LostOneRules.Escape(Walker, random);
        GameMessages.Post(LostOneRules.Say(LostOneRules.Line(settings.FleeLines, random), Walker, RaidName), MessageKind.Warning);
        EliteAffixDefinition trait = settings.FindTrait(learned);
        if (trait != null && !string.IsNullOrWhiteSpace(settings.LearnedLine))
        {
            GameMessages.Post(LostOneRules.Say(settings.LearnedLine, Walker, RaidName, trait.DisplayName));
        }

        Event(trait != null ? $"{Walker.FullName} fled into the mist. You saw through it: it is {trait.DisplayName}." : $"{Walker.FullName} fled into the mist.");
    }

    /// <summary>
    /// It was laid to rest: what it carried falls where it lies (picked up and carried out like any find), and a killer on the player's
    /// side earns its rest's Cursed Essence and Insight on top of the elite's loot.
    /// </summary>
    public void NoteLaidToRest(LostOne body, DamageInfo blow)
    {
        if (laidToRest || Walker == null || !Walker.IsWalking)
        {
            return;
        }

        laidToRest = true;
        LostOneSettings settings = LostOneSettings.Current;
        List<ItemStack> carried = LostOneRules.LayToRest(Walker, GameSession.Progress.LostOnes.Homecomings, RaidName, settings);
        foreach (ItemStack stack in carried)
        {
            if (DroppedItem.Drop(body.transform, stack) == null)
            {
                Debug.LogWarning($"The Lost: {stack.DisplayName} could not be dropped where {Walker.FullName} fell.");
            }
        }

        var random = new System.Random(RaidLayout.SeedFor(raid.Session.Seed, "The Lost Rests"));
        GameMessages.Post(LostOneRules.Say(LostOneRules.Line(settings.RestLines, random), Walker, RaidName), MessageKind.Gain);
        if (carried.Count > 0)
        {
            GameMessages.Post("What it took from you lies where it fell.", MessageKind.Gain);
        }

        Combatant taker = LootOnDeath.TakerOf(blow.Source);
        if (taker != null && Player.IsPlayer(taker.gameObject))
        {
            Reward(taker.gameObject, settings);
        }

        Event($"{Walker.FullName} was laid to rest.");
    }

    private void Reward(GameObject killer, LostOneSettings settings)
    {
        int above = Mathf.Max(0, Walker.Rank - 1);
        ResourceDefinition essence = SettlementSettings.Current.CursedEssence;
        int amount = settings.EssenceAtRest + settings.EssencePerRank * above;
        if (essence != null && amount > 0)
        {
            List<ResourceAmount> overflow = LootGrant.GiveResources(killer, new List<ResourceAmount> { new ResourceAmount(essence, amount) });
            if (overflow.Count > 0)
            {
                GameMessages.Post("Can't carry more: " + AmountText.Describe(overflow) + " left behind.", MessageKind.Warning);
                LootGrant.LeftBehind(killer, overflow);
            }
        }

        ExperienceTracker.GrantInsight(settings.InsightAtRest + settings.InsightPerRank * above);
    }

    /// <summary>
    /// The raid's end, a homecoming: the one met counts the raid, the one that killed the player rises with what the death took from
    /// their hands (<paramref name="final"/> against <paramref name="keeps"/>), and new ones rise to fill the roster once the gate is
    /// open. Called by the RaidDirector before the raid's end is saved.
    /// </summary>
    public void Settle(PlayerSnapshot final, PlayerSnapshot keeps)
    {
        LostOneSettings settings = LostOneSettings.Current;
        LostOneRoster roster = GameSession.Progress.LostOnes;
        if (Walker != null)
        {
            if (met)
            {
                Walker.RaidsMet++;
                Walker.LastSeenIn = RaidName;
            }

            if (killedThePlayer && Walker.IsWalking)
            {
                var random = new System.Random(RaidLayout.SeedFor(raid.Session.Seed, "The Lost Rises"));
                LostOneRules.RiseOnKill(Walker, LostOneRules.TakenStacks(final, keeps), settings, random);
            }
        }

        roster.Homecomings++;
        // This run's extraction is counted at home after the raid's end is told (RaidFinished), so it is added here: the third
        // homecoming opened the gate only at the fourth.
        int extractions = GameSession.Home.SuccessfulExtractions + (raid.Session != null && raid.Session.Outcome == RaidOutcome.Extracted ? 1 : 0);
        bool open = LostOneRules.GateOpen(settings, GameSession.Quests, extractions);
        LostOneRules.Refill(roster, settings, open, RaidSession.NewSeed());
    }

    // One line in the raid's report and journal for the Lost One, the latest in place of the last.
    private void Event(string line)
    {
        if (raid.Session == null)
        {
            return;
        }

        raid.Session.RecordEvent(line, lastEvent);
        lastEvent = line;
    }
}

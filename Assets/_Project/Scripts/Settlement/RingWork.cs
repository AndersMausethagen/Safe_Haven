using System.Collections.Generic;
using UnityEngine;

/// <summary>What a ring drawn in the Keeper's view is for.</summary>
public enum RingKind
{
    /// <summary>Trees to fell (the woodcutters), saplings planted behind.</summary>
    Fell = 0,
    /// <summary>Rocks to break (the masons): the boulders cleared for good, a stone outcrop broken each time it grows back.</summary>
    Quarry = 1,
}

/// <summary>
/// A ring drawn in the Keeper's view (Docs/ColonyModeResearch_2026-11-21.md §4.6-4.7, round two). A felling ring: the woodcutter fells
/// the grown trees inside it one by one, carries the logs to the chests and plants a sapling where each stood (the forester's loop: the
/// woods grow back and stay green); it is done when no grown tree is left. A quarry ring: the mason breaks the rocks inside it that a
/// pickaxe of their tier can, and carries the stone to the chests; it is done when nothing is left in it to break and nothing grows
/// back. Saved on the settlement.
/// </summary>
public sealed class RingOrder
{
    public RingOrder(Vector3 centre, float radius, RingKind kind = RingKind.Fell)
    {
        Centre = centre;
        Radius = Mathf.Max(1f, radius);
        Kind = kind;
    }

    public Vector3 Centre { get; }
    public float Radius { get; }
    public RingKind Kind { get; }

    /// <summary>Trees felled (or rocks broken) while the player was away, still to be laid down at home when the Haven is next the scene.</summary>
    public int PendingFells { get; set; }

    /// <summary>Grown trees (or rocks to break) in it when last seen at home (caps the work while away); -1 before it has been looked at.</summary>
    public int StandingSeen { get; set; } = -1;

    /// <summary>Where a sapling didn't take (a tree still too near it): tried again when the ring is done. Not saved.</summary>
    public List<Vector3> Unplanted { get; } = new List<Vector3>();

    public bool Contains(Vector3 point)
    {
        Vector3 apart = point - Centre;
        apart.y = 0f;
        return apart.sqrMagnitude <= Radius * Radius;
    }

    /// <summary>Whether the two rings share ground (a tree in both would be counted twice away).</summary>
    public bool Overlaps(RingOrder other)
    {
        Vector3 apart = other.Centre - Centre;
        apart.y = 0f;
        return apart.magnitude < Radius + other.Radius;
    }
}

/// <summary>What a ring worker is at: a painted tree (its terrain and index), a rock placed in the scene (a node), or a painted rock
/// (its terrain and index).</summary>
public readonly struct RingTarget
{
    public RingTarget(TerrainTreeFelling felling, TerrainTreeFelling.Standing tree)
    {
        Felling = felling;
        Index = tree.Index;
        Rock = null;
        Painted = null;
        Foot = tree.Foot;
        Height = tree.Height;
    }

    public RingTarget(ResourceNode rock)
    {
        Felling = null;
        Index = -1;
        Rock = rock;
        Painted = null;
        Foot = rock != null ? rock.transform.position : default;
        Height = 0f;
    }

    public RingTarget(TerrainNodeHarvesting painted, int index)
    {
        Felling = null;
        Index = index;
        Rock = null;
        Painted = painted;
        Foot = painted != null ? painted.PositionOf(index) : default;
        Height = 0f;
    }

    public TerrainTreeFelling Felling { get; }
    public int Index { get; }
    public ResourceNode Rock { get; }
    public TerrainNodeHarvesting Painted { get; }
    public Vector3 Foot { get; }
    public float Height { get; }

    public bool IsSet => Felling != null || Rock != null || Painted != null;

    /// <summary>True for a rock, placed or painted (a quarry ring's).</summary>
    public bool IsRock => Rock != null || Painted != null;

    /// <summary>What a rock's yield is read from: the placed node, or the painted one's prefab.</summary>
    public ResourceNode RockNode => Rock != null ? Rock : Painted != null ? Painted.PrefabNodeOf(Index) : null;

    /// <summary>Whether it is still there to work (nobody felled or broke it meanwhile).</summary>
    public bool Stands => Rock != null ? !Rock.IsDepleted && Rock.isActiveAndEnabled
        : Painted != null ? Painted.IsPresent(Index) : Felling != null && Felling.IsStanding(Index);

    /// <summary>Breaks the rock (placed or painted); <paramref name="quietly"/>, with no sound or burst. False when it was already down.</summary>
    public bool Break(bool quietly) => Rock != null ? Rock.BreakDown(quietly) : Painted != null && Painted.BreakByHand(Index, quietly);
}

/// <summary>
/// The work of rings (WorkerSettings › Felling rings, Quarry rings): who works each kind (the Fellers' and Quarriers' jobs), the next tree
/// or rock in a ring, what it gives, the sapling planted after a tree, and the work done while the player is away, laid down on coming
/// home. Plain rules where they can be (RingWorkTests); the scene's terrains through TerrainTreeFelling, its rocks through ResourceNode. A
/// ring worker's own bench comes first, at home and away: they work a ring while it has nothing to work on, so what they bring feeds it.
/// </summary>
public static class RingWork
{
    // Away: seconds towards the next tree or rock, by ring (not saved: one under way when the game is saved starts over).
    private static readonly Dictionary<RingOrder, float> AwayClock = new Dictionary<RingOrder, float>();

    // Trees and rocks a ring worker is at now (walking to it or working it): nobody else takes them, and coming home doesn't lay them down.
    private static readonly HashSet<(TerrainTreeFelling, int)> Claimed = new HashSet<(TerrainTreeFelling, int)>();
    private static readonly HashSet<ResourceNode> ClaimedRocks = new HashSet<ResourceNode>();
    private static readonly HashSet<(TerrainNodeHarvesting, int)> ClaimedPainted = new HashSet<(TerrainNodeHarvesting, int)>();
    // Painted rocks near a ring, gathered for each look (not kept).
    private static readonly List<int> Near = new List<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => ForgetGame();

    /// <summary>Forgets ring work under way (a new game, a load).</summary>
    public static void ForgetGame()
    {
        AwayClock.Clear();
        Claimed.Clear();
        ClaimedRocks.Clear();
        ClaimedPainted.Clear();
        Near.Clear();
        awayWorkers = null;
    }

    /// <summary>A woodcutter takes this tree (until <see cref="Release"/>).</summary>
    public static void Claim(TerrainTreeFelling felling, int index)
    {
        if (felling != null && index >= 0)
        {
            Claimed.Add((felling, index));
        }
    }

    /// <summary>Lets go of a tree taken with <see cref="Claim"/>.</summary>
    public static void Release(TerrainTreeFelling felling, int index)
    {
        if (felling != null && index >= 0)
        {
            Claimed.Remove((felling, index));
        }
    }

    /// <summary>A ring worker takes this tree or rock (until <see cref="Release(RingTarget)"/>).</summary>
    public static void Claim(RingTarget target)
    {
        if (target.Painted != null)
        {
            ClaimedPainted.Add((target.Painted, target.Index));
        }
        else if (target.Rock != null)
        {
            ClaimedRocks.Add(target.Rock);
        }
        else
        {
            Claim(target.Felling, target.Index);
        }
    }

    /// <summary>Lets go of what <see cref="Claim(RingTarget)"/> took (a rock destroyed meanwhile too: by reference, not Unity's null).</summary>
    public static void Release(RingTarget target)
    {
        if ((object)target.Painted != null)
        {
            ClaimedPainted.Remove((target.Painted, target.Index));
        }
        else if ((object)target.Rock != null)
        {
            ClaimedRocks.Remove(target.Rock);
        }
        else
        {
            Release(target.Felling, target.Index);
        }
    }

    /// <summary>Whether <paramref name="record"/>'s job fells rings (WorkerSettings › Fellers), at work and not paused.</summary>
    public static bool Fells(ResidentRecord record, WorkerSettings settings) => Works(record, RingKind.Fell, settings);

    /// <summary>Whether <paramref name="record"/>'s job works rings of <paramref name="kind"/> (Fellers, Quarriers), at work and not paused.</summary>
    public static bool Works(ResidentRecord record, RingKind kind, WorkerSettings settings)
    {
        IReadOnlyList<JobDefinition> jobs = kind == RingKind.Quarry ? settings.Quarriers : settings.Fellers;
        if (record == null || record.Job == null || record.WorkPaused || jobs == null)
        {
            return false;
        }

        foreach (JobDefinition job in jobs)
        {
            if (job == record.Job)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The kind of ring <paramref name="record"/> works, if any (a job does one kind).</summary>
    public static bool KindFor(ResidentRecord record, WorkerSettings settings, out RingKind kind)
    {
        kind = RingKind.Fell;
        if (Works(record, RingKind.Fell, settings))
        {
            return true;
        }

        kind = RingKind.Quarry;
        return Works(record, RingKind.Quarry, settings);
    }

    /// <summary>Seconds a feller spends on one tree: the chopping (shared by their work speed) and the walk with the logs.</summary>
    public static float SecondsPerTree(float workSpeed, WorkerSettings settings) =>
        settings.ChopSeconds / Mathf.Max(0.1f, workSpeed) + settings.FellWalkSeconds;

    /// <summary>Seconds a quarrier spends on one rock: the breaking (shared by their work speed) and the walk with the stone.</summary>
    public static float SecondsPerRock(float workSpeed, WorkerSettings settings) =>
        settings.QuarrySeconds / Mathf.Max(0.1f, workSpeed) + settings.FellWalkSeconds;

    /// <summary>Seconds of work on one target of a ring of <paramref name="kind"/> at work speed 1, at its tree or rock.</summary>
    public static float WorkSeconds(RingKind kind, WorkerSettings settings) => kind == RingKind.Quarry ? settings.QuarrySeconds : settings.ChopSeconds;

    /// <summary>
    /// The logs a tree of <paramref name="height"/> metres gives a feller: as many as the player's tree worked to the end (felled, every
    /// section cut: TreeChoppingSettings.AllLogs), of the woods the ring's axe tier rolls.
    /// </summary>
    public static List<ItemAmount> Logs(float height, WorkerSettings settings, System.Func<float> random)
    {
        var logs = new List<ItemAmount>();
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        float size = rules.SizeOf(Mathf.Max(0.5f, height));
        // The Haven's trees: never a raid wood, though the player is on a raid while the ring is felled.
        ChoppableTree.Roll(rules.AllLogs(settings.FellerAxeTier, size), settings.FellerAxeTier, size, random, logs, rules.HomeMaxWoodTier);
        return logs;
    }

    /// <summary>
    /// Whether a quarrier breaks <paramref name="rock"/>: a node a pickaxe gathers, within their pick's tier, gone once gathered, placed in
    /// the scene (not a painted rock's stand-in, which only stands near the player and isn't saved when gathered).
    /// </summary>
    public static bool Quarries(ResourceNode rock, WorkerSettings settings)
    {
        GatheringNodeDefinition definition = rock != null ? rock.Definition : null;
        return Quarries(definition, settings) && (rock.transform.parent == null || !rock.transform.parent.TryGetComponent(out TerrainNodeHarvesting _));
    }

    /// <summary>Whether a quarrier breaks rocks of <paramref name="definition"/> (placed or painted).</summary>
    public static bool Quarries(GatheringNodeDefinition definition, WorkerSettings settings) =>
        definition != null && definition.GatherTool == ToolType.Pickaxe && definition.DepleteOnGather
        && definition.RequiredToolTier <= settings.QuarryPickTier && settings.MayQuarry(definition);

    // A painted rock of a kind a quarrier breaks.
    private static bool QuarriesPainted(TerrainNodeHarvesting painted, int index, WorkerSettings settings)
    {
        ResourceNode node = painted.PrefabNodeOf(index);
        return node != null && Quarries(node.Definition, settings);
    }

    /// <summary>What a rock gives a quarrier: its yield and its burst, rolled as for the player outside a raid (items only).</summary>
    public static List<ItemAmount> Stone(ResourceNode rock)
    {
        var stone = new List<ItemAmount>();
        if (rock == null)
        {
            return stone;
        }

        if (rock.TryGetComponent(out ResourceSource source))
        {
            source.Drops.Roll(0, 1f, stone);
        }

        if (rock.Definition != null && rock.Definition.BurstYields != null)
        {
            rock.Definition.BurstYields.Roll(0, 1f, stone);
        }

        stone.RemoveAll(amount => amount.Item == null || amount.Amount <= 0);
        return stone;
    }

    /// <summary>The next tree or rock in <paramref name="ring"/>, nearest its centre, that nobody else is at; false when none is left.</summary>
    public static bool NextTarget(RingOrder ring, out RingTarget target)
    {
        target = default;
        if (ring == null)
        {
            return false;
        }

        if (ring.Kind == RingKind.Quarry)
        {
            return NextRock(ring, WorkerSettings.Current, out target);
        }

        if (NextTree(ring, out TerrainTreeFelling felling, out TerrainTreeFelling.Standing tree))
        {
            target = new RingTarget(felling, tree);
            return true;
        }

        return false;
    }

    /// <summary>The grown tree nearest the ring's centre that nobody else is at, on any terrain under it, or false.</summary>
    public static bool NextTree(RingOrder ring, out TerrainTreeFelling felling, out TerrainTreeFelling.Standing tree)
    {
        felling = null;
        tree = default;
        float best = float.MaxValue;
        foreach (TerrainTreeFelling candidate in TerrainTreeFelling.Active)
        {
            if (candidate == null)
            {
                continue;
            }

            foreach (TerrainTreeFelling.Standing standing in candidate.StandingNear(ring.Centre, ring.Radius))
            {
                if (Growing(candidate, standing.Index) || Claimed.Contains((candidate, standing.Index)))
                {
                    continue;
                }

                Vector3 apart = standing.Foot - ring.Centre;
                apart.y = 0f;
                if (apart.sqrMagnitude < best)
                {
                    best = apart.sqrMagnitude;
                    felling = candidate;
                    tree = standing;
                }
            }
        }

        return felling != null;
    }

    /// <summary>The rock (placed or painted) nearest the ring's centre that a quarrier breaks and nobody else is at, or false.</summary>
    public static bool NextRock(RingOrder ring, WorkerSettings settings, out RingTarget rock)
    {
        rock = default;
        float best = float.MaxValue;
        foreach (ResourceNode candidate in ResourceNode.Active)
        {
            if (candidate == null || candidate.IsDepleted || ClaimedRocks.Contains(candidate) || !ring.Contains(candidate.transform.position)
                || !Quarries(candidate, settings))
            {
                continue;
            }

            Vector3 apart = candidate.transform.position - ring.Centre;
            apart.y = 0f;
            if (apart.sqrMagnitude < best)
            {
                best = apart.sqrMagnitude;
                rock = new RingTarget(candidate);
            }
        }

        foreach (TerrainNodeHarvesting painted in TerrainNodeHarvesting.Active)
        {
            if (painted == null)
            {
                continue;
            }

            painted.PresentNear(ring.Centre, ring.Radius, Near);
            foreach (int index in Near)
            {
                if (ClaimedPainted.Contains((painted, index)) || !QuarriesPainted(painted, index, settings))
                {
                    continue;
                }

                Vector3 apart = painted.PositionOf(index) - ring.Centre;
                apart.y = 0f;
                if (apart.sqrMagnitude < best)
                {
                    best = apart.sqrMagnitude;
                    rock = new RingTarget(painted, index);
                }
            }
        }

        return rock.IsSet;
    }

    /// <summary>How many trees (grown, those being chopped too; saplings still growing don't count) or rocks to break stand in the ring
    /// now (at home).</summary>
    public static int Standing(RingOrder ring)
    {
        int count = 0;
        if (ring.Kind == RingKind.Quarry)
        {
            WorkerSettings settings = WorkerSettings.Current;
            foreach (ResourceNode rock in ResourceNode.Active)
            {
                count += rock != null && !rock.IsDepleted && ring.Contains(rock.transform.position) && Quarries(rock, settings) ? 1 : 0;
            }

            foreach (TerrainNodeHarvesting painted in TerrainNodeHarvesting.Active)
            {
                if (painted == null)
                {
                    continue;
                }

                painted.PresentNear(ring.Centre, ring.Radius, Near);
                foreach (int index in Near)
                {
                    count += QuarriesPainted(painted, index, settings) ? 1 : 0;
                }
            }

            return count;
        }

        foreach (TerrainTreeFelling candidate in TerrainTreeFelling.Active)
        {
            if (candidate == null)
            {
                continue;
            }

            foreach (TerrainTreeFelling.Standing standing in candidate.StandingNear(ring.Centre, ring.Radius))
            {
                count += Growing(candidate, standing.Index) ? 0 : 1;
            }
        }

        return count;
    }

    /// <summary>Whether something in a quarry ring grows back once broken (a stone outcrop): the ring stays, a quarry for good.</summary>
    public static bool Regrows(RingOrder ring)
    {
        if (ring.Kind != RingKind.Quarry)
        {
            return false;
        }

        WorkerSettings settings = WorkerSettings.Current;
        foreach (ResourceNode rock in ResourceNode.Active)
        {
            if (rock != null && rock.Definition != null && rock.Definition.RegrowsAfter > 0f && ring.Contains(rock.transform.position)
                && Quarries(rock, settings))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a ring with <paramref name="standing"/> left is done: nothing to work, and (a quarry) nothing that grows back.</summary>
    public static bool IsDone(int standing, bool regrows) => standing == 0 && !regrows;

    // A sapling still growing (planted by the forester, or by hand) is left standing.
    private static bool Growing(TerrainTreeFelling felling, int index) => PlantedTrees.Current != null && PlantedTrees.Current.IsGrowing(felling, index);

    /// <summary>A sapling where a tree came down (the forester), if the Haven plants and the ground takes one. False when it didn't.</summary>
    public static bool Replant(Vector3 foot, WorkerSettings settings) =>
        settings.Sapling != null && PlantedTrees.Current != null && PlantedTrees.Current.TryPlant(foot, settings.Sapling, out _);

    /// <summary>The ring done: saplings where they didn't take before, now that the trees round them are down.</summary>
    public static void ReplantLeft(RingOrder ring, WorkerSettings settings)
    {
        foreach (Vector3 foot in ring.Unplanted)
        {
            Replant(foot, settings);
        }

        ring.Unplanted.Clear();
    }

    /// <summary>
    /// The player away (SettlementSystem): <paramref name="seconds"/> more of each kind's ring workers' work: those at home (not the one
    /// along) whose bench has nothing to work on and who have no scaffold to raise, each for their kind's share of the time (working
    /// hours). Each tree or rock done gives what an average one does to the chests and is laid down at home later (PendingFells), up to
    /// what was last seen standing. The first ring of each kind with something left takes that kind's work.
    /// </summary>
    public static void WorkAway(SettlementState home, StashStorage stash, float seconds, ResidentDefinition along, WorkerSettings settings)
    {
        if (home.Rings.Count == 0 || seconds <= 0f)
        {
            return;
        }

        WorkAway(home, stash, seconds, along, settings, RingKind.Fell);
        WorkAway(home, stash, seconds, along, settings, RingKind.Quarry);
    }

    private static void WorkAway(SettlementState home, StashStorage stash, float seconds, ResidentDefinition along, WorkerSettings settings, RingKind kind)
    {
        float speed = 0f;
        foreach (ResidentRecord record in home.Residents)
        {
            if (record.Resident == along || !Works(record, kind, settings) || ResidentsOut.Now(home, record, along))
            {
                continue;
            }

            // Their own bench first, and a scaffold waiting, as at home.
            StationQueue bench = WorkerProduction.QueueOf(home, record);
            if ((bench != null && !bench.IsIdle && !bench.Blocked) || (home.ScaffoldsWantHands && ResidentBuilders.Builds(record, along)))
            {
                continue;
            }

            // A practised feller or quarrier counts for more, and how they are that day as at home (ResidentLife.WorkPace).
            float share = kind == RingKind.Quarry ? settings.AwayQuarryShare : settings.AwayFellingShare;
            speed += share * ResidentLife.WorkPace(record, SkillOf(kind));
            (awayWorkers ??= new List<ResidentRecord>()).Add(record);
        }

        if (speed <= 0f)
        {
            awayWorkers?.Clear();
            return;
        }

        // The first ring with something left takes the work, as at home; one never looked at waits to be seen.
        foreach (RingOrder ring in home.Rings)
        {
            if (ring.Kind != kind || ring.StandingSeen < 0 || ring.StandingSeen - ring.PendingFells <= 0)
            {
                continue;
            }

            AwayClock.TryGetValue(ring, out float clock);
            clock += seconds * speed;
            float each = kind == RingKind.Quarry ? SecondsPerRock(1f, settings) : SecondsPerTree(1f, settings);
            while (clock >= each && ring.StandingSeen - ring.PendingFells > 0)
            {
                clock -= each;
                ring.PendingFells++;
                // Each who worked it learns a share of the tree or rock (ResidentTalents).
                foreach (ResidentRecord worker in awayWorkers)
                {
                    ResidentTalents.Practise(worker, SkillOf(kind), 1,
                        LevelingSettings.Current.TierExperience(1) * PeopleSettings.Current.RingPractice / awayWorkers.Count);
                }

                IEnumerable<ItemAmount> gives = kind == RingKind.Quarry ? settings.AwayRockYield : Logs(settings.AverageTreeHeight, settings, () => Random.value);
                foreach (ItemAmount given in gives)
                {
                    if (given.Item != null && given.Amount > 0)
                    {
                        int added = stash.Items.Add(given.Item, given.Amount);
                        PlaytestLogger.Note(PlaytestLog.CameHome, kind == RingKind.Quarry ? "QuarryAway" : "FellAway", added, given.Item.Id);
                    }
                }
            }

            AwayClock[ring] = clock;
            awayWorkers.Clear();
            return;
        }

        awayWorkers?.Clear();
    }

    // Who worked the rings this stretch away, for their practice; cleared after each.
    private static List<ResidentRecord> awayWorkers;

    private static ProficiencyDefinition SkillOf(RingKind kind) =>
        kind == RingKind.Quarry ? PeopleSettings.Current.QuarrySkill : PeopleSettings.Current.FellingSkill;

    /// <summary>
    /// At home again: the trees felled and rocks broken while away are laid down (nearest the centre first, none a ring worker is already
    /// at), trees with their saplings; each ring's standing work noted, and rings done are removed. Only in the game that keeps the
    /// Haven, and only once a terrain is up to lay them on.
    /// </summary>
    public static void SettleAtHome(SettlementState home, WorkerSettings settings)
    {
        if (TerrainTreeFelling.Active.Count == 0)
        {
            return;
        }

        var done = new List<RingOrder>();
        foreach (RingOrder ring in home.Rings)
        {
            for (; ring.PendingFells > 0; ring.PendingFells--)
            {
                if (!NextTarget(ring, out RingTarget target))
                {
                    ring.PendingFells = 0;
                    break;
                }

                if (target.IsRock)
                {
                    // Its stone went to the chests while away; quietly, as it fell long ago.
                    target.Break(true);
                }
                else if (target.Felling.FellByHand(target.Index) && !Replant(target.Foot, settings))
                {
                    ring.Unplanted.Add(target.Foot);
                }
            }

            ring.StandingSeen = Standing(ring);
            if (IsDone(ring.StandingSeen, Regrows(ring)))
            {
                ReplantLeft(ring, settings);
                done.Add(ring);
            }
        }

        foreach (RingOrder ring in done)
        {
            home.RemoveRing(ring);
        }
    }
}

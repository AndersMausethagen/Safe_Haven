using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A reason to stay (Docs/SystemsGapReview_2026-11-19.md §4; GameDesignResearch T2): a danger stage with a reveal (RaidStage › Reveal)
/// places a cache 70-140 m from the player, out of sight and at least 50 m from any open exit, marks it gold on the minimap's rim and
/// says so: "Something glints beyond, to the north. The woods give more to those who stay." Its loot rolls with the stage's Reveal Loot
/// on top (the Glint +50%, the Hoard +80%). The stage bonus was a message and a number; a visible, timed offer away from the way home is
/// what makes the clock bind. Each game reveals its own (co-op), as rumours are. On the RaidDirector (added in its Awake).
/// <para>A stage with Veins (RaidStage › Veins) sprouts a cluster of the tier's ore the same way (RaidPacingSettings › Veins): richer
/// than the start layout's nodes, guarded by creatures of the tier, gold on the minimap. Nodes don't regrow in a raid (the user,
/// 2026-11-22), so new ore is found by going on, not by circling back.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class StageReveal : MonoBehaviour
{
    private const float Nearest = 70f;
    private const float Farthest = 140f;
    private const float FromExits = 50f;

    private const float VeinSpread = 4f;

    private static readonly List<LootCache> sites = new List<LootCache>();
    private static readonly List<ResourceNode> veins = new List<ResourceNode>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        sites.Clear();
        veins.Clear();
    }

    /// <summary>The revealed caches still standing in this raid, for the minimap.</summary>
    public static IReadOnlyList<LootCache> Sites => sites;

    /// <summary>True for a cache a stage revealed (for the log as it is opened).</summary>
    public static bool IsRevealed(LootCache cache) => cache != null && sites.Contains(cache);

    /// <summary>The ore a stage's veins sprouted in this raid, for the minimap (gold while not yet mined).</summary>
    public static IReadOnlyList<ResourceNode> Veins => veins;

    private RaidDirector director;
    private NavMeshPath path;

    private void Awake() => director = GetComponent<RaidDirector>();

    private void OnEnable()
    {
        if (director != null)
        {
            director.StageChanged += OnStageChanged;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.StageChanged -= OnStageChanged;
        }
    }

    private void OnDestroy()
    {
        sites.Clear();
        veins.Clear();
    }

    private void OnStageChanged(int index, RaidStage stage)
    {
        Player player = Player.Current;
        if (stage == null || player == null)
        {
            return;
        }

        path ??= new NavMeshPath();
        if (stage.Veins)
        {
            SproutVeins(index, stage, player.transform.position);
        }

        if (stage.Reveal == RaidStage.RevealKind.None || stage.RevealCache == null)
        {
            return;
        }

        var random = new System.Random(director.Session != null ? director.Session.Seed * 31 + index : index);
        Vector3 from = player.transform.position;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (!RaidSpots.TryFindSpot(from, Nearest, Farthest, random, path, out Vector3 spot) || NearAnExit(spot))
            {
                continue;
            }

            GameObject placed = Instantiate(stage.RevealCache, spot, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
            if (placed.TryGetComponent(out LootCache cache))
            {
                cache.ExtraLoot = Mathf.Max(1f, stage.RevealLoot);
                sites.Add(cache);
            }

            string way = HavenPlaceVerbs.Compass(new Vector2(from.x, from.z), new Vector2(spot.x, spot.z));
            string what = stage.Reveal == RaidStage.RevealKind.Hoard ? "A hoard lies open" : "Something glints";
            GameMessages.Post($"{what} beyond, to the {way}. The woods give more to those who stay.", MessageKind.Info);
            PlaytestLogger.Note(PlaytestLog.StageReveal, stage.Reveal.ToString(), Mathf.RoundToInt(Vector3.Distance(from, spot)), stage.DisplayName);
            return;
        }
    }

    // A cluster of the tier's ore away from the player and the exits, guarded. Seeded from the raid and the stage, like the cache.
    private void SproutVeins(int index, RaidStage stage, Vector3 from)
    {
        RaidPacingSettings pacing = RaidPacingSettings.Current;
        int tier = director.Session != null ? director.Session.Tier : 1;
        GameObject node = pacing.VeinNodeAt(tier);
        if (node == null)
        {
            return;
        }

        var random = new System.Random(director.Session != null ? director.Session.Seed * 37 + index : index + 7);
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (!RaidSpots.TryFindSpot(from, Nearest, Farthest, random, path, out Vector3 spot) || NearAnExit(spot))
            {
                continue;
            }

            int placed = 0;
            for (int i = 0; i < pacing.VeinsEach; i++)
            {
                if (!GroundNear(spot, i == 0 ? 0f : VeinSpread, random, out Vector3 ground))
                {
                    continue;
                }

                GameObject vein = Instantiate(node, ground, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                vein.name = node.name + " (Vein)";
                if (vein.TryGetComponent(out ResourceSource source))
                {
                    source.VeinYield = pacing.VeinYield;
                }

                if (vein.TryGetComponent(out ResourceNode resource))
                {
                    veins.Add(resource);
                }

                placed++;
            }

            if (placed == 0)
            {
                continue;
            }

            Guard(spot, tier, pacing.VeinGuards, random);
            string way = HavenPlaceVerbs.Compass(new Vector2(from.x, from.z), new Vector2(spot.x, spot.z));
            GameMessages.Post($"Ore breaks through the ground to the {way}: a rich vein (×{pacing.VeinYield:0.#}), and something guards it.", MessageKind.Info);
            PlaytestLogger.Note(PlaytestLog.StageReveal, "Vein", Mathf.RoundToInt(Vector3.Distance(from, spot)), stage.DisplayName);
            return;
        }
    }

    // Creatures of the raid's tier, at home by the vein (they don't hunt the player until they see them). Each game sprouts its own
    // veins; on a friend's run the creatures are the host's, so a guest's veins go unguarded rather than set the host's creatures by a
    // vein the host can't see.
    private void Guard(Vector3 spot, int tier, int count, System.Random random)
    {
        if (CreatureSource.Remote != null)
        {
            return;
        }

        var choices = new List<SpawnTable.Entry>();
        float total = 0f;
        foreach (RaidSpawnList list in director.Session.Definition.SpawnLists)
        {
            if (list == null || list.category != SpawnCategory.Enemies)
            {
                continue;
            }

            foreach (SpawnTable.Entry entry in list.EntriesFor(tier))
            {
                if (entry.prefab != null && entry.weight > 0f && entry.prefab.GetComponentInChildren<MonsterAI>(true) != null)
                {
                    choices.Add(entry);
                    total += entry.weight;
                }
            }
        }

        for (int i = 0; i < count && total > 0f; i++)
        {
            float pick = (float)random.NextDouble() * total;
            SpawnTable.Entry chosen = choices[choices.Count - 1];
            foreach (SpawnTable.Entry entry in choices)
            {
                pick -= entry.weight;
                if (pick <= 0f)
                {
                    chosen = entry;
                    break;
                }
            }

            if (!GroundNear(spot, VeinSpread * 2f, random, out Vector3 ground))
            {
                continue;
            }

            float yaw = (float)random.NextDouble() * 360f;
            GameObject root = Instantiate(chosen.prefab, ground, Quaternion.Euler(0f, yaw, 0f));
            foreach (MonsterAI monster in root.GetComponentsInChildren<MonsterAI>())
            {
                monster.ResetHome();
            }

            director.Creatures.Prepare(root, false);
        }
    }

    // Walkable dry ground within reach of a spot.
    private static bool GroundNear(Vector3 spot, float reach, System.Random random, out Vector3 ground)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            Vector3 point = spot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach * (0.6f + 0.4f * (float)random.NextDouble());
            if (NavMesh.SamplePosition(RaidSpots.Grounded(point), out NavMeshHit hit, 4f, NavMesh.AllAreas) && RaidSpots.IsDryEnough(hit.position))
            {
                ground = hit.position;
                return true;
            }
        }

        ground = default;
        return false;
    }

    private static bool NearAnExit(Vector3 spot)
    {
        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            if (exit != null && exit.Status != ExtractionStatus.Closed && (exit.transform.position - spot).sqrMagnitude < FromExits * FromExits)
            {
                return true;
            }
        }

        return false;
    }
}

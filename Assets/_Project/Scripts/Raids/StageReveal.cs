using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A reason to stay (Docs/SystemsGapReview_2026-11-19.md §4; GameDesignResearch T2): a danger stage with a reveal (RaidStage › Reveal)
/// places a cache 70-140 m from the player, out of sight and at least 50 m from any open exit, marks it gold on the minimap's rim and
/// says so: "Something glints beyond, to the north. The woods give more to those who stay." Its loot rolls with the stage's Reveal Loot
/// on top (the Glint +50%, the Hoard +80%). The stage bonus was a message and a number; a visible, timed offer away from the way home is
/// what makes the clock bind. Each game reveals its own (co-op), as rumours are. On the RaidDirector (added in its Awake).
/// </summary>
[DisallowMultipleComponent]
public sealed class StageReveal : MonoBehaviour
{
    private const float Nearest = 70f;
    private const float Farthest = 140f;
    private const float FromExits = 50f;

    private static readonly List<LootCache> sites = new List<LootCache>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => sites.Clear();

    /// <summary>The revealed caches still standing in this raid, for the minimap.</summary>
    public static IReadOnlyList<LootCache> Sites => sites;

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

    private void OnDestroy() => sites.Clear();

    private void OnStageChanged(int index, RaidStage stage)
    {
        Player player = Player.Current;
        if (stage == null || stage.Reveal == RaidStage.RevealKind.None || stage.RevealCache == null || player == null)
        {
            return;
        }

        path ??= new NavMeshPath();
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

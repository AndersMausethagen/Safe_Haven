using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A supply cache found in raids: a chest, a crate, a fallen traveller's pack. A chest is locked: the player has to pick it
/// first (PlayerLockpicking), which is heard, and so is its lid coming up. Searching it (its Interactable) rolls the loot
/// table once, scaled by the raid's loot modifier, and hands the player what fits: items to the bag, resources to the
/// resource bars. Whatever doesn't fit stays inside for another search; once empty it shows its empty look and can't be
/// searched again. The Interactable beside it must be Work, not Use, and must not deactivate on completion.
/// <para>
/// What it holds is its Loot Table asset (a kind of cache: a hunter's cache, a barrow chest, a troll's hoard) when one is set, plus
/// the raid's own Regional Loot, rolled at the raid's tier: the same kind of chest holds different things in different raids.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[OnAFriendsCopy(CopyRule.Same, "personal loot: each game rolls its own")]
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid's cache, personal loot")]
public sealed class LootCache : MonoBehaviour, IInteractionVerb
{
    [Tooltip("What this kind of cache holds (Data/Loot/Caches). Empty: the inline Drops list below.")]
    [SerializeField] private LootTable table;
    [SerializeField] private ItemDropTable drops = new ItemDropTable();
    [Tooltip("Pins in its lock, each a press of the lockpicking bar. 0: no lock, as on a fallen traveller's pack.")]
    [SerializeField, Range(0, 6)] private int lockPins = 3;
    [Tooltip("Shown while the cache still holds something, such as a closed lid. Optional.")]
    [SerializeField] private GameObject fullVisual;
    [Tooltip("Shown once the cache is empty, such as an open lid. Optional.")]
    [SerializeField] private GameObject emptyVisual;
    [Tooltip("A small find in a pocket off the trail (a dropped sack, an offering, a wanderer's remains): only its own table, no regional " +
             "loot and no chance of a relic or an inscription (Docs/RaidLayoutAnalysis_2026-11-26.md R2).")]
    [SerializeField] private bool smallFind;

    private const int MostEarlyTier = 2;

    private readonly List<ItemAmount> contents = new List<ItemAmount>();
    private readonly List<ResourceAmount> resourceContents = new List<ResourceAmount>();
    private bool holdsRelic;
    private bool holdsInscription;
    private Interactable interactable;
    private bool rolled;
    private bool unlocked;
    private bool forced;

    public ItemDropTable Drops => table != null ? table.Drops : drops;
    public LootTable Table => table;

    /// <summary>How much more it holds than its table (a cache a stage revealed, StageReveal): 1 for none.</summary>
    public float ExtraLoot { get; set; } = 1f;
    public bool IsEmpty => rolled && contents.Count == 0 && resourceContents.Count == 0;
    public int LockPins => lockPins;
    public bool SmallFind => smallFind;

    /// <summary>True until its lock has been opened. Nothing is ever locked without pins.</summary>
    public bool IsLocked => lockPins > 0 && !unlocked;

    public string Verb => IsLocked ? "Pick the lock" : "Search";

    /// <summary>The lock is open. <paramref name="byForce"/>: it broke, which was its own noise, so the lid adds none.</summary>
    public void Unlock(bool byForce)
    {
        unlocked = true;
        forced = byForce;
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        ShowState();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnSearched);
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnSearched);
    }

    private void OnValidate()
    {
        drops.Validate();
    }

    private void OnSearched()
    {
        GameObject searcher = interactable.Interactor;
        if (StageReveal.IsRevealed(this) && RaidDirector.Current != null)
        {
            PlaytestLogger.Note(PlaytestLog.RevealOpened, name, Mathf.RoundToInt(RaidDirector.Current.Session.TimeRemaining));
        }

        ItemInventory bag = searcher != null ? searcher.GetComponentInParent<ItemInventory>() : null;
        if (bag == null)
        {
            return;
        }

        if (!rolled)
        {
            Fill();
            LiftLid(searcher);
        }

        // A relic or an inscription is something: "Nothing useful inside" only when the cache gave nothing at all (review 2026-11-17, R3).
        bool gaveSomething = false;
        if (holdsRelic)
        {
            holdsRelic = false;
            RaidDirector raid = RaidDirector.Current;
            gaveSomething |= !RelicSettings.Grant(searcher, new System.Random(Random.Range(0, int.MaxValue)), raid.Session.Tier).IsEmpty;
        }

        if (holdsInscription)
        {
            holdsInscription = false;
            int tier = RaidDirector.Current != null ? RaidDirector.Current.Session.Tier : 1;
            EnchantmentDefinition found = Inscriptions.Pick(Inscriptions.Pool(EnchantmentSource.Found, tier), searcher, new System.Random(Random.Range(0, int.MaxValue)));
            if (found != null)
            {
                gaveSomething |= !Inscriptions.Grant(searcher, found, "a supply cache").IsEmpty;
            }
        }

        if (!gaveSomething && contents.Count == 0 && resourceContents.Count == 0)
        {
            GameMessages.Post("Nothing useful inside.");
        }

        List<ResourceAmount> resourcesLeft = LootGrant.GiveResources(searcher, resourceContents);
        resourceContents.Clear();
        resourceContents.AddRange(resourcesLeft);
        if (resourceContents.Count > 0)
        {
            GameMessages.Post("Can't carry more, still inside: " + AmountText.Describe(resourceContents), MessageKind.Warning);
        }

        var leftOver = new List<ItemAmount>();
        foreach (ItemAmount entry in contents)
        {
            int given = bag.Give(entry.Item, entry.Amount, ArrivalCause.Found);
            if (given < entry.Amount)
            {
                leftOver.Add(new ItemAmount(entry.Item, entry.Amount - given));
            }
        }

        contents.Clear();
        contents.AddRange(leftOver);
        if (contents.Count > 0)
        {
            GameMessages.Post("Bag full, still inside: " + AmountText.Describe(contents), MessageKind.Warning);
        }

        ShowState();
    }

    // A chest's lid is heard however well the lock was picked; a pack without a lock opens silently.
    private void LiftLid(GameObject searcher)
    {
        if (lockPins <= 0 || forced)
        {
            return;
        }

        LockpickingSettings settings = LockpickingSettings.Current;
        AudioManager.Play(settings.OpenSound, transform.position);
        PlayerNoise noise = searcher.GetComponentInParent<PlayerNoise>();
        if (noise != null)
        {
            noise.Make(transform.position, settings.OpenNoise);
        }
    }

    private void Fill()
    {
        rolled = true;
        float multiplier = (RaidDirector.Current != null ? RaidDirector.Current.Parameters.LootMultiplier * RaidDirector.StageLootNow : 1f) * ExtraLoot;
        if (RaidDirector.Current != null)
        {
            JournalNotices.RollForFind();
        }

        holdsRelic = !smallFind && RaidDirector.Current != null && Random.value < RelicSettings.Current.CacheChance * multiplier;
        holdsInscription = !smallFind && RaidDirector.Current != null && Random.value < EssenceSettings.Current.CacheInscriptionChance * multiplier;
        int tier = RaidDirector.Current != null ? RaidDirector.Current.Session.Tier : 0;
        LootTable regional = RaidDirector.Current != null && !smallFind ? RaidDirector.Current.Session.Definition.RegionalLoot : null;
        var items = Drops.Roll(tier, multiplier);
        if (regional != null)
        {
            regional.Drops.Roll(tier, multiplier, items);
        }

        foreach (ItemAmount drop in items)
        {
            if (RaidDirector.Current != null && (IsAboveThePlayer(drop.Item, GameSession.Home.SmithyTier) || !RaidDirector.Current.Session.Allows(drop.Item)))
            {
                continue;
            }

            contents.Add(drop);
        }

        Drops.RollResources(tier, multiplier, resourceContents);
        if (regional != null)
        {
            regional.Drops.RollResources(tier, multiplier, resourceContents);
        }
    }

    /// <summary>
    /// True when a cache holds this back from a player whose Smithy stands at <paramref name="smithyTier"/>: gear (equipment and tools)
    /// the Smithy is tiers away from making comes at most a tier ahead of it (and up to tier 2 before it stands). Materials, salvage,
    /// parts, trophies, relics and other finds aren't the forge's to hold back: the raid tier's own drop cap governs them
    /// (RaidDifficulty.dropsUpToTier, RaidSession.Allows), as it does for creatures' drops. The cap dates from 2026-11-02 and was meant
    /// for metal and gear; once the loot overhaul made its raid parts and junk tier 3 to 5 Materials, it kept a Barrow Chest's Barrow
    /// Goods and Grave Iron out until Copper Fire (review 2026-11-17, R6).
    /// </summary>
    internal static bool IsAboveThePlayer(ItemDefinition item, int smithyTier)
    {
        bool gear = item.Category == ItemCategory.Equipment || item.Category == ItemCategory.Tool;
        return gear && item.Tier > Mathf.Max(MostEarlyTier, smithyTier + 1);
    }

    private void ShowState()
    {
        bool empty = IsEmpty;
        if (fullVisual != null)
        {
            fullVisual.SetActive(!empty);
        }

        if (emptyVisual != null)
        {
            emptyVisual.SetActive(empty);
        }

        interactable.enabled = !empty;
    }
}

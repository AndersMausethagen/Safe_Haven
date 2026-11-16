using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A supply cache found in raids: a chest, a crate, a fallen traveller's pack. A chest is locked: the player has to pick it
/// first (PlayerLockpicking), which is heard, and so is its lid coming up. Searching it (its Interactable) rolls the loot
/// table once, scaled by the raid's loot modifier, and hands the player what fits: items to the bag, resources to the
/// resource bars. Whatever doesn't fit stays inside for another search; once empty it shows its empty look and can't be
/// searched again. The Interactable beside it must be Work, not Use, and must not deactivate on completion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[OnAFriendsCopy(CopyRule.Same, "personal loot: each game rolls its own")]
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid's cache, personal loot")]
public sealed class LootCache : MonoBehaviour, IInteractionVerb
{
    [SerializeField] private ItemDropTable drops = new ItemDropTable();
    [Tooltip("Pins in its lock, each a press of the lockpicking bar. 0: no lock, as on a fallen traveller's pack.")]
    [SerializeField, Range(0, 6)] private int lockPins = 3;
    [Tooltip("Shown while the cache still holds something, such as a closed lid. Optional.")]
    [SerializeField] private GameObject fullVisual;
    [Tooltip("Shown once the cache is empty, such as an open lid. Optional.")]
    [SerializeField] private GameObject emptyVisual;

    private const int MostEarlyTier = 2;

    private readonly List<ItemAmount> contents = new List<ItemAmount>();
    private readonly List<ResourceAmount> resourceContents = new List<ResourceAmount>();
    private bool holdsRelic;
    private bool holdsInscription;
    private Interactable interactable;
    private bool rolled;
    private bool unlocked;
    private bool forced;

    public ItemDropTable Drops => drops;
    public bool IsEmpty => rolled && contents.Count == 0 && resourceContents.Count == 0;
    public int LockPins => lockPins;

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

        if (holdsRelic)
        {
            holdsRelic = false;
            RaidDirector raid = RaidDirector.Current;
            RelicSettings.Grant(searcher, new System.Random(Random.Range(0, int.MaxValue)), raid.Session.Tier);
        }

        if (holdsInscription)
        {
            holdsInscription = false;
            int tier = RaidDirector.Current != null ? RaidDirector.Current.Session.Tier : 1;
            EnchantmentDefinition found = Inscriptions.Pick(Inscriptions.Pool(EnchantmentSource.Found, tier), searcher, new System.Random(Random.Range(0, int.MaxValue)));
            if (found != null)
            {
                Inscriptions.Grant(searcher, found, "a supply cache");
            }
        }

        if (contents.Count == 0 && resourceContents.Count == 0)
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
            int given = bag.Give(entry.Item, entry.Amount);
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
        float multiplier = RaidDirector.Current != null ? RaidDirector.Current.Parameters.LootMultiplier * RaidDirector.StageLootNow : 1f;
        if (RaidDirector.Current != null)
        {
            JournalNotices.RollForFind();
        }

        holdsRelic = RaidDirector.Current != null && Random.value < RelicSettings.Current.CacheChance * multiplier;
        holdsInscription = RaidDirector.Current != null && Random.value < EssenceSettings.Current.CacheInscriptionChance * multiplier;
        foreach (ItemAmount drop in drops.Roll())
        {
            if (RaidDirector.Current != null && (IsAboveThePlayer(drop.Item) || !RaidDirector.Current.Session.Allows(drop.Item)))
            {
                continue;
            }

            int amount = RaidParameters.ScaleAmount(drop.Amount, multiplier, Random.value);
            if (amount > 0)
            {
                contents.Add(new ItemAmount(drop.Item, amount));
            }
        }

        foreach (ResourceAmount drop in drops.RollResources())
        {
            int amount = RaidParameters.ScaleAmount(drop.Amount, multiplier, Random.value);
            if (amount > 0)
            {
                resourceContents.Add(new ResourceAmount(drop.Resource, amount));
            }
        }
    }

    // A cache can't hand over what the Smithy is tiers away from using: metal and gear come at most a tier ahead of it.
    private static bool IsAboveThePlayer(ItemDefinition item)
    {
        bool ladder = item.Category == ItemCategory.Material || item.Category == ItemCategory.Equipment || item.Category == ItemCategory.Tool;
        return ladder && item.Tier > Mathf.Max(MostEarlyTier, GameSession.Here.SmithyTier + 1);
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

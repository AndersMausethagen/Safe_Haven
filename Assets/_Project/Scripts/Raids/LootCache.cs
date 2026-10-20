using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A supply cache found in raids: a chest, a crate, a fallen traveller's pack. Searching it (its Interactable) rolls the
/// loot table once, scaled by the raid's loot modifier, and hands the player what fits: items to the bag, resources to the
/// resource bars. Whatever doesn't fit stays inside for another search; once empty it shows its empty look and can't be
/// searched again. The Interactable beside it must not deactivate on completion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class LootCache : MonoBehaviour
{
    [SerializeField] private ItemDropTable drops = new ItemDropTable();
    [Tooltip("Shown while the cache still holds something, such as a closed lid. Optional.")]
    [SerializeField] private GameObject fullVisual;
    [Tooltip("Shown once the cache is empty, such as an open lid. Optional.")]
    [SerializeField] private GameObject emptyVisual;

    private readonly List<ItemAmount> contents = new List<ItemAmount>();
    private readonly List<ResourceAmount> resourceContents = new List<ResourceAmount>();
    private Interactable interactable;
    private bool rolled;

    public ItemDropTable Drops => drops;
    public bool IsEmpty => rolled && contents.Count == 0 && resourceContents.Count == 0;

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

    private void Fill()
    {
        rolled = true;
        float multiplier = RaidDirector.Current != null ? RaidDirector.Current.Parameters.LootMultiplier : 1f;
        foreach (ItemAmount drop in drops.Roll())
        {
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

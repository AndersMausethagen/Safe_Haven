using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The storage window made quicker to use (Hearthline 3), as the bag was: the bag's grid shows its open slots and one
/// row of locked ones (what is under it comes up), each side gets a Sort button over its grid (ItemInventoryWindow.Arranged),
/// the bag a "Store all" that puts everything it carries into storage as far as it fits, and the headings their icons.
/// </summary>
public sealed partial class StashWindow
{
    private bool kitBuilt;
    private readonly List<RectTransform> belowBag = new List<RectTransform>();
    private float lifted;

    // Called by a grid after it shows its slots.
    private void Compacted(ItemContainer container, List<ItemSlotView> views, RectTransform root)
    {
        if (container == null || root != bagGrid)
        {
            return;
        }

        int columns = Mathf.Max(1, container.Columns);
        int open = Mathf.CeilToInt(container.UnlockedSlots / (float)columns) * columns;
        int shown = Mathf.Min(container.SlotCount, open + columns);
        int last = 0;
        for (int i = 0; i < views.Count; i++)
        {
            bool show = i < shown || !container.GetSlot(i).IsEmpty;
            if (views[i].gameObject.activeSelf != show)
            {
                views[i].gameObject.SetActive(show);
            }

            if (show)
            {
                last = i;
            }
        }

        var layout = bagGrid.GetComponent<GridLayoutGroup>();
        if (layout != null && kitBuilt)
        {
            int hiddenRows = Mathf.Max(0, Mathf.CeilToInt(container.SlotCount / (float)columns) - (last / columns + 1));
            float lift = hiddenRows * (layout.cellSize.y + layout.spacing.y);
            float by = lift - lifted;
            if (!Mathf.Approximately(by, 0f))
            {
                lifted = lift;
                foreach (RectTransform part in belowBag)
                {
                    part.anchoredPosition += new Vector2(0f, by);
                }
            }
        }
    }

    private void BuildKit()
    {
        if (kitBuilt)
        {
            return;
        }

        kitBuilt = true;
        // What sits under the bag's grid in its column: it comes up into the rows no longer shown.
        var corners = new Vector3[4];
        bagGrid.GetWorldCorners(corners);
        float bottom = corners[0].y, left = corners[0].x - 2f, right = corners[2].x + 2f;
        foreach (Transform child in bagGrid.parent)
        {
            if (child == bagGrid || !(child is RectTransform part) || part.anchorMin == Vector2.zero && part.anchorMax == Vector2.one)
            {
                continue;
            }

            var c = new Vector3[4];
            part.GetWorldCorners(c);
            if (c[1].y <= bottom + 1f && c[0].x >= left - 40f && c[0].x < right)
            {
                belowBag.Add(part);
            }
        }

        // Over each grid, at the right end of its first row (worked out from its cells, as the layout places them later).
        GridButton(bagGrid, bag, "Sort", "Stats", 104f, 0f, () => Sort(bag.Container));
        GridButton(bagGrid, bag, "Store all", "Lock", 132f, 112f, StoreAll);
        GridButton(stashGrid, stash, "Sort", "Stats", 104f, 0f, () => Sort(stash.Container, stash.First, stash.Count));
        // Last raid's draughts, food and arrows again, from the stash into the bag (PackForRaid).
        GridButton(stashGrid, stash, "Pack as last time", "Bags", 196f, 112f, () => PackForRaid.Pack(player));
        // Whether the Store key fills this chest (StashChest.SkipsStore).
        skipButton = GridButton(stashGrid, stash, "", "Lock", 176f, 316f, ToggleSkip);
        ShowSkip();

        foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
        {
            string text = label.text != null ? label.text.Trim() : "";
            // Not the window's own title (it says "Storage" too), nor what is on a button.
            if (label.name == "Title" || label.GetComponentInParent<Selectable>(true) != null || text.Contains("<sprite"))
            {
                continue;
            }

            if (text.StartsWith("Bag")) HearthKit.DecorateHeading(label, "Bags");
            else if (text.StartsWith("Storage")) HearthKit.DecorateHeading(label, "Lock");
            else if (text == "Resources") HearthKit.DecorateHeading(label, "Hammer");
        }
    }

    private KitButton skipButton;

    private void ToggleSkip()
    {
        if (chest == null)
        {
            return;
        }

        chest.SkipsStore = !chest.SkipsStore;
        ShowSkip();
        GameMessages.Post(chest.SkipsStore
            ? "The Store key passes this chest over now: it keeps only what you put in by hand."
            : "The Store key fills this chest again.", MessageKind.Info);
    }

    // Called when the window opens on a chest, and when the switch is pressed.
    private void ShowSkip()
    {
        if (skipButton == null || chest == null)
        {
            return;
        }

        skipButton.Bind(chest.SkipsStore ? new MenuAction("Store skips this", "The Store key leaves this chest alone. Press to let it fill.", true, null) { Icon = "Lock" }
            : new MenuAction("Store fills this", "The Store key puts things here. Press to keep this chest as it is.", true, null) { Icon = "Drop" });
    }

    private KitButton GridButton(RectTransform grid, Grid side, string name, string icon, float width, float fromRight, System.Action act)
    {
        int columns = side.Container != null ? Mathf.Max(1, side.Container.Columns) : 1;
        var layout = grid.GetComponent<GridLayoutGroup>();
        float cellsRight = layout != null ? layout.padding.left + columns * layout.cellSize.x + (columns - 1) * layout.spacing.x : grid.rect.width;
        var button = new KitButton(grid, 0f, 0f, width, 30f, _ => act(), KitButtonShape.Compact);
        button.Bind(new MenuAction(name, null, true, null) { Icon = icon });
        RectTransform rect = button.Rect;
        rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(cellsRight - fromRight, 6f);
        return button;
    }

    private static void Sort(ItemContainer container) => Sort(container, 0, container != null ? container.SlotCount : 0);

    // Sorts the open slots from first to first + count - 1 (a chest sorts only its own block).
    private static void Sort(ItemContainer container, int first, int count)
    {
        if (container == null)
        {
            return;
        }

        var stacks = new List<ItemStack>();
        var places = new List<int>();
        for (int i = first; i < Mathf.Min(container.SlotCount, first + count); i++)
        {
            if (!container.IsUnlocked(i))
            {
                continue;
            }

            places.Add(i);
            if (!container.GetSlot(i).IsEmpty)
            {
                stacks.Add(container.GetSlot(i));
            }
        }

        List<ItemStack> sorted = ItemInventoryWindow.Arranged(stacks);
        for (int i = 0; i < places.Count; i++)
        {
            container.SetSlot(places[i], i < sorted.Count ? sorted[i] : ItemStack.Empty);
        }
    }

    // Everything the bag carries into storage, as far as it fits.
    private void StoreAll()
    {
        ItemContainer from = bag.Container, to = stash.Container;
        if (from == null || to == null)
        {
            return;
        }

        int moved = 0;
        for (int i = 0; i < from.SlotCount; i++)
        {
            if (!from.GetSlot(i).IsEmpty)
            {
                moved += GameSession.Stash.Store(from, i, player, chest != null ? chest.Block : -1);
            }
        }

        if (moved == 0)
        {
            GameMessages.Post(from.CountItems() > 0 ? "This chest has no room for more." : "Nothing in the bag to store.", MessageKind.Info);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bag window made quicker to use (Hearthline 3, the user's bag and crafting picture): the grid shows only the slots
/// in use and one row of locked ones beyond (more locked rows said nothing but took half the window; a locked slot still
/// holding something always shows), and a Sort button by the Bag heading puts like with like (by kind, then the better
/// first, then by name) and puts identical part stacks together. Nothing else of the window's own behaviour changes.
/// </summary>
public sealed partial class ItemInventoryWindow
{
    private bool kitBuilt;
    private KitButton sortButton;
    private readonly List<RectTransform> below = new List<RectTransform>();
    private float lifted;

    // How many of the bag's slots show: those open, rounded up to a full row, and one row of locked ones beyond.
    private int ShownSlots(ItemContainer source)
    {
        int columns = Mathf.Max(1, source.Columns);
        int open = Mathf.CeilToInt(source.UnlockedSlots / (float)columns) * columns;
        return Mathf.Min(source.SlotCount, open + columns);
    }

    private void Compact(ItemContainer source, List<ItemSlotView> views)
    {
        if (source == null || source != container)
        {
            return;
        }

        int shown = ShownSlots(source);
        int last = 0;
        for (int i = 0; i < views.Count; i++)
        {
            bool show = i < shown || !source.GetSlot(i).IsEmpty;
            if (views[i].gameObject.activeSelf != show)
            {
                views[i].gameObject.SetActive(show);
            }

            if (show)
            {
                last = i;
            }
        }

        // What is under the grid (the hints, the safe pocket, the essences) comes up into the rows no longer shown.
        var layout = grid.GetComponent<GridLayoutGroup>();
        if (layout != null && kitBuilt)
        {
            int columns = Mathf.Max(1, source.Columns);
            int hiddenRows = Mathf.Max(0, Mathf.CeilToInt(source.SlotCount / (float)columns) - (last / columns + 1));
            float lift = hiddenRows * (layout.cellSize.y + layout.spacing.y);
            float by = lift - lifted;
            if (!Mathf.Approximately(by, 0f))
            {
                lifted = lift;
                foreach (RectTransform part in below)
                {
                    part.anchoredPosition += new Vector2(0f, by);
                }
            }
        }
    }

    private void BuildKit()
    {
        if (kitBuilt || grid == null)
        {
            return;
        }

        kitBuilt = true;
        RectTransform parent = (RectTransform)grid.parent;
        float scale = grid.lossyScale.x > 0f ? grid.lossyScale.x : 1f;
        var gridCorners = new Vector3[4];
        grid.GetWorldCorners(gridCorners);
        float gridBottom = gridCorners[0].y;
        foreach (Transform child in parent)
        {
            if (child == grid || !(child is RectTransform part))
            {
                continue;
            }

            var corners = new Vector3[4];
            part.GetWorldCorners(corners);
            if (corners[1].y <= gridBottom + 1f && part.anchorMin != Vector2.zero)
            {
                below.Add(part);
            }
        }

        // On the grid itself, outside its layout, just above the right end of its first row (worked out from the cells,
        // not from where they are drawn: the layout puts them in place only later), in the Bag heading's row.
        int columns = Mathf.Max(1, container.Columns);
        var layout = grid.GetComponent<GridLayoutGroup>();
        float cellsRight = layout != null ? layout.padding.left + columns * layout.cellSize.x + (columns - 1) * layout.spacing.x : grid.rect.width;
        sortButton = new KitButton(grid, 0f, 0f, 104f, 32f, _ => SortBag(), KitButtonShape.Compact);
        sortButton.Bind(new MenuAction("Sort", "Like with like", true, null) { Icon = "Stats" });
        RectTransform button = sortButton.Rect;
        button.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        button.anchorMin = button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(1f, 0f);
        button.anchoredPosition = new Vector2(cellsRight, 6f);
        Compact(container, slots);
    }

    /// <summary>Puts the open slots in order: by kind, the better first, then by name; identical part stacks joined.</summary>
    public void SortBag()
    {
        if (container == null)
        {
            return;
        }

        var stacks = new List<ItemStack>();
        var places = new List<int>();
        for (int i = 0; i < container.SlotCount; i++)
        {
            if (!container.IsUnlocked(i))
            {
                continue;
            }

            places.Add(i);
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty)
            {
                stacks.Add(stack);
            }
        }

        List<ItemStack> joined = Arranged(stacks);

        for (int i = 0; i < places.Count; i++)
        {
            container.SetSlot(places[i], i < joined.Count ? joined[i] : ItemStack.Empty);
        }

        UIMotion.Punch(grid, 0.04f, 0.25f);
    }

    /// <summary>Stacks put in order (by kind, the better first, then by name), identical part stacks (the same in
    /// everything but how many) joined up to full ones. What SortBag lays out.</summary>
    public static List<ItemStack> Arranged(IEnumerable<ItemStack> stacks)
    {
        var joined = new List<ItemStack>();
        foreach (ItemStack stack in stacks)
        {
            ItemStack rest = stack;
            for (int j = 0; j < joined.Count && !rest.IsEmpty; j++)
            {
                ItemStack other = joined[j];
                int room = other.Item.MaximumStack - other.Count;
                if (room > 0 && other.WithCount(1).Equals(rest.WithCount(1)))
                {
                    int moved = Mathf.Min(room, rest.Count);
                    joined[j] = other.WithCount(other.Count + moved);
                    rest = rest.Count - moved > 0 ? rest.WithCount(rest.Count - moved) : ItemStack.Empty;
                }
            }

            if (!rest.IsEmpty)
            {
                joined.Add(rest);
            }
        }

        joined.Sort((a, b) =>
        {
            int byKind = a.Item.Category.CompareTo(b.Item.Category);
            if (byKind != 0) return byKind;
            int byTier = b.Item.Tier.CompareTo(a.Item.Tier);
            if (byTier != 0) return byTier;
            int byName = string.CompareOrdinal(a.Item.DisplayName, b.Item.DisplayName);
            return byName != 0 ? byName : b.Count.CompareTo(a.Count);
        });
        return joined;
    }
}

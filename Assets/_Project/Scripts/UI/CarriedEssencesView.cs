using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The essences the player is carrying, in the bag window: icon, name, a thin fill line and amount/capacity for each one found
/// so far. Essences have no weight or slot, so they live here rather than in the grid. Built in code on enable; it follows the
/// player's ResourceStorage and only displays it.
/// </summary>
public sealed class CarriedEssencesView : MonoBehaviour
{
    private const int Columns = 3;
    private const float RowHeight = 28f;
    private const float ColumnGap = 10f;
    private const float IconSize = 20f;
    private const float AmountWidth = 48f;

    private sealed class Row
    {
        public ResourceDefinition Resource;
        public GameObject Root;
        public RectTransform Fill;
        public TMP_Text Amount;
    }

    [SerializeField] private ResourceCatalog catalog;

    private readonly List<Row> rows = new List<Row>();
    private readonly List<ResourceDefinition> ordered = new List<ResourceDefinition>();
    private ResourceStorage storage;
    private TMP_Text emptyNote;

    private void OnEnable()
    {
        ResourceInventory inventory = Player.Current != null ? Player.Current.Resources : null;
        if (inventory == null || catalog == null)
        {
            return;
        }

        storage = inventory.Storage;
        storage.Changed += OnChanged;
        storage.CapacitiesChanged += Refresh;
        storage.Discovered += OnDiscovered;
        Rebuild();
    }

    private void OnDisable()
    {
        if (storage != null)
        {
            storage.Changed -= OnChanged;
            storage.CapacitiesChanged -= Refresh;
            storage.Discovered -= OnDiscovered;
            storage = null;
        }
    }

    private void OnChanged(ResourceChange change) => Refresh();

    private void OnDiscovered(ResourceDefinition resource) => Rebuild();

    private void Rebuild()
    {
        foreach (Row row in rows)
        {
            Destroy(row.Root);
        }

        rows.Clear();
        catalog.GetOrdered(ordered);
        UIStyle style = UIStyle.Current;
        var area = (RectTransform)transform;
        float width = (area.rect.width - ColumnGap * (Columns - 1)) / Columns;
        foreach (ResourceDefinition resource in ordered)
        {
            if (storage.IsDiscovered(resource))
            {
                rows.Add(BuildRow(resource, rows.Count, width, style));
            }
        }

        if (emptyNote == null)
        {
            emptyNote = Label("Empty", area, 0f, 2f, area.rect.width, RowHeight, 14f, style.Muted);
            emptyNote.text = "None yet. Essences come from what you fight and gather in the woods.";
        }

        emptyNote.gameObject.SetActive(rows.Count == 0);
        Refresh();
    }

    private Row BuildRow(ResourceDefinition resource, int index, float width, UIStyle style)
    {
        float x = index % Columns * (width + ColumnGap);
        float y = index / Columns * RowHeight;
        RectTransform rect = At(resource.DisplayName, transform, x, y, width, RowHeight);
        var icon = At("Icon", rect, 0f, 2f, IconSize, IconSize).gameObject.AddComponent<Image>();
        icon.sprite = resource.Icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = resource.Icon != null;
        float nameLeft = IconSize + 5f;
        TMP_Text name = Label("Name", rect, nameLeft, 0f, width - nameLeft - AmountWidth, 22f, 14f, style.Text);
        name.text = resource.DisplayName;
        name.fontSizeMax = name.fontSize;
        name.fontSizeMin = 11f;
        name.enableAutoSizing = true;
        TMP_Text amount = Label("Amount", rect, width - AmountWidth, 0f, AmountWidth, 22f, 14f, style.Muted, TextAlignmentOptions.Right);
        RectTransform track = At("Track", rect, nameLeft, 22f, width - nameLeft, 2f);
        track.gameObject.AddComponent<Image>().color = style.Track;
        RectTransform fill = Stretch("Fill", track);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = resource.Color;
        fillImage.raycastTarget = false;
        return new Row { Resource = resource, Root = rect.gameObject, Fill = fill, Amount = amount };
    }

    private void Refresh()
    {
        if (storage == null)
        {
            return;
        }

        foreach (Row row in rows)
        {
            int amount = storage.GetAmount(row.Resource);
            int capacity = storage.GetCapacity(row.Resource);
            row.Amount.text = $"{amount}/{capacity}";
            row.Fill.anchorMax = new Vector2(capacity > 0 ? Mathf.Clamp01((float)amount / capacity) : 0f, 1f);
        }
    }
}

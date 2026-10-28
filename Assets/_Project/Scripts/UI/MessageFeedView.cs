using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Short messages near the bottom of the screen: what was gathered or looted ("+3 Oak Wood"), newly found resources, and
/// anything posted through GameMessages ("Needs a Tier II Pickaxe"). Each line fades out after a few seconds; the newest
/// is at the bottom. While the dialogue box is open the lines move up above it.
/// </summary>
public sealed class MessageFeedView : MonoBehaviour
{
    private struct Line
    {
        public TMP_Text Label;
        public float ShownAt;
    }

    [Tooltip("Text used as a template for each line. It is hidden itself.")]
    [SerializeField] private TMP_Text lineTemplate;
    [SerializeField, Min(1)] private int maxLines = 5;
    [SerializeField, Min(0.5f)] private float visibleSeconds = 3.5f;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.6f;
    [SerializeField] private Color infoColor = new Color(0.92f, 0.92f, 0.92f);
    [SerializeField] private Color gainColor = new Color(0.62f, 0.92f, 0.55f);
    [SerializeField] private Color warningColor = new Color(1f, 0.7f, 0.35f);
    [Tooltip("How far the lines move up while the dialogue box is open, so they don't cover what is said.")]
    [SerializeField, Min(0f)] private float liftWhileTalking = 230f;

    private readonly List<Line> lines = new List<Line>();
    private ResourceInventory resources;
    private ItemInventory items;
    private RectTransform area;
    private Vector2 restingPosition;

    private void Awake()
    {
        area = lineTemplate.transform.parent as RectTransform;
        if (area != null)
        {
            restingPosition = area.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        lineTemplate.gameObject.SetActive(false);
        GameMessages.Posted += Show;

        resources = Player.Current != null ? Player.Current.Resources : FindAnyObjectByType<ResourceInventory>();
        if (resources != null)
        {
            resources.Storage.Changed += OnResourceChanged;
            resources.Storage.Discovered += OnResourceDiscovered;
        }

        items = Player.Current != null ? Player.Current.Bag : FindAnyObjectByType<ItemInventory>();
        if (items != null)
        {
            items.Received += OnItemsReceived;
        }
    }

    private void OnDisable()
    {
        GameMessages.Posted -= Show;
        if (resources != null)
        {
            resources.Storage.Changed -= OnResourceChanged;
            resources.Storage.Discovered -= OnResourceDiscovered;
        }

        if (items != null)
        {
            items.Received -= OnItemsReceived;
        }
    }

    private void Update()
    {
        if (area != null)
        {
            area.anchoredPosition = restingPosition + (DialogueBox.IsOpen ? Vector2.up * liftWhileTalking : Vector2.zero);
        }

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - lines[i].ShownAt;
            float alpha = 1f - Mathf.Clamp01((age - visibleSeconds) / fadeSeconds);
            if (alpha <= 0f)
            {
                Destroy(lines[i].Label.gameObject);
                lines.RemoveAt(i);
                continue;
            }

            Color color = lines[i].Label.color;
            color.a = alpha;
            lines[i].Label.color = color;
        }
    }

    public void Show(string text, MessageKind kind)
    {
        if (lines.Count >= maxLines)
        {
            Destroy(lines[0].Label.gameObject);
            lines.RemoveAt(0);
        }

        TMP_Text label = Instantiate(lineTemplate, lineTemplate.transform.parent);
        label.gameObject.SetActive(true);
        label.text = text;
        label.color = kind == MessageKind.Gain ? gainColor : kind == MessageKind.Warning ? warningColor : infoColor;
        label.transform.SetAsLastSibling();
        lines.Add(new Line { Label = label, ShownAt = Time.unscaledTime });
    }

    private void OnResourceChanged(ResourceChange change)
    {
        int gained = change.Amount - change.PreviousAmount;
        if (gained > 0)
        {
            Show($"+{gained} {change.Resource.DisplayName}", MessageKind.Gain);
        }
    }

    private void OnResourceDiscovered(ResourceDefinition resource)
    {
        Show($"New resource found: {resource.DisplayName} (Tier {Tiers.ToRoman(resource.Tier)})", MessageKind.Info);
    }

    // Item names show in their rarity's color; rarities set to announce get an extra line so a big find stands out.
    private void OnItemsReceived(ItemDefinition item, int amount)
    {
        Show($"+{amount} {Rarity.RichName(item)}", MessageKind.Gain);
        RarityStyle style = Rarity.StyleOf(item.Rarity);
        if (style.Announce)
        {
            Show($"<b>{Rarity.Rich(style.Label.ToUpperInvariant() + " FIND!", item.Rarity)}</b>  {Rarity.RichName(item)}", MessageKind.Gain);
        }
    }
}

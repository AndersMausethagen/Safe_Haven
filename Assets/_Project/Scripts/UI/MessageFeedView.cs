using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Short messages. One feed shows the player's pickups ("+3 Oak Wood", newly found resources) following them on screen, so the eye
/// stays on the character; another rests apart with anything longer posted through GameMessages ("Needs a Tier II Pickaxe"), so
/// that sentences don't cover the fight. Without a player in view a following feed rests where the prefab put it. Each line fades out after a few seconds; the newest is at
/// the bottom. While the dialogue box is open resting lines move up above it.
/// </summary>
public sealed class MessageFeedView : MonoBehaviour
{
    private struct Line
    {
        public TMP_Text Label;
        public float ShownAt;
        // The message as posted, and how many times it came while still on screen ("×2").
        public string Text;
        public int Times;
    }

    [Tooltip("Text used as a template for each line. It is hidden itself.")]
    [SerializeField] private TMP_Text lineTemplate;
    [Tooltip("Show what is posted through GameMessages: warnings, what the raid is doing, who said what.")]
    [SerializeField] private bool showPosted = true;
    [Tooltip("Show what the player picks up.")]
    [SerializeField] private bool showPickups = true;
    [SerializeField, Min(1)] private int maxLines = 5;
    [SerializeField, Min(0.5f)] private float visibleSeconds = 3.5f;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.6f;
    [SerializeField] private Color infoColor = new Color(0.92f, 0.92f, 0.92f);
    [SerializeField] private Color gainColor = new Color(0.62f, 0.92f, 0.55f);
    [SerializeField] private Color warningColor = new Color(1f, 0.7f, 0.35f);
    [Tooltip("How far the lines move up while the dialogue box is open, so they don't cover what is said.")]
    [SerializeField, Min(0f)] private float liftWhileTalking = 230f;
    [Tooltip("Keep the lines beside the player on screen rather than at their resting place.")]
    [SerializeField] private bool followPlayer = true;
    [Tooltip("The point on the player the lines start from, in metres above their feet.")]
    [SerializeField, Min(0f)] private float followHeight = 1.1f;
    [Tooltip("Where the lines' lower left corner sits from that point, in canvas units: to the right of the character.")]
    [SerializeField] private Vector2 followOffset = new Vector2(70f, -10f);
    [Tooltip("How quickly the lines catch up with the player on screen.")]
    [SerializeField, Min(0.1f)] private float followSharpness = 14f;

    private readonly List<Line> lines = new List<Line>();
    private ResourceInventory resources;
    private ItemInventory items;
    private RectTransform area;
    private RectTransform areaParent;
    private Canvas canvas;
    private Vector2 restingPosition;
    private Vector2 restingPivot;
    private TextAnchor restingAlignment;
    private bool following;

    private void Awake()
    {
        area = lineTemplate.transform.parent as RectTransform;
        if (area != null)
        {
            restingPosition = area.anchoredPosition;
            restingPivot = area.pivot;
            restingAlignment = area.TryGetComponent(out VerticalLayoutGroup layout) ? layout.childAlignment : TextAnchor.LowerRight;
            areaParent = area.parent as RectTransform;
            canvas = area.GetComponentInParent<Canvas>();
        }
    }

    private void OnEnable()
    {
        lineTemplate.gameObject.SetActive(false);
        if (showPosted)
        {
            GameMessages.Posted += Show;
        }

        // The player's own pickups, from the player now or once they arrive.
        Player.Spawned += Bind;
        Bind(Player.Current);
    }

    private void OnDisable()
    {
        GameMessages.Posted -= Show;
        Player.Spawned -= Bind;
        Unbind();
    }

    private void Bind(Player player)
    {
        if (!showPickups)
        {
            return;
        }

        if (player != null && player.Resources == resources && player.Bag == items)
        {
            return;
        }

        Unbind();
        if (player == null)
        {
            return;
        }

        resources = player.Resources;
        if (resources != null)
        {
            resources.Storage.Changed += OnResourceChanged;
            resources.Storage.Discovered += OnResourceDiscovered;
        }

        items = player.Bag;
        if (items != null)
        {
            items.Received += OnItemsReceived;
        }
    }

    private void Unbind()
    {
        if (resources != null)
        {
            resources.Storage.Changed -= OnResourceChanged;
            resources.Storage.Discovered -= OnResourceDiscovered;
        }

        if (items != null)
        {
            items.Received -= OnItemsReceived;
        }

        resources = null;
        items = null;
    }

    private void Update()
    {
        PlaceArea();

        // While a window is open the lines wait, hidden, their time held: they drew over the windows (the systems review, 2026-11-21).
        bool covered = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (covered)
            {
                Line held = lines[i];
                held.ShownAt += Time.unscaledDeltaTime;
                lines[i] = held;
                Color hidden = lines[i].Label.color;
                hidden.a = 0f;
                lines[i].Label.color = hidden;
                continue;
            }

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
        // The same words again while they are still up: one line, counted. Two wolves howling wrote the same sentence twice, one
        // above the other (the HUD review of 2026-11-18).
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].Text == text && lines[i].Label != null)
            {
                Line again = lines[i];
                again.Times++;
                again.ShownAt = Time.unscaledTime;
                again.Label.text = $"{text}  ×{again.Times}";
                again.Label.transform.SetAsLastSibling();
                lines.RemoveAt(i);
                lines.Add(again);
                return;
            }
        }

        if (lines.Count >= maxLines)
        {
            Destroy(lines[0].Label.gameObject);
            lines.RemoveAt(0);
        }

        TMP_Text label = Instantiate(lineTemplate, lineTemplate.transform.parent);
        label.gameObject.SetActive(true);
        label.text = text;
        label.alignment = following ? TextAlignmentOptions.Left : lineTemplate.alignment;
        label.color = kind == MessageKind.Gain ? gainColor : kind == MessageKind.Warning ? warningColor : infoColor;
        label.transform.SetAsLastSibling();
        lines.Add(new Line { Label = label, ShownAt = Time.unscaledTime, Text = text, Times = 1 });
    }

    // Beside the player while they are on screen; at the resting place (lifted above an open dialogue box) otherwise.
    private void PlaceArea()
    {
        if (area == null)
        {
            return;
        }

        Vector2 target = default;
        bool follow = followPlayer && TryPlayerPoint(out target);
        if (follow != following)
        {
            SetFollowing(follow);
            if (follow)
            {
                area.anchoredPosition = target;
            }
        }

        if (!follow)
        {
            area.anchoredPosition = restingPosition + (DialogueBox.IsOpen ? Vector2.up * liftWhileTalking : Vector2.zero);
            return;
        }

        float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        area.anchoredPosition = Vector2.Lerp(area.anchoredPosition, target, blend);
    }

    // The anchored position that puts the lines' lower left corner beside the player, kept inside the screen.
    private bool TryPlayerPoint(out Vector2 anchored)
    {
        anchored = default;
        Player player = Player.Current;
        Camera view = Camera.main;
        if (player == null || view == null || areaParent == null || canvas == null)
        {
            return false;
        }

        Vector3 screen = view.WorldToScreenPoint(player.transform.position + Vector3.up * followHeight);
        if (screen.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
        {
            return false;
        }

        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(areaParent, screen, canvasCamera, out Vector2 local))
        {
            return false;
        }

        Rect bounds = areaParent.rect;
        Vector2 size = area.rect.size;
        local += followOffset;
        local.x = Mathf.Clamp(local.x, bounds.xMin, bounds.xMax - size.x);
        local.y = Mathf.Clamp(local.y, bounds.yMin, bounds.yMax - size.y);
        Vector2 anchor = bounds.min + Vector2.Scale(bounds.size, (area.anchorMin + area.anchorMax) * 0.5f);
        anchored = local - anchor;
        return true;
    }

    private void SetFollowing(bool follow)
    {
        following = follow;
        area.pivot = follow ? Vector2.zero : restingPivot;
        if (area.TryGetComponent(out VerticalLayoutGroup layout))
        {
            layout.childAlignment = follow ? TextAnchor.LowerLeft : restingAlignment;
        }

        foreach (Line line in lines)
        {
            line.Label.alignment = follow ? TextAlignmentOptions.Left : lineTemplate.alignment;
        }
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

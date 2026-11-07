using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What was just made, shown where the eye is: a card rising above the experience bar with the item's icon and name in the
/// colour of its make (Fine in the Rare colour, Masterwork in the Epic one), the experience it taught, and for good work its
/// own sound and a burst of sparks. Crafts of the same thing close together add up ("x5"); an order queued at a station
/// shows as queued. Built in code for every scene with a player (ExperienceTracker); it listens to CraftedWork.
/// </summary>
public sealed class CraftFeedback : MonoBehaviour
{
    private const float Hold = 2.2f;
    private const float GoodHold = 3.6f;
    private const float Merge = 1.6f;

    private RectTransform card;
    private CanvasGroup group;
    private Image icon;
    private Image edge;
    private TMP_Text title;
    private TMP_Text line;
    private ItemDefinition shownItem;
    private ItemQuality shownQuality;
    private int shownCount;
    private bool shownQueued;
    private float shownAt = -100f;
    private float holdFor;
    private int madeFrame = -1;

    public static CraftFeedback Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public static CraftFeedback Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Craft Feedback");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        var view = root.AddComponent<CraftFeedback>();
        view.Build(root.transform);
        return view;
    }

    private void OnEnable() => Current = this;

    private void OnDestroy()
    {
        CraftedWork.Made -= OnMade;
        ExperienceTracker.Gained -= OnGained;
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        card = CodeUI.Box("Card", root, new Vector2(1f, 0f), new Vector2(460f, 84f), new Vector2(-266f, 470f));
        card.gameObject.AddComponent<Image>().color = style.Panel;
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        edge = CodeUI.At("Edge", card, 0f, 0f, 4f, 84f).gameObject.AddComponent<Image>();
        edge.raycastTarget = false;
        icon = CodeUI.At("Icon", card, 16f, 10f, 64f, 64f).gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        title = CodeUI.Label("Title", card, 94f, 12f, 350f, 32f, 22f, style.Text);
        title.richText = true;
        line = CodeUI.Label("Line", card, 94f, 46f, 350f, 26f, 17f, style.Muted);
        line.richText = true;
        CraftedWork.Made += OnMade;
        ExperienceTracker.Gained += OnGained;
    }

    /// <summary>An order queued at a station, shown the same way (no sparks: it isn't made yet).</summary>
    public static void ShowQueued(ItemDefinition item, int count)
    {
        if (Current != null && item != null && count > 0)
        {
            Current.Show(item, count, ItemQuality.Plain, item.DisplayName, true);
        }
    }

    private void OnMade(ItemDefinition item, int count, ItemQuality quality, string name) => Show(item, count, quality, name, false);

    private void Show(ItemDefinition item, int count, ItemQuality quality, string name, bool queued)
    {
        bool same = item == shownItem && quality == shownQuality && queued == shownQueued && Time.unscaledTime - shownAt < Merge;
        shownCount = same ? shownCount + count : count;
        shownItem = item;
        shownQuality = quality;
        shownQueued = queued;
        shownAt = Time.unscaledTime;
        madeFrame = Time.frameCount;
        holdFor = quality == ItemQuality.Plain ? Hold : GoodHold;

        UIStyle style = UIStyle.Current;
        Color colour = quality == ItemQuality.Masterwork ? Rarity.ColorOf(ItemRarity.Epic)
            : quality == ItemQuality.Fine ? Rarity.ColorOf(ItemRarity.Rare)
            : style.Accent;
        icon.sprite = item.Icon;
        icon.enabled = item.Icon != null;
        edge.color = colour;
        string times = shownCount > 1 ? $"  <color={style.MutedHex}>x{shownCount}</color>" : "";
        title.text = $"<color={UIStyle.Hex(colour)}>{name}</color>{times}";
        line.text = queued ? "Queued at the station" : quality == ItemQuality.Masterwork ? "Masterwork: your finest work"
            : quality == ItemQuality.Fine ? "Fine work" : "Made";

        if (!same || quality != ItemQuality.Plain)
        {
            group.alpha = 1f;
            UIMotion.Pop(card, 0.7f, 0.35f);
            UIMotion.Punch(icon.transform, 0.25f, 0.45f);
        }
        else
        {
            UIMotion.Punch(card, 0.05f, 0.25f);
        }

        if (quality != ItemQuality.Plain)
        {
            UIMotion.Flash(title, Color.white, 0.6f);
            UIMotion.Burst(card, new Vector2(-182f, 0f), quality == ItemQuality.Masterwork ? 22 : 12, colour,
                quality == ItemQuality.Masterwork ? 260f : 170f, 1.1f, quality == ItemQuality.Masterwork ? 12f : 9f);
            AudioSettingsDefinition sounds = AudioManager.Sounds;
            UIMotion.Sound(sounds == null ? null : quality == ItemQuality.Masterwork ? sounds.CraftMasterwork : sounds.CraftFine);
        }
        else if (queued)
        {
            UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.CraftQueued : null);
        }
    }

    // The experience a craft taught comes in the same frame it was made: it goes on the card.
    private void OnGained(ProficiencyDefinition skill, int amount)
    {
        if (skill != null && Time.frameCount == madeFrame && !shownQueued)
        {
            line.text += $"   <color={UIStyle.Current.GoodHex}>+{amount} {skill.DisplayName}</color>";
        }
    }

    private void LateUpdate()
    {
        // In the right-hand column above the minimap, out of the way of the world; at the top of the screen while a
        // station's window fills the middle.
        bool windows = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        Vector2 anchor = windows ? new Vector2(0.5f, 1f) : new Vector2(1f, 0f);
        if (card.anchorMin != anchor)
        {
            card.anchorMin = card.anchorMax = anchor;
            card.anchoredPosition = windows ? new Vector2(0f, -70f) : new Vector2(-266f, 470f);
        }

        float age = Time.unscaledTime - shownAt;
        if (age > holdFor)
        {
            group.alpha = Mathf.Max(0f, 1f - (age - holdFor) / 0.5f);
        }
    }
}

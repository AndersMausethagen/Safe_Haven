using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Hold Alt to issue one shared order to the companions, use a carried consumable, or (hovering Emotes) make a
/// gesture.</summary>
[DisallowMultipleComponent]
public sealed class CommandWheel : MonoBehaviour
{
    private const float WheelRadius = 150f;
    private const float SlotSize = 88f;
    private const float CentreDeadZone = 38f;
    private static readonly CompanionOrder[] Orders =
    {
        CompanionOrder.Follow,
        CompanionOrder.Stay,
        CompanionOrder.Defend,
        CompanionOrder.Retreat,
        CompanionOrder.AttackTarget,
    };

    private sealed class Choice
    {
        public readonly string Label;
        public readonly Action Select;
        /// <summary>Hovered for a moment, it turns the wheel to another page instead of being chosen on release.</summary>
        public readonly Action Opens;

        public Choice(string label, Action select, Action opens = null)
        {
            Label = label;
            Select = select;
            Opens = opens;
        }
    }

    private const float HoverToOpen = 0.3f;
    private const int MaxChoices = 8;

    private readonly List<Choice> choices = new List<Choice>();
    private readonly List<Image> slots = new List<Image>();
    private readonly List<TMP_Text> labels = new List<TMP_Text>();
    private GameObject root;
    private CanvasGroup group;
    private TMP_Text centreLabel;
    private Sprite circle;
    private int selected = -1;
    private float hoveredSince;
    private string title = "Choose";

    public bool IsOpen => group != null && group.alpha > 0f;

    /// <summary>True while any wheel is held open: the camera lets go of the cursor, so the mouse can pick a choice.</summary>
    public static bool IsHeld { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsHeld = false;

    private void Awake()
    {
        BuildView();
    }

    private void OnDisable() => Cancel();

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root);
        }

        if (circle != null)
        {
            Destroy(circle.texture);
            Destroy(circle);
        }
    }

    public void Open()
    {
        if (GamePause.IsPaused || BuildPlacer.IsBusy || DialogueBox.IsSpeaking ||
            (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen))
        {
            return;
        }

        BuildChoices();
        ShowPage("Choose");
    }

    // Lays out the choices just built as a fresh page of the open wheel.
    private void ShowPage(string pageTitle)
    {
        if (choices.Count == 0)
        {
            Cancel();
            return;
        }

        EnsureSlots();
        selected = -1;
        title = pageTitle;
        root.SetActive(true);
        group.alpha = 1f;
        IsHeld = true;
        centreLabel.text = title;
        UpdateSelection();
    }

    public void CloseAndExecute()
    {
        Choice choice = selected >= 0 && selected < choices.Count ? choices[selected] : null;
        Cancel();
        choice?.Select?.Invoke();
    }

    public void Cancel()
    {
        IsHeld = false;
        if (group != null)
        {
            group.alpha = 0f;
            root.SetActive(false);
        }

        choices.Clear();
        selected = -1;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (GamePause.IsPaused || BuildPlacer.IsBusy ||
            (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen))
        {
            Cancel();
            return;
        }

        UpdateSelection();
        if (selected >= 0 && choices[selected].Opens != null && Time.unscaledTime - hoveredSince >= HoverToOpen)
        {
            choices[selected].Opens();
        }
    }

    private void BuildChoices()
    {
        choices.Clear();
        var companions = new List<Companion>();
        foreach (Companion companion in Companion.Active)
        {
            if (companion != null && companion.isActiveAndEnabled && !companion.IsDowned && companion.Definition != null)
            {
                companions.Add(companion);
            }
        }

        foreach (CompanionOrder order in Orders)
        {
            bool available = companions.Count > 0;
            foreach (Companion companion in companions)
            {
                if (!companion.Definition.Takes(order))
                {
                    available = false;
                    break;
                }
            }

            if (available)
            {
                CompanionOrder chosen = order;
                choices.Add(new Choice(OrderLabel(chosen), () =>
                {
                    foreach (Companion companion in companions)
                    {
                        companion.Command(chosen);
                    }
                }));
            }
        }

        Player player = Player.Current;
        if (player != null && player.TryGetComponent(out CharacterEmotes emotes) && WheelEmotes().Count > 0)
        {
            choices.Add(new Choice("Emotes", null, () => OpenEmotes(emotes)));
        }

        ItemInventory inventory = player != null ? player.Bag : null;
        if (inventory == null)
        {
            return;
        }

        AddConsumables(inventory, inventory.Container);
        AddConsumables(inventory, inventory.SafePocket);
    }

    private static List<EmoteDefinition> WheelEmotes()
    {
        var onWheel = new List<EmoteDefinition>();
        foreach (EmoteDefinition emote in CharacterCatalog.Current.Emotes)
        {
            if (emote != null && emote.OnWheel && onWheel.Count < MaxChoices)
            {
                onWheel.Add(emote);
            }
        }

        return onWheel;
    }

    private void OpenEmotes(CharacterEmotes emotes)
    {
        choices.Clear();
        foreach (EmoteDefinition emote in WheelEmotes())
        {
            EmoteDefinition chosen = emote;
            choices.Add(new Choice(chosen.DisplayName, () => emotes.Play(chosen)));
        }

        ShowPage("Emotes");
    }

    private void AddConsumables(ItemInventory inventory, ItemContainer container)
    {
        if (container == null)
        {
            return;
        }

        for (int i = 0; i < container.UnlockedSlots && choices.Count < MaxChoices; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (stack.IsEmpty || stack.Item.Category != ItemCategory.Consumable || !stack.Item.IsConsumable)
            {
                continue;
            }

            int slot = i;
            ItemContainer source = container;
            choices.Add(new Choice($"{stack.Item.DisplayName} x{stack.Count}", () => inventory.Use(source, slot)));
        }
    }

    private void BuildView()
    {
        root = new GameObject("Command Wheel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 420;
        UIRoot.ConfigureScaler(root.GetComponent<CanvasScaler>());
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        circle = MakeCircle();

        RectTransform backdrop = Rect("Backdrop", root.transform, WheelRadius * 2f + SlotSize, WheelRadius * 2f + SlotSize, Vector2.zero);
        Image backing = backdrop.gameObject.AddComponent<Image>();
        backing.sprite = circle;
        backing.color = new Color(0.06f, 0.05f, 0.04f, 0.88f);
        backing.raycastTarget = false;

        RectTransform centre = Rect("Selection", root.transform, 180f, 58f, Vector2.zero);
        centreLabel = centre.gameObject.AddComponent<TextMeshProUGUI>();
        centreLabel.fontSize = 20f;
        centreLabel.enableAutoSizing = true;
        centreLabel.fontSizeMin = 12f;
        centreLabel.fontSizeMax = 20f;
        centreLabel.alignment = TextAlignmentOptions.Center;
        centreLabel.color = UIStyle.Current.Text;
        centreLabel.raycastTarget = false;
        root.SetActive(false);
    }

    private void EnsureSlots()
    {
        UIStyle style = UIStyle.Current;
        while (slots.Count < choices.Count)
        {
            RectTransform rect = Rect("Choice", root.transform, SlotSize, SlotSize, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = circle;
            image.raycastTarget = false;
            // On a child: an object can only hold one graphic, and adding a second gives null.
            RectTransform label = Rect("Label", rect, SlotSize - 8f, SlotSize - 8f, Vector2.zero);
            TMP_Text text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 16f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = style.Text;
            text.raycastTarget = false;
            slots.Add(image);
            labels.Add(text);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool used = i < choices.Count;
            slots[i].gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }

            float angle = i * Mathf.PI * 2f / choices.Count;
            RectTransform rect = (RectTransform)slots[i].transform;
            rect.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * WheelRadius;
            labels[i].text = choices[i].Label;
        }
    }

    private void UpdateSelection()
    {
        RectTransform canvasRect = (RectTransform)root.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, GameInput.PointerPosition, null, out Vector2 pointer))
        {
            return;
        }

        int previous = selected;
        selected = -1;
        if (pointer.magnitude >= CentreDeadZone)
        {
            float angle = Mathf.Atan2(pointer.x, pointer.y) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            selected = Mathf.RoundToInt(angle / 360f * choices.Count) % choices.Count;
        }

        if (selected != previous)
        {
            hoveredSince = Time.unscaledTime;
        }

        UIStyle style = UIStyle.Current;
        for (int i = 0; i < choices.Count; i++)
        {
            bool active = i == selected;
            slots[i].color = active ? style.Accent : style.Control;
            labels[i].color = active ? style.Panel : style.Text;
            slots[i].transform.localScale = Vector3.one * (active ? 1.12f : 1f);
        }

        centreLabel.text = selected >= 0 ? choices[selected].Label : title;
    }

    private static string OrderLabel(CompanionOrder order)
    {
        switch (order)
        {
            case CompanionOrder.Follow: return "Follow";
            case CompanionOrder.Stay: return "Stay";
            case CompanionOrder.Defend: return "Defend";
            case CompanionOrder.Retreat: return "Retreat";
            case CompanionOrder.AttackTarget: return "Attack target";
            default: return order.ToString();
        }
    }

    private static RectTransform Rect(string name, Transform parent, float width, float height, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = position;
        return rect;
    }

    private static Sprite MakeCircle()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Command Wheel Circle",
            hideFlags = HideFlags.DontSave,
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - point.magnitude) * size * 0.5f)));
            }
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f);
    }
}

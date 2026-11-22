using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Keeper's view's orders, as a slim bar along the bottom (the way Manor Lords, Frostpunk and Gord keep their commands: icons in
/// groups, a word under each, the sentence on hover). Each command is an icon button that lights while it is under way (a ring being
/// drawn, the horn sounding), goes grey with the reason in its tooltip when it can't be given, and can carry a count (rings drawn,
/// posts set). Groups: the Haven's work (felling, quarrying), its defence (posts, the horn) and the Haven itself (build, its window,
/// back to one's feet). A plain helper; KeeperHud owns it and refreshes it a few times a second.
/// </summary>
public sealed class KeeperCommandBar
{
    private const float ButtonWidth = 78f;
    private const float ButtonHeight = 78f;
    private const float Gap = 6f;
    private const float GroupGap = 22f;
    private const float Pad = 14f;
    private const float HeadingHeight = 20f;

    /// <summary>One order: what it looks like, whether it can be given (null) or why not, whether it is under way, its count, and what
    /// it does.</summary>
    public sealed class Command
    {
        public string Icon;
        public string Caption;
        public Func<string> Title;
        public Func<string> Why = () => null;
        public Func<bool> Lit = () => false;
        public Func<int> Count = () => 0;
        public Func<bool> Shown = () => true;
        public Func<string> Words = () => "";
        public Action Run;
    }

    private sealed class Slot
    {
        public Command Command;
        public RectTransform Rect;
        public Button Button;
        public Image Fill;
        public Image Rim;
        public Image Icon;
        public TMP_Text Caption;
        public RectTransform Badge;
        public TMP_Text BadgeLabel;
        public string Why = "-";
        public bool Lit;
        public int Count = -1;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly KeeperTip tip;
    private Slot pointed;
    // The order lit with a controller's d-pad (-1: none); A gives it.
    private int focused = -1;

    /// <summary>True while an order is lit with the d-pad.</summary>
    public bool HasFocus => focused >= 0;

    /// <summary>The bar itself (for the key line above it and the overlap audit).</summary>
    public RectTransform Root { get; }

    /// <summary>Builds the bar at the bottom of <paramref name="canvas"/> with <paramref name="groups"/> of commands, each under its title.</summary>
    public KeeperCommandBar(RectTransform canvas, KeeperTip tip, IReadOnlyList<(string title, Command[] commands)> groups)
    {
        UIStyle style = UIStyle.Current;
        this.tip = tip;
        int buttons = 0;
        foreach ((string _, Command[] commands) in groups)
        {
            buttons += commands.Length;
        }

        float width = Pad * 2f + buttons * ButtonWidth + (buttons - groups.Count) * Gap + (groups.Count - 1) * GroupGap;
        float height = Pad + HeadingHeight + ButtonHeight + Pad;
        Root = HearthKit.Card(canvas, "Keeper Commands", 0f, 0f, width, height, 0.55f, style.Panel);
        Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0f);
        Root.pivot = new Vector2(0.5f, 0f);
        Root.anchoredPosition = new Vector2(0f, 20f);
        // A gilded corner at each end, smaller than a window's.
        HearthKit.Corners(Root, 34f);

        float x = Pad;
        for (int g = 0; g < groups.Count; g++)
        {
            (string title, Command[] commands) = groups[g];
            float groupWidth = commands.Length * ButtonWidth + (commands.Length - 1) * Gap;
            TMP_Text heading = CodeUI.Label("Group", Root, x, Pad - 4f, groupWidth, HeadingHeight, 13f, style.Muted, TextAlignmentOptions.Center);
            heading.text = title.ToUpperInvariant();
            heading.characterSpacing = 8f;
            heading.overflowMode = TextOverflowModes.Overflow;
            if (g > 0)
            {
                // A thin gold line between the groups.
                Image rule = CodeUI.At("Divider", Root, x - GroupGap * 0.5f - 1f, Pad + 6f, 2f, HeadingHeight + ButtonHeight - 12f).gameObject.AddComponent<Image>();
                rule.color = HearthKit.Faded(style.Accent, 0.3f);
                rule.raycastTarget = false;
            }

            foreach (Command command in commands)
            {
                slots.Add(MakeSlot(command, x, Pad + HeadingHeight));
                x += ButtonWidth + Gap;
            }

            x += GroupGap - Gap;
        }

        Refresh(true);
    }

    private Slot MakeSlot(Command command, float x, float y)
    {
        UIStyle style = UIStyle.Current;
        var slot = new Slot { Command = command };
        slot.Rect = CodeUI.At(command.Caption, Root, x, y, ButtonWidth, ButtonHeight);
        slot.Fill = HearthKit.Sliced(slot.Rect.gameObject.AddComponent<Image>(), style.Card, 1.4f);
        slot.Rim = HearthKit.Rim(slot.Rect, 0.5f, 1.4f);
        slot.Button = slot.Rect.gameObject.AddComponent<Button>();
        slot.Button.targetGraphic = slot.Fill;
        HearthKit.Tint(slot.Button);
        UIHighlight.On(slot.Rect.gameObject);
        slot.Button.onClick.AddListener(() =>
        {
            if (slot.Why == null)
            {
                command.Run?.Invoke();
                Refresh(true);
            }
        });
        var hover = slot.Rect.gameObject.AddComponent<UIHover>();
        hover.Entered = () =>
        {
            pointed = slot;
            ShowTip(slot);
        };
        hover.Exited = () =>
        {
            if (pointed == slot)
            {
                pointed = null;
                tip.Hide();
            }
        };

        slot.Icon = HearthKit.Icon(slot.Rect, command.Icon, (ButtonWidth - 38f) * 0.5f, 10f, 38f);
        slot.Caption = CodeUI.Label("Caption", slot.Rect, 2f, ButtonHeight - 27f, ButtonWidth - 4f, 22f, 14f, style.Text, TextAlignmentOptions.Center);
        slot.Caption.overflowMode = TextOverflowModes.Overflow;
        slot.Caption.text = command.Caption;

        // A count in the corner (rings drawn, posts set).
        slot.Badge = CodeUI.At("Badge", slot.Rect, ButtonWidth - 30f, -6f, 30f, 22f);
        Image plate = HearthKit.Sliced(slot.Badge.gameObject.AddComponent<Image>(), style.Card, 2f);
        plate.color = style.ControlOn;
        plate.raycastTarget = false;
        HearthKit.Rim(slot.Badge, 0.8f, 2f);
        slot.BadgeLabel = CodeUI.Label("Count", slot.Badge, 0f, 0f, 30f, 22f, 14f, style.AccentBright, TextAlignmentOptions.Center);
        slot.BadgeLabel.fontStyle = FontStyles.Bold;
        slot.BadgeLabel.overflowMode = TextOverflowModes.Overflow;
        slot.Badge.gameObject.SetActive(false);
        return slot;
    }

    /// <summary>Each command's look from its state, written again only when the state changed (<paramref name="all"/>: every one).</summary>
    public void Refresh(bool all = false)
    {
        UIStyle style = UIStyle.Current;
        foreach (Slot slot in slots)
        {
            Command command = slot.Command;
            bool shown = command.Shown();
            if (slot.Rect.gameObject.activeSelf != shown)
            {
                slot.Rect.gameObject.SetActive(shown);
            }

            string why = command.Why();
            bool lit = why == null && command.Lit();
            int count = command.Count();
            if (!all && why == slot.Why && lit == slot.Lit && count == slot.Count)
            {
                continue;
            }

            slot.Why = why;
            slot.Lit = lit;
            slot.Count = count;
            bool can = why == null;
            bool focus = focused >= 0 && focused < slots.Count && slots[focused] == slot;
            slot.Fill.color = !can ? style.Disabled : lit ? style.ControlOn : focus ? style.ControlLit : style.Control;
            slot.Rim.color = focus ? style.AccentBright
                : !can ? HearthKit.Faded(style.Inactive, 0.35f) : HearthKit.Faded(lit ? style.AccentBright : style.Accent, lit ? 0.95f : 0.5f);
            slot.Icon.color = !can ? style.Inactive : lit ? style.AccentBright : HearthKit.IconColour(command.Icon);
            slot.Caption.color = !can ? style.Inactive : lit ? style.AccentBright : style.Text;
            slot.Badge.gameObject.SetActive(count > 0);
            slot.BadgeLabel.text = count.ToString();
            if (pointed == slot || focus)
            {
                ShowTip(slot);
            }
        }
    }

    /// <summary>Lights the next (or previous) order that is shown, its words above it; from none, the first (or last).</summary>
    public void Step(int direction)
    {
        if (slots.Count == 0 || direction == 0)
        {
            return;
        }

        int at = focused;
        for (int tries = 0; tries < slots.Count; tries++)
        {
            at = at < 0 ? (direction > 0 ? 0 : slots.Count - 1) : (at + direction + slots.Count) % slots.Count;
            if (slots[at].Rect.gameObject.activeSelf)
            {
                break;
            }
        }

        SetFocus(at);
    }

    /// <summary>Gives the lit order (when it can be given), and lets go of it.</summary>
    public void RunFocused()
    {
        if (focused < 0)
        {
            return;
        }

        Slot slot = slots[focused];
        SetFocus(-1);
        if (slot.Why == null)
        {
            slot.Command.Run?.Invoke();
            Refresh(true);
        }
        else
        {
            GameMessages.Post(slot.Why, MessageKind.Warning);
        }
    }

    /// <summary>Lets go of the lit order. False when none was lit.</summary>
    public bool ClearFocus()
    {
        if (focused < 0)
        {
            return false;
        }

        SetFocus(-1);
        return true;
    }

    private void SetFocus(int index)
    {
        int before = focused;
        focused = index;
        if (before >= 0 && before < slots.Count)
        {
            Restyle(slots[before]);
        }

        if (index < 0)
        {
            tip.Hide();
            return;
        }

        Restyle(slots[index]);
        ShowTip(slots[index]);
    }

    // The lit order's rim bright and its face raised, over its state's look.
    private void Restyle(Slot slot)
    {
        slot.Count = -1;
        Refresh(true);
    }

    private void ShowTip(Slot slot)
    {
        string words = slot.Why ?? slot.Command.Words();
        tip.Show(slot.Command.Title != null ? slot.Command.Title() : slot.Command.Caption, GameInput.WithRebinds(words ?? ""), slot.Rect);
    }
}

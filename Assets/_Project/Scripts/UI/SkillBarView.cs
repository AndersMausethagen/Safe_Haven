using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The always-visible skill bar on the HUD, V Rising style: the nine loadout slots with their keys (a skill, or a
/// consumable with how many are carried), a radial shade and seconds counter while a skill cools down, a bright pulse when it is cast and a red one when a cast is refused. The
/// slots are the same ItemSlotView cells the bag and skill window use; this view only feeds them. Casting is input ->
/// PlayerSkills and arranging skills is the skill window; the one change made here is a consumable dragged from the bag
/// and dropped on a slot (the user, 2026-11-15). Its size is the Skill Bar Size option (GameOptions.SkillBarSize).
/// Its slots can also be reordered by dragging one onto another (the user, 2026-11-20), while the bag, the character screen or the
/// skill book is open (the bar takes no clicks in play): the two swap (SkillLoadout.Move).
/// <para>
/// Where it belongs: the SkillHUD prefab, placed in every gameplay scene. It finds the player through Player.Current /
/// Player.Spawned, so it needs no scene references.
/// </para>
/// </summary>
public sealed class SkillBarView : MonoBehaviour, IItemSlotHandler
{
    [Serializable]
    private sealed class Slot
    {
        public ItemSlotView view;
        [Tooltip("Filled (radial) image over the icon showing the cooldown left.")]
        public Image cooldown;
        [Tooltip("Optional: seconds left.")]
        public TMP_Text timer;
        [Tooltip("Optional: pulses when the skill is cast or refused.")]
        public Image flash;

        [NonSerialized] public float flashUntil = -1f;
        [NonSerialized] public CanvasGroup fade;
        [NonSerialized] public Color flashColor;
        [NonSerialized] public int shownSeconds = -1;
        [NonSerialized] public int shownCount = -1;
        [NonSerialized] public bool dimmed;
    }

    [SerializeField] private Slot[] slots = new Slot[SkillLoadout.SlotCount];
    [SerializeField] private Color castColor = new Color(0.98f, 0.90f, 0.70f, 0.55f);
    [SerializeField] private Color refusedColor = new Color(0.85f, 0.18f, 0.12f, 0.6f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.35f;
    [Tooltip("Optional: hidden while there is no player.")]
    [SerializeField] private CanvasGroup group;

    private Player bound;
    private PlayerSkills skills;
    private ItemInventory inventory;
    private PlayerFocus focus;
    private PlayerCombat combat;
    [Tooltip("The pulse on the weapon art's slot while a free follow-up can be struck.")]
    [SerializeField] private Color followUpColor = new Color(0.98f, 0.80f, 0.40f, 0.5f);
    private Vector3 baseScale;
    private bool baseScaleKnown;
    // A slot of this bar being dragged onto another (-1: none), and the icon that follows the pointer meanwhile.
    private int dragFrom = -1;
    private Image dragGhost;

    private void OnEnable()
    {
        Player.Spawned += Bind;
        GameInput.KeysChanged += OnKeysChanged;
        GameOption.AnyChanged += OnOptionChanged;
        SetVisible(false);
        ApplySize();
        for (int i = 0; i < slots.Length; i++)
        {
            // The skill window is where skills are arranged; here a consumable can only be dropped on from the bag. The
            // slot's frame catches the drop, and only while the bag is open (CatchDrops): in play the bar never takes a click.
            slots[i]?.view?.Bind(this, i);
            if (slots[i]?.view != null && slots[i].view.TryGetComponent(out Image frame))
            {
                frame.raycastTarget = true;
            }
        }
    }

    private void OnDisable()
    {
        Player.Spawned -= Bind;
        GameInput.KeysChanged -= OnKeysChanged;
        GameOption.AnyChanged -= OnOptionChanged;
        EndBarDrag();
        Unbind();
    }

    private void OnOptionChanged(GameOption option)
    {
        if (option == GameOptions.SkillBarSize)
        {
            ApplySize();
        }
    }

    // The bar grows from its bottom centre (its pivot), so it stays on the screen's edge at any size.
    private void ApplySize()
    {
        if (!baseScaleKnown)
        {
            baseScale = transform.localScale;
            baseScaleKnown = true;
        }

        transform.localScale = baseScale * GameOptions.SkillBarSize.Value;
    }

    // ----- Dropping a consumable from the bag -----

    public void OnSlotDropped(ItemSlotView target)
    {
        if (skills != null && target != null && dragFrom >= 0)
        {
            int from = dragFrom;
            EndBarDrag();
            if (skills.Loadout.Move(from, target.Index))
            {
                GameMessages.Post($"Moved to {PlayerInputHandler.SkillKeyLabel(target.Index)} on the skill bar.", MessageKind.Info);
            }

            return;
        }

        if (skills == null || !ItemDrag.Active || target == null)
        {
            return;
        }

        ItemStack stack = ItemDrag.Source.GetSlot(ItemDrag.Index);
        if (stack.IsEmpty)
        {
            return;
        }

        if (!QuickItems.Accepts(stack.Item))
        {
            GameMessages.Post("Only things you can use go on the skill bar.", MessageKind.Warning);
            return;
        }

        SkillDefinition replaced = skills.Loadout.Get(target.Index);
        if (skills.Loadout.EquipItem(target.Index, stack.Item))
        {
            GameMessages.Post($"{stack.Item.DisplayName} is on the skill bar: {PlayerInputHandler.SkillKeyLabel(target.Index)} uses it"
                + (replaced != null ? $" ({replaced.DisplayName} went back to the skill book)." : "."), MessageKind.Info);
        }
    }

    public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
    {
    }

    // ----- Reordering the bar by dragging one of its slots onto another -----

    public void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData)
    {
        if (skills == null || slot == null || skills.Loadout.IsEmpty(slot.Index))
        {
            return;
        }

        dragFrom = slot.Index;
        SkillDefinition skill = skills.Loadout.Get(slot.Index);
        ItemDefinition item = skills.Loadout.GetItem(slot.Index);
        Sprite sprite = skill != null ? skill.Icon : item != null ? item.Icon : null;
        if (dragGhost == null)
        {
            var go = new GameObject("Skill Bar Drag", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            dragGhost = go.AddComponent<Image>();
            dragGhost.raycastTarget = false;
            dragGhost.preserveAspect = true;
            var ghostRect = (RectTransform)go.transform;
            // Anchored at the bar's pivot, so a point local to the bar is where the ghost goes.
            ghostRect.anchorMin = ghostRect.anchorMax = ((RectTransform)transform).pivot;
            ghostRect.sizeDelta = ((RectTransform)slot.transform).rect.size * 0.9f;
        }

        dragGhost.sprite = sprite;
        dragGhost.color = new Color(1f, 1f, 1f, 0.8f);
        dragGhost.transform.SetAsLastSibling();
        dragGhost.gameObject.SetActive(sprite != null);
        OnSlotDragged(eventData);
    }

    public void OnSlotDragged(PointerEventData eventData)
    {
        if (dragFrom < 0 || dragGhost == null || eventData == null)
        {
            return;
        }

        var parent = (RectTransform)dragGhost.transform.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            ((RectTransform)dragGhost.transform).anchoredPosition = local;
        }
    }

    // Let go anywhere else: nothing moves.
    public void OnSlotDragEnded() => EndBarDrag();

    private void EndBarDrag()
    {
        dragFrom = -1;
        if (dragGhost != null)
        {
            dragGhost.gameObject.SetActive(false);
        }
    }

    public void OnSlotHovered(ItemSlotView slot)
    {
    }

    public void OnSlotUnhovered(ItemSlotView slot)
    {
    }

    // Each slot shows the key or button that casts it.
    private void OnKeysChanged()
    {
        FadeForWindows();
        if (skills == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            OnSlotChanged(i);
        }
    }

    private void Bind(Player player)
    {
        if (player == bound)
        {
            return;
        }

        Unbind();
        bound = player;
        skills = bound != null ? bound.Skills : null;
        inventory = bound != null ? bound.GetComponent<ItemInventory>() : null;
        focus = bound != null ? bound.GetComponent<PlayerFocus>() : null;
        combat = bound != null ? bound.GetComponent<PlayerCombat>() : null;
        if (skills == null)
        {
            return;
        }

        skills.Loadout.SlotChanged += OnSlotChanged;
        skills.SkillCast += OnSkillCast;
        skills.CastRefused += OnCastRefused;
        skills.BarItemUsed += OnBarItemUsed;
        for (int i = 0; i < slots.Length; i++)
        {
            OnSlotChanged(i);
        }

        SetVisible(true);
    }

    private void Unbind()
    {
        // The player may already be destroyed (scene change); its events went with it.
        if (!ReferenceEquals(skills, null) && skills != null)
        {
            skills.Loadout.SlotChanged -= OnSlotChanged;
            skills.SkillCast -= OnSkillCast;
            skills.CastRefused -= OnCastRefused;
            skills.BarItemUsed -= OnBarItemUsed;
        }

        bound = null;
        skills = null;
        inventory = null;
    }

    private void OnSlotChanged(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index]?.view == null)
        {
            return;
        }

        SkillDefinition skill = skills.Loadout.Get(index);
        ItemDefinition item = skills.Loadout.GetItem(index);
        if (item != null)
        {
            // A consumable: its icon and how many are carried (greyed when none).
            int carried = inventory != null ? inventory.CountCarried(item) : 0;
            slots[index].view.Show(item.Icon, Color.clear, PlayerInputHandler.SkillKeyLabel(index), carried.ToString(), isLocked: carried <= 0);
            slots[index].shownCount = carried;
        }
        else
        {
            slots[index].view.Show(
                skill != null ? skill.Icon : null,
                skill != null ? SkillSchools.ColorOf(skill.School) : Color.clear,
                PlayerInputHandler.SkillKeyLabel(index));
            slots[index].shownCount = -1;
        }

        slots[index].shownSeconds = -1;
        slots[index].dimmed = false; // Show set the icon afresh: UpdateSlot greys it again if it still can't be paid for
    }

    private void OnSkillCast(int index, SkillDefinition skill) => Flash(index, castColor);

    private void OnBarItemUsed(int index, ItemDefinition item) => Flash(index, castColor);

    private void OnCastRefused(int index, SkillCastResult result)
    {
        if (result != SkillCastResult.EmptySlot)
        {
            Flash(index, refusedColor);
        }
    }

    private void Flash(int index, Color color)
    {
        if (index >= 0 && index < slots.Length && slots[index] != null)
        {
            slots[index].flashColor = color;
            slots[index].flashUntil = Time.unscaledTime + flashDuration;
        }
    }

    private void Update()
    {
        if (bound == null)
        {
            if (Player.Current != null)
            {
                Bind(Player.Current);
            }
            else if (group != null && group.alpha > 0f)
            {
                SetVisible(false);
            }
        }

        FadeForWindows();
        if (skills == null)
        {
            return;
        }

        // Empty slots, in a Contextual HUD, only while something could go into them (dragged, or the skills window open): four empty
        // frames sat beside five filled ones all game (the HUD review of 2026-11-18). Faded, not removed, so every key keeps its place.
        bool showEmpty = HudRules.ShowsEverything || ItemDrag.Active || SkillsWindow.IsOpen;
        for (int i = 0; i < slots.Length; i++)
        {
            UpdateSlot(slots[i], skills.Loadout.Get(i));
            ItemDefinition item = skills.Loadout.GetItem(i);
            if (item != null && slots[i] != null && inventory != null && inventory.CountCarried(item) != slots[i].shownCount)
            {
                OnSlotChanged(i);
            }

            FadeEmpty(slots[i], showEmpty || item != null || skills.Loadout.Get(i) != null);
        }
    }

    private static void FadeEmpty(Slot slot, bool shown)
    {
        if (slot?.view == null)
        {
            return;
        }

        if (slot.fade == null && !slot.view.TryGetComponent(out slot.fade))
        {
            slot.fade = slot.view.gameObject.AddComponent<CanvasGroup>();
        }

        slot.fade.alpha = Mathf.MoveTowards(slot.fade.alpha, shown ? 1f : 0f, 6f * Time.unscaledDeltaTime);
        // Still a drop target while dragging (showEmpty), so a faded slot never takes a click it can't be seen to.
        slot.fade.blocksRaycasts = shown;
    }

    private void UpdateSlot(Slot slot, SkillDefinition skill)
    {
        if (slot == null)
        {
            return;
        }

        float fraction = skill != null ? skills.CooldownFraction(skill) : 0f;
        if (slot.cooldown != null)
        {
            slot.cooldown.enabled = fraction > 0f;
            slot.cooldown.fillAmount = fraction;
        }

        if (slot.timer != null)
        {
            int seconds = fraction > 0f ? Mathf.CeilToInt(skills.CooldownRemaining(skill)) : 0;
            if (seconds != slot.shownSeconds)
            {
                slot.shownSeconds = seconds;
                slot.timer.SetText(seconds > 0 ? "{0}" : "", seconds);
            }
        }

        // Grey while no Focus circle is full to pay for it (an art's free follow-up needs none): it showed ready until pressed.
        bool followUp = skill != null && skill.WeaponArt == WeaponArtSlot.Own && combat != null && combat.ArtFollowUpOpen;
        bool unpaid = skill != null && focus != null && !followUp
            && (skill.WeaponArt != WeaponArtSlot.None ? !focus.HasCircle : focus.Circles < skill.FocusCircles);
        if (slot.view != null && unpaid != slot.dimmed)
        {
            slot.dimmed = unpaid;
            slot.view.SetDimmed(unpaid);
        }

        if (slot.flash != null)
        {
            float t = slot.flashUntil >= 0f ? Mathf.Clamp01((slot.flashUntil - Time.unscaledTime) / flashDuration) : 0f;
            if (t > 0f)
            {
                slot.flash.enabled = true;
                Color color = slot.flashColor;
                color.a *= t;
                slot.flash.color = color;
            }
            else if (followUp)
            {
                // The art may be struck again, free: the slot pulses gold until the window shuts.
                slot.flashUntil = -1f;
                slot.flash.enabled = true;
                Color color = followUpColor;
                color.a *= 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 14f);
                slot.flash.color = color;
            }
            else
            {
                slot.flash.enabled = false;
                slot.flashUntil = -1f;
            }
        }
    }

    // Out of the way while a window covers the screen (as the rest of the HUD, HudGivesWay), except the bag and the character
    // sheet: putting things on the bar from the bag (hover, then a number key) shows where they went.
    private void FadeForWindows()
    {
        if (group == null || bound == null)
        {
            return;
        }

        UIStateManager ui = UIStateManager.Current;
        bool bagOpen = ui != null && (ui.IsOpen(UIWindowId.Items) || ui.IsOpen(UIWindowId.Character) || ui.IsOpen(UIWindowId.Skills));
        bool hide = HudGivesWay.Covered && !bagOpen;
        float target = hide ? 0f : 1f;
        if (group.alpha != target)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 7f);
        }

        // Catches a stack dragged from the bag; otherwise the pointer goes through to the game.
        bool catchDrops = bagOpen && group.alpha > 0.5f;
        if (group.blocksRaycasts != catchDrops)
        {
            group.blocksRaycasts = catchDrops;
        }
    }

    private void SetVisible(bool visible)
    {
        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
        }
    }
}

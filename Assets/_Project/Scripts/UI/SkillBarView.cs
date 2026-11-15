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
        [NonSerialized] public Color flashColor;
        [NonSerialized] public int shownSeconds = -1;
        [NonSerialized] public int shownCount = -1;
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
    private Vector3 baseScale;
    private bool baseScaleKnown;

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

    public void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData)
    {
    }

    public void OnSlotDragged(PointerEventData eventData)
    {
    }

    public void OnSlotDragEnded()
    {
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

        for (int i = 0; i < slots.Length; i++)
        {
            UpdateSlot(slots[i], skills.Loadout.Get(i));
            ItemDefinition item = skills.Loadout.GetItem(i);
            if (item != null && slots[i] != null && inventory != null && inventory.CountCarried(item) != slots[i].shownCount)
            {
                OnSlotChanged(i);
            }
        }
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

        if (slot.flash != null)
        {
            float t = slot.flashUntil >= 0f ? Mathf.Clamp01((slot.flashUntil - Time.unscaledTime) / flashDuration) : 0f;
            slot.flash.enabled = t > 0f;
            if (t > 0f)
            {
                Color color = slot.flashColor;
                color.a *= t;
                slot.flash.color = color;
            }
            else
            {
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
        bool bagOpen = ui != null && (ui.IsOpen(UIWindowId.Items) || ui.IsOpen(UIWindowId.Character));
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

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The always-visible skill bar on the HUD, V Rising style: the five loadout slots with their keys, a radial shade and
/// seconds counter while a skill cools down, a bright pulse when it is cast and a red one when a cast is refused. The
/// slots are the same ItemSlotView cells the bag and skill window use; this view only feeds them. It changes nothing:
/// casting is input -> PlayerSkills, arranging is the skill window.
/// <para>
/// Where it belongs: the SkillHUD prefab, placed in every gameplay scene. It finds the player through Player.Current /
/// Player.Spawned, so it needs no scene references.
/// </para>
/// </summary>
public sealed class SkillBarView : MonoBehaviour
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
    }

    [SerializeField] private Slot[] slots = new Slot[SkillLoadout.SlotCount];
    [SerializeField] private Color castColor = new Color(0.98f, 0.90f, 0.70f, 0.55f);
    [SerializeField] private Color refusedColor = new Color(0.85f, 0.18f, 0.12f, 0.6f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.35f;
    [Tooltip("Optional: hidden while there is no player.")]
    [SerializeField] private CanvasGroup group;

    private Player bound;
    private PlayerSkills skills;

    private void OnEnable()
    {
        Player.Spawned += Bind;
        SetVisible(false);
        foreach (Slot slot in slots)
        {
            // The HUD bar is display only: the skill window is where skills are arranged.
            slot?.view?.Bind(null, 0);
        }
    }

    private void OnDisable()
    {
        Player.Spawned -= Bind;
        Unbind();
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
        if (skills == null)
        {
            return;
        }

        skills.Loadout.SlotChanged += OnSlotChanged;
        skills.SkillCast += OnSkillCast;
        skills.CastRefused += OnCastRefused;
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
        }

        bound = null;
        skills = null;
    }

    private void OnSlotChanged(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index]?.view == null)
        {
            return;
        }

        SkillDefinition skill = skills.Loadout.Get(index);
        slots[index].view.Show(
            skill != null ? skill.Icon : null,
            skill != null ? SkillSchools.ColorOf(skill.School) : Color.clear,
            PlayerInputHandler.SkillKeyLabel(index));
        slots[index].shownSeconds = -1;
    }

    private void OnSkillCast(int index, SkillDefinition skill) => Flash(index, castColor);

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

        if (skills == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            UpdateSlot(slots[i], skills.Loadout.Get(i));
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

    private void SetVisible(bool visible)
    {
        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
        }
    }
}

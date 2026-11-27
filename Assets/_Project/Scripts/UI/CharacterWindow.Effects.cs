using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The character sheet's Active Effects (the user, 2026-11-27: "It should be easier to see which buffs are active, It should show on the C
/// menu under 'Active effects' and the player should easily see what gave that buff and what it does"): a card across the sheet's foot
/// with a picture for each thing at work (ActiveEffects), its name under it and its time left in the corner; what gave it and what it does
/// come up on hover. Harmful things have a red rim. Part of CharacterWindow, in its own file.
/// </summary>
public sealed partial class CharacterWindow
{
    private const float EffectsTop = 614f;
    private const float EffectsHeight = 150f;
    // Wider than its picture, with two lines for the name: "Venison with Lingonberries" was cut to "Barkskin Dr"-length.
    private const float EffectCardWidth = 92f;
    private const float EffectPicture = 64f;
    private const float EffectGap = 10f;
    private const float EffectsRefreshSeconds = 0.5f;

    private RectTransform effectsCard;
    private TMP_Text effectsNone;
    private readonly List<KitPictureCard> effectCards = new List<KitPictureCard>();
    private List<ActiveEffect> shownEffects = new List<ActiveEffect>();
    private int hoveredEffect = -1;
    private float nextEffectsRefresh;

    private void BuildEffects(RectTransform window)
    {
        UIStyle style = UIStyle.Current;
        effectsCard = HearthKit.Card(window, "Active Effects", 24f, EffectsTop, SheetWidth - 48f, EffectsHeight);
        HearthKit.Section(effectsCard, "Active effects", "Star", 20f, 14f);
        effectsNone = CodeUI.Label("None", effectsCard, 20f, 54f, SheetWidth - 88f, 26f, 17f, style.Muted);
        effectsNone.text = "Nothing at work on you now. Meals, tonics, a sauna and your skills' blessings show here.";
    }

    private void UpdateEffects()
    {
        if (effectsCard == null || Time.unscaledTime < nextEffectsRefresh)
        {
            return;
        }

        nextEffectsRefresh = Time.unscaledTime + EffectsRefreshSeconds;
        shownEffects = ActiveEffects.On(Player.Current);
        effectsNone.gameObject.SetActive(shownEffects.Count == 0);
        UIStyle style = UIStyle.Current;
        // One row across the sheet (ten pictures): more than that at once has never been possible (two meals, a tonic, a coating, a
        // few blessings and statuses), so the rest are left out rather than squeezed.
        int room = Mathf.Max(1, Mathf.FloorToInt((SheetWidth - 88f + EffectGap) / (EffectCardWidth + EffectGap)));
        int count = Mathf.Min(shownEffects.Count, room);
        for (int i = 0; i < count; i++)
        {
            if (i >= effectCards.Count)
            {
                int index = i;
                var made = new KitPictureCard(effectsCard, "Effect " + i, EffectCardWidth, EffectPicture, 2, 12f,
                    () => ShowEffect(index), HideEffect);
                effectCards.Add(made);
            }

            ActiveEffect effect = shownEffects[i];
            KitPictureCard card = effectCards[i];
            card.SetActive(true);
            card.Place(20f + i * (EffectCardWidth + EffectGap), 44f);
            card.ShowPicture(effect.Picture, effect.Icon, effect.Harmful ? style.Danger : (Color?)null);
            card.SetName(effect.Name);
            card.SetBadge(effect.SecondsLeft >= 0f ? RaidStats.FormatTime(effect.SecondsLeft) : "");
            card.SetLook(false, effect.Harmful ? style.Danger : (Color?)null);
        }

        for (int i = count; i < effectCards.Count; i++)
        {
            effectCards[i].SetActive(false);
        }

        if (hoveredEffect >= 0)
        {
            ShowEffect(hoveredEffect);
        }
    }

    private void ShowEffect(int index)
    {
        hoveredEffect = index;
        if (dragFrom >= 0)
        {
            return;
        }

        // The one pointed at has worn off and the row grew shorter: its words go with it.
        if (index < 0 || index >= shownEffects.Count)
        {
            tooltip.Hide();
            return;
        }

        UIStyle style = UIStyle.Current;
        ActiveEffect effect = shownEffects[index];
        string time = effect.SecondsLeft >= 0f ? $"\n<color={style.MutedHex}>{RaidStats.FormatTime(effect.SecondsLeft)} left</color>" : "";
        string does = string.IsNullOrEmpty(effect.Does) ? "" : $"\n{effect.Does}";
        tooltip.Show(effect.Name, effect.Harmful ? style.Danger : style.AccentBright,
            $"<color={style.MutedHex}>From:</color> {effect.Source}{does}{time}", effect.Picture);
    }

    private void HideEffect()
    {
        hoveredEffect = -1;
        tooltip.Hide();
    }
}

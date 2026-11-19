using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "While you were away" (Homecoming): a Hearthline kit card on the left of the screen, under the Haven's banner, that fades in a
/// moment after coming home from a raid, holds while it is read (longer for more lines) and fades away by itself; it takes no input,
/// so it never stands between the player and the game. Made the first time it is needed; one per game.
/// </summary>
public sealed class HomecomingCard : MonoBehaviour
{
    private const float Width = 460f;
    private const float Delay = 2f;
    private const float Fade = 0.4f;

    private static HomecomingCard current;
    private RectTransform card;
    private CanvasGroup group;
    private TMP_Text heading;
    private TMP_Text body;
    private float shownAt = -100f;
    private float holdFor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>Shows the lines, after the moment of arriving.</summary>
    public static void Show(List<string> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Homecoming Card");
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            current = root.AddComponent<HomecomingCard>();
            current.Build(root.transform);
        }

        current.Fill(lines);
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        card = CodeUI.Box("Card", root, new Vector2(0f, 0.5f), new Vector2(Width, 200f), new Vector2(40f + Width * 0.5f, 120f));
        HearthKit.Sliced(card.gameObject.AddComponent<Image>(), style.Card, 1.3f).color = style.Panel;
        card.GetComponent<Image>().raycastTarget = false;
        HearthKit.Rim(card, 0.85f, 1.3f);
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        heading = CodeUI.Label("Heading", card, 22f, 14f, Width - 44f, 28f, 20f, style.Accent);
        style.Title(heading);
        heading.text = "While you were away";
        body = CodeUI.Label("Lines", card, 22f, 48f, Width - 44f, 140f, 16f, style.Text);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.raycastTarget = false;
    }

    private void Fill(List<string> lines)
    {
        UIStyle style = UIStyle.Current;
        var text = new System.Text.StringBuilder();
        foreach (string line in lines)
        {
            text.Append(text.Length > 0 ? "\n" : "").Append($"<color={style.AccentHex}>·</color> ").Append(line);
        }

        body.text = text.ToString();
        float height = Mathf.Max(22f, body.GetPreferredValues(body.text, Width - 44f, 0f).y);
        body.rectTransform.sizeDelta = new Vector2(Width - 44f, height);
        card.sizeDelta = new Vector2(Width, 52f + height + 20f);
        shownAt = Time.unscaledTime + Delay;
        holdFor = Mathf.Clamp(4f + lines.Count * 1.6f, 6f, 16f);
    }

    private void Update()
    {
        float age = Time.unscaledTime - shownAt;
        float alpha = age < 0f ? 0f : age < Fade ? age / Fade : age < holdFor ? 1f : Mathf.Clamp01(1f - (age - holdFor) / Fade);
        // Out of the way of a window or a conversation.
        bool busy = (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen) || DialogueBox.IsSpeaking
            || Player.Current == null || RaidDirector.Current != null;
        group.alpha = busy ? 0f : alpha;
    }
}

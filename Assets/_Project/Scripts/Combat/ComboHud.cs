using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small readout under the player's feet on screen while fighting: one pip per swing of the weapon's chain (lit up to
/// the swing just made, gold for the finisher), the rhythm stacks, "Counter ready" while one is, and a word when a
/// press is judged ("Rhythm" on the beat, "Too early" when mashing). It fades out a moment after the fighting stops.
/// Made by CombatFeedback for the player in every scene; it only reads PlayerCombat's events.
/// </summary>
public sealed class ComboHud : MonoBehaviour
{
    private const int MaxPips = 5;
    private const float VisibleSeconds = 2.5f;

    private static readonly Color PipOff = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color PipOn = new Color(1f, 0.97f, 0.9f, 0.95f);
    private static readonly Color PipFinisher = new Color(1f, 0.78f, 0.35f, 1f);
    private static readonly Color RhythmColor = new Color(1f, 0.85f, 0.4f);
    private static readonly Color CounterColor = new Color(0.55f, 0.85f, 1f);
    private static readonly Color EarlyColor = new Color(0.75f, 0.75f, 0.75f);

    private readonly List<Image> pips = new List<Image>();
    private PlayerCombat combat;
    private CanvasGroup group;
    private TMP_Text status;
    private TMP_Text callout;
    private float visibleUntil;
    private float calloutUntil;
    private int litPips;
    private int chainLength;
    private float pulse;

    public static ComboHud Create(PlayerCombat combat)
    {
        var go = new GameObject("Combo HUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = go.AddComponent<ComboHud>();
        hud.group = go.AddComponent<CanvasGroup>();
        hud.group.alpha = 0f;
        hud.group.interactable = false;
        hud.group.blocksRaycasts = false;

        var row = new GameObject("Pips", typeof(RectTransform));
        row.transform.SetParent(go.transform, false);
        var rowRect = (RectTransform)row.transform;
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0f);
        rowRect.anchoredPosition = new Vector2(0f, 190f);
        for (int i = 0; i < MaxPips; i++)
        {
            var pip = new GameObject("Pip " + i, typeof(RectTransform));
            pip.transform.SetParent(row.transform, false);
            var rect = (RectTransform)pip.transform;
            rect.sizeDelta = new Vector2(14f, 14f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = pip.AddComponent<Image>();
            image.raycastTarget = false;
            hud.pips.Add(image);
        }

        hud.status = Text("Status", go.transform, 22f, new Vector2(0f, 222f));
        hud.callout = Text("Callout", go.transform, 28f, new Vector2(0f, 258f));
        hud.Bind(combat);
        return hud;
    }

    private static TMP_Text Text(string name, Transform parent, float size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = "";
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(600f, size * 1.4f);
        rect.anchoredPosition = position;
        return text;
    }

    private void Bind(PlayerCombat target)
    {
        combat = target;
        combat.SwingStarted += OnSwing;
        combat.ChainJudged += OnJudged;
        combat.CounterReady += OnCounterReady;
    }

    private void OnDestroy()
    {
        if (combat != null)
        {
            combat.SwingStarted -= OnSwing;
            combat.ChainJudged -= OnJudged;
            combat.CounterReady -= OnCounterReady;
        }
    }

    private void OnSwing(SwingInfo swing)
    {
        visibleUntil = Time.unscaledTime + VisibleSeconds;
        chainLength = Mathf.Clamp(swing.ChainLength, 1, MaxPips);
        litPips = swing.Kind == SwingKind.Light ? Mathf.Clamp(swing.Step + 1, 1, chainLength) : chainLength;
        pulse = 1f;
        if (swing.IsCounter)
        {
            Callout(CombatFeelSettings.Current.CounterText, CounterColor);
        }
        else if (swing.Kind == SwingKind.Heavy)
        {
            Callout("Heavy", PipFinisher);
        }
        else if (swing.IsFinisher)
        {
            Callout("Finisher", PipFinisher);
        }
    }

    private void OnJudged(ChainPress press)
    {
        if (press == ChainPress.Rhythm)
        {
            Callout("Rhythm", RhythmColor);
        }
        else if (press == ChainPress.Early && combat.IsAttacking)
        {
            Callout("Too early", EarlyColor);
        }
    }

    private void OnCounterReady()
    {
        visibleUntil = Time.unscaledTime + VisibleSeconds;
    }

    private void Callout(string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        callout.text = text;
        callout.color = color;
        calloutUntil = Time.unscaledTime + 0.7f;
        visibleUntil = Mathf.Max(visibleUntil, Time.unscaledTime + VisibleSeconds);
    }

    private void Update()
    {
        if (combat == null)
        {
            Destroy(gameObject);
            return;
        }

        float now = Time.unscaledTime;
        group.alpha = Mathf.MoveTowards(group.alpha, now < visibleUntil ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 5f);
        for (int i = 0; i < pips.Count; i++)
        {
            Image pip = pips[i];
            bool used = i < chainLength;
            pip.enabled = used;
            if (!used)
            {
                continue;
            }

            bool lit = i < litPips;
            bool finisher = chainLength > 1 && i == chainLength - 1;
            pip.color = lit ? (finisher ? PipFinisher : PipOn) : PipOff;
            float x = (i - (chainLength - 1) * 0.5f) * 26f;
            float scale = lit && i == litPips - 1 ? 1f + pulse * 0.6f : 1f;
            pip.rectTransform.anchoredPosition = new Vector2(x, 0f);
            pip.rectTransform.localScale = Vector3.one * scale;
        }

        string text = combat.Rhythm > 0 ? $"Rhythm ×{combat.Rhythm}" : "";
        if (combat.CounterIsReady)
        {
            text = text.Length > 0 ? text + "   Counter ready" : "Counter ready";
            visibleUntil = Mathf.Max(visibleUntil, now + 0.5f);
        }

        status.text = text;
        status.color = combat.CounterIsReady ? CounterColor : RhythmColor;
        float calloutLeft = calloutUntil - now;
        callout.alpha = Mathf.Clamp01(calloutLeft / 0.25f);
        callout.rectTransform.localScale = Vector3.one * (1f + Mathf.Clamp01(calloutLeft - 0.45f) * 1.2f);
    }
}

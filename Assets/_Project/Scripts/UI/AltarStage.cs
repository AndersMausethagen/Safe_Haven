using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The altar's stage (Docs/AltarUXResearch_2026-11-28.md): what is being worked on, large, inside a slowly turning ring of runes with a
/// glow in its colour, and the moment of working it. The apply button is held, not clicked: a fill sweeps across it while the ring
/// turns faster, the glow rises and a charge sounds; let go early and it drains away. When the hold is full the working is done, and
/// the stage answers by how much it matters (AltarMoment): a flash, a shockwave, a burst of sparks, the picture punched up and back,
/// a small shake of the stage alone, the commit's sound and a line saying what happened. A pad's press (or Enter) runs the same hold
/// by itself. The Screen Flashes and Screen Shake options scale the light and the shake; the sound and the line always come.
/// Built in code by AltarWindow; everything runs on unscaled time, as the window may pause the game.
/// </summary>
public sealed class AltarStage : MonoBehaviour
{
    private sealed class Spark
    {
        public Image Image;
        public Vector2 Velocity;
        public float Age;
        public float Life;
    }

    private RectTransform stage;
    private Image ring;
    private Image glow;
    private Image picture;
    private Image flash;
    private Image shock;
    private TMP_Text result;
    private CanvasGroup resultCard;
    private Image[] resultRules;
    private float resultCardY;
    private bool resultTicks;
    private Image holdFill;
    private Button button;
    private readonly List<Spark> sparks = new List<Spark>();
    private Vector2 stageHome;

    private Color tint = Color.white;
    private Func<bool> canCommit;
    private Action commit;
    private Func<(AltarMoment moment, string line)> describe;

    private bool holding;
    private bool autoHold;
    private float held;
    private AltarMoment holdingMoment;
    private float sinceCommit = -1f;
    private AltarMoment lastMoment;
    private float resultAge = -1f;

    private static AltarFeel Feel => AltarFeel.Current;

    // A filled Image needs a sprite to fill: a plain white one, made once.
    private static Sprite white;
    private static Sprite WhiteSprite => white != null ? white : white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));

    /// <summary>Builds the stage at (x, y) in <paramref name="parent"/>, <paramref name="size"/> across.</summary>
    public static AltarStage Build(RectTransform parent, float x, float y, float size)
    {
        RectTransform root = CodeUI.At("Altar Stage", parent, x, y, size, size);
        var made = root.gameObject.AddComponent<AltarStage>();
        made.stage = root;
        made.stageHome = root.anchoredPosition;
        made.Make(size);
        return made;
    }

    private void Make(float size)
    {
        glow = Layer("Glow", Feel.Glow, size * 1.15f);
        ring = Layer("Ring", Feel.GlyphRing, size);
        picture = Layer("Picture", null, size * 0.56f);
        picture.preserveAspect = true;
        shock = Layer("Shock", Feel.Shock, size);
        flash = Layer("Flash", Feel.Glow, size * 1.4f);
        flash.color = Color.clear;
        shock.color = Color.clear;
        // The result card (Docs/AltarUXResearch_2026-11-28.md, review): a dark plate across the ring's foot, so its words read over the
        // sparks and glow; it rises in, ticks as it lands, stays and fades.
        // Under the ring, over the name it repeats while it shows (review: across the ring's foot it read as runes over text).
        RectTransform plate = CodeUI.At("Result Card", stage, -size * 0.45f, size + 2f, size * 1.9f, 40f);
        var plateImage = plate.gameObject.AddComponent<Image>();
        // Solid: it lies over the offer's name while it shows, which must not read through it.
        plateImage.color = new Color(0.07f, 0.055f, 0.045f, 1f);
        plateImage.raycastTarget = false;
        // Thin rules of the kind's colour above and below (an Outline on a filled plate copies the whole fill in the rule's
        // colour, which washed it gold).
        resultRules = new[] { Rule(plate, 0f), Rule(plate, 38f) };
        resultCard = plate.gameObject.AddComponent<CanvasGroup>();
        resultCard.alpha = 0f;
        resultCard.blocksRaycasts = false;
        resultCardY = plate.anchoredPosition.y;
        result = CodeUI.Label("Result", plate, 6f, 3f, size * 1.9f - 12f, 34f, 20f, UIStyle.Current.AccentBright, TextAlignmentOptions.Center);
        UIStyle.Current.Title(result);
        result.color = UIStyle.Current.AccentBright;
        result.richText = true;
        result.enableAutoSizing = true;
        result.fontSizeMin = 14f;
        result.fontSizeMax = 20f;
        result.fontStyle = FontStyles.Bold;
        result.text = "";
    }

    private static Image Rule(RectTransform plate, float y)
    {
        RectTransform rect = CodeUI.At("Rule", plate, 0f, y, plate.rect.width, 2f);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private Image Layer(string name, Sprite sprite, float size)
    {
        RectTransform rect = CodeUI.Box(name, stage, new Vector2(0.5f, 0.5f), new Vector2(size, size), Vector2.zero);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.enabled = sprite != null || name == "Picture";
        return image;
    }

    /// <summary>What stands on the stage now, in the colour of what it is (a kind's, a school's).</summary>
    public void Show(Sprite shown, Color colour)
    {
        picture.sprite = shown;
        picture.enabled = shown != null;
        tint = colour;
    }

    // Steps as pips under the ring (Diablo IV's masterwork ranks): lit for those held, faint for those ahead, the next one ringed.
    private readonly List<Image> pips = new List<Image>();

    /// <summary>Shows <paramref name="total"/> pips under the ring, <paramref name="filled"/> of them lit; 0 hides them.</summary>
    public void SetPips(int filled, int total)
    {
        const float pip = 20f, gap = 10f;
        while (pips.Count < total)
        {
            RectTransform rect = CodeUI.Box("Pip", stage, new Vector2(0.5f, 0f), new Vector2(pip, pip), Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Feel.Glow;
            image.raycastTarget = false;
            pips.Add(image);
        }

        float width = total * pip + (total - 1) * gap;
        for (int i = 0; i < pips.Count; i++)
        {
            bool shown = i < total;
            pips[i].enabled = shown;
            if (!shown)
            {
                continue;
            }

            pips[i].rectTransform.anchoredPosition = new Vector2(-width * 0.5f + pip * 0.5f + i * (pip + gap), -10f);
            bool held = i < filled;
            pips[i].color = held ? Color.Lerp(tint, Color.white, 0.35f) : HearthKit.Faded(tint, i == filled ? 0.7f : 0.35f);
            pips[i].rectTransform.localScale = Vector3.one * (held ? 1.15f : i == filled ? 1f : 0.8f);
        }
    }

    /// <summary>
    /// Makes <paramref name="apply"/> a held button for this stage: <paramref name="can"/> says whether it may be done now,
    /// <paramref name="does"/> does it, and <paramref name="what"/> names its moment and the line shown when it is done.
    /// </summary>
    public void Attach(Button apply, Func<bool> can, Action does, Func<(AltarMoment, string)> what)
    {
        button = apply;
        canCommit = can;
        commit = does;
        describe = what;
        // The fill that sweeps across the button while it is held, over its face and under its words.
        RectTransform fillRect = CodeUI.Stretch("Hold Fill", apply.transform);
        fillRect.SetSiblingIndex(0);
        holdFill = fillRect.gameObject.AddComponent<Image>();
        holdFill.sprite = WhiteSprite;
        holdFill.color = HearthKit.Faded(UIStyle.Current.AccentBright, 0.55f);
        holdFill.type = Image.Type.Filled;
        holdFill.fillMethod = Image.FillMethod.Horizontal;
        holdFill.fillAmount = 0f;
        holdFill.raycastTarget = false;
        apply.onClick.RemoveAllListeners();
        var hold = apply.gameObject.AddComponent<AltarHoldButton>();
        hold.Stage = this;
    }

    internal void BeginHold(bool byItself)
    {
        if (holding || canCommit == null || !canCommit() || !button.interactable)
        {
            return;
        }

        holding = true;
        autoHold = byItself;
        held = 0f;
        holdingMoment = describe != null ? describe().moment : AltarMoment.Small;
        Play(Feel.Charge);
    }

    internal void EndHold()
    {
        if (!holding || autoHold)
        {
            return;
        }

        holding = false;
        if (held > 0.05f)
        {
            AudioManager.Stop(Feel.Charge);
            Play(Feel.Cancel);
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float duration = Feel.HoldFor(holdingMoment);
        if (holding)
        {
            held += dt;
            if (held >= duration)
            {
                holding = false;
                Commit();
            }
        }
        else
        {
            // Drains back faster than it filled.
            held = Mathf.Max(0f, held - dt * 3f);
        }

        float charge = Mathf.Clamp01(held / Mathf.Max(0.01f, duration));
        if (holdFill != null)
        {
            holdFill.fillAmount = charge;
        }

        float light = Mathf.Clamp01(GameOptions.ScreenFlash.Value);
        // The ring turns, faster with the charge and for a while after a commit; the glow breathes in the subject's colour.
        float afterglow = sinceCommit >= 0f ? Mathf.Clamp01(1f - sinceCommit / 1.2f) : 0f;
        ring.rectTransform.Rotate(0f, 0f, -Feel.RingTurn * (1f + charge * 4f + afterglow * 6f + (Working > 0f ? 2f : 0f)) * dt);
        ring.color = HearthKit.Faded(tint, 0.35f + charge * 0.45f + afterglow * 0.3f);
        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.6f);
        glow.color = HearthKit.Faded(tint, (0.18f + breathe * 0.08f + charge * 0.35f + afterglow * 0.4f) * Mathf.Max(0.35f, light));
        glow.rectTransform.localScale = Vector3.one * (1f + charge * 0.15f);
        picture.rectTransform.localScale = Vector3.one * (1f + charge * 0.06f + Punch(sinceCommit));

        AnimateCommit(dt, light);
        AnimateSparks(dt);
        AnimateResult(dt);
    }

    private void Commit()
    {
        (AltarMoment moment, string line) = describe != null ? describe() : (AltarMoment.Small, "");
        AudioManager.Stop(Feel.Charge);
        commit?.Invoke();
        lastMoment = moment;
        sinceCommit = 0f;
        Play(Feel.CommitFor(moment));
        Burst(Feel.SparksFor(moment));
        ShowResult(line, true);
    }

    private void ShowResult(string line, bool ticks)
    {
        result.text = line ?? "";
        foreach (Image rule in resultRules)
        {
            rule.color = HearthKit.Faded(tint, 0.85f);
        }
        resultTicks = ticks && !string.IsNullOrEmpty(line);
        resultAge = string.IsNullOrEmpty(line) ? -1f : 0f;
        resultCard.alpha = 0f;
    }

    // The picture punched up and back: up fast, a little past rest, and settled.
    private static float Punch(float since)
    {
        if (since < 0f || since > 0.45f)
        {
            return 0f;
        }

        return since < 0.08f ? since / 0.08f * 0.14f : 0.14f * Mathf.Exp(-(since - 0.08f) * 9f) * Mathf.Cos((since - 0.08f) * 22f);
    }

    private void AnimateCommit(float dt, float light)
    {
        if (sinceCommit < 0f)
        {
            return;
        }

        sinceCommit += dt;
        float strength = lastMoment == AltarMoment.Milestone ? 1f : lastMoment == AltarMoment.Large ? 0.7f : 0.45f;
        // A flash of white-gold, quick in and slower out.
        float flashAlpha = sinceCommit < 0.06f ? sinceCommit / 0.06f : Mathf.Max(0f, 1f - (sinceCommit - 0.06f) / 0.4f);
        flash.color = new Color(1f, 0.95f, 0.8f, flashAlpha * 0.75f * strength * light);
        // A ring of light running out from the picture.
        float wave = Mathf.Clamp01(sinceCommit / 0.65f);
        shock.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.35f, 1f - (1f - wave) * (1f - wave));
        shock.color = HearthKit.Faded(Color.Lerp(tint, Color.white, 0.4f), (1f - wave) * strength * Mathf.Max(0.3f, light));
        // The stage alone shakes a little, the more the working matters.
        float shake = sinceCommit < 0.28f ? (1f - sinceCommit / 0.28f) * (lastMoment == AltarMoment.Milestone ? 10f : lastMoment == AltarMoment.Large ? 6f : 3f)
            * GameOptions.ScreenShake.Value : 0f;
        stage.anchoredPosition = stageHome + (shake > 0f ? new Vector2(Mathf.Sin(sinceCommit * 90f), Mathf.Cos(sinceCommit * 73f)) * shake : Vector2.zero);
        if (sinceCommit > 2.5f)
        {
            sinceCommit = -1f;
            stage.anchoredPosition = stageHome;
        }
    }

    private void Burst(int count)
    {
        float light = Mathf.Clamp01(GameOptions.ScreenFlash.Value);
        count = Mathf.RoundToInt(count * Mathf.Max(0.25f, light));
        Color colour = Color.Lerp(tint, Color.white, 0.35f);
        for (int i = 0; i < count; i++)
        {
            Spark spark = sparks.Find(s => s.Life <= 0f);
            if (spark == null)
            {
                RectTransform rect = CodeUI.Box("Spark", stage, new Vector2(0.5f, 0.5f), new Vector2(18f, 18f), Vector2.zero);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = Feel.Spark;
                image.raycastTarget = false;
                spark = new Spark { Image = image };
                sparks.Add(spark);
            }

            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float speed = UnityEngine.Random.Range(140f, 420f);
            spark.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            spark.Age = 0f;
            spark.Life = UnityEngine.Random.Range(0.5f, 1.1f);
            spark.Image.rectTransform.anchoredPosition = spark.Velocity.normalized * 20f;
            spark.Image.rectTransform.localScale = Vector3.one * UnityEngine.Random.Range(0.6f, 1.4f);
            spark.Image.color = colour;
            spark.Image.enabled = true;
            spark.Image.transform.SetAsLastSibling();
        }
    }

    private void AnimateSparks(float dt)
    {
        foreach (Spark spark in sparks)
        {
            if (spark.Life <= 0f)
            {
                continue;
            }

            spark.Age += dt;
            if (spark.Age >= spark.Life)
            {
                spark.Life = 0f;
                spark.Image.enabled = false;
                continue;
            }

            // Out fast, slowing, and drifting up a little as embers do.
            spark.Velocity *= Mathf.Exp(-3.2f * dt);
            spark.Velocity.y += 60f * dt;
            RectTransform rect = spark.Image.rectTransform;
            rect.anchoredPosition += spark.Velocity * dt;
            Color c = spark.Image.color;
            c.a = 1f - spark.Age / spark.Life;
            spark.Image.color = c;
        }
    }

    private void AnimateResult(float dt)
    {
        if (resultAge < 0f)
        {
            return;
        }

        float before = resultAge;
        resultAge += dt;
        // Rises into place, ticks as it lands, stays, fades.
        float rise = Mathf.Clamp01(resultAge / 0.3f);
        if (before < 0.3f && resultAge >= 0.3f && resultTicks)
        {
            Play(Feel.Tick);
        }

        float fade = resultAge < 2.6f ? 1f : Mathf.Max(0f, 1f - (resultAge - 2.6f) / 0.6f);
        resultCard.alpha = rise * fade;
        RectTransform plate = (RectTransform)resultCard.transform;
        plate.anchoredPosition = new Vector2(plate.anchoredPosition.x, resultCardY - (1f - rise) * 14f);
        if (fade <= 0f)
        {
            resultAge = -1f;
            result.text = "";
        }
    }

    /// <summary>The stage's answer without a hold (a batch put in the purifying queue): light, sparks and a line, no sound of its own.</summary>
    public void Pulse(AltarMoment moment, string line)
    {
        lastMoment = moment;
        sinceCommit = 0f;
        Burst(Feel.SparksFor(moment));
        // Purifying is done often: its own sound carries it, no tick on top.
        ShowResult(line, false);
    }

    /// <summary>True while the result card is up.</summary>
    public bool ShowingResult => resultAge >= 0f;

    /// <summary>A working under way (the purifying queue), 0 to 1: the ring turns with it.</summary>
    public float Working { get; set; }

    /// <summary>A choice laid on the stone: the select sound, and the ring's glow rises a moment.</summary>
    public void Selected()
    {
        Play(Feel.Select);
        sinceCommit = sinceCommit < 0f ? 1.0f : sinceCommit;
    }

    private void Play(SoundDefinition sound)
    {
        if (sound != null)
        {
            AudioManager.Play(sound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }
    }
}

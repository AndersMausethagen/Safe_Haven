using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The countdown HUD for raids and quests whose timers are marked Show Timer In Hud. With no timer it hides its Canvas,
/// not its GameObject, so it can notice the next one. In a raid it answers two questions and leaves the rest to the maps: how long
/// is left (turning orange and then pulsing red) and which way out (the nearest open exit and its arrow, or how an extraction under
/// way is going). Under that, one line of news at a time, for what the message feed doesn't say. The danger stage stays once the raid's name has gone. Above the vitals:
/// the companion's health and what would be lost if the raid failed now. Every exit, rumour and open event is on the minimap and
/// the world map (MinimapHud, WorldMapHud). A banner gives the outcome when the raid ends. Everything shown comes from the
/// RaidDirector and the extraction points, so it only reads.
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class RaidHudView : MonoBehaviour
{
    private const float NameSeconds = 8f;
    private const float ArrowGap = 18f;
    private const float RumourHintAfter = 3f;

    [SerializeField] private TMP_Text raidNameLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text extractionLabel;
    [Tooltip("Optional. One line at a time of what the message feed doesn't say: where an event opened, a rumour's site found, how long a downed companion has left.")]
    [SerializeField, FormerlySerializedAs("exitsLabel")] private TMP_Text alertLabel;
    [Tooltip("Optional. What would be lost if the raid failed now: everything carried but the safe pocket and deployed gear. Empty when that is nothing.")]
    [SerializeField] private TMP_Text riskLabel;
    [Tooltip("Optional. The companion on this raid and how they are.")]
    [SerializeField] private TMP_Text companionLabel;
    [Tooltip("Seconds between recounts of what is at risk; counting snapshots the player's belongings.")]
    [SerializeField, Min(0.1f)] private float riskRefreshSeconds = 1f;
    [Tooltip("Optional. Turned to point at the nearest open extraction as the player sees it; its graphic should point up. Sits to the left of the way line.")]
    [SerializeField] private RectTransform extractionArrow;
    [Tooltip("Shown when the raid ends.")]
    [SerializeField] private GameObject banner;
    [SerializeField] private TMP_Text bannerTitle;
    [SerializeField] private TMP_Text bannerSubtitle;
    [Tooltip("Seconds left at which the timer turns to the warning color.")]
    [SerializeField, Min(0f)] private float warningTime = 120f;
    [Tooltip("Seconds left at which the timer turns to the critical color and pulses.")]
    [SerializeField, Min(0f)] private float criticalTime = 60f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.72f, 0.3f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.36f, 0.3f);
    [SerializeField] private Color extractedColor = new Color(0.55f, 0.92f, 0.55f);
    [SerializeField] private Color stageColor = new Color(1f, 0.6f, 0.3f);

    private readonly HashSet<RaidEventSite> announced = new HashSet<RaidEventSite>();
    private RaidAlertLine alerts = new RaidAlertLine();
    private RaidDirector director;
    private float nextRiskRefresh;
    private float nextAlertsRefresh;
    private float nameUntil;
    private float timerSize;
    private bool rumoursHinted;
    private Canvas canvas;

    private void OnEnable()
    {
        timerSize = timerLabel.fontSize;
        canvas = GetComponent<Canvas>();
        if (banner != null)
        {
            banner.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.Ended -= OnRaidEnded;
            director.StageChanged -= OnStageChanged;
            director = null;
        }
    }

    private void Update()
    {
        // The director may wake after this view, so it is picked up on the first frame it exists.
        if (director == null && RaidDirector.Current != null)
        {
            director = RaidDirector.Current;
            raidNameLabel.color = UIStyle.Current.AccentFor(true);
            director.Ended += OnRaidEnded;
            director.StageChanged += OnStageChanged;
            alerts = new RaidAlertLine();
            announced.Clear();
            rumoursHinted = false;
        }

        if (director == null)
        {
            bool show = QuestTimers.TryGetHudTimer(out QuestDefinition quest, out float remaining, out float duration);
            canvas.enabled = show;
            if (!show) return;
            raidNameLabel.text = quest.DisplayName.ToUpperInvariant();
            ShowTime(remaining, Mathf.Min(warningTime, duration * 0.5f), Mathf.Min(criticalTime, duration * 0.2f));
            QuestLog.Entry entry = GameSession.Quests.Get(quest);
            string objective = "";
            for (int i = 0; i < entry.Counts.Length; i++)
                if (entry.IsObjectiveOpen(i))
                {
                    objective = quest.Objectives[i].Describe();
                    break;
                }
            extractionLabel.text = objective;
            SetText(alertLabel, "");
            SetText(riskLabel, "");
            SetText(companionLabel, "");
            PointArrowAt(null);
            if (banner != null) banner.SetActive(false);
            return;
        }

        canvas.enabled = true;
        RaidSession session = director.Session;
        ShowTime(session.TimeRemaining, warningTime, criticalTime);
        ShowRaidName();
        if (session.IsOver)
            timerLabel.color = session.Outcome == RaidOutcome.Extracted ? extractedColor : criticalColor;
        ExtractionPoint target = null;
        extractionLabel.text = session.IsOver ? "" : DescribeExtraction(out target);
        PointArrowAt(target);
        if (session.IsOver)
        {
            SetText(alertLabel, "");
            SetText(companionLabel, "");
        }
        else if (Time.unscaledTime >= nextAlertsRefresh)
        {
            nextAlertsRefresh = Time.unscaledTime + 0.2f;
            WatchForNews(session);
            string down = DescribeCompanionDown();
            string news = alerts.Current(Time.unscaledTime);
            SetText(alertLabel, down.Length > 0 ? down : news);
            SetText(companionLabel, DescribeCompanion());
        }

        if (session.IsOver)
        {
            SetText(riskLabel, "");
        }
        else if (riskLabel != null && Time.unscaledTime >= nextRiskRefresh)
        {
            nextRiskRefresh = Time.unscaledTime + riskRefreshSeconds;
            riskLabel.text = DescribeRisk();
        }
    }

    private void OnStageChanged(int index, RaidStage stage)
    {
        nameUntil = Time.unscaledTime + NameSeconds;
    }

    // The raid's name when the player arrives and when things change; after that only the stage, if there is one.
    private void ShowRaidName()
    {
        RaidStage stage = director.CurrentStage;
        string stageText = stage == null ? "" : $"<color=#{ColorUtility.ToHtmlStringRGB(stageColor)}>{stage.DisplayName.ToUpperInvariant()}</color>";
        if (director.Session.Elapsed >= NameSeconds && Time.unscaledTime >= nameUntil)
        {
            SetText(raidNameLabel, stageText);
            return;
        }

        string raid = director.Session.Definition.DisplayName.ToUpperInvariant();
        SetText(raidNameLabel, stage == null ? raid : $"{raid}  ·  {stageText}");
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value)
        {
            label.text = value;
        }
    }

    private void WatchForNews(RaidSession session)
    {
        GameObject player = director.Player;
        if (player == null)
        {
            return;
        }

        Vector3 position = player.transform.position;
        if (!director.TryGetComponent(out RaidEventDirector events))
        {
            return;
        }

        if (!rumoursHinted && session.Elapsed >= RumourHintAfter)
        {
            rumoursHinted = true;
            int rumours = events.PinnedSites.Count;
            if (rumours > 0 && !WorldMapHud.UsedOnARaid)
            {
                string key = GameInput.KeyLabel(GameInput.WorldMap);
                string on = string.IsNullOrEmpty(key) ? "on your map" : $"on your map ({key})";
                alerts.Post(rumours == 1 ? $"A rumour is marked {on}" : $"{rumours} rumours are marked {on}", 8f);
            }
        }

        foreach (RaidEventSite site in events.PinnedSites)
        {
            if (site != null && !site.IsResolved && site.IsFoundFrom(position) && announced.Add(site))
            {
                alerts.Post($"<color=#FFD27A>{site.DisplayName} is close by</color>");
            }
        }

        foreach (RaidEventSite site in events.OpenSites)
        {
            if (site != null && !site.IsResolved && announced.Add(site))
            {
                Vector3 offset = site.transform.position - position;
                offset.y = 0f;
                alerts.Post($"<color=#DCCBF2>{site.DisplayName}  {Mathf.RoundToInt(offset.magnitude)} m  {SideOf(offset)}</color>");
            }
        }
    }

    private static string SideOf(Vector3 offset)
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return "";
        }

        float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(view.transform.forward, Vector3.up), offset, Vector3.up);
        return Mathf.Abs(angle) < 45f ? "ahead" : Mathf.Abs(angle) > 135f ? "behind" : angle > 0f ? "to the right" : "to the left";
    }

    private string DescribeRisk()
    {
        GameObject player = director.Player;
        if (player == null)
        {
            return "";
        }

        RaidRisk risk = RaidRisk.Of(PlayerSnapshot.Capture(player), director.Session.Loadout);
        Transport cart = PlayerTransport.Current != null ? PlayerTransport.Current.RaidCart(out _) : null;
        if (cart != null)
        {
            risk = risk.WithCargo(cart.Cargo.CountItems(), cart.CargoValue);
        }

        if (risk.IsEmpty && cart == null)
        {
            return "";
        }

        string carted = cart != null ? $"  ·  the {cart.Definition.DisplayName}" : "";
        return $"At risk: <color=#F0BC6C>{risk.Items} items ({risk.Value} value)  ·  {risk.Resources} materials{carted}</color>";
    }

    private void ShowTime(float remaining, float warning, float critical)
    {
        timerLabel.fontSize = GameOptions.LargeTimer.On ? timerSize * 1.6f : timerSize;
        timerLabel.text = RaidStats.FormatTime(remaining);
        if (remaining <= critical)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            timerLabel.color = Color.Lerp(criticalColor, Color.white, pulse * 0.35f);
        }
        else
        {
            timerLabel.color = remaining <= warning ? warningColor : normalColor;
        }
    }

    private string DescribeExtraction(out ExtractionPoint nearest)
    {
        nearest = null;
        GameObject player = director.Player;
        foreach (ExtractionPoint point in ExtractionPoint.Active)
        {
            if (!point.IsLit)
            {
                continue;
            }

            nearest = point;
            int percent = Mathf.FloorToInt(point.HoldFraction * 100f);
            switch (point.HoldState)
            {
                case ExtractionHoldState.Holding:
                case ExtractionHoldState.Done:
                    return $"<color=#BFD8A5>Holding {point.DisplayName}... {percent}%  ({point.TimeLeft:0} s)</color>";
                case ExtractionHoldState.Contested:
                    return $"<color=#F0917E>{point.DisplayName} contested! Drive them out  ({percent}%)</color>";
                default:
                    float away = player != null ? point.DistanceFrom(player.transform.position) : 0f;
                    return $"<color=#F0BC6C>Back to {point.DisplayName}'s circle  {Mathf.RoundToInt(away)} m  ({percent}%)</color>";
            }
        }

        if (player == null)
        {
            return "";
        }

        nearest = ExtractionPoint.FindNearest(player.transform.position, out float distance);
        if (nearest != null)
        {
            return RaidHudText.Way(nearest.DisplayName, distance, nearest.SecondsUntilChange, nearest.CostText);
        }

        nearest = NextToOpen();
        return nearest != null ? $"<color=#F0917E>No way out yet:</color> {nearest.DisplayName} opens in {RaidStats.FormatTime(nearest.SecondsUntilChange)}"
            : "<color=#F0917E>No open way out right now</color>";
    }

    private static ExtractionPoint NextToOpen()
    {
        ExtractionPoint next = null;
        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            if (exit.Status == ExtractionStatus.NotYetOpen && (next == null || exit.SecondsUntilChange < next.SecondsUntilChange))
            {
                next = exit;
            }
        }

        return next;
    }

    // A word and a number, so it doesn't rest on a colour.
    private string DescribeCompanion()
    {
        string along = HowTheyAre(director != null ? director.Companion : null);
        string found = director != null && director.LostOneFollows ? HowTheyAre(director.LostOne) : "";
        return along.Length > 0 && found.Length > 0 ? $"{along}  ·  {found}" : along + found;
    }

    private static string HowTheyAre(Companion companion)
    {
        if (companion == null)
        {
            return "";
        }

        return companion.IsLost ? $"<color=#FF7B6B>{companion.DisplayName} is gone</color>"
            : companion.IsDowned ? $"<color=#FF7B6B>{companion.DisplayName} is down</color>"
            : $"{companion.DisplayName}: {Mathf.RoundToInt(companion.HealthShare * 100f)}%";
    }

    // The one thing that takes the news line from everything else: it is the only moment the player can still do something.
    private string DescribeCompanionDown()
    {
        if (director == null)
        {
            return "";
        }

        string along = CountdownFor(director.Companion);
        return along.Length > 0 || !director.LostOneFollows ? along : CountdownFor(director.LostOne);
    }

    private static string CountdownFor(Companion companion)
    {
        return companion != null && companion.IsDowned && !companion.IsLost
            ? $"<color=#FF7B6B>{companion.DisplayName} is down: {companion.BleedOutLeft:0} s to help them up</color>"
            : "";
    }

    // Up on screen is the camera's forward along the ground, so the arrow turns by the angle from there to the target.
    private void PointArrowAt(ExtractionPoint target)
    {
        if (extractionArrow == null)
        {
            return;
        }

        GameObject player = director != null ? director.Player : null;
        Camera view = Camera.main;
        bool show = target != null && player != null && view != null;
        if (extractionArrow.gameObject.activeSelf != show)
        {
            extractionArrow.gameObject.SetActive(show);
        }

        if (show)
        {
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            Vector3 toTarget = Vector3.ProjectOnPlane(target.transform.position - player.transform.position, Vector3.up);
            extractionArrow.localEulerAngles = new Vector3(0f, 0f, -Vector3.SignedAngle(forward, toTarget, Vector3.up));
            extractionArrow.anchoredPosition = new Vector2(-(extractionLabel.preferredWidth * 0.5f + ArrowGap), extractionArrow.anchoredPosition.y);
        }
    }

    private void OnRaidEnded(RaidSession session)
    {
        if (banner == null)
        {
            return;
        }

        banner.SetActive(true);
        switch (session.Outcome)
        {
            case RaidOutcome.Extracted:
                bannerTitle.text = "EXTRACTED";
                bannerTitle.color = extractedColor;
                bannerSubtitle.text = "Heading home with everything you carry.";
                break;
            case RaidOutcome.TimeExpired:
                bannerTitle.text = "RAID COLLAPSED";
                bannerTitle.color = criticalColor;
                bannerSubtitle.text = "Time ran out. Everything you carried is lost.";
                break;
            default:
                bannerTitle.text = "YOU DIED";
                bannerTitle.color = criticalColor;
                bannerSubtitle.text = "Everything you carried is lost.";
                break;
        }
    }
}

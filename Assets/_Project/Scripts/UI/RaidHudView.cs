using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The raid's heads-up display: the time left, turning orange and then pulsing red as it runs low; the danger stage; how
/// far the nearest open extraction is and which way (or how long an extraction under way still takes); every exit and
/// whether it is open, opens later, has gone dark or has a cost; what would be lost if the raid failed now; and a banner
/// with the outcome when the raid ends. Everything shown comes from the RaidDirector and the extraction points, so it
/// only reads.
/// </summary>
public sealed class RaidHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text raidNameLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text extractionLabel;
    [Tooltip("Optional. Every exit in the raid: its distance and whether it is open, opens later, has gone dark or has a cost.")]
    [SerializeField] private TMP_Text exitsLabel;
    [Tooltip("Optional. What would be lost if the raid failed now: everything carried but the safe pocket and deployed gear.")]
    [SerializeField] private TMP_Text riskLabel;
    [Tooltip("Seconds between recounts of what is at risk; counting snapshots the player's belongings.")]
    [SerializeField, Min(0.1f)] private float riskRefreshSeconds = 1f;
    [Tooltip("Optional. Turned to point at the nearest open extraction as the player sees it; its graphic should point up.")]
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

    private readonly List<ExtractionPoint> exits = new List<ExtractionPoint>();
    private readonly StringBuilder text = new StringBuilder();
    private RaidDirector director;
    private float nextRiskRefresh;
    private float nextExitsRefresh;
    private float timerSize;

    private void OnEnable()
    {
        timerSize = timerLabel.fontSize;
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
            director.Ended += OnRaidEnded;
            director.StageChanged += OnStageChanged;
            ShowRaidName();
        }

        if (director == null)
        {
            timerLabel.text = "-:--";
            extractionLabel.text = "";
            SetText(exitsLabel, "");
            SetText(riskLabel, "");
            PointArrowAt(null);
            return;
        }

        RaidSession session = director.Session;
        timerLabel.fontSize = GameOptions.LargeTimer.On ? timerSize * 1.6f : timerSize;
        timerLabel.text = RaidStats.FormatTime(session.TimeRemaining);
        timerLabel.color = TimerColor(session);
        ExtractionPoint target = null;
        extractionLabel.text = session.IsOver ? "" : DescribeExtraction(out target);
        PointArrowAt(target);
        if (session.IsOver)
        {
            SetText(exitsLabel, "");
        }
        else if (exitsLabel != null && Time.unscaledTime >= nextExitsRefresh)
        {
            nextExitsRefresh = Time.unscaledTime + 0.2f;
            SetText(exitsLabel, DescribeExits());
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
        ShowRaidName();
    }

    private void ShowRaidName()
    {
        string raid = director.Session.Definition.DisplayName.ToUpperInvariant();
        RaidStage stage = director.CurrentStage;
        raidNameLabel.text = stage == null ? raid
            : $"{raid}  ·  <color=#{ColorUtility.ToHtmlStringRGB(stageColor)}>{stage.DisplayName.ToUpperInvariant()}</color>";
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value)
        {
            label.text = value;
        }
    }

    // One line per exit, nearest first: "Hollow Oak  32 m  opens in 1:20".
    private string DescribeExits()
    {
        GameObject player = director.Player;
        if (player == null)
        {
            return "";
        }

        Vector3 position = player.transform.position;
        exits.Clear();
        exits.AddRange(ExtractionPoint.Active);
        exits.Sort((a, b) => a.DistanceFrom(position).CompareTo(b.DistanceFrom(position)));
        text.Clear();
        foreach (ExtractionPoint exit in exits)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            int distance = Mathf.RoundToInt(exit.DistanceFrom(position));
            switch (exit.Status)
            {
                case ExtractionStatus.NotYetOpen:
                    text.Append($"<color=#D9D2C4>{exit.DisplayName}  {distance} m  opens in {RaidStats.FormatTime(exit.SecondsUntilChange)}</color>");
                    break;
                case ExtractionStatus.Closed:
                    text.Append($"<color=#E09A88><s>{exit.DisplayName}</s>  gone dark</color>");
                    break;
                default:
                    float closesIn = exit.SecondsUntilChange;
                    string color = closesIn >= 0f && closesIn <= 60f ? "#F0BC6C" : "#BFD8A5";
                    text.Append($"<color={color}>{exit.DisplayName}  {distance} m");
                    if (exit.IsLit)
                    {
                        text.Append($"  lit {Mathf.FloorToInt(exit.HoldFraction * 100f)}%");
                    }

                    if (closesIn >= 0f)
                    {
                        text.Append($"  closes in {RaidStats.FormatTime(closesIn)}");
                    }

                    text.Append("</color>");
                    break;
            }

            if (exit.CostItem != null)
            {
                text.Append($"  <color=#EBD29A>needs {exit.CostText}</color>");
            }
        }

        AppendEventSites(position);
        return text.ToString();
    }

    // Raid events waiting to be reached, with which way they lie as the player sees it: "Overturned Wagon  45 m  ahead".
    private void AppendEventSites(Vector3 position)
    {
        if (!director.TryGetComponent(out RaidEventDirector events))
        {
            return;
        }

        AppendSites(events.PinnedSites, position, "#FFD27A");
        AppendSites(events.OpenSites, position, "#DCCBF2");
    }

    private void AppendSites(IReadOnlyList<RaidEventSite> sites, Vector3 position, string colour)
    {
        foreach (RaidEventSite site in sites)
        {
            if (site == null || site.IsResolved)
            {
                continue;
            }

            Vector3 offset = site.transform.position - position;
            offset.y = 0f;
            text.Append($"\n<color={colour}>{site.DisplayName}  {Mathf.RoundToInt(offset.magnitude)} m  {SideOf(offset)}</color>");
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
            return "<color=#D9D2C4>Nothing at risk yet</color>";
        }

        string carted = cart != null ? $"  ·  the {cart.Definition.DisplayName}" : "";
        return $"At risk: <color=#F0BC6C>{risk.Items} items ({risk.Value} value)  ·  {risk.Resources} materials{carted}</color>";
    }

    private Color TimerColor(RaidSession session)
    {
        if (session.IsOver)
        {
            return session.Outcome == RaidOutcome.Extracted ? extractedColor : criticalColor;
        }

        float remaining = session.TimeRemaining;
        if (remaining <= criticalTime)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            return Color.Lerp(criticalColor, Color.white, pulse * 0.35f);
        }

        return remaining <= warningTime ? warningColor : normalColor;
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
        return nearest != null ? $"Nearest open: {nearest.DisplayName}  {Mathf.RoundToInt(distance)} m" : "<color=#F0917E>No open way out right now</color>";
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

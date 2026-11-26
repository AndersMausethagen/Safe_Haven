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
    private int shownSecond = -1;
    private float shownGather = 1f;
    private bool shownOvertime;
    private RaidStage namedStage;
    private RaidDirector namedFor;
    private string stageText = "";
    private string raidAndStageText = "";
    private QuestDefinition timedQuest;
    private int timedObjective = -1;
    private int timedKeys = -1;
    private string timedName = "";
    private string timedText = "";
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
            ShowTime(remaining, Mathf.Min(warningTime, duration * 0.5f), Mathf.Min(criticalTime, duration * 0.2f));
            QuestLog.Entry entry = GameSession.Quests.Get(quest);
            int open = -1;
            for (int i = 0; i < entry.Counts.Length; i++)
                if (entry.IsObjectiveOpen(i))
                {
                    open = i;
                    break;
                }
            // Made again only when the quest, its open step or the keys change (WithRebinds runs a regex over the line).
            if (quest != timedQuest || open != timedObjective || GameInput.KeysVersion != timedKeys)
            {
                timedQuest = quest;
                timedObjective = open;
                timedKeys = GameInput.KeysVersion;
                timedName = quest.DisplayName.ToUpperInvariant();
                timedText = open >= 0 ? GameInput.WithRebinds(quest.Objectives[open].Describe()) : "";
            }
            SetText(raidNameLabel, timedName);
            SetText(extractionLabel, timedText);
            SetText(alertLabel, "");
            SetText(riskLabel, "");
            SetText(companionLabel, "");
            PointArrowAt(null);
            if (banner != null) banner.SetActive(false);
            return;
        }

        canvas.enabled = true;
        RaidSession session = director.Session;
        // A boss encounter shows its boss's enrage instead of the raid clock, and what to do instead of the way home until it falls.
        EncounterArena arena = session.Definition.IsEncounter ? EncounterArena.Current : null;
        if (arena != null)
            ShowEncounterTime(arena);
        else if (director.InOvertime)
            ShowOvertime();
        else
            ShowTime(session.TimeRemaining, warningTime, criticalTime, director.StageGather);
        ShowRaidName();
        if (session.IsOver)
            timerLabel.color = session.Outcome == RaidOutcome.Extracted ? extractedColor : criticalColor;
        ExtractionPoint target = null;
        // A lit way out comes first even before the boss falls: turning back at the arrival stone shows its hold, not the arrival line.
        SetText(extractionLabel, session.IsOver ? "" : arena != null && !arena.BossFallen && !AnyLit() ? DescribeEncounter(arena) : DescribeExtraction(out target));
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
        // Both lines made when the stage changes, not every frame.
        RaidStage stage = director.CurrentStage;
        if (stage != namedStage || namedFor != director)
        {
            namedStage = stage;
            namedFor = director;
            stageText = stage == null ? "" : $"<color=#{ColorUtility.ToHtmlStringRGB(stageColor)}>{stage.DisplayName.ToUpperInvariant()}</color>";
            // The gold badge's meaning and its cost, side by side (the user, 2026-11-22: "make the bonus clear... make the downside apparent").
            if (stage != null && stage.GatherBonus > 0f)
            {
                stageText += $"  ·  <color={UIStyle.Current.AccentHex}>×{1f + stage.GatherBonus:0.##} gathering</color>" +
                             $"  <color={UIStyle.Current.WarningHex}>tools wear more</color>";
            }
            string raid = director.Session.Definition.DisplayName.ToUpperInvariant();
            raidAndStageText = stage == null ? raid : $"{raid}  ·  {stageText}";
        }

        SetText(raidNameLabel, director.Session.Elapsed >= NameSeconds && Time.unscaledTime >= nameUntil ? stageText : raidAndStageText);
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
                alerts.Post($"<color={UIStyle.Current.AccentHex}>{site.DisplayName} is close by</color>");
            }
        }

        foreach (RaidEventSite site in events.OpenSites)
        {
            if (site != null && !site.IsResolved && announced.Add(site))
            {
                Vector3 offset = site.transform.position - position;
                offset.y = 0f;
                // What is at stake, so the walk is a choice (Docs/RaidEventsPlan_2026-11-26.md E1).
                string stakes = site.Definition != null && !site.Definition.Stakes.IsEmpty
                    ? "  ·  " + site.Definition.Stakes.Words(UIStyle.Current.AccentHex, UIStyle.Current.WarningHex, UIStyle.Current.MutedHex)
                    : "";
                alerts.Post($"<color={UIStyle.Current.TextHex}>{site.DisplayName}  {Mathf.RoundToInt(offset.magnitude)} m  {SideOf(offset)}</color>{stakes}");
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

        string mist = DescribeMist();
        RaidRisk risk = RaidRisk.Of(PlayerSnapshot.Capture(player), director.Session.Loadout);
        Transport cart = PlayerTransport.Current != null ? PlayerTransport.Current.RaidCart(out _) : null;
        if (cart != null)
        {
            risk = risk.WithCargo(cart.Cargo.CountItems(), cart.CargoValue);
        }

        if (risk.IsEmpty && cart == null)
        {
            return mist;
        }

        string carted = cart != null ? $"  ·  the {cart.Definition.DisplayName}" : "";
        string atRisk = $"At risk: <color={UIStyle.Current.WarningHex}>{risk.Items} items ({risk.Value} value)  ·  {risk.Resources} materials{carted}</color>";
        return mist.Length > 0 ? atRisk + "\n" + mist : atRisk;
    }

    // Mist-touched: what staying is costing the player now (RaidDirector.MistNow), each stat once.
    private string DescribeMist()
    {
        IReadOnlyList<StatBonus> mist = director.MistNow;
        if (mist.Count == 0)
        {
            return "";
        }

        var parts = new List<string>(mist.Count);
        foreach (StatBonus bonus in mist)
        {
            parts.Add(bonus.Describe());
        }

        return $"<color={UIStyle.Current.WarningHex}>Mist-touched: {string.Join(", ", parts)}</color>";
    }

    // Out of time, portals open: how long before they close, pulsing.
    private void ShowOvertime()
    {
        timerLabel.fontSize = GameOptions.LargeTimer.On ? timerSize * 1.6f : timerSize;
        float left = director.OvertimeLeft;
        int second = Mathf.Max(0, Mathf.CeilToInt(left - 0.001f));
        if (second != shownSecond || !shownOvertime)
        {
            shownSecond = second;
            shownOvertime = true;
            timerLabel.text = $"<size=55%>PORTALS CLOSE</size> {RaidStats.FormatTime(left)}";
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        timerLabel.color = Color.Lerp(criticalColor, Color.white, pulse * 0.35f);
    }

    private void ShowTime(float remaining, float warning, float critical, float gather = 1f)
    {
        timerLabel.fontSize = GameOptions.LargeTimer.On ? timerSize * 1.6f : timerSize;
        // The clock shows whole seconds: written when the second changes, not a new string every frame. Beside it, gold, what the stage
        // adds to gathering (RaidStage › Gather Bonus).
        int second = Mathf.Max(0, Mathf.CeilToInt(remaining - 0.001f));
        if (second != shownSecond || !Mathf.Approximately(gather, shownGather) || shownOvertime)
        {
            shownSecond = second;
            shownGather = gather;
            shownOvertime = false;
            timerLabel.text = gather > 1.001f
                ? $"{RaidStats.FormatTime(remaining)} <size=55%><color={UIStyle.Current.AccentHex}>×{gather:0.##}</color></size>"
                : RaidStats.FormatTime(remaining);
        }
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

    // Nothing before the gate, the time to the enrage during the fight, and "ENRAGED" after it.
    private void ShowEncounterTime(EncounterArena arena)
    {
        if (!arena.IsSealed || arena.BossFallen)
        {
            shownSecond = -1;
            SetText(timerLabel, "");
            return;
        }

        if (arena.IsEnraged)
        {
            shownSecond = -1;
            SetText(timerLabel, "ENRAGED");
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            timerLabel.color = Color.Lerp(criticalColor, Color.white, pulse * 0.35f);
            return;
        }

        ShowTime(arena.EnrageIn, warningTime, criticalTime);
    }

    // A way out being held (or waiting for the player to come back into its circle).
    private static bool AnyLit()
    {
        IReadOnlyList<ExtractionPoint> points = ExtractionPoint.Active;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null && points[i].IsLit)
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeEncounter(EncounterArena arena)
    {
        if (!arena.IsSealed)
        {
            return arena.Definition != null ? arena.Definition.ArrivalLine : $"Beyond the mist: {arena.BossName}.";
        }

        return arena.IsEnraged
            ? $"<color={UIStyle.Current.DangerHex}>{arena.BossName} is enraged.</color> Bring it down."
            : $"Bring down {arena.BossName}.";
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
                    return Unchanged(point, 1, percent, Mathf.RoundToInt(point.TimeLeft)) ? lastWay
                        : lastWay = $"<color={UIStyle.Current.GoodHex}>Holding {point.DisplayName}... {percent}%  ({point.TimeLeft:0} s)</color>";
                case ExtractionHoldState.Contested:
                    return Unchanged(point, 2, percent, Mathf.RoundToInt(point.TimeLeft)) ? lastWay
                        : lastWay = $"<color={UIStyle.Current.DangerHex}>{point.DisplayName} contested! Slowed until you drive them out... {percent}%  ({point.TimeLeft:0} s)</color>";
                default:
                    int away = Mathf.RoundToInt(player != null ? point.DistanceFrom(player.transform.position) : 0f);
                    return Unchanged(point, 3, percent, away) ? lastWay
                        : lastWay = $"<color={UIStyle.Current.WarningHex}>Back to {point.DisplayName}'s circle  {away} m  ({percent}%)</color>";
            }
        }

        if (player == null)
        {
            return "";
        }

        nearest = ExtractionPoint.FindNearest(player.transform.position, out float distance);
        if (nearest != null)
        {
            return Unchanged(nearest, 4, Mathf.RoundToInt(distance), Mathf.CeilToInt(nearest.SecondsUntilChange)) ? lastWay
                : lastWay = RaidHudText.Way(nearest.DisplayName, distance, nearest.SecondsUntilChange, nearest.CostText);
        }

        nearest = NextToOpen();
        if (nearest == null)
        {
            return Unchanged(null, 6, 0, 0) ? lastWay : lastWay = $"<color={UIStyle.Current.DangerHex}>No open way out right now</color>";
        }

        return Unchanged(nearest, 5, Mathf.CeilToInt(nearest.SecondsUntilChange), 0) ? lastWay
            : lastWay = $"<color={UIStyle.Current.DangerHex}>No way out yet:</color> {nearest.DisplayName} opens in {RaidStats.FormatTime(nearest.SecondsUntilChange)}";
    }

    // The way-out line is made again only when what it says changes (its exit, state, whole metres, percent, whole seconds): it
    // was a new string every frame of every raid.
    private string lastWay = "";
    private ExtractionPoint lastWayExit;
    private (int kind, int a, int b) lastWayKey = (-1, 0, 0);

    private bool Unchanged(ExtractionPoint point, int kind, int a, int b)
    {
        if (point == lastWayExit && (kind, a, b) == lastWayKey)
        {
            return true;
        }

        lastWayExit = point;
        lastWayKey = (kind, a, b);
        return false;
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
        string along = HowTheyAre(director != null ? director.Companions.Companion : null);
        string found = director != null && director.Companions.LostOneFollows ? HowTheyAre(director.Companions.LostOne) : "";
        string who = along.Length > 0 && found.Length > 0 ? $"{along}  ·  {found}" : along + found;
        // A task an event set going (hold the bell, carry the head out): its line under the companions (RaidEventTask).
        string tasks = RaidEventTask.HudLines();
        return who.Length > 0 && tasks.Length > 0 ? $"{who}\n{tasks}" : who + tasks;
    }

    private static string HowTheyAre(Companion companion)
    {
        if (companion == null)
        {
            return "";
        }

        return companion.IsLost ? $"<color={UIStyle.Current.DangerHex}>{companion.DisplayName} is gone</color>"
            : companion.IsDowned ? $"<color={UIStyle.Current.DangerHex}>{companion.DisplayName} is down</color>"
            : $"{companion.DisplayName}: {Mathf.RoundToInt(companion.HealthShare * 100f)}%";
    }

    // The one thing that takes the news line from everything else: it is the only moment the player can still do something.
    private string DescribeCompanionDown()
    {
        if (director == null)
        {
            return "";
        }

        string along = CountdownFor(director.Companions.Companion);
        return along.Length > 0 || !director.Companions.LostOneFollows ? along : CountdownFor(director.Companions.LostOne);
    }

    private static string CountdownFor(Companion companion)
    {
        return companion != null && companion.IsDowned && !companion.IsLost
            ? $"<color={UIStyle.Current.DangerHex}>{companion.DisplayName} is down: {companion.BleedOutLeft:0} s to help them up</color>"
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
                // What a failed raid takes (PlayerSnapshot.AfterFailedRaid), as the planner and the results say it.
                bannerSubtitle.text = "Time ran out. Materials, food and finds are lost; your gear and safe pocket come home.";
                break;
            default:
                bannerTitle.text = "YOU DIED";
                bannerTitle.color = criticalColor;
                bannerSubtitle.text = "Materials, food and finds are lost; your gear and safe pocket come home.";
                break;
        }
    }
}

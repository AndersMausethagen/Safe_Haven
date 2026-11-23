using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Who does what (Docs/PeopleSystemReview_2026-11-23.md §2.4; RimWorld's Work tab): everyone who lives here down the side, every trade the
/// Haven knows across, each cell their level in that trade's skill with their passion's hearts. Their job now is rimmed in gold; a click
/// gives them a trade that has room and whose building may be learned (as in talk: ResidentNpc.GiveJob); a closed one is grey and says why
/// on hover. Opened from the Haven window's People and a resident's Overview. In one's own Haven only (the people are its).
/// </summary>
public sealed class WorkGridWindow : MonoBehaviour
{
    private const float Margin = 34f;
    // The window's scroll corners cover about 70 units: names and the last row keep clear of them.
    private const float CornerClear = 72f;
    private const float NameWidth = 190f;
    private const float CellWidth = 84f;
    private const float CellHeight = 44f;
    private const float RowStep = 50f;
    private const float Top = 168f;
    private const int MostRows = 12;
    private const int MostColumns = 13;
    private static float Width => Margin * 2f + NameWidth + MostColumns * (CellWidth + 4f);
    private static float Height => Top + MostRows * RowStep + 70f;

    private static WorkGridWindow current;

    private UIWindow window;
    private RectTransform panel;
    private RectTransform content;
    private TMP_Text hint;
    private readonly List<TMP_Text> rowNames = new List<TMP_Text>();
    private readonly List<TMP_Text> columnNames = new List<TMP_Text>();
    private readonly Cell[,] cells = new Cell[MostRows, MostColumns];
    private readonly List<ResidentRecord> people = new List<ResidentRecord>();
    private readonly List<JobDefinition> jobs = new List<JobDefinition>();
    private float nextRefresh;
    private int hoverRow = -1;
    private int hoverColumn = -1;

    private sealed class Cell
    {
        public GameObject Root;
        public Image Fill;
        public Image Rim;
        public Button Button;
        public TMP_Text Label;
        public string Why;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Opens it (in one's own Haven).</summary>
    public static void Show()
    {
        if (!Place.InOwnHaven)
        {
            GameMessages.Post("Who does what is for your own Haven.", MessageKind.Warning);
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Work Grid Window");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 206;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<WorkGridWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        // Open first, then fit (as the resident window: a hidden canvas measures nothing).
        current.window.Open();
        HearthKit.FitToScreen(current.panel, (RectTransform)current.transform, Width, Height);
        current.Refresh();
    }

    private void Build(Transform root)
    {
        float width = Width;
        float height = Height;
        panel = HearthKit.Window(root, width, height, () => window.Close(), out content);
        UIStyle style = UIStyle.Current;
        TMP_Text title = HearthKit.DisplayName(panel, Margin + 8f, Margin - 6f, 600f);
        title.text = "Who does what";
        hint = CodeUI.Paragraph("Hint", panel, Margin + 8f, 96f, width - Margin * 2f - 16f, 46f, 16f, style.Muted);
        hint.richText = true;
        for (int column = 0; column < MostColumns; column++)
        {
            // Two lines for a long trade's name ("Rune Scholar"), never cut short.
            TMP_Text name = CodeUI.Label("Job " + column, panel, Margin + NameWidth + column * (CellWidth + 4f), Top - 44f, CellWidth, 40f, 15f, style.Accent,
                TextAlignmentOptions.Bottom);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.overflowMode = TextOverflowModes.Overflow;
            columnNames.Add(name);
        }

        for (int row = 0; row < MostRows; row++)
        {
            float y = Top + row * RowStep;
            rowNames.Add(CodeUI.Label("Person " + row, panel, CornerClear, y + 8f, NameWidth + Margin - CornerClear - 4f, 28f, 18f, style.Text));
            for (int column = 0; column < MostColumns; column++)
            {
                cells[row, column] = MakeCell(Margin + NameWidth + column * (CellWidth + 4f), y, row, column, style);
            }
        }

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.WorkGrid, content.gameObject);
        window.OpenChanged += open => PlayerHold.Set(this, open);
    }

    private Cell MakeCell(float x, float y, int row, int column, UIStyle style)
    {
        RectTransform rect = CodeUI.At($"Cell {row},{column}", panel, x, y, CellWidth, CellHeight);
        var cell = new Cell { Root = rect.gameObject };
        cell.Fill = HearthKit.Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1.3f);
        cell.Rim = HearthKit.Rim(rect, 0.6f, 1.3f);
        cell.Button = rect.gameObject.AddComponent<Button>();
        cell.Button.targetGraphic = cell.Fill;
        HearthKit.Tint(cell.Button);
        cell.Button.onClick.AddListener(() => Choose(row, column));
        cell.Label = CodeUI.Label("Level", rect, 2f, 0f, CellWidth - 4f, CellHeight, 17f, style.Text, TextAlignmentOptions.Center);
        cell.Label.richText = true;
        var hover = rect.gameObject.AddComponent<UIHover>();
        hover.Entered = () =>
        {
            hoverRow = row;
            hoverColumn = column;
            Hint(row, column);
        };
        hover.Exited = () =>
        {
            hoverRow = hoverColumn = -1;
            Hint(-1, -1);
        };
        return cell;
    }

    private void Update()
    {
        if (window != null && window.IsOpen && Time.unscaledTime >= nextRefresh)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + 0.5f;
        UIStyle style = UIStyle.Current;
        IHavenView here = GameSession.Here;
        people.Clear();
        jobs.Clear();
        foreach (ResidentRecord record in here.Residents)
        {
            if (record.Resident != null && people.Count < MostRows)
            {
                people.Add(record);
            }
        }

        foreach (JobDefinition job in here.KnownJobs())
        {
            if (jobs.Count < MostColumns)
            {
                jobs.Add(job);
            }
        }

        for (int column = 0; column < MostColumns; column++)
        {
            SetText(columnNames[column], column < jobs.Count ? jobs[column].DisplayName : "");
        }

        // As big as its people and trades (at least four across, three down), fitted to the screen.
        float width = Margin + NameWidth + Mathf.Max(4, jobs.Count) * (CellWidth + 4f) + CornerClear;
        float height = Top + Mathf.Max(3, people.Count) * RowStep + CornerClear;
        if (!Mathf.Approximately(panel.sizeDelta.x, width) || !Mathf.Approximately(panel.sizeDelta.y, height))
        {
            panel.sizeDelta = new Vector2(width, height);
            hint.rectTransform.sizeDelta = new Vector2(width - Margin * 2f - 16f, 46f);
            HearthKit.FitToScreen(panel, (RectTransform)transform, width, height);
        }

        for (int row = 0; row < MostRows; row++)
        {
            ResidentRecord record = row < people.Count ? people[row] : null;
            SetText(rowNames[row], record != null ? record.Resident.DisplayName + (record.IsAway ? " (away)" : "") : "");
            List<JobDefinition> open = record != null ? here.JobsOpenTo(record.Resident) : null;
            for (int column = 0; column < MostColumns; column++)
            {
                Cell cell = cells[row, column];
                JobDefinition job = column < jobs.Count ? jobs[column] : null;
                cell.Root.SetActive(record != null && job != null);
                if (record == null || job == null)
                {
                    continue;
                }

                ProficiencyDefinition skill = job.Skill;
                int level = skill != null ? ResidentTalents.LevelOf(record, skill) : 0;
                bool holds = record.Job == job;
                bool mayTake = holds || open.Contains(job);
                bool theirTrade = System.Array.IndexOf(record.Resident.Jobs, job) >= 0;
                // A trade with no skill of its own (the Leader) shows no level: anyone may lead.
                SetText(cell.Label, skill != null ? $"{ResidentTalents.Flames(ResidentTalents.PassionOf(record, skill))}{level}" : "-");
                cell.Label.color = holds ? style.AccentBright : mayTake ? style.Text : style.Muted;
                cell.Fill.color = holds ? style.ControlOn : mayTake ? style.Control : style.Disabled;
                cell.Rim.color = holds ? style.Accent : theirTrade ? HearthKit.Faded(style.Accent, 0.6f) : HearthKit.Faded(style.Muted, 0.35f);
                cell.Button.interactable = mayTake && !holds && !record.IsAway;
                cell.Why = holds ? "Their work now."
                    : record.IsAway ? "Away on a foray."
                    : mayTake ? (theirTrade ? "Their own trade." : "Open to them.")
                    : job.Workplace != null && !BuildingGate.MayLearn(job.Workplace) ? $"The {job.Workplace.DisplayName} isn't known yet."
                    : $"No room: as many {job.DisplayName}s as there are {(job.Workplace != null ? job.Workplace.DisplayName + "s" : "places")}.";
            }
        }

        // The hovered cell keeps its words through the refresh.
        Hint(hoverRow, hoverColumn);
    }

    private static void SetText(TMP_Text label, string text)
    {
        if (label.text != text)
        {
            label.text = text;
        }
    }

    private void Hint(int row, int column)
    {
        if (row < 0 || row >= people.Count || column < 0 || column >= jobs.Count)
        {
            hint.text = $"Their level in each trade's skill; {HearthKit.InlineIcon(ResidentTalents.PassionIcon)} what they love. Gold: their work now. Click to give them a trade.";
            return;
        }

        ResidentRecord record = people[row];
        JobDefinition job = jobs[column];
        ProficiencyDefinition skill = job.Skill;
        if (skill == null)
        {
            hint.text = $"<b>{record.Resident.DisplayName}</b> as {job.DisplayName}: no skill of its own. {cells[row, column].Why}";
            return;
        }

        int level = ResidentTalents.LevelOf(record, skill);
        float speed = Mathf.Max(PeopleSettings.Current.LeastSpeed, 1f + PeopleSettings.Current.SpeedPerLevel * (level - 10));
        hint.text = $"<b>{record.Resident.DisplayName}</b> as {job.DisplayName}: {skill.DisplayName} {level} ({ResidentTalents.RankNameAt(level)}), work at x{speed:0.00}. {cells[row, column].Why}";
    }

    private void Choose(int row, int column)
    {
        if (row >= people.Count || column >= jobs.Count)
        {
            return;
        }

        ResidentNpc.GiveJob(people[row].Resident, jobs[column], Player.Current != null ? Player.Current.gameObject : null);
        nextRefresh = Time.unscaledTime + 0.1f;
    }

    private void OnDestroy() => PlayerHold.Set(this, false);
}

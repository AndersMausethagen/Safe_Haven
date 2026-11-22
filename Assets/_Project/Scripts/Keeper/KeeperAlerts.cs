using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What needs the keeper's eye, down the left of the Keeper's view under the stores (Against the Storm's and RimWorld's alerts): a
/// breach or a corruption night coming, the horn holding, a field gone dry, work ready at a station, a plan waiting for its materials,
/// two friends in a small scene, a wish for a home. Most urgent first, a few at most; each an icon and a few words, more on hover, and
/// clicked the view flies there. A plain helper; KeeperHud owns it and refreshes it twice a second.
/// </summary>
public sealed class KeeperAlerts
{
    private const int MostShown = 6;
    private const float RowHeight = 42f;
    private const float Gap = 6f;
    private const float Top = 88f;
    private const float LeastWidth = 220f;
    private const float MostWidth = 460f;

    private enum Tone
    {
        Danger,
        Warning,
        Plain,
    }

    private sealed class Alert
    {
        public string Icon;
        public string Words;
        public Tone Tone;
        public Vector3 At;
        public string TipTitle;
        public string TipWords;
        public Action Then;
    }

    private sealed class Row
    {
        public RectTransform Rect;
        public Image Rim;
        public Image Icon;
        public TMP_Text Words;
        public Alert Alert;
        public string Shown = "-";
    }

    private readonly RectTransform root;
    private readonly KeeperTip tip;
    private readonly List<Row> rows = new List<Row>();
    private readonly List<Alert> alerts = new List<Alert>();
    private Row pointed;

    public KeeperAlerts(RectTransform canvas, KeeperTip tip)
    {
        this.tip = tip;
        root = CodeUI.Box("Keeper Alerts", canvas, new Vector2(0f, 1f), new Vector2(MostWidth, 10f), new Vector2(20f, -Top));
        root.pivot = new Vector2(0f, 1f);
        for (int i = 0; i < MostShown; i++)
        {
            rows.Add(MakeRow(i));
        }
    }

    /// <summary>The column itself (for the overlap audit).</summary>
    public RectTransform Root => root;

    private Row MakeRow(int index)
    {
        UIStyle style = UIStyle.Current;
        var row = new Row();
        row.Rect = HearthKit.Card(root, "Alert", 0f, index * (RowHeight + Gap), LeastWidth, RowHeight, 0.4f, style.Panel);
        row.Rim = row.Rect.Find("Rim").GetComponent<Image>();
        Image hit = row.Rect.GetComponent<Image>();
        hit.raycastTarget = true;
        var button = row.Rect.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        HearthKit.Tint(button);
        UIHighlight.On(row.Rect.gameObject);
        button.onClick.AddListener(() =>
        {
            if (row.Alert != null && KeeperView.Current != null)
            {
                KeeperView.Current.LookAt(row.Alert.At);
                row.Alert.Then?.Invoke();
            }
        });
        var hover = row.Rect.gameObject.AddComponent<UIHover>();
        hover.Entered = () =>
        {
            pointed = row;
            ShowTip(row);
        };
        hover.Exited = () =>
        {
            if (pointed == row)
            {
                pointed = null;
                tip.Hide();
            }
        };

        row.Icon = HearthKit.Icon(row.Rect, "Alert", 10f, (RowHeight - 26f) * 0.5f, 26f);
        row.Words = CodeUI.Label("Words", row.Rect, 44f, 0f, LeastWidth - 56f, RowHeight, 16f, style.Text, TextAlignmentOptions.MidlineLeft);
        row.Words.overflowMode = TextOverflowModes.Overflow;
        row.Rect.gameObject.SetActive(false);
        return row;
    }

    /// <summary>Reads what needs attention now and shows the most urgent.</summary>
    public void Refresh()
    {
        Gather();
        UIStyle style = UIStyle.Current;
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            Alert alert = i < alerts.Count ? alerts[i] : null;
            row.Alert = alert;
            bool used = alert != null;
            if (row.Rect.gameObject.activeSelf != used)
            {
                row.Rect.gameObject.SetActive(used);
            }

            if (!used || row.Shown == alert.Icon + alert.Words + alert.Tone)
            {
                continue;
            }

            row.Shown = alert.Icon + alert.Words + alert.Tone;
            Color tone = alert.Tone == Tone.Danger ? style.Danger : alert.Tone == Tone.Warning ? style.Warning : style.Accent;
            row.Icon.sprite = style.Icon(alert.Icon);
            row.Icon.enabled = row.Icon.sprite != null;
            row.Icon.color = alert.Tone == Tone.Plain ? HearthKit.IconColour(alert.Icon) : Color.Lerp(tone, style.Accent, 0.25f);
            row.Words.text = alert.Words;
            row.Words.color = alert.Tone == Tone.Plain ? style.Text : Color.Lerp(tone, style.Text, 0.35f);
            row.Rim.color = HearthKit.Faded(tone, alert.Tone == Tone.Plain ? 0.35f : 0.8f);
            float width = Mathf.Clamp(44f + Mathf.Ceil(row.Words.GetPreferredValues(alert.Words).x) + 16f, LeastWidth, MostWidth);
            row.Rect.sizeDelta = new Vector2(width, RowHeight);
            row.Words.rectTransform.sizeDelta = new Vector2(width - 56f, RowHeight);
        }

        if (pointed != null && pointed.Alert != null)
        {
            ShowTip(pointed);
        }
    }

    private int stepped = -1;

    /// <summary>A controller's bumpers: the view flies to the next (or previous) alert, whose words show beside it.</summary>
    public void Step(int direction)
    {
        if (alerts.Count == 0 || direction == 0)
        {
            return;
        }

        stepped = stepped < 0 ? (direction > 0 ? 0 : alerts.Count - 1) : (stepped + direction + alerts.Count) % alerts.Count;
        Row row = rows[Mathf.Min(stepped, rows.Count - 1)];
        if (row.Alert != null && KeeperView.Current != null)
        {
            KeeperView.Current.LookAt(row.Alert.At);
            row.Alert.Then?.Invoke();
            ShowTip(row);
        }
    }

    private void ShowTip(Row row)
    {
        if (row.Alert != null)
        {
            tip.Show(row.Alert.TipTitle ?? row.Alert.Words, row.Alert.TipWords, row.Rect, beside: 1);
        }
    }

    // Most urgent first: the Haven threatened, the horn, then the work and the people.
    private void Gather()
    {
        alerts.Clear();
        Vector3 portal = HomePortal.Current != null ? HomePortal.Current.transform.position : Vector3.zero;
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach != null && (breach.IsActive || breach.IsPending))
        {
            Vector3 from = breach.Lanes.Count > 0 ? breach.Lanes[0].Start : portal;
            Add("Skull", breach.IsActive ? "The ward is breached" : "A breach is coming", breach.IsActive ? Tone.Danger : Tone.Warning, from,
                breach.IsActive ? "A ward breach" : "A breach is coming", breach.Hint);
        }

        CorruptionNights nights = CorruptionNights.Current;
        if (nights != null && nights.IsActive)
        {
            Vector3 side = portal + CorruptionNights.DirectionOf(nights.Bearing) * 25f;
            Add("Skull", $"Corruption from the {BreachLanes.Compass(portal, side)}", Tone.Danger, side, "A corruption night",
                "Its band comes from that side. The guards stand toward it; the Watch Post's archer shoots.");
        }
        else if (CorruptionNights.IsForetold)
        {
            Vector3 side = portal + CorruptionNights.DirectionOf(CorruptionNights.ForetoldBearing) * 25f;
            Add("Skull", $"Tonight, from the {BreachLanes.Compass(portal, side)}", Tone.Warning, side, "A corruption night is coming",
                "At nightfall it comes from that side: set posts toward it, and sound the horn when it is close.");
        }

        if (HavenHorn.IsSounding)
        {
            Add("Horn", "The horn holds", Tone.Warning, portal, "The horn", "The guards are at their posts, everyone else at the portal. Sound it again to stand them down.");
        }

        int dry = 0, ready = 0, plans = 0;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (alerts.Count >= MostShown || building == null || !building.TryGetComponent(out Construction construction))
            {
                continue;
            }

            string name = KeeperHud.NameOf(building);
            if (construction.IsPlan)
            {
                if (plans++ < 1)
                {
                    Add("Hammer", $"Plan: {name}", Tone.Plain, building.transform.position, $"The planned {name}",
                        "The builders start once the chests hold its materials; a raid's finds count toward it.");
                }

                continue;
            }

            StationQueue queue = !construction.IsBuilding && building.TryGetComponent(out CraftingStation station) ? GameSession.Here.Workshops.Find(station.Id) : null;
            if (queue == null)
            {
                continue;
            }

            if (Fields.Grows(queue) && !queue.IsIdle && queue.Water <= 0f && dry++ < 2)
            {
                Add("Drop", $"Dry: {name}", Tone.Warning, building.transform.position, $"The {name} is dry",
                    "Its crop grows at half its pace until it is watered: by rain, a Waterskin used on it, or a Farmer's trip to the water.");
            }
            else if (queue.OutputAmount > 0 && ready++ < 2)
            {
                Add("Star", $"{queue.OutputAmount} ready: {name}", Tone.Plain, building.transform.position, $"Ready at the {name}",
                    "Its worker takes it to the chests on their next trip, or collect it there yourself.");
            }
        }

        if (SmallScenes.Running(out Vector3 at, out string doing))
        {
            Add("Spark", doing, Tone.Plain, at, "A small scene", "Two friends in their free time. Click to go and hear them; those near are cheered.",
                () =>
                {
                    if (SmallScenes.Current != null)
                    {
                        SmallScenes.Current.Tell();
                    }
                });
        }

        foreach (ResidentRecord record in GameSession.Here.Residents)
        {
            string wish = alerts.Count < MostShown ? HomeWishes.WishOf(record) : null;
            ResidentLife life = wish != null ? ResidentLife.Of(record.Resident) : null;
            if (life != null)
            {
                Add("Heart", $"{record.Resident.DisplayName} wishes: {wish}", Tone.Plain, life.transform.position, $"{record.Resident.DisplayName}'s wish",
                    $"A home with \"{wish}\". Met, they thank you and leave a gift in the chests.");
                break;
            }
        }
    }

    private void Add(string icon, string words, Tone tone, Vector3 at, string tipTitle, string tipWords, Action then = null)
    {
        if (alerts.Count < MostShown)
        {
            alerts.Add(new Alert { Icon = icon, Words = words, Tone = tone, At = at, TipTitle = tipTitle, TipWords = tipWords, Then = then });
        }
    }
}

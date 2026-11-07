using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Friends at a glance in co-op, at the left edge in Hearthline's frameless style: each friend's name, a thin health line,
/// and where they are ("Down!", "34 m", or "Elsewhere" when not in the same place). Made by <see cref="CoopSession"/> while a
/// session runs; reads friends' <see cref="NetworkPlayerAvatar"/>s.
/// </summary>
public sealed class PartyHud : MonoBehaviour
{
    private const float Width = 260f;
    private const float RowHeight = 46f;
    private const float Top = -190f;
    private const float RefreshSeconds = 0.25f;

    private sealed class Row
    {
        public RectTransform Rect;
        public TMP_Text Name;
        public TMP_Text Where;
        public RectTransform Fill;
        public Image FillImage;
    }

    private readonly List<Row> rows = new List<Row>();
    private readonly List<NetworkPlayerAvatar> friends = new List<NetworkPlayerAvatar>();
    private CanvasGroup group;
    private float nextRefresh;

    public static PartyHud Current { get; private set; }

    public static PartyHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Party HUD");
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 96;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        Current = root.AddComponent<PartyHud>();
        Current.group = root.AddComponent<CanvasGroup>();
        Current.group.blocksRaycasts = false;
        Current.group.interactable = false;
        return Current;
    }

    public static void Remove()
    {
        if (Current != null)
        {
            Destroy(Current.gameObject);
            Current = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + RefreshSeconds;
        friends.Clear();
        foreach (NetworkPlayerAvatar avatar in NetworkPlayerAvatar.All)
        {
            if (avatar != null && !avatar.IsOwner && avatar.Look.Name.Length > 0)
            {
                friends.Add(avatar);
            }
        }

        // Hidden in menus and scenes without a player (loading, results).
        group.alpha = friends.Count > 0 && Player.Current != null && !GamePause.IsPaused ? 1f : 0f;
        while (rows.Count < friends.Count)
        {
            rows.Add(MakeRow(rows.Count));
        }

        UIStyle style = UIStyle.Current;
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            row.Rect.gameObject.SetActive(i < friends.Count);
            if (i >= friends.Count)
            {
                continue;
            }

            NetworkPlayerAvatar friend = friends[i];
            PlayerLookState look = friend.Look;
            bool here = friend.IsVisible;
            float health = look.Health / 255f;
            row.Name.text = look.DisplayName;
            row.Fill.anchorMax = new Vector2(here || look.Health > 0 ? health : 0f, 1f);
            row.FillImage.color = look.Downed ? style.Danger : health < 0.3f ? style.Danger : style.Good;
            if (look.Downed)
            {
                row.Where.text = "Down!";
                row.Where.color = style.Danger;
            }
            else if (here && Player.Current != null)
            {
                row.Where.text = $"{Vector3.Distance(Player.Current.transform.position, friend.Body.transform.position):0} m";
                row.Where.color = style.Muted;
            }
            else
            {
                row.Where.text = "Elsewhere";
                row.Where.color = style.Muted;
            }
        }
    }

    private Row MakeRow(int index)
    {
        UIStyle style = UIStyle.Current;
        var row = new Row();
        row.Rect = Rect("Friend", transform, new Vector2(0f, 1f), new Vector2(Width, RowHeight), new Vector2(30f + Width * 0.5f, Top - index * RowHeight));
        row.Name = Label(row.Rect, "Name", new Vector2(0f, 1f), new Vector2(Width * 0.65f, 26f), new Vector2(Width * 0.325f, -13f), 19f, style.Text, TextAlignmentOptions.Left);
        row.Where = Label(row.Rect, "Where", new Vector2(1f, 1f), new Vector2(Width * 0.35f, 26f), new Vector2(Width * -0.175f, -13f), 16f, style.Muted, TextAlignmentOptions.Right);
        RectTransform bar = Rect("Health", row.Rect, new Vector2(0.5f, 1f), new Vector2(Width, 3f), new Vector2(0f, -32f));
        bar.gameObject.AddComponent<Image>().color = style.Track;
        row.Fill = Rect("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        row.Fill.anchorMin = Vector2.zero;
        row.Fill.anchorMax = Vector2.one;
        row.Fill.offsetMin = row.Fill.offsetMax = Vector2.zero;
        row.FillImage = row.Fill.gameObject.AddComponent<Image>();
        row.FillImage.color = style.Good;
        return row;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchorAt;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    // Hearthline's text on the world: the body font with its soft shadow.
    private static TMP_Text Label(Transform parent, string name, Vector2 anchorAt, Vector2 size, Vector2 position, float fontSize, Color colour,
        TextAlignmentOptions alignment)
    {
        var text = Rect(name, parent, anchorAt, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        UIStyle.Current.OnWorld(text);
        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }
}

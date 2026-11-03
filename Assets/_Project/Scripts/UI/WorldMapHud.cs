using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shows the whole painted and explored world map, with where the player's quests are and who has quests for them, each
/// in the colour of its quest's rank. M opens and closes it; Escape also returns to play.</summary>
public sealed class WorldMapHud : MonoBehaviour
{
    private static readonly int ExploredId = Shader.PropertyToID("_Explored");
    private static readonly int FogId = Shader.PropertyToID("_Fog");
    private const float LabelCrowd = 60f;
    private const float LabelWide = 300f;

    private const string UsedOnARaidKey = "WorldMap.UsedOnARaid";

    private Rect area;
    private RectTransform canvasRect;
    private RectTransform mapRect;
    private RawImage mapImage;
    private Material material;
    private Texture2D map;
    private bool ownsMap;
    private TMP_Text playerMarker;
    private TMP_Text titleLabel;
    private bool suspendedGameplay;

    private sealed class QuestMark
    {
        public RectTransform Root;
        public Image Diamond;
        public TMP_Text Label;
    }

    private readonly System.Collections.Generic.List<QuestMark> questMarks = new System.Collections.Generic.List<QuestMark>();
    private readonly System.Collections.Generic.List<Vector3> marked = new System.Collections.Generic.List<Vector3>();

    public static WorldMapHud Current { get; private set; }
    public static bool IsOpen => Current != null;

    /// <summary>The player has opened the map on a raid, so the HUD needn't tell them what is on it. Remembered between sessions.</summary>
    public static bool UsedOnARaid => PlayerPrefs.GetInt(UsedOnARaidKey, 0) == 1;

    public static void Toggle()
    {
        if (Current != null)
        {
            Current.Close();
            return;
        }

        if (GamePause.IsPaused || (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyWindowOpen))
        {
            return;
        }

        if (!MinimapMap.TryGetArea(out Rect area))
        {
            GameMessages.Post("There is no world map in this area.", MessageKind.Warning);
            return;
        }

        MinimapSettings settings = MinimapSettings.Current;
        if (settings == null)
        {
            Debug.LogError("WorldMapHud requires MinimapSettings.");
            return;
        }

        var root = new GameObject("World Map", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        UIRoot.ConfigureScaler(root.GetComponent<CanvasScaler>());
        WorldMapHud hud = root.AddComponent<WorldMapHud>();
        hud.Build(area, settings);
        hud.Open();
    }

    private void OnEnable()
    {
        Current = this;
        GameInput.Cancel.performed += OnCancel;
    }

    private void OnDisable()
    {
        GameInput.Cancel.performed -= OnCancel;
    }

    private void OnDestroy()
    {
        if (suspendedGameplay)
        {
            GameInput.ResumeGameplay();
            suspendedGameplay = false;
        }

        if (map != null && ownsMap)
        {
            Destroy(map);
        }

        if (material != null)
        {
            Destroy(material);
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    private void Build(Rect worldArea, MinimapSettings settings)
    {
        area = worldArea;
        canvasRect = (RectTransform)transform;
        CanvasGroup group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = false;

        RectTransform backdrop = Rect("Backdrop", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image backing = backdrop.gameObject.AddComponent<Image>();
        backing.color = new Color(0.025f, 0.022f, 0.02f, 1f);
        backing.raycastTarget = true;

        RectTransform titleRect = Rect("Title", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(420f, 48f), new Vector2(0f, -42f));
        TMP_Text title = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
        title.text = "World Map";
        title.fontSize = 32f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = UIStyle.Current.Accent;
        title.raycastTarget = false;
        titleLabel = title;

        RectTransform helpRect = Rect("Help", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1100f, 34f), new Vector2(0f, 34f));
        TMP_Text help = helpRect.gameObject.AddComponent<TextMeshProUGUI>();
        string mapKey = GameInput.KeyLabel(GameInput.WorldMap);
        help.text = (RaidDirector.Current != null ? "Exits in green  •  Rumours in amber, only roughly where  •  " : "Quests in gold (main), silver (side) and bronze (optional)  •  ") + "Unexplored areas remain hidden  •  " + (string.IsNullOrEmpty(mapKey) ? "" : mapKey + " or ") + "Esc to return";
        help.fontSize = 18f;
        help.alignment = TextAlignmentOptions.Center;
        help.color = UIStyle.Current.Muted;
        help.raycastTarget = false;

        mapRect = Rect("Map", transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        mapImage = mapRect.gameObject.AddComponent<RawImage>();
        mapImage.color = Color.white;
        mapImage.raycastTarget = false;

        Shader shader = Shader.Find("Safe Haven/UI/World Map");
        if (shader == null)
        {
            Debug.LogError("World Map shader is missing.", this);
            return;
        }

        material = new Material(shader);
        material.SetColor(FogId, UIStyle.Current.Panel);
        mapImage.material = material;

        RectTransform marker = Rect("Player", mapRect, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(28f, 28f), Vector2.zero);
        playerMarker = marker.gameObject.AddComponent<TextMeshProUGUI>();
        playerMarker.text = "▲";
        playerMarker.fontSize = 26f;
        playerMarker.alignment = TextAlignmentOptions.Center;
        playerMarker.color = UIStyle.Current.Accent;
        playerMarker.raycastTarget = false;

        // The minimap has already painted this scene's ground: showing it is instant. Painting again only without one.
        Texture2D painted = MinimapHud.Current != null ? MinimapHud.Current.PaintedMap : null;
        if (painted != null)
        {
            map = painted;
            mapImage.texture = map;
        }
        else
        {
            StartCoroutine(Paint(area, settings));
        }
    }

    private void Open()
    {
        gameObject.SetActive(true);
        if (RaidDirector.Current != null)
        {
            try
            {
                PlayerPrefs.SetInt(UsedOnARaidKey, 1);
            }
            catch (PlayerPrefsException exception)
            {
                Debug.LogWarning(exception.Message);
            }
        }

        GameInput.SuspendGameplay();
        suspendedGameplay = true;
        // M stays live while the rest of play waits, so the key that opened the map also closes it.
        GameInput.WorldMap?.Enable();
    }

    private void Close()
    {
        Destroy(gameObject);
    }

    private IEnumerator Paint(Rect worldArea, MinimapSettings settings)
    {
        yield return MinimapMap.Paint(worldArea, settings, finished =>
        {
            map = finished;
            ownsMap = true;
            mapImage.texture = map;
        });
    }

    private void LateUpdate()
    {
        if (canvasRect == null || mapRect == null)
        {
            return;
        }

        Vector2 available = canvasRect.rect.size * 0.78f;
        float aspect = area.width / Mathf.Max(0.01f, area.height);
        float width = available.x;
        float height = width / aspect;
        if (height > available.y)
        {
            height = available.y;
            width = height * aspect;
        }

        mapRect.sizeDelta = new Vector2(width, height);
        // The raid doesn't stop for the map, and the map covers the clock.
        if (RaidDirector.Current != null)
        {
            titleLabel.text = $"World Map  ·  {RaidStats.FormatTime(RaidDirector.Current.Session.TimeRemaining)}";
        }

        ExplorationMap exploration = ExplorationMap.Current;
        material.SetTexture(ExploredId, exploration != null ? exploration.Texture : Texture2D.whiteTexture);
        PlaceQuestMarks(width, height);

        Player player = Player.Current;
        if (player == null)
        {
            playerMarker.gameObject.SetActive(false);
            return;
        }

        Vector2 uv = new Vector2(
            Mathf.Clamp01((player.transform.position.x - area.xMin) / area.width),
            Mathf.Clamp01((player.transform.position.z - area.yMin) / area.height));
        playerMarker.rectTransform.anchoredPosition = new Vector2((uv.x - 0.5f) * width, (uv.y - 0.5f) * height);
        playerMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -player.transform.eulerAngles.y);
        playerMarker.gameObject.SetActive(true);
    }

    // Where the quests want the player, then who else has a quest for them, unless a waypoint already marks them.
    private void PlaceQuestMarks(float width, float height)
    {
        QuestLog log = QuestManager.Instance != null ? QuestManager.Instance.Log : GameSession.Quests;
        QuestMarkerSettings settings = QuestMarkerSettings.Current;
        int used = PlaceRaidMarks(width, height);
        marked.Clear();
        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint != null && waypoint.Quest != null && waypoint.IsWanted(log))
            {
                marked.Add(waypoint.transform.position);
                ShowMark(used++, waypoint.transform.position, settings.RankColor(waypoint.Quest.Rank), waypoint.Label, width, height);
            }
        }

        foreach (QuestGiver giver in QuestGiver.Active)
        {
            if (giver == null || giver.ShownQuest == null || marked.Exists(p => (p - giver.transform.position).sqrMagnitude < 9f)
                || (giver.ShownState != QuestGiverState.Available && giver.ShownState != QuestGiverState.ReadyToTurnIn))
            {
                continue;
            }

            ShowMark(used++, giver.transform.position, settings.RankColor(giver.ShownQuest.Rank), giver.DisplayName, width, height);
        }

        for (int i = used; i < questMarks.Count; i++)
        {
            questMarks[i].Root.gameObject.SetActive(false);
        }
    }

    // Everything the HUD no longer lists: every exit and how it stands, what rumours point to (roughly) and open events.
    private int PlaceRaidMarks(float width, float height)
    {
        RaidDirector director = RaidDirector.Current;
        Player player = Player.Current;
        if (director == null || player == null)
        {
            return 0;
        }

        UIStyle style = UIStyle.Current;
        int used = 0;
        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            ExtractionStatus status = exit.Status;
            Color colour = status == ExtractionStatus.Open ? style.Good : status == ExtractionStatus.Closed ? style.Danger : style.Muted;
            ShowMark(used++, exit.transform.position, colour, RaidHudText.MapLabel(exit.DisplayName, status, exit.SecondsUntilChange, exit.CostText), width, height);
        }

        if (!director.TryGetComponent(out RaidEventDirector events))
        {
            return used;
        }

        Vector3 from = player.transform.position;
        foreach (RaidEventSite site in events.PinnedSites)
        {
            if (site != null && !site.IsResolved)
            {
                ShowMark(used++, site.MarkFrom(from), style.Accent, RaidHudText.RumourLabel(site.DisplayName, site.PlaceName), width, height);
            }
        }

        foreach (RaidEventSite site in events.OpenSites)
        {
            if (site != null && !site.IsResolved)
            {
                ShowMark(used++, site.transform.position, style.Text, site.DisplayName, width, height);
            }
        }

        return used;
    }

    private void ShowMark(int index, Vector3 position, Color colour, string label, float width, float height)
    {
        if (index == questMarks.Count)
        {
            RectTransform root = Rect("Quest Mark", mapRect, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            RectTransform shape = Rect("Diamond", root, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(16f, 16f), Vector2.zero);
            shape.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = shape.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            RectTransform words = Rect("Label", root, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(260f, 28f), new Vector2(0f, -24f));
            var text = words.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            questMarks.Add(new QuestMark { Root = root, Diamond = image, Label = text });
            playerMarker.transform.SetAsLastSibling();
        }

        QuestMark mark = questMarks[index];
        mark.Root.gameObject.SetActive(true);
        mark.Root.anchoredPosition = new Vector2(
            (Mathf.Clamp01((position.x - area.xMin) / area.width) - 0.5f) * width,
            (Mathf.Clamp01((position.z - area.yMin) / area.height) - 0.5f) * height);
        mark.Diamond.color = colour;
        mark.Label.color = colour;
        mark.Label.text = label;
        // Names that would print over each other stack instead: they are wide and low.
        int under = 0;
        for (int i = 0; i < index; i++)
        {
            Vector2 apart = questMarks[i].Root.anchoredPosition - mark.Root.anchoredPosition;
            if (Mathf.Abs(apart.x) < LabelWide && Mathf.Abs(apart.y) < LabelCrowd)
            {
                under++;
            }
        }

        mark.Label.rectTransform.anchoredPosition = new Vector2(0f, -24f - under * 20f);
    }

    private void OnCancel(InputAction.CallbackContext context) => Close();

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shows the whole painted and explored world map, with where the player's quests are and who has quests for them, each
/// in the colour of its quest's rank. Where the scene has a WorldAtlas (the Haven) its regions are drawn with their borders and
/// names, and the named places the player has found (PlaceDiscovery) with theirs. M opens and closes it; Escape also returns to play.</summary>
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
    private TMP_Text help;
    private RectTransform titleMark;
    private bool suspendedGameplay;
    private int writtenSeconds = -1;

    private sealed class QuestMark
    {
        public RectTransform Root;
        public Image Icon;
        public TMP_Text Label;
    }

    private readonly System.Collections.Generic.List<QuestMark> questMarks = new System.Collections.Generic.List<QuestMark>();
    // The atlas's regions and places, when the scene has one.
    private WorldAtlas atlas;
    private Texture2D borders;
    private readonly System.Collections.Generic.List<TMP_Text> regionLabels = new System.Collections.Generic.List<TMP_Text>();
    private readonly System.Collections.Generic.List<(WorldAtlas.Place place, RectTransform root)> placeMarks =
        new System.Collections.Generic.List<(WorldAtlas.Place, RectTransform)>();
    private static readonly Color BorderColour = new Color(0.94f, 0.86f, 0.64f, 0.7f);
    private static readonly Color RegionColour = new Color(0.94f, 0.88f, 0.72f, 0.62f);
    private static readonly Color PlaceColour = new Color(0.96f, 0.92f, 0.8f, 1f);
    private const float PlaceLabelWide = 150f;
    private const float PlaceLabelHigh = 17f;
    private readonly System.Collections.Generic.List<Vector2> placed = new System.Collections.Generic.List<Vector2>();
    private readonly System.Collections.Generic.List<(string region, RectTransform root)> stoneMarks =
        new System.Collections.Generic.List<(string, RectTransform)>();
    private readonly System.Collections.Generic.List<Vector3> marked = new System.Collections.Generic.List<Vector3>();

    public static WorldMapHud Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;
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

        if (GamePause.IsPaused || (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen))
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
        GameInput.KeysChanged += WriteHelp;
    }

    private void OnDisable()
    {
        GameInput.Cancel.performed -= OnCancel;
        GameInput.KeysChanged -= WriteHelp;
    }

    // The line along the bottom names the keys that close the map, so it follows the device in use.
    private void WriteHelp()
    {
        if (help == null)
        {
            return;
        }

        string mapKey = GameInput.KeyLabel(GameInput.WorldMap);
        help.text = (RaidDirector.Current != null ? "Doorways: exits  •  Rings: rough rumours  •  Stars: open events  •  " : "Quest names include their rank  •  Stars: places you have found  •  Hearths: boundary stones  •  ") + "Unexplored areas remain hidden  •  " + (string.IsNullOrEmpty(mapKey) ? "" : mapKey + " or ") + GameInput.Keys("{key:Back} to return");
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

        if (borders != null)
        {
            Destroy(borders);
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
        UIStyle.Current.Title(title);
        title.color = UIStyle.Current.AccentFor(RaidDirector.Current != null);
        title.raycastTarget = false;
        titleLabel = title;
        Image hearthmark = HearthlineArt.AddIcon(transform, "Hearthmark", UIStyle.Current.Hearthmark, new Vector2(0.5f, 1f),
            Vector2.one * 34f, new Vector2(-115f, -42f), title.color);
        if (hearthmark != null)
        {
            titleMark = hearthmark.rectTransform;
        }

        RectTransform helpRect = Rect("Help", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1100f, 34f), new Vector2(0f, 34f));
        help = helpRect.gameObject.AddComponent<TextMeshProUGUI>();
        WriteHelp();
        help.fontSize = 18f;
        help.alignment = TextAlignmentOptions.Center;
        help.color = UIStyle.Current.Muted;
        help.raycastTarget = false;

        mapRect = Rect("Map", transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        mapImage = mapRect.gameObject.AddComponent<RawImage>();
        mapImage.color = Color.white;
        mapImage.raycastTarget = false;

        Shader shader = settings.WorldMapShader;
        if (shader == null)
        {
            Debug.LogError("MinimapSettings has no World Map shader: assign Safe Haven/UI/World Map.", this);
            Destroy(gameObject);
            return;
        }

        material = new Material(shader);
        material.SetColor(FogId, UIStyle.Current.Panel);
        mapImage.material = material;

        BuildAtlas();

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
        WriteHelp();
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
        if (canvasRect == null || mapRect == null || material == null)
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
            // Written again once a second, as the clock turns.
            int seconds = Mathf.CeilToInt(RaidDirector.Current.Session.TimeRemaining);
            if (seconds != writtenSeconds)
            {
                writtenSeconds = seconds;
                titleLabel.text = $"World Map  ·  {RaidStats.FormatTime(RaidDirector.Current.Session.TimeRemaining)}";
            }
        }

        if (titleMark != null)
        {
            Vector2 position = titleMark.anchoredPosition;
            position.x = -(titleLabel.preferredWidth + titleMark.rect.width) * 0.5f - 18f;
            titleMark.anchoredPosition = position;
        }

        ExplorationMap exploration = ExplorationMap.Current;
        material.SetTexture(ExploredId, exploration != null ? exploration.Texture : Texture2D.whiteTexture);
        PlaceAtlas(width, height);
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
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        QuestMarkerSettings settings = QuestMarkerSettings.Current;
        int used = PlaceRaidMarks(width, height);
        // The player's own horses, by name.
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsTamed && horse.IsAlive && horse.Rider == null)
            {
                ShowMark(used++, horse.transform.position, UIStyle.Current.Accent, null, horse.HorseName, width, height);
            }
        }

        marked.Clear();
        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint != null && waypoint.Quest != null && waypoint.IsWanted(log))
            {
                marked.Add(waypoint.transform.position);
                ShowMark(used++, waypoint.transform.position, settings.RankColor(waypoint.Quest.Rank), UIStyle.Current.QuestMark,
                    QuestLabel(waypoint.Label, waypoint.Quest.Rank), width, height);
            }
        }

        foreach (QuestGiver giver in QuestGiver.Active)
        {
            if (giver == null || giver.ShownQuest == null || marked.Exists(p => (p - giver.transform.position).sqrMagnitude < 9f)
                || (giver.ShownState != QuestGiverState.Available && giver.ShownState != QuestGiverState.ReadyToTurnIn))
            {
                continue;
            }

            ShowMark(used++, giver.transform.position, settings.RankColor(giver.ShownQuest.Rank), UIStyle.Current.QuestMark,
                QuestLabel(giver.DisplayName, giver.ShownQuest.Rank), width, height);
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
            ShowMark(used++, exit.transform.position, colour, style.ExitMark,
                RaidHudText.MapLabel(exit.DisplayName, status, exit.SecondsUntilChange, exit.CostText), width, height);
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
                ShowMark(used++, site.MarkFrom(from), style.Accent, style.RumourMark,
                    RaidHudText.RumourLabel(site.DisplayName, site.PlaceName), width, height);
            }
        }

        foreach (RaidEventSite site in events.OpenSites)
        {
            if (site != null && !site.IsResolved)
            {
                ShowMark(used++, site.transform.position, style.Text, style.EventMark, site.DisplayName, width, height);
            }
        }

        return used;
    }

    public static string QuestLabel(string label, QuestRank rank) => $"{label} ({QuestMarkerSettings.RankName(rank)})";

    private void ShowMark(int index, Vector3 position, Color colour, Sprite sprite, string label, float width, float height)
    {
        if (index == questMarks.Count)
        {
            RectTransform root = Rect("Map Mark", mapRect, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            RectTransform shape = Rect("Icon", root, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(22f, 22f), Vector2.zero);
            var image = shape.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var outline = shape.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            RectTransform words = Rect("Label", root, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(300f, 28f), new Vector2(0f, -24f));
            var text = words.gameObject.AddComponent<TextMeshProUGUI>();
            UIStyle.Current.OnWorld(text);
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            questMarks.Add(new QuestMark { Root = root, Icon = image, Label = text });
            playerMarker.transform.SetAsLastSibling();
        }

        QuestMark mark = questMarks[index];
        mark.Root.gameObject.SetActive(true);
        mark.Root.anchoredPosition = new Vector2(
            (Mathf.Clamp01((position.x - area.xMin) / area.width) - 0.5f) * width,
            (Mathf.Clamp01((position.z - area.yMin) / area.height) - 0.5f) * height);
        mark.Icon.sprite = sprite;
        mark.Icon.color = colour;
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

    // The regions' borders as one picture over the map (a line wherever a cell's region differs from the one east or north of it),
    // a name for each region, and a mark for every place: hidden until it is found.
    private void BuildAtlas()
    {
        atlas = WorldAtlas.For(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        if (atlas == null || atlas.GridSize <= 0)
        {
            atlas = null;
            return;
        }

        int size = atlas.GridSize;
        borders = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Region Borders" };
        var pixels = new Color32[size * size];
        Color32 line = BorderColour;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                byte here = atlas.RegionAtCell(x, y);
                bool edge = (x + 1 < size && atlas.RegionAtCell(x + 1, y) != here) || (y + 1 < size && atlas.RegionAtCell(x, y + 1) != here);
                pixels[y * size + x] = edge ? line : new Color32(0, 0, 0, 0);
            }
        }

        borders.SetPixels32(pixels);
        borders.Apply();
        RectTransform overlay = Rect("Region Borders", mapRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var image = overlay.gameObject.AddComponent<RawImage>();
        image.texture = borders;
        image.raycastTarget = false;
        // The map shows the scene's area; the atlas covers its own (the same terrain): the part of it under the map.
        Rect covered = atlas.Area;
        image.uvRect = new Rect((area.xMin - covered.xMin) / covered.width, (area.yMin - covered.yMin) / covered.height,
            area.width / covered.width, area.height / covered.height);

        foreach (WorldAtlas.Region region in atlas.Regions)
        {
            RectTransform words = Rect("Region " + region.name, mapRect, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(360f, 40f), Vector2.zero);
            var text = words.gameObject.AddComponent<TextMeshProUGUI>();
            UIStyle.Current.OnWorld(text, true);
            text.text = region.name.ToUpperInvariant();
            // Small regions get smaller names, so the tarn's name doesn't drown the woods around it.
            text.fontSize = Mathf.Lerp(13f, 21f, Mathf.InverseLerp(0.01f, 0.2f, region.share));
            text.characterSpacing = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = RegionColour;
            text.raycastTarget = false;
            regionLabels.Add(text);
        }

        // Each region's boundary stone (Territory), once the region is known.
        foreach (WorldAtlas.Region region in atlas.Regions)
        {
            if (region.stoneAt == Vector2.zero)
            {
                continue;
            }

            RectTransform root = Rect("Stone " + region.name, mapRect, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(18f, 18f), Vector2.zero);
            var mark = root.gameObject.AddComponent<Image>();
            mark.sprite = UIStyle.Current.Hearthmark;
            mark.raycastTarget = false;
            var shade = root.gameObject.AddComponent<Outline>();
            shade.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shade.effectDistance = new Vector2(1f, -1f);
            stoneMarks.Add((region.name, root));
        }

        foreach (WorldAtlas.Place place in atlas.Places)
        {
            RectTransform root = Rect("Place " + place.name, mapRect, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            RectTransform shape = Rect("Icon", root, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(16f, 16f), Vector2.zero);
            var icon = shape.gameObject.AddComponent<Image>();
            icon.sprite = UIStyle.Current.EventMark;
            icon.color = PlaceColour;
            icon.raycastTarget = false;
            var outline = shape.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            RectTransform words = Rect("Label", root, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(260f, 24f), new Vector2(0f, -18f));
            var text = words.gameObject.AddComponent<TextMeshProUGUI>();
            UIStyle.Current.OnWorld(text);
            text.text = place.name;
            text.fontSize = 15f;
            text.fontStyle = FontStyles.Italic;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = PlaceColour;
            text.raycastTarget = false;
            placeMarks.Add((place, root));
        }
    }

    private void PlaceAtlas(float width, float height)
    {
        if (atlas == null)
        {
            return;
        }

        TerritoryState lands = GameSession.Settlement.Territory;
        UIStyle style = UIStyle.Current;
        for (int i = 0; i < regionLabels.Count && i < atlas.Regions.Count; i++)
        {
            string name = atlas.Regions[i].name;
            regionLabels[i].rectTransform.anchoredPosition = OnMap(atlas.Regions[i].label, width, height);
            // Held lands in the accent, the one the forest pushes on in warning, the wild as the map's own ink.
            bool pushed = lands.Incursion != null && lands.Incursion.Region == name;
            regionLabels[i].color = pushed ? style.Warning : lands.IsClaimed(name) ? style.Accent : RegionColour;
            regionLabels[i].text = pushed ? name.ToUpperInvariant() + "  !" : name.ToUpperInvariant();
        }

        foreach ((string region, RectTransform root) in stoneMarks)
        {
            bool known = lands.IsClaimed(region) || Territory.PlacesFound(region) > 0;
            root.gameObject.SetActive(known);
            if (known)
            {
                WorldAtlas.Region at = null;
                foreach (WorldAtlas.Region candidate in atlas.Regions)
                {
                    if (candidate.name == region)
                    {
                        at = candidate;
                    }
                }

                root.anchoredPosition = OnMap(at.stoneAt, width, height);
                root.GetComponent<Image>().color = lands.Incursion != null && lands.Incursion.Region == region ? style.Warning
                    : lands.IsClaimed(region) ? style.Accent : style.Muted;
            }
        }

        JournalRecord record = GameSession.Progress.Journal;
        placed.Clear();
        // The regions' names take their room first: a place's name keeps clear of them too.
        foreach (TMP_Text region in regionLabels)
        {
            placed.Add(region.rectTransform.anchoredPosition);
        }

        foreach ((WorldAtlas.Place place, RectTransform root) in placeMarks)
        {
            bool found = record.HasDiscovered(place.id);
            if (root.gameObject.activeSelf != found)
            {
                root.gameObject.SetActive(found);
            }

            if (!found)
            {
                continue;
            }

            root.anchoredPosition = OnMap(place.at, width, height);
            // A name that would print over one already placed goes above its star instead, then further out.
            var words = (RectTransform)root.GetChild(1);
            float[] tries = { -18f, 18f, -36f, 36f };
            Vector2 label = root.anchoredPosition + new Vector2(0f, tries[0]);
            foreach (float offset in tries)
            {
                label = root.anchoredPosition + new Vector2(0f, offset);
                if (!placed.Exists(other => Mathf.Abs(other.x - label.x) < PlaceLabelWide && Mathf.Abs(other.y - label.y) < PlaceLabelHigh))
                {
                    break;
                }
            }

            words.anchoredPosition = label - root.anchoredPosition;
            placed.Add(label);
        }
    }

    private Vector2 OnMap(Vector2 world, float width, float height) => new Vector2(
        (Mathf.Clamp01((world.x - area.xMin) / area.width) - 0.5f) * width,
        (Mathf.Clamp01((world.y - area.yMin) / area.height) - 0.5f) * height);

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

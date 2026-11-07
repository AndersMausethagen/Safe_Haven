using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The round map in the corner. The ground is painted once for the scene (MinimapMap) and moved by a shader, so it costs next to
/// nothing; it turns with the camera, so up on the map is up on the screen, and what hasn't been explored stays in the fog of
/// war (ExplorationMap). Over it go the player, what they have found (buildings, carts, dropped items, portals), their
/// companions, the quest they are on, creatures close enough to be seen, and a ward breach's lanes and attackers. In a raid
/// also its exits (a large dot, dim until it opens), rumours (a ring, only roughly where) and open events (a diamond). What the
/// player is looking for stays pinned to the rim when out of reach: this is what replaces lists of directions on the HUD. M opens the full world map. Only reads the game. Made for every scene with a player
/// (ExperienceTracker).
/// </summary>
public sealed class MinimapHud : MonoBehaviour
{
    private const string ZoomKey = "Minimap.Zoom.v2";
    private const float Margin = 26f;
    private const float RefreshSeconds = 0.25f;
    private const float ZoomSpeed = 8f;
    private const float RimInset = 7f;
    private const int Far = 1;
    private const int Hidden = 2;
    private const int ZoomSteps = 3;

    private static readonly int CentreId = Shader.PropertyToID("_Centre");
    private static readonly int SpanId = Shader.PropertyToID("_Span");
    private static readonly int AreaId = Shader.PropertyToID("_Area");
    private static readonly int TurnId = Shader.PropertyToID("_Turn");
    private static readonly int RingCentreId = Shader.PropertyToID("_RingCentre");
    private static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
    private static readonly int ExploredId = Shader.PropertyToID("_Explored");
    private static readonly int FogId = Shader.PropertyToID("_Fog");
    private static readonly int RimId = Shader.PropertyToID("_Rim");
    private static readonly int WardId = Shader.PropertyToID("_Ward");

    private sealed class Marker
    {
        public RectTransform Rect;
        public Image Image;
        public Transform Follow;
        public Vector3 Position;
        public bool Pinned;
        public bool Facing;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private MinimapSettings settings;
    private CanvasGroup group;
    private RectTransform disc;
    private RectTransform arrow;
    private RectTransform north;
    private TMP_Text northLabel;
    private RawImage ground;
    private Material material;
    private Texture2D map;
    private Sprite dot;
    private Sprite pointer;
    private Rect area;
    private int used;
    private int zoom;
    private float reach;
    private float turn;
    private float nextRefresh;
    private bool painted;
    private bool? raidTheme;

    public static MinimapHud Current { get; private set; }

    /// <summary>This scene's painted ground, or null until it is painted. Owned by the minimap; the world map only shows it.</summary>
    public Texture2D PaintedMap => painted ? map : null;

    /// <summary>Makes the map for this scene, unless it has one or there is no ground to map.</summary>
    public static MinimapHud Create()
    {
        if (Current != null || !MinimapMap.TryGetArea(out Rect found) || MinimapSettings.Current.Material == null)
        {
            return Current;
        }

        var root = new GameObject("Minimap");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        var hud = root.AddComponent<MinimapHud>();
        hud.area = found;
        hud.group = root.AddComponent<CanvasGroup>();
        hud.group.blocksRaycasts = false;
        hud.Build(root.transform);
        return hud;
    }

    /// <summary>Close in, zoomed out, hidden, and round again. Remembered between sessions.</summary>
    public void CycleZoom()
    {
        zoom = (zoom + 1) % ZoomSteps;
        SaveZoom();
    }

    /// <summary>Where a world offset from the player lies on a map turned this far (radians), in map units at this scale.</summary>
    public static Vector2 Project(Vector3 offset, float turn, float unitsPerMetre)
    {
        float sine = Mathf.Sin(turn);
        float cosine = Mathf.Cos(turn);
        return new Vector2(offset.x * cosine - offset.z * sine, offset.x * sine + offset.z * cosine) * unitsPerMetre;
    }

    private void SaveZoom()
    {
        try
        {
            PlayerPrefs.SetInt(ZoomKey, zoom);
        }
        catch (PlayerPrefsException exception)
        {
            Debug.LogWarning(exception.Message);
        }
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnDestroy()
    {
        Destroy(material);
        Destroy(map);
        if (dot != null)
        {
            Destroy(dot.texture);
            Destroy(dot);
        }

        if (pointer != null)
        {
            Destroy(pointer.texture);
            Destroy(pointer);
        }

    }

    private void Build(Transform root)
    {
        settings = MinimapSettings.Current;
        UIStyle style = UIStyle.Current;
        zoom = Mathf.Clamp(PlayerPrefs.GetInt(ZoomKey, 0), 0, ZoomSteps - 1);
        reach = TargetReach;
        dot = MakeSprite(Round);
        pointer = MakeSprite(Arrowhead);

        disc = CodeUI.Box("Disc", root, new Vector2(1f, 0f), Vector2.one * settings.Size, new Vector2(-Margin - settings.Size * 0.5f, Margin + settings.Size * 0.5f));
        ground = disc.gameObject.AddComponent<RawImage>();
        ground.raycastTarget = false;
        material = new Material(settings.Material);
        material.SetColor(FogId, style.Panel);
        material.SetColor(RimId, style.RimFor(RaidDirector.Current != null));
        material.SetColor(WardId, style.Accent);
        material.SetVector(AreaId, new Vector4(area.width, area.height, 0f, 0f));
        ground.material = material;
        ground.color = Color.white;

        RectTransform northRect = CodeUI.Box("North", disc, new Vector2(0.5f, 0.5f), new Vector2(26f, 22f), Vector2.zero);
        northLabel = northRect.gameObject.AddComponent<TextMeshProUGUI>();
        northLabel.text = "N";
        northLabel.fontSize = 15f;
        northLabel.alignment = TextAlignmentOptions.Center;
        northLabel.raycastTarget = false;
        northLabel.color = style.AccentFor(RaidDirector.Current != null);
        style.OnWorld(northLabel, false);
        north = northRect;

        arrow = CodeUI.Box("Player", disc, new Vector2(0.5f, 0.5f), Vector2.one * 24f, Vector2.zero);
        var arrowImage = arrow.gameObject.AddComponent<Image>();
        arrowImage.sprite = pointer;
        arrowImage.color = style.Text;
        arrowImage.raycastTarget = false;
        var outline = arrow.gameObject.AddComponent<Outline>();
        outline.effectColor = style.Panel;
        outline.effectDistance = new Vector2(1f, -1f);
        group.alpha = 0f;
        StartCoroutine(Paint());
    }

    /// <summary>
    /// Measures the ground again and paints it anew, for a map laid out after this was made: a generated raid's, which a large raid also
    /// widens. Measured too early, the map would show the old ground and read the fog of war at the wrong places (all black).
    /// </summary>
    public void Refit()
    {
        if (material == null || !MinimapMap.TryGetArea(out Rect found))
        {
            return;
        }

        area = found;
        material.SetVector(AreaId, new Vector4(area.width, area.height, 0f, 0f));
        StopAllCoroutines();
        StartCoroutine(Paint());
    }

    // A moment after the scene wakes: a raid lays itself out, and its NavMesh, during its first frames.
    private IEnumerator Paint()
    {
        yield return new WaitForSeconds(0.2f);
        yield return MinimapMap.Paint(area, settings, finished =>
        {
            if (map != null && map != finished)
            {
                Destroy(map);
            }

            map = finished;
            ground.texture = map;
            painted = true;
        });
    }

    private float TargetReach => zoom == Far ? settings.FarReach : settings.NearReach;

    private void LateUpdate()
    {
        Player player = Player.Current;
        bool windows = UIStateManager.Instance != null && UIStateManager.Instance.IsAnyWindowOpen;
        bool shown = painted && player != null && zoom != Hidden && !windows && !WorldMapHud.IsOpen && !GamePause.IsPaused;
        group.alpha = shown ? 1f : 0f;
        if (!shown)
        {
            return;
        }

        bool onRaid = RaidDirector.Current != null;
        if (raidTheme != onRaid)
        {
            raidTheme = onRaid;
            UIStyle style = UIStyle.Current;
            material.SetColor(RimId, style.RimFor(onRaid));
            northLabel.color = style.AccentFor(onRaid);
        }

        ExplorationMap explored = ExplorationMap.Current;
        material.SetTexture(ExploredId, explored != null ? explored.Texture : Texture2D.whiteTexture);
        reach = Mathf.Lerp(reach, TargetReach, 1f - Mathf.Exp(-ZoomSpeed * Time.unscaledDeltaTime));
        Camera view = Camera.main;
        if (view != null)
        {
            Vector3 forward = view.transform.forward;
            turn = Mathf.Atan2(forward.x, forward.z);
        }

        Vector3 centre = player.transform.position;
        material.SetVector(CentreId, new Vector4((centre.x - area.xMin) / area.width, (centre.z - area.yMin) / area.height, 0f, 0f));
        material.SetVector(SpanId, new Vector4(reach / area.width, reach / area.height, 0f, 0f));
        material.SetFloat(TurnId, turn);
        HomePortal tree = HomePortal.Current;
        material.SetFloat(RingRadiusId, tree != null ? tree.WardRadius : 0f);
        if (tree != null)
        {
            Vector3 place = tree.transform.position;
            material.SetVector(RingCentreId, new Vector4((place.x - area.xMin) / area.width, (place.z - area.yMin) / area.height, 0f, 0f));
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            ReadMarkers(player);
        }

        Place(centre, player);
    }

    private void Place(Vector3 centre, Player player)
    {
        float radius = settings.Size * 0.5f;
        float scale = radius / reach;
        arrow.localRotation = Quaternion.Euler(0f, 0f, turn * Mathf.Rad2Deg - player.transform.eulerAngles.y);
        north.anchoredPosition = Project(Vector3.forward, turn, radius - RimInset * 1.6f);
        for (int i = 0; i < used; i++)
        {
            Marker marker = markers[i];
            Vector3 position = marker.Follow != null ? marker.Follow.position : marker.Position;
            Vector2 spot = Project(position - centre, turn, scale);
            float limit = radius - RimInset;
            bool beyond = spot.sqrMagnitude > limit * limit;
            marker.Rect.gameObject.SetActive(!beyond || marker.Pinned);
            if (beyond)
            {
                spot = spot.normalized * limit;
            }

            marker.Rect.anchoredPosition = spot;
            if (marker.Facing)
            {
                marker.Rect.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, -spot));
            }
        }
    }

    private void ReadMarkers(Player player)
    {
        UIStyle style = UIStyle.Current;
        ExplorationMap explored = ExplorationMap.Current;
        used = 0;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (Found(building.transform.position, explored))
            {
                bool broken = building.TryGetComponent(out Damageable health) && health.IsBroken;
                Add(building.transform, null, broken ? style.Danger : style.Muted, 7f);
            }
        }

        foreach (Transport transport in Transport.All)
        {
            Add(transport.transform, null, style.Text, 6f);
        }

        foreach (DroppedItem dropped in DroppedItem.All)
        {
            if (Found(dropped.transform.position, explored))
            {
                Add(dropped.transform, dot, style.Text, 5f);
            }
        }

        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            ExtractionStatus status = exit.Status;
            if (status != ExtractionStatus.Closed)
            {
                bool open = status == ExtractionStatus.Open;
                Add(exit.transform, style.ExitMark, open ? style.Good : style.Muted, open ? 17f : 14f, pinned: true);
            }
        }

        foreach (Companion companion in Companion.Active)
        {
            // Someone lost, not found yet: only roughly where, as the rumour says.
            if (companion.Paused && RaidDirector.Current != null)
            {
                continue;
            }

            bool down = companion.IsDowned;
            Add(companion.transform, style.CompanionMark, down ? style.Danger : style.Good, down ? 16f : 13f, pinned: down);
        }

        // Co-op: friends here (kept at the edge when far, so the party can regroup), and pings.
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && !friend.IsOwner && friend.IsVisible && friend.Body != null)
            {
                bool friendDown = friend.Look.Downed;
                Add(friend.Body.transform, style.CompanionMark, friendDown ? style.Danger : style.Accent, friendDown ? 16f : 13f, pinned: true);
            }
        }

        foreach (CoopPings.Ping ping in CoopPings.Active)
        {
            Marker mark = Add(null, style.EventMark, ping.Enemy ? style.Danger : style.Accent, 15f, pinned: true);
            mark.Position = ping.Position;
        }

        ReadRaidSites(player, style);
        ReadQuest(style);
        ReadBreach(style);
        ReadCreatures(player, style);
        for (int i = used; i < markers.Count; i++)
        {
            markers[i].Rect.gameObject.SetActive(false);
        }
    }

    private void ReadRaidSites(Player player, UIStyle style)
    {
        RaidDirector director = RaidDirector.Current;
        if (director == null || !director.TryGetComponent(out RaidEventDirector events))
        {
            return;
        }

        Vector3 from = player.transform.position;
        foreach (RaidEventSite site in events.PinnedSites)
        {
            if (site != null && !site.IsResolved)
            {
                Add(null, style.RumourMark, style.Accent, 16f, pinned: true).Position = site.MarkFrom(from);
            }
        }

        foreach (RaidEventSite site in events.OpenSites)
        {
            if (site != null && !site.IsResolved)
            {
                Add(site.transform, style.EventMark, style.Text, 16f, pinned: true);
            }
        }
    }

    private static bool Found(Vector3 position, ExplorationMap explored) => explored == null || explored.ExploredAt(position) >= 0.5f;

    private void ReadQuest(UIStyle style)
    {
        QuestLog log = GameSession.Quests;
        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint != null && waypoint.IsWanted(log))
            {
                Color colour = waypoint.Quest != null ? QuestMarkerSettings.Current.RankColor(waypoint.Quest.Rank) : style.Accent;
                Add(waypoint.transform, style.QuestMark, colour, 16f, pinned: true);
            }
        }

        // Whoever has a quest to give or to take back.
        foreach (QuestGiver giver in QuestGiver.Active)
        {
            if (giver != null && giver.ShownQuest != null && (giver.ShownState == QuestGiverState.Available || giver.ShownState == QuestGiverState.ReadyToTurnIn))
            {
                Add(giver.transform, style.QuestMark, QuestMarkerSettings.Current.RankColor(giver.ShownQuest.Rank), 13f);
            }
        }
    }

    private void ReadBreach(UIStyle style)
    {
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach == null || !(breach.IsActive || breach.IsPending))
        {
            return;
        }

        foreach (BreachLane lane in breach.Lanes)
        {
            Marker marker = Add(null, pointer, style.Danger, 14f, pinned: true);
            marker.Position = lane.Start;
            marker.Facing = true;
        }

        foreach (Damageable attacker in breach.Attackers)
        {
            if (attacker != null && attacker.IsAlive)
            {
                Add(attacker.transform, dot, style.Danger, 7f);
            }
        }
    }

    private void ReadCreatures(Player player, UIStyle style)
    {
        float sight = settings.CreatureSight(NightSettings.Darkness);
        Vector3 from = player.transform.position;
        WardBreachDirector breach = WardBreachDirector.Current;
        foreach (MonsterAI creature in MonsterAI.Active)
        {
            bool seen = (creature.transform.position - from).sqrMagnitude <= sight * sight;
            if (seen && creature.CurrentState != MonsterAI.State.Dead && (breach == null || !breach.IsActive)
                && creature.TryGetComponent(out Combatant side) && side.Faction == Faction.Enemy)
            {
                Add(creature.transform, dot, style.Danger, 7f);
            }
        }
    }

    private Marker Add(Transform follow, Sprite sprite, Color colour, float size, bool pinned = false)
    {
        if (used == markers.Count)
        {
            RectTransform rect = CodeUI.Box("Marker", disc, new Vector2(0.5f, 0.5f), Vector2.one, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            markers.Add(new Marker { Rect = rect, Image = image });
            arrow.SetAsLastSibling();
        }

        Marker marker = markers[used++];
        marker.Follow = follow;
        marker.Pinned = pinned;
        marker.Facing = false;
        marker.Image.sprite = sprite;
        marker.Image.color = colour;
        marker.Rect.sizeDelta = Vector2.one * size;
        marker.Rect.localRotation = Quaternion.identity;
        return marker;
    }

    // ----- Sprites, drawn rather than imported: two small shapes -----

    private static Sprite MakeSprite(System.Func<Vector2, float> coverage)
    {
        const int Size = 48;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var point = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(coverage(point) * Size * 0.5f + 0.5f)));
            }
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), Vector2.one * 0.5f);
    }

    // How far inside the shape a point is, in the -1..1 square; negative outside.
    private static float Round(Vector2 point) => 0.9f - point.magnitude;

    private static float Arrowhead(Vector2 point)
    {
        // A dart pointing up: inside its two long sides, above its base, and above the notch in its tail.
        float sides = (1f - point.y) * 0.7f - Mathf.Abs(point.x);
        float notch = point.y + 0.45f + Mathf.Abs(point.x) * 0.64f;
        return Mathf.Min(Mathf.Min(sides, point.y + 0.9f), notch);
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    private const float Margin = 26f;
    private const float RefreshSeconds = 0.25f;
    private const float ZoomSpeed = 8f;
    private const float RimInset = 7f;

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
    private float reach;
    private float turn;
    private float nextRefresh;
    private bool painted;
    // The ground the painting shows: a raid lays its map out after this is made, and its first painting is of the old ground.
    private Rect paintedArea;
    private bool? raidTheme;

    public static MinimapHud Current { get; private set; }

    /// <summary>The minimap's top and centre, in canvas units from the bottom-right corner, at the size it is drawn now (the region's
    /// name sits over it: RegionTitleHud).</summary>
    public Vector2 TopCentre => settings != null
        ? new Vector2(-Margin - settings.Size * HudRules.MinimapScale * 0.5f, Margin + settings.Size * HudRules.MinimapScale)
        : new Vector2(-Margin - 118f, Margin + 236f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        TakesWheel = false;
    }

    /// <summary>This scene's painted ground, or null until it is painted. Owned by the minimap; the world map only shows it.</summary>
    public Texture2D PaintedMap => painted ? map : null;

    /// <summary>The painted ground if it shows <paramref name="wanted"/> (the world map's area now), else null: the world
    /// map then paints its own rather than show the ground as it was before a raid laid its map out.</summary>
    public Texture2D PaintedMapFor(Rect wanted) => painted && paintedArea == wanted ? map : null;

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

    /// <summary>True while the pointer is over the shown minimap: the mouse wheel there sets its distance, not the camera's.</summary>
    public static bool TakesWheel { get; private set; }

    /// <summary>Where a world offset from the player lies on a map turned this far (radians), in map units at this scale.</summary>
    public static Vector2 Project(Vector3 offset, float turn, float unitsPerMetre)
    {
        float sine = Mathf.Sin(turn);
        float cosine = Mathf.Cos(turn);
        return new Vector2(offset.x * cosine - offset.z * sine, offset.x * sine + offset.z * cosine) * unitsPerMetre;
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
        reach = TargetReach;
        dot = MakeSprite(Round);
        pointer = MakeSprite(Arrowhead);

        disc = CodeUI.Box("Disc", root, new Vector2(1f, 0f), Vector2.one * settings.Size, new Vector2(-Margin - settings.Size * 0.5f, Margin + settings.Size * 0.5f));
        // Held by its corner, so it grows and shrinks into it (HudRules.MinimapScale): the same place at full size.
        disc.pivot = new Vector2(1f, 0f);
        disc.anchoredPosition = new Vector2(-Margin, Margin);
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
        // The old painting is of the ground before the raid was laid out: hidden until the new one is done.
        painted = false;
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
            paintedArea = area;
            painted = true;
        });
    }

    // The player's Minimap Distance (Options › Gameplay; the wheel over the map). It was a fixed 28 m, too close.
    private static float TargetReach => GameOptions.MinimapReaches[Mathf.Clamp(GameOptions.MinimapDistance.Index, 0, GameOptions.MinimapReaches.Length - 1)];

    private void LateUpdate()
    {
        Player player = Player.Current;
        bool windows = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        bool shown = painted && player != null && TargetReach > 0f && !windows && !WorldMapHud.IsOpen && !GamePause.IsPaused;
        group.alpha = shown ? 1f : 0f;
        // A quarter smaller on a Contextual or Minimal HUD: a third of the screen's height at 720p (the HUD review of 2026-11-18).
        disc.localScale = Vector3.one * HudRules.MinimapScale;
        TakesWheel = shown && Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(disc, Mouse.current.position.ReadValue(), null);
        if (!shown)
        {
            return;
        }

        if (TakesWheel)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                // Up comes closer; never down to Hidden from the wheel (the option can hide it).
                int index = Mathf.Clamp(GameOptions.MinimapDistance.Index - (int)Mathf.Sign(wheel), 1, GameOptions.MinimapReaches.Length - 1);
                GameOptions.MinimapDistance.Value = index;
            }
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
            // Paths are many small pieces: they'd crowd the map with dots.
            if (building.Definition != null && building.Definition.IsPath)
            {
                continue;
            }

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

        // The player's own horses, where they were left (the one ridden is under the player's arrow).
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsTamed && horse.IsAlive && horse.Rider == null)
            {
                Add(horse.transform, dot, style.Accent, 8f);
            }
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
        ReadLandmarks(player, style);
        ReadHavenPlaces(player, style);
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

        // A standing site (a bridge's toll) is part of the place, not a rumour: marked once the player has come upon it.
        foreach (RaidEventSite site in events.StandingSites)
        {
            if (site != null && !site.IsResolved && site.IsFoundFrom(from))
            {
                Add(site.transform, style.EventMark, style.Text, 16f, pinned: true);
            }
        }
    }

    // A generated map's landmarks (RaidMapLandmark, G4): once found they stay marked, pinned to the rim when far, so the way can be
    // found by them from anywhere on the map.
    private void ReadLandmarks(Player player, UIStyle style)
    {
        Vector3 from = player.transform.position;
        foreach (RaidMapLandmark landmark in RaidMapLandmark.Active)
        {
            if (landmark != null && landmark.CanBeMarked && landmark.IsFoundFrom(from))
            {
                Add(landmark.transform, landmark.Icon != null ? landmark.Icon : dot, style.Text, landmark.Icon != null ? 15f : 8f, pinned: true);
            }
        }
    }

    // The Haven's named places (Docs/HavenExplorationReview_2026-11-19.md §3): the minimap showed none. A found one (or one seen from a
    // lookout, fainter) is marked, kept on the rim within Known Rim; an unfound one close by gets a faint rumour mark on the rim its way,
    // two at most and none in a fight: the top-down short flag. Not on a raid (its own landmarks).
    private const float KnownRim = 150f;
    private const float HintRim = 70f;
    private const int MostHints = 2;

    private void ReadHavenPlaces(Player player, UIStyle style)
    {
        WorldAtlas atlas = PlaceDiscovery.Current != null ? PlaceDiscovery.Current.Atlas : null;
        if (atlas == null || RaidDirector.Current != null)
        {
            return;
        }

        JournalRecord journal = GameSession.Progress.Journal;
        Vector3 from = player.transform.position;
        var here = new Vector2(from.x, from.z);
        bool fight = HudRules.InAFight;
        int hints = 0;
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            float distance = Vector2.Distance(place.at, here);
            bool found = journal.HasDiscovered(place.id);
            if ((found || journal.HasSeen(place.id)) && distance <= KnownRim)
            {
                Color colour = style.Muted;
                colour.a *= found ? 1f : 0.55f;
                Add(null, style.EventMark, colour, found ? 11f : 9f, pinned: true).Position = new Vector3(place.at.x, from.y, place.at.y);
            }
            else if (!found && !fight && hints < MostHints && distance <= HintRim)
            {
                hints++;
                Color colour = style.Muted;
                colour.a *= 0.6f;
                Add(null, style.RumourMark, colour, 12f, pinned: true).Position = new Vector3(place.at.x, from.y, place.at.y);
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

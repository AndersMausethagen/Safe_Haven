using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The portal at the heart of Safe Haven (GDD §1.1, §6.1): the old stones that dropped the player here, and what protects the clearing.
/// The player wakes and revives beside it (put a PlayerArrivalPoint "HomePortal" next to it), its ward is the area where the player may
/// build (BuildPlacer asks Contains), and using it opens the one menu for all of it: what it says (the line for the active First Days
/// quest, so it guides the player one step at a time), planning a raid and everything else RaidPortal offers, strengthening the ward with
/// Purified Essence (GDD §6.4), Portal Sparks at its CraftingStation and, when the ward is weakening, the defense against a breach (GDD §6.6).
/// <para>
/// Where it belongs: on the portal in Safe Haven, beside its RaidPortal and a CraftingStation with Opens When Used off, next to an
/// Interactable that doesn't deactivate on completion. The ward radius and upgrade costs live in SettlementSettings; the ward level in
/// SettlementState; the ring shows the ward. The portal is the ward and the voice; what its menu offers is <see cref="HomePortalMenu"/>.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.ItsChoicesDecide, "the portal talks to a visitor; its raids and the Haven's work are the host's")]
public sealed class HomePortal : MonoBehaviour, IArea, IBuildArea
{
    /// <summary>What the portal says while a quest is active, so it points the way one step at a time.</summary>
    [System.Serializable]
    public struct QuestWhisper
    {
        public QuestDefinition quest;
        [TextArea(2, 5)] public string text;
        [Tooltip("Optional: the first time the player uses the portal during this quest, this Quest Event is raised instead of the menu opening. A Story Script listens for it (When Quest Event) and has the conversation; it raises Talked Event if a quest waits for that.")]
        public GameEventDefinition moment;
    }

    [Tooltip("What the portal says while each quest is active. The last active one in the list speaks first, so the main story goes last; Listen goes through the others. When none is active, it whispers the lines below.")]
    [SerializeField] private QuestWhisper[] questWhispers = System.Array.Empty<QuestWhisper>();
    [Tooltip("What the portal whispers when no quest whisper applies, one line per listen, in order.")]
    [SerializeField, TextArea(2, 5)] private string[] whispers =
    {
        "Warmth stirs in the old stone. <i>Little one... you are safe here, inside my light. But my strength is thin.</i>",
        "<i>Gather what the clearing offers. Wood, stone, berries, water. Build a fire, and a table to work at.</i>",
        "<i>Three stones still hold my light, choked by cursed roots. Free them, and I may wake.</i>",
        "<i>Beyond me the forest is sick. Go, take what you need, and come home before the way closes.</i>",
    };
    [Tooltip("For quests: what finishing a conversation at the portal counts as (Talk To), e.g. the Portal Quest Target.")]
    [SerializeField, QuestIdentity] private QuestTargetDefinition questTarget;

    /// <summary>What talking to the portal counts as for quests (Talk To).</summary>
    public QuestTargetDefinition QuestTarget => questTarget;
    [Tooltip("A story moment of its own, raised when a conversation with the portal ends. Optional: Talk To objectives need none.")]
    [SerializeField, RaisedByGame] private GameEventDefinition talkedEvent;
    [Tooltip("What is heard when it speaks: its whispers here, and the same sound on its lines in story graphs. Optional.")]
    [SerializeField] private SoundDefinition voice;
    [Tooltip("Draws the edge of the ward. Optional.")]
    [SerializeField] private LineRenderer wardRing;
    [SerializeField, Min(8)] private int ringSegments = 96;
    [Tooltip("The menu closes when the player walks further than this, in meters.")]
    [SerializeField, Min(1f)] private float menuRange = 7f;
    [Tooltip("How far from the portal building is open in a scene whose map has no Haven region (no World Atlas).")]
    [SerializeField, Min(10f)] private float buildReach = 165f;
    [Tooltip("The most the ground may rise or fall across a building's footprint, in metres: more is too uneven to build on.")]
    [SerializeField, Min(0.05f)] private float buildUnevenness = 0.5f;

    private WorldAtlas atlas;
    private WorldAtlas.Region homeRegion;
    private bool atlasRead;
    private Terrain ground;

    private Interactable interactable;
    private RaidPortal raid;
    private CraftingStation station;
    private HomePortalMenu menu;
    private int whisperIndex;
    private int questLineIndex;
    private readonly List<string> questLines = new List<string>();

    /// <summary>The portal in the loaded scene, or null outside Safe Haven.</summary>
    public static HomePortal Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>How far from the portal the player may build, at the current ward level.</summary>
    public float WardRadius => SettlementSettings.Current.WardRadiusAt(GameSession.Here.WardLevel)
        + HavenSettings.Current.WardBonusAt(GameSession.Here.HavenRank)
        - (GameSession.Here.WardStrain > 0 ? WardBreachSettings.Current.StrainRadius : 0f);

    /// <summary>True when a point (and a margin around it, such as a building's size) is inside the ward.</summary>
    public bool Contains(Vector3 position, float margin = 0f)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude + margin <= WardRadius;
    }

    bool IArea.Contains(Vector3 position) => Contains(position);

    // ----- Building (IBuildArea): anywhere in the Haven's own land (its region on the map), on the ground as it lies -----

    /// <summary>
    /// True where the player may build: anywhere in the Haven's own region on the map (its meadows, the region the portal stands
    /// in), and always inside the ward. Without a map, within Build Reach of the portal.
    /// </summary>
    public bool InBuildLand(Vector3 position)
    {
        if (Contains(position))
        {
            return true;
        }

        ReadAtlas();
        if (homeRegion != null)
        {
            return atlas.RegionAt(position) == homeRegion;
        }

        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude <= buildReach;
    }

    /// <summary>What the land the player builds on is called (Haven Meadows).</summary>
    public string BuildLandName
    {
        get
        {
            ReadAtlas();
            return homeRegion != null && !string.IsNullOrEmpty(homeRegion.name) ? homeRegion.name : "the Haven's meadows";
        }
    }

    private void ReadAtlas()
    {
        if (atlasRead)
        {
            return;
        }

        atlasRead = true;
        atlas = WorldAtlas.For(gameObject.scene.name);
        homeRegion = atlas != null ? atlas.RegionAt(transform.position) : null;
    }

    private Terrain Ground => ground != null ? ground : ground = Terrain.activeTerrain;

    string IBuildArea.WhyClosed => null;
    string IBuildArea.Guidance => $"Build anywhere in {BuildLandName}. Inside the portal's ward ({WardRadius:0} m) it is safe at night. ";
    bool IBuildArea.AllowsChanges => true;
    float IBuildArea.GroundHeight => transform.position.y;

    IEnumerable<BuildingDefinition> IBuildArea.Buildable(PlayerCrafting crafting)
    {
        foreach (BuildingDefinition building in crafting.Buildings)
        {
            if (building != null && building.ShowInBuildMenu && crafting.IsKnown(building) && BuildingUnlocks.IsOpen(building))
            {
                yield return building;
            }
        }
    }

    // The ground under the pointer: the terrain as it lies (the meadows rise and fall), else the portal's level.
    bool IBuildArea.TryGetGroundPoint(Camera view, out Vector3 point)
    {
        Terrain terrain = Ground;
        if (terrain != null && CameraPointer.TryGetRay(view, out Ray ray) && terrain.TryGetComponent(out TerrainCollider collider)
            && collider.Raycast(ray, out RaycastHit hit, 600f))
        {
            point = hit.point;
            return true;
        }

        return CameraPointer.TryGetPointOnPlane(view, new Plane(Vector3.up, transform.position), out point);
    }

    Vector3 IBuildArea.OnGround(Vector3 point) => OnGround(point);

    private Vector3 OnGround(Vector3 point)
    {
        Terrain terrain = Ground;
        if (terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point;
    }

    // In the Haven's land, out of the water, and on ground level enough for the footprint (its corners and middle).
    string IBuildArea.CheckSpot(FootprintRect footprint, Vector3 point)
    {
        Vector2 x = footprint.AxisX * footprint.HalfSize.x;
        Vector2 z = footprint.AxisZ * footprint.HalfSize.y;
        float highest = float.MinValue, lowest = float.MaxValue;
        for (int corner = 0; corner < 5; corner++)
        {
            Vector2 at = corner == 4 ? footprint.Center : footprint.Center + ((corner & 1) == 0 ? x : -x) + ((corner & 2) == 0 ? z : -z);
            Vector3 spot = OnGround(new Vector3(at.x, point.y, at.y));
            if (!InBuildLand(spot))
            {
                return $"Outside {BuildLandName}. Build nearer home.";
            }

            // Looked for from a little over the ground, so a deep pond's bed is under its water too.
            if (WaterSurface.At(spot + Vector3.up * 1.5f, 3.5f, out float surface) != null && surface > spot.y + 0.05f)
            {
                return "Not in the water.";
            }

            highest = Mathf.Max(highest, spot.y);
            lowest = Mathf.Min(lowest, spot.y);
        }

        return highest - lowest > buildUnevenness ? "The ground is too uneven here: find flatter ground." : null;
    }

    string IBuildArea.CheckBuilding(BuildingDefinition building) => null;

    private void Awake()
    {
        if (!TryGetComponent(out HavenProgress _))
        {
            gameObject.AddComponent<HavenProgress>();
        }

        // Who lives in which house and works where, and the paths drawn between them (Docs/BuildingSystemResearch.md §3.4).
        if (!TryGetComponent(out HavenHomes _))
        {
            gameObject.AddComponent<HavenHomes>();
        }

        if (!TryGetComponent(out HavenPaths _))
        {
            gameObject.AddComponent<HavenPaths>();
        }

        if (!TryGetComponent(out Yards _))
        {
            gameObject.AddComponent<Yards>();
        }

        if (!TryGetComponent(out ResidentBuilders _))
        {
            gameObject.AddComponent<ResidentBuilders>();
        }

        if (!TryGetComponent(out Footpaths _))
        {
            gameObject.AddComponent<Footpaths>();
        }

        interactable = GetComponent<Interactable>();
        raid = GetComponent<RaidPortal>();
        station = GetComponent<CraftingStation>();
        Current = this;
        BuildArea.Enter(this);
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        GameSession.Here.Changed += DrawWard;
        DrawWard();
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        GameSession.Here.Changed -= DrawWard;
    }

    private void OnDestroy()
    {
        BuildArea.Leave(this);
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        GameEventDefinition moment = PendingMoment();
        if (moment != null)
        {
            GameEvents.Raise(moment, user);
            return;
        }

        questLineIndex = 0;
        Speak();
        // Raised when the conversation ends, so the next quest doesn't appear while the portal is still speaking.
        menu ??= new HomePortalMenu(this, raid, station);
        // Its own window (PortalWindow: its painting, its words, what it offers), not the plain menu.
        PortalWindow.Show(() => menu.Build(user), user, transform, menuRange, () => RaiseTalked(user));
    }

    private void RaiseTalked(GameObject user)
    {
        // A friend's portal: what this player's own portal has to tell them (a quest's Talk To) waits for their own Haven.
        if (HavenVisit.InHostsHaven)
        {
            return;
        }

        QuestDeeds.Raise(GameEventKind.Talked, questTarget, user);
        if (talkedEvent != null)
        {
            GameEvents.Raise(talkedEvent, user);
        }
    }

    /// <summary>The story moment waiting for the player to use the portal, or null. It plays once, and only in the player's own
    /// Haven: a visitor's stories don't play in a friend's (StoryScript), so there it would be spent with nothing said.</summary>
    private GameEventDefinition PendingMoment()
    {
        if (HavenVisit.InHostsHaven)
        {
            return null;
        }

        for (int i = questWhispers.Length - 1; i >= 0; i--)
        {
            QuestWhisper whisper = questWhispers[i];
            if (whisper.quest != null && whisper.moment != null && GameSession.Quests.StatusOf(whisper.quest) == QuestStatus.Active
                && !GameSession.Quests.HasHappened(whisper.moment))
            {
                return whisper.moment;
            }
        }

        return null;
    }

    /// <summary>The portal's lines for the quests the player is on, the one that speaks first at the front (the last in
    /// the list: the main story). Empty when no quest whisper applies. The list is reused: read it, don't keep it.</summary>
    private List<string> QuestLines()
    {
        questLines.Clear();
        for (int i = questWhispers.Length - 1; i >= 0; i--)
        {
            QuestWhisper whisper = questWhispers[i];
            if (whisper.quest != null && !string.IsNullOrEmpty(whisper.text) && GameSession.Quests.StatusOf(whisper.quest) == QuestStatus.Active)
            {
                questLines.Add(whisper.text);
            }
        }

        return questLines;
    }

    /// <summary>What the portal says now: its line for a quest the player is on (the story's step first), or one of its
    /// whispers; null when it has nothing.</summary>
    public string SpokenLine()
    {
        List<string> lines = QuestLines();
        if (lines.Count > 0)
        {
            return lines[questLineIndex % lines.Count];
        }

        return whispers.Length > 0 ? whispers[Mathf.Clamp(whisperIndex, 0, whispers.Length - 1)] : null;
    }

    /// <summary>True when it has more to say: a line for another quest the player is on, or more whispers when no quest has one.</summary>
    public bool HasMoreToSay
    {
        get
        {
            int lines = QuestLines().Count;
            return lines > 1 || (lines == 0 && whispers.Length > 1);
        }
    }

    /// <summary>Its next line, aloud: the next open quest's, or its next whisper.</summary>
    public void SayNext()
    {
        int lines = QuestLines().Count;
        if (lines > 1)
        {
            questLineIndex = (questLineIndex + 1) % lines;
        }
        else if (lines == 0 && whispers.Length > 0)
        {
            whisperIndex = (whisperIndex + 1) % whispers.Length;
        }

        Speak();
    }

    // Its line in its recorded voice when it has one (VoiceBank, under the name its story lines use), else the sound of
    // its voice without words.
    private void Speak()
    {
        if (!VoicePlayer.Say(SpeakerName, SpokenLine()))
        {
            AudioManager.Play(voice, transform.position);
        }
    }

    /// <summary>The name the portal's lines are spoken under, in its menu and in story graphs alike.</summary>
    public const string SpeakerName = "The Portal";

    private void DrawWard()
    {
        if (wardRing == null)
        {
            return;
        }

        float radius = WardRadius;
        wardRing.useWorldSpace = true;
        wardRing.loop = true;
        wardRing.positionCount = ringSegments;
        Vector3 center = transform.position + Vector3.up * 0.06f;
        for (int i = 0; i < ringSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / ringSegments;
            wardRing.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }
}

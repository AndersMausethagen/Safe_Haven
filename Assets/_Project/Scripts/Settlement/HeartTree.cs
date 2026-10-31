using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The Heart Tree (GDD §1.1, §6.1): the ancient protector of the clearing. The player wakes and revives beside it (put a
/// PlayerArrivalPoint "HeartTree" next to it), its ward is the area where the player may build (BuildPlacer asks
/// Contains), and talking to it opens a menu to listen to it (it speaks the line for the active First Days quest, so it
/// guides the player one step at a time), strengthen the ward with Purified Essence (GDD §6.4) and,
/// when the ward is weakening, begin the defense against a breach (GDD §6.6).
/// <para>
/// Where it belongs: on the Heart Tree in Safe Haven, next to an Interactable that doesn't deactivate on completion. The
/// ward radius and upgrade costs live in SettlementSettings; the ward level in SettlementState; the ring shows the ward.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class HeartTree : MonoBehaviour, IArea, IBuildArea
{
    /// <summary>What the tree says while a quest is active, so it points the way one step at a time.</summary>
    [System.Serializable]
    public struct QuestWhisper
    {
        public QuestDefinition quest;
        [TextArea(2, 5)] public string text;
        [Tooltip("Optional: the first time the player talks to the tree during this quest, this Quest Event is raised instead of the menu opening. A Story Script listens for it (When Quest Event) and has the conversation; it raises Talked Event if a quest waits for that.")]
        public GameEventDefinition moment;
    }

    [Tooltip("What the tree says while each quest is active. The last active one in the list wins. When none is active, it whispers the lines below.")]
    [SerializeField] private QuestWhisper[] questWhispers = System.Array.Empty<QuestWhisper>();
    [Tooltip("What the tree whispers when no quest whisper applies, one line per listen, in order.")]
    [SerializeField, TextArea(2, 5)] private string[] whispers =
    {
        "Warmth stirs in the old bark. <i>Little one... you are safe here, inside my roots. But my strength is thin.</i>",
        "<i>Gather what the clearing offers. Wood, stone, berries, water. Build a fire, and a table to work at.</i>",
        "<i>Three stones still hold my light, choked by cursed roots. Free them, and the old portal may wake.</i>",
        "<i>Beyond the portal the forest is sick. Go, take what you need, and come home before it closes.</i>",
    };
    [Tooltip("Raised when the player finishes talking to the tree (the menu closes), for quests such as \"Listen to the Heart Tree\".")]
    [SerializeField, RaisedByGame] private GameEventDefinition talkedEvent;
    [Tooltip("Draws the edge of the ward. Optional.")]
    [SerializeField] private LineRenderer wardRing;
    [SerializeField, Min(8)] private int ringSegments = 96;
    [Tooltip("The menu closes when the player walks further than this, in meters.")]
    [SerializeField, Min(1f)] private float menuRange = 7f;

    private Interactable interactable;
    private int whisperIndex;

    /// <summary>The Heart Tree in the loaded scene, or null outside Safe Haven.</summary>
    public static HeartTree Current { get; private set; }

    /// <summary>How far from the tree the player may build, at the current ward level.</summary>
    public float WardRadius => SettlementSettings.Current.WardRadiusAt(GameSession.Settlement.WardLevel)
        + HavenSettings.Current.WardBonusAt(GameSession.Settlement.HavenRank);

    /// <summary>True when a point (and a margin around it, such as a building's size) is inside the ward.</summary>
    public bool Contains(Vector3 position, float margin = 0f)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude + margin <= WardRadius;
    }

    bool IArea.Contains(Vector3 position) => Contains(position);

    // ----- Building (IBuildArea): the ward is where the player builds what they know -----

    string IBuildArea.WhyClosed => null;
    string IBuildArea.Guidance => $"Build inside the Heart Tree's ward ({WardRadius:0} m). ";
    bool IBuildArea.AllowsChanges => true;
    float IBuildArea.GroundHeight => transform.position.y;

    IEnumerable<BuildingDefinition> IBuildArea.Buildable(PlayerCrafting crafting)
    {
        foreach (BuildingDefinition building in crafting.Buildings)
        {
            if (building != null && building.ShowInBuildMenu && crafting.IsKnown(building))
            {
                yield return building;
            }
        }
    }

    bool IBuildArea.TryGetGroundPoint(Camera view, out Vector3 point) =>
        CameraPointer.TryGetPointOnPlane(view, new Plane(Vector3.up, transform.position), out point);

    Vector3 IBuildArea.OnGround(Vector3 point) => point;

    string IBuildArea.CheckSpot(FootprintRect footprint, Vector3 point) =>
        Contains(footprint.WorldCenter(point.y), Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y))
            ? null : "Outside the Heart Tree's ward. Build closer to the tree.";

    string IBuildArea.CheckBuilding(BuildingDefinition building) => null;

    private void Awake()
    {
        if (!TryGetComponent(out HavenProgress _))
        {
            gameObject.AddComponent<HavenProgress>();
        }

        interactable = GetComponent<Interactable>();
        Current = this;
        BuildArea.Enter(this);
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        GameSession.Settlement.Changed += DrawWard;
        DrawWard();
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        GameSession.Settlement.Changed -= DrawWard;
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

        if (ActionMenuWindow.Current == null)
        {
            Debug.LogError("The Heart Tree found no ActionMenuWindow in the scene.", this);
            RaiseTalked(user);
            return;
        }

        // Raised when the conversation ends, so the next quest doesn't appear while the tree is still speaking.
        ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange, () => RaiseTalked(user));
    }

    private void RaiseTalked(GameObject user)
    {
        if (talkedEvent != null)
        {
            GameEvents.Raise(talkedEvent, user);
        }
    }

    /// <summary>The story moment waiting for the player to talk to the tree, or null. It plays once.</summary>
    private GameEventDefinition PendingMoment()
    {
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

    /// <summary>The tree's line for the current step of the story, or null when no quest whisper applies.</summary>
    private string QuestLine()
    {
        for (int i = questWhispers.Length - 1; i >= 0; i--)
        {
            QuestWhisper whisper = questWhispers[i];
            if (whisper.quest != null && !string.IsNullOrEmpty(whisper.text) && GameSession.Quests.StatusOf(whisper.quest) == QuestStatus.Active)
            {
                return whisper.text;
            }
        }

        return null;
    }

    private static void OpenHaven()
    {
        if (HavenWindow.Current != null)
        {
            HavenWindow.Current.Open();
        }
    }

    private MenuPage BuildPage(GameObject user)
    {
        SettlementSettings settings = SettlementSettings.Current;
        SettlementState settlement = GameSession.Settlement;
        var body = new StringBuilder();
        string questLine = QuestLine();
        if (questLine != null)
        {
            body.Append(questLine).Append("\n\n");
        }
        else if (whispers.Length > 0)
        {
            body.Append(whispers[Mathf.Clamp(whisperIndex, 0, whispers.Length - 1)]).Append("\n\n");
        }

        body.Append($"<color=#E8C46A>The ward shelters {WardRadius:0} m around the tree. You can build inside it.</color>");
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach != null && breach.IsPending)
        {
            body.Append("\n\n<color=#FF9A6B>").Append(breach.Definition.PendingText).Append("</color>");
        }

        var page = new MenuPage { Title = "The Heart Tree", Body = body.ToString() };
        if (questLine == null && whispers.Length > 1)
        {
            page.Add(new MenuAction("Listen", null, true, () => whisperIndex = (whisperIndex + 1) % whispers.Length));
        }

        int level = settlement.WardLevel;
        if (level < settings.WardLevels.Count)
        {
            SettlementSettings.WardLevel next = settings.WardLevels[level];
            MaterialPool pool = MaterialPool.For(user);
            bool affordable = pool.CanAfford(next.cost, null, out string missing);
            string detail = $"{AmountText.Describe(next.cost)}  →  ward {settings.WardRadiusAt(level + 1):0} m" + (affordable ? "" : $"   <color=#FF7B6B>{missing}</color>");
            page.Add(new MenuAction("Strengthen the ward", detail, affordable, () => Strengthen(user)));
        }

        if (breach != null && breach.IsPending)
        {
            page.Add(new MenuAction("Begin the defense", breach.Definition.StartHint, true, breach.Begin, closesMenu: true));
        }

        page.Add(new MenuAction("How is Safe Haven doing?", $"Rank, renown and what to do next ({GameInput.KeyLabel(GameInput.Haven)})", true, OpenHaven));
        return page;
    }

    private void Strengthen(GameObject user)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Settlement.WardLevel;
        if (level >= settings.WardLevels.Count || !MaterialPool.For(user).TrySpend(settings.WardLevels[level].cost))
        {
            return;
        }

        GameSession.Settlement.RaiseWardLevel();
        GameMessages.Post($"The Heart Tree glows brighter. The ward now reaches {WardRadius:0} m.", MessageKind.Gain);
    }

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

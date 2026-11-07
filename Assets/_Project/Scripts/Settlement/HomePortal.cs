using System.Collections.Generic;
using System.Text;
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
/// SettlementState; the ring shows the ward.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
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

    [Tooltip("What the portal says while each quest is active. The last active one in the list wins. When none is active, it whispers the lines below.")]
    [SerializeField] private QuestWhisper[] questWhispers = System.Array.Empty<QuestWhisper>();
    [Tooltip("What the portal whispers when no quest whisper applies, one line per listen, in order.")]
    [SerializeField, TextArea(2, 5)] private string[] whispers =
    {
        "Warmth stirs in the old stone. <i>Little one... you are safe here, inside my light. But my strength is thin.</i>",
        "<i>Gather what the clearing offers. Wood, stone, berries, water. Build a fire, and a table to work at.</i>",
        "<i>Three stones still hold my light, choked by cursed roots. Free them, and I may wake.</i>",
        "<i>Beyond me the forest is sick. Go, take what you need, and come home before the way closes.</i>",
    };
    [Tooltip("Raised when the player finishes at the portal (the menu closes), for quests such as \"Listen to the portal\".")]
    [SerializeField, RaisedByGame] private GameEventDefinition talkedEvent;
    [Tooltip("What is heard when it speaks: its whispers here, and the same sound on its lines in story graphs. Optional.")]
    [SerializeField] private SoundDefinition voice;
    [Tooltip("Draws the edge of the ward. Optional.")]
    [SerializeField] private LineRenderer wardRing;
    [SerializeField, Min(8)] private int ringSegments = 96;
    [Tooltip("The menu closes when the player walks further than this, in meters.")]
    [SerializeField, Min(1f)] private float menuRange = 7f;

    private Interactable interactable;
    private RaidPortal raid;
    private CraftingStation station;
    private int whisperIndex;

    /// <summary>The portal in the loaded scene, or null outside Safe Haven.</summary>
    public static HomePortal Current { get; private set; }

    /// <summary>How far from the portal the player may build, at the current ward level.</summary>
    public float WardRadius => SettlementSettings.Current.WardRadiusAt(GameSession.Settlement.WardLevel)
        + HavenSettings.Current.WardBonusAt(GameSession.Settlement.HavenRank)
        - (GameSession.Settlement.WardStrain > 0 ? WardBreachSettings.Current.StrainRadius : 0f);

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
    string IBuildArea.Guidance => $"Build inside the portal's ward ({WardRadius:0} m). ";
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

    bool IBuildArea.TryGetGroundPoint(Camera view, out Vector3 point) =>
        CameraPointer.TryGetPointOnPlane(view, new Plane(Vector3.up, transform.position), out point);

    Vector3 IBuildArea.OnGround(Vector3 point) => point;

    string IBuildArea.CheckSpot(FootprintRect footprint, Vector3 point) =>
        Contains(footprint.WorldCenter(point.y), Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y))
            ? null : "Outside the portal's ward. Build closer to it.";

    string IBuildArea.CheckBuilding(BuildingDefinition building) => null;

    private void Awake()
    {
        if (!TryGetComponent(out HavenProgress _))
        {
            gameObject.AddComponent<HavenProgress>();
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
            Debug.LogError("The portal found no ActionMenuWindow in the scene.", this);
            RaiseTalked(user);
            return;
        }

        AudioManager.Play(voice, transform.position);
        // Raised when the conversation ends, so the next quest doesn't appear while the portal is still speaking.
        ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange, () => RaiseTalked(user));
    }

    private void RaiseTalked(GameObject user)
    {
        if (talkedEvent != null)
        {
            GameEvents.Raise(talkedEvent, user);
        }
    }

    /// <summary>The story moment waiting for the player to use the portal, or null. It plays once.</summary>
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

    /// <summary>The portal's line for the current step of the story, or null when no quest whisper applies.</summary>
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

        UIStyle style = UIStyle.Current;
        body.Append($"<color={UIStyle.Hex(style.Accent)}>The ward shelters {WardRadius:0} m around the portal. You can build inside it.</color>");
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach != null && breach.IsPending)
        {
            body.Append($"\n\n<color={UIStyle.Hex(style.Warning)}>").Append(breach.Definition.PendingText).Append("</color>");
        }

        if (settlement.WardStrain > 0)
        {
            int raids = settlement.WardStrain;
            body.Append($"\n\n<color={UIStyle.Hex(style.Warning)}>The ward is strained. It will mend after {raids} more successful raid{(raids == 1 ? "" : "s")}.</color>");
        }

        if (raid != null && !raid.IsRepaired)
        {
            body.Append("\n\n").Append(raid.RuinText);
        }

        var page = new MenuPage { Title = raid != null && !raid.IsRepaired ? "The Sleeping Portal" : "The Portal", Body = body.ToString() };
        // What most visits are for comes first: going through it.
        raid?.AddActions(page, user);
        if (station != null && station.Usable)
        {
            page.Add(new MenuAction("Make Portal Sparks", "Queue them at the portal's own fire", true, () => station.OpenMenu(user), closesMenu: true));
        }

        if (questLine == null && whispers.Length > 1)
        {
            page.Add(new MenuAction("Listen", null, true, () =>
            {
                whisperIndex = (whisperIndex + 1) % whispers.Length;
                AudioManager.Play(voice, transform.position);
            }));
        }

        int level = settlement.WardLevel;
        if (level < settings.WardLevels.Count)
        {
            SettlementSettings.WardLevel next = settings.WardLevels[level];
            MaterialPool pool = MaterialPool.For(user);
            bool affordable = pool.CanAfford(next.cost, null, out string missing);
            string opens = string.IsNullOrEmpty(next.opens) ? "" : $". {next.opens}";
            string detail = $"{AmountText.Describe(next.cost)}  →  ward {settings.WardRadiusAt(level + 1):0} m{opens}" + (affordable ? "" : $"   <color={UIStyle.Hex(style.Danger)}>{missing}</color>");
            page.Add(new MenuAction("Strengthen the ward", detail, affordable, () => Strengthen(user)));
        }

        if (breach != null && breach.IsPending)
        {
            page.Add(new MenuAction("Begin the defense", breach.Hint, true, breach.Begin, closesMenu: true));
        }

        MenuAction mend = BuildingRepairs.MendAction(user);
        if (mend != null)
        {
            page.Add(mend);
        }

        page.Add(new MenuAction("How is Safe Haven doing?", $"Rank, renown and what to do next ({GameInput.KeyLabel(GameInput.Haven)})", true, OpenHaven));
        return page;
    }

    private void Strengthen(GameObject user)
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Settlement.WardLevel;
        if (level >= settings.WardLevels.Count || !MaterialPool.For(user).TrySpend(settings.WardLevels[level].cost))
        {
            return;
        }

        GameSession.Settlement.RaiseWardLevel();
        string opens = settings.WardLevels[level].opens;
        GameMessages.Post($"The portal glows brighter. The ward now reaches {WardRadius:0} m.{(string.IsNullOrEmpty(opens) ? "" : " " + opens)}", MessageKind.Gain);
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

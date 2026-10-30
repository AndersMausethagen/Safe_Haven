using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runs authored encounters from quest progress; never creates a raid or applies raid loss rules. Everything the player
/// is asked to do shows in the quest tracker like any quest (timed chapters with their time left, QuestTimers); a
/// chapter's arrival text is said in the dialogue box by the companion it names ("Runa: ...") or the leader.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrologueDirector : MonoBehaviour, IBuildArea
{
    [SerializeField] private PrologueDefinition definition;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform campPoint;
    [SerializeField] private Transform portalPoint;
    [SerializeField] private Terrain buildTerrain;
    [SerializeField] private Transform[] campSpawns;
    [SerializeField] private Transform[] portalSpawns;
    [SerializeField] private PrologueCompanion[] companions;
    [SerializeField] private GameObject portalVisual;
    [Tooltip("Retired: the quest tracker shows the chapter now. Hidden when the prologue starts.")]
    [SerializeField] private TMP_Text statusLabel;

    private readonly List<Damageable> enemies = new List<Damageable>();
    private Damageable health;
    private PrologueStep step = PrologueStep.Finished;
    private PrologueChapter chapter;
    private float elapsed;
    private float nextWave;
    private float nextRumble;
    private int wave;
    private bool ready;
    private bool ending;
    private bool reviving;
    private bool chapterActive;

    public static PrologueDirector Current { get; private set; }
    public PrologueDefinition Definition => definition;
    public PrologueStep Step => step;
    public float SecondsRemaining => Mathf.Max(0f, (chapter != null ? chapter.Duration : 0f) - elapsed);
    public bool CanSave => ready && !ending && !reviving && !IsFighting;
    public bool IsFighting => chapterActive && !ending && (step == PrologueStep.DefendCamp || step == PrologueStep.HoldPortal);
    public IReadOnlyList<Damageable> Enemies => enemies;
    public bool ExpeditionStarted => definition != null && definition.Chapters.Length > 0 &&
        GameSession.Quests.StatusOf(definition.Chapters[0].Quest) != QuestStatus.NotStarted;
    public bool HasAcceptedChapter => ready && chapterActive && !ending;
    public bool CanBuild => ready && !ending && chapterActive && step == PrologueStep.LightFire &&
        definition.Campfire != null && BuildingObject.Find(definition.Campfire) == null;
    public Vector3 BuildOrigin => campPoint.position;
    public Terrain BuildTerrain => buildTerrain;

    public bool IsBuildSpot(Vector3 point, float reach)
    {
        Vector3 offset = point - campPoint.position;
        offset.y = 0f;
        return CanBuild && offset.magnitude + reach <= definition.BuildRadius;
    }

    // ----- Building (IBuildArea): only the campfire, beside the shelter, when the leader asks for it -----

    string IBuildArea.WhyClosed => CanBuild ? null : definition.WaitingForLeaderText;
    string IBuildArea.Guidance => "Build the expedition's campfire beside the shelter. ";
    bool IBuildArea.AllowsChanges => false;
    float IBuildArea.GroundHeight => campPoint.position.y;

    IEnumerable<BuildingDefinition> IBuildArea.Buildable(PlayerCrafting crafting)
    {
        yield return definition.Campfire;
    }

    // Forest placement follows the real terrain rather than a flat plane.
    bool IBuildArea.TryGetGroundPoint(Camera view, out Vector3 point)
    {
        point = default;
        if (!CameraPointer.TryGetRay(view, out Ray ray) || !buildTerrain.GetComponent<TerrainCollider>().Raycast(ray, out RaycastHit hit, 200f))
        {
            return false;
        }

        point = hit.point;
        return true;
    }

    Vector3 IBuildArea.OnGround(Vector3 point) => OnBuildGround(point);

    string IBuildArea.CheckSpot(FootprintRect footprint, Vector3 point)
    {
        if (!IsBuildSpot(footprint.WorldCenter(point.y), Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y)))
        {
            return "Build the fire beside the shelter.";
        }

        Vector2 x = footprint.AxisX * footprint.HalfSize.x;
        Vector2 z = footprint.AxisZ * footprint.HalfSize.y;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 sample = footprint.Center + ((corner & 1) == 0 ? x : -x) + ((corner & 2) == 0 ? z : -z);
            if (Mathf.Abs(OnBuildGround(new Vector3(sample.x, 0f, sample.y)).y - point.y) > 0.2f)
            {
                return "The ground is too uneven. Try flatter ground beside the shelter.";
            }
        }

        return null;
    }

    string IBuildArea.CheckBuilding(BuildingDefinition building) => CanBuild && building == definition.Campfire
        ? null : "Only the expedition's campfire can be built here, when the leader asks for it.";

    private Vector3 OnBuildGround(Vector3 point)
    {
        point.y = buildTerrain.SampleHeight(point) + buildTerrain.transform.position.y;
        return point;
    }

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("Only one PrologueDirector may run.", this);
            enabled = false;
            return;
        }
        Current = this;
        BuildArea.Enter(this);
        GameEvents.Raised += OnGameEvent;
    }

    private void Start()
    {
        string problem = definition != null ? definition.FindProblem() : "No prologue definition.";
        if (problem == null && (startPoint == null || campPoint == null || portalPoint == null ||
            campSpawns.Length == 0 || portalSpawns.Length == 0 || portalVisual == null ||
            buildTerrain == null || buildTerrain.GetComponent<TerrainCollider>() == null))
            problem = "Prologue scene references are incomplete.";
        if (problem != null || Player.Current == null)
        {
            Debug.LogError(problem ?? "The prologue has no player.", this);
            enabled = false;
            return;
        }
        health = Player.Current.Health;
        health.Died += OnPlayerDied;
        if (statusLabel != null) statusLabel.gameObject.SetActive(false);
        SceneTransition.WhenReady(Begin);
    }

    private void Begin()
    {
        if (this == null) return;
        ready = true;
        if (!GameSession.Quests.HasHappened(definition.StartedEvent))
        {
            if (!GiveStartingItem(definition.StartingWeapon)) return;
            if (!GiveStartingItem(definition.StartingTorch)) return;
            foreach (var armor in definition.StartingArmor)
                if (!GiveStartingItem(armor)) return;
        }
        if (definition.StartingWeapon != null && Player.Current.Equipment != null)
            Player.Current.Equipment.AutoEquip(definition.StartingWeapon);
        if (definition.StartingTorch != null && Player.Current.Equipment != null)
            Player.Current.Equipment.AutoEquip(definition.StartingTorch);
        if (Player.Current.Equipment != null)
            foreach (var armor in definition.StartingArmor)
                Player.Current.Equipment.AutoEquip(armor);
        GameEvents.Raise(definition.StartedEvent);
        RefreshStep(true);
    }

    private bool GiveStartingItem(ItemDefinition item)
    {
        if (item == null) return true;
        if (Player.Current.GetComponent<ItemInventory>().Give(item, 1) == 1) return true;
        ready = false;
        Debug.LogError("The expedition starting loadout does not fit in the player's bag: " + item.DisplayName, this);
        GameMessages.Post("The expedition's starting equipment could not be granted. Check the starting loadout.", MessageKind.Warning);
        return false;
    }

    private void OnDestroy()
    {
        BuildArea.Leave(this);
        GameEvents.Raised -= OnGameEvent;
        if (health != null)
        {
            health.Died -= OnPlayerDied;
            health.RemoveInvulnerability(this);
        }
        if (Current == this) Current = null;
    }

    private void Update()
    {
        if (!ready || ending || reviving) return;
        RefreshStep(false);
        if (!ready || ending) return;
        if (!chapterActive)
        {
            return;
        }
        elapsed += Time.deltaTime;
        enemies.RemoveAll(enemy => enemy == null || !enemy.IsAlive);
        if (step == PrologueStep.CatchBreath && elapsed >= chapter.Duration)
            GameEvents.Raise(definition.RestedEvent);
        else if (step == PrologueStep.DefendCamp && enemies.Count == 0)
            GameEvents.Raise(definition.CampDefendedEvent);
        else if (step == PrologueStep.HoldPortal)
        {
            if (elapsed >= chapter.Duration)
            {
                BeginEnding(false);
                return;
            }
            if (elapsed >= nextWave)
            {
                wave++;
                Spawn(portalSpawns, definition.WaveSize(wave));
                nextWave += definition.WaveInterval;
            }
            if (elapsed >= nextRumble)
            {
                Rumble(0.12f + elapsed / chapter.Duration * 0.3f);
                nextRumble += 18f;
            }
        }
    }

    private void RefreshStep(bool restoring)
    {
        PrologueChapter next = definition.Chapter(GameSession.Quests);
        bool active = next != null && GameSession.Quests.StatusOf(next.Quest) != QuestStatus.NotStarted;
        if (!restoring && next == chapter && chapterActive == active) return;
        if (chapter != null) QuestTimers.Clear(chapter.Quest);
        chapter = next;
        chapterActive = active;
        step = chapter != null ? chapter.Behaviour : PrologueStep.Finished;
        elapsed = 0f;
        nextWave = definition.WaveInterval;
        nextRumble = 12f;
        wave = 0;
        if (IsFighting && ActionMenuWindow.Current != null) ActionMenuWindow.Current.Close();
        portalVisual.SetActive(step.PortalOpen());
        PrologueSite? waitAt = step.Rally();
        Transform rally = waitAt.HasValue ? PointAt(waitAt.Value) : null;
        foreach (PrologueCompanion companion in companions)
            if (companion != null) companion.RallyAt(rally, restoring);
        if (restoring)
            MovePlayer(PointAt(step.Checkpoint()));
        if (step == PrologueStep.Finished)
        {
            BeginEnding(false);
            return;
        }

        if (chapterActive)
        {
            // In a fight the line becomes a subtitle once the enemies close in (DialogueBox), so nothing waits for it.
            if (chapter.Quest.Start != QuestStart.ByQuestGiver && !string.IsNullOrWhiteSpace(chapter.ArrivalText))
                Speak(chapter.ArrivalText, null);
            StartChapter();
        }
        if (CanSave && !SaveGameService.TrySave(out string problem))
            GameMessages.Post("Prologue checkpoint could not be saved: " + problem, MessageKind.Warning);
    }

    private Transform PointAt(PrologueSite site) =>
        site == PrologueSite.Portal ? portalPoint : site == PrologueSite.Camp ? campPoint : startPoint;

    // The chapter's clock and arrivals: wolves, the portal's rumble.
    private void StartChapter()
    {
        elapsed = 0f;
        if (chapter.Duration > 0f)
            QuestTimers.Set(chapter.Quest, chapter.Duration);
        if (step == PrologueStep.DefendCamp)
            Spawn(campSpawns, chapter.InitialEnemies);
        else if (step == PrologueStep.ReachPortal)
            Rumble(0.4f);
        else if (step == PrologueStep.HoldPortal)
            Spawn(portalSpawns, chapter.InitialEnemies);
    }

    // "Runa: Wolves!" is said by Runa; a line without a name by the leader. Done runs once the player has moved on
    // from the line (or the box closed another way), or at once when no one can say it.
    private void Speak(string text, System.Action done)
    {
        PrologueCompanion speaker = null;
        int colon = text.IndexOf(':');
        if (colon > 0 && colon < 16)
        {
            string name = text.Substring(0, colon).Trim();
            speaker = System.Array.Find(companions, c => c != null && c.Resident != null && c.Resident.DisplayName == name);
            if (speaker != null) text = text.Substring(colon + 1).Trim();
        }

        if (speaker == null && definition.Leader != null)
            speaker = System.Array.Find(companions, c => c != null && c.isActiveAndEnabled && c.Resident == definition.Leader);
        if (speaker == null)
        {
            GameMessages.Post(text);
            done?.Invoke();
            return;
        }

        DialogueBox box = DialogueBox.Current;
        System.Action finish = null;
        if (done != null)
        {
            // Closed also covers a line another story says over this one, which drops this line's callback.
            finish = () =>
            {
                box.Closed -= finish;
                done();
            };
            box.Closed += finish;
        }

        box.Say(new DialogueSpeaker(speaker.Resident.DisplayName, speaker.transform, null, definition.DialogueCameraDistance), text, finish);
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        if (ready && chapterActive && step == PrologueStep.LightFire &&
            gameEvent.Kind == GameEventKind.Built && gameEvent.Subject == definition.Campfire && gameEvent.ByPlayer)
            GameEvents.Raise(definition.FireEvent, gameEvent.Actor);
    }

    private void Spawn(Transform[] points, int count)
    {
        int spawned = 0;
        for (int i = 0; i < count && enemies.Count < definition.MaximumEnemies; i++)
        {
            Transform point = points[(wave + i) % points.Length];
            if (point == null || !NavMesh.SamplePosition(point.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                Debug.LogError("A prologue encounter spawn is not on the NavMesh.", this);
                continue;
            }
            GameObject prefab = chapter.Enemy(wave, Random.value);
            if (prefab == null) continue;
            GameObject enemy = Instantiate(prefab, hit.position, point.rotation);
            enemies.Add(enemy.GetComponent<Damageable>());
            MonsterAI ai = enemy.GetComponent<MonsterAI>();
            if (ai != null) ai.Roam(Player.Current.transform.position);
            spawned++;
        }
        if (spawned == 0 && enemies.Count == 0)
        {
            Debug.LogError("Prologue encounter could not spawn any enemies; progression has been stopped.", this);
            ready = false;
            GameMessages.Post("The encounter could not start. Reload the last checkpoint.", MessageKind.Warning);
        }
    }

    private void Rumble(float strength)
    {
        CameraShake.Add(strength);
        AudioManager.Play(definition.Rumble, Player.Current.transform.position);
    }

    private void OnPlayerDied(Damageable victim, DamageInfo hit)
    {
        if (ending || reviving) return;
        if (step == PrologueStep.HoldPortal) BeginEnding(true);
        else StartCoroutine(RecoverAtCheckpoint());
    }

    private IEnumerator RecoverAtCheckpoint()
    {
        reviving = true;
        GameMessages.Post(definition.RecoveryText, MessageKind.Warning);
        yield return new WaitForSeconds(definition.RecoverySeconds);
        ClearEnemies();
        health.Revive(1f);
        if (Player.Current.TryGetComponent(out PlayerAnimationController body)) body.SetDead(false);
        RefreshStep(true);
        reviving = false;
    }

    private void ClearEnemies()
    {
        foreach (Damageable enemy in enemies)
            if (enemy != null) Destroy(enemy.gameObject);
        enemies.Clear();
    }

    private void MovePlayer(Transform point)
    {
        CharacterController body = Player.Current.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (wasEnabled) body.enabled = false;
        Player.Current.transform.SetPositionAndRotation(point.position, point.rotation);
        if (wasEnabled) body.enabled = true;
    }

    private void BeginEnding(bool overwhelmed)
    {
        if (ending) return;
        if (!Application.CanStreamedLevelBeLoaded(definition.HomeScene))
        {
            Debug.LogError("The prologue home scene is missing from Build Settings.", this);
            ready = false;
            return;
        }
        ending = true;
        if (chapter != null) QuestTimers.Clear(chapter.Quest);
        StartCoroutine(EndSequence(overwhelmed));
    }

    private IEnumerator EndSequence(bool overwhelmed)
    {
        health.AddInvulnerability(this);
        ClearEnemies();
        portalVisual.SetActive(true);
        PlayerInputHandler input = Player.Current.GetComponent<PlayerInputHandler>();
        input.enabled = false;
        if (ActionMenuWindow.Current != null) ActionMenuWindow.Current.Close();
        Rumble(0.65f);
        GameMessages.Post(overwhelmed ? definition.OverwhelmedText : definition.SurvivedText);
        if (statusLabel != null) statusLabel.text = definition.EndingStatus;
        yield return new WaitForSeconds(definition.EndingSeconds);
        GameEvents.Raise(definition.FinishedEvent);
        GameSession.SetArrivalPoint(PlayerArrivalPoint.HeartTree);
        // The expedition's supplies are not the settlement's starting inventory. No raid loss or durability penalty.
        if (!SceneLoader.Load(definition.HomeScene, "Beneath the Heart Tree"))
        {
            Debug.LogError("The prologue could not start its home transition.", this);
            GameMessages.Post("The return to Safe Haven failed. Reload the last checkpoint.", MessageKind.Warning);
            health.RemoveInvulnerability(this);
            input.enabled = true;
            ending = false;
            ready = false;
        }
    }
}

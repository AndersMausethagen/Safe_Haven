using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runs authored encounters from quest progress; never creates a raid or applies raid loss rules. Everything the player
/// is asked to do shows in the quest tracker like any quest (timed chapters with their time left, QuestTimers); a
/// chapter's arrival text is said in the dialogue box by the companion it names ("Runa: ...") or the leader. Where the
/// player may build meanwhile (the campfire, when the leader asks) is its <see cref="PrologueBuildArea"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrologueDirector : MonoBehaviour
{
    [SerializeField] private PrologueDefinition definition;
    [Tooltip("The forest's edge, where the expedition stops inside the trees (PrologueSite.Forest).")]
    [SerializeField] private Transform startPoint;
    [Tooltip("The morning fire in the meadow, where the game starts (PrologueSite.Meadow).")]
    [SerializeField] private Transform meadowPoint;
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
    private bool wolvesComing; // the camp's warning line is up; its wolves spawn when it closes
    private const float BetweenCampWaves = 3f;
    private PrologueBuildArea buildArea;

    public static PrologueDirector Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;
    public PrologueDefinition Definition => definition;
    public PrologueStep Step => step;
    public float SecondsRemaining => Mathf.Max(0f, (chapter != null ? chapter.Duration : 0f) - elapsed);
    public bool CanSave => ready && !ending && !reviving && !IsFighting;
    public bool IsFighting => chapterActive && !ending && (step == PrologueStep.DefendCamp || step == PrologueStep.HoldPortal);
    public IReadOnlyList<Damageable> Enemies => enemies;
    /// <summary>True while the companions stand still and take no orders: before the game has begun and around the morning fire.</summary>
    public bool CompanionsHeld => !ready || chapter == null || chapter.Companions == PrologueCompanions.StandStill;
    /// <summary>True while the companions leave enemies to the player.</summary>
    public bool CompanionsHoldFire => chapter != null && (chapter.Companions == PrologueCompanions.WatchOnly || chapter.Companions == PrologueCompanions.Flee);
    /// <summary>The place the companions talk about now.</summary>
    public PrologueSite TalkSite => chapter != null ? chapter.Talk : PrologueSite.Portal;
    /// <summary>True while there is time to talk (the companions' topics).</summary>
    public bool AtRest => chapter != null && chapter.AtRest;
    public bool HasAcceptedChapter => ready && chapterActive && !ending;
    public bool CanBuild => ready && !ending && chapterActive && step == PrologueStep.LightFire &&
        definition.Campfire != null && BuildingObject.Find(definition.Campfire) == null;
    public Terrain BuildTerrain => buildTerrain;

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("Only one PrologueDirector may run.", this);
            enabled = false;
            return;
        }
        Current = this;
        buildArea = new PrologueBuildArea(this, campPoint, buildTerrain);
        BuildArea.Enter(buildArea);
        GameEvents.Raised += OnGameEvent;
    }

    private void Start()
    {
        string problem = definition != null ? definition.FindProblem() : "No prologue definition.";
        if (problem == null && (startPoint == null || meadowPoint == null || campPoint == null || portalPoint == null ||
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
            // First, and on at once: the room it gives is what the rest goes into.
            if (!GiveStartingItem(definition.StartingBackpack)) return;
            if (definition.StartingBackpack != null && Player.Current.Equipment != null)
                Player.Current.Equipment.AutoEquip(definition.StartingBackpack);
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
        if (buildArea != null) BuildArea.Leave(buildArea);
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
        else if (step == PrologueStep.DefendCamp && enemies.Count == 0 && !wolvesComing)
        {
            // They come again once the last of a wave has fallen, after a breath to see it is over.
            if (wave >= chapter.Waves) GameEvents.Raise(definition.CampDefendedEvent);
            else if (nextWave < 0f) nextWave = elapsed + BetweenCampWaves;
            else if (elapsed >= nextWave)
            {
                nextWave = -1f;
                wave++;
                if (!string.IsNullOrWhiteSpace(chapter.NextWaveText)) Speak(chapter.NextWaveText, null);
                Spawn(campSpawns, chapter.InitialEnemies);
            }
        }
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
        // An attack cuts a conversation short, answers and all: it is the point of being attacked.
        if (IsFighting && !restoring && DialogueBox.IsOpen) DialogueBox.Current.Close();
        portalVisual.SetActive(step.PortalOpen());
        // A story leads them once it gets going; until then they wait at the chapter's place like anyone.
        PrologueCompanions orders = chapter != null ? chapter.Companions : PrologueCompanions.WaitAtSite;
        if (orders != PrologueCompanions.StandStill && (orders != PrologueCompanions.StoryLeads || restoring))
        {
            Transform rally = orders == PrologueCompanions.Follow ? null : PointAt(chapter != null ? chapter.RallySite : PrologueSite.Portal);
            foreach (PrologueCompanion companion in companions)
                if (companion != null) companion.RallyAt(rally, restoring);
        }

        if (restoring)
            MovePlayer(PointAt(chapter != null ? chapter.Checkpoint : step.Checkpoint()));
        if (step == PrologueStep.Finished)
        {
            BeginEnding(false);
            return;
        }

        if (chapterActive)
        {
            // A briefing the player accepted has been said by its giver; a later part of the same quest is called out in passing.
            bool opens = definition.OpensItsQuest(chapter);
            bool line = (!opens || chapter.Quest.Start != QuestStart.ByQuestGiver) && !string.IsNullOrWhiteSpace(chapter.ArrivalText);
            if (line && step == PrologueStep.DefendCamp && !restoring)
            {
                // The camp's wolves come once the warning has been spoken (an aside, so the player is free meanwhile).
                wolvesComing = true;
                Speak(chapter.ArrivalText, () =>
                {
                    if (!wolvesComing || !chapterActive || step != PrologueStep.DefendCamp) return;
                    wolvesComing = false;
                    StartChapter();
                }, false);
            }
            else
            {
                // In a fight the line becomes a subtitle once the enemies close in (DialogueBox), so nothing waits for it.
                if (line)
                    Speak(chapter.ArrivalText, null, !opens);
                wolvesComing = false;
                StartChapter();
            }
        }
        if (CanSave && !SaveGameService.TrySave(out string problem))
            GameMessages.Post("Prologue checkpoint could not be saved: " + problem, MessageKind.Warning);
    }

    private Transform PointAt(PrologueSite site) =>
        site == PrologueSite.Portal ? portalPoint : site == PrologueSite.Camp ? campPoint :
        site == PrologueSite.Meadow ? meadowPoint : startPoint;

    // The chapter's clock and arrivals: wolves, the portal's rumble.
    private void StartChapter()
    {
        elapsed = 0f;
        if (chapter.Duration > 0f)
            QuestTimers.Set(chapter.Quest, chapter.Duration);
        if (step == PrologueStep.DefendCamp)
        {
            wave = 1;
            nextWave = -1f;
            Spawn(campSpawns, chapter.InitialEnemies);
        }
        else if (step == PrologueStep.ReachPortal)
            Rumble(0.4f);
        else if (step == PrologueStep.HoldPortal)
            Spawn(portalSpawns, chapter.InitialEnemies);
    }

    // "Runa: Wolves!" is said by Runa; a line without a name by the leader. Done runs once the player has moved on
    // from the line (or the box closed another way), or at once when no one can say it. In passing: a subtitle that
    // leaves the controls and the camera to the player, for a word said while they are busy.
    private void Speak(string text, System.Action done, bool inPassing = false)
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
        var who = new DialogueSpeaker(speaker.Resident.DisplayName, speaker.transform, null, definition.DialogueCameraDistance);
        // Before a fight a word is enough: a conversation box and the camera turning to the speaker stopped the play.
        if (inPassing || step == PrologueStep.DefendCamp || step == PrologueStep.HoldPortal)
        {
            box.Aside(who, text, done);
            return;
        }

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

        box.Say(who, text, finish, subtitleTimeMultiplier: 1.8f);
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
        // The ending takes the camera and the controls: a story cinematic still playing gives them up first.
        InGameCinematic.StopAll();
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
        GameSession.SetArrivalPoint(PlayerArrivalPoint.HomePortal);
        // The expedition's supplies are not the settlement's starting inventory: what was worn comes through, as after a
        // failed raid, and what was carried does not. No durability penalty.
        if (definition.WornGearComesHome)
            GameSession.CarryPlayerState(PlayerSnapshot.Capture(Player.Current.gameObject).WhatWasWorn());
        // The portal tells what happened at the stones, over the storm, while home loads behind it. The cinematic
        // holds the game still, which does not stop this from waiting a frame at a time.
        if (definition.EndingCinematic != null)
        {
            if (definition.LoadHomeDuringCinematic) SceneTransition.Preload(definition.HomeScene);
            bool told = false;
            CinematicScreen.Play(definition.EndingCinematic, () => told = true);
            while (!told) yield return null;
        }
        if (!SceneLoader.Load(definition.HomeScene, "Beside the old portal"))
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

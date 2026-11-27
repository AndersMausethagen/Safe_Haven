using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The Keeper's view (Docs/ColonyModeResearch_2026-11-21.md, the user's go-ahead 2026-11-21): at home, a key (`, System › Keeper
/// View), the Haven window's "Look over the Haven" or the controller's windows wheel lifts the camera off the player into a free
/// view over the Haven, to read the town, pick people and buildings and place buildings from above, the way Gord and RimWorld are
/// played. The player's character waits where it stands; the same key, Escape or the button in the corner brings the camera back
/// down to it.
/// <para>How it works: the camera stays the player's (PlayerCameraController); this is its overview (IOverview), which it glides
/// into and out of. While open the player's controls are held, keeping the window keys (PlayerControl.Kept.Windows), which this
/// routes itself since the input handler is off under a hold; the Keeper map (GameInput.Keeper*) and the Camera map's wheel, right
/// button and right stick move the view (KeeperCameraRig). Sounds are heard from over the middle of the view, the see-through and
/// the fog of war give way, and the player's HUD stands aside (HudGivesWay).</para>
/// <para>When: KeeperViewRules, from KeeperViewSettings (opens once The Road North has started; Cheats › No Requirements opens it
/// anyway). It gives the player back their feet the moment they are hunted or hurt, a breach's fight starts or a cinematic or a
/// conversation begins. On the HomePortal, added by it, so only in the Haven.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class KeeperView : MonoBehaviour, PlayerCameraController.IOverview
{
    // Escape closes a building being placed (300) and windows (200) first, then the view.
    private const int BackPriority = 60;

    /// <summary>The Haven's view, while the Haven is the scene.</summary>
    public static KeeperView Current { get; private set; }

    /// <summary>True while the Keeper's view is open.</summary>
    public static bool IsOpen => Current != null && Current.open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    private readonly KeeperPicker picker = new KeeperPicker();
    private KeeperViewSettings settings;
    private KeeperCameraRig rig;
    private KeeperHud hud;
    private bool open;
    private Player player;
    private SeeThrough seeThrough;
    private Material outlineMaterial;
    private Material maskMaterial;
    private Transform following;
    private float hurtAt = float.NegativeInfinity;
    private bool warHeld;

    private Vector3 focus;
    private Vector3 focusVelocity;
    private float zoomNow;
    private float zoomVelocity;
    private float yawNow;
    private float yawVelocity;
    private bool dragging;
    private Vector3 dragAnchor;
    // Drawing a ring for the woodcutters (RingWork): its radius, and the lines on the ground.
    private bool ringPlacing;
    private RingKind ringKind;
    private float ringRadius;
    private KeeperRings ringLines;
    // Setting a guard post (GuardPosts): its marks on the ground.
    private const float PostMarkRadius = 1.6f;
    private bool postPlacing;
    private KeeperRings postLines;
    private KeeperRings laneLines;
    private readonly System.Collections.Generic.List<Vector3[]> laneRoads = new System.Collections.Generic.List<Vector3[]>();
    private System.Func<bool> back;

    // ----- IOverview: what the player's camera frames while open -----

    /// <summary>The ground point in the middle of the view.</summary>
    public Vector3 Focus => focus;
    public float Distance => Mathf.Lerp(Settings.MinDistance, Settings.MaxDistance, zoomNow);
    public float Pitch => Mathf.Lerp(Settings.PitchNear, Settings.PitchFar, zoomNow);
    public float Yaw => yawNow;
    public float FieldOfView => Settings.FieldOfView;

    private KeeperViewSettings Settings => settings != null ? settings : settings = KeeperViewSettings.Current;

    // ----- Opening and closing -----

    /// <summary>Why the view can't open now (said to the player; empty: nothing to say), or null when it can.</summary>
    public string WhyNot() => KeeperViewRules.WhyNot(Situation());

    /// <summary>Opens the view, or says why it can't. Nothing happens while it is open.</summary>
    public void Open()
    {
        if (open)
        {
            return;
        }

        string why = WhyNot();
        if (why != null)
        {
            if (why.Length > 0)
            {
                GameMessages.Post(why, MessageKind.Warning);
            }

            return;
        }

        player = Player.Current;
        PlayerCameraController view = PlayerCameraController.Current;
        if (player.TryGetComponent(out PlayerInteractor interactor) && interactor.Current != null)
        {
            interactor.CancelInteraction();
        }

        Vector3 start = Ground(player.transform.position);
        rig = new KeeperCameraRig(start, view.ViewYaw, Settings.StartZoom);
        focus = start;
        focusVelocity = Vector3.zero;
        zoomNow = Settings.StartZoom;
        zoomVelocity = 0f;
        yawNow = view.ViewYaw;
        yawVelocity = 0f;
        following = null;
        dragging = false;

        open = true;
        PlayerControl.Hold(player.gameObject, this, PlayerControl.Kept.Windows);
        RouteWindowKeys(true);
        view.BeginOverview(this, Settings.GlideSeconds);
        seeThrough = player.GetComponent<SeeThrough>();
        if (seeThrough != null)
        {
            seeThrough.Active = false;
        }

        if (player.TryGetComponent(out PlayerInteractionOutline outline))
        {
            outlineMaterial = outline.OutlineMaterial;
            maskMaterial = outline.MaskMaterial;
        }

        if (hud == null)
        {
            hud = KeeperHud.Create(transform);
        }

        hud.Show(true);
        warHeld = true;
    }

    /// <summary>Brings the camera back down to the player, saying <paramref name="why"/> if there is something to say.</summary>
    public void Close(string why)
    {
        if (!open)
        {
            return;
        }

        open = false;
        if (!string.IsNullOrEmpty(why))
        {
            GameMessages.Post(why, MessageKind.Warning);
        }

        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.EndOverview(this);
        }

        if (player != null)
        {
            PlayerControl.Release(player.gameObject, this);
        }

        RouteWindowKeys(false);
        if (seeThrough != null)
        {
            seeThrough.Active = true;
        }

        AudioManager.HearFrom = null;
        ringPlacing = false;
        postPlacing = false;
        if (ringLines != null)
        {
            ringLines.Hide();
        }

        if (postLines != null)
        {
            postLines.Hide();
        }

        if (laneLines != null)
        {
            laneLines.Hide();
        }

        picker.Clear();
        following = null;
        dragging = false;
        if (hud != null)
        {
            hud.Show(false);
        }
    }

    /// <summary>Opens it if it is shut, shuts it if it is open: the key, the wheel.</summary>
    public void Toggle()
    {
        if (open)
        {
            Close(null);
        }
        else
        {
            Open();
        }
    }

    private KeeperSituation Situation()
    {
        Player who = Player.Current;
        Damageable health = who != null ? who.Health : null;
        bool atHome = HomePortal.Current != null && who != null && health != null && health.IsAlive && PlayerCameraController.Current != null;
        if (!atHome)
        {
            return new KeeperSituation(false, false, false, false, false, false, false, false, false);
        }

        QuestDefinition unlock = Settings.UnlockedBy;
        bool unlocked = unlock == null || Cheats.NoRequirements || GameSession.Quests.StatusOf(unlock) != QuestStatus.NotStarted;
        bool breachFight = WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive;
        bool cinematic = CinematicScreen.IsPlaying || InGameCinematic.IsPlaying;
        bool afloat = (who.TryGetComponent(out PlayerStateMachine state) && (state.IsSwimming || state.IsAboard))
            || (who.TryGetComponent(out PlayerRiding riding) && riding.IsRiding);
        bool hunted = MonsterAI.AnyEngaging(health);
        bool hurt = Time.time - hurtAt < Settings.HurtGraceSeconds;
        bool heldByOthers = PlayerControl.HeldOn(who.gameObject) && !PlayerControl.HeldBy(who.gameObject, this);
        return new KeeperSituation(true, unlocked, breachFight, cinematic, DialogueBox.IsOpen, afloat, hunted, hurt, heldByOthers);
    }

    // ----- Lifetime -----

    private void Awake() => back = OnBack;

    private void OnEnable()
    {
        Current = this;
        Damageable.AnyDamaged += OnAnyDamaged;
        BackHandlers.Register(BackPriority, back);
        if (GameInput.KeeperView != null)
        {
            GameInput.KeeperView.performed += OnToggleKey;
        }
    }

    private void OnDisable()
    {
        Close(null);
        Damageable.AnyDamaged -= OnAnyDamaged;
        BackHandlers.Unregister(back);
        if (GameInput.KeeperView != null)
        {
            GameInput.KeeperView.performed -= OnToggleKey;
        }

        if (warHeld)
        {
            AtmosphereFeature.WarScale = 1f;
            warHeld = false;
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnToggleKey(InputAction.CallbackContext context)
    {
        // Typing a name or choosing a key: the key is a letter there, not the view.
        if (GameInput.IsGameplaySuspended || InputRebinds.IsListening || GamePause.IsPaused)
        {
            return;
        }

        Toggle();
    }

    private bool OnBack()
    {
        if (!open)
        {
            return false;
        }

        if (ringPlacing || postPlacing)
        {
            ringPlacing = false;
            postPlacing = false;
            return true;
        }

        // An order lit on the bar with the d-pad: Back lets go of it first.
        if (hud != null && hud.ClearFocus())
        {
            return true;
        }

        Close(null);
        return true;
    }

    private void OnAnyDamaged(Damageable hurt, DamageInfo info, DamageResult result)
    {
        // A blow struck by something: not a poison's or a burn's tick (nothing is after the player then), nor harm with no striker.
        if (Player.Current != null && hurt == Player.Current.Health && result.Landed && !info.IsOverTime && info.Source != null)
        {
            hurtAt = Time.time;
        }
    }

    private void Update()
    {
        HoldWar();
        if (!open)
        {
            return;
        }

        string leave = KeeperViewRules.WhyLeave(Situation());
        if (leave != null || player == null || PlayerCameraController.Current == null)
        {
            Close(leave);
            return;
        }

        bool windowUp = (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen) || CommandWheel.IsHeld;
        bool placing = BuildPlacer.IsBusy;
        bool overUI = !InputDevices.UsingGamepad && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!windowUp && !GamePause.IsPaused)
        {
            ReadInput(placing, overUI);
        }

        // Someone followed who left the Haven (a foray hides them) is let go, as the people column already shows.
        if (following != null && !following.gameObject.activeInHierarchy)
        {
            following = null;
        }

        if (following != null)
        {
            rig.Target = following.position;
        }

        if (HomePortal.Current != null)
        {
            rig.KeepWithin(HomePortal.Current.transform.position, Settings.BoundsRadius);
        }

        rig.Target = Ground(rig.Target);
        float dt = Time.unscaledDeltaTime;
        focus = Vector3.SmoothDamp(focus, rig.Target, ref focusVelocity, Settings.SettleSeconds, Mathf.Infinity, dt);
        zoomNow = Mathf.SmoothDamp(zoomNow, rig.Zoom, ref zoomVelocity, Settings.SettleSeconds, Mathf.Infinity, dt);
        yawNow = Mathf.SmoothDampAngle(yawNow, rig.Yaw, ref yawVelocity, Settings.SettleSeconds, Mathf.Infinity, dt);
        AudioManager.HearFrom = focus + Vector3.up * Settings.EarHeight;

        DrawRings(windowUp || placing || overUI);
        Point(windowUp || placing || overUI || ringPlacing || postPlacing);
    }

    // The fog of war is a flat plane at the player's height: from above it lay in bands over the hills. It fades as the camera rises,
    // as in a cinematic, and comes back as it comes down; written only while this view has it.
    private void HoldWar()
    {
        PlayerCameraController view = PlayerCameraController.Current;
        float weight = view != null ? view.OverviewWeight : 0f;
        // A cinematic that closed the view fades the fog of war itself (CinematicView): two writers made it flicker.
        if (!open && (CinematicScreen.IsPlaying || InGameCinematic.IsPlaying))
        {
            warHeld = false;
            return;
        }

        if (weight > 0f || warHeld)
        {
            AtmosphereFeature.WarScale = 1f - weight;
            warHeld = weight > 0f || open;
        }
    }

    // ----- Input -----

    private void ReadInput(bool placing, bool overUI)
    {
        KeeperViewSettings tuning = Settings;
        float dt = Time.unscaledDeltaTime;
        Vector2 pan = GameInput.KeeperPan != null ? GameInput.KeeperPan.ReadValue<Vector2>() : Vector2.zero;
        if (pan.sqrMagnitude > 0.0004f)
        {
            rig.Pan(pan, dt, tuning);
            following = null;
        }

        float turn = GameInput.KeeperTurn != null ? GameInput.KeeperTurn.ReadValue<float>() : 0f;
        Vector2 stick = GameInput.CameraLookStick != null ? GameInput.CameraLookStick.ReadValue<Vector2>() : Vector2.zero;
        if (stick.sqrMagnitude < 0.02f)
        {
            stick = Vector2.zero;
        }

        rig.Turn((turn + stick.x) * tuning.TurnSpeed * dt);
        rig.ZoomBy(-stick.y * tuning.StickZoomSpeed * dt);

        // The mouse: the wheel zooms, the right button turns, the middle button drags the ground (not over a window or while placing,
        // where the wheel turns the building and the buttons place it).
        if (!placing && !overUI)
        {
            float scroll = GameInput.CameraZoom != null ? GameInput.CameraZoom.ReadValue<float>() : 0f;
            if (Mathf.Abs(scroll) > 0.01f && ringPlacing)
            {
                ringRadius = Mathf.Clamp(ringRadius + Mathf.Sign(scroll) * 1.5f, 4f, 30f);
            }
            else if (Mathf.Abs(scroll) > 0.01f)
            {
                rig.ZoomBy(-Mathf.Sign(scroll) * tuning.ZoomStep);
            }

            if (GameInput.CameraOrbit != null && GameInput.CameraOrbit.IsPressed() && GameInput.CameraLook != null)
            {
                rig.Turn(GameInput.CameraLook.ReadValue<Vector2>().x * tuning.DragTurnSensitivity);
            }
        }

        Drag(placing || overUI);

        // A controller's way to the HUD: the d-pad along the orders and through the people, the bumpers through the alerts; A gives the
        // order lit instead of picking.
        if (!placing && hud != null)
        {
            if (Pressed(GameInput.KeeperCommand, out int along))
            {
                hud.StepCommand(along);
            }

            if (Pressed(GameInput.KeeperPerson, out int down))
            {
                hud.StepPerson(down);
            }

            if (Pressed(GameInput.KeeperAlert, out int next))
            {
                hud.StepAlert(next);
            }

            if (hud.HasFocus && GameInput.KeeperSelect != null && GameInput.KeeperSelect.WasPressedThisFrame())
            {
                hud.RunFocused();
                return;
            }
        }

        // While placing a building its keys are the build map's (a pad's Y frees the placement).
        if (!placing && GameInput.KeeperFindMe != null && GameInput.KeeperFindMe.WasPressedThisFrame() && player != null)
        {
            following = null;
            rig.Target = player.transform.position;
        }

        if (!placing && GameInput.KeeperFollow != null && GameInput.KeeperFollow.WasPressedThisFrame())
        {
            following = following == null && picker.Picked is ResidentNpc person ? person.transform : null;
        }

        if ((ringPlacing || postPlacing) && !overUI)
        {
            if (GameInput.KeeperSelect != null && GameInput.KeeperSelect.WasPressedThisFrame() && RingSpot(out Vector3 at))
            {
                if (ringPlacing)
                {
                    LayRing(at);
                }
                else
                {
                    LayPost(at);
                }
            }
            else if (GameInput.CameraOrbit != null && GameInput.CameraOrbit.WasPressedThisFrame())
            {
                ringPlacing = false;
                postPlacing = false;
            }

            return;
        }

        if (!placing && !overUI && GameInput.KeeperSelect != null && GameInput.KeeperSelect.WasPressedThisFrame())
        {
            Pick(picker.Hovered);
        }
    }

    // ----- From the HUD: the people, the alerts and the command bar (KeeperHud) -----

    /// <summary>Flies the view to <paramref name="at"/> (an alert); stops following anyone.</summary>
    public void LookAt(Vector3 at)
    {
        if (!open || rig == null)
        {
            return;
        }

        following = null;
        rig.Target = at;
    }

    /// <summary>Flies the view to <paramref name="who"/> and keeps them in the middle until the view is moved (a person's card).</summary>
    public void FollowPerson(Transform who)
    {
        if (!open || rig == null || who == null)
        {
            return;
        }

        following = who;
        rig.Target = who.position;
    }

    /// <summary>Who the view is following now, or null.</summary>
    public Transform Following => following;

    /// <summary>True while a ring of <paramref name="kind"/> is being drawn.</summary>
    public bool IsDrawing(RingKind kind) => open && ringPlacing && ringKind == kind;

    /// <summary>True while a guard post is being set.</summary>
    public bool IsSettingPost => open && postPlacing;

    /// <summary>Stops drawing a ring or setting a post (the command's button pressed again).</summary>
    public void StopPlacing()
    {
        ringPlacing = false;
        postPlacing = false;
    }

    /// <summary>The build menu, as at home on foot (placing works from the view).</summary>
    public void OpenBuildMenu()
    {
        if (!open || player == null)
        {
            return;
        }

        StopPlacing();
        if (player.TryGetComponent(out BuildPlacer placer))
        {
            placer.ToggleMenu();
        }
    }

    // An axis pressed this frame (a d-pad or bumper), as -1 or 1.
    private static bool Pressed(UnityEngine.InputSystem.InputAction action, out int sign)
    {
        sign = 0;
        if (action == null || !action.WasPressedThisFrame())
        {
            return false;
        }

        float value = action.ReadValue<float>();
        sign = value > 0f ? 1 : value < 0f ? -1 : 0;
        return sign != 0;
    }

    // The ground point under the pointer when the middle button went down stays under it as the mouse moves.
    private void Drag(bool blocked)
    {
        bool held = !blocked && GameInput.KeeperDrag != null && GameInput.KeeperDrag.IsPressed();
        if (!held || !TryPointer(out Ray ray) || !TryGround(ray, out Vector3 under))
        {
            dragging = false;
            return;
        }

        if (!dragging)
        {
            dragging = true;
            dragAnchor = under;
            following = null;
            return;
        }

        Vector3 moved = dragAnchor - under;
        moved.y = 0f;
        rig.Target += moved;
        // At once, not smoothed: a drag that lags the hand slides.
        focus += moved;
    }

    // ----- Rings for the woodcutters (RingWork) -----

    /// <summary>Starts drawing a ring for the woodcutters to fell, or says why not (no woodcutter, too many rings, a friend's Haven).</summary>
    public void BeginRing() => BeginRing(RingKind.Fell);

    /// <summary>Starts drawing a ring of <paramref name="kind"/> (trees for the woodcutters, stone for the masons), or says why not.</summary>
    public void BeginRing(RingKind kind)
    {
        if (!open)
        {
            return;
        }

        string why = WhyNoRing(kind);
        if (why != null)
        {
            GameMessages.Post(why, MessageKind.Warning);
            return;
        }

        ringPlacing = true;
        ringKind = kind;
        postPlacing = false;
        ringRadius = WorkerSettings.Current.RingRadius;
        string what = kind == RingKind.Quarry ? "the masons should break stone" : "the woodcutters should fell";
        GameMessages.Post(InputDevices.UsingGamepad
            ? $"{GameInput.KeyLabel(GameInput.KeeperSelect)} lays the ring where {what}; zooming makes it larger or smaller. " +
              $"{GameInput.KeyLabel(GameInput.Back)} stops."
            : $"Click where {what}; the wheel makes the ring larger or smaller. Right-click to stop.");
    }

    /// <summary>Takes every ring away (the woodcutters go back to their own work).</summary>
    public void ClearRings()
    {
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        var all = new System.Collections.Generic.List<RingOrder>(GameSession.Home.Rings);
        foreach (RingOrder ring in all)
        {
            GameSession.Home.RemoveRing(ring);
        }
    }

    /// <summary>Why no felling ring can be drawn now, or null.</summary>
    public static string WhyNoRing() => WhyNoRing(RingKind.Fell);

    /// <summary>Why no ring of <paramref name="kind"/> can be drawn now, or null.</summary>
    public static string WhyNoRing(RingKind kind)
    {
        if (HavenVisit.IsVisiting)
        {
            return "Rings are drawn in your own Haven.";
        }

        bool worker = false;
        foreach (ResidentRecord record in GameSession.Home.Residents)
        {
            worker |= RingWork.Works(record, kind, WorkerSettings.Current);
        }

        if (!worker)
        {
            return kind == RingKind.Quarry
                ? "Nobody here breaks stone yet: a mason's work."
                : "Nobody here fells trees yet: a woodcutter's work at the Woodcutter's Lodge.";
        }

        return RingsOf(kind) >= WorkerSettings.Current.MostRings ? $"At most {WorkerSettings.Current.MostRings} rings of a kind at once." : null;
    }

    /// <summary>How many rings of <paramref name="kind"/> stand in the Haven one is in.</summary>
    public static int RingsOf(RingKind kind)
    {
        int count = 0;
        foreach (RingOrder ring in GameSession.Here.Rings)
        {
            count += ring.Kind == kind ? 1 : 0;
        }

        return count;
    }

    private bool RingSpot(out Vector3 at)
    {
        at = default;
        return TryPointer(out Ray ray) && TryGround(ray, out at);
    }

    private void LayRing(Vector3 at)
    {
        ringPlacing = false;
        var ring = new RingOrder(at, ringRadius, ringKind);
        // Rings of a kind never share ground: a tree or rock in two would be worked twice over while away.
        foreach (RingOrder other in GameSession.Home.Rings)
        {
            if (other.Kind == ring.Kind && other.Overlaps(ring))
            {
                GameMessages.Post("That ring would cross another of its kind: draw it beside it, or clear the rings first.", MessageKind.Warning);
                return;
            }
        }

        int found = RingWork.Standing(ring);
        // An outcrop broken to the ground still makes a quarry: it grows back.
        if (found == 0 && !(ring.Kind == RingKind.Quarry && RingWork.Regrows(ring)))
        {
            GameMessages.Post(ring.Kind == RingKind.Quarry
                ? $"No rock a mason can break stands in that ring (boulders and stone, a Tier {Tiers.ToRoman(WorkerSettings.Current.QuarryPickTier)} pickaxe's)."
                : "No grown trees stand in that ring.", MessageKind.Warning);
            return;
        }

        ring.StandingSeen = found;
        GameSession.Home.AddRing(ring);
        GameMessages.Post(ring.Kind == RingKind.Quarry
            ? $"A quarry ring of {found} rock{(found == 1 ? "" : "s")} for the masons: in working hours, when their bench has nothing to work on, " +
              (RingWork.Regrows(ring) ? "they break it and take the stone to the chests, and come back as the stone grows back." : "they break it and take the stone to the chests.")
            : $"A ring of {found} tree{(found == 1 ? "" : "s")} for the woodcutters: in working hours, when their bench has nothing " +
              "to work on, they fell it, take the logs to the chests and plant saplings where there is room.", MessageKind.Info);
    }

    private void DrawRings(bool hidden)
    {
        if (ringLines == null)
        {
            ringLines = new KeeperRings(transform);
            postLines = new KeeperRings(transform, UIStyle.Current.Danger, "Keeper's View Posts");
            laneLines = new KeeperRings(transform, UIStyle.Current.Warning, "Keeper's View Lanes");
        }

        Vector3 ghost = default;
        bool ghostShown = (ringPlacing || postPlacing) && !hidden && RingSpot(out ghost);
        ringLines.Show(GameSession.Here.Rings, ghost, ghostShown && ringPlacing ? ringRadius : 0f, ringKind);
        postLines.ShowPoints(GameSession.Here.Posts, PostMarkRadius, ghost, ghostShown && postPlacing);
        // A ward breach coming or under way: the roads its creatures will take (WardBreachDirector's lanes), so posts can be set on them.
        laneRoads.Clear();
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach != null && (breach.IsPending || breach.IsActive))
        {
            foreach (BreachLane lane in breach.Lanes)
            {
                laneRoads.Add(lane.Road);
            }
        }

        laneLines.ShowRoads(laneRoads);
    }

    // ----- Guard posts (GuardPosts) -----

    /// <summary>Starts setting a guard post, or says why not (nobody keeps guard, as many posts as the defences hold, a friend's Haven).</summary>
    public void BeginPost()
    {
        if (!open)
        {
            return;
        }

        string why = WhyNoPost();
        if (why != null)
        {
            GameMessages.Post(why, MessageKind.Warning);
            return;
        }

        postPlacing = true;
        ringPlacing = false;
        GameMessages.Post(InputDevices.UsingGamepad
            ? $"{GameInput.KeyLabel(GameInput.KeeperSelect)} sets where a guard stands when the ward breaks or a corruption night comes. " +
              $"{GameInput.KeyLabel(GameInput.Back)} stops."
            : "Click where a guard should stand when the ward breaks or a corruption night comes. Right-click to stop.");
    }

    /// <summary>Takes every post away (the guards stand where they did before).</summary>
    public void ClearPosts()
    {
        if (!HavenVisit.IsVisiting)
        {
            GameSession.Home.ClearPosts();
        }
    }

    /// <summary>The horn (HavenHorn): sounds it, or stands everyone down while it holds.</summary>
    public void SoundHorn()
    {
        if (HavenHorn.IsSounding)
        {
            HavenHorn.StandDown();
        }
        else
        {
            HavenHorn.Sound();
        }
    }

    /// <summary>Why no post can be set now, or null.</summary>
    public static string WhyNoPost() => GuardPosts.WhyNoPost(GameSession.Here, DefenceSettings.Current, HavenVisit.IsVisiting);

    private void LayPost(Vector3 at)
    {
        postPlacing = false;
        if (HomePortal.Current == null || !HomePortal.Current.InBuildLand(at))
        {
            GameMessages.Post("Posts go inside the Haven's lands.", MessageKind.Warning);
            return;
        }

        // On ground a guard can walk to, out of deep water (the nearest within a couple of metres).
        if (!UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit ground, 2f, DeepWater.MaskFor(UnityEngine.AI.NavMesh.AllAreas, false)))
        {
            GameMessages.Post("A guard can't stand there.", MessageKind.Warning);
            return;
        }

        GameSession.Home.AddPost(ground.position);
        int posts = GameSession.Home.Posts.Count;
        string guard = GuardPosts.GuardFor(GameSession.Home, posts - 1);
        GameMessages.Post($"Guard post {posts} set: when the ward breaks, {guard ?? $"the {Ordinal(posts)} guard"} stands here; on a corruption " +
            "night, the posts on its side are kept.", MessageKind.Info);
    }

    private static string Ordinal(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => $"{n}th" };

    // ----- Picking -----

    private void Point(bool blocked)
    {
        Camera camera = Camera.main;
        bool pad = InputDevices.UsingGamepad;
        if (blocked || camera == null)
        {
            picker.ClearHover();
        }
        else if (pad)
        {
            Ray middle = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            picker.Pick(middle, Settings.PickRange, focus, Settings.PadSnapRadius);
        }
        else if (TryPointer(out Ray ray))
        {
            picker.Pick(ray, Settings.PickRange, null, 0f);
        }

        picker.ShowOutlines(outlineMaterial, maskMaterial);
        Vector2 screen = pad && camera != null ? (Vector2)camera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f)) : GameInput.PointerPosition;
        if (hud != null)
        {
            hud.Point(picker.Hovered, screen, pad && !blocked);
            hud.ShowMarks(camera, !blocked);
        }
    }

    // A person: their page. A station: its window, to queue and watch. A scaffold: how far it has come. Any other building: its
    // upgrades and fittings.
    private void Pick(Component thing)
    {
        picker.Picked = thing;
        if (thing == null || player == null)
        {
            return;
        }

        // The portal: raids, the ward and its tier, planned from here.
        if (thing is HomePortal portal)
        {
            portal.OpenFromAfar(player.gameObject);
            return;
        }

        if (thing is ResidentNpc person)
        {
            if (person.Resident != null)
            {
                ResidentWindow.Show(person.Resident);
            }

            return;
        }

        if (thing is BuildingObject building)
        {
            if (building.TryGetComponent(out Construction construction) && construction.IsPlan)
            {
                GameMessages.Post($"The {KeeperHud.NameOf(building)} is a plan: the builders start once the chests hold its materials.");
                return;
            }

            if (construction != null && construction.IsBuilding)
            {
                GameMessages.Post($"The {KeeperHud.NameOf(building)}: {Mathf.RoundToInt(construction.Share * 100f)}% built. " +
                    "Hammer it yourself, or the builders will see to it.");
                return;
            }

            if (building.TryGetComponent(out CraftingStation station) && station.Usable && station.OpenFromAfar(player.gameObject))
            {
                return;
            }

            BuildingUpgradeWindow.Show(building, player.gameObject);
        }
    }

    // ----- The window keys, kept while the controls are held: the input handler is off, so they are answered here -----

    private void RouteWindowKeys(bool on)
    {
        foreach (string path in PlayerControl.WindowKeyPaths)
        {
            InputAction action = InputSystem.actions != null ? InputSystem.actions.FindAction(path) : null;
            if (action == null)
            {
                continue;
            }

            action.performed -= OnWindowKey;
            action.canceled -= OnWindowKeyUp;
            if (on)
            {
                action.performed += OnWindowKey;
                action.canceled += OnWindowKeyUp;
            }
        }
    }

    // The controller's windows wheel closes, choosing, when its button is let go.
    private void OnWindowKeyUp(InputAction.CallbackContext context)
    {
        if (open && player != null && context.action == GameInput.WindowsWheel && player.TryGetComponent(out CommandWheel wheel) && wheel.IsOpen)
        {
            wheel.CloseAndExecute();
        }
    }

    private void OnWindowKey(InputAction.CallbackContext context)
    {
        if (!open || player == null)
        {
            return;
        }

        WindowKeys.Press(context.action, player.gameObject);
    }

    // ----- The ground -----

    private bool TryPointer(out Ray ray)
    {
        Camera camera = Camera.main;
        if (camera == null || Mouse.current == null)
        {
            ray = default;
            return false;
        }

        ray = camera.ScreenPointToRay(GameInput.PointerPosition);
        return true;
    }

    private static bool TryGround(Ray ray, out Vector3 point)
    {
        foreach (Terrain terrain in Terrains.Active)
        {
            if (terrain != null && terrain.TryGetComponent(out TerrainCollider collider) && collider.Raycast(ray, out RaycastHit hit, 1000f))
            {
                point = hit.point;
                return true;
            }
        }

        var plane = new Plane(Vector3.up, Current != null ? Current.focus : Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = default;
        return false;
    }

    /// <summary>The point on the terrain under <paramref name="point"/> (its height kept where there is none).</summary>
    public static Vector3 Ground(Vector3 point)
    {
        Terrain terrain = Terrains.Under(point);
        if (terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point;
    }
}

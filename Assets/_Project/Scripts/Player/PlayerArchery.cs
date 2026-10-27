using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What happened when an arrow was loosed, for animation, sound and the HUD.</summary>
public readonly struct ArrowShot
{
    public readonly ItemDefinition Arrow;
    /// <summary>Share of a full draw (0-1).</summary>
    public readonly float Draw;
    public readonly Vector3 Origin;
    public readonly Vector3 Velocity;
    public readonly float ReloadSeconds;

    public ArrowShot(ItemDefinition arrow, float draw, Vector3 origin, Vector3 velocity, float reloadSeconds)
    {
        Arrow = arrow;
        Draw = draw;
        Origin = origin;
        Velocity = velocity;
        ReloadSeconds = reloadSeconds;
    }
}

/// <summary>
/// Shooting a bow. While the equipped main-hand weapon is a bow (WeaponDefinition › Kind Bow), the attack button draws
/// instead of swinging: hold to draw (the bow's Draw Seconds to full), let go to loose the first arrow stack in the bag,
/// then the next arrow is nocked in Reload Seconds. Holding through the reload draws again at once. The player keeps
/// moving while drawing, slowed to the bow's Draw Move Speed and facing the aim (drawing enters GameState.Combat, which
/// strafes); a dodge, a staggering hit, eating or lowering the bow puts the arrow back.
/// <para>
/// Aim: the hostile monster nearest the cursor or crosshair (within <see cref="aimAssistAngle"/> with the action camera,
/// or <see cref="aimAssistRadius"/> of the cursor's spot top-down) that the bow can see is the <see cref="Target"/>,
/// outlined in red (ArcheryTargetOutline); arrows are loosed on the arc that lands on it. Without one they fly to the
/// cursor's spot at chest height, or to what the crosshair points at. Partial draws are slower, weaker and less accurate
/// (ArcheryRules); the arrow sets the damage, the bow's shot attack its type, scaling and poise.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine), typeof(PlayerMovement), typeof(Combatant))]
public sealed class PlayerArchery : MonoBehaviour
{
    public enum Phase
    {
        /// <summary>No bow in hand.</summary>
        None,
        Ready,
        Drawing,
        Reloading,
    }

    // Seconds between "No arrows" messages while the button is held.
    private const float NoArrowsMessageSeconds = 2f;
    private const float ShotNoise = 3f;

    [SerializeField] private Camera viewCamera;
    [Tooltip("Where arrows leave from: this high above the player's feet...")]
    [SerializeField, Min(0f)] private float bowHeight = 1.45f;
    [Tooltip("...and this far in front.")]
    [SerializeField, Min(0f)] private float bowForward = 0.35f;
    [Tooltip("How far the bow looks for a target, in metres.")]
    [SerializeField, Min(1f)] private float aimRange = 45f;
    [Tooltip("With the action camera: a monster within this many degrees of the crosshair is the target.")]
    [SerializeField, Range(0f, 20f)] private float aimAssistAngle = 5f;
    [Tooltip("Top-down: a monster within this many metres of the cursor's spot is the target.")]
    [SerializeField, Min(0f)] private float aimAssistRadius = 1.5f;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Dictionary<MonsterAI, Collider> bodies = new Dictionary<MonsterAI, Collider>();
    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Combatant combatant;
    private PlayerCombat combat;
    private PlayerDodge dodge;
    private PlayerEating eating;
    private PlayerGathering gathering;
    private PlayerGuard guard;
    private PlayerStamina stamina;
    private ItemInventory items;
    private Damageable health;

    private bool held;
    private float drawStartedAt;
    private float reloadEndsAt;
    private float noArrowsMessageAt = float.NegativeInfinity;

    public Phase State { get; private set; }

    /// <summary>The bow in hand, or null.</summary>
    public WeaponDefinition Bow => combat != null && combat.Weapon != null && combat.Weapon.IsBow ? combat.Weapon : null;

    public bool IsDrawing => State == Phase.Drawing;

    /// <summary>Share of a full draw right now (0 when not drawing).</summary>
    public float Draw => IsDrawing && Bow != null ? ArcheryRules.DrawShare(Time.time - drawStartedAt, Bow.DrawSeconds) : 0f;

    /// <summary>The monster an arrow loosed now would fly at; null when there is none in sight.</summary>
    public Damageable Target { get; private set; }

    /// <summary>Where an arrow loosed now would fly to.</summary>
    public Vector3 AimPoint { get; private set; }

    /// <summary>Where arrows leave from.</summary>
    public Vector3 Origin => transform.position + Vector3.up * bowHeight + transform.forward * bowForward;

    /// <summary>The arrows a shot would use: the first arrow stack in the bag, or null.</summary>
    public ItemDefinition NextArrow => FindArrow();

    /// <summary>Raised when drawing starts, with the seconds to full draw.</summary>
    public event Action<float> DrawStarted;

    /// <summary>Raised when a draw ends without a shot (let go too early, dodged, staggered, bow lowered).</summary>
    public event Action DrawCancelled;

    public event Action<ArrowShot> Shot;

    /// <summary>Raised when a bow is taken up (true) or put away (false).</summary>
    public event Action<bool> BowChanged;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        combatant = GetComponent<Combatant>();
        combat = GetComponent<PlayerCombat>();
        dodge = GetComponent<PlayerDodge>();
        eating = GetComponent<PlayerEating>();
        gathering = GetComponent<PlayerGathering>();
        guard = GetComponent<PlayerGuard>();
        stamina = GetComponent<PlayerStamina>();
        items = GetComponent<ItemInventory>();
        health = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        stateMachine.AttackRequested += OnAttackPressed;
        stateMachine.AttackReleased += OnAttackReleased;
        if (combat != null)
        {
            combat.WeaponChanged += OnWeaponChanged;
            combat.Flinched += OnFlinched;
        }

        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        if (eating != null)
        {
            eating.Started += OnEatingStarted;
        }

        State = Bow != null ? Phase.Ready : Phase.None;
    }

    private void OnDisable()
    {
        stateMachine.AttackRequested -= OnAttackPressed;
        stateMachine.AttackReleased -= OnAttackReleased;
        if (combat != null)
        {
            combat.WeaponChanged -= OnWeaponChanged;
            combat.Flinched -= OnFlinched;
        }

        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (eating != null)
        {
            eating.Started -= OnEatingStarted;
        }

        held = false;
        CancelDraw();
        Target = null;
    }

    private void Update()
    {
        WeaponDefinition bow = Bow;
        if (bow == null)
        {
            Target = null;
            return;
        }

        UpdateAim();
        if (State == Phase.Reloading && Time.time >= reloadEndsAt)
        {
            State = Phase.Ready;
        }

        if (State == Phase.Ready && held)
        {
            TryStartDraw(bow);
        }
        else if (State == Phase.Drawing)
        {
            if (!CanDraw())
            {
                CancelDraw();
                return;
            }

            combat?.KeepInCombat();
        }
    }

    // ----- Input -----

    private void OnAttackPressed()
    {
        if (Bow == null)
        {
            return;
        }

        held = true;
        if (State == Phase.Ready)
        {
            TryStartDraw(Bow);
        }
    }

    private void OnAttackReleased()
    {
        held = false;
        if (State != Phase.Drawing)
        {
            return;
        }

        WeaponDefinition bow = Bow;
        if (bow == null || Draw < bow.MinimumDraw)
        {
            CancelDraw();
            return;
        }

        Loose(bow);
    }

    // ----- Drawing and shooting -----

    private bool CanDraw()
    {
        return stateMachine.CanMove && (health == null || health.IsAlive) && (dodge == null || !dodge.IsDodging)
               && (eating == null || !eating.IsEating) && (gathering == null || gathering.Session == null)
               && (combat == null || !combat.IsFlinching) && (guard == null || !guard.IsStunned && !guard.IsGuarding);
    }

    private void TryStartDraw(WeaponDefinition bow)
    {
        if (!CanDraw())
        {
            return;
        }

        if (FindArrow() == null)
        {
            held = false;
            if (Time.time >= noArrowsMessageAt)
            {
                noArrowsMessageAt = Time.time + NoArrowsMessageSeconds;
                GameMessages.Post("No arrows", MessageKind.Warning);
            }

            return;
        }

        float cost = bow.ShotAttack != null ? Mathf.Max(0f, bow.ShotAttack.StaminaCost) : 0f;
        if (stamina != null && cost > 0f && !stamina.TrySpend(cost))
        {
            return;
        }

        State = Phase.Drawing;
        drawStartedAt = Time.time;
        movement.SetSpeedScale(this, bow.DrawMoveSpeed);
        combat?.KeepInCombat();
        AudioManager.Play(bow.DrawSound, Origin);
        DrawStarted?.Invoke(bow.DrawSeconds);
    }

    private void CancelDraw()
    {
        if (State != Phase.Drawing)
        {
            return;
        }

        State = Bow != null ? Phase.Ready : Phase.None;
        movement.SetSpeedScale(this, 1f);
        DrawCancelled?.Invoke();
    }

    private void Loose(WeaponDefinition bow)
    {
        ItemDefinition arrowItem = FindArrow();
        if (arrowItem == null || arrowItem.Arrow.Projectile == null || items.Container.Remove(arrowItem, 1) < 1)
        {
            CancelDraw();
            return;
        }

        float heldFor = Time.time - drawStartedAt;
        float draw = ArcheryRules.DrawShare(heldFor, bow.DrawSeconds);
        ArrowDefinition arrow = arrowItem.Arrow;
        Vector3 origin = Origin;
        float speed = bow.ArrowSpeed * arrow.SpeedMultiplier * ArcheryRules.SpeedShare(draw);
        float gravity = Physics.gravity.magnitude * arrow.GravityScale;
        Vector3 direction = ArcheryRules.LaunchDirection(origin, AimPoint, speed, gravity);
        float spread = ArcheryRules.SpreadDegrees(bow.Spread, draw, heldFor, bow.DrawSeconds);
        direction = ArcheryRules.Stray(direction, spread, UnityEngine.Random.value, UnityEngine.Random.value);

        ArrowProjectile projectile = PrefabPool.Spawn(arrow.Projectile, origin, Quaternion.LookRotation(direction));
        projectile.Launch(combatant, bow.ShotAttack, arrow, arrow.Damage * ArcheryRules.DamageShare(draw), direction * speed);

        movement.SnapFacing(direction);
        movement.SetSpeedScale(this, 1f);
        AudioManager.Play(bow.SwingSound, origin);
        WorldNoise.Emit(transform.position, ShotNoise);
        combat?.KeepInCombat();

        State = Phase.Reloading;
        reloadEndsAt = Time.time + bow.ReloadSeconds;
        Shot?.Invoke(new ArrowShot(arrowItem, draw, origin, direction * speed, bow.ReloadSeconds));
    }

    private ItemDefinition FindArrow()
    {
        ItemContainer bag = items != null ? items.Container : null;
        if (bag == null)
        {
            return null;
        }

        for (int i = 0; i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.IsArrow)
            {
                return stack.Item;
            }
        }

        return null;
    }

    // ----- Aim -----

    private void UpdateAim()
    {
        Vector3 origin = Origin;
        viewCamera = viewCamera != null && viewCamera.isActiveAndEnabled ? viewCamera : Camera.main;
        if (!CameraPointer.TryGetRay(viewCamera, out Ray ray))
        {
            Target = null;
            AimPoint = origin + movement.AimDirection * aimRange;
            return;
        }

        bool crosshair = CameraPointer.UsesScreenCenter;
        float reach = Vector3.Distance(ray.origin, transform.position) + aimRange;
        bool pointed = Nearest(ray, reach, out RaycastHit hit);
        Vector3 spot = pointed ? hit.point : ray.GetPoint(reach);

        Damageable best = pointed ? HostileAt(hit.collider) : null;
        Vector3 bestPoint = pointed ? hit.point : spot;
        if (best != null && !InSight(origin, bestPoint, best))
        {
            best = null;
        }

        if (best == null)
        {
            float bestScore = float.MaxValue;
            var monsters = MonsterAI.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterAI monster = monsters[i];
                if (monster == null || !monster.TryGetComponent(out Damageable body) || !IsHostile(body))
                {
                    continue;
                }

                Vector3 centre = Centre(monster);
                if ((centre - origin).sqrMagnitude > aimRange * aimRange)
                {
                    continue;
                }

                float score;
                if (crosshair)
                {
                    score = Vector3.Angle(ray.direction, centre - ray.origin);
                    if (score > aimAssistAngle)
                    {
                        continue;
                    }
                }
                else
                {
                    Vector3 offset = centre - spot;
                    offset.y = 0f;
                    score = offset.magnitude;
                    if (score > aimAssistRadius)
                    {
                        continue;
                    }
                }

                if (score < bestScore && InSight(origin, centre, body))
                {
                    bestScore = score;
                    best = body;
                    bestPoint = centre;
                }
            }
        }

        Target = best;
        if (best != null)
        {
            AimPoint = bestPoint;
        }
        else
        {
            // Top-down, the cursor's spot is on the ground: shoot level at chest height toward it instead of into the dirt.
            AimPoint = crosshair ? spot : spot + Vector3.up * (origin.y - transform.position.y);
        }
    }

    private bool Nearest(Ray ray, float reach, out RaycastHit nearest)
    {
        nearest = default;
        int count = Physics.RaycastNonAlloc(ray, hits, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= closest)
            {
                continue;
            }

            closest = hits[i].distance;
            nearest = hits[i];
            found = true;
        }

        return found;
    }

    // Clear line from the bow to the point. The player, the target and other creatures (the arrow would strike them
    // instead) don't block it.
    private bool InSight(Vector3 from, Vector3 to, Damageable target)
    {
        Vector3 line = to - from;
        float length = line.magnitude;
        if (length < 0.01f)
        {
            return true;
        }

        int count = Physics.RaycastNonAlloc(from, line / length, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform blocker = hits[i].collider.transform;
            if (blocker.IsChildOf(transform) || blocker.IsChildOf(target.transform))
            {
                continue;
            }

            Damageable other = hits[i].collider.GetComponentInParent<Damageable>();
            if (other == null || other.Category != TargetCategory.Creature)
            {
                return false;
            }
        }

        return true;
    }

    private Damageable HostileAt(Collider collider)
    {
        Damageable body = collider.GetComponentInParent<Damageable>();
        return body != null && IsHostile(body) ? body : null;
    }

    private bool IsHostile(Damageable body) =>
        body.IsAlive && body.Category == TargetCategory.Creature && combatant.Faction.WillAttack(body.Faction);

    // The middle of a monster's body collider (cached), so arrows fly at the chest rather than the feet.
    private Vector3 Centre(MonsterAI monster)
    {
        if (!bodies.TryGetValue(monster, out Collider body) || body == null)
        {
            if (bodies.Count > 128)
            {
                bodies.Clear(); // forget monsters that are gone
            }

            body = monster.GetComponentInChildren<Collider>();
            bodies[monster] = body;
        }

        return body != null && body.enabled ? body.bounds.center : monster.transform.position + Vector3.up;
    }

    // ----- Reactions -----

    private void OnWeaponChanged(WeaponDefinition weapon)
    {
        bool bow = weapon != null && weapon.IsBow;
        bool had = State != Phase.None;
        CancelDraw();
        State = bow ? Phase.Ready : Phase.None;
        if (bow != had)
        {
            BowChanged?.Invoke(bow);
        }
    }

    private void OnDodged(Vector3 direction) => CancelDraw();

    private void OnFlinched(float seconds, bool lostSwing) => CancelDraw();

    private void OnEatingStarted(ItemDefinition meal) => CancelDraw();
}

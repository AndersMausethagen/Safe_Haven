using UnityEngine;

/// <summary>
/// The enemy the player is fighting. While in combat with a melee weapon the player faces it, swings and steps at it, and a ring
/// marks it on the ground, without any lock-on key: the aim picks it (rules in <see cref="TargetRules"/>), it is kept until another
/// is clearly nearer the aim, and while the aim points well away from it the player looks and swings where the aim points. A bow
/// has its own target (PlayerArchery.Target). Numbers: Combat Technique › Focus on an enemy.
/// <para>
/// Drawn with Graphics.RenderMesh for the reason given in SlashEffect. It only asks PlayerMovement to face a direction and
/// PlayerCombat reads it, so nothing else knows it exists.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerFocusTarget : MonoBehaviour
{
    private const float RefreshSeconds = 0.2f;
    private const float MarkerLift = 0.07f;
    private const float MarkerMargin = 0.45f;
    private static readonly int TintId = Shader.PropertyToID("_TintColor");
    private static Mesh quad;

    private PlayerMovement movement;
    private PlayerCombat combat;
    private Combatant combatant;
    private Damageable self;
    private GameStateManager gameState;
    private Damageable target;
    private float nextRefresh;
    private float markerRadius;
    private MaterialPropertyBlock block;

    /// <summary>The enemy in focus, or null. The player may be aiming away from it (see <see cref="HasFocus"/>).</summary>
    public Damageable Target => target;

    /// <summary>True while the player faces and acts toward <see cref="Target"/>.</summary>
    public bool HasFocus { get; private set; }

    /// <summary>The horizontal direction to the target.</summary>
    public Vector3 DirectionToTarget
    {
        get
        {
            if (target == null)
            {
                return transform.forward;
            }

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
        }
    }

    private static CombatTechniqueSettings Rules => CombatTechniqueSettings.Current;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        combatant = GetComponent<Combatant>();
        self = GetComponent<Damageable>();
        block = new MaterialPropertyBlock();
    }

    private void OnDisable() => Clear();

    private void Update()
    {
        gameState = gameState != null ? gameState : GameStateManager.Current;
        bool inCombat = gameState != null && gameState.CurrentState == GameState.Combat;
        if (!inCombat || !CanFocus)
        {
            Clear();
            return;
        }

        if (target == null || !target.IsAlive || Time.time >= nextRefresh)
        {
            Refresh();
        }
        else
        {
            ApplyFacing();
        }
    }

    private bool CanFocus => GameOptions.FocusTarget.On && (self == null || self.IsAlive)
        && (combat == null || combat.Weapon == null || !combat.Weapon.TakesAttackInput);

    /// <summary>Looks again at who is in reach. PlayerCombat asks as a swing starts, so the first blow goes where it should.</summary>
    public void Refresh()
    {
        nextRefresh = Time.time + RefreshSeconds;
        if (!CanFocus)
        {
            Clear();
            return;
        }

        Vector3 aim = movement.RawAimDirection;
        float keep = Rules.FocusKeepRange;
        float currentScore = float.MaxValue;
        if (target != null && target.IsAlive && Measure(target, aim, out float currentAngle, out float currentDistance) && currentDistance <= keep)
        {
            currentScore = TargetRules.Score(currentAngle, currentDistance, keep);
        }
        else
        {
            target = null;
        }

        Damageable best = null;
        float bestScore = float.MaxValue;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster == null || !monster.TryGetComponent(out Damageable health) || health == target || !health.IsAlive
                || health.Category != TargetCategory.Creature || (combatant != null && health.Faction.AreAllies(combatant.Faction))
                || !Measure(health, aim, out float angle, out float distance)
                || !TargetRules.CanAcquire(angle, distance, Rules.FocusAcquireCone, Rules.FocusAcquireRange))
            {
                continue;
            }

            float score = TargetRules.Score(angle, distance, keep);
            if (score < bestScore)
            {
                bestScore = score;
                best = health;
            }
        }

        if (best != null && (target == null || TargetRules.ShouldSwitch(currentScore, bestScore, Rules.FocusSwitchMargin)))
        {
            SetTarget(best);
        }

        ApplyFacing();
    }

    private bool Measure(Damageable candidate, Vector3 aim, out float angle, out float distance)
    {
        Vector3 to = candidate.transform.position - transform.position;
        to.y = 0f;
        distance = to.magnitude;
        angle = distance > 0.05f ? Vector3.Angle(aim, to) : 0f;
        return true;
    }

    private void SetTarget(Damageable next)
    {
        target = next;
        markerRadius = 0.5f;
        if (next.TryGetComponent(out Collider body))
        {
            Vector3 size = body.bounds.extents;
            markerRadius = Mathf.Max(size.x, size.z);
        }
    }

    private void ApplyFacing()
    {
        HasFocus = target != null && !TargetRules.IsAimingAway(Vector3.Angle(movement.RawAimDirection, DirectionToTarget), Rules.FocusFreeAimAngle);
        if (HasFocus)
        {
            movement.SetFocusDirection(DirectionToTarget);
        }
        else
        {
            movement.ClearFocusDirection();
        }
    }

    private void Clear()
    {
        target = null;
        HasFocus = false;
        if (movement != null)
        {
            movement.ClearFocusDirection();
        }
    }

    private void LateUpdate()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (target == null || feel.FocusMarkerMaterial == null || GamePause.IsPaused)
        {
            return;
        }

        // Committed to a strike, it can't turn: show where to get to.
        if (HasFocus && target.TryGetComponent(out MonsterAI monster) && monster.IsOpenToBackstab)
        {
            BackstabMarker.Show(monster);
        }

        Color color = feel.FocusMarkerColor;
        color.a *= HasFocus ? 1f : 0.35f;
        block.SetColor(TintId, color);
        float size = (markerRadius + MarkerMargin) * 2f;
        Vector3 at = target.transform.position + Vector3.up * MarkerLift;
        var draw = new RenderParams(feel.FocusMarkerMaterial)
        {
            matProps = block,
            layer = gameObject.layer,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(at, Vector3.one * (size + 1f)),
        };
        Graphics.RenderMesh(draw, Quad, 0, Matrix4x4.TRS(at, Quaternion.identity, Vector3.one * size));
    }

    /// <summary>A flat unit square on the ground, facing up. Shared with the backstab marker.</summary>
    public static Mesh Quad
    {
        get
        {
            if (quad == null)
            {
                quad = new Mesh
                {
                    name = "Ground Marker",
                    hideFlags = HideFlags.DontSave,
                    vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
                    uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                quad.RecalculateBounds();
            }

            return quad;
        }
    }
}

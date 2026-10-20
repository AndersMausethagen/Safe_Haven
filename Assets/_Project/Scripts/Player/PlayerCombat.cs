using System;
using UnityEngine;

/// <summary>
/// Runs the player's attacks. A request starts an attack when the previous one has recovered (one request is buffered
/// until then), sends the animation on its way, and deals the damage at the moment the swing connects, using the equipped
/// weapon and the player's stats. It also keeps the global mode in step: attacking enters GameState.Combat, and a stretch
/// without fighting returns to Exploration.
/// Animation observes AttackPerformed and ShieldBashPerformed; damage numbers, audio and loot listen to the targets and to
/// the Combatant, so none of them are known here.
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Combatant))]
public sealed class PlayerCombat : MonoBehaviour
{
    // Recovery for an attack that has no definition, so unconfigured attacks still animate and can't be spammed.
    private const float FallbackRecoveryTime = 0.6f;

    [Header("Mode")]
    [Tooltip("Seconds in Combat without fighting before returning to Exploration. 0 stays in Combat.")]
    [SerializeField, Min(0f)] private float combatTimeout = 5f;

    [Header("Equipment")]
    [Tooltip("Sets the animation stance, the damage and the left mouse button attack. Empty means no damage.")]
    [SerializeField] private WeaponDefinition weapon;
    [Tooltip("Shield bash only works while a shield is equipped.")]
    [SerializeField] private bool hasShield;
    [Tooltip("The attack the shield bash performs.")]
    [SerializeField] private AttackDefinition shieldBash;

    [Header("Hits")]
    [Tooltip("Layers that can be hit. Targets still have to be damageable and hostile.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    private enum AttackKind
    {
        None,
        Basic,
        ShieldBash,
    }

    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Combatant combatant;
    private PlayerDodge dodge;
    private PlayerStamina stamina;
    private GameStateManager gameState;
    private float lastActivityTime;
    private WeaponDefinition announcedWeapon;

    private AttackKind activeKind;
    private AttackKind queuedKind;
    private AttackDefinition activeAttack;
    private float activeWeaponDamage;
    private float activeStartTime;
    private bool hitPending;
    private Vector3 activeDirection;

    public WeaponDefinition Weapon
    {
        get => weapon;
        set
        {
            weapon = value;
            AnnounceWeapon();
        }
    }

    public bool HasShield
    {
        get => hasShield;
        set => hasShield = value;
    }

    /// <summary>Raised when a weapon attack starts. Animation plays here.</summary>
    public event Action AttackPerformed;

    /// <summary>Raised when a shield bash starts.</summary>
    public event Action ShieldBashPerformed;

    /// <summary>Raised when the equipped weapon changes, including being cleared.</summary>
    public event Action<WeaponDefinition> WeaponChanged;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        combatant = GetComponent<Combatant>();
        dodge = GetComponent<PlayerDodge>();
        stamina = GetComponent<PlayerStamina>();
        announcedWeapon = weapon;
    }

    private void OnEnable()
    {
        stateMachine.AttackRequested += OnAttackRequested;
        stateMachine.ShieldBashRequested += OnShieldBashRequested;
        combatant.DamageDealt += OnDamageDealt;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        gameState = GameStateManager.Instance;
        if (gameState == null)
        {
            Debug.LogWarning("PlayerCombat found no GameStateManager; attacking won't change the game mode.", this);
            return;
        }

        gameState.GameStateChanged += OnGameStateChanged;
        lastActivityTime = Time.time;
    }

    private void OnDisable()
    {
        stateMachine.AttackRequested -= OnAttackRequested;
        stateMachine.ShieldBashRequested -= OnShieldBashRequested;
        combatant.DamageDealt -= OnDamageDealt;
        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (gameState != null)
        {
            gameState.GameStateChanged -= OnGameStateChanged;
            gameState = null;
        }

        activeKind = AttackKind.None;
        queuedKind = AttackKind.None;
        activeAttack = null;
        hitPending = false;
    }

    private void OnValidate()
    {
        // Lets the weapon be swapped in the Inspector while playing.
        if (Application.isPlaying && announcedWeapon != weapon)
        {
            AnnounceWeapon();
        }
    }

    private void Update()
    {
        UpdateAttack();

        if (gameState != null
            && combatTimeout > 0f
            && gameState.CurrentState == GameState.Combat
            && Time.time - lastActivityTime >= combatTimeout)
        {
            gameState.TrySetState(GameState.Exploration);
        }
    }

    private void OnAttackRequested()
    {
        RequestAttack(AttackKind.Basic);
    }

    private void OnShieldBashRequested()
    {
        if (hasShield)
        {
            RequestAttack(AttackKind.ShieldBash);
        }
    }

    private void RequestAttack(AttackKind kind)
    {
        if (activeKind != AttackKind.None || (dodge != null && dodge.IsDodging))
        {
            // Buffered: it starts as soon as the current attack has recovered (when the animation can chain) or the
            // dodge has ended.
            queuedKind = kind;
            return;
        }

        BeginAttack(kind);
    }

    // Dodging is the way out of trouble, so it cuts a swing short; a swing that hadn't landed yet deals no damage.
    private void OnDodged(Vector3 direction)
    {
        activeKind = AttackKind.None;
        queuedKind = AttackKind.None;
        activeAttack = null;
        hitPending = false;
    }

    private void BeginAttack(AttackKind kind)
    {
        queuedKind = AttackKind.None;

        // Pay the full cost when the swing actually starts, rechecking affordability for buffered attacks.
        if (stamina != null && !stamina.TrySpend(kind == AttackKind.Basic ? stamina.AttackCost : stamina.ShieldBashCost))
        {
            return;
        }

        activeKind = kind;

        if (kind == AttackKind.Basic)
        {
            activeAttack = weapon != null ? weapon.BasicAttack : null;
            activeWeaponDamage = weapon != null ? weapon.Damage : 0f;
        }
        else
        {
            activeAttack = shieldBash;
            activeWeaponDamage = 0f;
        }

        activeStartTime = Time.time;
        hitPending = activeAttack != null;
        // The direction is locked in when the attack starts, like V Rising: toward the cursor, as the swing begins.
        activeDirection = movement.AimDirection;

        EnterCombat();

        if (kind == AttackKind.Basic)
        {
            AttackPerformed?.Invoke();
        }
        else
        {
            ShieldBashPerformed?.Invoke();
        }
    }

    private void UpdateAttack()
    {
        if (activeKind == AttackKind.None)
        {
            if (queuedKind != AttackKind.None && (dodge == null || !dodge.IsDodging))
            {
                BeginAttack(queuedKind);
            }

            return;
        }

        float elapsed = Time.time - activeStartTime;

        if (hitPending && elapsed >= activeAttack.HitDelay)
        {
            hitPending = false;
            AttackResolver.Execute(combatant, activeAttack, activeWeaponDamage, transform.position, activeDirection, hitLayers);
        }

        float recovery = activeAttack != null ? activeAttack.RecoveryTime : FallbackRecoveryTime;
        if (elapsed >= recovery)
        {
            AttackKind next = queuedKind;
            activeKind = AttackKind.None;
            activeAttack = null;

            if (next != AttackKind.None)
            {
                BeginAttack(next);
            }
        }
    }

    private void AnnounceWeapon()
    {
        announcedWeapon = weapon;
        WeaponChanged?.Invoke(weapon);
    }

    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        lastActivityTime = Time.time;
    }

    // The timeout counts from entering Combat as well, so a manual toggle also returns to Exploration when left idle.
    private void OnGameStateChanged(GameState previous, GameState current)
    {
        if (current == GameState.Combat)
        {
            lastActivityTime = Time.time;
        }
    }

    private void EnterCombat()
    {
        lastActivityTime = Time.time;
        if (gameState != null)
        {
            gameState.TrySetState(GameState.Combat);
        }
    }
}

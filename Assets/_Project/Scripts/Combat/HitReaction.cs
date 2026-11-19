using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// How one target shows that it was hit: a short flash, a squash punch and, for monsters on the NavMesh, a small
/// knockback away from the attacker. Strengths come from the CombatFeel settings.
/// <para>
/// Where it belongs: Combat. CombatFeedback adds it on the fly to anything that is hit, so no setup is needed. Add it to
/// a prefab yourself only to change how that one target reacts (a boss that can't be pushed, a wall that doesn't squash).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class HitReaction : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private bool flash = true;
    [SerializeField] private bool punch = true;
    [Tooltip("Scales the knockback from the CombatFeel settings. 0 means this target can't be pushed.")]
    [SerializeField, Min(0f)] private float knockbackScale = 1f;

    /// <summary>How far this target is pushed, as a share of the CombatFeel distances. 0: it can't be pushed.</summary>
    public float KnockbackScale => knockbackScale;
    [Tooltip("The part that squashes. Left empty, the model under this object is used.")]
    [SerializeField] private Transform visualRoot;

    private readonly List<Renderer> renderers = new List<Renderer>();
    private MaterialPropertyBlock block;
    // Each renderer's own property block while the flash is on, put back when it ends (an elite's ring keeps its colour).
    private readonly Dictionary<Renderer, MaterialPropertyBlock> ownBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
    private bool flashing;
    private NavMeshAgent agent;
    private Damageable health;
    private Vector3 baseScale;
    private bool initialised;
    private float flashUntil = -1f;
    private float punchStart = -1f;
    private Vector3 knockbackVelocity;
    private float knockbackUntil = -1f;

    /// <summary>Squash on hits. Monsters turn it off: their MonsterPose owns the model's pose and recoils instead.</summary>
    public bool Punch
    {
        get => punch;
        set => punch = value;
    }

    /// <summary>The target's HitReaction, added with the default settings when it has none.</summary>
    public static HitReaction For(Component target)
    {
        if (!target.TryGetComponent(out HitReaction reaction))
        {
            reaction = target.gameObject.AddComponent<HitReaction>();
        }

        return reaction;
    }

    public void Play(in DamageInfo damage, in DamageResult result)
    {
        Initialise();
        CombatFeelSettings feel = CombatFeelSettings.Current;
        float now = Time.unscaledTime;

        if (flash && feel.FlashSeconds > 0f && renderers.Count > 0)
        {
            flashUntil = now + feel.FlashSeconds;
            SetFlash(true);
        }

        if (punch && feel.PunchAmount > 0f && feel.PunchSeconds > 0f && visualRoot != null)
        {
            punchStart = now;
        }

        float distance = KnockbackFor(damage, result, this) * knockbackScale;
        if (distance > 0f && agent != null && health != null && health.IsAlive)
        {
            Vector3 direction = damage.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                float seconds = KnockbackSecondsFor(distance);
                knockbackVelocity = direction.normalized * (distance / seconds);
                knockbackUntil = Time.time + seconds;
            }
        }

        enabled = true;
    }

    /// <summary>
    /// How far a blow shoves the body it lands on, in metres, before the body's own Knockback Scale: by the blow's weight (its Impact,
    /// to the power 1.5), its technique and the body (Docs/CombatFeelReview_2026-11-19.md §4). It was 0.35 m, or 0.8 m on a critical,
    /// whatever struck what: a dagger's sure critical shoved a troll further than a warhammer's heavy shoved a rootling. A dagger's stab
    /// now moves a body about 0.18 m, a sword's slash 0.35, a hammer's heavy 1.0, its charged blow or art up to Knockback Most; a
    /// heavyweight takes Knockback Heavyweight Share of it and a boss nothing. The chasm's push off an edge reads the same rule.
    /// </summary>
    public static float KnockbackFor(in DamageInfo damage, in DamageResult result, Component target)
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (feel == null)
        {
            return 0f;
        }

        if (target != null && target.TryGetComponent(out BossEncounter _))
        {
            return 0f;
        }

        float impact = damage.Attack != null ? damage.Attack.Impact : 1f;
        HitTechnique kind = damage.Technique;
        bool art = (kind & HitTechnique.WeaponArt) != 0;
        bool heavy = (kind & (HitTechnique.Heavy | HitTechnique.Finisher)) != 0;
        float technique = art ? feel.KnockbackArt : heavy ? feel.KnockbackHeavy
            : (kind & (HitTechnique.Counter | HitTechnique.GuardCounter)) != 0 ? feel.KnockbackCounter : 1f;
        float metres = feel.KnockbackDistance * Mathf.Pow(Mathf.Max(0f, impact), 1.5f) * technique * (result.IsCritical ? feel.KnockbackCritShare : 1f);
        // A heavy that isn't charged keeps the body near enough for its follow-up to reach (a chain's step closes about a metre).
        float most = heavy && !art && impact < 2f ? Mathf.Min(1f, feel.KnockbackMost) : feel.KnockbackMost;
        metres = Mathf.Min(metres, most);
        if (target != null && target.TryGetComponent(out MonsterAI monster) && monster.IsHeavyweight)
        {
            metres *= feel.KnockbackHeavyweightShare;
        }

        return metres;
    }

    /// <summary>How long a shove of <paramref name="metres"/> takes: a short shove snaps, a long one carries.</summary>
    public static float KnockbackSecondsFor(float metres) => 0.10f + 0.05f * metres;

    private void Initialise()
    {
        if (initialised)
        {
            return;
        }

        initialised = true;
        block = new MaterialPropertyBlock();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Damageable>();
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
        {
            // Effects and ground markers (telegraphs, trails, particles) shouldn't flash with the body.
            // Nor an outline's copies or a label's text (the elite's name), which flashed white too.
            if (candidate is ParticleSystemRenderer || candidate is TrailRenderer || candidate is LineRenderer
                || candidate.GetComponentInParent<AttackTelegraph>() != null || OutlineCopies.IsCopy(candidate)
                || candidate.GetComponent<TMPro.TMP_Text>() != null)
            {
                continue;
            }

            renderers.Add(candidate);
        }

        visualRoot = visualRoot != null ? visualRoot : FindVisualRoot();
        if (visualRoot != null)
        {
            baseScale = visualRoot.localScale;
        }
    }

    // Squashing the root would also squash its collider, character controller or agent, so a model child is preferred.
    private Transform FindVisualRoot()
    {
        foreach (Renderer candidate in renderers)
        {
            Transform part = candidate.transform;
            if (part == transform)
            {
                bool physical = GetComponent<CharacterController>() != null || agent != null || GetComponent<Rigidbody>() != null;
                return physical ? null : transform;
            }

            while (part.parent != null && part.parent != transform)
            {
                part = part.parent;
            }

            if (part.parent == transform)
            {
                return part;
            }
        }

        return null;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        bool busy = false;

        if (flashUntil >= 0f)
        {
            if (now >= flashUntil)
            {
                flashUntil = -1f;
                SetFlash(false);
            }
            else
            {
                busy = true;
            }
        }

        if (punchStart >= 0f && visualRoot != null)
        {
            CombatFeelSettings feel = CombatFeelSettings.Current;
            float t = (now - punchStart) / feel.PunchSeconds;
            if (t >= 1f)
            {
                punchStart = -1f;
                visualRoot.localScale = baseScale;
            }
            else
            {
                // Squashed flat and wide at the moment of impact, springing back with a small overshoot.
                float squash = feel.PunchAmount * Mathf.Cos(t * Mathf.PI * 1.5f) * (1f - t);
                visualRoot.localScale = Vector3.Scale(baseScale, new Vector3(1f + squash, 1f - squash, 1f + squash));
                busy = true;
            }
        }

        if (knockbackUntil >= 0f)
        {
            if (Time.time >= knockbackUntil || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                knockbackUntil = -1f;
            }
            else
            {
                agent.Move(knockbackVelocity * Time.deltaTime);
                busy = true;
            }
        }

        enabled = busy;
    }

    private void OnDisable()
    {
        if (!initialised)
        {
            return;
        }

        if (flashUntil >= 0f)
        {
            flashUntil = -1f;
            SetFlash(false);
        }

        if (punchStart >= 0f && visualRoot != null)
        {
            punchStart = -1f;
            visualRoot.localScale = baseScale;
        }

        knockbackUntil = -1f;
    }

    private void SetFlash(bool on)
    {
        Color color = CombatFeelSettings.Current.FlashColor;
        foreach (Renderer target in renderers)
        {
            if (target == null)
            {
                continue;
            }

            if (on)
            {
                // Its own block first (an elite's affix ring takes its colour from one), kept to put back: clearing it left the ring
                // in the telegraph material's colour after the first blow (Docs/CombatFeelReview_2026-11-19.md §13).
                if (!flashing && target.HasPropertyBlock())
                {
                    if (!ownBlocks.TryGetValue(target, out MaterialPropertyBlock own))
                    {
                        own = new MaterialPropertyBlock();
                        ownBlocks[target] = own;
                    }

                    target.GetPropertyBlock(own);
                }
                else if (!flashing)
                {
                    ownBlocks.Remove(target);
                }

                block.Clear();
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                block.SetColor(EmissionColorId, color);
                target.SetPropertyBlock(block);
            }
            else
            {
                target.SetPropertyBlock(ownBlocks.TryGetValue(target, out MaterialPropertyBlock own) ? own : null);
            }
        }

        flashing = on;
    }
}

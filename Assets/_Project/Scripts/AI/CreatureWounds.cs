using UnityEngine;

/// <summary>
/// A creature's hurts show (2026-11-14): badly hurt it limps (slower, so a hurt one can be run down); hurt enough it leaves
/// a trail of blood as it goes, which can be followed to where it fled; an armoured one has pieces of its armour broken off
/// by heavy blows, each one taking some of its armour with it (MonsterDefinition › Wounds). Added by MonsterAI.
/// <para>
/// A boss whose bark no single blow could chip loses a piece each time it topples instead (Monster Definition › Topple Breaks Armour;
/// Docs/HeartrootBossReview_2026-11-17.md C2): breaking its poise is what wears its armour away.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class CreatureWounds : MonoBehaviour
{
    private const float TrailStep = 1.4f;

    private MonsterAI ai;
    private Damageable health;
    private Combatant combatant;
    private MonsterDefinition definition;
    private bool limping;
    private int chipsLeft;
    private Vector3 lastDrop;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        health = GetComponent<Damageable>();
        combatant = GetComponent<Combatant>();
    }

    private void Start()
    {
        definition = ai != null ? ai.Definition : null;
        chipsLeft = definition != null ? definition.ArmourChips : 0;
        lastDrop = transform.position;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.HealthChanged += OnHealthChanged;
        }

        if (ai != null)
        {
            ai.Toppled += OnToppled;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.HealthChanged -= OnHealthChanged;
        }

        if (ai != null)
        {
            ai.Toppled -= OnToppled;
        }
    }

    private float Share => health != null && health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 1f;

    // Limping starts when it falls below the line and stops if it mends above it (a salve, regrowth).
    private void OnHealthChanged(float current, float max)
    {
        if (definition == null || ai == null || definition.LimpBelow <= 0f || !health.IsAlive)
        {
            return;
        }

        bool hurt = Share < definition.LimpBelow;
        if (hurt == limping)
        {
            return;
        }

        limping = hurt;
        float speed = Mathf.Max(0.3f, definition.LimpSpeed);
        ai.SpeedScale = limping ? ai.SpeedScale * speed : ai.SpeedScale / speed;
    }

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (chipsLeft <= 0 || result.Killed || result.Damage <= 0f || damage.IsOverTime || combatant == null || health.MaxHealth <= 0f)
        {
            return;
        }

        float needed = definition.ChipFromShare * (damage.Strike == StrikeKind.Blunt ? 0.5f : 1f);
        if (result.Damage / health.MaxHealth < needed)
        {
            return;
        }

        BreakPiece(damage.HitPoint, damage.Direction);
    }

    // It toppled: a piece of its bark splits off at its chest, whatever the blow that floored it.
    private void OnToppled(float seconds)
    {
        if (definition == null || !definition.ToppleBreaksArmour || chipsLeft <= 0 || combatant == null || health == null || !health.IsAlive)
        {
            return;
        }

        BreakPiece(transform.position + Vector3.up * ai.ChestHeight, -transform.forward);
    }

    // A piece breaks off: shards fly, the piece itself falls away when the model names one, and it is that much less protected.
    private void BreakPiece(Vector3 at, Vector3 blowDirection)
    {
        int chip = definition.ArmourChips - chipsLeft;
        chipsLeft--;
        if (chip < definition.ArmourParts.Count && !string.IsNullOrWhiteSpace(definition.ArmourParts[chip]))
        {
            ArmourDebris.BreakOff(transform, definition.ArmourParts[chip], blowDirection);
        }

        float armour = combatant.Stats.Get(StatType.Armor);
        combatant.Stats.AddModifier(new StatModifier(StatType.Armor, StatModifierOp.Flat, -armour * definition.ArmourPerChip, this));
        WoundSettings settings = WoundSettings.Current;
        if (settings.ArmourShards != null)
        {
            PrefabPool.Spawn(settings.ArmourShards, at, Quaternion.LookRotation(blowDirection.sqrMagnitude > 1e-4f ? -blowDirection : Vector3.forward));
        }

        AudioManager.Play(settings.ArmourChipSound, at);
    }

    private void Update()
    {
        if (definition == null || !definition.Bleeds || definition.BleedTrailBelow <= 0f || health == null || !health.IsAlive
            || Share >= definition.BleedTrailBelow || !GameOptions.Blood.On)
        {
            return;
        }

        Vector3 here = transform.position;
        Vector3 moved = here - lastDrop;
        moved.y = 0f;
        // Drops further apart the less it bleeds: a trail that thickens as it weakens.
        float step = TrailStep * Mathf.Lerp(1f, 2.2f, Share / Mathf.Max(0.01f, definition.BleedTrailBelow));
        if (moved.magnitude < step)
        {
            return;
        }

        lastDrop = here;
        Vector3 side = Vector3.Cross(Vector3.up, moved.normalized) * Random.Range(-0.25f, 0.25f);
        if (BloodDecals.Available && Physics.Raycast(here + side + Vector3.up, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore)
            && !ground.collider.transform.IsChildOf(transform))
        {
            BloodDecals.Splat(ground.point, ground.normal, Random.Range(0.25f, 0.5f), definition.BloodColor, WoundSettings.Current.TrailSeconds, 1.2f, 0.4f);
        }
    }
}

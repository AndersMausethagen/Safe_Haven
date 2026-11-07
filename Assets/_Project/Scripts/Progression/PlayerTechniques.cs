using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The techniques that answer what happens to the player (PerkTechnique): Riposte (a parry readies a counter), Second Wind
/// and Soul Siphon (a kill gives back stamina or health), Pinning Shot (a critical arrow roots), Sweep (picking a plant by
/// hand picks its neighbours) and Forager's Haste (a burst of speed after a plant). The rest live in the systems they
/// change. Added by Player.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTechniques : MonoBehaviour
{
    private static readonly Collider[] near = new Collider[32];
    private Combatant combatant;
    private PlayerCombat combat;
    private PlayerGuard guard;
    private PlayerStamina stamina;
    private Damageable health;
    private PlayerBoons boons;
    private bool sweeping;

    public static PlayerTechniques AddTo(GameObject player) =>
        player.TryGetComponent(out PlayerTechniques existing) ? existing : player.AddComponent<PlayerTechniques>();

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
        combat = GetComponent<PlayerCombat>();
        guard = GetComponent<PlayerGuard>();
        stamina = GetComponent<PlayerStamina>();
        health = GetComponent<Damageable>();
        boons = GetComponent<PlayerBoons>();
    }

    private void OnEnable()
    {
        if (combatant != null) combatant.DamageDealt += OnDamageDealt;
        if (guard != null) guard.Parried += OnParried;
        ResourceSource.Harvested += OnHarvested;
    }

    private void OnDisable()
    {
        if (combatant != null) combatant.DamageDealt -= OnDamageDealt;
        if (guard != null) guard.Parried -= OnParried;
        ResourceSource.Harvested -= OnHarvested;
    }

    private static TechniqueSettings Rules => TechniqueSettings.Current;

    private void OnParried(DamageInfo damage)
    {
        if (combat != null && Techniques.Has(PerkTechnique.Riposte))
        {
            combat.GrantCounter();
        }
    }

    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        if (result.Killed && (damage.Technique & HitTechnique.Melee) != 0 && stamina != null && Techniques.Has(PerkTechnique.SecondWind))
        {
            stamina.Restore(Rules.SecondWindStamina);
        }

        if (result.Killed && (damage.Technique & HitTechnique.Spell) != 0 && health != null && Techniques.Has(PerkTechnique.SoulSiphon))
        {
            health.Heal(health.MaxHealth * Rules.SoulSiphon);
        }

        if (!result.Killed && result.IsCritical && (damage.Technique & HitTechnique.Shot) != 0 && Rules.Pinning != null
            && target is Damageable body && body.IsAlive && Techniques.Has(PerkTechnique.PinningShot))
        {
            StatusEffects.Apply(body, Rules.Pinning, combatant);
        }
    }

    private void OnHarvested(GameObject gatherer, ResourceSource source)
    {
        GatheringNodeDefinition node = source != null ? source.Node : null;
        if (sweeping || node == null || gatherer != gameObject || (node.GatherTool != ToolType.None && node.GatherTool != ToolType.Sickle))
        {
            return;
        }

        if (boons != null && Techniques.Has(PerkTechnique.ForagersHaste))
        {
            boons.Add("technique.foragers_haste", "Forager's Haste",
                new List<StatBonus> { new StatBonus(StatType.MoveSpeed, Rules.HasteSpeed) }, Rules.HasteSeconds, false);
        }

        if (node.GatherTool == ToolType.None && Techniques.Has(PerkTechnique.Sweep))
        {
            Sweep(source, node);
        }
    }

    // The same plants close by come away with the one picked.
    private void Sweep(ResourceSource picked, GatheringNodeDefinition kind)
    {
        sweeping = true;
        try
        {
            int count = Physics.OverlapSphereNonAlloc(picked.transform.position, Rules.SweepRadius, near, ~0, QueryTriggerInteraction.Collide);
            int swept = 0;
            for (int i = 0; i < count && swept < Rules.SweepMost; i++)
            {
                ResourceNode other = near[i].GetComponentInParent<ResourceNode>();
                if (other != null && other.gameObject != picked.gameObject && other.Definition == kind && other.GatherBy(gameObject))
                {
                    swept++;
                }
            }

            if (swept > 0)
            {
                GameMessages.Post($"Sweep: {swept} more {kind.DisplayName} gathered.", MessageKind.Gain);
            }
        }
        finally
        {
            sweeping = false;
        }
    }
}

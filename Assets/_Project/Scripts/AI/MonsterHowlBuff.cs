using UnityEngine;

/// <summary>One timed howl per recipient; another howl refreshes it, never adds a second modifier.</summary>
[RequireComponent(typeof(Combatant))]
public sealed class MonsterHowlBuff : MonoBehaviour
{
    private Combatant combatant;
    private float expiresAt;
    public float Bonus { get; private set; }
    public bool IsActive => Bonus > 0f && Time.time < expiresAt;

    private void Awake() => combatant = GetComponent<Combatant>();

    public void Refresh(float bonus, float seconds)
    {
        combatant.Stats.RemoveModifiersFrom(this);
        Bonus = bonus;
        expiresAt = Time.time + seconds;
        combatant.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, bonus, this));
    }

    private void Update()
    {
        if (Bonus > 0f && Time.time >= expiresAt)
        {
            Clear();
        }
    }

    private void OnDisable() => Clear();

    private void Clear()
    {
        if (combatant != null)
        {
            combatant.Stats.RemoveModifiersFrom(this);
        }
        Bonus = 0f;
    }
}

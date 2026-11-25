using UnityEngine;

/// <summary>
/// Carries out <see cref="BlowShaping"/> on the player's own Damageable: no blow from anything but a boss takes more than a share of
/// their health, and a big blow is followed by a moment in which the next ones take less, so a chain of strikes landing inside a second
/// can't kill without a chance to answer. Added to the player by Player.Awake (AddTo); the Damageable asks it through IBlowShaper.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBlowGrace : MonoBehaviour, IBlowShaper
{
    private float graceUntil = float.NegativeInfinity;

    /// <summary>True while blows take less after a big one.</summary>
    public bool InGrace => Time.time < graceUntil;

    public static void AddTo(GameObject player)
    {
        if (player != null && !player.TryGetComponent(out PlayerBlowGrace _))
        {
            player.AddComponent<PlayerBlowGrace>();
        }
    }

    public float Shape(in DamageInfo hit, float final, float maxHealth)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        bool capped = IsCapped(hit.Source);
        bool inGrace = InGrace;
        float shaped = BlowShaping.Shape(final, maxHealth, capped, inGrace, rules);
        // A blow in the grace doesn't make it longer: the grace is the breath after a big blow, not a shield that keeps itself up.
        if (!inGrace && BlowShaping.StartsGrace(shaped, maxHealth, rules))
        {
            graceUntil = Time.time + rules.GraceSeconds;
        }

        return shaped;
    }

    /// <summary>A creature's blow, not a boss's: what the cap holds back.</summary>
    public static bool IsCapped(Combatant source) =>
        source != null && source.TryGetComponent(out MonsterAI _) && !source.TryGetComponent(out BossEncounter _);
}

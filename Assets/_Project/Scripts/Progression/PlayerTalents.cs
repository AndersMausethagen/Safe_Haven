using UnityEngine;

/// <summary>
/// Puts the talents bought in GameSession.Progress on the player: every rank's bonuses become stat modifiers on the
/// player's Combatant, kept up to date when talents change (bought, reset or loaded). Lives on the player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Combatant))]
public sealed class PlayerTalents : MonoBehaviour
{
    private Combatant combatant;
    private CharacterProgress watched;

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
    }

    private void OnEnable()
    {
        Watch(GameSession.Progress);
        Apply();
    }

    private void OnDisable()
    {
        Watch(null);
        combatant.Stats.RemoveModifiersFrom(this);
    }

    // A loaded game replaces the progress object, so it is checked again every frame (cheap: a reference compare).
    private void Update()
    {
        if (watched != GameSession.Progress)
        {
            Watch(GameSession.Progress);
            Apply();
        }
    }

    private void Watch(CharacterProgress progress)
    {
        if (watched != null)
        {
            watched.Changed -= Apply;
        }

        watched = progress;
        if (watched != null)
        {
            watched.Changed += Apply;
        }
    }

    /// <summary>Replaces the talent modifiers with the ones from the current ranks.</summary>
    public void Apply()
    {
        StatSheet stats = combatant.Stats;
        stats.RemoveModifiersFrom(this);
        LevelingSettings settings = LevelingSettings.Current;
        foreach (TalentDefinition talent in settings.Talents)
        {
            int rank = watched != null ? watched.RankOf(talent) : 0;
            if (talent == null || rank <= 0)
            {
                continue;
            }

            foreach (StatBonus bonus in talent.PerRank)
            {
                stats.AddModifier(new StatModifier(bonus.stat, bonus.op, bonus.value * rank, this));
            }
        }
    }
}

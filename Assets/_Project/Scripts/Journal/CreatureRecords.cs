using UnityEngine;

/// <summary>
/// Writes down the creatures the player comes up against, for the journal: one is met when it spots the player or a blow passes between them, and
/// slain when the player's side kills it. It also remembers what last brought the player down. The record itself is in CharacterProgress, so it is
/// saved and a failed raid doesn't take it back.
/// </summary>
public static class CreatureRecords
{
    private static MonsterDefinition lastKiller;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Listen()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyDamaged += OnAnyDamaged;
        Damageable.AnyDied -= OnAnyDied;
        Damageable.AnyDied += OnAnyDied;
        lastKiller = null;
    }

    /// <summary>The player and this creature have seen each other.</summary>
    public static void NoteMet(MonsterDefinition creature)
    {
        if (creature != null && !GameSession.Progress.HasMet(creature.Id))
        {
            GameSession.Progress.NoteMet(creature.Id);
            JournalNotices.CreatureWritten(creature, true);
        }
    }

    /// <summary>What brought the player down since this was last asked, by name. Null when nothing has.</summary>
    public static string TakeKillerName()
    {
        string name = lastKiller != null ? lastKiller.DisplayName : null;
        lastKiller = null;
        return name;
    }

    private static void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (damage.Source == null)
        {
            return;
        }

        if (Player.IsPlayer(damage.Source.gameObject) && victim.TryGetComponent(out MonsterAI struck))
        {
            NoteMet(struck.Definition);
        }
        else if (Player.Current != null && victim == Player.Current.Health && damage.Source.TryGetComponent(out MonsterAI striker))
        {
            NoteMet(striker.Definition);
        }
    }

    private static void OnAnyDied(Damageable victim, DamageInfo blow)
    {
        if (blow.Source == null)
        {
            return;
        }

        if (Player.Current != null && victim == Player.Current.Health)
        {
            if (blow.Source.TryGetComponent(out MonsterAI killer) && killer.Definition != null)
            {
                lastKiller = killer.Definition;
                int before = JournalContent.KnowledgeOf(lastKiller, GameSession.Progress);
                GameSession.Progress.Journal.NoteKilledBy(lastKiller.Id);
                if (JournalContent.KnowledgeOf(lastKiller, GameSession.Progress) > before)
                {
                    JournalNotices.CreatureWritten(lastKiller, false);
                }
            }

            return;
        }

        if (victim.TryGetComponent(out MonsterAI fallen) && fallen.Definition != null && blow.Source.Faction.IsPlayerSide())
        {
            bool first = !GameSession.Progress.HasMet(fallen.Definition.Id);
            int before = JournalContent.KnowledgeOf(fallen.Definition, GameSession.Progress);
            GameSession.Progress.NoteSlain(fallen.Definition.Id);
            if (first || JournalContent.KnowledgeOf(fallen.Definition, GameSession.Progress) > before)
            {
                JournalNotices.CreatureWritten(fallen.Definition, first);
            }
        }
    }
}

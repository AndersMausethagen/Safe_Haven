using UnityEngine;

/// <summary>
/// Writes down the creatures the player comes up against, for the journal: one is met when it spots the player or a blow passes between them, and
/// slain when the player's side kills it. It also remembers what last brought the player down. The record itself is in CharacterProgress, so it is
/// saved and a failed raid doesn't take it back.
/// </summary>
public static class CreatureRecords
{
    /// <summary>Marks a killer's name that is a person's own (a Lost One's), so it is said without "a" (<see cref="ByWhom"/>).</summary>
    public const char OwnNameMark = '~';

    private static MonsterDefinition lastKiller;
    // A killer with a name of its own (a Lost One), marked: said in place of its kind.
    private static string lastKillerName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Listen()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyDamaged += OnAnyDamaged;
        Damageable.AnyDied -= OnAnyDied;
        Damageable.AnyDied += OnAnyDied;
        lastKiller = null;
        lastKillerName = null;
    }

    /// <summary>Forgets what brought the player down in the game being played (a new game, or a load: GameSession).</summary>
    public static void ForgetGame()
    {
        lastKiller = null;
        lastKillerName = null;
    }

    /// <summary>
    /// How the death screen and the journal say who brought the player down, from a kept name: "a Draug", or, for one with a name of
    /// its own (marked with <see cref="OwnNameMark"/>), "Halvard Hollow-Eyed, one of the Lost". Records from before have no mark.
    /// </summary>
    public static string ByWhom(string broughtDownBy)
    {
        if (string.IsNullOrEmpty(broughtDownBy))
        {
            return broughtDownBy;
        }

        return broughtDownBy[0] == OwnNameMark ? broughtDownBy.Substring(1) : $"a {broughtDownBy}";
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
    public static string TakeKillerName() => TakeKillerName(out _);

    /// <summary>The same, with the killer's journal advice (how to face it), for the death screen (research G3). Null advice when it has none.</summary>
    public static string TakeKillerName(out string advice)
    {
        string name = lastKillerName ?? (lastKiller != null ? lastKiller.DisplayName : null);
        advice = lastKiller != null && !string.IsNullOrWhiteSpace(lastKiller.JournalAdvice) ? lastKiller.JournalAdvice : null;
        lastKiller = null;
        lastKillerName = null;
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
        // Every death of the player says what (if anything) brought them down: one with no creature behind it (a ravine fall)
        // reported the monster of some earlier death at home, never asked about, in the raid's report and journal for good.
        if (Player.Current != null && victim == Player.Current.Health)
        {
            lastKiller = null;
            lastKillerName = null;
        }

        if (blow.Source == null)
        {
            return;
        }

        if (Player.Current != null && victim == Player.Current.Health)
        {
            if (blow.Source.TryGetComponent(out MonsterAI killer) && killer.Definition != null)
            {
                lastKiller = killer.Definition;
                // One of the Lost is named, on the host's game and on a friend's copy alike (both carry its name on the elite).
                if (blow.Source.TryGetComponent(out EliteCreature elite) && elite.HasOwnName)
                {
                    lastKillerName = $"{OwnNameMark}{elite.Title}, one of the Lost";
                }

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

import os
os.chdir(r"D:\Safe Haven\Safe Haven\Assets\_Project\Scripts")


def edit(rel, pairs):
    raw = open(rel, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        assert s.count(old) == 1, (rel, old[:80], s.count(old))
        s = s.replace(old, new)
    if crlf:
        s = s.replace("\n", "\r\n")
    open(rel, "w", encoding="utf-8", newline="").write(s)
    print("ok", rel)


# 3. A rout is heard and seen.
edit("AI/MonsterAI.Fighting.cs", [
    ("""    /// <summary>A broken pack: it runs from its target for this long, and doesn't strike. Elites and those sent on a mission stand.</summary>
    public void Rout(float seconds)
    {
        if (seconds <= 0f || IsElite || Relentless || CurrentState == State.Dead)
        {
            return;
        }

        routedUntil = Time.time + seconds;
        circling = false;""", """    /// <summary>A broken pack: it runs from its target for this long, and doesn't strike. Elites and those sent on a mission stand.
    /// It cries out as it turns (after <paramref name="cryAfter"/> seconds, so a pack's cries come one after another) and its mark
    /// shows it is beaten (Docs/EnemyAIReview_2026-11-19.md §3: a rout made no sound and changed nothing to see).</summary>
    public void Rout(float seconds, float cryAfter = 0f)
    {
        if (seconds <= 0f || IsElite || Relentless || CurrentState == State.Dead)
        {
            return;
        }

        routedUntil = Time.time + seconds;
        circling = false;
        breakingAwayUntil = -1f;
        if (isActiveAndEnabled)
        {
            StartCoroutine(CryAfter(cryAfter));
        }"""),
    ("""    private bool IsRouted => Time.time < routedUntil;""", """    private bool IsRouted => Time.time < routedUntil;

    /// <summary>True while it runs from a broken pack (its mark shows it).</summary>
    public bool IsRoutedNow => IsRouted;

    private System.Collections.IEnumerator CryAfter(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        if (CurrentState != State.Dead)
        {
            CryOut();
        }
    }"""),
])

edit("AI/MonsterPack.cs", [
    ("""        broken = true;
        foreach (MonsterAI member in members)
        {
            if (member != null && member != fallen)
            {
                member.Rout(fleeSeconds);
            }
        }""", """        broken = true;
        int crying = 0;
        foreach (MonsterAI member in members)
        {
            if (member != null && member != fallen)
            {
                // One after another, so a pack's yelps are heard as a pack breaking, not one sound.
                member.Rout(fleeSeconds, 0.15f * crying++);
            }
        }

        // Said once a pack, so the player knows what they did (Halo, F.E.A.R.: show the group's decision).
        if (crying > 0 && Player.Current != null && Vector3.Distance(Player.Current.transform.position, transform.position) < 40f)
        {
            GameMessages.Post("The pack breaks!", MessageKind.Info);
        }

        PlaytestLogger.Note(PlaytestLog.EnemyAI, "Rout", crying, fallen != null && fallen.Definition != null ? fallen.Definition.name : name);"""),
    ("""        startCount = 0;
        foreach (MonsterAI member in members)""", """        // The one that leads stands out (§3): a little bigger and paler, so "bring down the one that howls" can be acted on.
        if (leader != null && !leaderMarked)
        {
            leaderMarked = true;
            leader.transform.localScale *= LeaderScale;
            CreatureLook.Tint(leader.gameObject, new Color(1.15f, 1.12f, 1.08f, 1f));
        }

        startCount = 0;
        foreach (MonsterAI member in members)"""),
    ("""    private bool broken;
""", """    private bool broken;
    private bool leaderMarked;
    private const float LeaderScale = 1.1f;
"""),
])

edit("AI/Perception/AwarenessIndicator.cs", [
    ("""        bool visible = rules.ShowIndicators && monster != null && monster.CurrentState != MonsterAI.State.Dead;
        if (visible && Time.time < spottedUntil)""", """        bool visible = rules.ShowIndicators && monster != null && monster.CurrentState != MonsterAI.State.Dead;
        if (visible && monster.IsRoutedNow)
        {
            // Beaten: running from its broken pack (Docs/EnemyAIReview_2026-11-19.md §3).
            Show("\\u25BC", new Color(0.7f, 0.7f, 0.7f, 0.85f), 0.8f);
        }
        else if (visible && Time.time < spottedUntil)"""),
])

# 0. Where a blow came from, in the Hurt row: front, side or back of the player; from out of view; how many were at them.
edit("AI/MonsterAI.Fighting.cs", [
    ("""    // How far past the screen's edge (a share of the screen) a body still counts as seen""", """    /// <summary>True when this game's player is its target and it stands out of their view (the camera's frame).</summary>
    public bool IsOffScreenForLocalPlayer => OffScreen(CombatTechniqueSettings.Current);

    /// <summary>How many creatures are after <paramref name="who"/> within <paramref name="range"/> metres of it: a crowd, for the log.</summary>
    public static int CountAt(IDamageable who, Vector3 at, float range)
    {
        int count = 0;
        foreach (MonsterAI monster in active)
        {
            if (monster != null && monster.target == who && monster.CurrentState != State.Dead && (monster.transform.position - at).sqrMagnitude <= range * range)
            {
                count++;
            }
        }

        return count;
    }

    // How far past the screen's edge (a share of the screen) a body still counts as seen"""),
])

edit("Playtest/PlaytestLogger.cs", [
    ("""        string by = damage.Source != null ? (damage.Source.TryGetComponent(out MonsterAI monster) && monster.Definition != null ? monster.Definition.name : damage.Source.name) : "unknown";
        Write(PlaytestLog.Hurt, by, Mathf.RoundToInt(result.Damage), $"{(damage.Attack != null ? damage.Attack.name : "-")} {damage.Type}{(result.Blocked ? " blocked" : "")}");""",
     """        string by = damage.Source != null ? (damage.Source.TryGetComponent(out MonsterAI monster) && monster.Definition != null ? monster.Definition.name : damage.Source.name) : "unknown";
        Write(PlaytestLog.Hurt, by, Mathf.RoundToInt(result.Damage), $"{(damage.Attack != null ? damage.Attack.name : "-")} {damage.Type}{(result.Blocked ? " blocked" : "")}"
            + WhereFrom(damage, health));
    }

    /// <summary>Where a blow came from (Docs/EnemyAIReview_2026-11-19.md §0), appended to the Hurt row so old readers still work: " from
    /// front|side|back" (front within the guard's 70° either side of facing, back beyond 110°), " offscreen" when the striker was out of
    /// view, and " crowd N", the creatures after the player within 8 m.</summary>
    public static string WhereFrom(in DamageInfo damage, Damageable victim)
    {
        if (damage.Source == null || victim == null)
        {
            return "";
        }

        Vector3 to = damage.Source.transform.position - victim.transform.position;
        to.y = 0f;
        Vector3 facing = victim.transform.forward;
        facing.y = 0f;
        string side = to.sqrMagnitude < 0.0001f ? "front" : FromSide(Vector3.Angle(facing, to));
        bool offscreen = damage.Source.TryGetComponent(out MonsterAI monster) && monster.IsOffScreenForLocalPlayer;
        int crowd = MonsterAI.CountAt(victim, victim.transform.position, 8f);
        return $" from {side}{(offscreen ? " offscreen" : "")} crowd {crowd}";
    }

    /// <summary>front, side or back, by the angle between the player's facing and the striker.</summary>
    public static string FromSide(float angle) => angle <= 70f ? "front" : angle >= 110f ? "back" : "side";"""),
])

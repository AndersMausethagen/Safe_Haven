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


# 8. The alarm as an action: the first of a group to spot the player calls before it engages; broken, only suspicion spreads.
edit("AI/MonsterAttack.cs", [
    ("""    [Tooltip("Every creature of its side within Buff Radius learns where its foe is and comes (a mistwing's screech).")]
    public bool alertsOthers;""", """    [Tooltip("Every creature of its side within Buff Radius learns where its foe is and comes (a mistwing's screech).")]
    public bool alertsOthers;
    [Tooltip("Raise the alarm (Docs/EnemyAIReview_2026-11-19.md §8): the first of its group to spot the player calls this before it fights, " +
             "once a group in 20 s, and its group waits for the call. Completed, every creature of its side within Alarm Radius comes; " +
             "broken (make it Interruptible), only the suspicion of its spotting spreads. Turns an alarm into something to answer.")]
    public bool onSpotted;
    [Min(1f)] public float alarmRadius = 18f;"""),
])

edit("AI/MonsterAttacks.cs", [
    ("""        Component foe = Target as Component;
        if (action.alertsOthers && foe != null)
        {
            foreach (MonsterAI other in MonsterAI.Active)
            {
                if (other.transform != self && other.CurrentState != MonsterAI.State.Dead && other.TryGetComponent(out Combatant side)
                    && side.Faction == combatant.Faction && Vector3.Distance(self.position, other.transform.position) <= action.buffRadius""",
     """        Component foe = Target as Component;
        // A raised alarm carries further than the call's rally (§8).
        float alertReach = action.onSpotted ? Mathf.Max(action.buffRadius, action.alarmRadius) : action.buffRadius;
        if (action.alertsOthers && foe != null)
        {
            if (action.onSpotted)
            {
                PlaytestLogger.Note(PlaytestLog.EnemyAI, "Alarm", 0, self.name);
            }

            foreach (MonsterAI other in MonsterAI.Active)
            {
                if (other.transform != self && other.CurrentState != MonsterAI.State.Dead && other.TryGetComponent(out Combatant side)
                    && side.Faction == combatant.Faction && Vector3.Distance(self.position, other.transform.position) <= alertReach"""),
])

edit("AI/MonsterAI.Awareness.cs", [
    ("""        if (global::Player.IsPlayer(targetComponent.gameObject))
        {
            CreatureRecords.NoteMet(definition);
            NoteSpottedPlayer();
        }""", """        if (global::Player.IsPlayer(targetComponent.gameObject))
        {
            CreatureRecords.NoteMet(definition);
            NoteSpottedPlayer();
            PlanAlarm();
        }"""),
    ("""    // It has just spotted (or been set on) a target: the "!", a sound when it's the player, and a call that sends idle""",
     """    // The alarm (Docs/EnemyAIReview_2026-11-19.md §8): which of its calls it raises on spotting the player, if one is ready and its group
    // hasn't called in the last 20 s. Its group then waits for the call instead of coming at once.
    private int alarmAction = -1;
    private static readonly System.Collections.Generic.Dictionary<Vector2Int, float> alarmsRaised = new System.Collections.Generic.Dictionary<Vector2Int, float>();
    private const float AlarmGroupSeconds = 20f;

    private Vector2Int AlarmGroup => pack != null
        ? new Vector2Int(pack.GetHashCode(), 0)
        : new Vector2Int(Mathf.FloorToInt(transform.position.x / 20f), Mathf.FloorToInt(transform.position.z / 20f) + 100000);

    private void PlanAlarm()
    {
        alarmAction = -1;
        if (IsRemote || definition == null || (pack != null && !pack.IsLeader(this) && PackHasCaller()))
        {
            return;
        }

        Vector2Int group = AlarmGroup;
        if (alarmsRaised.TryGetValue(group, out float at) && Time.time - at < AlarmGroupSeconds)
        {
            return;
        }

        for (int i = 0; i < definition.Attacks.Count; i++)
        {
            MonsterAttack action = definition.Attacks[i];
            if (action != null && action.onSpotted && action.kind == MonsterAttackKind.Howl && (!action.packLeaderOnly || IsHowler))
            {
                alarmAction = i;
                alarmsRaised[group] = Time.time;
                return;
            }
        }
    }

    // A pack whose leader can raise the alarm leaves it to the leader.
    private bool PackHasCaller()
    {
        foreach (MonsterAttack action in definition.Attacks)
        {
            if (action != null && action.onSpotted && action.packLeaderOnly)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True while it is about to raise the alarm, or raising it: its pack waits for the call.</summary>
    private bool RaisesAlarm => alarmAction >= 0;

    // It has just spotted (or been set on) a target: the "!", a sound when it's the player, and a call that sends idle"""),
])

edit("AI/MonsterAI.Targets.cs", [
    ("""        RefreshAmbush();
        if (pack != null)
        {
            pack.Alert(newTarget);
        }""", """        RefreshAmbush();
        // Raising the alarm, the pack waits for the call: break it and only the suspicion spreads (§8).
        if (pack != null && !RaisesAlarm)
        {
            pack.Alert(newTarget);
        }"""),
])

edit("AI/MonsterAI.Fighting.cs", [
    ("""        if (HoldsWhileDrawing(toTarget) || DodgesArrow())
        {
            return;
        }
""", """        if (HoldsWhileDrawing(toTarget) || DodgesArrow())
        {
            return;
        }

        // The alarm goes first, before any blow, when it can be raised (§8).
        if (alarmAction >= 0)
        {
            int call = alarmAction;
            alarmAction = -1;
            if (attacks.IsReady && call < definition.Attacks.Count)
            {
                movement.Stop();
                SetState(State.Chasing);
                BeginAttack(call, toTarget);
                return;
            }
        }
"""),
])

edit("AI/MonsterAI.cs", [
    ("""        grudges.Clear();""", """        grudges.Clear();
        alarmsRaised.Clear();"""),
])
print("batch 5 done")

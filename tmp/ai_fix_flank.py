import os
os.chdir(r"D:\Safe Haven\Safe Haven\Assets\_Project\Scripts")


def edit(rel, pairs):
    raw = open(rel, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        assert s.count(old) == 1, (rel, old[:90], s.count(old))
        s = s.replace(old, new)
    if crlf:
        s = s.replace("\n", "\r\n")
    open(rel, "w", encoding="utf-8", newline="").write(s)
    print("ok", rel)


edit("AI/MonsterAI.Fighting.cs", [
    ("""        if (flankers.TryGetValue(target, out var role) && (role.who == null || now > role.until || role.who.target != target))
        {
            flankers.Remove(target);
            role = default;
        }""", """        if (flankers.TryGetValue(target, out var role) && (role.who == null || now > role.until || role.who.target != target))
        {
            // Out of time before it got round (a stone at the target's back): it sits the next ones out, so another gets the chance
            // and it gets its turns at the front.
            if (role.who != null && !role.who.flankInPlace)
            {
                role.who.flankRestUntil = now + FlankRest;
            }

            flankers.Remove(target);
            role = default;
        }"""),
    ("""            Vector3 left = behind - transform.position;
            left.y = 0f;
            if (left.magnitude > 1f)
            {
                if (!movement.TryFindGround(behind, 1.5f, out Vector3 ground))
                {
                    // No ground behind the target (a wall at its back): the role goes.
                    flankers.Remove(target);
                    flankInPlace = false;
                    return false;
                }

                SetState(State.Chasing);
                flankInPlace = false;
                movement.MoveTo(ground);
                return true;
            }

            flankInPlace = true;
            return false;""", """            if (flankInPlace)
            {
                return false;
            }

            if (!movement.TryFindGround(behind, 1.5f, out Vector3 ground))
            {
                // No ground behind the target (a wall at its back): the role goes.
                flankers.Remove(target);
                flankRestUntil = now + FlankRest;
                return false;
            }

            // Arrived at the ground found there, not the point in the air behind the target.
            Vector3 left = ground - transform.position;
            left.y = 0f;
            if (left.magnitude > 1f)
            {
                SetState(State.Chasing);
                movement.MoveTo(ground);
                return true;
            }

            flankInPlace = true;
            return false;"""),
    ("""        flankInPlace = false;
        if (role.who != null || CurrentState == State.Attacking)
        {
            return false;
        }""", """        flankInPlace = false;
        if (role.who != null || CurrentState == State.Attacking || now < flankRestUntil)
        {
            return false;
        }"""),
    ("""            if (other == null || other.target != target || other.CurrentState == State.Dead || !other.CanFlank || other.IsRouted
                || other.CurrentState == State.Staggered)
            {
                continue;
            }""", """            if (other == null || other.target != target || other.CurrentState == State.Dead || !other.CanFlank || other.IsRouted
                || other.CurrentState == State.Staggered || now < other.flankRestUntil)
            {
                continue;
            }"""),
    ("""    private bool flankInPlace;
""", """    private bool flankInPlace;
    // Sitting out the flank for a while: it didn't get round in time, or there was no ground there.
    private float flankRestUntil = -1f;
    private const float FlankRest = 6f;
"""),
])
print("flank fix applied")

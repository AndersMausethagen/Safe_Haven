# Enemy AI review (2026-11-19)

The user asked: "do some research on enemy ai and how we can improve it. implement it to the best of your abilities."

This is a read-only review written for the agent who builds it. I changed no code, scenes or assets, opened no Unity and ran no tests.
Paths are under `Assets/_Project/Scripts/` unless they start with `Docs/`. `file:line` points at `test` today. Effort: **S** under half a
day, **M** one to two days, **L** more. *(from the code)* marks a behaviour I worked out by reading code, not one seen in Play.
*(unverified)* marks an outside claim with only a secondary source. ★ marks a quick win.

**What I read.** All of `AI/` (MonsterAI and its partials, AttackTurns, MonsterPositioning, MonsterPack, MonsterAttacks, MonsterAttack,
TargetPriority, MonsterTargeting, Perception), `Raids/RoamingSpawner`, `Raids/Events/RaidEventDirector`, `Raids/CreatureSleep`,
`Settlement/HomeAssault`, `Settlement/NightProwlers`, `Multiplayer/CreatureHost`, `Playtest/PlaytestLogger`. Data: every Monster
Definition, `Resources/CombatTechniqueSettings.asset`, `Resources/PerceptionSettings.asset`, the raids' roaming settings, `WolfPack.prefab`.
Docs: `EnemyCombatResearch.md` (Status, §0-3), `CombatFeelReview_2026-11-19.md`, `SystemsGapReview_2026-11-19.md` (§0, §14),
GDD §10.3, §10.5, §10.8 and §20.7. Telemetry: all 1,535 CSVs in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests`.

---

## 0. Verdict

The fight layer is rich: telegraph tiers, weighted strike choice that reads the foe, holds and combos, poise and reeling, guards,
backstab memory, turn-taking with an off-screen rule, flanking spread, routs, hurt moods, elites. **What is thin is everything between
the blows and around the fight:**

1. **Creatures that can strike stand at your feet and strike on cooldown.** A wolf in reach stops and bites every cooldown
   (`MonsterAttacks.HoldsGround`, `MonsterAttacks.cs:275-316`). Wolf bites are 56 of the 58 significant blows in the post-rework human
   runs, and wolves caused 8 of the 15 deaths in the long runs that look human.
2. **Who strikes next is first come, first served.** No queue, no fairness, and nothing about where the player faces.
3. **Group decisions are invisible.** A pack that breaks makes no sound. A spotter's alarm is a sound you can't answer. Halo's lesson
   was that players missed exactly this.
4. **Losing the player is a spin on the spot.** A searcher walks to the last place it saw you, turns at 70°/s for 6 s and walks home.
5. **Ranged creatures are turrets.** They stand still between shots and back straight away.
6. **Roamers arrive on a timer, not on a rhythm.** A new group is sent at the player every 20 s whether a fight is on or not.

The fixes are mostly small rules in code that already exists. Nothing below scales stats, and nothing copies the player's verbs.

---

## 1. How a creature decides today

| Concern | What happens | Where |
|---|---|---|
| State machine | Idle, Investigating, Chasing, Attacking, Staggered, Hopping, Returning, Dead; Engage is the fight step | `MonsterAI.cs:494-683` |
| Senses, spotting | Sight cone, close sense, hearing; "?" then "!" (scan 0.25 s). Spotting rings a bell, makes a 10 m noise (others grow suspicious) and engages its whole authored pack at once | `MonsterAI.Awareness.cs:34-67, 178-199`, `MonsterAI.Targets.cs:68-71` |
| Target choice | The player first while seen and reachable, else the nearest other (switch margin 1.5 m). A blow retargets only a creature with no target | `TargetPriority.cs:25-37`, `MonsterAI.Reactions.cs:299-310` |
| Strike choice | Highest priority ready action, or a weighted roll in its band; last used counts half, a match to what the foe does (guarding, backing off, shooting, at its flank) counts 3×; held 0.6 s | `MonsterAttacks.cs:115-237`, `MonsterAI.Tells.cs:132-171` |
| Turns at the player | At most 2 melee creatures closing in, starts 0.35 s apart (one static clock); the rest circle 0.8-1.6 s at 3.5 m or stand ready; off screen waits 0.6 s. Ranged: 2 at once. Heavyweights don't wait. Packs add 0.65 s spacing | `MonsterAI.Fighting.cs:303-403`, `AttackTurns.cs:24-32`, `MonsterPack.cs:99-107` |
| Positioning | Straight at the target; inside 12 m with others closing, each pushes its bearing off the others' by up to 80° × Flank Spread | `MonsterPositioning.cs:77-101`, `AttackTurns.cs:37-64` |
| Between blows | Stands and turns if any strike reaches (`AnyStrikeReaches`), else closes | `MonsterAI.Fighting.cs:199-207` |
| Ranged | Keep Distance: too close, retreat straight back; in band, stand still and face; no clear line, walk round to a random side, re-rolled every 2.5 s | `MonsterAI.Fighting.cs:179-196, 450-463`, `MonsterPositioning.cs:64-74` |
| Morale | Cowardly flee below a health share (with a cry). Authored packs (only `WolfPack` and `RootlingSwarm`) rout for 8 s when the leader falls or 60% are lost: no sound, no pose. A brutal kill makes others within 8 m falter 0.6 s | `MonsterAI.Fighting.cs:70-85, 141-152`, `MonsterPack.cs:34-65`, `MonsterAI.Tells.cs:177-230` |
| Lost target | 3 s out of sight: walk at 0.55 speed to where it last saw you, turn on the spot for 6 s, walk home (15 s cap) | `MonsterAI.Awareness.cs:69-85`, `MonsterAI.Investigation.cs:97-180` |
| Leash | Gives up past its leash (wolves 16 m) or chase range unless provoked (12 s after a blow); cut off from an unreachable target for 4 s, it walks home and ignores sightings for 10 s | `MonsterAI.cs:664-680`, `MonsterAI.Fighting.cs:21-51` |
| Roamers | From 60 s, a group every 20 s up to a cap that grows with stage and alarms; each sent to the player ±15 m, idle ones re-sent every 20 s | `Raids/RoamingSpawner.cs:56-89, 157-211, 276-299` |
| Home attacks | Keep a fight within reach, else the nearest building; wall-breakers go for walls. Prowlers stay out of light | `Settlement/HomeAssault.cs:14-84` |
| Co-op | The host thinks; a friend's copy follows poses, strikes and staggers. The pose's Hunting byte is full | `Multiplayer/CreatureHost.cs:47-84` |
| Cost | Far idle creatures sleep (85 m). Hunters call `NavMesh.CalculatePath` on scans; no profiler markers in `AI/` | `AI/MonsterTargeting.cs:92-107, 169-172` |

**Where it is thin or predictable** *(from the code)*:
- **Turn order is Update order.** `AttackTurns.Decide` (`AttackTurns.cs:24-32`) has no ranking. When the shared clock opens, the first
  creature whose Update runs takes the turn, and Unity runs Updates in a stable order, so the same creature tends to win. One that just
  struck competes as soon as its 1 s cooldown ends.
- **Where the player faces is ignored.** The player's guard covers 140° (`blockArc`). A creature behind the player takes a turn as
  readily as one in front, unless it is also off screen.
- **Waiting creatures ignore whoever hits them.** A wolf circling the player while a companion cuts it keeps circling.
- **An archer on unreachable ground gets free kills.** Each arrow retargets the walker (`MonsterAI.Reactions.cs:299-305`). It walks to
  the nearest point, waits 4 s, walks off, and the next arrow starts it again. The 10 s ignore stops only its senses.

---

## 2. What the logs say

Human runs, as identified in `SystemsGapReview_2026-11-19.md` §0: 11-14 (two), 11-15, and the prologue-only runs of 11-16 and 11-17.
`Hurt` rows (blows of 5% of health or more) exist only from 11-15.

| Measure | Value | Source |
|---|---|---|
| Who lands significant blows | 56 of 58 `WolfHamstringBite`, 2 `MistwingCharge` | Hurt rows, 11-15/16/17 |
| Deaths by killer (runs over 20 min since 10-17; not all confirmed human) | Wolves 8 (Forest-Bitten, Expedition, Packmate), Small Moss Troll 4, Blight Boar 1, Bog Spitter 1, unknown 1 | PlayerDied rows |
| Bites close together | 0.41-0.51 s gaps recur within fights (e.g. 11-17: 0.41, 0.42, 0.44, 0.49), the pattern `CombatFeelReview` §5 fixed with the flinch turn hold | Hurt rows, utc |
| Time to kill a wolf | Median 7.4 s (11-16, 156 health) and 8.2 s (11-17, 78 health); range 4.8-16.8 s | Kill rows |
| Who does the killing | 11-17: companions 69% of the damage and 16 of 20 kills; 11-16: 27% and 5 of 19 | Kill rows, "others" |
| How fights start | Sight 11, Struck 6, Search 4, Close 1; 2 ambushes | Combat `Spotted.*` rows |
| Raid length | 201-337 s; no human reached the second danger stage | RaidFinished rows |

**What the logs can't tell yet:** where a blow came from relative to the player's facing, whether its source was on screen, and how many
creatures were on the player at the time. Item 0 adds those three.

An agent probe on 11-18 (`20261118-190233`: 9 wolves on a player standing with fists, before the flinch turn hold) took 6 bites in
2.4 s. That is the ceiling of the turn rules, not human play.

---

## 3. What strong games do

| Source | Technique | What it means for Safe Haven (top-down) |
|---|---|---|
| *Kingdoms of Amalur*, GAIP 1 ch. 28 (Dawe) | Grid and attack capacity round the player. Creatures without a slot leave the circle, which "helped make it clear to the player which creatures were attacking"; slots are given up "immediately after launching an attack", so "no single creature could monopolize attack opportunities"; a better-placed creature can take a slot | Step away after striking (item 1), rank who goes next (item 2) |
| *DOOM* (2016), AI and Games | Attack tokens per attack type; better-placed demons take tokens so those in front don't stand idle | Best-placed goes first (item 2) |
| *The Last of Us*, GAIP 2 ch. 34 (McIntosh) | A Combat Coordinator hands out roles; for **Flanker** "the coordinator would only allow a single, ideal NPC". Dialogue "allowed the AI to communicate the decisions they make to the player" | One flanker, and it is announced (item 4) |
| *Days Gone*, GAIP Online 2021 ch. 12 | Squads act on **Confidence**. "If the player remains passive, the AI will try to pressure the player into action"; a player who flanks is often allowed "a small win" | Pressure passivity, not activity (item 4) |
| *F.E.A.R.*, GAIP 2 ch. 2 (Orkin) | "There is no point in expending significant effort implementing complex AI if the player doesn't notice it." AI should "talk to each other about what they are doing" | Calls and cries carry group state (items 3, 8) |
| *Halo*, GDC 2002 (Butcher, Griesemer), slides 6, 19 | Decisions shown by animation and dialogue; "If it isn't totally obvious, it's too subtle". Players missed Grunts fleeing when an Elite died until it was exaggerated. "Nothing breaks our AI faster than engaging it from the wrong range" | A loud rout (item 3); ranged creatures hold their range (item 6) |
| GAIP 2 ch. 27, "Looking for Trouble" | 2-3 s of "intuition" after losing sight; a narrow search at the last known position, then a broad sweep with spots handed out by a coordinator; any pull to the real position "should be quite subtle" | Search ahead along the heading, shared (item 5) |
| *Warframe*, GAIP Online 2021 ch. 7 (Brewer) | Intensity rises with damage taken (in proportion to health) and kills, and decays. At the peak, "we hold off spawning more enemies until the intensity dies down" | A director that times roamers (item 9) |
| *Left 4 Dead* (Booth 2009); Valve wiki | Build up, sustain peak, fade, relax. Sample script values: sustain peak 5-10 s, relax 1-5 *(snippet only)* | The same cycle (item 9) |
| *Elden Ring*, dataminer via PC Gamer | Enemies react to the animation of an item use. Zullie admits "some amount of conjecture" | Already ruled out here: never punish healing (§5) |
| *Shadow of Mordor* | Nemesis memory | Built: `BackstabMemory`, The Lost (2026-11-18). Not re-proposed |

---

## 4. Ranked improvements (fun gained per effort)

Order: 0 (instrumentation), then 1-9 by fun per effort. Quick wins: **0, 1, 2, 3, 6**. Every new field defaults to today's behaviour.

### 0. Log where blows come from ★ (S)

**What's wrong now.** The Hurt row says who hit and how hard (`Playtest/PlaytestLogger.cs:335-345`). It can't answer "hit from behind,
from off screen, by a crowd?"

**The change.** Append three fields to the Hurt row's detail: `from front|side|back` (front within ±70° of the player's facing, the guard's
half-arc; back beyond 110°); `offscreen` when the source was outside the camera's view (make `MonsterAI.OffScreen` public as
`IsOffScreenForLocalPlayer`); and `crowd N`, the creatures engaging the player within 8 m. Add Combat rows for the new behaviours below
(`BreakAway`, `Flank`, `Rout`, `Alarm`, `SearchPoint`, `Intensity.Peak`, `Intensity.Relax`).

**How to verify.** A test through `PlaytestLogger`: a blow from behind writes `from back`. Fields go at the end so old parsers still work.

### 1. Bite and break away ★ (S-M)

**What's wrong now.** A creature whose strike reaches stops and strikes again on cooldown (`MonsterAI.Fighting.cs:199-203`,
`MonsterAttacks.cs:275-316`). The wolf's bite has a 1 s cooldown, a 0.45 s wind-up and 1.53 m reach, so two wolves stand at the player's
legs and alternate. That is the 56/58 above.

**The change.** New Monster Attack fields under "Melee: after the blow": **Break Away** (metres, 0 = off) and **Break Away Seconds**
(range). When a strike with Break Away ends, landed or not:
- The creature goes to `AttackTurns.BreakAwaySpot`: Break Away metres from the target, 50-70° round from where it struck, on the side
  away from the nearest other attacker (random when alone).
- It runs there facing away, then turns and holds a ready stance (`MarkWaiting`) for its seconds.
- It may not start a melee strike before its seconds end, so the others get their turns (Amalur: slots given up "immediately after
  launching an attack"). Today it is free again after its 1 s cooldown and is still standing in reach. A pounce or charge band it now
  stands in is allowed: at tier II the wolf's pounce (3.5-10 m, weight 0.6) comes into play by itself.

Data:

| Creature | Break Away | Seconds |
|---|---|---|
| Forest-Bitten Wolf, bite | 3.5 m | 0.8-1.4 s |
| Rootling, grasp | 2.5 m | 0.5-0.9 s |
| Troll Runt, melee | 3 m | 0.6-1.0 s |

Boars, Draug, trolls and bosses: unchanged.

**Why.** Hit-and-run is what "fast melee pressure" (GDD §10.5) should read like, and a wolf turning away is an opening. The Mistwing
already lands and retreats.

**How to build.**
- `MonsterAttack`: the two fields.
- `MonsterAI.Fighting`: set `breakingAwayUntil` where a strike ends (`:264-287`), and handle it in `Engage` the way `circling` is
  handled (`:119-130`).
- `AttackTurns.BreakAwaySpot`: pure.
- Movement: the run, then `movement.Strafe` facing the target. The ready stance and side-step blend already exist.

**Co-op.** The host decides. The friend's copy follows the poses, and the ready stance travels as `WaitingFlag`. No new bits.

**Risk.** Tier I wolves get easier: sustained pack damage drops by roughly a third *(from the code)*. If the playtest finds wolves soft,
raise the bite's damage in data (18 → 20). It shows in the bestiary, which fits "show, don't scale".

**Verify.**
- `StrikeRulesTests`: BreakAwaySpot lies at the distance, on the side away from another attacker. Include a canary: a spot equal to the
  target's position fails.
- Play probe: three wolves on an invulnerable, standing player for 60 s. Expect about 30-40% fewer Hurt rows than today. No wolf should
  stay within 1.6 m for more than 1 s after its bite, and each wolf should get bites.

### 2. Fair turns: waited longest, in front, goes first ★ (S)

**What's wrong now.** See §1: first come in Update order; the striker can go again; facing ignored.

**The change.** `AttackTurns.Rank` (pure) scores each creature that is ready, in reach and wants a turn at the same target:
- **+ seconds waited** since it became ready;
- **+0.6** inside the target's guard arc (±70° of its facing; `blockArc`);
- **+0.3** on screen;
- **−1.0** if it made the last strike at this target.

Only the top-ranked ready creature may Strike. Keep the 2-at-once limit, the 0.35 s spacing and the off-screen wait. A creature that
comes into reach in front can take the turn from one still turning, as Doom and Amalur do. Distance breaks ties.

**How to build.**
- A small `AttackTurnBoard` (static per target, cleared on SubsystemRegistration like `nextTurnAt`): a dictionary of
  `readySince` per creature.
- `HoldsBack` (`MonsterAI.Fighting.cs:303-377`) asks it.
- New `CombatTechniqueSettings` fields: Front Arc Bonus (0.6), Last Striker Penalty (1.0).
- A friend's body (`FriendHitbox`) supplies its own facing.
- Heavyweights still don't wait.

**Co-op.** Host only. Nothing new on the wire.

**Risk.** Slightly easier, because blows come from where the guard works. That is the same principle as the approved off-screen rule.

**Verify.**
- `StrikeRulesTests`: a waiting creature outranks the last striker, and front beats back at equal waits. Include a canary that fails if
  the rank is ignored.
- Play probe: four Draugs, tier I, around an invulnerable player for 60 s. Add the instance id to the strike's Combat row: each Draug's
  share should be within ±30% of the mean.

### 3. Show the pack's state: the rout, the leader ★ (S)

**What's wrong now.** `MonsterPack.MemberFell` routs the rest (`MonsterPack.cs:57-63`), but `Rout` (`MonsterAI.Fighting.cs:70-85`) and
the routed branch of `Engage` (`:107-112`) make no sound and change no pose. Only the cowardly cry (`NoteFleeing`, `MonsterAI.Answers.cs:602`).
The howler that breaks the pack looks like the others.

**The change.**
- **On Rout,** each member cries (`CryOut`, staggered 0.15 s apart) and drops into a cowed pose for the flight: body 10% lower, tail
  and head down, 0.8× turn speed.
- Its awareness mark shows a grey down-chevron.
- One alert line, "The pack breaks!", once per pack.
- **The leader** (`MonsterPack.leader`) is 10% bigger (`CreatureLook`) with a paler ruff.
- The wolf's journal advice gains "Bring down the one that howls and the rest will run."

**Why.** Halo and F.E.A.R. (§3).

**Co-op.** A `Routed` bit. The Hunting byte is full, so add one **Mood** byte to `CreatureHost.SentPose` for this and items 4, 5 and 8:
bits Routed, Flanking, Calling and Searching. This raises `MultiplayerSettings.WireFormat` by one, with a
`CoopMessageTests` round trip.

**Verify.** Play probe: kill a WolfPack's leader. Expect yelps, the pose, one line and a `Rout` row. `CoopMessageTests` for the byte.

### 4. The flanker: packs pressure a passive player (M)

**What's wrong now.** Flank Spread spreads the approach (`AttackTurns.cs:37-64`), but nothing answers a player who turtles or plants
their feet. Item 2 also makes back strikes rarer, so turtling needs a counter.

**The change.** At most one **Flanker** per target. The role exists only while the target is passive:
- **Passive** means guard held ≥1.2 s (`PlayerGuard.GuardedFor`, the read `ReadFoe` uses at `MonsterAI.Tells.cs:157`), or moved less
  than 0.5 m in the last 2 s, while two or more creatures with Flanks hunt it.
- **Who flanks:** the creature with the smallest walk to the back half (beyond ±70° of facing) that isn't striking. It holds the role
  for up to 4 s.
- **What it does:** it walks to 3.5 m behind the target and growls (Monster Definition › Flank Sound; the wolf's own growl). If it is
  off frame, the edge chevron shows. While it holds the role it ranks as if in front (item 2), so it gets the next turn.
- **What ends it:** the target moves 1.5 m, dodges or turns to face it. It then rejoins the ring.

Data: Monster Definition › Flanks (bool) on wolves, Draug and the Lost.

**Why.** Days Gone, TLOU and F.E.A.R. (§3). Moving and dodging stay the answer.

**How to build.**
- `AttackTurns.PickFlanker` (pure).
- A flanker slot on item 2's `AttackTurnBoard`.
- `MonsterAI.Fighting`: a Flank branch before `HoldsBack`.
- `CombatTally` writes a `Flank` row.

**Co-op.** Host. The Flanking bit plays the growl on a friend's copy.

**Risk.** Harder for players who block a lot (4 parries and 6 blocks across the human runs suggest few do). Decision D1: from tier I,
or tier II as "creatures learn".

**Verify.**
- `StrikeRulesTests` for PickFlanker: one at most, and none when the target is active.
- Play probe: an invulnerable player guards facing north with three wolves round them; within 3 s one goes behind and strikes, and a
  `Flank` row is written. Walking in circles instead: no Flank rows.

### 5. A search that hunts (M)

**What's wrong now.** `lastKnownTargetPosition` updates only while seen (`MonsterAI.Awareness.cs:72-76`). The searcher walks there, turns
on the spot for 6 s (`MonsterAI.Investigation.cs:162-179`) and goes home. A pack all stack on one point.

**The change.**
1. **Heading guess.** On losing track, the first point is the last known position plus the target's last velocity × 1.5 s, at most 8 m
   further, snapped to the NavMesh and kept inside the leash (`InvestigationSpot`). That is where you were going, not where you are.
2. **Sweep.** After a 2 s look at a point, take up to two more, 5-9 m further on in a ±50° cone round the heading. Each must be reachable
   and inside the leash.
3. **Share.** A `SearchBoard` keyed by pack (or a 6 m bucket of the stimulus) hands each searcher a different point. A third and later
   searcher stands and looks along the heading.
4. **Time.** Lost-target searches get 20 s (Perception Settings › Lost Search Seconds); a sound's search keeps 15 s.

**Why.** GAIP 2 ch. 27 (§3), within GDD §10.8's rule that creatures notice and never simply know.

**Co-op.** Host. The Searching bit tints the friend's "?" grey.

**Verify.**
- `PerceptionTests`: the search points lie ahead along the heading, not behind. Canary: zero velocity gives the last known position.
- Play probe: be spotted, break line of sight behind a rock and walk on. The wolf should come round the rock your way and visit two or
  more points; `SearchPoint` rows; `LostTrackOfPlayer` still fires.

### 6. Ranged creatures move between shots ★ (S-M)

**What's wrong now.** Keep Distance backs straight off (`MonsterAI.Fighting.cs:181-184`, `:450-463`): against a rock it simply stops.
In band it stands still (`:189-195`).

**The change.**
- **Relocate once after each ranged action.** Score six spots on the ring at Preferred Distance, within ±60° of its bearing. Reject any
  without a clear shot (`senses.HasClearShotTo`, at most three tests per relocation), any wet spot (`RaidSpots.IsDryEnough`) and any
  beyond the leash. Score +1 per metre higher (up to 2) and −2 within 3 m of another shooter. Walk to the best at 0.8 of its speed,
  facing the target.
- **Retreat.** Try back-left 45°, back-right 45°, then straight back.
- **Cornered.** If all three fail for 1.5 s, it stops trying and its close action gets first pick. Every Keep Distance creature has
  one except the Marsh Light and the Mistwing; those two just stop.

**Why.** Halo on engagement range (§3); spot scoring after GAIP 1 ch. 26 (title only). A shooter that moves can be pinned against terrain.

**How to build.** `MonsterPositioning.Relocate` and a pure `ScoreSpot`; `Retreat` gets the three tries. Never faster than 0.8 of its
speed, so a chasing player closes.

**Co-op.** Host only, poses.

**Verify.** A `ScoreSpot` test, with higher, dry and in-sight spots winning. Play probe: a Bog Spitter in the Moss Marsh moves after each
spit; walked into a rock wall, it slides sideways and then shoves.

### 7. Waiting creatures answer the companion who hits them (S, decision D2)

**What's wrong now.** A blow retargets only a creature without a target (`MonsterAI.Reactions.cs:299-305`), and TargetPriority pulls
everyone back to the player. In the 11-17 run companions did 69% of the damage to creatures that kept circling the player.

**The change.** A creature waiting its turn (circling, standing ready, breaking away) that takes a blow from someone other than the
player holds a **grudge** for 4 s. It targets that attacker, and player priority doesn't pull it back until the grudge ends or the
attacker is more than 6 m away. Only one grudge-holder per companion at a time.

**Why.** Doom: demons don't stand idle. Companions get fights of their own and the player gets fewer blows. `TargetPriority.Decide`
gains a `grudge` input.

**Risk.** Companions take more hits. The companion death rules are the agent's own unplayed choice.

**Verify.** A `CompanionTests` case for the grudge. Play probe: the prologue camp waves with companions. Player Hurt rows should drop and
companion hurt should rise.

### 8. Raise the alarm as an action (M, decision D3)

**What's wrong now.** Spotting tells the pack and nearby creatures at once, with a bell (`MonsterAI.Awareness.cs:178-199`,
`MonsterAI.Targets.cs:68-71`). There is nothing to answer.

**The change.** A Howl action with a new **On Spotted** flag (the Alerts Others field already exists, `MonsterAttack.cs:137-138`). The
first of a group to spot the player plays it before engaging:
- **The call:** interruptible (blue), 0.8 s, the Call state. One per group per 20 s.
- **If it completes,** every same-side creature within Buff Radius (18 m) engages.
- **If it is broken,** only today's 10 m suspicion spreads.

Give it to the wolf leader (its howl), the Draug (its Howl entry) and the Moss Shaman.

**Why.** It turns an alarm into a decision (F.E.A.R. and Halo: show group decisions). Silent takedowns get a target.

**Co-op.** It is a strike, so it already replicates. The Calling bit marks it.

**Risk.** Stealth balance moves both ways: easier if broken, a wider radius if not.

**Verify.** A rules test: a completed call engages, a broken one leaves others Investigating. Play probe: an `Alarm` row.

### 9. A pacing director for roamers (M, decision D4)

**What's wrong now.** Roamers come on a timer and are sent at the player regardless of a fight (`Raids/RoamingSpawner.cs:80-89`,
`:199-211`). The stage reveal (b3c008ab) now asks players to stay into stage 2+, where the caps rise.

**The change.** `RaidIntensity` (plain C#, owned by RaidDirector) tracks a score:
- **Rises:** +100 × damage ÷ max health for each blow on the player; +8 per kill; +2 per second per creature hunting the player's side
  (at most +10 per second).
- **Falls:** −8 per second after 4 s of none of these.

Phases:
- **Build** below 100.
- **Peak** at 100, held 6 s.
- **Fade:** no roamer spawn or hunt until the score is below 25.
- **Relax:** 15-30 s, ended early by 40 m of travel. Then Build again.

`TrySpawn` and `Hunt` run only in Build. **Not gated:** events, alarms (`SpawnWave`), stage spawns, Forest Annoyance and extraction,
because they are announced. Caps and budgets are unchanged. Entering Relax after a Peak shows "The woods go quiet" (the combat music
already ends with the fight, `Audio/SceneAudio.cs`).

**Why.** Warframe and Left 4 Dead: hold spawns after a peak, then give a breather.

**Co-op.** Host only. It counts creatures hunting any player's side, so a friend's fight counts too.

**Risk.** It is pacing, not stats, and it is shown. Still, it is a hidden-ish assist: decision D4.

**Verify.**
- Pure `RaidIntensityTests`: the timeline Build → Peak → Fade → Relax → Build.
- `RoamingAndPoolTests`: no spawn in Fade.
- Play probe: log rows `Intensity.Peak` then `Intensity.Relax`, with no roamer during Relax.

### Lower priority

- **Punish a committed whiff, tier II+ (S, decision D5).** Add `FoeDoing.Spent`: the player is in the recovery of a heavy, rush or art
  that landed nothing. A creature standing ready within reach of its quick strike may start now, ignoring the 0.35 s spacing but not
  the 2-at-once limit. "Creatures learn" with the tier, as the pounce does. Never on healing.
- **Out of reach isn't free (S-M, decision D6).** While cut off from someone who hurt it in the last 12 s, a creature with no ranged
  action moves out of that shooter's sight (the nearest of six points within 10 m that isn't visible from the shooter). It waits there,
  mending 3% a second shown on its bar, until the shooter comes down or 20 s pass. Fixes the arrow loop in §1.
- **AI cost (S).** Measure first: frame time is terrain and tree shadows today. Add `ProfilerMarker`s to `MonsterAI.Update`, `Sense`,
  `TryFindBetter` and `CanReach`. Cache `CanReach` per target for 1 s. Measure a stage-3 raid with 20+ awake creatures before going
  further.
- **Home attacks.** Breach roles (Runner, Breaker, Seeker, Hexer) are `SystemsGapReview` §14, approved after the playtest; not
  repeated here. Items 1, 2, 4 and 6 apply to breach attackers by themselves. The rout doesn't, because `Relentless` attackers stand.

---

## 5. What not to do

- **React to healing.** `EnemyCombatResearch.md` §3 settled "never to healing", after Elden Ring's complaints.
- **More feints, holds or shorter wind-ups.** `CombatFeelReview` §4: every unbreakable melee strike winds up for 0.45 s or more; holds
  are deliberately rare.
- **Raise Attackers At Once or the counts per tier.** Amalur scales capacity with difficulty, but here it would be a hidden scale. Tiers
  change verbs (`fromTier`), not crowd size.
- **Player verbs on creatures.** No rolls, no player clips. Breaking away and flanking use runs, strafes and the ready stance they
  already own.
- **Omniscient raid creatures.** Searching follows the heading, never the true position. Breach attackers and hunt waves still always
  know (GDD §10.8).
- **A planner or utility rewrite.** GDD §20.7 asks for readable state machines; every item fits the current one.
- **Kiting faster than the player,** or ranged creatures retreating forever.
- **Nemesis memory again.** The Lost and BackstabMemory cover it.

## 6. Decisions for the user

- **D1** The flanker: from tier I, or tier II ("creatures learn")? Wolves only, or the Draug and the Lost too? *Recommended: tier I
  wolves; Draug and the Lost from tier II.*
- **D2** Companion grudges: companions take more hits. *Recommended: yes, with the 4 s limit.*
- **D3** Alarms as an interruptible call. *Recommended: yes, wolves and the Shaman first.*
- **D4** The pacing director, shown by "The woods go quiet". *Recommended: yes; it never adds spawns.*
- **D5** Whiff punish from tier II. *Recommended: yes.*
- **D6** Health mended out of reach, shown on the bar. *Recommended: yes.*

**Build order:** 0, 1, 2, 3 (one batch; the Mood byte comes with 3), then 6, 5, 4, then the decisions. Re-run `StrikeRulesTests`,
`MonsterBehaviorTests`, `PerceptionTests`, `CompanionTests`, `CoopMessageTests` and the static-state guards. Probes follow the raid probe
rules: invulnerable, clock extended, no `sh_save`.

---

## Sources

Read for this review (primary):
- Dawe, "Beyond the Kung-Fu Circle", Game AI Pro 1 ch. 28:
  http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf
- "Looking for Trouble: Making NPCs Search Realistically", Game AI Pro 2 ch. 27:
  http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter27_Looking_for_Trouble_Making_NPCs_Search_Realistically.pdf
- McIntosh, "Human Enemy AI in The Last of Us", Game AI Pro 2 ch. 34:
  http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter34_Human_Enemy_AI_in_The_Last_of_Us.pdf
- Orkin, "Combat Dialogue in FEAR: The Illusion of Communication", Game AI Pro 2 ch. 2:
  http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter02_Combat_Dialogue_in_FEAR_The_Illusion_of_Communication.pdf
- Brewer, "Managing Pacing in Procedural Levels in Warframe", Game AI Pro Online 2021 ch. 7:
  http://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter07_Managing_Pacing_in_Procedural_Levels_in_Warframe.pdf
- "Squad Coordination in Days Gone", Game AI Pro Online 2021 ch. 12:
  http://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter12_Squad_Coordination_in_Days_Gone.pdf
- Butcher and Griesemer, "The Illusion of Intelligence", GDC 2002, slides 6, 18 and 19:
  https://halo.bungie.org/misc/gdc.2002.haloai/talk.html?page=6 (and `?page=18`, `?page=19`)

Secondary, or title or snippet only:
- AI and Games, "Cyber Demons: The AI of DOOM (2016)": https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-
- Campbell, "Bringing Hell to Life", GDC 2017 (listing only): https://gdcvault.com/play/1024186/Bringing-Hell-to-Life-AI
- Booth, "The AI Systems of Left 4 Dead" (opening pages only):
  https://cdn.fastly.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf
- Valve wiki, DirectorOptions (search snippet): https://developer.valvesoftware.com/wiki/DirectorOptions
- Tactical Position Selection, Game AI Pro 1 ch. 26 (title only):
  http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter26_Tactical_Position_Selection.pdf
- PC Gamer on the Elden Ring dataminer:
  https://www.pcgamer.com/elden-ring-dataminer-explains-why-enemies-smack-you-when-you-reach-for-a-potion
- Envato Tuts+, "Battle Circle AI" *(unverified)*:
  https://code.tutsplus.com/battle-circle-ai-let-your-player-feel-like-theyre-fighting-lots-of-enemies--gamedev-13535t

Not found: a primary source for Dark Souls' leash and regeneration, Arkham's turn-taking, or Monster Hunter's attack tables (only a
modding wiki: https://github.com/Ezekial711/MonsterHunterWorldModding/wiki/A-Brief-Survey-of-Monster-AI-Files). Nothing here relies on
them.

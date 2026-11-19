# Combat feel review (2026-11-19)

The user asked: "do a combat feedback check. what can we do better? how can we make combat feel better?"

This is a read-only review. I changed no code or data, did not open Unity and ran no tests. Paths are relative to
`Assets/_Project/` unless they start with `Docs/` or `Tools/`. `file:line` points at the `test` branch today. Effort uses the project's
sizes: **S** under half a day, **M** one to two days, **L** more. Numbers marked *(from the code)* were worked out by following the code
path and need a look in Play. Numbers from other games are marked *(unverified)* when the only source is a fan wiki or a forum.

**What I read:** `Docs/Combat.md`, `Docs/CombatResearch.md` (Status, §0, §2), `Docs/EnemyCombatResearch.md` (Status, §0-3),
`Docs/LookAndFeelResearch_2026-11-18.md` (§1, 6-8, 11), `Docs/GameDesignResearch_2026-11-17.md` (§0-1),
`Docs/AgentDecisions_2026-11-17.md` and `_2026-11-18.md` (combat parts), `Docs/TODO.md` (Combat rework), GDD §10.1 and §10.3, and
ARCHITECTURE.md › Combat feel and Combat technique. Code: `CombatFeedback`, `CameraShake`, `HitReaction`, `AttackTelegraph`,
`PlayerCombat.Swings`/`.Reactions`, `PlayerDodge`, `PlayerGuard`, `MonsterAI.Fighting`, `AttackTurns`, `DamageNumber(Spawner)`,
`CombatScreenEffects`, `HeldItemView`, `ForcedMove`, `EliteCreature`. Data: `Resources/CombatFeel.asset`,
`Resources/CombatTechniqueSettings.asset`, `Data/Player/PlayerMovementSettings.asset`, `Data/Camera/PlayerCameraSettings.asset`, all
173 attack assets, every monster's attack list, every weapon's sounds. Logs: the three human runs since the 2026-11-15 rework.

---

## 0. Verdict

Combat is not short of systems, and most of the design is right: real telegraph tiers (solid, dashed and hatched patterns, so colour isn't
the only cue), an opening ring, reeling embers, interrupts, weighted strike choice, per-family chains and signatures, hit stop and shake
scaled by the attack's Impact, blood, ragdolls, a bounded buffer and dodge cancels. The wind-ups are fair. Every melee strike under 0.45 s
is one any blow breaks off (shoves, the tongue, grasps, bites), so a quick hit answers it. The one exception is the tier II wolf's
pounce, a 0.4 s charge whose contact comes later, at the end of its run.

What hurts the feel is in the tuning at the seams, not in missing features:

1. **Hit stop stacks per target.** A sweep through a pack freezes the game once for every body it reaches, and the stops on big kills
   go past what fighting games use for their heaviest blows.
2. **The parry cue can't be reacted to.** The glint and the pale fill appear when the 0.15 s window opens, which is quicker than
   anyone can react (180-250 ms to a visual cue). They confirm the timing but can't prompt it.
3. **Blows taken all weigh the same.** Every monster attack has Impact 1, so a wolf's nip and a troll's stomp land alike, and being
   nipped shakes the camera harder than any kill the player makes.
4. **Knockback ignores weight.** A dagger critical shoves an Old Moss Troll 0.8 m, and a hammer's heavy blow shoves a rootling 0.35 m.
5. **Packs can lock the player.** Two wolves' bites can land 0.35 s apart, inside the 0.45 s flinch during which you can't dodge or
   guard. In the human runs, 18% of the significant hits came within 0.6 s of the one before.

Fix those five (all S) and the existing machinery reads as weight and fairness. After them come the success moments, which are weaker
than plain hits (a parry freezes the world less than a jab), and sound identity per weapon family.

---

## 1. What the logs and numbers say

### 1.1 Human playtest runs since the rework

`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests`. These are the only post-rework runs with more than 50 Combat rows.
20261116-171414 ends at the time `Player-prev.log` was written, so it is a build run. 20261115-184337 used the F1 cheat menu (13 Give
rows) and swung six cheated weapons at the air: those weapons show Landed 0, so its whiff count is inflated. 20261117-193730 moves at
a human pace through the prologue, but I couldn't confirm it.

| Run | Weapon | Light / Heavy / Rush / Art | Landed / Whiffed | Parry | Block | Dodge | Perfect dodge | Interrupt | Flinch / Lost swing | Hurt |
|---|---|---|---|---|---|---|---|---|---|---|
| 11-15 (49 min, Haven + prologue + raid) | Worn Sword | 322 / 51 / 35 / 40 | 210 / 208 | 4 | 2 | 27 | 0 | 35 | 15 / 9 | 39 |
| 11-16 (10 min, prologue, build) | Worn Sword | 85 / 0 / 8 / 11 | 102 / 26 | 0 | 2 | 8 | 1 | 14 | 13 / 8 | 25 |
| 11-17 (9 min, prologue) | Fire-Hardened Spear | 65 / 18 / 10 / 2 | 45 / 33 | 0 | 2 | 5 | 0 | 14 | 14 / 10 | 23 |

- **Hurt rows** (blows of 5% of health or more): **56 of 58 are `WolfHamstringBite`** (14 damage each); the other 2 are Mistwing charges.
- **Gaps between significant hits:** 10 of 55 were under 0.6 s (2/18, 4/18, 4/19 per run), almost all two wolves biting in turn.
- **Defence is almost unused:** 4 parries, 1 perfect dodge and 40 dodges against 87 times hurt. Interrupts (63) are the most-used
  technique after landing a blow. That is what the wolves' blue, dashed warning asks for, so it isn't a fault. It does mean the
  interrupt's feedback is the one heard most.
- No player deaths in these runs.

### 1.2 Feel values today (from the data, worked through the code)

**Hit stop** (`CombatFeedback.cs:244-258`: base × technique extra × the attack's Impact, at time scale 0.05):

| Blow | Formula | Stop (ms) |
|---|---|---|
| Dagger stab (Impact 0.65) | 70 × 0.65 | 46 |
| Sword slash (1.0) | 70 | 70 |
| Sword finisher (1.15; strong, ×1.25) | 100 × 1.25 × 1.15 | 144 |
| Sword heavy (1.3; ×1.5) | 100 × 1.5 × 1.3 | 195 |
| Hammer heavy (1.8) | 100 × 1.5 × 1.8 | 270 |
| Hammer charged (2.2) | 100 × 1.5 × 2.2 | 330 |
| Hammer art (2.0; ×1.75) | 100 × 1.75 × 2.0 | 350 |
| **Kill** with the hammer art | 130 × 1.75 × 2.0 | **455**, for each target |
| Any blow taken, from any monster | 70 | 70 |
| **Parry** | `SlowTime(0.4, 0.09)`: about 54 ms of world time lost | less than a jab (about 66 ms) |

For comparison, Street Fighter V uses 8, 12 and 15 frames (about 133, 200 and 250 ms) for light, medium and heavy blows, and Smash caps
hitlag at 30 frames (`Docs/CombatResearch.md` §2.9).

**Camera shake** (`CameraShake.cs`: offset = 0.35 m × trauma², roll 2.5° × trauma²). At the fight camera's 16 m (12-20 m at zoom 0.5,
`PlayerCameraSettings.asset:63-65`) and FOV 40, the screen is about 11.6 m tall, so at 1080p:

| Trigger | Trauma | Offset | Roll (at the screen edge) |
|---|---|---|---|
| The player's light hit | 0.15 | 0.7 px | about 1 px |
| The player's critical | 0.30 | 2.9 px | about 4 px |
| The player's kill | 0.45 | 6.6 px | about 9 px |
| **Any blow taken (a wolf's nip)** | **0.55** | **9.8 px** | **about 13 px** |
| The player's hammer heavy | 0.81 | 21 px | about 27 px |

**Knockback** (`HitReaction.cs:76`): 0.35 m, or 0.8 m on a critical or a kill, for every target. No prefab in the project sets
`knockbackScale` (searched across every `.prefab`), so trolls and great beasts are pushed as far as a rootling.

---

## 2. Ranked improvements (feel gained per effort)

**Quick wins** (S, low risk, mostly one file each): 1, 2, 3, 4, 5, 6, 7 and 13. Each item names what was already decided and how it
leaves that alone.

---

### 1. One hit stop per swing, and a ceiling on it ★ quick win

**What is wrong now.** `CombatFeedback.OnAnyDamaged` runs once for every body a swing reaches (`CombatFeedback.cs:244-275`). A
sweep queues its targets 0-0.18 s apart in game time (`PlayerCombat.Swings.cs:624-647`, `StrikeRules.SweepSeconds` 0.09 s per 180°).
Each one lands after the previous stop has ended, so each starts a new stop and adds more trauma. A greataxe sweep (Impact 1.3) through
three rootlings is about 3 × 91 ms of near-freeze. If they die, it is 3 × 169 ms, so half a second of strobing. The hammer art killing four
is up to 4 × 455 ms. Trauma adds up too: four hammer-heavy hits saturate it at 1, which is 0.35 m of offset and 2.5° of roll. *(From
the code.)* The same stop also plays for blocks the player isn't part of: the Blocked case (`:378-383`) doesn't check who blocked.

**The change.**
- The first target of a swing gets its full stop. Each further target **of the same swing** extends that stop by 15% instead of
  starting a new one, up to ×1.45.
- **Ceiling:** 0.25 s for a blow and 0.30 s for a kill (Street Fighter V's heavy is about 250 ms). The boss stop (0.45 s) and a
  boss's topple (×2, `:253-256`) are exempt.
- Kill stop = `HitStopOnKill × min(extra, 1.25) × Impact`, so arts and backstabs don't multiply a kill twice.
- Shake per swing: full trauma for the first target, +15% of it for each further one.
- `HitShake` still plays on every struck body (each one shudders), but for the merged length.
- The Blocked and Parried cases only stop time when the player is the victim or the attacker.

**Why.** A developer on itch.io decided against a full stop on every hit for exactly this reason: "it would look weird if i swiped at
lets say 5 enemies and it hitstops 5 times" (itch.io post 7434658). The stop exists to give a hit weight, not to stretch time in a
crowd. Hades and Diablo IV clear crowds with short stops.

**How to build.** `CombatFeedback`: keep `swingKey` (= `playerCombat.SwingStartTime`, already public, `:130`), `swingStopBegan`,
`swingTargets` and `swingFirstStop`. In the player-attacker branch, if the key matches, compute
`min(first × (1 + 0.15 × n), cap)` and set `hitStopUntil = max(hitStopUntil, swingStopBegan + that)` rather than calling
`HitStop(stop)`. Thrown weapons and arrows keep a stop per hit, since they land seconds apart. A cast's fan of bolts shares the swing
key, so it merges. New fields in `CombatFeelSettings`: Extra Target Share (0.15), Most Per Swing (1.45), Stop Ceiling (0.25),
Kill Stop Ceiling (0.30).

**Effort:** S. **Risk to what is decided:** none. Impact still orders the weapons, and a lone target feels as it does now.
**Verify in Play:** at the Haven's training dummy, then on three rootlings. A small probe coroutine logs `Time.timeScale` each frame
while a greataxe sweep and then the hammer art land. Expected: one continuous dip per swing, no dip longer than 0.30 s, and three or
fewer dips in total for a sword chain into a pack.

---

### 2. A parry cue you can act on ★ quick win

**What is wrong now.** The glint and the pale fill appear only when the window opens, 0.15 s before contact
(`AttackTelegraph.cs:213-227` and `:245-249`; `parryWindow: 0.15`, `CombatTechniqueSettings.asset:92`). Simple visual reaction time
is about 180-250 ms, and to sound about 140-160 ms (Encyclopedia.com, Reaction Time; PMC4456887). So a press made in answer to
the cue lands after the hit. The cue only rewards a player who has already learned the animation. Logs: 4 parries in about 70 minutes
of play, 0 in both prologue runs (§1.1).

**The change** (the window stays 0.15 s):
- **A parry line** on the fan: a thin arc in the parry colour (alpha 0.35) at the radius the fill reaches mid-window, `1 − (W/2) /
  HitDelay` (0.83 of the reach for a wolf's 0.45 s bite, 0.90 for a Draug's 0.75 s cleave). The player sees the fill front coming toward
  the line and presses as it crosses. Predicting when something moving will reach a mark (coincidence-anticipation timing) is a
  different and much more accurate skill than reacting (PubMed 20681339). Rhythm games and quick-time rings work this way.
- **A lead cue:** move the glint, and add a short bright "ting", to `HitDelay − W − ParryCueLead`, with Parry Cue Lead 0.12 s
  (0.27 s before contact). A player who reacts to the sound in 140-160 ms then presses inside the window. The pale fill inside the
  window stays as the confirmation.
- Only for strikes that can be parried (`cuesParry`, not Unblockable, `HitDelay > W + lead`) and only for strikes aimed at the local
  player, so a crowd doesn't fill with lines and tings. With the Parry and Dodge Timing assist, `W` is already scaled by
  `GameOptions.TimingShare`.
- **Holds must still bait.** A strike that can hold (Draug cleave 30%, Small Moss Troll shove 25%) never cues before its hold point
  (60% of the fill). The strike's clock already stops during a hold (`AttackTelegraph.cs:191-194`).

**Why.** GDD §10.3 asks for distinguishable anticipation, active and recovery windows. Sekiro's deflect window is often given as about
12 frames (200 ms) *(unverified, fan wiki)* and is read from the attack's own animation, which this camera height shrinks. A predictive
mark brings that back from above.

**How to build.** `AttackTelegraph`: a fourth part, "Parry Line", made like Heavy Edge (`CreatePart` plus `GroundMarks.BandFor(feel,
arc, 0.04)` and its `Shape`) and scaled to the radius above. Show it on `OnAttackStarted` when the conditions hold and hide it after the
window. In `UpdateParryCue`, fire the glint and the new `CombatFeel` › Parry Cue Sound at the lead time. Sound: a new `parry_cue`
recipe in `Tools/Audio/combat.py`: two short iron partials (about 2.4 and 3.6 kHz, 70 ms decay), 12 dB under Parry. Make one take
only: cues stay identical on purpose (`Docs/AgentDecisions_2026-11-18.md` batch 10).

**Effort:** S. **Risk to what is decided:** parrying gets easier to time, but not more rewarding. The economy (18 Focus, heavyweights
losing 40% poise) is untouched. Watch the Parry share in the Combat rows. If parry turns into the whole loop again, shorten Parry Cue
Lead rather than taking the line away. **Verify in Play:** a wolf and a Draug at the Haven. Look at the line on flat ground and on a
slope from the game camera. Count parries in ten tries with the line and lead cue, then with both off.

---

### 3. Blows taken carry their weight ★ quick win (mostly data)

**What is wrong now.**
- All 80-odd monster attack assets have **Impact 1** (the attack table: `impact: 1` on every non-player attack).
- The player's hurt stop and shake are flat (`CombatFeedback.cs:229-231`: 70 ms and trauma 0.55, `CombatFeel.asset:18,24`). A
  rootling's grasp and a Stone Troll's stomp land the same, and a nip shakes about 10-13 px, more than the player's own kill (about
  7-9 px).
- No monster blow moves the player except the Shield Rush and the drags.

**The change.**
- **Code:** `impact = damage.Attack?.Impact ?? 1`, then `HitStop(HitStopWhenHurt × impact)` and `CameraShake.Add(ShakeWhenHurt ×
  impact)`, with Shake When Hurt 0.55 → **0.40**. A bite at 0.7 then shakes about 3 px with a 49 ms stop, and a stomp at 1.8 about
  17-22 px with 126 ms. Rumble follows the shake by itself (`ControllerRumble.FromShake`).
- **Data:** Impact per monster attack.
  - **0.7**, nips and grabs: WolfHamstringBite, BoarGore, BlightGore, RootGrasp, TongueLash, ShamanShove, TrollShove, MonsterStrike,
    HuldraClaw, WidowBite.
  - **1.0** (as now): DraugBackswing, LostCut, LostBackcut, TrollShoveFollow, BoarTrample, WolfPounce, WidowPounce, MistwingCharge,
    and every projectile.
  - **1.3-1.4**, big blows not flagged Heavy: DraugCleave 1.3, ShieldbearerCleave 1.3, Knight Slashes 1.3, LostFinisher 1.3,
    TrollRush 1.3, GuardianSlam 1.4, TrollSweep 1.4, HeartrootSlam, Sweep and Chop 1.4.
  - **1.6-2.0**, only attacks flagged Monster Attack › Heavy: DraugLunge 1.6, ShieldRush 1.6, BoarCharge 1.6, LostRush 1.5,
    LostHeavy 1.6, Knight Charges 1.7, TrollStomp 1.8, FrostStomp 1.8, OldTrollCharge 1.8, GuardianGroundSlam 1.8, BergQuake 2.0,
    HeartrootUproot, Hammerfall and GroundSlam 2.0.
- **Keep unflagged attacks under 1.5.** `AttackTelegraph.IsHeavy` also draws the dark edge and swell for Impact ≥ Heavy Impact (1.5)
  (`AttackTelegraph.cs:157`). Of the big blows, only those listed in the last group are flagged Heavy (checked against every
  monster's attack list).
- **Shove-back on great blows (data):** a negative Attack › Pull, which `ForcedMove.AfterHit` already applies over 0.35 s.
  TrollStomp −1.0, TrollSweep −0.8, GuardianGroundSlam −1.0, BergQuake −1.2, FrostStomp −1.0, BoarCharge −1.2, Knight Charges −1.2,
  OldTrollCharge −1.8, DraugLunge −0.6, HeartrootGroundSlam and Hammerfall −1.2. `Landed` includes blocked hits but not parried
  ones (`DamageResult.cs:14-17`, `ForcedMove.cs:26`), so a blocked troll sweep still slides you back. That is the weight of a block
  Souls games show, made without a new clip.

**Why.** Sakurai: hitlag scales with the blow. God of War (2018) lowered its shake to keep targets readable (CombatResearch §2.9). The
player's own blows already scale by Impact; the blows taken are the half that doesn't.

**How to build.** Three lines in `CombatFeedback`, one settings value, and about 40 attack assets through the Content Editor or a
SerializedObject eval. Then run the telegraph and combat-feel tests, and `MonsterBehaviorTests`.

**Effort:** S. **Risk:** the shove-back makes follow-ups (Draug cleave into backswing) whiff more often, which is slightly easier. Watch
for ledges: check that `ForcedMove` on the player respects a `RaidChasm` edge before raising any value above 1 m. Damage, timing and
the decided damage order are untouched. **Verify in Play:** take a wolf's bite, then a Stone Troll's stomp, then block a sweep. They
should feel different in the frame, and the block should slide you back. Watch the heavy warning swell on the Draug's cleave: it must
not have gained one.

---

### 4. Knockback by weight, not by critical ★ quick win

**What is wrong now.** `HitReaction.cs:76` pushes 0.35 m, or 0.8 m on a critical or a kill, whatever the attack, its technique or the
body. A dagger's always-critical Focused Stab shoves an Old Moss Troll 0.8 m, while a warhammer's heavy pushes a rootling 0.35 m. Weapon
weight is the identity the families most need, and here it is flat.

**The change.**
`metres = min(0.35 × Impact^1.5 × technique × (critical ? 1.2 : 1), cap) × body`, with cap 1.6 m. The body share comes last.
- **Technique:** Heavy or Finisher 1.5, Charged or Art 2.0, Counter or Guard Counter 1.3.
- **Body:** ordinary 1, `MonsterAI.IsHeavyweight` (great beasts, elites, boss fights) 0.35, a `BossEncounter` 0.
- **Duration** = 0.10 + 0.05 × metres.
- Heavies other than Charged and Art are capped at 1.0 m, so a heavy follow-up still reaches.
- **Results:** dagger stab 0.18 m (critical 0.22); sword slash 0.35; sword finisher 0.65; sword heavy 0.78; hammer heavy 1.0 (cap);
  hammer charged 1.6; on a Stone Troll, the hammer heavy 0.35 and a dagger stab 0.06.

**Why.** The weight of a weapon is read in what it does to the other body (Sakurai; CombatResearch §2.9). From 16 m up, displacement
reads better than a pose change.

**How to build.** A static `HitReaction.KnockbackFor(in DamageInfo, in DamageResult, Component target)` used by `Play` and by
`RaidChasm.cs:81`, which pushes off chasm edges with the same numbers today. New `CombatFeel` fields: Knockback Heavy (1.5), Charged
(2.0), Counter (1.3), Heavyweight Share (0.35) and Most (1.6). Knockback Distance Crit becomes the critical factor.

**Effort:** S. **Risk:** a lighter chain step (Lunge 0.8-1.0 m, `StepIntoBlow` closes up to it) still reaches a body pushed 0.35 m. Hammers
pushing harder may scatter rootlings, which is fine for a weapon that crushes. The `RaidChasm` knock-off gets stronger for heavy
weapons and weaker for daggers: check one chasm. **Verify in Play:** the same rootling hit by a dagger, a sword and a hammer heavy
should move 0.2, 0.35 and 1.0 m (read `transform.position` before and after in a probe). A troll should barely move.

---

### 5. No blow you can't answer after a flinch ★ quick win

**What is wrong now.**
- Strikes at the player start at least 0.35 s apart: `attackTurnSpacing`, a shared static (`MonsterAI.cs:132`,
  `MonsterAI.Fighting.cs:244`).
- A wolf's bite winds up for 0.45 s, so a second wolf's bite lands 0.35 s or more after the first.
- A flinch lasts 0.45 s (`CombatTechniqueSettings.asset:33`). During it the player can't dodge or guard (`PlayerCombat.cs:313,316`).
- So a second bite landing 0.35-0.45 s after the first can't be answered. This is consistent with the 18% of significant hits that
  came within 0.6 s of the previous one (§1.1).
- The flinch grace (1 s) stops a second flinch, but not the damage.

**The change.**
- **(a)** When the local player flinches, push the shared turn clock to `flinchUntil + 0.2 s`, so no new strike at the player *starts*
  inside the flinch.
- **(b)** Let a dodge cut the last part of a flinch short: `CanDodge` is true from 0.25 s into a flinch (new Flinch Dodge After
  setting), and the dodge ends the flinch. Not for a knockdown, nor for a monster's parry thrown back at you (`Rebuff`). The swing is
  still lost. The dodge already buffered during the flinch (0.35 s, `PlayerDodge.cs:61`) then goes at 0.25 s, and its 0.3 s of
  invulnerability covers the second bite.

**Why.** Losing control should be bounded and readable. Diablo IV cut the crowd control monsters put on players to about 1.5 s on
average (CombatResearch §2.4). The project's own anti-overwhelm rules (Heavy Hit Invulnerable, turn-taking) already intend this. A pack
that can chain an unanswerable second blow breaks GDD §10.3's "readable telegraphs".

**How to build.** `MonsterAI`: a static `HoldTurns(float until)` (`nextTurnAt = Max(nextTurnAt, until)`), called from a listener
on the local player's `PlayerCombat.Flinched` (`CombatFeedback` already watches `PlayerCombat`, or wire it in `MonsterAI`'s own static
setup). `PlayerCombat.CanDodge` and `OnDodged` handle the flinch tail. New `CombatTechniqueSettings` fields: Flinch Turn Grace (0.2),
Flinch Dodge After (0.25).

**Effort:** S. **Risk:** slightly easier. "Getting hit costs you your swing" and "trading blows loses" still hold. Update the Flinch rule
in `Docs/Combat.md`. **Verify in Play:** three wolves at the Haven, standing still. Count bites landing within 0.5 s of each other in the
Hurt rows (expected near 0), then try dodging out of a flinch at 0.25 s.

---

### 6. A dodge press is never lost ★ quick win

**What is wrong now.** `PlayerDodge.TryDodge` returns false while the cooldown runs (`PlayerDodge.cs:157`) and keeps nothing. Only a
press during a wind-up is buffered (`:163-173`). The cooldown is 0.9 s against a 0.5 s roll (`PlayerMovementSettings.asset:21-23`), so
a press in the 0.4 s after the roll is dropped silently. The art already answers a refused press with a sound "so the press isn't simply
lost" (`CombatFeedback.cs:643-647`); the dodge doesn't.

**The change.** A press within the last **0.2 s** of the cooldown is kept (`bufferedUntil = readyAt + 0.05`, which Update already
retries at `:318-328`). An earlier press plays the short refusal sound (Out of Stamina, as the art does). The cooldown stays 0.9 s.

**Why.** Buffers are standard: Smash uses 9 frames (about 150 ms) (CombatResearch §2.9). NRftW's patches kept tuning its dodge and
quickstep queue windows (§2.1).

**How to build.** `PlayerDodge.TryDodge`, plus a Cooldown Buffer field in `PlayerMovementSettings` (0.2). **Effort:** S.
**Risk:** none. Stamina and cooldown still limit dodging. **Verify in Play:** double-tap dodge at about 0.75 s; a second roll should come
at 0.9 s, every time.

---

### 7. Success lands harder than a plain hit ★ quick win

**What is wrong now.**
- **Parry:** `SlowTime(0.4, 0.09)` (`CombatFeedback.cs:393`; `CombatTechniqueSettings.asset:109-110`), about 54 ms of world time
  lost. A light slash freezes about 66 ms. The hardest technique in the game lands lighter than a jab. A perfect dodge is the same.
- **The counter window** (1.0 s, 1.5 s with the sword's Counter Chain) is a sound only (`OnCounterReady`, `:597-600`). Nothing shows
  how long it lasts.
- **The guard counter** ("a hard answer, 1.75 times the poise") is missing from the strong set (`:173-174`) and from the extra
  multiplier (`:246-248`). It lands like a light blow, with no critical layer.

**The change.**
- **Parry:** `HitStop(0.12 s)` at time scale 0.05, the length of a heavy, with the flash and the parry ring as now. A monster's parry
  of the player keeps today's small hitch.
- **Perfect dodge:** `HitStop(0.08 s)` plus the current effect.
- Both stay "a beat of weight, not slow motion", as `Docs/Combat.md` › Feedback decided. A slow tail (0.18 s at 0.5) can sit behind a
  setting that is off by default.
- **Counter-ready:** for as long as the counter is ready, the weapon's art light turns pale steel-blue (0.55, 0.85, 1) at intensity
  1.4 and pulses at 8 Hz, then goes out when the window ends. That is the counter's own colour already: its slash is tinted that way at
  `:572-575`.
- **Guard counter:** add `HitTechnique.GuardCounter` to the strong set and give it the ×1.5 extra.

**Why.** A player's best moment should be the strongest beat on screen. Monster Hunter Wilds restored its hitstop after players
objected (CombatResearch §2.8).

**How to build.** `CombatFeedback.OnAnyGuarded` (the Parried case) and `OnPerfectDodge`. New `CombatTechniqueSettings` values: Parry
Stop (0.12) and Perfect Dodge Stop (0.08). `HeldItemView.UpdateArtReady` (`HeldItemView.cs:141-170`) reads a new
`PlayerCombat.CounterReadyFor` (seconds left) and overrides colour and intensity while it is above 0.

**Effort:** S. **Risk:** none to the economy (Focus 18 and the counter's ×1.3 are unchanged). **Verify in Play:** parry a Draug, then land
a light slash. The parry must feel heavier. The pale glow must show and go out with the window.

---

### 8. The blow that ends the fight

**What is wrong now.** The last kill of a fight is handled like any kill (`CombatFeedback.cs:251`, 130 ms × extra × Impact). Then
`PlayerCombatMode` waits 5 s of quiet before the fight is over. Nothing marks the moment the danger ends, except the boss stop (V6).

**The change.** On a player kill with no other living hostile hunting the player side within Fight Hold Range (14 m), and only if the
fight had two foes or lasted 4 s or more:
- after the kill stop, `SlowTime(0.3, 0.35 s)` (real seconds; skipped while hosting co-op, as all slow time is);
- a single "fight won" sting: a low drum hit and a long breath of wind, 1.2 s, in `Tools/Audio/combat.py`, one take;
- end Combat early: call `PlayerCombatMode` so the music's combat layer resolves now, not 5 s later.

Not for a boss (the boss stop already plays) or a kill on wildlife.

**Why.** Closure: Sakurai's "boss stop" generalised to every encounter, as the project's V6 already does for bosses. Extraction and
ARPG fights are a string of small encounters, and the end of each one is a reward beat. Hades marks every cleared chamber with a sting
*(unverified)*.

**How to build.** `CombatFeedback` (the check walks `MonsterAI.Active`, as `HoldsBack` does), a `CombatFeel` › Fight Won Sound and
Last Kill Slow fields, and a `PlayerCombatMode` method to end it now.

**Effort:** S. **Risk:** none. **Verify in Play:** clear a pack of three wolves. The last one slows and the sting plays. Kill a lone
rootling while a troll still hunts you: nothing extra happens.

---

### 9. Damage numbers that say whose blow it was and how big

**What is wrong now.**
- `PlayerArmature.prefab` has a `DamageNumberSpawner`. `DamageNumber.Show` colours only by damage type and critical
  (`DamageNumber.cs:31-38`), so blows the player takes float the same white as blows the player deals.
- Companions' and friends' blows look like the player's.
- A sweep or fan floats several numbers per target.
- In a pack this is the HUD clutter the user just asked about (8e9ba0ab, `HudRules.cs`).

**The change.**
- **Blows taken:** red (0.9, 0.25, 0.2), 0.85 size, no pop.
- **Others' blows** (companions, co-op friends): 0.7 size at 60% alpha.
- **Size by share** of the target's maximum health: 0.85 under 5%, 1.0 at 10%, 1.3 from 30%.
- **Merging:** hits on the same target within 0.25 s become one number that counts up and pops again (staff fans, flurries, the
  greataxe's Momentum sweeps).

**Why.** In top-down ARPGs, numbers sized by how much a hit mattered let the eye skip the chip damage (Diablo IV does this,
*(unverified as a stated rule)*). Fewer numbers on screen also answers the user's HUD request directly.

**How to build.** `DamageNumber.Show(DamageResult, kind, share)`. `DamageNumberSpawner` knows its owner (`Player.IsPlayer`), the
source (`damage.Source`, local player or not) and keeps the last number per spawner for merging. Colours and sizes go in the prefab's
serialized fields.

**Effort:** S. **Risk:** none (the Damage Numbers option is unchanged). **Verify in Play:** a wolf pack with a companion. Your numbers
are large, the companion's faint, the bites on you red, and a sweep shows one number per wolf.

---

### 10. Each family sounds like its own weapon, and an interrupt has its own crack

**What is wrong now.**
- No attack has its own swing sound, and only Bow Shot has its own hit sound (checked across all 172 attack assets).
- By weapon: swords, daggers, spears and the staff have no swing sound, so they all play the game-wide `Swing` (a broad 0.3-0.38 s
  whoosh). Axes, greatswords, hammers, clubs and the maul all play `Axe Swing`.
- So a dagger's 0.2 s stab, a spear's thrust and a sword's arc whoosh alike, and a warhammer sounds like a hatchet.
- An interrupt plays the **Block** clang (`CombatFeedback.cs:429`), the same as the player blocking. Interrupts are the most common
  technique in the logs (63), and `Docs/TODO.md` asks "are interrupts readable".

**The change** (Python, `Tools/Audio/combat.py`, 3-4 takes each through `variations.py`):
- `thrust_swing`: 0.16-0.2 s, a narrow band rising from 1.2 to 4 kHz with a sharp end, for daggers and spears.
- `blunt_swing`: 0.5 s, 70-600 Hz, heavy flutter at about 18 Hz, for hammers, clubs and the maul.
- `great_blade_swing`: the axe swing's body plus a 3-4.5 kHz steel edge, for greatswords.
- Axes keep `Axe Swing`; swords keep `Swing`.
- `interrupt_break`: a dry crack and a short falling rattle, 0.25 s. It replaces Block in `OnAnyInterrupted`.
- **Data:** new `SoundDefinition`s in `Audio/Sounds/Library`, set as `WeaponDefinition.swingSound` on the dagger, the spears, the
  hammers, the club, the maul and the greatswords. A new `AudioSettings` › Interrupt entry.

**Why.** Each family's identity is already in its chain, signature and art. Sound is the cheapest place to carry it, and it is heard
from above, where poses shrink. Python first follows the user's rule (memory: audio synthesis).

**Effort:** S-M. **Risk:** none (the sounds only). The user listens before it ships: add the takes to the Sound picks page.
**Verify in Play:** swing each family at the dummy with eyes closed; each should be nameable. Interrupt a wolf's bite, then block one:
they should sound different.

---

### 11. A charge you can feel building

**What is wrong now.** A heavy held to charge (up to 0.9 s) gives no feedback until it is full. Then comes one glint and the Counter
Ready chime (`CombatFeedback.cs:627-631`). `WeaponTrail` hides while charging (`WeaponTrail.cs:113`). Whether to let go early or hold
for the charged attack (Hades II-style) is a blind count.

**The change.**
- **Light:** the weapon's art light (`HeldItemView`, `:131-170`) ramps with `PlayerCombat.Charge`: from ember (1, 0.62, 0.3) at
  0.3 to white-hot (1, 0.9, 0.7) at 2.0, range 1.1 → 1.8 m, with a flicker at full.
- **Sound:** a riser, `charge_rise` (0.95 s: noise climbing from 200 to 1800 Hz plus a leather creak, Python), on a dedicated
  `AudioSource` on the player (`AudioManager.Configure`). It stops on release, dodge or flinch, and the existing chime marks the full
  charge.
- **Optional camera:** a 4% push-in at full charge, eased back on release.

**Why.** Monster Hunter's Great Sword flashes at each charge level, and Hades II shows an Omega move's charge on the character
*(unverified)*. A
charge with a visible build lets the player choose when to let go instead of counting.

**How to build.** `HeldItemView` reads `PlayerCombat.IsCharging` and `Charge` (both already public); a small `ChargeSound` component
beside `CombatFeedback`. **Effort:** S-M. **Risk:** none. The art-ready ember keeps its meaning when not charging. **Verify in Play:**
hold a hammer heavy and let go at half charge and at full. The glow and the riser should show the difference without the chime.

---

### 12. A warning at the screen's edge for blows from out of frame

**What is wrong now.** G1 made "off screen" the real viewport, and shooters now wait. But a melee creature off screen still strikes
once it has waited Off Screen Wait (0.6 s) with nobody else closing in (`MonsterAI.Fighting.cs:345-350`). The third part of G1, a
pip on the screen's rim, was not built (`Docs/AgentDecisions_2026-11-17.md`: G1 "Done" lists only the viewport and the shooters).
At 16 m, the fight camera's frame is only about 21 m wide at the player.

**The change.** While a creature out of frame winds up a strike aimed at the local player, a small chevron sits on the screen's rim on
its side. It is coloured by the telegraph tier (red, blue or purple, with the dark edge for a great blow) and fills with the strike's
progress. It goes when the blow lands or the creature comes into frame. At most three are shown, and none outside a fight.

**Why.** Off-screen attacks are the top-down genre's chronic complaint. Warnings at the edge let the player answer what they can't see
(GameDesignResearch §1, Last Epoch and PoE forums).

**How to build.** A new `OffScreenWarnings` view beside `StealthReadout`, reusing its edge-arrow placement (`StealthReadout.cs:164`). It
reads `MonsterAI.Active` (attacking, target is the player, `OffScreen`, made public) and `AttackTelegraph`'s fill progress.

**Effort:** S-M. **Risk:** HUD clutter, but it is one element, shown only during wind-ups. Add it to `HudRules`. **Verify in Play:** a
probe puts a Draug 4 m behind the player at the bottom edge. A chevron should fill and the blow should be dodgeable without turning.

---

### 13. The hit flash wipes elite rings ★ quick win (correctness)

**What is wrong now.**
- `HitReaction` collects every child renderer on the first hit (`HitReaction.cs:102-112`).
- When the flash ends it calls `SetPropertyBlock(null)` on each of them (`:241`).
- Elites' feet rings (one per affix, `EliteCreature.cs:178`, `:238-251`) and the Unyielding ring (`EliteBehaviour.cs:255-264`) take
  their colour from a property block.
- So, reading the code, the first blow on an elite flashes its rings white and then leaves them in the telegraph material's own colour.
  The colour that says what kind of elite it is goes away after one hit.
- Outline copies (`OutlineCopies.IsCopy`) and the elite's label are flashed too.

**The change.**
- Before the first flash, read each renderer's own block (`GetPropertyBlock` into one block per renderer) and restore that block
  instead of null.
- Skip `OutlineCopies.IsCopy`, `TMP_Text` renderers and anything using the telegraph or ground-mark material.

**Effort:** S. **Risk:** none. **Verify in Play:** strike an elite with an aura affix (tier II raid or a probe spawn) and check the ring
colour before and after the first hit.

---

### 14. A last-moment dodge out of the blow counts as a perfect dodge (needs the user's sign-off)

**What is wrong now.**
- A perfect dodge needs the blow to strike the player during the dodge's invulnerability (`PlayerCombat.Reactions.cs:139-151`, via
  `Damageable.Evaded`).
- A dodge covers 4 m in 0.5 s, about 8 m/s (`PlayerMovementSettings.asset:20-21`). A wolf's bite reaches 1.8 m. A dodge started
  0.2 s before contact has already left the area, so the blow misses: no Evaded, no counter, no Focus.
- So the reward goes to dodging *into* a blow, not to dodging it well. The logs show 1 perfect dodge in about 40 dodges (§1.1).

**The change.** When a melee strike resolves with no target, check the local player. If they were inside its arc and reach when their
dodge began, and the dodge began within the invulnerability time (0.3 s) before contact, it is a near-miss perfect dodge: the counter
is readied, plus **half** the Focus (9).

**Why.** Bayonetta's Witch Time rewards the last-moment dodge, not standing in the blow. The game's own text says "dodging through a
hit", and players read that as "at the last moment".

**How to build.** `PlayerDodge` records where and when the dodge began. `MonsterAttacks`, when a melee blow finds nobody, tests the
local player's record against the attack's arc and range from the attacker. A new `PlayerCombat.NearMiss(in DamageInfo)` grants the
counter and raises `PerfectDodge` (so the feedback from item 7 plays).

**Effort:** M. **Risk:** more counters and Focus for the player. The parry economy was decided, so this needs the user's yes, and the
Focus share can be 0 if they prefer. **Verify in Play:** dodge away from a Draug cleave at the pale fill. The counter should ready, and a
dodge made at the first moment of the wind-up should not count.

---

### 15. A camera kick that points along the player's big blows

**What is wrong now.** The only camera motion is trauma shake: Perlin noise in every direction (`CameraShake.cs:81-88`). For the
player's heavy blows (21 px plus 1.6° of roll for a hammer heavy) it is noise rather than a push. Light blows rightly get none
(0.7 px).

**The change.** `CameraShake.Kick(Vector3 worldDirection, float metres)`: an offset along the blow's direction (projected onto the
screen) that rises in 30 ms and springs back over 120 ms in unscaled time, so it plays through the hit stop.
- Used for the player's heavy, finisher, charged and art landings at `0.05 × Impact` m (hammer heavy 0.09 m, about 8 px), once per
  swing (item 1).
- With the kick in place, drop those blows' trauma by about 40%, so the frame pushes instead of rattling.
- Blows taken from Heavy attacks kick the other way.
- Under the Screen Shake slider.

**Why.** Vlambeer's screen kick in the shot's direction, and Eiserloh's trauma model for the rest (CombatResearch §2.9). God of War cut
its shake for readability.

**Effort:** S-M. **Risk:** none (it is feel only). **Verify in Play:** hammer heavy on the dummy from four sides. The frame should nudge
away from you along the blow, never sideways.

---

## 3. Waiting on the user, not re-ranked here

- **G2, the fight camera** (`Docs/GameDesignResearch_2026-11-17.md` §1): the camera pulls in about 25% when a fight starts (16 m
  fighting against 21 m exploring), against GDD §10.1's "zoom out slightly for large fights". It would help the crowd and off-screen
  problems more than item 12, and is a one-line data test (Combat profile distances = Explore's).
- **U4, value contrast** (`Docs/AgentDecisions_2026-11-18.md`): figures at 1.2-1.4:1 against the ground in the Woods and the Valley
  by day. Foes read by outline and colour, not by value; three options are already written up for the user.

## 4. Considered and not recommended

- **A longer parry window.** It is 0.15 s, close to Sekiro's quoted 12 frames *(unverified)*. The fault is the cue (item 2), not the
  window, and widening it re-opens the parry-wait loop the economy closed. The timing assist already scales it for those who want it.
- **Per-animator hit stop in single player** (freezing only attacker and victim). The global dip is what single-player action games
  use and it keeps the telegraphs frozen fairly. Co-op already uses per-animator `HitShake`.
- **Shorter monster wind-ups or more "hold" strikes.** Every non-breakable melee strike already winds up for 0.45 s or more, and holds
  are on the Draug and the small troll only. More would punish reflexes the game wants to teach.
- **New hit-reaction clips for the player's block.** The user's rule is that enemies and the player don't share clips, and the owned
  block-hit clips went to the Shieldbearer and the Draug. Item 3's shove-back gives a block weight without a clip.

## 5. What each item leaves alone

None of these change damage, the cadence (Recovery Time), `WeaponIdentityTests`' family pace, the damage order (plain 19, mid-strike
backstab 26, perfect-dodge counter 44, staggered backstab 50), the parry economy's numbers, the half dodge refund, enemies learning
backstabs, or unrooted swings. Items 5 and 14 make the game slightly easier. 14 needs the user's yes; 5 is a fairness fix within
the anti-overwhelm rules already in place.

## Sources

Project: `Docs/CombatResearch.md` §2 (SFV hitstop frames, Sakurai on hitstop, Smash buffer, Vlambeer, Eiserloh, God of War 2018,
Monster Hunter Wilds hitstop, Diablo IV crowd control, NRftW patches); `Docs/GameDesignResearch_2026-11-17.md` §1 (off-screen attacks).

Web (this review):
- [itch.io post 7434658](https://itch.io/post/7434658): "it would look weird if i swiped at lets say 5 enemies and it hitstops 5 times"
- [Reaction Time, Encyclopedia.com](https://www.encyclopedia.com/social-sciences/applied-and-social-sciences-magazines/reaction-time):
  typical simple RT about 180 ms visual, 140 ms auditory, 150-250 ms for simple tasks
- [Jain et al., PMC4456887](https://pmc.ncbi.nlm.nih.gov/articles/PMC4456887): auditory reaction faster than visual
- [Shelton & Kumar (2010)](https://coachsci.sdsu.edu/csa/vol204/shelton.htm): visual 331 ms against auditory 284 ms in their sample
- [Coincidence-anticipation timing and reaction time, PubMed 20681339](https://pubmed.ncbi.nlm.nih.gov/20681339/): timing to a moving
  target is a separate skill from reaction time
- [Sekiro Deflection, Fextralife](https://sekiroshadowsdietwice.wiki.fextralife.com/Deflection) and
  [Switchblade Gaming](https://www.switchbladegaming.com/action-rpg/vs-sekiro-2/): the deflect window quoted as about 12 frames
  (200 ms) *(fan sources, unverified)*
- [Godot multi-hit prevention (uhiyama-lab)](https://uhiyama-lab.com/en/notes/godot/multi-hit-prevention/): "one attack × one
  target" as the unit of a swing

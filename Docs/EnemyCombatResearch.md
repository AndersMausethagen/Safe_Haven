# Enemy combat research: fights that differ, read well and answer back

Research date: 2026-11-15, after the player's combat rework (`Docs/CombatResearch.md` › Status). This was read-only: no Unity, no CLI, no
project edits apart from this file. Paths are relative to `d:\Safe Haven\Safe Haven`; `file:line` points at the `test` branch today.

**The question (the user):** now that the player has so many more animations and techniques, how can enemies use them, without moving
or behaving like the player, so a fight is more dynamic, more varied, and gives better feedback?

## 0. Summary

**The three biggest findings:**
1. **Human-shaped enemies already move like the player.** Every strike, flinch, stagger, knockdown and death state on
   `MonsterHumanoid.controller` plays a clip that `PlayerAnimator.controller` also plays (§1.6). Every humanoid action without a state of
   its own (a shaman's call, a troll-wife's mend, a spit, a stone skin) plays the generic `Attack` state. That state is Kevin Iglesias
   `Attack1H01_R`, the player's axe-chain opener `Chop A`. So a call for help reads as a sword swing.
2. **Enemies pick their strikes like a metronome.** The highest-priority ready attack always wins (`MonsterAttacks.cs:123-136`), so a
   Draug lunges whenever its 9 s lunge is ready and cleaves otherwise. The only chance in it is the follow-up and hold rolls.
3. **Enemies have almost no defensive verbs, and what they have shows weakly.** The Shieldbearer's shield is passive and only up while
   it chases. A blocked blow moves nothing on its body, and the guard never breaks as a guard. Punish windows (recovery) and the build-up
   to a stagger (poise) can't be seen from the camera, except as a thin optional bar. That bar is wrong for friends in co-op, because their
   copy keeps no poise (§1.7).

**Ranked recommendations** (detail in §4):

| # | Recommendation | Impact | Effort | Changes balance? |
|---|---|---|---|---|
| 1 | Give enemies their own animation set from clips the player never plays (calls, casts, ready stances, side steps, stun loop, get-up, weapon draw) | High | M | No |
| 2 | Show what the player can do on the ground: interruptible strikes and heavy strikes get their own look, and the recovery window shows | High | S | No |
| 3 | Weighted, situational strike choice with a no-repeat rule | High | S-M | No (cooldowns kept) |
| 4 | Show when poise is low ("Reeling", at the hammer's 30% Crush line), let a heavy blow then fell it, and send poise to friends | High | M | Slight |
| 5 | Enemy guard verbs: the Shieldbearer's shield wall that can break; a reactive weapon guard and counter on armed undead | High | M-L | Yes, sign-off |
| 6 | Hit reactions by body type and direction, and a flinch after a blow has landed | Med | M | No |
| 7 | Hurt states for ordinary creatures (fury, desperation or flight), with tells | Med | S-M | Slight |
| 8 | Elite affixes that change verbs: Armoured, Vigilant, Relentless | Med | S each | Yes, sign-off |
| 9 | Armour that visibly breaks off, and a shield that can be broken | Med | M | Slight (easier) |
| 10 | Answers to the bow: side steps and raised arms on agile humanoids | Med-Low | S-M | Yes (bow pace) |
| 11 | Enemies react to each other: summons scatter, nearby creatures hesitate after a brutal kill | Low-Med | S | Slight (easier) |

## Status (2026-11-15): phases A, B and C built

The user asked for phases A and B, then C ("it sounds good"). All three are built on `test`, checked by Edit Mode tests and Play-mode
probes from the CLI, not yet played by hand. An independent code review of A and B found five issues, all fixed with C (a second draw on
finding a lost target, the interrupt reel overwritten by a flinch, the opening ring left on a disabled telegraph, a channel looping after a
disable, MoveZ unchecked).

**Phase C (answers back; changes balance, approved by the user):**

| Item | Built | Where |
|---|---|---|
| 4.4 Reeling payoff | A heavy, charged, art or guard-counter blow on a reeling creature breaks its poise and fells it: an ordinary creature is down for Felled Seconds (1.8 s: falls, lies in `Down`, gets up in `Get Up`, UAL2 LayToIdle), a body without the clip recoils fully. A charged blow or one worth 75% of its poise fells too (it was a 1 s knockdown). Great beasts, bosses and elites only stagger. Felled travels to friends as a pose bit | `MonsterAI.TakeBlowOnPoise`, `Fell`, `GuardRules.FellsReeling`, `PoiseMeter.Break`, driver `KnockDown` |
| 4.5a Shield wall | A raised shield taking a blow jolts the arm (`Block Hit`, KI BlockShield01) and shoves the body; poise broken through it is a Guard Broken stagger ×1.5 with the guard-break crash; two of them splinter the shield (it falls away, the arm is let go, no more blocking) | `GuardBroken`, `MonsterShieldPose.Splinter`, `ArmourDebris` |
| 4.5b Weapon guard | The Draug, from raid tier II: three light blows within 2 s raise its sword (`Guard`, KI Parry1H01_R) for 1 s; light blows from the front are turned aside (30% through, poise still worn, `Guard Hit` on the arms); a heavy, an art or a blow from its flank breaks it and it reels; the third light blow into it is answered with a strike (its own wind-up and warning). 4 s before it can guard again. Not on the knights (no rider clips) | `MonsterAI.NoteLightBlow`, `WeaponGuard`, `UpdateWeaponGuard`, Monster Definition › Answering back |
| 4.7 Hurt states | Below 30% health: Fury (Stone Troll, Small Moss Troll, wolves, boars, Troll Runt, Tusse): a cry, angry eyes, breath over its head, cooldowns ×0.85. Desperate (Draug): a cry and angry eyes, and a new Below Health action, its cleave run into a backswing and another cleave (cd 7 s). Cowardly creatures cry out as they turn to run | `MonsterAI.UpdateHurt`, `NoteFleeing`, Monster Definition › When Hurt |
| 4.8 Elite verbs | Unyielding (tier II+): a yellow ring; nothing flinches, interrupts or staggers it (a parry only makes it recoil) until a third of its health is gone, then it breaks with a crash and a 2.2 s stagger. Watchful (tier II+): reads backstabs as if it remembered two, always. Relentless (tier III+): always follows up and holds. In both raids' pools | `EliteBehaviour.cs`: `UnyieldingBehaviour`, `WatchfulBehaviour`, `PressingBehaviour`; Data/Raids/Elites |
| 4.9 Armour falls off | Each armour chip breaks the next named piece off the model (Draug and Shieldbearer: pauldrons, then the helm; knights: their shoulders), thrown along the blow and gone after 6 s | Monster Definition › Armour Parts, `CreatureWounds`, `ArmourDebris` |
| 4.10 Bow answers | Huldra (50%), Fossegrim (40%), Tusse (35%) and Troll Runt (30%) may step aside once per draw from a bow held on them past its perfect release; never from a perfect release; 5 s between steps. The local player's bow only | `MonsterAI.DodgesArrow`, `GuardRules.SidestepsDraw` |

Phase C checks: `MonsterBehaviorTests` 17/17 (felling verbs, light blows, guard raising, the sidestep rule, poise breaks), `RaidTierTests`
36/36, and Play probes (felled and getting up, armour pieces falling, the guard raised, turning a blow aside and answering, a shield jolting
and splintering after two guard breaks, a wolf's fury, an Unyielding elite ignoring a poise break and breaking at 65%). The arrow dodge was
not seen live: a probe can't hold the bow's aim on a creature, so its rule is tested on its own.

**Phases A and B:**

| Item | Built | Where |
|---|---|---|
| 4.1 Calls and casts | Howls and summons channel arms up (`Call`, Spell_Unarmed_Up), shots channel arms forward (`Cast`, Spell_Unarmed_Forward), and let go in `<state> Release` the attack's Strike Lead before the hit. Stones, dirt and the troll's log are thrown (`Throw`, Combat_Defend_Spell); shoves are their own (`Shove`, Combat_Unarmed_Attack01); drags, rushes and grasps claw. An action with no state falls back by kind | `MonsterAnimationDriver.OnAttackStarted`, `KindState`; attack assets' Animation State and Strike Lead |
| 4.1 Draug and Shieldbearer strikes | Draug: UAL1 Sword_Attack, Combat_1H_Attack_Off02, UAL2 Zombie_Scratch. Shieldbearer (own override): Combat_Defend_Attack01, KI AttackShield02, its own `ShieldbearerCleave` (the Draug's numbers, its clip's timing) | `Draug.overrideController`, `Shieldbearer.overrideController` |
| 4.1 Ready stances and side steps | `Ready` state, a 2D blend on MoveX/MoveZ (KI Walk01 left/right/back/forward) round Combat_Unarmed_Ready; the Draug holds a sword ready, the Shieldbearer its shield, casters (`Caster.overrideController`: Shaman, Fossegrim, troll-wife) their hands; riders sit, treants idle | `MonsterAI.IsWaitingTurn` (marked while circling, standing ready, holding aim or distance); `CreatureHost.WaitingFlag` in co-op |
| 4.1 Stun loop | `Stagger` plays KI CombatDamage02, then `Stunned` (Combat_Stun) while the `Staggered` bool holds | controller |
| 4.1 Get-up | Not changed: the knockdown clip already ends standing within its 1.4 s; a separate lie-and-rise would lengthen the opening, which belongs with C's Reeling payoff | |
| 4.1 Weapon draw | Draug and Shieldbearer (Monster Definition › Spotted Pause 0.55 s) stop and draw (KI CombatEnter1H01) when they spot someone while idle or looking round | `MonsterAI.BeginDrawIfUnaware`, `HoldsWhileDrawing` |
| 4.2 Telegraph tiers | Interruptible strikes fill blue; a creature's great blow (Monster Attack › Heavy: charges, stomps, quakes, ground slams; or Impact ≥ 1.5) gets a dark edge and a low swell (Heavy Windup, `Tools/Audio/combat.py`); after a blow is spent a pale green ring shrinks at its feet for the opening. The damage-share rule in §4.2 marked 31 of 68 strikes at 100 health, so it was dropped for the explicit mark | `AttackTelegraph`, `MonsterAttacks.TryGetOpening`, `CombatFeelSettings` › Attack telegraphs |
| 4.3 Weighted choice | Monster Attack › Weight and When Foe; the one used last counts half, an answer to what the foe does counts 3×, a weightless action above the band still goes first, a choice holds 0.6 s. Given to Draug, Shieldbearer, Stone Troll, Bog Troll, Small Moss Troll, wolves and boars; bosses unchanged | `MonsterAttacks.SelectAttack`, `ChoiceWeight`, `MonsterAI.ReadFoe` |
| 4.4 Reeling look | Below Reeling Share (0.3) of poise the body sways, embers glow at the chest (Reeling Embers) and the poise bar pulses; poise is a byte in co-op poses (20 bytes now), so friends see it too. The payoff (a heavy blow fells it) is phase C | `ReelingTell`, `MonsterPose.Reeling`, `WorldHealthBar`, `CreatureHost.PoiseByte` |
| 4.6 Reactions | Light flinch UAL1 Hit_Chest, heavy Hit_Head; casters Combat_Cast_Hit, the Shieldbearer Combat_Defend_Hit; none from behind (the procedural pose tips it forward); an arms-only `Flinch` on the upper-body `Flinch Arms` layer when struck in its own recovery; a strike broken off (interrupt, recoil) plays the heavy flinch | `MonsterAnimationDriver.OnDamaged`, `OnInterrupted` |
| 4.11 Reacting to each other | Summons rout for 4 s when their summoner dies; a finishing blow or a staggered backstab kill makes ordinary creatures within 8 m falter 0.6 s | `MonsterAI.ShakeTheOthers`, `Waver`, `MonsterAttacks.RoutSummoned` |

Not built from A and B: the Draug's guard-breaking shove (it would be a new attack; the Shieldbearer's rush and the Stone Troll's stomp
answer a raised guard instead). Every new field defaults to the old behaviour; Old Moss Troll's log throw now plays slowly (×0.29) to land
its throw at its 1.4 s wind-up: a playtest check.

## 1. What enemies do in a fight today

### 1.1-1.5 The fight, system by system

| System | What it does today | Where |
|---|---|---|
| Choosing a strike | The **highest priority** ready action wins (in range, off cooldown, known at the tier and boss phase, under Below Health); list order breaks ties; nothing random. Draug: Cleave (priority 10, 1 s, hold 30%, one follow-up) or Lunge (Charge, priority 20, 9 s). Stone Troll: Sweep (20, 2.2 s) > Stomp (15, 9 s) > Lob (12, 7 s). Knights: Slash and Charge only | `MonsterAttacks.cs:123-136`; `MonsterAI.Fighting.cs:102-224` |
| Taking turns | At most two close in, starts 0.35 s apart; the rest circle at 3.5 m for 0.8-1.6 s or stand ready; off screen waits; ranged attacks have their own turns; great beasts and elites don't wait | `MonsterAI.Fighting.cs:280-393` |
| Warning | The arc fills over Hit Delay; the parry colour and a glint for the last window; Unblockable has its own colour | `AttackTelegraph.cs:106-180` |
| Holds, combos | A hold stops the wind-up at 60% fill for 0.45 s while tracking; each follow-up is aimed anew with its own warning | `MonsterAttacks.cs:225-247`, `:384-392` |
| Openings | Recovery (`RecoveryTime`) has no tell; the next strike may start 0.15 s after. Charges end Exposed; Open After (only the Heartroot) ends in a stagger | `MonsterAttacks.cs:394`, `:398-444`, `:630-639`; `MonsterAI.Fighting.cs:258-262` |
| Defence | A shield blocks from the front **only while Chasing** (Shieldbearer only, arc 150°, `MonsterShieldPose` raises the arm). Backstabbed, it strikes back or hops (`Hop` = `Combat_1H_Dodge`, monster-only), learns, then may hop aside or parry a swing from behind. Nothing else: no guard stance, no side step, no guard break | `MonsterAI.Reactions.cs:229-258`; `MonsterAI.cs:229`; `MonsterAI.Backstabs.cs:11-111` |
| Poise and stagger | Back at 15 per second after 2 s; breaks into a 1.3 s stagger at ×1.2 damage taken. Draug 140, knights and Stone Troll 220, Old Moss Troll and Berg-jutul 450. A parry staggers an ordinary creature; a heavyweight loses 40% of its poise and recoils 0.45 s | `PoiseMeter.cs:27-51`; `CombatTechniqueSettings.asset:120-124`; `MonsterAI.Reactions.cs:33-128` |
| Interrupts | Interruptible strikes (13 light ones) break off on any blow in the wind-up; Break Through (a heavy, counter, guard counter, finisher or art, or 25% of poise) breaks off an ordinary creature's wind-up; a spark and a 0.5 s wait | `MonsterAI.Reactions.cs:97-103`, `:176-182`; `MonsterAttacks.cs:307-316`; `CombatFeedback.cs:396-406` |
| Body reaction | Every body recoils in code by the blow's poise share (0.45-1.2); blocked blows don't move it. Humanoids flinch (`Hit`, `Hit Heavy`) except while Attacking or Staggered; a felling blow that breaks poise plays `Knockdown` | `MonsterAI.Reactions.cs:167-174`; `MonsterPose.cs:73-88`; `MonsterAnimationDriver.cs:178-241` |
| Feedback | A one-colour hit flash, `HitShake`, hit stop, hit materials, a hurt sound, the stagger's thud and ring, the backstab wedge | `HitReaction.cs:59-68`; `CombatFeedback.cs:168-220`, `:382-391` |
| Wounds | Limp below 30%, blood trail below 50%, armour chips (−12% armour, shards, a sound; the model doesn't change) | `CreatureWounds.cs:56-125` |
| Elites, bosses, packs | Seven affixes scale numbers. Five behaviours: status on hit, regrowth, burst on death, rally aura, mist. Marked by ring, tint, eyes and name; an elite counts as a heavyweight. Boss phases reel the boss, raise its damage and change its glow. Authored packs break when the leader falls; summons are not in a pack | `EliteCreature.cs:51-169`; `EliteBehaviour.cs`; `BossDefinition.cs:6-21`; `MonsterPack.cs:32-65` |

The 2026-11-14 roles (lobs, hazards, unblockable pulls, calls, `RisesAgain`, decoys) sit on top of this (memory: monster roles).

### 1.6 The animation set: almost all of it is the player's

`MonsterHumanoid.controller` states and the clips they play. The "Player plays it as" column was checked against
`PlayerAnimator.controller` and the attacks' Animation State.

| Monster state (used by) | Clip | Player plays it as |
|---|---|---|
| Strike Chop (Draug Cleave, Guardian Slam) | Stylized `Combat_1H_Attack` | `1H Chop`: Chop C finisher, Chop Charged |
| Strike Lunge (Draug Lunge, Old Troll Charge) | `Combat_1H_Attack02` | `1H Stab`: Dagger Stab C, Chop Rush, Finishing Stab |
| Strike Overhead (Troll Stomp, Guardian Ground Slam, lobs) | `Combat_2H_Attack` | `2H Overhead`: Axe Heavy, Hammer Finisher |
| Strike Wide (Troll Sweep, Draug Backswing) | `Combat_2H_Attack_Base` | `2H Wide`: Axe Sweep, Greatsword Mid (and mirrored) |
| Attack (every action without a state) | KI `Attack1H01_R` | `KI 1H Forehand`: Chop A |
| Hit / Hit Heavy / Knockdown | `Combat_1H_Hit` / `Combat_2H_Hit` / `Combat_Knockdown` | `Hit 1H` / `Hit 2H` / `Knockdown` |
| Stagger | UAL2 `Hit_Knockback` | `Knockback` |
| Death | KI `Death01` | `Death` |
| Hop, Strike Claw | `Combat_1H_Dodge`, `Combat_Unarmed_Attack` | not used: these are the monsters' own |

Sources: `CombatMovesets.cs:21-39` (the player) and `:177-195` (the monsters). **Exceptions:**
- The undead knights use Malbers rider clips through `Undead Knight.overrideController`. They are the model to follow.
- The Draug walks with UAL2 `Zombie_Walk_Fwd_Loop` and idles with `Zombie_Idle_Loop` (`Draug.overrideController`), but strikes with the
  player's clips. The Shieldbearer is a Draug variant and shares the same override controller.
- Locomotion is a 1D blend on Speed. So, from reading the controller, a humanoid circling sideways at 3.5 m plays its forward walk
  (`MonsterMovement.Strafe`, `MonsterMovement.cs:109-114`).

**Owned clips the player never plays** (checked against the controllers):

| Pack | Clips | Fits |
|---|---|---|
| Kevin Iglesias | `Parry1H01_L/_R`, `Parry2H01`, `ParryPolearm01`, `ParryDW01` (Loop and Hit), `BlockShield01` (Loop, Hit), `Stun01`, `CombatDamage02`, `CombatDeath01-04`, `Death02`, `Dodge01`, `CombatEnter1H01/2H01/Polearm01`, `CombatExit*`, `AttackShield02`, `Walk01_Left/_Right/_Backward` | Guard stances and their hits, stun loop, flinch, deaths, weapon draw on spotting, strafing |
| Stylized base pack | `Combat_Stun`, `Combat_1H/2H/2HL/Unarmed/Cast/Defend_Ready`, `Combat_Defend_Hit/Dodge/Attack01/AttackSpecial/Spell`, `Combat_2H/2HL/Unarmed/Cast_Dodge`, `Combat_Cast_Hit`, `Combat_Cast_SpellA_Loop`, `Spell_Unarmed_*` (Ready, Forward/Up Channel and Impact, Hit), `Spell_Staff_Forward_Channel`, `Combat_1H_Attack_Off/_Off02`, `Combat_Unarmed_Attack01`, `Sheath_*` | Casters' calls and casts, ready stances while waiting, shield-bearer set, side steps |
| Quaternius UAL1 / UAL2 | `Hit_Chest`, `Hit_Head`, `Sword_Attack`, `Spell_Simple_*`, `Push_Loop` / `Zombie_Scratch`, `LayToIdle`, `Idle_No_Loop` | Hits by direction, a Draug strike and claw, a shove, getting up |

The left-hand Kevin Iglesias variants (`Attack1H0x_L`, kicks `_L`) are mirrors of the player's swings. Leave them out.

### 1.7 Co-op
The host runs creatures. Friends get poses (19 bytes: place, yaw, speed, state, quarry), strikes, deaths, and staggers they caused
(`CoopCreatures.cs:1-16`; `MonsterAI.Remote.cs:139-166`).
- **Poise is never sent.** A friend's copy never wears its own poise (`MonsterAI.Reactions.cs:191`, `TakePoise` `:86`), so their poise
  bar (`WorldHealthBar.cs:138-153`) should read full all fight. This is from reading the code and was not tested.
- A new `State` value travels in the pose stream as it is. Anything else needs a byte or a message.

## 2. Gaps: why fights feel the same, and where feedback is weak

1. **The player's movements on enemies** (§1.6). This is worst on human-shaped bodies (Draug, Shieldbearer, Huldra, Shaman, Fossegrim,
   troll-wife). Calls look like attacks, when a call should read as "stop it now": it is interruptible.
2. **The same order every time** (§1.1-1.5, Choosing a strike). Nothing reacts to what the player is doing: guarding, backing off,
   shooting.
3. **No defensive verbs** (§1.1-1.5, Defence). Every creature is the same timing game. The Shieldbearer's shield gives no feedback.
4. **Openings can't be seen.**
   - The recovery window has no tell.
   - The 13 interruptible strikes look like the rest.
   - Low poise is a thin optional bar, and on a great beast (450 poise, back at 15 a second) it hardly moves.
5. **Reactions are generic.**
   - One flinch for every humanoid, with no direction, and none once a strike has started.
   - The stagger is a one-shot knockback, not a held stun.
   - No get-up after a knockdown.
6. **Only bosses escalate.** Elites are numbers and auras, except Regrowing and Burst. Armour chips change nothing on the model.
7. **Co-op poise is invisible to friends** (§1.7).

## 3. What strong games do (web research)

**(unverified)** marks a claim from a single or community source.

| Game | Technique | Takeaway for Safe Haven | Source |
|---|---|---|---|
| No Rest for the Wicked (The Breach) | "Updated hit reactions for all humanoids based on weapon class/archetype"; "Every humanoid archetype gained anti-backstab behavior"; "Each humanoid received evasive ability against projectile attacks"; enemies "recoil if poise defense is lower than shield's poise damage"; "Removed visually similar attacks to improve readability" | Safe Haven has the anti-backstab and shield poise. Missing: reactions by body type, evasion, readability | https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046 |
| NRftW (Together) | "Enemies that are blocking player attacks with shields, now have block hit reaction animations"; "Anti-Range abilities"; "Bosses are no longer fully staggered from parry" | Shield-bearers need a block reaction. Answers to range | https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726 |
| Sekiro | Enemy posture regenerates if left alone, so you must keep up the pressure. Perilous attacks show one red warning, and the body tells which answer (thrust, sweep or grab) | One warning for "don't block", and the pose tells the answer. Regeneration slower at low health is a **(community)** claim | https://news.xbox.com/en-us/2019/03/19/sekiro-shadows-die-twice-interview/, https://sekiroshadowsdietwice.wiki.fextralife.com/Combat, https://samurai-gamers.com/sekiro-shadows-die-twice/posture-system/ |
| Lies of P | Elites and bosses glow red before Fury attacks; a white border on the health bar shows an enemy close to Groggy; a charged blow then opens a Fatal Attack | A "nearly broken" state you can see, cashed in by a heavy blow | https://liesofp.wiki.fextralife.com/Combat, https://segmentnext.com/lies-of-p-red-attack |
| Elden Ring | Delayed attacks and long strings punish panic rolls, and players complain when they feel arbitrary. Enemies react to the player drinking a potion **(unverified: a dataminer report, not read in full)** | Holds exist here already: keep them sparing. React to what the player shows (guard up, backing off), never to healing | https://automaton-media.com/en/news/20220316-10577/, https://www.pcgamer.com/elden-ring-dataminer-explains-why-enemies-smack-you-when-you-reach-for-a-potion |
| Hades II | Armoured enemies are outlined yellow with a yellow bar and can't be stunned or knocked back until it breaks | An armour layer on elites that reads at a glance | https://primagames.com/gaming/how-to-break-enemy-armor-in-hades-2, https://hades.fandom.com/wiki/Gameplay_mechanics/Hades_II |
| Diablo IV | Bosses can't be crowd-controlled; crowd control fills a stagger meter under the health bar, and when it is full everything lands at once | Heavy, crowd-control-like blows build to a guaranteed window | https://www.purediablo.com/diablo4/Stagger_Meter |
| Path of Exile 2 | Heavy stun builds up under the life bar. Past a threshold by rarity the monster is **Primed**, and a crushing blow stuns it at once. The thresholds (40% normal, 60% magic) are **(guide-level)** | "Reeling" at the hammer's Crush line | https://www.poe2wiki.net/wiki/Primed, https://mobalytics.gg/poe-2/guides/stun |
| God of War (2018) | Exaggerated reactions; hit stop holds both bodies; a stun bar under health opens a stun grab | Reactions can break realism to read; a stun meter with a finisher | https://blog.playstation.com/?p=370399, https://twinfinite.net/guides/god-of-war-stun-enemies-how/ **(guide-level)** |
| Monster Hunter | Each part has its own hidden health and its own reaction (head reels, legs trip); breaking parts shows; enraged monsters vent steam, glow, chain more attacks | Parts and armour that break visibly; fury with a body tell. Rage tells are **(guide-level)** | https://mhworld.kiranico.com/guide/understanding-monster, https://mp1st.com/news/monster-hunter-world-enraged-monsters-guide |
| Sifu | One Structure gauge for both sides; some enemies with a glowing aura get a second wind instead of a takedown **(single source)** | `RisesAgain` is this already; a tell before it helps | https://blog.playstation.com/2021/11/18/how-sifus-kung-fu-combat-works/, https://www.looper.com/764019/sifu-tips-to-know-before-you-start-playing/ |
| Ghost of Tsushima | Each enemy type (sword, shield, spear, brute) needs the stance that breaks its guard; terrified enemies flee | Guards answered by a specific tool (here: heavy blows and Guard Breaker); morale | https://ghostoftsushima.wiki.fextralife.com/Enemies, https://www.redbull.com/gb-en/ghost-of-tsushima-combat-tips-guide |
| Doom (2016) and Doom Eternal | A shared pool of attack tokens, which an on-screen enemy can take from an off-screen one **(secondary)**. Staggered demons glow blue, then orange when a Glory Kill is ready; demons visibly fall apart with damage | Safe Haven's turns already do the first. A glow when a creature can be finished, and damage you can see | https://serfofcinder.itch.io/archetype-portal-to-hell/devlog/738495/enemy-ai-tech-talk, https://twinfinite.net/guides/doom-eternal-glory-kills/, https://www.gamestar.de/artikel/doom-eternal-daemonenzerstoerung-soll-kaempfe-taktischer-machen,3333370.html |
| Readability (Death's Gambit talk) | Readability is two things: the telegraph *before* an attack and expectations *after* it. Use colour, gesture and voice per stage | Show the recovery window, not only the wind-up | https://www.gamedeveloper.com/game-platforms/designing-for-difficulty-readability-in-arpgs, https://gamedeveloper.com/design/enemy-attacks-and-telegraphing |

**What every game here shares:**
- Enemies have **their own verbs** (guard, recover, enrage, flee).
- A **nearly broken** state is visible.
- **Openings after the attack** are as readable as the wind-up.
- Elites change **how** you fight, not only how long.

Death's Door and Hyper Light Drifter gave little enemy-specific material beyond few enemy types with one clear answer each (review-level),
and `Docs/Combat.md` › Creatures already works that way.

## 4. Recommendations

The rule throughout: **new data defaults to today's behaviour** (weight 0, no state, no flag), so nothing changes until a creature is
given it, and nothing is removed.

### 4.1 An animation set of their own (impact High, effort M, no balance change)

- **Calls and casts:** when Animation State is empty, `MonsterAnimationDriver.OnAttackStarted` (`:157-176`) falls back by
  `MonsterAttackKind`:
  - Howl or Summon plays a new **Call** state (`Spell_Unarmed_Up_Channel` into `_Up_Impact`; `Emote_Roar` for beasts).
  - Projectile or Lob plays a **Cast** state (`Spell_Unarmed_Forward_Channel` into `_Forward_Impact`, or UAL1 `Spell_Simple_Shoot`).

  The Shaman's call, the troll-wife's mend and the Fossegrim's tune then read as "interrupt me" (they are already Interruptible).
- **Draug and Shieldbearer strikes:** override `Strike Chop`, `Strike Lunge` and `Strike Wide` in `Draug.overrideController` with UAL1
  `Sword_Attack`, Stylized `Combat_1H_Attack_Off02` (a ragged backhand) and UAL2 `Zombie_Scratch` (an off-hand rake as a follow-up).
  Give the Shieldbearer its own override with `Combat_Defend_Attack01` and `AttackShield02`. Monster clips are fitted to Hit Delay
  (`CombatMovesets.cs:248-249`), so a swap moves no timing or damage. Judge each at contact, from the side, in Play.
- **Waiting reads as waiting:** circling and standing ready play a ready stance (`Combat_1H/2H/Unarmed/Cast/Defend_Ready`) through a
  `Ready` bool, and strafing gets a 2D blend (Speed, Sideways) with Kevin Iglesias `Walk01_Left/_Right/_Backward`. "Its turn is coming"
  and "it is coming now" then look different from above.
- **A stagger with a length:** `Stagger` loops `Stun01` (or `Combat_Stun`) for its length, instead of the player's knockback.
  `Knockdown` ends in UAL2 `LayToIdle` timed to the stagger's end; `RisesAgain` uses the same get-up.
- **Weapon draw on spotting:** from Idle or Investigating only, `CombatEnter1H01/2H01` plays with the "!". It is a 0.6 s beat that tells
  a sneaking player "seen" and gives a first-strike chance.

**Tools:** `CombatClipKit.MonsterState`, override controllers per creature, `HasState` fallbacks as today. **Co-op:** animation follows the
state and strike messages already sent. **Not the player's move:** every clip is from the never-played list (§1.6), and the verbs are
ones the player lacks.

### 4.2 Openings you can see on the ground (impact High, effort S, no balance change)

In `AttackTelegraph.UpdateFill` (`:160-180`), which already colours Unblockable strikes:
- **Interruptible:** the fill gets a third look (a broken edge or its own colour) when `ActiveAction.interruptible` is set, so the 13
  strikes and Break Through can be learned.
- **Heavy:** Impact ≥ 1.5, or a hit worth ≥ 25% of the player's health, gets a thicker edge and a low rumble at the wind-up start.
- **Recovery:** after the hit, a faint ring at the feet shrinks over the rest of `RecoveryTime` (or `exposedSeconds` and `openAfter`).
  This is the "expectations after" half of readability.
- Colours go in `CombatFeelSettings` beside `UnblockableColor`.

### 4.3 Weighted, situational strike choice (impact High, effort S-M, no balance change: cooldowns kept)

New fields on `MonsterAttack`:
- **Weight:** 0 keeps today's pure priority. Above 0, priority picks the band and weights choose within it.
- **Repeat penalty:** each use halves the weight for the next pick.
- **When Foe** flags, from what the player can see they are doing:
  - **Guarding** (guard up over 0.8 s in front): the Draug's shove (UAL1 `Push_Loop`, high poise against the guard) or the
    Shieldbearer's Shield Rush.
  - **Backing off:** the lunge.
  - **Shooting:** close in.
  - **Behind:** a backhand.
- Never react to eating or healing (§3).

`SelectAttack` stays static and testable (`MonsterBehaviorTests`). The host chooses, and the strike message carries the result.

### 4.4 Reeling: poise you can read (impact High, effort M, slight balance change)

- **The state:** below `ReelingShare` of poise (0.3, the line the hammer's Crush already uses), a creature sways at half the stagger's
  amplitude, shows an ember crack at the chest, pulses its poise bar and breathes hard. It keeps fighting. This is PoE2's Primed, the
  Lies of P border and Doom's glow.
- **The payoff:** a heavy, charged, art or guard-counter blow on a Reeling creature breaks its poise and fells it. Humanoids play
  Knockdown then the get-up; procedural bodies play a full recoil. Today felling needs a charged blow or 75% of poise. This teaches the
  rework's heavy and guard-counter verbs; regeneration stays, so pressure matters, as in Sekiro.
- **Co-op:** add one poise byte to `CreaturePoses` (19 → 20 bytes). That also fixes the friend's bar (§1.7).
- **Balance:** knockdowns come a little sooner; damage and health don't change.

### 4.5 Enemy guard verbs (impact High, effort M-L, needs the user's sign-off)

- **(a) The Shieldbearer's shield wall:**
  - A blocked blow plays `BlockShield01 - Hit` (or `Combat_Defend_Hit`) and a small shove back. Today a blocked blow skips the body
    reaction (`MonsterAI.Reactions.cs:167`).
  - `BlockShield01 - Loop` plays while it walks in.
  - Poise broken through the shield is a **Guard Broken** stagger: 1.5× as long (`Stun01`), with the guard-break crash. The axe's Guard
    Breaker becomes the clear answer, as Ghost of Tsushima's stances are.
- **(b) A reactive guard on armed undead** (Draug, knights; tier II and up):
  - **Trigger:** after N light blows in T seconds while not striking, it raises a weapon guard (`Parry1H01_R - Loop`, `Parry2H01`) for at
    most 1 s.
  - **While it is up:** light blows from the front are deflected (the parry Hit clip, a spark, a share through) but still cost poise.
  - A heavy, Guard Breaker, art or flank breaks it.
  - A third light blow into it draws a quick answer, with a wind-up of at least 0.45 s and its telegraph.
- **Data:** a `MonsterDefinition › Guard` block and `MonsterAI.State.Guarding` (added at the end of the enum). The state travels in the
  pose stream.
- **Not the player's guard:** no timed parry and no hold. It is a short stance with a fixed rule, played from parry and block loops the
  player never plays (the player guards with UAL2 `Sword_Block` and `Idle_Shield_Loop`).
- **Balance:** it lengthens fights for light-swing mashers. Keep the guard up under about 15% of a fight, and measure with the encounter
  model and `RaidTierTests`.

### 4.6 Reactions by body type and direction (impact Med, effort M, no balance change)

- **By direction:** `MonsterAnimationDriver.OnDamaged` (`:178-204`) picks by the blow's direction: front `Hit_Chest`, a head-high heavy
  blow `Hit_Head`, from behind a forward stumble (`CombatDamage02`).
- **By body type:** casters play `Combat_Cast_Hit` or `Spell_Unarmed_Hit`; the shield plays `Combat_Defend_Hit`.
- **After contact:** allow an arms-only flinch in recovery: a visual "punished" that cancels nothing.

### 4.7 Hurt states for ordinary creatures (impact Med, effort S-M, slight balance change)

A `MonsterDefinition › When Hurt` block: at a share of health, one of these happens.
- **Fury** (trolls, wolves): a call state, steam or breath, brighter eyes (`CreatureLook.SetEyes`), cooldowns ×0.85; wind-ups unchanged.
- **Desperate** (Draug): its Below Health strike unlocks, with a tell.
- **Flight:** Cowardly, with a call.

Monster Hunter's rage tells are the model. This is limited to the last 25-30% of health.

### 4.8 Elite affixes that change verbs (impact Med, effort S each, sign-off)

New `EliteBehaviour` kinds (affixes already reach friends with the spawn):
- **Armoured** (Hades): the first 30% of health can't be flinched, interrupted or staggered. It shows yellow, and breaking it is a long
  stagger with a crash. It is taken from health, so hits to kill hold.
- **Vigilant:** starts with backstab memory.
- **Relentless:** always takes its follow-ups and holds.

### 4.9 Armour that falls off, a shield that breaks (impact Med, effort M, slightly easier)

- Armour chips (`CreatureWounds`) also hide named parts on the prefab (helmet, pauldron, bark, stone hide) and throw them, as in Monster
  Hunter's part breaks and Doom Eternal's destructible demons.
- The Shieldbearer's shield splinters after two Guard Broken staggers (Shield Arc goes to 0 at runtime).
- Co-op: chips go with the hit.

### 4.10 Answers to the bow (impact Med-Low, effort S-M, sign-off)

- Agile humanoids (Huldra, Fossegrim, Tusse) that are the bow's target at full draw, and not striking, may side-step (`Hop`,
  monster-only) on a cooldown. Armoured ones raise an arm (NRftW's evasion against projectiles).
- A perfect release is never evaded, and the chance stays low: the bow already trades pace for range. (Corrected: 1.22 is the runic
  staff's pace, not the bow's; the bow's numbers by tier are in Docs/RangedStealthResearch.md §1.1.)

### 4.11 Enemies reacting to each other (impact Low-Med, effort S, slightly easier)

- A summoner's death routs its summons for 4 s.
- A finishing blow or a staggered backstab kill within 8 m makes ordinary creatures waver for 0.6 s (Ghost of Tsushima's terrify, in
  small).

## 5. Phased plan

| Phase | Contents | Balance |
|---|---|---|
| A: readability (S) | 4.2 telegraph tiers and recovery ring; 4.1 Call and Cast fallbacks, ready stances, stun loop and get-up; the co-op poise byte and the Reeling *look* (4.4 without the payoff) | None |
| B: identity (M) | 4.1 Draug and Shieldbearer strike clips, strafe blend, weapon draw; 4.3 weighted choice; 4.6 reactions; 4.11 | None or easier |
| C: answers back (M-L, user sign-off) | 4.4 Reeling payoff; 4.5 guards; 4.7 hurt states; 4.8 elite verbs; 4.9 breaks; 4.10 bow answers | Measured |

**Verify each step:**
- Edit Mode tests: `MonsterBehaviorTests` for selection, and new pure rules for Reeling and guard triggers, as `GuardRules` is tested
  today.
- Run them through `RunTests.ps1` (memory).
- Live probes with `sh_spawn`, invulnerable and with the clock extended (memory: raid probes).
- Clips checked at contact from the side.
- Phase C also needs `RaidTierTests`, the tier I encounter model (`Docs/GearScoreAndRaidTiers.md`) and the playtest Combat rows
  (interrupts, parries, guard breaks), to see whether fights shift away from parry-waiting.

## 6. Constraints honoured

- **Enemies don't mirror the player:**
  - Only clips the player never plays; the left-hand mirrors are left out.
  - Verbs the player lacks (call, stance guard, reel, get up, flee). No dodge rolls, player-style chains or timed parries.
- **Nothing removed:** every addition defaults to today's behaviour, and `HasState` fallbacks stay.
- **Raid-tier balance:** no health or damage changes, so hits to kill (about 5) and to die (about 13) hold. Items that change a fight's
  length (4.4 payoff, 4.5, 4.7, 4.8, 4.10) are flagged for sign-off and measured; Armoured takes from health.
- **Readable from a top-down camera:** body-sized poses, ground rings and fills, colour, light and sound. No facial cues; the HUD stays
  small.
- **Co-op:** the host decides. New states travel in the pose stream, poise as one byte, chips with hits.
- **Working rules:** targeted edits through `CombatClipKit` with a before and after diff (the builders are retired). Feel numbers are the
  user's.

**Unsure or unverified:**
- The circling slide and the friend's full poise bar come from reading the code, not from seeing them in Play.
- Single-source web claims are marked in §3.
- How each suggested clip looks on these rigs is untested: Quaternius clips need their import fix, and the Draug's proportions differ.

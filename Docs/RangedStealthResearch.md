# Ranged and stealth research: the bow, the runic staff and sneaking

Research date: 2026-11-15, after the melee rework (`Docs/CombatResearch.md`) and during the enemy work (`Docs/EnemyCombatResearch.md`,
phase C being built). This was read-only: no Unity, no CLI, no project edits apart from this file. Paths are relative to
`d:\Safe Haven\Safe Haven`, and `file:line` refers to the `test` branch today (scripts under `Assets/_Project/Scripts/`).

**The question (the user):** "look at stealth and ranged combat, like archery and staff in this game. Use the same method you used to
analyse combat; that one mainly focused on melee combat."

## Status (2026-11-15): built

The user: "Do all changes, great suggestions." Every recommendation (§3.1-3.11) was built the same day on `test`, checked by Edit Mode
tests (`ArcheryTests` with a per-tier bow band, `PerceptionTests`, `MonsterBehaviorTests`, `CoopWireTests` at wire format 24) and
Play probes (the noticing fill and cone, noise and unseen rings, an ambush kill heard 4 m and a watcher turning suspicious, Hunter's
Mark's ring and ×1.5 poise, the staff's aim line, cast target and spell hits). Not played by hand.

Choices made where the report left them open:
- §3.5: Option B, a bow Draw Weight (Crude 1.5, Hunter's 0.95, Elderwood 1.25 with the Hunter's 0.85 s draw, Moonstrung 1.95,
  Trollhorn 2.4), and the perfect release's extra multiplier set to 1 on every bow, so its reward is the sure critical. Full draws let go
  late come to 47-55% of the tier's sword, all-perfect to 86-93% (the test holds 45-62% and 80-98%). No new arrows.
- §3.6: Escape Shot costs 25 Focus on every bow; Hunter's Mark lasts 6 s at ×1.5 poise (Combat Technique Settings).
- §3.8: the point-blank fan stays, but a body struck again by the same cast takes half (`CastVolley.RepeatShare`).
- §3.4 and §3.7 are on in `PerceptionSettings`: Ambush Kill Noise 4 m, Ambush Death Cry 0.3, Ambush Kill Fighting off, Ambush From
  Behind When Suspicious on, Sees Kill Range 9 m, Heard Awareness Far 0.2.
- §3.10: `Concealment` (×0.5 visibility while sneaking) on the Misty Woods shrubs (Broadleaf, Forest Shrub A and B, Young Cedar Bushy).
- §3.11: both bird profiles call a 14 m alarm when flushed.
- §3.9: friends' sounds over 3 m (at most 5 a second), a Drawing flag in the look state, the noticing bytes, and visual-only arrows and
  bolts for friends. The enemy answer to a friend's bow reads Drawing; the arrow side-step still answers only the local bow.
- §3.1's "rim darkens" became a dark ring at the feet (no rim shader to darken).

## 0. Summary

**The three biggest findings:**
1. **The bow stops growing at tier II, and its pace rests on the perfect release.**
   - The arrow sets the damage, and the best arrow is the tier II Bodkin (22).
   - A perfect release is also a forced critical, so ×1.875, not the "25% harder" in `Combat.md` › The bow.
   - All-perfect, the Hunter's Bow (II) beats a Copper Sword (123%), then falls to 81%, 64% and 59% of the tier's sword. Without
     perfect releases it does 22-57%.
   - "1.22 against 1.6" is the **staff's** figure, though the brief and `EnemyCombatResearch.md` §4.10 quote it for the bow. Bows
     aren't in `GearBalance` or `RaidTierTests` (§1.1).
2. **Stealth has no grace and no second kill.**
   - Any heard sound gives a "?" (Heard Awareness Far 0.35 = Suspicious At 0.35), and a "?" ends the ambush, so it is gone the moment
     a sign shows.
   - The first blow makes 13 m of noise at the victim, and a kill adds a death cry, so one ambush is all a group allows. Silent Takedown
     is loud.
   - Nothing hides the player but solid cover, night and fog, and no cone is shown.
   - In 593 cheat-free playtest minutes, Sneaking earned 82 XP and Archery 17, against 1,215 for Melee (§1.6).
3. **The staff is a melee chain that shoots, and its bolts skip what a swing gets.**
   - As monster projectiles, the bolts get no rhythm or charge multiplier, no skill scaling (they train nothing), no sneak critical and
     no aim line.
   - It deals 53% of an Iron Sword's damage per second, and its held fan is strongest point-blank.

**Co-op:** the host's creatures never hear a friend (noise isn't sent), so a friend's ambushes can chain where the host's can't. The
enemy answers to the bow read only the host's bow.

**Ranked recommendations** (detail in §3):

| # | Recommendation | Impact | Effort | Changes balance? |
|---|---|---|---|---|
| 1 | Stealth you can read from above: the fill before the "?", the noticing creature's cone on the ground, noise rings, an "unseen" tint | High | S-M | No |
| 2 | An ambush prompt on the target: the bow outline and the focus ring say "unaware", and a skull says "this blow kills" | High | S | No |
| 3 | Bow, cast and stealth rows in the playtest log | High (enables tuning) | S | No |
| 4 | Quiet kills: a clean ambush kill makes little noise, so stealth can chain; Silent Takedown becomes silent | High | S | Slight (easier), sign-off |
| 5 | Bow damage that grows with the tiers, and the bow added to `GearBalance` and `RaidTierTests` | High | S-M | Yes, sign-off |
| 6 | Bow verbs: Escape Shot (dodge back at full draw looses the arrow, for Focus) and the signature Hunter's Mark (a perfect release marks for poise) | High | M | Slight, sign-off |
| 7 | Grace on the "?": a suspicious creature struck from outside its cone is still ambushed; faint sounds turn heads without a "?" | Med-High | S-M | Slight (easier), sign-off |
| 8 | The staff as a caster: bolts take the swing's multipliers and the sneak rules and train Runecraft; an aim line and outline; casting counts as shooting | Med | S-M | Yes (up), sign-off |
| 9 | Co-op: friends' noise reaches the host; a "drawing" bit; awareness in the pose stream | Med | M | Co-op harder (a fix) |
| 10 | Places to hide: bracken, reeds and shadow that read from above | Med | M-L | No |
| 11 | Birds that give you away, and can be used as a lure | Low-Med | S | No |

## 1. How it works today

### 1.1 The bow

**What the player sees:**
- Hold to draw while walking at 55-60% speed, then let go. The arc and its landing ring brighten with the draw and turn gold in the
  0.2 s perfect window (`ArcheryTrajectoryView`). The target is outlined red, and arrows leave a trail.
- The aim button halves the spread. A perfect dodge readies a quick shot (60% damage), and a draw out of a dodge starts 35% full
  (`PlayerArchery.cs:383-385`).

**The rules** (`PlayerArchery.cs`, `Combat/ArcheryRules.cs`):
- A quarter draw is the minimum: about 60% speed, 44% damage, triple stray (`ArcheryRules.cs:15-18, 29-44`).
- A perfect release has no stray and ×Perfect Release Damage, and it is a sure critical (`ArrowProjectile.cs:264-269`; crit damage
  +50% by default, `CombatStatsDefinition.cs:54-57`). An unaware target adds ×1.5 and the Ambush Damage stat (`:265-273`).
- Each draw costs 4 stamina (`PlayerArchery.cs:375-379`).
- Bow hits earn Focus (`PlayerFocus.cs:41`), but no bow has an art or a signature.
- Aim assist is 2.5 m around the cursor and 9° with the action camera, out to 45 m (`PlayerArmature.prefab:9529-9531`).
  `Combat.md` › The bow still says 1.5 m and 5°.

**Bows and arrows** (`Data/Combat/Weapons`, `Data/Combat/Arrows`). Bows have weapon damage 0, so the arrow sets the damage:
Crude Stone 14 and Fire 10 (+5/s burn) at tier I, Whistling 6 (heard 25 m: a lure), and Bodkin 22 at tier II. There is no arrow above
tier II.

| Bow (tier) | Draw / reload (s) | Arrow speed | Perfect ×dmg | Arrow damage/s, all perfect | ...full draw, not perfect |
|---|---|---|---|---|---|
| Crude Wooden (I) | 1.1 / 0.7 | 30 | 1.25 | 1.01 | 0.49 |
| Hunter's (II) | 0.85 / 0.5 | 40 | 1.25 | **1.34** | 0.63 |
| Elderwood Longbow (III) | 1.0 / 0.55 | 50 | 1.25 | 1.17 | 0.56 |
| Moonstrung (IV) | 0.9 / 0.55 | 58 | 1.40 | 1.40 | 0.59 |
| Trollhorn (V) | 0.85 / 0.55 | 66 | 1.55 | 1.60 | 0.61 |

All perfect = Perfect ×dmg × 1.5 (the critical) ÷ (draw + 0.05 + reload); not perfect = 1 ÷ (draw + 0.25 + reload). Both leave out
power (the bow scales at 0.8, a swing at 1.0), armour, skills and perks. Against the tier's best one-hander (`CombatResearch.md` §1.3,
no base crit chance):

| Tier | Bow + best arrow, all perfect | Not perfect | Sword (damage × 1.6) | Bow as share of sword |
|---|---|---|---|---|
| I | 14.1 (Crude Stone) | 6.8 | Worn Sword 19.2 | 74% / 36% |
| II | 29.5 (Bodkin) | 13.8 | Copper Sword 24.0 | **123%** / 57% |
| III | 25.8 | 12.2 | Iron Sword 32.0 | 81% / 38% |
| IV | 30.8 | 12.9 | Moonsilver Blade 48.0 | 64% / 27% |
| V | 35.3 | 13.3 | Trollstone Blade 60.0 | 59% / 22% |

The Elderwood Longbow (III) is slower than the Hunter's Bow (II). `GearBalance.KitOf` picks the best one-hander by damage
(`Editor/Raids/RaidTierEditor.cs:67-68`), which a bow (damage 0) never is, so raid tiers were tuned without bows.

**Noise.** A loosed arrow makes 3 m at the archer (`PlayerArchery.cs:68, 448-451`). Its impact makes 6 m (Fire 8, Whistling 25) where
it lands (`ArrowProjectile.cs:291`). A hit adds the 13 m combat noise at the victim (`PlayerNoise.cs:147-158`).

### 1.2 The runic staff

- A melee-family weapon (damage 14, tier III) whose chain casts: Staff Bolt and Staff Cast (Frost, 0.8 × damage, 20 m/s, 16 m), then
  Staff Burst (1.3 ×, 3.2 m around, throws back). Σ2.9 ratio over Σ2.38 s gives the documented 1.22: 17.1/s against an Iron Sword's
  32 (53%).
- Held, Staff Lance fans three bolts 12° apart (1.1 × each, 1.1 s recovery). All three strike one body only up close (0.4 m apart at
  2 m, 1.7 m at 8 m), so point-blank it deals about twice the chain's rate (arithmetic, not seen in play).
- Art: Staff Binding (30 Focus), a bolt that leaves a web. Signature: Channel, 1.5× a hit's Focus per cast.

**How a cast works:** `PlayerCombat.Arts.cs:125-156` spawns `MonsterProjectile`s with `damage = activeWeaponDamage` (`:130`), aimed by
the melee assist (35°, about 15 m; `PlayerCombat.Swings.cs:742-743`). `MonsterProjectile` builds plain damage with no technique
(`AI/MonsterProjectile.cs:74-79`), so:
- The swing's own multiplier (rhythm, heavy charge) never reaches a bolt (`PlayerCombat.Swings.cs:473-485`).
- The Melee skill (`:587`), Runecraft's Spell Damage (skills only, `PlayerSkills.cs:153`), the sure critical, Ambush Damage
  (`PlayerProficiencies.cs:131-139`) and Silent Takedown don't apply; the flat ×1.5 on an unaware target does.
- Hits train no skill, only Insight (`PlayerTraining.cs:120-135`).
- There is no aim line or outline, and monsters don't see casting as shooting (`MonsterAI.Tells.cs:121-124` reads `PlayerArchery` only).

### 1.3 Stealth: senses, noise and the ambush

**Senses** (`AI/Perception/*`, `Resources/PerceptionSettings.asset`): a cone (mostly 100-150°) out to 8-18 m by creature; a close
sense of 2.5-6 m that notices from any direction, through cover (`MonsterSenses.cs:93`); hearing ×0.6-1.8. Terrain, trees, rocks and
buildings block sight; bodies, grass and shadow never do (`MonsterSenses.cs:228-232`). Only night (×0.75) and dense fog (×0.55)
shorten it.

**Visibility** (`PlayerStealth.cs:26-45`): moving 1, still 0.75, sneaking 0.45, fighting 1.4 for 3 s after any hit. Sight range scales
with √visibility (`PerceptionRules.cs:37-41`), so a sneaking player is seen from 67% of it. Sneaking is 2.5 m/s, and 1.4 m/s while
also drawing (the scales multiply, `PlayerMovement.cs:235-238`).

**Noticing** (`PerceptionRules.cs:60-82`): 0.6 s near to 3 s at the edge of the cone, 0.35 s inside the close sense, each divided by
visibility × alertness; scans run every 0.25 s (`MonsterAI.cs:72`).

| Sneaking player (0.45) against | Time to "?" (0.35) | Time to "!" |
|---|---|---|
| Draug (alertness 0.8), in its cone at 3.5 m, just outside its close sense | about 1.9 s | 5.5 s |
| Draug, edge of its 6 m sneaking range | about 2.9 s | 8.3 s |
| Any creature, inside its close sense (Draug 3 m, trolls 6 m) | about 0.35 s | 1.0 s |

Approaching from behind, the close sense is the wall. Against a Draug a lunge from 3 m just works. Against a troll's 6 m close sense,
crossing 3 m at 2.5 m/s takes 1.2 s, so a melee ambush on a troll needs the dagger's blink art or the bow.

**Hearing** (`PerceptionRules.cs:89-99`, `MonsterAI.Investigation.cs:14-54`): a sound within radius × hearing lifts awareness to
between 0.35 at the edge and 0.9 at the source. Because Heard Awareness Far equals Suspicious At, **any heard sound gives a "?"**, an
investigation and the loss of the ambush. Radii: walking 5 m, sneaking 1.2 m, dodge 7 m, combat 13 m (plus up to 10 m of build-up),
×1.4 in heavy gear, ×0.7 in rain (`PlayerNoise.cs:88-139`). Spotting calls 10 m around (`MonsterAI.Awareness.cs:147`); a death cries
9 m × size (`MonsterAI.Reactions.cs:296-301`).

**The ambush:** `IsUnaware` needs level < 0.35 and no target (`MonsterAI.Awareness.cs:97-99`). It then takes ×1.5, and blades and
arrows add a sure critical (`PlayerProficiencies.cs:131`): ×2.25. Silent Takedown (melee only, `PlayerCombat.Swings.cs:258-279`)
finishes an ordinary creature if the blow is worth a quarter of its health, but still makes the 13 m noise and the death cry.

**What the player sees:** a "?" from level 0.35, brightening to spotted (`AwarenessIndicator.cs:58-75`); a red "!" and a bell; a
crouch on the body; the HUD noise meter (metres and a word). Never the fill below 0.35, a cone, whether they are in one, how far a
sound reached, or whether a target is still ambushable.

### 1.4 What enemies do about range and stealth

- **Already built (phase B):** drawing or aiming a bow counts as `FoeDoing.Shooting`, which weights a lunge or charge
  (`MonsterAI.Tells.cs:121-124`). Ranged attackers need a clear shot past bodies (`MonsterSenses.cs:154-189`). The Barrow Shieldbearer's
  shield takes 75% of arrows from the front.
- **Being built now, uncommitted:** `MonsterAI.Answers.cs:301-330`. The nimble side-step a bow held past its perfect release, once per
  draw, never a perfect release (`ArrowDodge` on the Monster Definition). The same file adds felling, shield breaks, weapon guards, hurt
  states and elite affixes (Unyielding, Watchful, Relentless).
- **None of it reads the staff or a friend's bow.**

### 1.5 Co-op

| What | On the wire today | Effect |
|---|---|---|
| A friend's visibility | One byte in `PlayerLookState` (`:43-44`), sent when it moves by 40/255 | Host creatures see sneaking friends correctly |
| A friend's noise | **Nothing** (no `WorldNoise` in `Multiplayer/`) | Host creatures never hear a friend's steps, shots or fights; a friend's ambush makes no 13 m noise, so their ambushes chain and the host's don't |
| A friend's hits | Amount, crit, poise, technique (`CreaturePuppets.cs:448-466`); the host applies its own ×1.5 (`CreatureHost.cs:566`) | Ambush works; the friend's copy judges "unaware" from the state alone (Idle or Returning, `MonsterAI.Remote.cs:54`), so its sure critical can disagree with the host's view (unverified) |
| Awareness fill | Not sent; puppets never sense | A friend sees the "?" only once the host's creature is Investigating, at half strength |
| Arrows and bolts in flight | Not sent (no projectile message found) | A friend sees the draw (animator mirror) but not the arrow (unverified) |
| Enemy answers to the bow | Read `PlayerArchery` on the target | Only against the host |

### 1.6 Real play data

`Playtests` (under `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven`) holds 1,220 CSVs, most of them probes.
- **Combat rows** (since 2026-11-15): only 34, all melee or staff; bow shots aren't logged as Combat rows at all.
- **Skill XP** in cheat-free runs of 15 minutes or more (18 runs, 593 minutes; five log skills): Melee 1,215, Sneaking 82 (3 runs,
  about ten ambushes at most), Archery 17 (one run).

**Too little to judge feel, enough to say the bow and sneaking are barely used.** Nothing records perfect releases, misses, ambushes
lost or how players got spotted.

### 1.7 Gaps

1. No bow damage step after tier II; a slower tier III bow; bow balance unmeasured.
2. The bow's value swings 2.2× on one 0.2 s window (a perfect release is ×1.875); docs say 25%.
3. No bow art or signature, though it earns Focus: no answer to a wolf at its feet but a dodge.
4. Staff bolts skip the damage path, skills, sneak rules and training; no aim line; strongest point-blank.
5. Any sound gives a "?", the "?" ends the ambush, and the fill before it is invisible.
6. One ambush per group, and Silent Takedown isn't silent.
7. Only solid cover hides; no cone, hidden state or noise on the ground.
8. Co-op: friends are silent, enemy answers read only the host's bow, awareness isn't shared.
9. No telemetry for ranged play or stealth.

## 2. What strong games do (web research)

The sources were gathered on 2026-11-15. Several wikis refused to load (fandom, mobalytics, poe2wiki, GDC Vault), so some rows rest on
the next-best source. **(unverified)** marks a single community, wiki or forum source, and **(datamined)** marks poe2db.

| Game | Technique (numbers where published) | Take for a top-down co-op game | Source |
|---|---|---|---|
| No Rest for the Wicked | Basic arrows went Focus → Stamina (EA Patch 1) → Focus (The Breach) and back to cheap after a revolt. Together Patch 1 added falloff and gravity; players said the range "isn't visually clear" (unverified) | Keep the basic shot cheap and spend the scarce resource on the special. Show any range limit on the aim line | forum.norestforthewicked.com/t/patch-notes-early-access-patch-1/9963 ; …/t/patch-notes-the-breach-hotfix-1/15380 ; …/t/patch-notes-together-patch-1/25662 |
| NRftW stealth | Players: no in-between suspicious state, "no idea how stealthy I am", spotted from beyond the screen edge, patrols faster than sneaking (forum) | The same camera problem as here: show the build-up, never detect off-screen unseen | forum.norestforthewicked.com/t/stealth-sneaking-feedback/2954 |
| Hades | Power Shot: release "when the bow flashes"; players say the window opens just before the flash, and rumble helps (unverified). Chiron: the Special homes on the last target hit | Put the perfect cue at the window's start (as here) and add a rumble. "Mark with one shot, cash in with another" reads from above | hades.wiki.fextralife.com/Coronacht,+the+Heart-Seeking+Bow ; steamcommunity.com/app/1145350/discussions/0/4357872738332054877 |
| Hades II | Hold to charge an Omega version that costs Magick, vulnerable while charging; Argent Skull ammo stays on the floor until reclaimed | Tap free, hold paid. Ammo you fetch keeps the shooter in the fight | game8.co/games/Hades-2/archives/453563 ; hades2.wiki.fextralife.com/Argent+Skull |
| Path of Exile 2 | Bow skills usable while moving at −70% speed (datamined). Escape Shot leaps back 6 m (0.3.0) and chills where you stood | One disengage shot that carries control. Walking while drawing (here 55-60%) is generous already | poe2db.tw/us/Bow_Shot ; poe2db.tw/us/Escape_Shot |
| Diablo IV | Rogue: basic skills build 3 Combo Points, core skills spend them. Concealment: 5 s stealth, +30% speed, attacking ends it (16-20 s cooldown) | Build and spend gives ranged play a rhythm. ARPG stealth is a cooldown that sets up an opener | maxroll.gg/d4/resources/rogue-specialization ; purediablo.com/diablo4/Concealment |
| Death's Door | Bow and spells cost magic nodes; each melee hit refills one | Tie ranged ammo to melee so the archer doesn't kite forever | kotaku.com/13-things-i-wish-someone-told-me-before-i-started-death-1848117572 |
| Hunt: Showdown | Arrows can be pulled out (with damage); 1.45 raised minimum-draw damage so a tap shot is a choice | Make the tap worth something. Recovered arrows tie the bow to positioning | pcgamesn.com/hunt-showdown/patch-1-6-2 ; huntshowdown.com/news/developer-insight-balance-changes-coming-in-update-23 |
| Elden Ring | Sorceries are tap, charge or channel (Comet Azur channels about 3 s, fixed aim). Light bows fire out of a roll or sprint (unverified wiki) | Give each staff move one clear commitment: tap, charge or channel | eldenring.wiki.fextralife.com/Comet+Azur ; eldenring.wiki.gg/wiki/Light_Bows |
| Mark of the Ninja | Hidden is "totally binary", and the ninja is drawn dark when concealed. Noise is "a spreading blue ring that says exactly how far that noise will reach". If a guard's behaviour changes, the player must understand why | Rings on the ground for every sound, a hidden tint on the player, a last-seen ghost | gamedeveloper.com/design/-i-mark-of-the-ninja-i-s-five-stealth-design-rules |
| Shadow Tactics | Cone zones: crouch-safe (patterned), seen, and bush zones. On detection only *that* guard's cone shows, and yellow fills from its eyes to you | Show the cone of the one creature noticing you, filling toward you | gamedeveloper.com/design/game-design-deep-dive-dynamic-detection-in-i-shadow-tactics-i- |
| Desperados III | "We always want the players to know why their plan fails"; no hidden rolls in detection, randomness only in the search | Keep detection deterministic | gamedeveloper.com/design/designing-the-real-time-stealth-and-combat-of-i-desperados-iii-i- |
| Hitman | A white on-screen arrow points to a threat becoming suspicious | Edge arrows for a creature noticing you off-screen | feralinteractive.com/en/manual/hitman/latest/linux/ |
| Sekiro, Ghost of Tsushima, Dishonored | White, yellow, red; **yellow still allows the stealth kill** in Sekiro. Tsushima chains 2-3 assassinations. Dishonored separates harmless from threatening sounds (guide-level) | Grace on the "?"; a payoff for a clean chain; faint sounds turn heads | gamewith.net/sekiro/article/show/8499 ; gamepressure.com/ghost-of-tsushima/stealth-ghost-style/z3d68a ; gameranx.com/updates/id/10285/ |
| Assassin's Creed Shadows | The assassination prompt is coloured by result: white kills, yellow wounds, red denied | Colour the ambush prompt by its predicted result | rpgsite.net/news/16590-ubisoft-shares-stealth-gameplay-overview-for-assassins-creed-shadows |
| Thief; Hunt: Showdown sound | Thief's light gem was added because players got "confused signals". Hunt: "sounds need to be readable"; crows give players away | A visible state at the player; flushed birds as an alarm and a lure | gamedeveloper.com/design/building-the-original-i-thief-s-i-revolutionary-stealth-system ; blog.playstation.com/?p=221483 |

**What they share:**
- **Readable state, continuous build-up:** hidden or not, plus a white → yellow → red ladder that fills where you can see it.
- **The player knows why they were seen:** the detector shows itself (its cone, an arrow, a fill toward you).
- **Grace before detection:** a fill you can still answer (Shadow Tactics, Sekiro's yellow, Dishonored's decay).
- **Ranged has one decision per shot:** tap or charge, a timing window, ammo or position, and one disengage verb.
- **Casters commit up front:** a charge or a channel, paid from a resource.

**Not taken:**
- Slowed time for aiming (Horizon, MGSV Reflex): co-op can't slow the world.
- Focus as the cost of the basic arrow (NRftW's backlash).
- Distance falloff: the bow already trades damage for range.

## 3. Recommendations

The rule throughout: **new data defaults to today's behaviour** (a 0 or off setting, an appended enum value), nothing is removed, and
every tell lives on the ground or the body, not in new HUD panels.

### 3.1 Stealth you can read from above (impact High, effort S-M, no balance change)

- **The fill before the "?":** `AwarenessIndicator` (`:58-75`) shows a faint eye filling from 0 to Suspicious At while the local
  player is what it notices (Tsushima's white bar). `MonsterSenses.Look` keeps its best candidate (`Noticing`) beside
  `IsSeeingSomething` (`:104`).
- **The noticing creature's cone:** while it fills on you, a faint ground wedge (`SenseProfile`, range scaled by your visibility) and
  its close-sense circle, drawn like `AttackTelegraph`. Only that creature's cone shows, as in Shadow Tactics.
- **Noise on the ground:** `PlayerNoise.Make` (`:88-93`) raises an event, and a ring of the sound's real radius spreads for sounds over
  about 2 m (Mark of the Ninja). The HUD meter stays.
- **Unseen tint:** sneaking and noticed by nothing, the body's rim darkens. **Edge arrow:** a creature filling on you off-screen
  (Hitman).
- **Co-op:** local, except that friends need the fill: two bytes per creature in the host's pose (level, and whom it notices; §3.9).

### 3.2 An ambush prompt on the target (impact High, effort S, no balance change)

The bow outline (`ArcheryTargetOutline`) turns pale gold instead of red while the target `IsUnaware`, and so does the focus ring
(`PlayerFocusTarget`), shown while sneaking near an unaware creature too. With Silent Takedown, a small skull shows when the blow would
finish it, from the perk's own arithmetic (`PlayerCombat.Swings.cs:266-271`), like AC Shadows' prompt. It answers "still
ambushable?" without the player learning the 0.35 rule. Colours go in `PerceptionSettings` › Presentation.

### 3.3 Telemetry first (impact High, effort S, no balance change)

`PlaytestLogger` rows per played minute, with the weapon:
- **Bow:** Shot, Perfect, Counter, Hit, Miss, Recovered (`PlayerArchery.Shot`, `ArrowProjectile.Strike`).
- **Staff:** Cast, Bolt hit (`PlayerCombat.Arts.Cast`).
- **Stealth:** Ambush landed (`LastHitAmbushed`); Ambush lost (a target turned "?" within 2 s of the player sneaking near it);
  Spotted, by creature, distance and cause (sight, close sense, sound); Lost track.

The perfect-release rate decides whether the bow sits at 57% or 123% of a sword.

### 3.4 Quiet kills, so stealth can chain (impact High, effort S, slight balance change, sign-off)

New `PerceptionSettings` › Ambush settings, each defaulting to today:
- **Ambush Kill Noise** (default 13 m, suggested 4 m) for a blow that kills an unaware creature (`PlayerNoise.OnAnyDamaged`,
  `:147-158`, reading `result.Killed` and `LastHitAmbushed`).
- **Ambush Death Cry** (default 1, suggested 0.3) as a share of the cry (`MonsterAI.Reactions.cs:296-301`).
- **Ambush Kill Fighting** (default on): whether that kill sets fighting visibility (`PlayerStealth.cs:92-97`).

An isolated sentry then dies quietly, but a creature that **sees** its neighbour fall turns suspicious toward the body (a sight check
in `ShakeTheOthers`, the enemy agent's §4.11 code), and a messy fight still wakes the camp. Silent Takedown becomes what it says.
**Balance:** more ×2.25 first blows where players sneak; open fights unchanged.

### 3.5 Bow damage that grows with the tiers (impact High, effort S-M, changes balance, sign-off)

- **Option A:** arrows for tiers III-V (say Barrow-iron 28, Moonsilver 34, Trollhorn 40): data only, fits crafting.
  **Option B:** a bow **Draw Weight** multiplier (`WeaponDefinition`, default 1) in `PlayerArchery.Loose` (`:442`).
- **Targets:** full draws without perfect releases at about 55-60% of the tier's sword (the price of range), all-perfect at about
  85-95%. That trims the Hunter's Bow's 123% (Bodkin 22 → 18, or perfect ×1.25 → 1.1). Give the Elderwood the Hunter's 0.85 s draw.
- **Measure it:** a bow kit row in `GearBalance`, a `RaidTierTests` band for the bow kit's hits to kill, and an
  `ArcheryRules.ShotsPerSecond` helper tested in `ArcheryTests`.
- **Docs:** fix `Combat.md` › The bow (2.5 m and 9°; a perfect release is a critical) and the "1.22" quoted for the bow.

### 3.6 Bow verbs: Escape Shot and Hunter's Mark (impact High, effort M, slight balance change, sign-off for numbers)

- **Escape Shot, the bow's art with no new button:**
  - Today a dodge cancels the draw (`PlayerArchery.cs:740`).
  - New: a dodge away from the aim at full draw, with Focus to pay (`WeaponDefinition` › Escape Shot Focus, default 0 = today), looses
    the arrow as the hop starts, perfect if the window was open.
  - This is PoE2's Escape Shot and Elden Ring's shot out of a roll: the bow's answer to a wolf at its feet, and a use for its Focus.
- **Hunter's Mark, the signature (`WeaponSignature.HuntersMark`, appended):**
  - A perfect release marks the creature for about 6 s, with a thin ring at its feet. It takes ×1.5 poise from every source, and
    damage is unchanged.
  - The archer sets up the stagger that a friend, or their own melee weapon after Z, cashes in. It feeds Reeling (built) and the enemy
    agent's felling payoff.
- **Co-op:** a Mark bit beside the technique the hit message already sends (`CreaturePuppets.cs:465`), and one pose bit for the ring.
- **Balance:** one Focus-gated arrow per dodge, and staggers come sooner, not damage. The enemy agent's side-step never fires on a
  perfect release, so the two fit.

### 3.7 Grace on the "?" (impact Med-High, effort S-M, slight balance change, sign-off)

- **Sekiro's rule:** a suspicious creature struck from outside its cone (its back shows from above) still takes the ambush. Today
  `RefreshAmbush` sets one multiplier for every attacker (`MonsterAI.Awareness.cs:97-114`). This needs `IsUnawareOf(Vector3 from)`,
  read by `CaughtUnaware`, `AmbushScale` and `ArrowProjectile`, behind a setting that defaults off.
- **Faint sounds turn heads:** Heard Awareness Far 0.35 → about 0.2 (data). The outer part of a sound's reach then makes a creature
  turn toward it (`UpdateIdleGaze`) without a "?" (Dishonored's harmless sounds).
- **Co-op:** the host decides from the hit message's `HitPoint` and `Direction`.

### 3.8 The staff as a caster (impact Med, effort S-M, changes balance (up), sign-off)

- **Same damage path:** `Cast()` (`PlayerCombat.Arts.cs:125-156`) passes `LandSwing`'s multiplier and `HitTechnique.Spell` (which
  trains Runecraft, `PlayerTraining.cs:129`). Bolts then apply the sneak critical, Ambush Damage and Spell Damage. Use a player-side
  hook, not changes to `MonsterProjectile`, which the enemy agent edits. Then re-measure the 1.22.
- **Aim you can see:** a straight `ArcheryTrajectoryView` line during a cast's wind-up, and the cast-assist target outlined. Today the
  35° assist can pick a creature the player didn't mean.
- **The point-blank fan:** the user decides whether it is identity ("the staff's shotgun") or should spread from the hand.
- **Casting is shooting:** `MonsterAI.Tells.cs:121-124` (the enemy agent's file) also reads a cast wind-up.

### 3.9 Co-op parity (impact Med, effort M, co-op harder: a fix, sign-off)

- **Friends' noise:** a client's `PlayerNoise.Make` sends sounds of 3 m or more (position and radius, about 13 bytes, at most 5 a
  second) to the host, which emits them.
- **A drawing or aiming bit** in `PlayerLookState`, on `FriendHitbox`, so `ReadFoe` and the side-step answer friends.
- **Awareness bytes** for §3.1, and **cosmetic projectiles** (origin, velocity, kind: about 29 bytes) so friends see arrows and
  bolts. Damage stays with the shooter, as today.

### 3.10 Places to hide (impact Med, effort M-L, no change to fights)

A `Concealment` volume (bracken, reeds, deep shadow under spruce) multiplies a sneaking player's visibility, say ×0.5
(`PlayerStealth.Visibility`, `:26-45`), like Shadow Tactics' bushes and Tsushima's tall grass. The close sense still finds you, so
melee range is never safe. Foliage parts as you pass, and the unseen tint shows. Placement goes through the raid layout
(`RaidLayoutPlan`) under the raid look rules.

### 3.11 Birds that give you away (impact Low-Med, effort S, no balance change)

`Birds.OnNoise` (`Environment/Birds.cs:110-128`) already flushes flocks on noise, "purely visual". A `BirdProfile` › Alarm Radius
(default 0) makes a flock the player flushes emit a `WorldNoise`, guarded against re-triggering. Running gives you away (Hunt's
crows), and a Whistling Arrow into a flock makes a bigger lure. Birds are local, so only the host's flocks alarm the host's creatures.

## 4. Phased plan and how to verify it

| Phase | Contents | Balance |
|---|---|---|
| A: readability and measurement (S-M) | 3.1, 3.2, 3.3; staff aim line and outline (3.8 second item); doc fixes (3.5 last item); the co-op awareness bytes and drawing bit (3.9) | None |
| B: identity (M) | 3.6 Escape Shot and Hunter's Mark with the user's numbers; 3.10; 3.11; casting counts as shooting (3.8) | Slight, Focus-gated |
| C: for the user's sign-off (S-M) | 3.4 quiet kills; 3.5 bow scaling and trimming the tier II peak; 3.7 grace; 3.8 the staff's damage path; 3.9 friends' noise | Measured |

**Verify each step:**
- **Edit Mode tests**, through `RunTests.ps1` (memory):
  - `PerceptionTests`: heard level below suspicious, the behind-grace rule, ambush kill noise.
  - `ArcheryTests`: an `ArcheryRules.ShotsPerSecond` helper per bow; the Escape Shot rule as a pure function.
  - `WeaponIdentityTests`: staff ratio per second with and without rhythm.
  - `RaidTierTests`: the bow kit band, phase C only.
- **Play-mode probes:**
  - `sh_spawn` an idle Draug and a Stone Troll. Sneak from behind and measure the time to "?" and the ambush landing. Kill one of three
    and check whether the other two turn suspicious.
  - Loose 20 perfect and 20 late shots and compare damage (memory: raid probes: invulnerable, extend the clock, timed work in in-game
    coroutines, never `sh_save`).
- **Playtest:** the new Bow, Cast and Stealth rows (perfect rate, ambushes per raid, ambushes lost, causes of spotting), compared before
  and after each phase in the Run Comparison window.
- **Co-op:** a two-client session checks that a friend's footsteps draw a "?" on the host and that the friend sees the fill.

## 5. Constraints honoured and overlaps

- **Nothing removed:** the dodge still cancels a draw when Escape Shot can't pay; settings default to today; enum values are appended.
- **Balance:** 3.4-3.9 are flagged. Phases A and B change no health or damage (Escape Shot is Focus-gated, the mark is poise only),
  and bow numbers are read off a measured kit, as raid tiers are.
- **Top-down:** every tell is on the ground or the body; the HUD gains only an edge arrow.
- **Enemies don't mirror the player:** 3.4 and 3.7 change how creatures perceive, not how they fight.
- **The enemy agent:** 3.4 extends `ShakeTheOthers`; 3.6 relies on its side-step skipping perfect releases; 3.8 and 3.9 touch
  `MonsterAI.Tells.cs` and `MonsterAI.Answers.cs`. Agree on those edits first.

## 6. Unsure or unverified

- **The per-second tables are arithmetic:** they assume a release 0.05 s into the window and leave out power, armour, skills, perks,
  misses and travel time. Melee's "ratio per second" leaves out rhythm. Real perfect-release rates are unknown (§3.3).
- **The staff's point-blank fan advantage** is geometry from the asset numbers, not seen in play.
- **Troll ambush:** the close-sense timings use the 0.25 s scan and may vary by a scan. Whether a sword's step-in reliably beats the
  Draug's 3 m close sense is untested.
- **Co-op:** whether a friend's client and the host can disagree on "unaware" for one hit; whether friends see the "!" and arrows in
  flight; only `Multiplayer/` was searched for a noise relay.
- **Off-screen detection:** whether it happens depends on the camera's framing, not measured here. Sight is at most 18 m (23 m against
  a fighting player).
- **Playtest data:** the logs mix probes and play, and only five long runs have skill rows. The numbers say "barely used", nothing finer.
- **The enemy agent's phase C** is uncommitted (`MonsterAI.Answers.cs`) and may change before this is built.
- **Web sources:** rows marked (unverified) rest on one wiki, guide or forum. No Hades Power Shot window size, NRftW anti-ranged list or
  Hades II Magick cost could be confirmed. GDC talks were not read (no transcripts).
- **The suggested numbers** (arrow damages, 4 m kill noise, ×0.5 concealment, ×1.5 mark poise, 6 s mark) are starting points for the
  user, not tuned values.

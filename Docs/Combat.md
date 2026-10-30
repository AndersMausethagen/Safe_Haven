# Combat

Combat in Safe Haven is deliberate and dangerous. The player is a survivor, not a super soldier: every swing costs
stamina and time, getting hit costs you your swing, and the way to win is to read the enemy, guard or step out of the way
at the right moment, and strike when it is open. Skill is timing and patience, not clicking speed. Feedback is carried
by motion, sound and weight rather than on-screen text.

## Controls

| Input | Does |
|---|---|
| Left mouse / right trigger (tap) | Light swing, starting the moment you press. Pressed again **in time** after a swing lands, the next swing of the weapon's chain |
| Left mouse (hold) | Held on (0.22 s), the light swing turns into the heavy swing while it still winds up: slow, nearly rooted, hard; breaks poise. You pay only the difference in stamina |
| Right mouse / left trigger (hold) | Guard. Raised just before a hit lands: parry |
| Left mouse while guarding | Shield bash with a shield; without one it drops the guard and swings |
| Space / south button | Dodge: a quickstep, a roll or a heavy shoulder charge, by how much gear you wear |
| Using food or drink | Stop and eat: takes a moment, heals over time |
| Left Ctrl / left bumper (hold) | Sneak: half speed, quiet, hard to see. A hit on a monster that hasn't noticed you is an ambush (+50%) |
| Left mouse with a bow (hold, let go) | Draw while moving (slowed), let go to shoot. Let go too early and the arrow stays on the string |
| Right mouse with a bow (hold) | Aim: steadier shots, slower walk, the camera looks further ahead (no guard with a bow) |
| Z | Swap weapons: the weapon in hand and the one held before it (say the axe and the bow). With only one weapon it is put away (fists) and taken out again. Not mid-swing; the torch keeps L |
| Middle mouse | Next kind of arrow in the bag |

## The rules

- **Chains.** Each weapon has a chain of light swings (sword: three; axes: two; spear: three; fists: three). A press
  after a swing has landed continues the chain; the last swing is the **finisher** (wider, harder, more poise damage).
  A press *before* the swing has landed is buffered: the chain goes on, but the press misses the rhythm bonus.
  (Combat Technique › Early Press Fumbles turns mashing back into a fumble that restarts the chain.) A press right as a
  swing recovers is **on the rhythm**: a small damage edge (+5% per step, at most two).
  Swings never get faster.
- **Getting out.** A dodge cuts a swing short at any time. During the wind-up the swing is lost: it deals no damage,
  and its stamina is spent. You move slowly while swinging and hardly at all during a heavy swing. (Combat Technique ›
  Dodge Cancels Windup off makes every swing a commitment again: no dodging until it lands.)
- **Flinch.** A hit that gets through (not blocked) while you wind up loses the swing, and you reel for a moment
  (no attacking, guarding or dodging). Trading blows with a monster is a losing game.
- **Guard.** Blocks hits from the front. Part of each hit still gets through (60% with fists, 35–40% with a weapon,
  10% with a shield) and each block costs stamina. Run out and the guard **breaks**: you reel for over a second.
- **Parry.** Raise the guard within 0.15 s before a hit lands: no damage, no stamina, the attacker staggers and your
  next swing is a **counter** (a critical hit; the sword's is a dashing thrust). Lowering the guard locks it for 0.3 s,
  so tapping it is no free parry. A monster's ground telegraph turns pale and the monster glints exactly when to raise
  it.
- **Perfect dodge.** Dodging through a hit (during the dodge's invulnerability) also readies a counter.
- **Stamina.** 100, back at 26 per second from 0.9 s after the last spend (empty to full in under five seconds): a
  light swing costs 12, a dodge 20 (more for a heavy roll), a shield bash 18, a drawn arrow 4, a block part of the hit.
- **Focus.** Fighting well earns Focus (the gold bar under stamina): each hit that lands (finishers twice as much, heavy
  swings and counters half again), a parry (worth about six hits), a perfect dodge (about four), a block (a little). It
  holds for ten seconds after the last gain, then drains. Skills cost Focus on top of their cooldown (Wolf's Lunge 25,
  Whirling Steel 40, Trollsbane Wrath 60, ...), so they are what good fighting buys, not what opens a fight.
- **Gear weight decides the dodge.** Worn armour, rings and the weapon add up to an equipment load. Light (6 or less): a
  short, cheap quickstep. Normal: a half-second roll. Heavy (13 or more): a slow, costly shoulder charge that barges
  enemies at its end (it breaks their poise), and you move a little slower.
- **Eating is a commitment.** Using food or drink stops you for 1.3 s (slowed, no attacking or guarding). A hit that gets
  through or a dodge stops the meal and the food is kept. Its healing then arrives over six seconds, not at once.
- **Poise and stagger.** Every hit wears down a monster's poise (heavy swings, finishers and counters the most); at
  zero it staggers: its attack is cancelled, it stands reeling and takes 20% more damage. Poise comes back after two
  seconds without being hit. Tough monsters have more (Content Editor › Monsters › Poise).

## The bow

A bow is held in the left hand and shoots arrows from the bag. It trades the melee chain for range and positioning.

- **Draw and loose.** Hold attack to draw, let go to shoot. You keep moving while drawing, at about half speed, facing
  the aim (drawing puts you in combat, so you strafe). A full draw takes the bow's **Draw Time**; after a shot the next
  arrow is nocked in its **Reload Time**, and holding through the reload draws again at once.
- **The draw matters.** A full draw flies at the bow's **Arrow Speed** and deals the arrow's whole damage. Letting go
  early is weaker (at a quarter draw about 60% speed and 45% damage) and strays three times further. Before a quarter
  draw nothing is shot. Holding a full draw for more than 2.5 s makes the arms shake and the aim wander.
- **The arrow decides the damage.** The Crude Stone Arrow does 14 before the player's power and the target's armor; the
  bow's Bow Shot attack sets the type, power scaling, crit and poise. Arrows fall on an arc: the bow aims high enough to
  land on the target, so the arc only shows on long shots.
- **The target in sight is outlined red.** The hostile monster nearest the cursor (within 1.5 m of its spot) or the
  crosshair (within 5°), in the bow's line of sight and in range (45 m), glows red; that is where the arrow flies.
  Without one, top-down arrows fly level at chest height toward the cursor.
- **Perfect release.** Let go within 0.2 s of the draw filling (the draw bar above the action bar turns gold) and the
  shot is critical, flies true and hits 25% harder.
- **Aim.** Hold right mouse: half the spread, you walk at drawing speed, and the camera narrows and (top-down) leans
  further toward the cursor. A bow can't guard or parry; dodge instead.
- **Quick shot.** A perfect dodge readies an instant shot: the next press looses an arrow at once (even mid-reload) for
  60% of a normal shot's damage. "Quick shot ready" shows above the action bar.
- **Sneak shots.** A monster that hasn't noticed you takes the ambush bonus (+50%) from arrows too.
- **Arrows are spent, some come back.** Each shot uses one arrow from the bag. Arrows that miss stick in the ground or
  trees; about half of them can be picked up again by walking over them (for 90 s). The strike is heard: a shot into the
  brush draws monsters to it, away from you. A dodge, a hit that staggers you or eating puts the drawn arrow back.
- **Kinds of arrows** (the middle mouse button cycles; the HUD shows the chosen kind and how many are left):
  Crude Stone Arrow (14 damage); Fire Arrow (10, sets creatures burning for 4 s at 5 per second and lights the fog where
  it lands; burns up); Whistling Arrow (6, heard 25 m around where it strikes: a lure).
- **Long shots** show a faint line along the arc while drawing (from 10 m out).
- **Crafting.** From the bag (By Hand): Crude Wooden Bow from 5 Sticks and 3 Nettle Fiber; 5 Crude Stone Arrows from 2
  Sticks and 1 Small Rocks; 3 Fire Arrows from 3 Crude Stone Arrows and 2 Flax; 3 Whistling Arrows from 3 Crude Stone
  Arrows and 1 Bone. At the Construction Table: the **Hunter's Bow** (Tier II: 0.85 s draw, 0.5 s reload, 40 m/s,
  steadier) from 3 Oak Planks, 2 Nettle Cord and 1 Rough Hide.

## Readable animation

Combat lives or dies on seeing what is about to happen, so the animation is built for it:

- **One body for every animation.** The player is Kevin Iglesias' human (the body the movement and work clips were
  made for); the Quaternius combat clips retarget onto it cleanly (Safe Haven › Characters › Use Human Body for Player).
- **Whole-body swings.** Standing still, swings play on a full-body layer, so the legs step into the blow and the weight
  shifts; that is what reads from a top-down camera. While running only the arms swing.
- **Quick to answer, sharp strike.** Light swings wind up at 0.9 of their clip's speed (fists at full speed), so a press
  is answered at once. Heavy swings and finishers wind up slower, for weight. From the moment a swing hits, the strike
  plays at its own speed (Follow Through Speed) and snaps through.
  Chain swings that aren't followed up blend into their recovery at the matching pose instead of starting over.
- **Monsters coil and lunge.** Monsters lean back, draw back and crouch through their wind-up (slowly, then fast at the
  end), snap forward at the hit and settle; staggered monsters sway and sag (`AttackPose`, on top of any animation).
  Their ground telegraph turns pale with a glint for the last moment: the time to parry.

The Quaternius libraries need their own import setup (Bake Axis Conversion, the rig's root as root motion node, and an
avatar made after that), or every pose comes out rotated; Set Up Combat Animations applies and checks it.

## Feedback

- Each weapon swing draws a slash arc through its reach (gold for finishers, ember for heavy swings, pale blue for
  counters). It sweeps the way the weapon moves in the animation, with its roll: Set Up Combat Animations measures the
  weapon hand at the contact and stores it on the attack (Slash Sweep, Slash Tilt).
- The swing's whoosh plays as the weapon moves, just before it lands (Combat Feel › Swing Sound Lead), not as the
  wind-up starts. A swing that is dodged out of or turned heavy stays silent.
- Monsters recoil from blows: the body tips and is shoved the way the blow went, squashes, and springs back with a small
  overshoot (`MonsterPose`). Strong blows (crits, heavy swings, finishers, counters, kills) rock it hardest.
- Hits spark where they land, with a thump layered for strong hits. Blocks clang, parries ring, guard breaks crash, and
  staggers thud with a ground ring.
- **Fists** draw no arc. They swish (Punch Swing) and land with a dull punch and a puff (Punch Hit, Punch Impact) instead
  of a blade's sparks.
- **Axes** have a deep whoosh and a heavy, low thud (Axe Swing, Axe Hit). Swords use the game-wide blade swing and hit.
- Parries and perfect dodges give a short hitch (a beat of weight, not slow motion). Hits on and by the player stop
  time for a few milliseconds and shake the camera, more for heavy swings and finishers.
- Hurt: the screen edges flash red, stronger for bigger hits; below 30% health they pulse.
- Optional: a chain readout under the player (Combat Technique › Show Combo Hud) and text callouts (Combat Feel ›
  Technique callouts). Both are off by default.

## Where to tune it

| What | Where |
|---|---|
| Chain timing, rhythm, heavy hold, guard, parry window, flinch, poise and stagger | Systems Editor › Player › Movement & Combat › Combat technique (`Resources/CombatTechniqueSettings`) |
| Hit stop, shake, effects, slash arcs (time, height, Slash Brightness), swing sound lead, hit recoil, vignette, telegraph colours, callouts | Systems Editor › Movement & Combat › Combat feel (`Resources/CombatFeel`) |
| A weapon's chain, heavy and counter attack, how well it guards, its sounds and Hit Effect | Content Editor › Weapons |
| An attack's damage, reach, timing, poise damage, lunge, animation and slash colour, sweep and tilt | Content Editor › Attacks & Abilities |
| A monster's poise, hurt and wind-up sounds | Content Editor › Monsters |
| Game-wide combat sounds | Sound Editor › Sound Effects › Game-wide effects |
| Focus gains, how long it holds, eating time and heal-over-time | Combat technique (Focus, Eating and drinking) |
| A skill's Focus cost | Content Editor › Skills › Focus Cost |
| Gear weights for each dodge, and each dodge's distance, time, cost and speed | Systems Editor › Movement & Combat › Movement and dodge (Equipment load) |
| A bow's draw time, reload time, arrow speed, spread, minimum draw, move speed while drawing, perfect release window and bonus, aim spread, quick shot damage and shot attack | Content Editor › Weapons (Bow) |
| An arrow's damage, speed, drop, impact noise, recover chance, burning and what flies | Content Editor › Arrows, and the arrow item's Arrow field |

**Animations** come from the Quaternius Universal Animation Libraries (CC0, `Assets/Quaternius`) and the Kevin
Iglesias packs. **Safe Haven › Characters › Set Up Combat Animations** imports them, adds the combat states to the
player's and monsters' animator controllers, and sets every built-in attack's Hit Delay and Recovery from its clip (it
finds the contact by playing the clip on the player's rig and watching the weapon hand). Rerun it after changing an
attack's Animation Speed. It also builds the **Bow** layer from Kevin Iglesias' *Human Archer Animations FREE* (Load,
Hold, Release and a bow idle, upper body only). Effects are rebuilt with **Safe Haven › Combat › Build Combat Effects**.

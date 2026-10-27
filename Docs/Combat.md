# Combat

Combat in Safe Haven is slow, deliberate and dangerous. The player is a survivor, not a super soldier: every swing is
a commitment, getting hit costs you your swing, and the way to win is to read the enemy, guard or step out of the way
at the right moment, and strike when it is open. Skill is timing and patience, not clicking speed. Feedback is carried
by motion, sound and weight rather than on-screen text.

## Controls

| Input | Does |
|---|---|
| Left mouse / right trigger (tap) | Light swing. Pressed again **in time** after a swing lands, the next swing of the weapon's chain |
| Left mouse (hold) | Heavy swing: slow, nearly rooted, hard; breaks poise |
| Right mouse / left trigger (hold) | Guard. Raised just before a hit lands: parry |
| Left mouse while guarding | Shield bash with a shield; without one it drops the guard and swings |
| Space / south button | Dodge: a quickstep, a roll or a heavy shoulder charge, by how much gear you wear |
| Using food or drink | Stop and eat: takes a moment, heals over time |
| Left Ctrl / left bumper (hold) | Sneak: half speed, quiet, hard to see. A hit on a monster that hasn't noticed you is an ambush (+50%) |

## The rules

- **Chains.** Each weapon has a chain of light swings (sword: three; axes: two; spear: three; fists: three). A press
  after a swing has landed continues the chain; the last swing is the **finisher** (wider, harder, more poise damage).
  A press *before* the swing has landed (mashing) fumbles the chain, so spamming the button only ever repeats the first
  swing. A press right as a swing recovers is **on the rhythm**: a small damage edge (+5% per step, at most two).
  Swings never get faster.
- **Commitment.** Once a swing starts you can't dodge out of it until it lands. A dodge pressed during the wind-up
  goes the moment the swing connects. You move slowly while swinging and hardly at all during a heavy swing.
- **Flinch.** A hit that gets through (not blocked) while you wind up loses the swing, and you reel for a moment
  (no attacking, guarding or dodging). Trading blows with a monster is a losing game.
- **Guard.** Blocks hits from the front. Part of each hit still gets through (60% with fists, 35–40% with a weapon,
  10% with a shield) and each block costs stamina. Run out and the guard **breaks**: you reel for over a second.
- **Parry.** Raise the guard within 0.15 s before a hit lands: no damage, no stamina, the attacker staggers and your
  next swing is a **counter** (a critical hit; the sword's is a dashing thrust). Lowering the guard locks it for 0.3 s,
  so tapping it is no free parry. A monster's ground telegraph turns pale and the monster glints exactly when to raise
  it.
- **Perfect dodge.** Dodging through a hit (during the dodge's invulnerability) also readies a counter.
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

## Readable animation

Combat lives or dies on seeing what is about to happen, so the animation is built for it:

- **One body for every animation.** The player is Kevin Iglesias' human (the body the movement and work clips were
  made for); the Quaternius combat clips retarget onto it cleanly (Safe Haven › Characters › Use Human Body for Player).
- **Whole-body swings.** Standing still, swings play on a full-body layer, so the legs step into the blow and the weight
  shifts; that is what reads from a top-down camera. While running only the arms swing.
- **Slow wind-up, sharp strike.** Each attack's wind-up plays slower than its clip (Animation Speed) so the blow is
  seen coming; from the moment it hits, the strike plays at its own speed (Follow Through Speed) and snaps through.
  Chain swings that aren't followed up blend into their recovery at the matching pose instead of starting over.
- **Monsters coil and lunge.** Monsters lean back, draw back and crouch through their wind-up (slowly, then fast at the
  end), snap forward at the hit and settle; staggered monsters sway and sag (`AttackPose`, on top of any animation).
  Their ground telegraph turns pale with a glint for the last moment: the time to parry.

The Quaternius libraries need their own import setup (Bake Axis Conversion, the rig's root as root motion node, and an
avatar made after that), or every pose comes out rotated; Set Up Combat Animations applies and checks it.

## Feedback

- Each swing draws a slash arc through its reach (gold for finishers, ember for heavy swings, pale blue for counters),
  with a whoosh; hits spark where they land, with a thump layered for strong hits; blocks clang, parries ring, guard
  breaks crash, staggers thud with a ground ring.
- Parries and perfect dodges give a short hitch (a beat of weight, not slow motion). Hits on and by the player stop
  time for a few milliseconds and shake the camera, more for heavy swings and finishers.
- Hurt: the screen edges flash red, stronger for bigger hits; below 30% health they pulse.
- Optional: a chain readout under the player (Combat Technique › Show Combo Hud) and text callouts (Combat Feel ›
  Technique callouts). Both are off by default.

## Where to tune it

| What | Where |
|---|---|
| Chain timing, rhythm, heavy hold, guard, parry window, flinch, poise and stagger | Systems Editor › Player › Movement & Combat › Combat technique (`Resources/CombatTechniqueSettings`) |
| Hit stop, shake, effects, slash arcs, vignette, telegraph colours, callouts | Systems Editor › Movement & Combat › Combat feel (`Resources/CombatFeel`) |
| A weapon's chain, heavy and counter attack, how well it guards, its sounds | Content Editor › Weapons |
| An attack's damage, reach, timing, poise damage, lunge, animation and slash colour | Content Editor › Attacks & Abilities |
| A monster's poise, hurt and wind-up sounds | Content Editor › Monsters |
| Game-wide combat sounds | Sound Editor › Sound Effects › Game-wide effects |
| Focus gains, how long it holds, eating time and heal-over-time | Combat technique (Focus, Eating and drinking) |
| A skill's Focus cost | Content Editor › Skills › Focus Cost |
| Gear weights for each dodge, and each dodge's distance, time, cost and speed | Systems Editor › Movement & Combat › Movement and dodge (Equipment load) |

**Animations** come from the Quaternius Universal Animation Libraries (CC0, `Assets/Quaternius`) and the Kevin
Iglesias packs. **Safe Haven › Characters › Set Up Combat Animations** imports them, adds the combat states to the
player's and monsters' animator controllers, and sets every built-in attack's Hit Delay and Recovery from its clip (it
finds the contact by playing the clip on the player's rig and watching the weapon hand). Rerun it after changing an
attack's Animation Speed. Effects are rebuilt with **Safe Haven › Combat › Build Combat Effects**.

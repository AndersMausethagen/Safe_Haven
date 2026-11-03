# Combat

Combat in Safe Haven is deliberate and dangerous. The player is a survivor, not a super soldier: every swing costs
stamina and time, getting hit costs you your swing, and the way to win is to read the enemy, guard or step out of the way
at the right moment, and strike when it is open. Skill is timing and patience, not clicking speed. Feedback is carried
by motion, sound and weight rather than on-screen text.

## Controls

| Input | Does |
|---|---|
| Left mouse / right trigger (tap) | Light swing, starting the moment you press. Pressed again **in time** after a swing lands, the next swing of the weapon's chain |
| Left mouse (hold) | Held on (0.22 s), the light swing turns into the heavy swing while it still winds up: slow, nearly rooted, hard; breaks poise. You pay only the difference in stamina. Held on further, it waits at the top of its wind-up and charges (up to 0.9 s: +40% damage, +50% poise); letting go strikes |
| Right mouse / left trigger (hold) | Guard. Raised just before a hit lands: parry |
| Left mouse while guarding | Shield bash with a shield (off hand); without one it drops the guard into the weapon art if Focus pays for it, else into a swing |
| Middle mouse | The weapon art (with a bow: next kind of arrow) |
| Space / south button | Dodge: a quickstep, a roll or a heavy shoulder charge, by how much gear you wear |
| Using food or drink | Stop and eat: takes a moment, heals over time |
| Left Ctrl / left bumper (hold) | Sneak: half speed, quiet, hard to see. A hit on a monster that hasn't noticed you is an ambush (+50%) |
| Left mouse with a bow (hold, let go) | Draw while moving (slowed), let go to shoot. Let go too early and the arrow stays on the string |
| Right mouse with a bow (hold) | Aim: steadier shots, slower walk, the camera looks further ahead (no guard with a bow) |
| Z | Swap weapons: the weapon in hand and the one held before it (say the axe and the bow). With only one weapon it is put away (fists) and taken out again. Not mid-swing; the torch keeps L |

## The rules

- **Focus on an enemy.** In a melee fight you face the enemy you are fighting, without a lock-on key: the aim picks it, a ring on the
  ground marks it, and it is kept until another is clearly nearer the aim. A swing still winding up keeps turning toward it. While the
  aim points well away from it (over 100°) you look and swing where the aim points, so you can always turn on something else. Bows
  have their own target. Options › Gameplay › Focus on Enemy turns it off.
- **Weapon arts.** Every armed weapon knows a special attack, paid for with Focus and no stamina: sword 30, dagger 25, one-handed
  axe or club 30, greataxe's whirlwind 40 (all around you), greatsword 35, hammer's ground blow 40 (all around you), spear 30.
- **Backstab.** An enemy struck from behind (a pale wedge on the ground shows where) takes more if it is open. Staggering, it takes a sure
  critical at 1.75 times the damage, on top of the staggered bonus: that is the best blow there is, so parry, roll behind, strike. Caught in the
  middle of a strike, it takes 1.35 times, with no sure critical, and the strike is broken off. Great beasts (Old Moss Troll, Root Guardian, Huldra)
  and elites are only ever open while staggered. A backstab has its own heavy sound, a beat of slow time, a flash and blood out the front.
  One straight out of a dodge (within 1.5 s) gives back half of what the dodge cost.
- **Enemies answer, and learn.** A backstabbed enemy that lives turns on you 0.3 s later, or as its stagger ends: usually a strike, wind-up as
  usual so you can dodge it, otherwise a hop back. It also remembers. Each backstab it took, or saw within 8 m, gives it a 35% chance (at most 90%) of
  seeing the next swing from behind coming while it strikes; it forgets one every 20 s. What it sees it breaks off for: a "!" and a hop aside, untouchable while
  it lasts, or, if it has the hands for it (Draug, Barrow Shieldbearer, Moss Shaman, Small Moss Troll), half the time a parry that throws you back for
  0.55 s. The wedge warms from pale to red as it learns. A staggering enemy never sees it coming.
- **Dodging around an enemy.** With an enemy in focus, a dodge steered toward or past it curves around to the spot behind it, on the side you
  lean, and turns you to face it as it arrives. An attack from there is the rush attack, and a backstab if the enemy is open. Steered away it is
  a plain roll; with no direction held it steps straight back. It reaches about 5 m around in about 0.55 s, faster than a straight dodge (the
  heavy roll is a charge and always goes straight). No dodge is blocked by bodies: it passes through them. The cooldown is 0.9 s (0.72 s for a quickstep); stamina
  is what really limits dodging: about six quicksteps, four rolls or two heavy rolls from full.
- **Sweeps.** A wide swing strikes its targets in the order the blade reaches them, not all at once; narrow swings hardly differ.
- **Chains.** Each weapon has a chain of light swings (sword: three; axes: two; spear: three; fists: three). A press
  after a swing has landed continues the chain; the last swing is the **finisher** (wider, harder, more poise damage).
  A press *before* the swing has landed is buffered: the chain goes on, but the press misses the rhythm bonus.
  (Combat Technique › Early Press Fumbles turns mashing back into a fumble that restarts the chain.) A press right as a
  swing recovers is **on the rhythm**: a small damage edge (+5% per step, at most two).
  Swings never get faster.
- **Rush attacks.** An attack pressed during a dodge or up to 0.3 s after, or while running toward where you aim, is the weapon's rush
  attack: a quick lunge that carries your movement into the blow (the sword's lands in 0.28 s). Pressed during a dodge it waits only for
  the dodge's invulnerability to end, then cuts the rest of the dodge short. After a perfect dodge it is the counter, where the weapon has one.
  The next press goes on with the chain's second swing.
- **Staying in the fight.** Being struck, blocking or dodging a blow puts you in Combat, and you stay there while an enemy within 14 m is
  after you, whether or not you hit back. Five seconds after the last blow with no one after you, it ends.
- **Getting out.** A dodge cuts a swing short at any time. During the wind-up the swing is lost: it deals no damage,
  and its stamina is spent. You move slowly while swinging and hardly at all during a heavy swing. (Combat Technique ›
  Dodge Cancels Windup off makes every swing a commitment again: no dodging until it lands.)
- **Flinch.** A hit that gets through (not blocked) while you wind up loses the swing, and you reel for a moment
  (no attacking, guarding or dodging). Trading blows with a monster is a losing game, unless the weapon is heavy enough: the wind-up of a
  heavy swing pushes through hits with a hammer or maul (40% less damage) and a greatsword or two-handed axe (30% less). Lighter weapons dodge instead.
- **Guard.** Blocks hits from the front. Part of each hit still gets through (60% with fists, 35–40% with a weapon,
  10% with a shield) and each block costs stamina. Run out and the guard **breaks**: you reel for over a second.
- **Parry.** Raise the guard within 0.15 s before a hit lands: no damage, no stamina, the attacker staggers and your
  next swing is a **counter** (a critical hit; the sword's is a dashing thrust). Lowering the guard locks it for 0.3 s,
  so tapping it is no free parry. A monster's ground telegraph turns pale and the monster glints exactly when to raise
  it.
- **Perfect dodge.** Dodging through a hit (during the dodge's invulnerability) also readies a counter.
- **Stamina.** 100, back at 26 per second from 0.9 s after the last spend (empty to full in under five seconds): a
  light swing costs 12, a dodge 25 (a quickstep 15, a heavy roll 35), a shield bash 18, a drawn arrow 4, a block part of the hit.
- **Focus.** Fighting well earns Focus (the gold bar under stamina): each hit that lands (finishers twice as much, heavy
  swings and counters half again), a parry (worth about six hits), a perfect dodge (about four), a block (a little). It
  holds for ten seconds after the last gain, then drains. Skills cost Focus on top of their cooldown (Wolf's Lunge 25,
  Whirling Steel 40, Trollsbane Wrath 60, ...), so they are what good fighting buys, not what opens a fight.
- **Gear weight decides the dodge.** Worn armour, rings and the weapon add up to an equipment load. Light (6 or less): a
  short, cheap quickstep. Normal: a half-second roll. Heavy (13 or more): a slow, costly shoulder charge that barges
  enemies at its end (it breaks their poise), and you move a little slower.
- **Eating is a commitment.** Using food or drink stops you for 1.3 s (slowed, no attacking or guarding). A hit that gets
  through or a dodge stops the meal and the food is kept. Its healing then arrives over six seconds, not at once.
- **Status effects.** Some attacks leave something behind when they get through (never when blocked or parried): **Poison**
  (health drains; an Antidote cures it), **Chill** (slower; a Warming Brew cures it) and **Roots** (held in place for a
  moment, but free to fight). They show above the vitals with their time left. Ticks don't flinch, can't be guarded and
  don't count as a perfect dodge. A new hit restarts the time; they don't stack.
- **Meals and tonics.** Cooked meals give timed bonuses on top of what they restore. Two meals at once: a third pushes out
  the one closest to ending. Tonics from the Cauldron take no meal slot. Both go through the portal with you, so the meal
  before a raid counts, and a failed raid loses them.
- **Poise and stagger.** Every hit wears down a monster's poise (heavy swings, finishers and counters the most); at
  zero it staggers: its attack is cancelled, it stands reeling and takes 20% more damage. Poise comes back after two
  seconds without being hit. Tough monsters have more (Content Editor › Monsters › Poise). The thin amber bar under a
  monster's health shows it (Options › Gameplay › Poise Bars).

## Shields

A shield is held in the off hand, beside a one-handed weapon. Two-handed weapons and bows don't leave a hand free; L swaps it
for the torch and back. Each shield has its own Guard Let Through: Round Shield 22%, Copper-Bound 15%, Iron Kite 8%. Heavier
ones add more to the equipment load, which decides the dodge.

## Creatures

Only two can be closing in on the player with a strike at once, and those strikes start at least 0.35 s apart: the rest circle at about 3.5 m, facing
you, until their next try (0.8 to 1.6 s). Ranged attacks, howls and summons don't wait, and neither do great beasts and elites, though they count.

Some melee strikes go on into a follow-up, aimed anew with its own wind-up and warning, or hold the wind-up for 0.45 s while tracking you, its warning
stopped part-filled, so a roll on reflex comes too soon. Draug and Barrow Shieldbearer chain a cleave into a backswing, and hold a cleave 30% of the time;
Small Moss Trolls chain their shove from Tier II, and hold it 25% of the time.

| Creature | Reads as | How to beat it |
|---|---|---|
| Marsh Light | Pale hovering light, keeps its distance, flees when hurt | Its cold bolts are slow: dodge, then shoot it or run it down |
| Draug | Slow armoured dead with a blade, hard to stagger | Fire hurts it more; bait the lunge, it stands open after |
| Barrow Shieldbearer | A Draug with a shield, slow to turn. Its shield takes 75% of what comes from the front, arrows too, while it hunts | Get behind it or to its side, catch it striking, or go through with a heavy blow or an art |
| Bog Spitter | Squat, spits venom from range | Close in: it is weak up close. Carry an Antidote |
| Blight Boar | Leaves you alone at a distance | Sidestep the charge and hit it while it stands blown |
| Rootling | Small, fast, always several; a grasp can root you | Sweeping swings. Kill the Shaman that calls them |
| Moss Shaman | Hangs back with a staff, calls Rootlings, rallies its side | Reach it first; hitting it interrupts a call |

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
- **Perfect release.** The bow's leather creaks and glints as the draw fills, and the draw bar above the action bar turns gold: let go within
  0.2 s and the shot is critical, flies true and hits 25% harder, with a beat of weight as it goes and a gold trail behind it.
- **Feel.** Arrows leave a trail, which is what shows a shot from the high camera. A loosed bow kicks the camera by the strength of its draw.
  An arrow stays where it struck a body for 12 s, going where that part goes, into the corpse too. A hit earns Focus as a swing does (a critical
  twice as much), so archers can pay for skills. Drawing out of a dodge starts 35% of the way to full: roll, then shoot.
- **Aim.** Hold right mouse: half the spread, you walk at drawing speed, and the camera narrows and (top-down) leans
  further toward the cursor. A bow can't guard or parry; dodge instead.
- **Quick shot.** A perfect dodge readies an instant shot: the next press looses an arrow at once (even mid-reload) for
  60% of a normal shot's damage. "Quick shot ready" shows above the action bar.
- **Sneak shots.** A monster that hasn't noticed you takes the ambush bonus (+50%) from arrows too.
- **Arrows are spent, some come back.** Each shot uses one arrow from the bag. Those in a body that dies have the same chance as below of going back in the bag with its loot. Arrows that miss stick in the ground or
  trees; about half of them can be picked up again by walking over them (for 90 s). The strike is heard: a shot into the
  brush draws monsters to it, away from you. A dodge, a hit that staggers you or eating puts the drawn arrow back.
- **Kinds of arrows** (the middle mouse button cycles; the HUD shows the chosen kind and how many are left):
  Crude Stone Arrow (14 damage); Fire Arrow (10, sets creatures burning for 4 s at 5 per second and lights the fog where
  it lands; burns up); Whistling Arrow (6, heard 25 m around where it strikes: a lure).
- **The aim line** shows the arc while drawing, from a couple of metres out, brightening with the draw and gold in the perfect window, with a ring where it comes down on the ground. A partial draw's arc falls short, as the shot would.
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
- **A build-up, then a strike.** A swing has three speeds: a slower build-up you can read (Animation Speed), a fast strike in the
  last moment before contact (Strike Speed, for Strike Lead seconds), and the follow-through. Set Up Combat Animations works the
  build-up out so the swing still lands at its Hit Delay: changing the shape of a swing changes no damage per second. Swings too quick
  for a build-up (a dagger's stab) play at one speed. The body steps into the blow in its last moments, ending as it lands.
  Chain swings that aren't followed up blend into their recovery at the matching pose instead of starting over.
- **Heavy swings keep their feet on the ground until they strike.** A heavy swing winds up and charges on the upper body over the planted
  legs, and the whole body joins in for the strike. Some clips stride or leave the ground, and holding one of those for as long as the
  button is held leaves the player hanging in the air, so leaps (`Combat_2H_AttackSpecial`, `Combat_2HL_AttackSpecial`) are used only for arts, which
  nothing holds, and play at one speed. `CombatMovesetTests` refuses a heavy swing that leaps.
- **Both hands on two-handed weapons** (`TwoHandGrip`): after the animation, the left arm is bent so the palm closes on the handle, the
  weapon's Second Hand Distance from the right hand. It lets go in a dodge, at work, eating or swimming, and when a follow-through takes the handle out of reach.
- **Each family moves its own way.** One-handed swords keep the Quaternius chain; daggers, one-handed axes and clubs, greataxes,
  greatswords, hammers and spears have their own swings from the Stylized Character pack, retimed to the cadence of the attack they
  were split from, which is also where their damage comes from. So a new clip never moves a weapon's damage per second or gear score.
  The pack's other 2HL swings lead with the butt of a spear, so spears keep the Kevin Iglesias thrust for their first two swings.
- **Humanoid monsters strike and flinch properly.** The Draug, Root Guardian, Old Moss Troll and Huldra play their own strikes,
  fitted so the blow lands at the attack's Hit Delay, and flinch from blows worth 4% of their health or more. Monsters built from
  rigid parts keep their procedural pose.
- **Monsters coil and lunge.** Monsters lean back, draw back and crouch through their wind-up (slowly, then fast at the
  end), snap forward at the hit and settle; staggered monsters sway and sag (`AttackPose`, on top of any animation).
  Their ground telegraph turns pale with a glint for the last moment: the time to parry.

The Quaternius libraries need their own import setup (Bake Axis Conversion, the rig's root as root motion node, and an
avatar made after that), or every pose comes out rotated; Set Up Combat Animations applies and checks it.

## Feedback

- A ribbon follows the blade through the strike (gold for finishers, ember for heavy swings and arts, pale for the rest), so it
  curves the way the animation curves. The older flat arc through the swing's reach is still there, off by default (Combat Feel ›
  Slash Arcs); its Slash Sweep and Tilt are measured from the clip.
- **What a blow sounds like** follows the weapon: swords cut (Blade Hit), axes and greatswords cleave (Cleave Hit, with a body's thump under
  it), daggers, spears and arrows pierce (Pierce Hit), hammers, clubs and shield bashes crush (Blunt Hit), fists thud (Punch Hit); a kill on what
  bleeds adds a splash (Gore Splash). The blades' are Mixkit's recordings (`Tools/Audio/blades.py`; Mixkit Sound Effects Free License: video
  games and commercial use named, not to be redistributed on their own, so take them out if this repository is ever public). The rest, and monsters'
  blows, are mixed from CC0 recordings in `Tools/Audio/combat.py`. A far blow of the player's is heard from at most 12 m, so an arrow's hit always reaches the shooter.
- **Blood.** What bleeds sprays droplets along the way the blow travelled and the blade was sweeping, a mist where it struck, a little
  cast back at the attacker, and stains where the drops come down. How far and how much follow the blow's force: the share of health
  it took, the attack's Impact, and whether it was strong or killing. Colour by creature (Content Editor › Monsters › Blood Color): red
  for the living, sap for what grows, black for the Draug; a Marsh Light has none. Options › Gameplay › Blood turns it off, and sparks
  come back in its place. With blood on, an ordinary hit on flesh draws no sparks.
- **Corpses.** A killed monster's body drops out of it and is thrown by the killing blow, as hard as it struck: jointed limbs on a skinned
  humanoid, jointed parts on one built from rigid pieces. It rests after 4 s, sinks at 14 s, and never blocks the player, the aim or a
  swing. At most 14 lie at once.
- **Bleeding out.** The killing blow throws blood as hard as blood can fly, whatever the blow. Then what bleeds goes on pumping from where it
  was struck for 5 s, in pulses that weaken and slow, flinging blood and stains around the body; and once it lies still a pool spreads out under
  it over about 9 s, as wide as the body lies, with a smaller one under the wound. Pools last 45 s. Amounts: Combat Feel › Blood and Corpses.
- Hit stop and shake scale with the attack's Impact, so a dagger's nick and a hammer's blow land differently.
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
| Chain timing, rhythm, heavy hold and charge, sweep time, swing tracking, focus on an enemy, backstab, guard, parry window, flinch, poise and stagger | Systems Editor › Player › Movement & Combat › Combat technique (`Resources/CombatTechniqueSettings`) |
| Hit stop, shake, effects, weapon trail, slash arcs, markers, blood amounts, corpses, swing sound lead, hit recoil, vignette, telegraph colours, callouts | Systems Editor › Movement & Combat › Combat feel (`Resources/CombatFeel`) |
| A weapon's chain, heavy and counter attack, weapon art and its Focus, how well it guards, its sounds and Hit Effect | Content Editor › Weapons |
| An attack's damage, reach, timing, poise damage, Impact, lunge, animation speeds (build-up, strike, follow-through) and slash colour, sweep and tilt | Content Editor › Attacks & Abilities |
| A monster's poise, blood (whether, and its colour), ragdoll, hurt and wind-up sounds | Content Editor › Monsters |
| Which clip each weapon family plays | `CombatMovesets.cs`, then Safe Haven › Characters › Set Up Combat Animations |
| Game-wide combat sounds | Sound Editor › Sound Effects › Game-wide effects |
| Focus gains, how long it holds, eating time and heal-over-time | Combat technique (Focus, Eating and drinking) |
| A skill's Focus cost | Content Editor › Skills › Focus Cost |
| Gear weights for each dodge, and each dodge's distance, time, cost and speed | Systems Editor › Movement & Combat › Movement and dodge (Equipment load) |
| A bow's draw time, reload time, arrow speed, spread, minimum draw, move speed while drawing, perfect release window and bonus, aim spread, quick shot damage and shot attack | Content Editor › Weapons (Bow) |
| An arrow's damage, speed, drop, impact noise, recover chance, burning and what flies | Content Editor › Arrows, and the arrow item's Arrow field |

**Animations** come from the Quaternius Universal Animation Libraries (CC0, `Assets/Quaternius`), the Kevin Iglesias packs and
the Stylized Character pack (`Assets/StylizedCharacter`). **Safe Haven › Characters › Set Up Combat Animations** imports them, adds the
combat states to the player's and monsters' animator controllers, and fits every built-in attack to its clip: it finds the contact by
playing the clip on the player's rig and watching the weapon hand. For the big Stylized swings the hand's top speed is often the weapon
going up, so there it looks for the hand driving forward or down, and allows for the blade following the hand (`DrivenContact`,
`BladeLag`). It leaves a built Sneak layer and the dodge's distance alone. It also builds the **Bow** layer from Kevin Iglesias' *Human
Archer Animations FREE* (Load, Hold, Release and a bow idle, upper body only). Effects are rebuilt with **Safe Haven › Combat › Build
Combat Effects**, blood and the focus ring alone with **Build Blood and Markers**.

# Combat

Combat in Safe Haven is deliberate and dangerous. The player is a survivor, not a super soldier: every swing costs
stamina and time, getting hit costs you your swing, and the way to win is to read the enemy, guard or step out of the way
at the right moment, and strike when it is open. Skill is timing and patience, not clicking speed. Feedback is carried
by motion, sound and weight rather than on-screen text.

## Controls

| Input | Does |
|---|---|
| Left mouse / right trigger (tap) | Light swing, starting the moment you press. Pressed again **in time** after a swing lands, the next swing of the weapon's chain. A press is kept for the next swing only in the last 0.25 s before the swing can chain; holding the button keeps the chain going |
| Left mouse (hold) | Held on (0.22 s), the light swing turns into the heavy swing while it still winds up: slow, planted, hard; breaks poise. You pay only the difference in stamina. Held on further, it waits at the top of its wind-up and charges (up to 0.9 s: +40% damage, +50% poise); letting go strikes. Held to the full charge, it lets go as the weapon's **charged attack**; held again as a heavy lands, the **heavy follow-up** (where the weapon has them) |
| Right mouse / left trigger (hold) | Guard. Raised just before a hit lands: parry |
| Left mouse while guarding | Right after a parry: the counter. Right after a block (0.6 s): the **guard counter**. Otherwise a shield bash with a shield (off hand); without one it drops the guard into an art: the attuned rune's if there is one, else the weapon's own, paid in Focus or, when Focus is short, in 30 stamina; a plain swing only when neither can pay |
| Middle mouse | The weapon's own art, even with a rune attuned (with a bow: next kind of arrow) |
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
- **Weapon arts.** Every weapon knows a special attack, paid for with Focus alone and no stamina (2026-11-16: the stamina fallback made them far too strong). Arts strike at Art Damage (0.9) on top of the Player Damage (0.7) every blow of the player's takes (Combat Technique Settings › The player's damage).
  Each art moves you: sword 30, a dash 3.5 m through enemies, striking all along the way and ending past them; dagger 25, a blink behind the
  focused enemy and a stab; one-handed axe or club 30, the weapon hurled 12 m, striking what it passes, and back to the hand; greataxe 40, a
  whirlwind you can walk at 40%; greatsword 35, a leap slam that closes 4 m; hammer 40, a quake leap of 3.5 m that throws back; spear 30, a
  javelin thrown 14 m that comes back; fists 25, a jumping uppercut; runic staff 30, a bolt that leaves the black widow's web where it lands. A thrown
  weapon glances off walls and never gets lost. While Focus can pay for the art, the weapon in hand breathes a faint ember light.
- **Art runes.** Eight runes, carved at the Alchemy Table with Runecraft 5, each give one kind of weapon a second art. Used from the bag, a rune
  attunes every weapon of its kind; it stays in the bag, another of the same kind takes its place, and the save keeps it. Attacking out of the
  guard without a shield strikes the rune's art; the middle button keeps the weapon's own. The arts the families had before 2026-11-15 are
  runes now, so none was lost: Rune of the Whirl (sword: a spin), of Many Cuts (dagger: a spin), of the Reaver (axe or club: a low cut), of the
  Vault (spear: a vaulting slam); and new ones: of the Cleaver (greataxe: a leap slam), of the Round (greatsword: a spinning sweep), of the
  Millstone (hammer: a walking whirlwind), of the Knee (fists: a flying knee). A rune's art costs what its family's own art costs.
- **What each weapon does that no other does** (its signature):

  | Weapon | Signature |
  |---|---|
  | Sword | **Counter Chain:** a parry or perfect dodge readies two counters, and their window is half again as long |
  | Dagger | **Assassin:** a backstab on a staggered enemy deals 2.0 times instead of 1.75; dodges cost 80% |
  | One-handed axe, club | **Guard Breaker:** twice the poise against a raised shield |
  | Greataxe | **Momentum:** each landed swing in a row adds 20° to the arc and 15% poise, up to three; it fades a second after the chain's grace without a landed swing, and with a weapon swap |
  | Greatsword | **Hyper Armour:** the chain's finisher and the charged blow push through hits, as a heavy swing does |
  | Hammer | **Crush:** a heavy, finisher or charged blow on an enemy whose poise is below 30% staggers it at once, great beasts too |
  | Spear | **Anti Charge:** a blow on an enemy charging in deals twice the poise and stops the charge (it staggers, unless a boss); thrusts pierce two |
  | Fists | **Flurry:** rhythm stacks to three, each adding a quarter of the blow's poise |
  | Runic staff | **Channel:** every cast earns half again a landed hit's Focus |
- **Backstab.** An enemy struck from behind (a pale wedge on the ground shows where) takes more if it is open. Staggering, it takes a sure
  critical at 1.75 times the damage, on top of the staggered bonus: that is the best blow there is, so parry, roll behind, strike. Caught in the
  middle of a strike, it takes 1.35 times, with no sure critical, and the strike is broken off. Great beasts (Old Moss Troll, Root Guardian, Huldra)
  and elites are only ever open while staggered. A backstab has its own heavy sound, a beat of slow time, a flash and blood out the front.
  One straight out of a dodge (within 1.5 s) gives back half of what the dodge cost.
- **Finishing blow.** Behind a staggered person-shaped creature (Draug, Barrow Shieldbearer), a press locks the two of you together for a stab. It is the
  weapon's own rush in numbers and time, so it pays what a staggered backstab pays. What changes is that nothing can knock you out of it before the blade lands.
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
- **Chains.** Each kind of weapon has its own chain of light swings, no two alike:

  | Weapon | Chain |
  |---|---|
  | Sword | slash, slash, backhand, finisher |
  | Dagger | thrust, twin cut, slash, stab |
  | One-handed axe, club | forehand, overhead, chop |
  | Greataxe | wide, mirrored backhand, cleave |
  | Greatsword | sweep, wide, diagonal finisher |
  | Hammer | cleave, sweep, overhead finisher |
  | Spear | thrust, low sweep, lunge |
  | Fists | jab, cross, hook, front kick |
  | Runic staff | rune bolt, rune bolt, a burst around you that throws back |

  A press after a swing has landed continues the chain; the last swing is the **finisher** (wider, harder, more poise damage).
  A press is kept for the next swing only in the last 0.25 s before the swing can chain (Combat Technique › Buffer Window);
  earlier presses are dropped, so mashing doesn't commit you to a whole chain. Holding the button through a swing keeps the chain
  going. A press kept before the swing has landed misses the rhythm bonus. (Combat Technique › Early Press Fumbles turns an
  early press into a fumble that restarts the chain.) A press right as a swing recovers is **on the rhythm**: a small damage
  edge (+5% per step, at most two). Swings never get faster: the next one still waits for the swing's Recovery Time.
- **Context attacks.** Where you attack from changes the attack, where the weapon has one:
  - **Counter:** after a parry or perfect dodge (attacking out of the guard right after a parry is the counter too, not the art).
  - **Guard counter:** attacking out of the guard within 0.6 s of a block: a hard answer, 1.75 times the poise, never a critical.
    A shield's is Shield Cut; without one it is the weapon's counter, else its heavy swing.
  - **Backstep attack:** out of a dodge at least 120° away from where you aim: the sword's and dagger's backstep thrust, the spear's butt
    strike (it shoves 1.5 m), the fists' push kick.
  - **Heavy follow-up:** holding attack again as a heavy swing lands (sword: a sweep; greatsword: a backhand; spear: a spin).
  - **Charged attack:** a heavy held to its full charge lets go as the weapon's strongest blow (sword: a spinning leap; dagger: a focused stab
    that is always critical; axe or club: a heavy chop; greataxe: a spinning sweep all around; greatsword: a lunging drive; hammer: a leap
    slam all around; spear: an overhead slam).
- **Moving while you swing.** A light swing lets you walk at a share of your speed while it winds up, the arms swinging over walking legs:
  daggers and fists 60%, swords, axes and spears 30%, two-handed weapons 5-8%. Heavy swings, rushes and arts keep the feet planted. Early in
  the wind-up the blow turns toward where you aim (150° a second, over its first 60%), and a charging heavy can be aimed while held. The
  step into the blow takes the last third of the wind-up: 0.8-1.0 m for chain swings, 1.2-1.5 m for finishers. A light swing at the
  enemy in focus when it is just out of reach steps up to 0.6 m further toward it, never past it and not when walking away (Combat
  Technique › Light Reach Assist, 2026-11-26: a third of the axe's swings at wolves that bit and backed off hit nothing). A moment after it lands
  (0.1 s) you walk off at half speed (two-handed weapons a quarter, heavy swings a fifth). Walking off never brings the next swing sooner.
- **Rush attacks.** An attack pressed during a dodge or up to 0.3 s after, or while running toward where you aim, is the weapon's rush
  attack: a quick lunge that carries your movement into the blow (the sword's lands in 0.28 s). Pressed during a dodge it waits only for
  the dodge's invulnerability to end, then cuts the rest of the dodge short. After a perfect dodge it is the counter, where the weapon has one;
  out of a dodge away from the aim, the backstep attack. The next press goes on with the chain's second swing.
- **Staying in the fight.** Being struck, blocking or dodging a blow puts you in Combat, and you stay there while an enemy within 14 m is
  after you, whether or not you hit back. Five seconds after the last blow with no one after you, it ends.
- **Getting out.** A dodge cuts a swing short at any time. During the wind-up the swing is lost: it deals no damage,
  and its stamina is spent. No new attack starts for 0.35 s after that, so dodging in and out of wind-ups can't fish for invulnerability. Out of a
  charging heavy there is no such pause: that is a feint. How fast you move while swinging is in *Moving while you swing* above. (Combat
  Technique › Dodge Cancels Windup off makes every swing a commitment again: no dodging until it lands.)
- **Flinch.** A hit that gets through (not blocked) while you wind up loses the swing, and you reel for a moment
  (no attacking, guarding or dodging), in a flinch that fits how you hold the weapon (one-handed, two-handed, a polearm or bare fists).
  Trading blows with a monster is a losing game, unless the weapon is heavy enough: the wind-up of a
  heavy swing pushes through hits with a hammer or maul (40% less damage) and a greatsword or two-handed axe (30% less), and a greatsword's
  finisher and charged blow do too. Lighter weapons dodge instead.
- **Knockdown.** A single blow worth 35% of your health or more floors you for a second; you can't be hit while you are down (Combat
  Technique › Knockdown Share and Knockdown Seconds).
- **Regain.** Part of a blow that got through (40%) can be won back for 3 s by striking back: every melee blow that lands heals half a point
  per point of damage it deals, up to what is left. What isn't won back drains away. The health bar shows it as an amber stretch. It
  rewards answering a hit rather than backing off to wait for a parry (Combat Technique › Regain).
- **Guard.** Blocks hits from the front. Part of each hit still gets through (60% with fists, 35–40% with a weapon,
  10% with a shield) and each block costs stamina: 0.8 for each point of damage the blow would have done you, after your
  armor and resistances (`GuardRules.BlockCost`), so armor makes a guard last and a raid's tier doesn't price blocking out
  (a tier IV wolf's bite costs 17 in moonsilver, where its raw 61 damage would cost 49). Run out and the guard **breaks**:
  you reel for over a second.
- **Parry.** Raise the guard within 0.15 s before a hit lands: no damage, no stamina, and your next swing is a **counter**
  (a critical hit; the sword's is a dashing thrust). What the attacker does depends on what it is (Content Editor › Monsters › Parry
  Response): an ordinary creature staggers; a great beast, boss or elite isn't floored but loses 40% of its poise and reels back 0.45 s,
  its strike broken off, and staggers only when that breaks its poise, so it takes two parries, or a parry and some hard blows. Lowering
  the guard locks it for 0.3 s; a guard tapped and lowered within 0.5 s that caught nothing stays down 0.6 s, so tapping it is no free
  parry. A monster's ground telegraph turns pale and the monster glints exactly when to raise it. Parrying is one way into a fight, not
  the whole of it: a parry earns about as much Focus as two landed blows.
- **Perfect dodge.** Dodging through a hit (during the dodge's invulnerability) also readies a counter.
- **Interrupts.** The light strikes of ordinary creatures (bites, gores, shoves, the tongue lash, the Rootling's grasp, a runt's strike) are
  broken off by any blow that lands in their wind-up. Any wind-up of an ordinary creature is broken off by a heavy swing, a counter, a guard
  counter, a finishing blow, an art, or a blow worth a quarter of its poise (Combat Technique › Break Through); a great beast's never is. An
  interrupted creature waits 0.5 s before it strikes again, and the break sparks, clangs and holds a beat.
- **Stamina.** 100, back at 26 per second (empty to full in under five seconds) from 0.5 s after a swing, or 0.9 s after a dodge or a block;
  a shorter pause never cuts a longer one short. A light swing costs 12, a dodge 25 (a quickstep 15, a heavy roll 35), a shield bash 18, a
  drawn arrow 4, a block part of the hit. A full charge or an art paid in Focus is never refused on an empty bar.
- **Focus.** Fighting well earns Focus (the gold bar under stamina): each hit that lands (8; finishers twice as much, heavy
  swings and counters half again), a parry (18, about two hits), a perfect dodge (18), a block (a little), and each cast of the runic
  staff (half again a hit). A whole sword chain that lands earns about 50. It holds for ten seconds after the last gain, then drains.
  Skills cost Focus on top of their cooldown (Wolf's Lunge 25, Whirling Steel 40, Trollsbane Wrath 60, ...), so they are what good
  fighting buys, not what opens a fight. Each skill plays its own animation (a cast, a high cast, a spin, a dash, a deep overhead).
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
ones add more to the equipment load, which decides the dodge. A blow blocked on a shield costs the attacker poise (Block Poise:
Wicker and Round 12, Copper-Bound 16, Iron Kite, Moonwood and Trollwall 20), and attacking out of the guard within 0.6 s of a block
is the shield's guard counter, Shield Cut.

## Creatures

Only two can be closing in on the player with a strike at once, and those strikes start at least 0.35 s apart: the rest circle at about 3.5 m, facing
you, until their next try (0.8 to 1.6 s). Ranged attacks, howls and summons don't wait, and neither do great beasts and elites, though they count.

A hit-and-run creature (Monster Attack › Break Away: wolves, Rootlings, Troll Runts) runs off after its strike. The wolf first stands where it
bit for 0.3 s (Break Away Delay), its pale green opening ring shrinking on through the wait, so an answer on reflex lands before it goes
(2026-11-26). The wolf's pounce is a leap of its own (its model's Jump_ToIdle: a crouch over the wind-up, airborne over the run, landing
as it strikes), not its bite, and a blow on a standing wolf turns its head away from the side struck (Idle_HitReact_Left/Right). Running,
it only recoils, as a standing flinch would glide.

Some melee strikes go on into a follow-up, aimed anew with its own wind-up and warning, or hold the wind-up for 0.45 s while tracking you, its warning
stopped part-filled, so a roll on reflex comes too soon. Draug and Barrow Shieldbearer chain a cleave into a backswing, and hold a cleave 30% of the time;
Small Moss Trolls chain their shove from Tier II, and hold it 25% of the time.

**Reading a creature (2026-11-15, Docs/EnemyCombatResearch.md):**
- *The warning on the ground says what to do.* Red fills are ordinary strikes. Blue fills can be broken off by any blow in the wind-up
  (calls, casts, quick shoves): hit them. Purple can't be blocked: dodge. A dark red edge and a low swell mark a creature's great blow
  (a charge, a stomp, a quake). The parry moment still turns the fill pale at the end.
- *The opening shows.* Once a blow is spent, a pale green ring at the creature's feet shrinks over the time it can't act: punish inside it.
- *Reeling.* Below 30% of its poise a creature sways, embers glow at its chest and its poise bar pulses: a stagger is near, so press.
- *Its own moves.* Creatures don't use the player's swings. Casters channel with their arms up (a call) or forward (a cast) and let go
  just before it lands; stones are thrown; the Draug swings ragged and rakes with its off hand, the Shieldbearer bashes with its shield.
  One waiting its turn stands ready and steps round you; a Draug or Shieldbearer that spots you unaware stops a beat to draw.
  Staggered, it is dazed for as long as the stagger lasts. Struck in its own recovery, only its arms flinch.
- *Not the same order every time.* Draug, Shieldbearer, trolls, wolves and boars pick among their strikes by chance, rarely the same
  one twice, and answer what you do: backing off or drawing a bow draws a lunge or a charge, a guard held up a while a shield rush or
  a stomp. Calls and bosses still go by priority.
- *They notice each other.* Rootlings run for a while when their Shaman falls, and a finishing blow or a staggered backstab kill
  makes ordinary creatures nearby falter a moment.

**Creatures answering back (2026-11-15, phase C):**
- *Fell what reels.* A heavy, a charged blow, an art or a guard counter on a reeling creature (poise under 30%) floors it for 1.8 s: it
  falls, lies and gets up. A charged blow, or one worth three quarters of its poise, floors it too. Great beasts, bosses and elites only
  stagger.
- *Shields break.* A Shieldbearer's shield jolts with each blow it takes; breaking its poise through the shield staggers it half as long
  again with a crash, and the second time the shield splinters and falls away.
- *The armed dead guard.* From Tier II a Draug hit by three light blows in two seconds raises its sword for a second: light blows from
  the front glance off (30% gets through, poise still wears), a heavy, an art or a blow from its side breaks the guard, and a third light
  blow into it is answered with a strike.
- *Hurt creatures turn.* Below 30% of their health trolls, wolves and boars rage (a cry, angry eyes, breath, shorter pauses between
  strikes); a Draug turns desperate (a cry, and a three-blow flurry it never uses otherwise); the cowardly cry out as they run.
- *Armour comes off.* Each heavy blow that breaks a piece of a Draug's, Shieldbearer's or knight's armour knocks it off the model.
- *The nimble watch your bow.* A Huldra, Fossegrim, Tusse or Troll Runt may step aside from a bow held on it past the perfect release.
  Release on time and it never does.
- *Elites with a trick.* Unyielding (a yellow ring: unstoppable until a third of its health is gone, then a long stagger), Watchful
  (while it strikes, sees a swing from behind coming from the start and steps aside or parries: beat it from the front), Relentless (every strike a held string).

| Creature | Reads as | How to beat it |
|---|---|---|
| Marsh Light | Pale hovering light, keeps its distance, flees when hurt | Its cold bolts are slow: dodge, then shoot it or run it down |
| Draug | Slow armoured dead with a blade, hard to stagger | Fire hurts it more; bait the lunge, it stands open after |
| Barrow Shieldbearer | A Draug with a shield, slow to turn. Its shield takes 75% of what comes from the front, arrows too, while it hunts | Get behind it or to its side, catch it striking, or go through with a heavy blow or an art; an axe or club breaks its guard |
| Bog Spitter | Squat, spits venom from range | Close in: it is weak up close. Carry an Antidote |
| Blight Boar | Leaves you alone at a distance | Sidestep the charge and hit it while it stands blown, or stop the charge with a spear |
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
- **The target in sight is outlined red** (pale gold while it is still unaware: a shot now is an ambush). The hostile monster nearest
  the cursor (within 2.5 m of its spot) or the crosshair (within 9°), in the bow's line of sight and in range (45 m), glows; that is where
  the arrow flies.
  Without one, top-down arrows fly level at chest height toward the cursor.
- **Perfect release.** The bow's leather creaks and glints as the draw fills, and the draw bar above the action bar turns gold: let go within
  0.2 s and the shot is a sure critical (×1.5 with the base critical damage) and flies true, with a beat of weight as it goes and a gold
  trail behind it. (Before 2026-11-15 it also hit 25-55% harder; the critical is now the whole reward.)
- **Bows grow with the tiers** (2026-11-15, Docs/RangedStealthResearch.md): each bow's Draw Weight multiplies the arrow (Crude 1.5,
  Hunter's 0.95, Elderwood 1.25 and as quick to draw as the Hunter's, Moonstrung 1.95, Trollhorn 2.4). Full draws let go late deal about
  half the tier's sword a second, every release perfect about nine tenths (`ArcheryTests.Bows_GrowWithTheTiersBelowTheSword`).
- **Escape Shot, the bow's art:** dodge away from the aim at full draw and, for 25 Focus, the arrow is loosed as you go (perfect if the
  window was open). Without the Focus the dodge drops the draw, as before.
- **Hunter's Mark, the bow's signature:** a perfect release marks what it strikes for 6 s (a thin green ring); every blow on it, yours or a
  friend's, wears ×1.5 poise, so it reels and staggers sooner. Damage is unchanged.
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

## The runic staff

A two-handed staff that casts instead of striking (GDD §10.4, "magic and control"), made at the Alchemy Table with Runecraft 3 from
3 Bone, 2 Nettle Cord and 2 Orb Tincture. It uses the melee buttons, guard and dodge as any weapon does.

- **Its chain** casts two rune bolts and ends in a burst around you that throws enemies back. Bolts fly straight from the hand as the
  blow would land, strike as the swing would at the staff's damage, and stop at scenery. Held, it casts a fan of three bolts.
- **Its art** (30 Focus) is a bolt that leaves the black widow's web where it lands. Its signature, Channel, makes every cast earn half
  again a landed hit's Focus, so a caster keeps skills and arts coming.
- **Pace.** From range it deals 1.22 times its damage a second, against a sword's 1.6 up close: range is what it trades for.
- **Its look:** its own low-poly model (about 540 triangles, flat colours like the other gear; `Tools/Blender/runic_staff.py`), carried upright in one hand like a walking staff, and its own icon. Its bolt and burst casts lead with the stone end at the hand; whether that reads well is for a playtest.
- **Bolts are spells** (2026-11-15): a cast takes the swing's multiplier (rhythm, a charge), a sneak attack's sure critical, Ambush
  Damage and Runecraft's Spell Damage, and trains Runecraft. A body struck again by the same cast takes half, so the held fan point-blank
  is strong without tripling. Through a cast's wind-up a line shows where the bolt will fly and its target is outlined.

## Readable animation

Combat lives or dies on seeing what is about to happen, so the animation is built for it:

- **One body for every animation.** The player is Kevin Iglesias' human (the body the movement and work clips were
  made for); the Quaternius combat clips retarget onto it cleanly (Safe Haven › Characters › Use Human Body for Player).
- **Whole-body swings.** Standing still, swings play on a full-body layer, so the legs step into the blow and the weight
  shifts; that is what reads from a top-down camera. Started on the move (by a swing that lets you walk), or walked out of after the
  blow, only the arms swing, over walking legs; the step into the blow is always the legs' own.
- **A build-up, then a strike.** A swing has three speeds: a slower build-up you can read (Animation Speed), a fast strike in the
  last moment before contact (Strike Speed, for Strike Lead seconds), and the follow-through. The build-up is worked out when a swing
  is fitted to its clip, so the swing still lands at its Hit Delay: changing the shape of a swing changes no damage per second. Swings too quick
  for a build-up (a dagger's stab) play at one speed. The body steps into the blow over the last third of its wind-up, ending as it lands.
  Chain swings that aren't followed up blend into their recovery at the matching pose instead of starting over.
- **Heavy swings keep their feet on the ground until they strike.** A heavy swing winds up and charges on the upper body over the planted
  legs, and the whole body joins in for the strike. Some clips stride or leave the ground, and holding one of those for as long as the
  button is held leaves the player hanging in the air, so leaps (`Combat_2H_AttackSpecial`, `Combat_2HL_AttackSpecial`) are used only for arts, which
  nothing holds, and play at one speed. `CombatMovesetTests` refuses a heavy swing that leaps. A charged attack is let go, never held, so it
  plays on the whole body and may leave the ground (the sword's spinning leap, the hammer's leap slam).
- **Both hands on two-handed weapons** (`TwoHandGrip`): after the animation, the left arm is bent so the palm closes on the handle, the
  weapon's Second Hand Distance from the right hand. It lets go in a dodge, at work, eating or swimming, and when a follow-through takes the handle out of reach.
- **Each family moves its own way.** Since 2026-11-15 no two families swing the same chain. Most new swings come from Kevin Iglesias'
  *Human Melee Animations* (bought by the user; made for the same body as the player), beside the Quaternius and Stylized Character
  clips already in use: one-handed swords keep the Quaternius slashes with a Kevin Iglesias backhand; one-handed axes and clubs no longer
  borrow the dagger's clips; spears no longer play the same thrust twice (the second swing is a low sweep); greataxes mirror their wide
  swing into a backhand. Every swing is retimed to the cadence of the attack it took its numbers from, so a new clip never moves a
  weapon's damage per second or gear score: each family's stays within 6% of what it was (`WeaponIdentityTests`).
- **Big blows coil further** (2026-11-26, Docs/CombatFeelAnalysis_2026-11-26.md §3.6): the sword's heavy and finisher and the axe's
  heavy and charged blow play copies of their clips (`Animation/Clips/Exaggerated`, states "… X") whose wind-up draws the trunk and arms
  a third again further from where they will be at contact, easing in and out, so each starts on the same pose and meets the same contact
  pose on the same frame. Timings, damage and the blade at contact are the source's. The copies are baked on the player's own rig
  (`SwingExaggeration`); change `SwingExaggeration.Strength` and run Safe Haven › Tools › Exaggerate Big Swings. The sword's charged
  spin-leap (it starts from its backswing top) and the axe's finisher (it left the chain swing before it too little room to blend) keep
  their clips.
- **The player reacts by stance and weight.** A flinch fits the grip (Hit 1H, 2H, Polearm, Unarmed); a blow worth 35% of health plays a
  knockdown on the whole body.
- **Humanoid monsters strike and flinch properly.** The Draug, Root Guardian, Old Moss Troll and Huldra play their own strikes,
  fitted so the blow lands at the attack's Hit Delay, and flinch from blows worth 4% of their health or more. They react by the
  blow's weight: a heavy swing, counter, guard counter, finisher or a blow worth 30% of their poise sends them staggering back (Hit Heavy),
  and a charged blow (or one worth 75% of their poise) that breaks their poise knocks them down. In the middle of a strike they don't
  flinch. Monsters built from rigid parts keep their procedural pose, recoiling harder the more poise a blow takes.
- **Monsters coil and lunge.** Monsters lean back, draw back and crouch through their wind-up (slowly, then fast at the
  end), snap forward at the hit and settle; staggered monsters sway and sag (`AttackPose`, on top of any animation).
  Their ground telegraph turns pale with a glint for the last moment: the time to parry.

The Quaternius libraries need their own import setup (Bake Axis Conversion, the rig's root as root motion node, and an
avatar made after that), or every pose comes out rotated; Set Up Combat Animations applied and checked it, and a new library needs the same.

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
- Hit stop and shake scale with the attack's Impact, so a dagger's nick and a hammer's blow land differently. The struck body shudders
  side to side through the stop, shrinking as it ends (Combat Feel › Victim Shake, 0.045 m). Hosting co-op, where time can't stop for
  everyone, the struck body's animation holds and the attacker's slows instead, so a blow still lands with weight.
- A wind-up broken off by a blow sparks, clangs and holds a beat. A signature at work (a crush, a stopped charge, a broken guard)
  flashes and shakes.
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
- Hurt: the screen edges flash red, stronger for bigger hits; below 30% health they pulse. What Regain can still win back shows as an
  amber stretch on the health bar.
- Art ready: the weapon in hand breathes a faint ember light while Focus can pay for its art.
- Optional: a chain readout under the player (Combat Technique › Show Combo Hud) and text callouts (Combat Feel ›
  Technique callouts, Interrupt Text). Both are off by default.

## Learning it

The combat pages of the journal open one at a time, each when what it is about first comes up: Swings and Chains on the first blow,
Guard and Parry on the first block, Dodging on the first dodge, Stamina and Focus when stamina runs out or on the first art, Poise and
Stagger on the first stagger, Backstabs on the first parry, Getting Hit when first hurt. Pages a save has already announced stay open.
The first three times in a save that the enemy in focus reels, the heavy's key floats over it for a moment ("Heavy [LMB]", a glyph on a
pad; `ReelingHint`): a heavy on a reeling creature floors it.
The pages are written from this document, so a changed rule has to be changed there too.

## Measuring it

The playtest log writes **Combat** rows for each played minute, with the weapon held: swings by kind, blows landed and whiffed,
parries, blocks, broken guards, dodges and perfect dodges, flinches and the swings they cost, counters, backstabs, interrupts and
times hurt. They show how much of a fight is parrying, to compare one playtest with the next. Since 2026-11-26 the move inside a kind
is counted too (`Move.Charged`, `Move.FollowUp`, `Move.Backstep`, `Move.Finisher`, on top of the kind's own row), and why a swing whiffed:
`Whiffed.InReach` (the focused enemy was within reach), `Whiffed.OutOfReach` (it had backed off) or `Whiffed.NoFocus`.

**Heavy and charged pay for the commitment** (2026-11-26, the user: heavies +40%): a one-hander's heavy earns 40-55% more a second than its
light chain (every family's at least a fifth more), and a full charge at least what the chain does, counting the 0.9 s it waited (`WeaponIdentityTests`). In the
11-25 playtests 80% of swings were light because neither paid.

## Where to tune it

| What | Where |
|---|---|
| Chain timing, rhythm, heavy hold and charge, sweep time, swing tracking, focus on an enemy, backstab, guard, parry window, flinch, poise and stagger | Systems Editor › Player › Movement & Combat › Combat technique (`Resources/CombatTechniqueSettings`) |
| Moving while swinging (fallbacks), the turn early in a wind-up, the step in, the buffer window, the lockout after a dodged wind-up, the backstep angle | Combat technique |
| The parry economy (a heavyweight's poise loss and recoil, the missed-parry relock), the guard counter, Break Through, the interrupt pause, knockdown, Regain | Combat technique |
| Hit stop, shake, victim shake, effects, weapon trail, slash arcs, markers, blood amounts, corpses, swing sound lead, hit recoil, vignette, telegraph colours, callouts | Systems Editor › Movement & Combat › Combat feel (`Resources/CombatFeel`) |
| A weapon's chain, heavy and counter attack, context attacks (backstep, guard counter, heavy follow-up, charged), weapon art and its Focus, signature and its Power, how well it guards, a shield's Block Poise, its sounds and Hit Effect | Content Editor › Weapons |
| An attack's damage, reach, timing, poise damage, Impact, lunge, animation speeds (build-up, strike, follow-through) and slash colour, sweep and tilt | Content Editor › Attacks & Abilities |
| How an attack lets the player move (Movement: wind-up and after-contact speed, turn rate, Move At, Lunge Share), how an art moves (dash, blink, leap, throw, and how far), Always Critical, and a staff's Cast (bolts, fan, hazard) | Content Editor › Attacks & Abilities |
| A monster's poise, Parry Response, blood (whether, and its colour), ragdoll, hurt and wind-up sounds | Content Editor › Monsters |
| Which monster strikes can be interrupted | The monster's attack list › Interruptible |
| A rune's art, its Focus and the kind of weapon it fits | The rune item › Art rune |
| The stamina pause after a swing | `PlayerStaminaSettings` › Attack Regen Delay (Regen Delay after a dodge or block) |
| A skill's animation | Content Editor › Skills › Animation State |
| Which clip each weapon family plays | `CombatMovesets.cs` and the animator; new swings go in one at a time with `CombatClipKit` (Set Up Combat Animations is retired) |
| Game-wide combat sounds | Sound Editor › Sound Effects › Game-wide effects |
| Focus gains, how long it holds, eating time and heal-over-time | Combat technique (Focus, Eating and drinking) |
| A skill's Focus cost | Content Editor › Skills › Focus Cost |
| Gear weights for each dodge, and each dodge's distance, time, cost and speed | Systems Editor › Movement & Combat › Movement and dodge (Equipment load) |
| A bow's draw time, reload time, arrow speed, spread, minimum draw, move speed while drawing, perfect release window and bonus, aim spread, quick shot damage and shot attack | Content Editor › Weapons (Bow) |
| An arrow's damage, speed, drop, impact noise, recover chance, burning and what flies | Content Editor › Arrows, and the arrow item's Arrow field |

**Animations** come from the Quaternius Universal Animation Libraries (CC0, `Assets/Quaternius`), the Kevin Iglesias packs (Human
Melee Animations among them since 2026-11-15) and the Stylized Character pack (`Assets/StylizedCharacter`). **Safe Haven › Setup ›
Characters › Set Up Combat Animations** is retired with the other bulk builders (2026-11-14): it rebuilt from a stored copy and could
overwrite newer work. New swings are added one at a time with `CombatClipKit` (a swing state on the player's three swing layers, a
monster state, and the attack fitted to its clip), which keeps the builder's fitting. The builder imported the clips, added the
combat states to the player's and monsters' animator controllers, and fitted every built-in attack to its clip. The fitting finds the contact by
playing the clip on the player's rig and watching the weapon hand. For the big Stylized swings the hand's top speed is often the weapon
going up, so there it looks for the hand driving forward or down, and allows for the blade following the hand (`DrivenContact`,
`BladeLag`). The builder left a built Sneak layer and the dodge's distance alone, and also built the **Bow** layer from Kevin Iglesias'
*Human Archer Animations FREE* (Load, Hold, Release and a bow idle, upper body only). Effects were built with **Safe Haven › Combat › Build
Combat Effects**, blood and the focus ring with **Build Blood and Markers**; both are retired too, so effects are edited directly now.

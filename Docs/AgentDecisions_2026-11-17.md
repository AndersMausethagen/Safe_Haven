# Decisions taken while you were away (2026-11-17, evening → night)

You asked me to carry on through every remaining task, have a world designer and a system designer review the game, implement
their suggestions, and write down my decisions for you to read. This file is that record, newest at the bottom of each section.
Each entry says what I did and why, and what to check.

## Your answers (Raid identity Q6–9)

- 6: the Frost Peaks get a simpler apex first; The Ride later. 7: Sigrun is rescued in the Peaks by the red thread and teaches fur
  gear. 8: the Peaks offer tiers IV and V once the Trollstone kit is measured. 9: one knight per raid. Recorded in
  `Docs/RaidIdentityResearch_2026-11-17.md` › Decisions.

## Decisions

### One knight per raid (Q9)
- **The Valley keeps the Fire Knight, the Marsh the Toxic Knight** (as they already had). The **Misty Woods loses the Fire and
  Toxic knights** from its Creatures and Hunters lists.
- **The Woods keeps the Ice Knight for now**, because the Frost Peaks aren't built: taking it out would leave it unused. It moves to
  the Peaks when they exist. *Check:* if you'd rather the Woods had no elemental knight at all, say so.

### The Heartroot's stage 3: deep dusk
- **Built `ArenaHourShift`** (C8 of the boss review): at 33% the hollow's hour slides from midday to deep dusk (20.4) over 4 s, a green
  light hangs at the Heartroot's heart so its topple opening shows, your own glow comes on with the dark, and the day comes back over
  6 s after the kill. It runs on each co-op game from the boss's stages. Checked in Play.
- **The black-sap drips during the rage and Thornwave are not built yet** (see below as I get to them).

### The "interrupted" events in the fight
- They were the Heartroot's own heavy blows tossing its Rootlings: the ground-impacts rule that a big stomp staggers and nicks the
  slammer's own kind nearby. **Kept as designed**: luring Rootlings next to its slams is a fair trick for the player.

### Thornwave (the Heartroot's stage 2)
- **Built as data**: a thorn-root projectile (the Root Tangle's lightest LOD, 1,439 tris), four in a quick volley, chosen more at a player
  who backs off or shoots, stage 2 only. It flies level at 1 m, so the root walls stop it.
- **Creature bolts now pass through their own kin**, as the player's already passed through companions: the first Thornwaves all died
  on the Rootlings the Heartroot had just called, standing in the line of fire. This applies to every monster's shots (bog spit,
  troll stones, wisp bolts): a pack no longer shields the player from its own archers.

## The system designer's review (Docs/SystemDesignReview_2026-11-17.md)

### Done
- **Quitting mid-raid now counts as a fall (K1).** From the pause menu (to the menu or the desktop) the raid ends as a death: what you
  carry is lost and the gear wears (25% on an expedition, the encounter's 10% on a boss), saved before the game goes; the confirm
  text says so. A **real crash** never reaches the menu, so it still puts you back at the portal as you set out: that's the grace.
  Why: quitting was cheaper than dying and gave a free retry of an "all in" boss. *Check:* you may want the crash grace narrower.
- **The briefing is pinned to the turn (K2).** A destination keeps its seed (hour, rumours, foretold events) until the territory's
  turn moves on at a dawn or a raid's end, so reopening the planner no longer fishes for a night run or a relic rumour.
- **No raid-only items in tithes (K4).** The Wolfwood now pays Rough Hide instead of a Wolf Fang, the Drowning Fen Cloudberries
  instead of Bog Venom; a test keeps raid-only items out of every tithe.
- **Horses make noise in raids (K10).** Each hoofbeat is heard by creatures: walk 2.5 m, trot 6, canter 8, gallop 11, sprint 13.
- **The Field Repair Kit says what it does (K5, option a):** "mends everything you are wearing by a third, wherever you are: cheaper
  than the smith after a raid that went wrong". Light wear in use (option b) is your call (Q4 below).
- A territory test asked every raid's rumours to answer the forest's push; the Valley and Marsh never could. Scoped to forest raids.

### Left for you (the review's questions; I didn't decide these because you've spoken on them before)
- **Q2 one death price** (raids 25%, bosses 10%; it suggests 15% for both). You said "rules should stay consistent… we might lower the
  global cost": your call.
- **Q3 rematch wait in real or game hours.** You said "24 hours"; I kept real hours.
- **Q4 light gear wear in use** (Valheim-like), Q5 **empowering carried forward** (reforge at 50%), Q6 **surplus comfort as rested
  charges**, Q7 **an endgame ladder**: design changes I'd like your yes on. My leanings: Q4 no for now, Q5 yes, Q6 yes, Q7 after the
  slice playtest.
- K3 (the strength-limit test counting every meal/tonic slot) is a tuning job that may cut meal numbers: queued.
- **Q5, a first step done:** breaking down an empowered piece now gives back **a quarter of the Purified Essence** its levels cost
  (the salvage text says so). Full reforging onto the next tier's piece is still yours to approve.
- **Q6 (rested charges) not built:** Well Rested is already given in full at every raid launch (HavenReadiness.GiveRest), so charges
  would change nothing. Surplus comfort only matters if rest had to be earned (e.g. a night at home between raids): a design choice
  for you.

## The world designer's review (Docs/WorldDesignReview_2026-11-17.md)

Its verdict: the maps aren't open squares, but they play like one (trails are only paint, sites are spread evenly, landmarks are
too small for the camera, one character per map). Its plan: districts, landmarks, compounds, barriers with gaps, a dressing pass,
roads. **The generator code for those is being written now** (in order: districts, landmarks with short flags, roads, the Valley's
crossing cap, the dressing pass, then compounds and barriers); I'll set the per-raid data and check each in launched raids.

### Done by me
- **Each raid its own fireflies:** the Valley 90 warm amber ones at night and none by day; the Marsh 240 green ones low over the
  water (will-o'-wisps), 14 by day. The Woods keeps its blue.
- **Heatmaps:** the playtest log now writes the player's position every 2 s in a raid (event `Position`, subject `x;z`), so we can
  see where players really go.
- **Arena dressing:** Guardian's Hollow got five giant cedars beyond the dead ring, root tangles and lichen boulders on its rim (the
  floor stays clear; navmesh rebaked). Brukam's Bridge got 70 crags set into its gorge walls the way the Valley's ravines do it (a
  first try with flat rock faces read as slabs and was removed), and the Brukam's club and old bones by his camp.
- **Not used on purpose:** the Troll Hoard cache in the Brukam's camp (it would add loot to his fight); the low-poly packs the
  review warns against.

### Entities / DOTS (you asked)
- **Not worth adopting now** (the review measured: scripts take about 1 ms; the frame goes to terrain, shadows and plants; co-op is
  Netcode for GameObjects). Revisit with NGO 3.0 / Unity 6.7.
- **The GPU Resident Drawer is on but probably skips the rocks:** their tessellated shader (`MeshSurface`) lacks DOTS instancing.
  Worth adding once the dressing pass puts more rocks out; a shader change with some risk, so it gets its own measured step.
- **Two new Marsh set pieces from props made for it and never placed:** "Bog-Iron Bloomery" (the bloomery ruin with stacked peat:
  bog iron was the marsh's trade) and "The Black Pool Landing" (a hauled-up rowboat, a stretch of boardwalk and the Nøkk's fiddle
  left on it). Both hold rumours. *Not yet seen in a launched raid* (three seeds didn't pick them); generation tests pass.

## Smoother player animation (you: "doing charge attacks in sequence is glitchy. it snaps in between states")
- **Measured first:** a probe held and released charged attacks three times in a row and logged the arm, spine and hip turn per
  frame with every animation layer's state. The jumps sat at three handovers whose blends were far too short: a new swing's blend in
  (0.06 s), the planted heavy handing over to the whole body for its strike (0.07 s), and a full charge let go into the charged blow
  (0.04 s). At a normal frame rate that is two to four frames between quite different poses.
- **Changed:** those blends to 0.10, 0.12 and 0.12 s. They are only the look: when a blow lands is the combat code's, so damage and
  cadence don't change, except that a charged blow released from a full hold now lands 0.12 s after the release (it waited for its
  0.04 s blend before), so the blow never lands mid-blend. Combat tests pass.
- **What's left:** the charged strike itself is fast by design. If it still feels like a snap in your hands, the next lever is the
  charged clip's strike speed, or a separate speed parameter for the upper-body layer (one speed drives every swing state, so a held
  charge at speed 0 can freeze a blow still blending out on another layer). *Please play a few charged chains and tell me.*

### The generator steps, switched on for the Valley and the Marsh
- Code (off by default, the Woods' pinned layouts unchanged): districts with roles (the start's is the Approach with more resources
  and fewer camps; the one nearest the apex is the Lair with 2.5× the camps), landmarks at trail junctions (one per district, each a
  different kind, found onto the minimap within 70 m), short flags along both main routes every 100-140 m, roads (the two routes
  wider), a cap on the Valley's river crossings (3: two bridges and a ford, every clearing still joined), and a hero/mid dressing pass.
- **Troll Valley:** 2 districts; landmarks the Sleeping Troll Rock, the Lookout Cairn and Wayfarer's Stones; flags cairns, field
  stones and boundary stones; roads ×1.3; at most 3 crossings; heroes Mountainside Crag, a large Lichen Boulder and Scots pines; mids
  from its own rocks and undergrowth.
- **Moss Marsh:** 2-3 districts; landmarks Drowned Stones, Wayfarer's Stones and a lone holm pine; flags boundary stones and carved
  posts; roads ×1.6 (the causeway feel); heroes Scots pines and a Lichen Boulder.
- **The Woods:** not switched on yet: it would re-pin the golden layout tests, and the Woods is the most-played raid. A decision
  for you, or for after you've seen the Valley and Marsh with it.
- Barriers with gaps and compounds (the review's G2 and G3), extraction kinds, Valley benches and cirques, the Marsh causeway and
  holms: not built yet.
- **Seen in a launched Valley** (the stone troll landmark by the start, a busier lair). Not yet seen: the Marsh with it.
- **Placeholders I noticed:** the Forward Camp and the Toll Troll still use primitive placeholder models (grey cubes, green spheres).

## The game design research (Docs/GameDesignResearch_2026-11-17.md)

### Done
- **No blows or shots from out of frame (G1):** "off screen" is now the camera's real viewport (with a 2% margin), not a 70° angle
  from the camera that counted creatures ahead of or behind the player as seen. Archers, spitters and throwers now also wait their
  beat while out of frame before they fire at you.
- **The death screen teaches (G3):** under "Brought down by a …" it shows that creature's journal advice (how to face it).

### Not done, and why
- **G2 fight camera** (keep the exploring distance in a fight): it changes how every fight feels; the research lists it for you (D2).
- **W3 weather in the briefing:** the weather evolves through a seeded state machine during the raid; foretelling the first weather
  needs a deterministic "opening weather" call. A medium job, queued.
- **Its questions for you (§8):** D1 spare the gear wear on a journey's first failed raid; D2 the fight camera; D3 the five Old Ways
  as folk laws every creature obeys (steel, fire, daylight, bells, naming); D4 a personal Tale per bond rank; D5 bonuses for a
  second player (solo unchanged); D6 a combat speed assist. Also "Hearth's Grace" (a falling damage-taken assist, logged as an assist)
  and colour-free telegraph rims for colour-blind players: worth doing; queued after your answers.

## The Lost: a nemesis system (you: "named enemies that improve… one of the lost… trying his best to kill the keeper of the flame")
- **Design:** a few named, mist-taken earlier settlers ("Halvard Hollow-Eyed, once the Haven's smith") walk the raids and hunt you.
  Kill you, and one rises a rank, takes a new trait (the elite traits we have) and **carries what you lost**. Wound one and it flees
  into the mist, back next time scarred, and you learn one of its traits. Kill it for good and it drops what it took, is laid to
  rest with an epitaph, and a new one may rise. A journal page keeps them; the briefing warns when one walks the raid you're
  planning. They start after The Weakening Ward and a few extractions; never in boss fights or the prologue.
- **Being built** (code by an agent; data, the Draug body and Play checks by me). Status below when done.

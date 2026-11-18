# Decisions taken while working through the open list (2026-11-18)

You said "go" to the open items from `Docs/AgentDecisions_2026-11-17.md` and yesterday's plans. This is what I did, why, and what
is left for you. Newest at the bottom of each section.

## Done

### The Heartroot's black sap drips at the rage (boss review C8)
- **Built `ArenaHazardDrops`** in Guardian's Hollow: once the encounter's clock runs out (the rage), a drop lands every 8 s on the
  floor's outer edge, warned 2 s ahead by a filling ring in the dodge colour, and leaves a pool of rot (Heartrot) that lasts 96 s,
  so the edge holds twelve at most. The drops go round a fixed ring of twelve places, each far across from the last: nothing random.
  The drop is the boss's blow, so its stage and rage scale it. Each co-op game drips its own from its own clock.
- **Seen in a launched raid** (probe, invulnerable, clock forced to the rage): drops every 8 s at the planned places, pools on
  the navmesh.
- **Fixed from the pictures:** the design drew a flat floor to 20 m, but the built hollow is flat only to about 17.5 m, and the
  bowl's wall rises from 18 m. Pools at 18 m were cut in half (a flat disc hides under a slope). The ring is now 16.2 m with 1.7 m
  pools: on the floor's edge, the fighting ring inside 14.5 m clean. A test samples the terrain under every pool's edge.
- **Heartrot pools are a rot olive now, not brown** (both the drips and the Heart Spray's pools): brown on the hollow's brown earth
  was nearly invisible at dusk. *Check:* that olive doesn't read as the green "opening" ring to you (one is a ring round a
  creature, the other a filled pool on the ground).
- **Also noticed, not changed:** the Hollow's Hold's outermost ring (18.5 m, 2.5 m circles) reaches up the same wall, so part of
  each warning circle hides under the slope while still hurting there. Worth a look when you play the finale.

### A Lost One that kills you is named on the death screen
- "Brought down by Halvard Hollow-Eyed, one of the Lost." in place of "a" and the body's kind; the journal's raid record says the
  same. Works on a friend's copy too (the name rides on the elite). Records from before read as they did.

## For you

### K3: meal and tonic slots against the strength bound
The bound (a fully stacked kit at most ×1.5 toughness and ×1.35 damage of the tier's kit) counted two meals and one tonic. The game
allows four meals (base 2, Kitchen III, the Third Plate perk) and three tonics (base 1, Master's Shelf, Second Vial). Measured,
toughness / damage of the stacked kit against the plain kit:

| Slots | Tier I | Tier II | Tier III | Tier IV |
|---|---|---|---|---|
| Base (2 meals, 1 tonic) | ×1.21 / ×1.03 | ×1.27 / ×1.30 | ×1.28 / ×1.29 | ×1.48 / ×1.26 |
| One more of each (the GDD's "Shelf **or** Vial") | ×1.21 / ×1.03 | ×1.42 / ×1.32 | ×1.47 / ×1.29 | **×1.71** / ×1.36 |
| Every slot (as the code adds them) | ×1.21 / ×1.03 | ×1.49 / ×1.32 | **×1.53** / **×1.39** | **×1.84** / **×1.45** |

So any extra slot breaks the bound at tier IV, where Haven Feast and Ironbark Draught are strong. You said to keep the band and
change the data. The choices:
- **a)** The extra slots stay, and tier IV's food is trimmed until "every slot" fits (Ironbark Draught's +32 armour is the biggest
  single lever).
- **b)** The extra slots hold food at half strength (a fuller belly): every source keeps its worth, and less trimming is needed.
- **c)** One extra of each at most (the GDD's wording), plus a smaller trim at tier IV.
Until you choose, the old test (base slots) still holds, and a second test with every slot is skipped with this reason. My lean: **b**.

## Done (continued)

### The weather foretold in the briefing (research W3)
- The briefing now says the sky a run opens in and what it does, from the rules themselves: "The sky as you go: Rain. Your steps
  carry 79% as far, and fire won't take. It may turn." (rain muffles steps only a little today: Perception Settings › Noise In Rain
  0.7 at full rain) (rain in a snowing season is "Snow: the cold bites in the open"; fog and mist
  "little can be seen far off"; storms "lightning").
- **The raid really opens in it** (seeded like the hour, so friends share it): before, every run opened in its scene's start
  weather (the Woods always in Mist). Now the climate's chances pick it: the Woods about half Mist, a third Breezy + Mist, a sixth
  Rain + Mist, 5% Storm; the Valley mostly Clear, Breezy or Valley Haze, 8% Rain; the Marsh Fen Haze, Mist, Clear and 19% rain.
  Boss arenas keep their one fixed look. Checked in Play: the planner showed the line, and a run launched from it opened in Rain +
  Mist with the ground already wet.
- **Not built (the research's other weather rules):** fog shortening sight for both sides, a storm's thunder masking the beacon,
  snow leaving tracks for hunters. The line only names what the game does today.

### Low rock shelves in the Valley's stony moods (world review G7)
- Scree and Stone and Gorge Narrows stand on two levels 3 m apart (terraces 2, 60 m patches). The height field made a one-level
  rise below 4.5 m into a 9 m slope, not a step; its cliff thresholds now scale down with a low terrace, so 3 m is a shelf. A 7 m
  terrace (the Woods' Gorges) keeps its numbers: the Woods' pinned layouts are unchanged (golden tests pass).
- **Rare in practice:** in a narrow valley, clearings and trails sit too close to have a cliff between, so they share a level; about
  6 seeds in 400 raise two or more clearings. Seen in a launched Valley (seed 19): a raised clearing with a ramp up and rock at the
  step. Making shelves common needs the planner to leave room for them (its shared-level groups): not done.

### Compounds (world review G3), first: the Valley's Seter Yard
- A set piece can now own a clearing (Owns Clearing): it stands at the clearing's middle and a ring of wall pieces runs round its
  yard, open wherever a trail or side path crosses (and one back way more), each piece kept 2 m clear of any trail. The planner
  prefers the clearings with the fewest ways in (chokepoints), never the start's, an exit's or the apex's, and passes over a
  clearing that would leave less than 45% of its ring standing (stubs, not a yard). Walls are solid: the navmesh built after the
  content leaves the openings as the ways in.
- **Troll Valley: one Seter Yard** (the Ruined Seter's ruin and guards in a dry-stone fold) on about a fifth of runs. Valley
  clearings are small and crossed by side paths, so a fold stands round about half its yard: a broken fold, which suits a ruin.
  Seen in a launched Valley (seed 25) from the game camera and close by.
- New plain wall pieces for raids (the Haven's without their building, health and save parts): `Prefabs/Raids/Compounds/Wall -
  Skigard`, `Wall - Dry-stone`, `Wall - Palisade`. The Woods and the Marsh have no compounds yet (the Woods' layouts are pinned; the
  Marsh's Peat Cutters' Camp is still to make).

## Your cliff-edge picture (you: "like this picture for example. can we achieve something closer to this?")
What makes the photo read as rock: a hard lip that overhangs with a dark underside, stacked beds in the walls, lichen on the tops
only, a lone pine on the edge, low sun. Kept: Nordic stone and colours (grey rock, ochre lichen, birch and pine), not red
sandstone. The photo is licensed stock: used only as a description, never as an input.

### Step 1, done: layered rock on the Valley's walls
- **Cliff rock from 40°** (was 56°) on Troll Valley's terrain: its walls fall mostly between 36° and 56°, which showed the pebbly
  "volcanic mossy rocks" texture and read as stacked gravel. Now they read as solid stone with cracks.
- **Layered beds** (new in the terrain shader generator, `WorldStrata`, off by default so every other terrain is unchanged): on faces
  steeper than 44° the rock steps in beds about 2.2 m high by world height, each its own shade and a little warmer or cooler, the
  lines wandering a few metres along a wall, a soft shadowed recess under each bed's lip and a lit edge above it. Troll Valley's
  material: Layered Rock 0.35, Bed Height 2.2, Seam 0.8, Wander 2.5. A first try with thin seams read as ruled lines from the game
  camera and was replaced.
- Seen in a launched Valley (seed 25) at eye height, from the game camera and close by (pictures sent).

### Next
- **Rim ledges:** flat-topped rock slabs along cliff tops (valley benches, shelves, the Brukam's gorge), top flush with the ground
  above, face jutting over the drop: new LocalSpawn models, then a generator pass.
- **A lone stunted pine** at some rim spots; **lichen on top faces** of the ledges; a lower, warmer sun at some Valley hours.

- **Brukam's Bridge** got the same layered rock and cliff rock from 40° (its own terrain material): seen from the approach, the
  walls round the arrival ground read as stacked beds. The approach ground itself is bare and flat: worth dressing some day.

### Step 2, done: lone pines on the rims
- A hero can stand **At Rims**: its own pass over the whole map finds a cliff's lip (near-level ground with a face steeper than 48°
  dropping at least 1.2 m within 3 m) and stands one there in a share of 36 m cells, off trails, clearings and water. Troll Valley:
  two stunted Scots pines (0.55-0.8 size) as rim heroes. Seen in a launched Valley (seed 25): 7 pines on lips, one on the ledge
  above a rock band from the game camera. The Valley floor's birches are dense, so a rim pine reads less lonely than in the photo.

### Step 3, waiting: the overhanging ledge slabs
- Three "Rim Ledge" looks are written into `Tools/LocalSpawn/props_manifest.json` (grey granite slab, lichen on the flat top only,
  stacked beds in the face, an overhanging lip with a worn hollow under it). Making them needs the Editor closed (the 3D step needs
  ~13 GB of RAM; 3 GB were free with Unity open), and closing Unity was refused by the permission guard, so they're not made. Then:
  ingest, a generator pass laying them along lips with their top flush and the lip over the drop, close-ups and game view.

### For you
- **The sun:** the Valley's most common hour is 11 (60% of runs), with short shadows; dawn and evening (30%) already have the
  photo's low light. Moving the main hour to mid-afternoon (about 15) would make long shadows the usual look. Your call.

## Seen at last: the Moss Marsh with the generator steps and its two new set pieces
- Launched (seed 2, Hummock Fen): four landmarks, three short flags, the Bog-Iron Bloomery (ruin, peat stacks, a black widow on
  guard) and the Black Pool Landing. **One oddity:** the Landing's boardwalk and hauled-up rowboat stood on dry grass, not by the
  pool: set pieces go to rumour sites, and nothing yet asks for one by water. A "by water" placement for such pieces would fix it.

## Your next message (look and feel research, rough edges, layered rock as the default)

### Layered rock is the default for every cliff and ravine
- The terrain shader's Layered Rock is on by default (0.35, beds of 2.2 m) for every World terrain: the Haven, the prologue, the
  Woods, the Marsh, Guardian's Hollow and whatever the World Builder makes next. A material sets Layered Rock 0 to turn it off. The
  Valley and Brukam's Bridge keep their own values (and cliff rock from 40°); the other biomes keep their own rock textures.
- **Seen:** the Woods' cliffs (lichen rock with beds). The Haven's cliffs read only faintly layered: its rock texture there is a
  pebbly one. Worth giving the Haven a cliff rock texture of its own if you want the beds to show there too.

### Rough, jagged cliffs and river banks
- New rule, Cliff Roughness: the finished heights are carved on steep ground (gullies and notched lips) and shores break up into
  spurs and bays; trails, clearings and level ground never move (a test holds that). Troll Valley 1.5 m, the Woods 1.2 m, the Marsh
  0.6 m; new rules start at 1.2 m.
- **A first try** raised the noise as much as it cut it: the Woods' cliff tops grew a saw blade of spikes. Now it mostly carves.
- **Seen** in launched raids (Valley seed 25, Woods seed 3): walls break into ledges and overhang shadows; the Valley river's
  shoreline wanders from above; the Woods' lip is notched, not spiked. The Haven (its own World Builder) and the hand-painted boss
  arenas aren't roughened: the Haven would need its builder changed and the world regenerated.

### Fixes from the code review (one subagent, read-only)
- Compound walls kept clearance from a trail's middle, not its edge, so pieces stood on the path: now from the edge (trail half
  width per mood, side paths narrower) plus a metre. A compound could take a clearing holding a camp or story site: no more. Later
  passes didn't know the ring: the yard is now a pocket, and node landmarks keep off the ring.
- Fixing those left Troll Valley no room at all for a compound (0 in 40 runs, its clearings small and full, its floor narrow), so the
  compound moved to the **Moss Marsh: the Bloomery Yard** (the Bog-Iron Bloomery and its guards in a skigard fence), in a clearing
  or a yard of its own beside a trail with a gate and a back way; about a third of Marsh runs. The Valley's Seter Yard is kept for
  when its clearings have room (Compounds 0).
- Rim pines now keep clear of trails and other heroes and stand as objects near walkable ground, like the other heroes.
- The weather line keeps "your steps carry X% as far" in snow (the noise rule applies to falling snow too), and fog now says
  creatures see less far in it (already true: their sight reads the fog's density).
- The Heartroot's drips: drops still falling are called off and the rot pools dry up when the boss falls; the ring's centre is fixed
  in the scene.

### Look and feel research
- `Docs/LookAndFeelResearch_2026-11-18.md`: what exists, what the screenshots show, each area (rendering, terrain, water, VFX,
  audio, UX, lighting) with gain, cost and risk, a ranked top 15 and data-only quick wins. Nothing from it is built yet: your pick.

## The look and feel work (you: "i agree with it. do all suggestions it presented. create folley and ambience with the localspawns model that best fits the task. wait until 6.7 with the new gpu methods.")
Order: the data quick wins, then terrain, water, ambience and foley, the mixer, VFX, UX, lighting; terrain texture arrays last (the
riskiest). GTAO, URP's SSR and Surface Cache GI wait for 6.7 LTS. Sound: MOSS-SoundEffect v2 (the better foley model) for one-shots;
beds from two MOSS takes panned left and right for a natural stereo field, cut to loop.

### Batch 1: the quick wins
- **Shadows:** the sun's cascades are fitted to the top-down view (0.24 / 0.42 / 0.66 of 50 m: the play area gets cascades of its
  own); over the shoulder and in cinematics the near splits swap in. Seen in a launched Valley: crisp shadows under plants and stones.
- **Valley river:** reflection 0.8 -> 0.3, clearer (2.6 m), less murk, shore foam 0.12 -> 0.03, caustics up, a warmer shallow colour:
  from above it reads as clear water over its bed, not a grey canal.
- **Marsh pools:** the milky halo was the shore foam: off (0.03 -> 0). Seen at the Black Pool Landing.
- **Troll Valley's main hour 11:00 -> 15:00** ("Low afternoon sun ... long shadows"), still called Day so the conditions that ask for
  day still hold.
- **Clouds:** the pink ones in the shots were dusk runs (19.2), right for dusk; now only clouds toward the sun warm up at sunset.
- **Brukam's mist curtain:** an ellipsoid of mist (thinning to its tips) instead of a box that stopped dead (the lozenge).
- **Dark plants:** measured: not dark textures and no black fringes (the textures are dilated); in the new afternoon light they read
  dark green, their rims are their own shadows (sharper with the cascades). No data change.
- **The unused legacy Post Processing package** is removed (nothing used it).

### Batch 2: the place's sound, wet shores, rocks sitting in the ground, bed ledges
- **Each raid sounds like its own place (A1).** MOSS-SoundEffect v2 (LocalSpawn's foley model) made the sounds, `Tools/Audio/biome_moss.py`
  (prompts, generation, then loops, cuts and steps in Python; rerun with `--process` only to redo the cutting). A bed for day and one
  for night per biome, each from two takes panned left and right for a real stereo field and crossfaded to loop: the Woods' wind high
  in the spruce, the Valley's breeze over stone with the river far below, the Marsh's midges and reeds by day and its frogs by night.
  `AmbienceBed` (Audio) fades its layers by the hour and the rain (insects and frogs go quiet in rain). It replaces the old Scene Music
  ambience track in the three raids and both arenas (Brukam's Bridge has the Valley's beds, Guardian's Hollow the Woods'). Two takes
  (a creak and a woodpecker) were thrown out: they held a pure tone.
- **Sounds from where they belong.** `PlacedAmbience` gains the nearest cliff (scree trickling, a raven off the wall) and trees (a
  woodpecker, a trunk creaking, an owl at night) beside the still water it had (a bittern, a plop, bubbles in the Marsh). The Marsh's
  fen is one sheet of water laid under the whole map, so a still-water sound comes from the nearest place its water shows, never
  from under your feet.
- **Foley.** Footsteps in mud, bog and peat and on gravel, scree and shingle (`AudioSettings` footstep surfaces, by the ground's
  layer name); walking through reeds, sedge, horsetail, ferns, bushes, tall grass or cotton grass rustles (`Plant Rustle`, two or more
  such plants under the foot).
- **The space a sound is heard in (A2-A4), with no mixer.** One reverb zone rides on the listener: the raid's place (Woods Forest,
  Valley Mountains, Marsh Plain, Brukam's gorge Quarry, the Hollow Forest), kept low under the sound; a room under a roof; muffled
  while swimming. Every effect voice now gets a low-pass that closes with distance (far is dull, near bright), further and 4 dB
  quieter when the ground or a building stands between it and the ears: a troll behind the ridge sounds behind it. Music, beds and
  the interface are untouched.
- **Wet shores (4.6).** A band a hand's breadth above every pond, pool and river is darker and glossier on the terrain and on rocks.
  The ground light field holds the nearest water as metres above or below its own ground (6 cm steps; over the map's heights a byte
  stepped 20 cm). Seen at the Valley river: the band follows both banks, wide on the gentle one, narrow under the steep one.
- **Rocks sit in the ground (4.3).** A rock's or log's foot takes the ground's colour, a little darker (damp soil), with a contact
  shadow. It reads the terrain's own 16-bit heightmap: the first try read the light field's byte heights, which on a bank were a
  metre off and tinted whole rocks. The band is a quarter of how far the thing stands out of the ground, up to 30 cm, so pebbles
  keep their read (a fixed band hid them from the game camera).
- **Bed ledges.** The layered rock's beds are tessellated out near the player (Bed Ledges, now 0.2 m; a material sets 0 to turn
  them off). The first version jumped out at each bed line and streaked the rock down the riser; now each bed ramps back out over
  its top 30% and the texture stretches 1.3 times at most, only on undersides the game camera never sees. Seen close up and from
  the game camera: soft stepped beds, no streaks.
- **Unheard by you:** all of the sound above. The beds play at the scenes' levels (checked in a launched Valley: day 0.75, night 0;
  the reverb in place).
- **Fixed after the code review (one subagent, read-only):** music, beds, voices, the interface and cinematics bypass the place's
  reverb (Unity sends every source into its one reverb unless it bypasses it: swimming had put the music under water); a tree's trunk
  no longer counts as the ground between a sound and you (Unity reports a trunk as the terrain's collider, so every sound behind a
  spruce was muffled); the reverb lets go when there is no player (the menu after dying in water played under water); the shore's
  band fades with distance from the water (it ended in a line 3 m out on gentle banks: seen fixed at the Valley river); the beds
  hold for cinematics and give way to speech as fast as the music.

### Batch 3: warnings on the ground, readable without colour; fog for the eye; the boss stop
- **Warnings lie on the ground (V1).** Every warning drawn on the ground (a strike's area and fill, the heavy edge, the opening ring,
  a lob's landing, hazards, the Hunter's Mark, the backstab zone) is projected onto whatever is under it, so it follows a slope or a
  step instead of clipping into the ground or floating over a hollow. A bright rim, a fill dim at its middle and glowing at its front,
  a flash as the hit lands. Characters and tall rocks inside it are left clean. Seen in a launched Valley: a troll's swipe on the
  slope by the river and its charge lane running behind the player's legs.
- **Kind without colour (A3, U5).** A solid rim is an ordinary strike, a dashed rim one any blow breaks off, hatched one no guard stops
  (unblockable fills and hazards). Colour still says it too.
- **Options › Accessibility:** High-Contrast Warnings (off: wider, brighter rims with a dark line inside; seen) and Fog Amount
  (40-100%, full by default): only the drawn fog and mist thin, creatures see through the fog as authored.
- **Boss stop (V6).** The blow that fells a boss holds 0.45 s (other kills 0.12 s) under Hit Stop, and the picture streaks out from it
  for 0.4 s under Screen Flashes; an elite felled by a heavy blow or a finisher gets half the streak. Not seen yet (no boss felled in a
  probe); hosting co-op, time can't stop, so only the streak plays.

### Batch 4: water that moves and carries things
- **Speed by shape (W3).** A river strip's flow now runs faster through a neck and down a fall, slower at its banks and in pools,
  faster round the outside of a bend; the water shader already whitens, clouds and foams faster water.
- **Foam where the current puts it (W2).** Streaks drawn out along the current (the river mesh's own along-the-run coordinates, so a
  curve never swirls them), densest mid-river where it runs fastest. The shared river's even shore foam is down to a thin line
  (0.3 -> 0.1), the Valley's was cut in batch 1. Seen in the launched Valley from the game camera: the river reads as moving water.
- **Things afloat (W5).** Procedural, no textures: the Marsh's fen and pools carry duckweed in drifts that close into torn mats at
  their heart, with slow gas rings rising; the Woods' ponds and the forest streams carry a few fallen leaves (pointed, varied, some
  sodden). The Haven's and the Valley's ponds stay clear (each raid's ponds have their own material now). Seen close up and from the
  game camera in launched Marsh and Woods raids; the first duckweed read as neon dots and was darkened and drawn into mats.
- **Warnings over water.** A lob ring or hazard over a pool lies on the water's surface (the raid's ground light field knows it),
  not on the bed below. Seen at the Marsh: a lob into the fen.

### Batch 5: life in the air (V2, V4)
- **Each raid's air has its own life** (`AmbientLife`, a profile per biome; Brukam's Bridge has the Valley's, Guardian's Hollow the
  Woods'). **Woods:** dust motes that shine only where a shaft of sun reaches them, moths round the lights at night, needles and twigs
  a gust carries. **Valley:** pollen and seed fluff drifting on the breeze, dust and grass seed lifted by gusts, and now and then a big
  bird's shadow (one or two) sliding over the ground: scale from above; the bird flies too high to see. **Marsh:** midges in glittering
  columns over open water, dragonflies (four clear wings, seen lying flat from above), cotton grass fluff in the gusts. Fewer at night
  or in rain as each swarm says (moths only at night).
- **Gusts as one event (V4):** debris comes in a wave from upwind at the speed the grass's gust waves roll, so wind reads across the
  frame.
- Seen in launched raids: the Valley's pollen and a bird's shadow crossing the clearing (the first ones landed 50 m off: the
  afternoon sun throws a shadow far, so flights are now aimed so the shadow crosses the view, and the first shape read as a
  triangle and got long bent wings, a head and a tail); the Marsh's midge columns (too dark at first: they now catch the sun) and
  dragonflies (first drawn as a cyan bar, now lying flat). The Woods' motes and moths are not seen yet.

### Batch 6: specular anti-aliasing (3.4)
- **Rock highlights spread where the normal turns within a pixel** (`Shaders/SpecularAA.hlsl`, HDRP's variance 0.1 and threshold
  0.2), on the terrain (generator) and Mesh Surface rocks, before wetness raises smoothness so wet rock keeps its sheen. A global
  `_SafeHavenSpecularAAOff` (1) turns it off for comparisons.
- Measured in a launched Valley at 15:00 with time frozen: it changes only the rock faces, slightly (mean 0.2 of 255). That dry
  afternoon light hardly glitters to begin with, so the sparkle it is for (`g7_*` shots) was not reproduced; watch for it in a
  wet or low-sun run.

### Batch 7: compounds light their gates at night (L5)
- **A torch on the end post of every run of wall** (a set piece's Gate Light, the buildings' Wall Torch: lit at dusk, out at dawn,
  each at its own moment), turned in toward the yard. The ends of the runs, not the planned openings: the ring also leaves pieces out
  where a trail or a steep bank comes, so the gaps are where the way in really is (the first try looked near the openings and found
  nothing: the Bloomery's wall stood 45° from its gates). The Bloomery Yard and the Seter Yard both have it.
- Seen in a launched Marsh at night (seed 7): two torches 18 m apart at the Bloomery's way in, on the skigard's end posts; from the
  game camera the flames mark the gate, the light a modest pool (range 6.4 m: the buildings' torch, unchanged).
- **L4 (mist darker under the canopy)** was already in place: the mist's sky light is cut by the canopy and the land's shade
  (`GroundLightAirAbove`, from the ground light field step). Nothing changed.

### Batch 8: sunflecks under the canopy (L3)
- **Spots of sun through the crowns** on the shaded forest floor (terrain, moss skin and plants), drifting a little as the leaves
  move, longer along the sun's way. Only inside the sun's own shadow and only where the field says a crown stands up-sun (so a
  cliff's or a building's shadow stays whole), so they follow the real crowns' shadows; none under overcast or at night. A raid's
  Look › Ground Light › Sunflecks: Woods and Hollow 0.9, Marsh 0.6, Valley 0.45 (Brukam's Bridge has no field, so none there).
- Traps found: the field's canopy marks the ground straight under each crown, while its shadow falls toward the sun (first try:
  almost nothing showed); a run that turns rainy has no sun, so test on a fresh clear run.
- Seen in a launched Woods at 11:00 in clear weather: soft patches of sun in the crowns' shadows, subtle in the misty grade (0.55
  read as nothing, now 0.9). **Tune it in each raid's Look if you want them stronger.**

### Batch 9: the moss's repeat broken up (4.5)
- **Hex-tiling** (Mikkelsen 2022, `Shaders/HexTiling.hlsl`) on the moss skin's colour, normal and packed maps: each corner of a
  triangle grid gets its own offset and turn, blended with the brighter sample winning a little, so the repeat breaks up without
  washing grey. Terrain Moss › Hex Tiling (1 by default, 0 the old single sample).
- Seen in a launched Woods: from above, the moss's woven-looking repeat is gone and it reads organic; close up no seams.
- Cost: 9 texture samples instead of 3 on the moss. Timed over 300 frames twice in the Editor: 0.2 and 1.6 ms more, inside its
  ±2 ms noise. If a build profile shows it, it can go behind the graphics options.

### Batch 10: the combat sound audit (A6)
- **Audited 119 sounds** the combat, monsters, weapons and the game's settings play. The hits, swings, blocks, parries, staggers and
  deaths already had 3-9 takes with ±5-8% pitch. Too few: Backstab Hit, Perfect Dodge, Ice Crack, Impact Quake (a boss toppling) and
  six creature calls (Shaman Call, Huldra Call, Light Splits, Wing Alarm, Stone Skin, Root Regrowth) had two takes, Gore Splash one.
- **A third take each** (two for Gore Splash) from `Tools/Audio/variations.py`: one take's attack spliced onto another's body and
  tail, resampled a few percent, the tone tilted a couple of dB, matched in loudness to its sources. Measured: loudness within 0.01
  RMS of the sources, no more tonal than they are (no whistles).
- Left as they are on purpose: cues that tell you something (Counter Ready, Out of Stamina, Bow Full Draw) and the single stingers
  (quests, breaches, crafting milestones), which should sound the same every time. `CombatSoundTests` guards the twenty repeated
  ones. **Unheard by you.**

### The value check (U4): figures don't stand out by value in the dark raids. Your call
- **Measured** on game-camera shots in grayscale (the player's lit part against the ground round them, linear luminance):
  Valley by day 1.3:1, the Woods by day 1.2-1.4:1, a night Marsh by the gate torches 2.1:1. The guideline for large shapes is 3:1.
  The figures read by shape and colour, not by value; in the Woods the brightest things on screen are a pale boulder and the
  glow mushrooms, so the eye goes there before the player.
- **Why the existing helpers don't fix it:** the character rim (0.2, +80% at night) is lit by the sky and the sun, both dark under
  the canopy; the player's fill light hangs 5 m off with a point light's falloff, so its 0.9 arrives as about 0.04 (it was meant to
  "lift the figures a little" in the Woods: measured, doubling or even tripling it changed nothing). With the falloff removed it
  works, but at its strength it lifts the figure only to 1.4:1: the Woods' dark grade and mist compress the range.
- **Not changed** (it would change the Woods' mood; your eye decides). Options:
  1. A real figure light in dark places: the fill without the falloff at about 2-3 (figures visibly brighter than the litter, as
     in Diablo; the Woods less murky around the fight).
  2. Lighter litter under the canopy (the biome a step brighter, the figures as they are).
  3. Leave it: the player reads by shape, colour and motion, and the fights by their ground warnings (batch 3).
  Recommended: 1, limited to raids and to the dark (scaled by how little sky and sun reach the player).

### The camera FOV experiment (U2): your eye decides, nothing changed
- The explore view (40° at 20 m) against 48° from 16.5 m (the same ground covered), from the game camera's angle at the Valley wall:
  at 48° the cliff leans in more and its near rock bulges toward the lens, the trees show a little more of their height; relief
  reads a little better, and tall things near the camera loom. Shots: fov_40.png / fov_48.png (sent with this note). If you like it,
  it is Player Camera Settings › Explore › Field of View 48 and Min/Max Distance × 0.82.

### Batch 11: the music follows the raid (A5, GameDesignResearch T1)
- **A stinger as each danger stage begins** (RaidStage › Stinger, all three raids): a lone horn far off and its answer as the land
  stirs; two horns nearer and lower over a frame drum as the hunt begins; three horns, the drum twice and a bow's scrape at the
  last stage. Made in `Tools/Audio/raid_music.py` with the War Horn's lip-reed model (low notes and membranes: no whistles; energy
  above 2 kHz is thousandths of a percent, as the War Horn's). They take the mix (music dips under them).
- **The quiet before the hunt:** the second stage hushes the exploring music for 60 s (RaidStage › Hush Seconds), leaving the
  ambience, so footfalls and crows are the music; a fight's music still plays, and the final stretch keeps its music. Seen in a
  launched Valley: the stage began, the trolls it woke came (combat music), and once they were gone the music read "Hushed".
- **The held beacon's heartbeat:** a frame-drum "lub-dub" while the circle is held, from 1.3 s apart as it is lit to 0.55 s as it
  fills (eased), four takes; it drops out while something contests the circle (Extraction Point › Heartbeat, on the prefab).
- Per-raid music sets stay your later plan; these cues are shared until then. **Unheard by you.**

### Batch 12: rain read from above (V3)
- **Half the splashes land within 6 m of the player** (Rain Effect › Near Share, Near Reach), where the eye is: the ground round
  the player boils with them; the rest still spread over the view.
- **A thin sheet of rain in front of the camera** (Rain Effect › Sheet): a fainter, shorter copy of the drops falling 5 m in front of
  the lens and turning with it, so streaks cross the view whatever the camera does.
- Puddles on the terrain already ring under the rain (the generator's WorldRainRings): nothing to add.
- Seen in a launched rainy Valley from the game camera: faint slanted streaks across the frame, splashes thick round the player.

### Grass motion vectors (3.3): Unity's detail renderer doesn't draw them, so MSAA stays the default
- **Tested:** a MotionVectors pass on `Grass.shader` (tagged AlwaysRenderMotionVectors, as Tree Wind is), with a debug switch that
  wrote a huge velocity. Under TAA with time frozen, the switch changed the picture no more than TAA's own jitter between two
  identical frames (0.46% of pixels against 0.50%): the terrain's detail renderer never calls the pass. Reverted (an unused pass only
  adds shader variants).
- **So:** grass under TAA, STP or DLAA keeps softening when it sways; 4x MSAA stays the default (the research's step 2). Giving grass
  motion vectors means drawing it through our own instanced path instead of Unity's detail renderer: a large job (L), and Unity 6.7's
  GTAO wants it too. **Left for later, with 6.7.**

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

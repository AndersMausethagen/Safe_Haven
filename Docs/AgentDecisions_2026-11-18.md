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

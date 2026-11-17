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

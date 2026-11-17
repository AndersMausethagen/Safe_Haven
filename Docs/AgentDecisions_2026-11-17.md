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

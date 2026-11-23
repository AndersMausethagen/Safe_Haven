# Are our meals, tonics and coatings worth eating? Balance review (2026-11-23)

The user (2026-11-23): "do not be so strict with that power limit between tiers and making 'stay within ×1.35' mandatory. I am afraid
that new effects or buffs get too weak so using them does almost nothing. If necessary, we can tune the power limit instead and make
each tier scale higher. [...] Do we have underwhelming effects from pots, foods, buffs etc? Should we instead of nerfing effects,
increase the power level of the different tiers?"

**What is built today:** Ember Pitch has a damage budget of its own, ×1.35 over the stacked kit (`PowerCurveSettings` › Coating
Damage), as enchantments have theirs. Nothing else here is built; the rest needs the user's answers (§5).

## The short answer

- **Yes, our boons are underwhelming.** Every damage meal or tonic adds 8 to 15%, which takes less than one blow off a kill that takes
  8 or 9. Health and armour food adds 2 to 15% toughness, and flat-armour food fades as tiers rise: Honey Cake is worth 5% at tier II
  and 2% at tier V.
- **The cap caused it.** A fully stacked tier I kit (two meals, a tonic, perks, Well Rested) adds only **8% damage**. The boons were
  kept small so the stack stays inside ×1.35.
- **Other games are bigger.** A single damage buff is usually +10 to 20% (Elden Ring 15 to 20%, Enshrouded's late food 24%). A
  single health or defence buff is bigger still (a Monster Hunter meal is +50% health; in Valheim, food *is* your health). A full
  prep runs ×1.2 to ×1.6 or more.
- **Recommended:** don't nerf effects; make them felt, and make the tiers expect them.
  1. Every boon at least one blow's worth at its tier: damage boons about +15 to 20%, toughness boons about +15%.
  2. Flat armour on food becomes percent armour, so it doesn't fade (flat health already became percent for the same reason).
  3. Raise the food reach to ×1.5 damage and ×1.7 toughness; keep the coating's own ×1.35 on top.
  4. Tune each tier to a **fed** entry kit (the tier's best Fierce and Hearty meal), not the bare one, and raise the tier multipliers
     to match. Bare play gets harder, fed play stays as today, and full prep fights about a tier up. Food becomes the everyday
     baseline it is in Valheim and Monster Hunter, instead of a bonus nobody feels.

## 1. What each boon does now (measured)

Measured with the game's own balance model (`GearBalance`, `FightMath`) on each tier's plain kit. "Kill" is blows to fell a reference
creature (100 health, 10 armour, the tier's health scale); "die" is blows of 15 (at the tier's power) the player survives.

| Tier | Plain kit kill / die | Stacked kit now (no coating) | Food the stack picks |
|---|---|---|---|
| I | 8.0 / 13.2 | dmg ×1.08, tough ×1.21 | Roast Boar, Barley Bread |
| II | 7.9 / 11.1 | ×1.10, ×1.37 | Smoked Boar, Hunter's Stew, Bear Tonic |
| III | 8.7 / 11.7 | ×1.20, ×1.24 | the same |
| IV | 9.2 / 11.2 | ×1.27, ×1.32 | Haven Feast, Smoked Boar, Bear Tonic |
| V | 9.3 / 11.6 | ×1.38, ×1.31 | the same |

At tiers IV and V most of the stack is Melee perks, not food.

| Boon | Kind | Damage | Toughness | What it changes (tier II) |
|---|---|---|---|---|
| Roast Boar (Strength 3) | meal t1 | ×1.08 | – | kill 7.9 → 7.3 |
| Smoked Boar (Strength 4) | meal t2 | ×1.10 to 1.12 | – | 7.9 → 7.2 |
| Trout in Cream (+8%) | meal t3 | ×1.08 | – | (tier III) 8.7 → 8.1 |
| Honey Mead (+10%, −5 armour) | meal t2 | ×1.10 | ×0.97 | 7.9 → 7.2 |
| Cursed Draught (+15%, −8 armour) | tonic t2 | ×1.15 | ×0.95 | 7.9 → 6.9 |
| Honey Cake (8 armour) | meal t2 | – | ×1.05 (t2) → ×1.02 (t5) | die 11.1 → 11.7 |
| Barkskin Draught (14 armour) | tonic t2 | – | ×1.08 → ×1.04 | 11.1 → 12.1 |
| Hunter's Stew (+9.5% health) | meal t2 | – | ×1.10 | 11.1 → 12.2 |
| Purified Draught (+10% health) | tonic t2 | – | ×1.10 | 11.1 → 12.2 |
| Boar Pie (+8% health) | meal t3 | – | ×1.08 | (tier III) 11.7 → 12.7 |
| Haven Feast (+10% health, 12 armour) | meal t4 | – | ×1.15 | (tier IV) 11.2 → 12.9 |
| Ironbark Draught (24 armour, +5%) | tonic t4 | – | ×1.14 | (tier IV) 11.2 → 12.8 |
| **Ember Pitch** (25% fire, 5% ignite) | coating t1 | **×1.32** | – | **7.9 → 6.0**; on the Heartroot (fire −40%) more |

Swift and Warding boons (stamina, speed, frost) aren't in the damage-and-toughness model; they're felt in other ways and aren't judged
here.

## 2. When is a buff felt?

- **In blows, not percent.** Obsidian (GDC, The Outer Worlds 2): 100 against 101 health is invisible, one hit against two is obvious;
  they tuned to hit counts.
- **One blow off a kill of N takes +1/(N−1):** 10 → 9 blows is +11%, 8 → 7 is +14%, 6 → 5 is +20%. Our kills take 8 or 9 blows, so a
  damage boon needs about **+14%** to take one blow off, and **+20 to 25%** to be felt clearly. Ours give 8 to 12%.
- **One more blow survived of 11 to 13** takes about +9%. Felt clearly at about +15 to 20%. Most of ours give 5 to 10%.
- **Spiderweb's Jeff Vogel:** players hoard or ignore consumables that don't change an outcome.

## 3. How other games size them

| Game | Consumable | Size | Content assumes it? |
|---|---|---|---|
| Valheim | Food (3 slots) | Health and stamina come from food: base 25 health, one late food +75 to +100 | Yes: an unfed player can't survive a pack |
| Valheim | Meads | Resistance: −50 to −75% of one damage type | Optional edge |
| Elden Ring | Exalted Flesh, Golden Vow | +20% physical; +15% damage and +10% defence | Optional; they multiply (×1.66 stacked) |
| Elden Ring, Dark Souls III | Greases and resins | +85 to +120 flat element for 60 s | Optional; huge early, small late |
| Monster Hunter World | Canteen meal | Attack or Defence Up S/M/L, +50 health | Yes: eaten before every hunt |
| Monster Hunter World | Might Seed, Demondrug, charms | +5 to +10 raw (3 to 7% late) | Small, but they stack |
| Enshrouded | Food | +3% (early) to +24% melee (late), +40 to +240 health | Expected |
| Diablo IV | Elixirs (launch) | +7 to 20% attack speed, +15 to 40% resistance | Optional |

Takeaways:
- **Two kinds of consumable.** Baseline ones (food) are cheap, long and eaten every outing, and content is tuned with them in.
  Optional edges (greases, seeds, elixirs) are short or dear, and content can be beaten without them.
- **We tune to the bare kit,** which puts our meals in the optional-edge group, and then keep them too small to be felt as an edge.
  That is the worst of both.
- **Coatings are flat adds in the Souls games,** so they matter by the element matchup. Ember Pitch is a share of the blow instead,
  so it stays the same at every tier, and the matchup (a fire weakness) makes it more.

## 4. Proposals

**B1. Flat armour on food becomes percent armour (data, S).** Honey Cake, Barkskin Draught, Ironbark Draught and Haven Feast's armour
turn into a share of the kit's armour, so a tier V player gets what a tier II player gets. This is the same fix flat-health meals got
(Docs/PlayerStatsResearch.md).

**B2. Every boon felt at its tier (data, S).** Targets: damage boons about +15 to 20%, Hearty boons about +15% toughness, the damage
tonic about +25% (its armour cost stays). For example:

| Boon | Now | Proposed |
|---|---|---|
| Roast Boar | Strength 3 (×1.08) | ×1.15 |
| Smoked Boar | Strength 4 (×1.10) | ×1.18 |
| Trout in Cream | +8% | +15% |
| Honey Mead | +10%, −5 armour | +18%, −5 armour |
| Cursed Draught | +15%, −8 armour | +25%, −8 armour |
| Honey Cake | 8 armour | +15% armour |
| Hunter's Stew, Purified Draught | +9.5 to 10% health | +15% health |
| Boar Pie | +8% health | +15% health, +5% armour |
| Haven Feast | +10% health, 12 armour | +18% health, +15% armour |
| Barkskin, Ironbark Draught | 14, 24 armour | +20%, +30% armour |

**B3. The reach (data, S).**
- The food's stacked reach: ×1.35 → **×1.5 damage**; toughness ×1.5 → **×1.7**.
- The coating's own budget stays ×1.35 on top (built).
- This keeps a full prep near the research's ×1.2 to ×1.6, about one and a half tiers.

**B4. Tiers tuned to a fed kit, and scaled up to match (data with the RaidTierEditor, M).**
- **The entry kit becomes fed:** the tier's best Fierce meal and best Hearty meal, no tonic, no coating, no rest.
- **Each tier's enemy health and power multipliers rise** until the fed kit's blows to kill and to die match what the bare kit's are
  today.
- **The ladder rules stay, measured from the fed kit:** mastered a fifth easier, the next tier at least 30% harder.
- **Effect:** coming in hungry is about 15 to 25% harder than today; fed is as today; a full prep (two meals, a tonic, a coating)
  plays about a tier up. Food becomes the baseline, and the tonic and coating become the felt edge for a hard fight. This is the
  Valheim and Monster Hunter model.

**B5. Then play it (the user).** The Kit rows (start and end of each raid) and the new BossTry rows show what players carry and
what it did.

**Order:** B1, B2, B3 together (all data, one test pass), then B4 (it reads the new food), then a playtest.

## 5. Questions for the user

1. **Re-anchor the tiers on a fed kit and scale them up (B4)?** *Recommended: yes.* It is what makes stronger food safe: tiers expect
   a meal, so the meal matters, and the cap can open without the game getting easy.
2. **Boons to the felt sizes (B1, B2)?** *Recommended: yes, with flat armour made percent.*
3. **The reach to ×1.5 damage and ×1.7 toughness, and the coating's ×1.35 on top (B3)?** *Recommended: yes.*
4. **Ember Pitch's ignite:** your 5% means about one burn every 50 seconds at your swing rate, which is easy to miss. Keep 5%, or
   let heavy and charged blows always ignite (and lights 5%)? *Recommended: heavy and charged always ignite.* It also gives the
   charged heavy, which you didn't use against the Heartroot, a reason to exist.

## Sources

Researched by a subagent (web only), with what it could confirm marked:
- Valheim food: [gamever: how food works](https://gamever.io/knowledge-base/how-food-works-in-valheim-three-slots-and-why-you-have-25-health),
  [eip.gg food list](https://eip.gg/valheim/guides/complete-food-list/); meads:
  [Valheim wiki](https://valheim.fandom.com/wiki/Poison_resistance_mead) (search summary only).
- Elden Ring: [Buffs and Debuffs](https://eldenring.wiki.fextralife.com/Buffs+and+Debuffs),
  [Exalted Flesh](https://eldenring.wiki.fextralife.com/Exalted+Flesh), [Golden Vow](https://eldenring.wiki.fextralife.com/Golden+Vow).
- Dark Souls III resins: [Charcoal Pine Resin](https://darksouls3.wiki.fextralife.com/Charcoal+Pine+Resin) (search summary only).
- Monster Hunter World: [Might Seed](https://monsterhunterworld.wiki.fextralife.com/Might+Seed),
  [Canteen](https://monsterhunterworld.wiki.fextralife.com/Canteen),
  [Powercharm](https://monsterhunterworld.wiki.fextralife.com/Powercharm).
- Enshrouded: [allthings.how food and potion buffs](https://allthings.how/enshrouded-every-food-and-potion-buff-and-duration/).
- Diablo IV: [PCGamesN elixirs](https://www.pcgamesn.com/diablo-4/elixirs) (launch values).
- Felt thresholds: [Thumbsticks on Obsidian's GDC talk](https://www.thumbsticks.com/how-obsidian-fixed-combat-in-the-outer-worlds-2/);
  [Jeff Vogel on consumables](https://bottomfeeder.substack.com/p/understanding-the-player-brain-pt).

In the project: `GearBalance` (RaidTierEditor.cs), `FightMath`, `PowerCurveSettings`, every boon item, RaidTierTests, PowerCurveTests,
and Docs/PlayerStatsResearch.md.

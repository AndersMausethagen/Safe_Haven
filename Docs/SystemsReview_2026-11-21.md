# Review of the systems built 2026-11-20/21, and how comparable games do them

What this covers:
- the systems built or changed in the last two days, with what I see as weak in each, from the code and the Play-mode screenshots;
- how well-regarded games handle the same thing, from a web research pass, with sources below;
- suggestions ranked by value for effort.

Nothing here is built yet: pick what you want (the rule for design documents).

## The short list (best value first)

| # | Suggestion | System | Why | Size |
|---|---|---|---|---|
| 1 | **Say what holds up the next rescue.** A tracker line and the notice: "The next one can be found after Bjarne's Copper Fire" | Quest pacing | Gates read as story when tied to something in the world (Terraria's arrival rules, Enshrouded's survivors); a hidden gate reads as a lock. Today the wait is invisible | S |
| 2 | **Quest toasts and messages wait while a window is open.** The quest toast and the HUD messages drew over the station window in every screenshot | HUD | The big notice already waits; the toasts don't | S |
| 3 | **Station luck counts for the player's own crafts too**, or the effect says "for workers". The Rune Shelf, Scarecrow and Apprentice's Corner luck only rolls on worker orders today | Modules | A player who fits a Rune Shelf and crafts at it sees nothing happen | S |
| 4 | **Before → after on every Fit and Upgrade row:** "Output slots 1 → 2", "Health 400 → 500" | Upgrade tab | Players judge an upgrade by its change, not its total (the research's UI section; Satisfactory's overclock panel) | S |
| 5 | **Warn about charcoal before it runs out:** "Charcoal for 2 more nights" when the stores fall low, instead of the per-night "N torches stand dark" | Torches | Valheim's resin refuelling is the chore its most-downloaded mods remove. Our auto-feed is right, the warning comes too late | S |
| 6 | **Per-chest "don't store here" and a per-item "keep" flag** for Store in chests | Inventory | Grounded added exactly these in 1.1; Palworld's stow has an exclusion list | M |
| 7 | **Trade-off modules:** a thrifty variant also crafts 25% slower; Iron Bands makes the building slower to repair | Modules | Factorio's and Satisfactory's modules are choices because each costs something. Ours are all bonuses, so five slots fill with whatever is known | S data |
| 8 | **Show a working module on the building:** its dressing part (planned), and a small glint and sound when its bonus fires (a spare plank, a turned-back blow) | Modules | Satisfactory's amplified buildings glow, so players can see the effect at a glance | M (art) |
| 9 | **A wall ring's total cost and ghost before the first post**, and the gate swap's refund in its preview | Walls | Stronghold-style drawing is liked; paying before seeing the whole ring isn't | S |
| 10 | **Per-row "reset to default" in Options**, and live preview with auto-revert for HUD scale and size | Options | The Xbox guidelines' examples (Forza, Fenyx) preview in context; we preview only the display mode | S |
| 11 | **Show a wild horse's temper before the lasso** ("Spirited: a hard struggle, a better horse"), and make the first feeding the first bond gain | Horses | RDR2 and BotW tie difficulty to quality and show it, so a hard tame feels earned | S |
| 12 | **A module list in the journal:** every module as a card, unknown ones as silhouettes with "taught by a quest" or "a tier teaches it" | Modules | Lets players plan (the research's "requirements players can't see" complaint, Enshrouded) without showing the whole tree at once | M |

## System by system

### Building upgrades and modules

**What works:**
- One slot per tier, kept to five, means a tier is something you feel.
- Thrifty variants are recipes, not percentages, so a cancelled craft refunds exactly what it took.
- Rare materials gate a few modules, each with a sure source, so no module depends on luck.
- Removed modules go to the store, never lost.
- Fitting is hammered, like the rest of building.

**Weak spots:**
- Luck applies to worker orders only (suggestion 3).
- Every module is a pure bonus, so there are no real choices (7).
- Nothing shows on the building (8).
- Nothing says how much better a module makes the station (4).
- Every module takes 5 s of hammering, whatever its tier: it could scale (3 s at tier I to 8 s at tier III, as the research doc planned).
- When a quest and a tier teach at the same moment, the "New module" messages can stack. They could become one notice with the module cards.

**From other games:**
- Valheim's placed workbench extensions are disliked for their spacing rules: "a deceptively large area".
- Enshrouded's upgrade props are called "brutal and convoluted": 574 of 2,536 recipes are gated by them.

Our slots in the Upgrade tab avoid both: no distance rules, and every need is shown.

### Taking buildings down

**What works:**
- A full refund, less the mending share of the damage.
- The refund shown as item cards before you confirm.
- Fitted modules go to the store.

This matches Palworld's full refund on disassembly and Valheim's.

**Weak spot:** when the bag and the chests are full, what's left drops on the ground. Players dislike things that quietly vanish, and ground drops can be missed. It could wait in a "returned" pile at the portal instead. This is minor.

### Quest pacing, people and rescues

**What works:**
- Each rescue starts that person's line.
- One building is taught per quest.
- Rescues come in order, each answering the problem just met.

This is close to Enshrouded's survivor-gated crafting and Palworld 1.0's "teach one system, use it, then layer the next".

**Weak spots:**
- The rescue gate is invisible: nothing says what you're waiting for (1).
- If a player doesn't want to raise the Smithy, the next rescue waits. That's intended, but it shouldn't leave them with nothing to do: Tales and the board's errands should stay open. The homecoming hold already keeps one of each kind going.

**From other games:** players praise light, meaningful gates (Vintage Story), and call long chains of locks "scripted" (7 Days to Die).

### Inventory helpers

**What works:**
- Store in chests tops up existing stacks first, then puts away materials, and leaves equipped and skill-bar items alone. That is the Terraria and Grounded pattern.

**Weak spots:**
- No per-chest or per-item control (6).
- Auto-drink posts no message. A short toast with the item's icon would say where the water went: no major game has auto-drink built in, so players won't expect it.

### Night, light and fuel

**What works:**
- Torches feed themselves from storage at dusk.
- Creatures keep out of the light unless provoked.

That's a clear rule of the kind Minecraft and Don't Starve teach.

**Weak spots:**
- The warning comes after the light has failed (5).
- A provoked creature crossing into the light has no tell. Eyes at the edge of the light would show the rule working.

### Walls

**What works:**
- A drawn line closes into a ring.
- Pieces go up as scaffolds you hammer.
- A piece can become a gate or take a torch, the way Manor Lords' modules snap into a wall.

**Weak spot:** you pay piece by piece without seeing the whole ring's cost (9).

### Options

**What works:** almost everything the Xbox Accessibility Guidelines' examples show:
- changes staged until Save;
- an unsaved-changes prompt set clearly apart;
- a details panel for the row in focus;
- a reset;
- settings that persist.

**Weak spot:** reset is per tab, not per row, and only the display mode previews live (10). Key rebinds still apply at once, which is fine for keys but differs from the other tabs. A one-line note on that tab would say so.

### Horses and the Stable's tale

**What works:**
- Sneak, rope, struggle, ride, feed: the RDR2 and BotW shape, without Ark's long waits.
- The tale now gives it a reason, and ends at the Stable.

**Weak spot:** a wild horse's temper isn't shown until you're on it (11).

### The Upgrade tab and its UI

**What works:**
- Each slot's state has an icon as well as a colour (lock, spark, module), which keeps it colourblind-safe as the PoE complaint warns.
- Every material is a picture with held/needed counts.

**Weak spots:**
- No before → after values (4).
- Hovering a module card shows no tooltip.
- A short material is marked only in red: "missing 3" would read faster.

## Sources

Every claim above about another game comes from the research pass. Its sources, by system:

- **Modules:**
  - Valheim workbench: https://timesaver.gg/blog/valheim-workbench-upgrades-crafting-station-levels and https://steamcommunity.com/app/892970/discussions/0/3817418437352836209
  - Satisfactory: https://satisfactory.wiki.gg/wiki/Clock_speed and https://satisfactory.wiki.gg/wiki/Production_amplifier
  - Factorio: https://wiki.factorio.com/Productivity_module_2
  - V Rising floors: https://www.bisecthosting.com/blog/v-rising-flooring-guide-types-bonuses-unlock-requirements-crafting-ingredients
  - Enshrouded: https://pindrop.gg/enshrouded/crafting/upgrades and https://steamcommunity.com/app/1203620/discussions/0/4142816460198438649
- **Refunds:**
  - Palworld: https://game8.co/games/Palworld/archives/440499
  - Valheim: https://steamcommunity.com/app/892970/discussions/0/3272436223667464231
  - Enshrouded altar: https://attackofthefanboy.com/guides/enshrouded-does-extinguishing-a-flame-altar-refund-upgrade-materials/
  - Raft: https://steamcommunity.com/app/648800/discussions/0/3050610473979915200
- **Pacing:**
  - Enshrouded survivors: https://allthings.how/enshrouded-1-0-why-crafting-depends-on-rescuing-craftspeople/
  - Terraria: https://terraria.wiki.gg/wiki/House
  - Palworld 1.0: https://ingamenews.com/pc-gaming/palworld-1-0-why-its-onboarding-appeals-to-new-survival-players/
  - Stardew: https://www.gamesradar.com/games/simulation/if-i-made-stardew-valley-again-i-would-try-to-be-a-little-more-nuanced-eric-barone-says-the-community-center-versus-jojamart-battle-was-too-black-and-white
  - Vintage Story: https://www.vintagestory.at/forums/topic/12233-structures-and-good-progression-this-game-does-it-right/
- **Inventory:**
  - Terraria: https://terraria.wiki.gg/wiki/Quick_Stack_to_Nearby_Chests
  - Grounded 1.1: https://player.one/grounded-extra-backpack-storage-and-hot-pouch-changes-implemented-update-152833
  - Enshrouded Update 8: https://hacktheminotaur.com/enshrouded/enshrouded-forging-the-path-update-8-full-patch-notes/
  - Valheim mod: https://thunderstore.io/c/valheim/p/Goldenrevolver/Quick_Stack_Store_Sort_Trash_Restock/
- **Night and fuel:**
  - Valheim torch mods: https://thunderstore.io/c/valheim/p/ComfyMods/TorchesAndResin/changelog
  - Don't Starve: https://dontstarve.wiki.gg/wiki/Guides/All_About_Night
  - Minecraft: https://minecraft.wiki/w/Tutorials/Spawn-proofing
- **Walls:**
  - Gord: https://pcgamer.com/gord-review
  - Manor Lords: https://wiki.hoodedhorse.com/Manor_Lords/0.8.035_-_Beta
  - Sons of the Forest: https://steamcommunity.com/app/1326470/discussions/0/3770113150036163982
- **Options:**
  - Xbox Accessibility Guidelines 114 and 115: https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/114 and https://learn.microsoft.com/en-ie/gaming/accessibility/xbox-accessibility-guidelines/115
  - Game Accessibility Guidelines: https://gameaccessibilityguidelines.com/ensure-that-all-settings-are-savedremembered/
- **Horses:**
  - RDR2: https://www.rdr2.org/guides/red-dead-redemption-2-horse-taming-guide/
  - BotW: https://play.nintendo.com/news-tips/tips-tricks/how-to-tame-horses-in-legend-of-zelda-breath-of-the-wild/
  - Ark: https://ark.wiki.gg/wiki/Narcotics
- **UI:**
  - Path of Exile sockets: https://www.pcgamer.com/games/rpg/after-13-years-path-of-exiles-devs-are-finally-ready-to-ditch-one-of-its-most-iconic-and-frustrating-mechanics/

The research found no usable sources for Grounded's or Sons of the Forest's refunds, Nightingale, Dragonwilds' bench tiers, or the walls in Banished and Against the Storm. Some Valheim figures came from guides that disagree with each other.

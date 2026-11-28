# The Purification Altar's UI: research and design (2026-11-28)

The user (2026-11-28): the altar holds many of the systems that matter for progression, and "lore, art and magic should be visible
in the UI"; "good feedback on enchanting, empowering and everything you can do there"; "more art from LocalSpawn, with good icons";
"a good presentation of enchantments"; "now everything is brown buttons with text"; "look at how other games have done it, with
feedback on buttons, animations, sound".

## What other games do (research agent; cited where a source was found)

| Game | Take from it |
|---|---|
| Diablo IV (Occultist, Blacksmith) | Scale the ceremony to the step: a short pulse for a rank, the long reveal only at milestones (Season 4 cut the animation on ordinary ranks; [Wowhead](https://wowhead.com/fr/news/masterworking-can-no-longer-fail-in-diablo-4-season-4-339193)). Ranks as a row of pips. |
| Diablo III Mystic | Choices as side-by-side cards make a reroll a decision. |
| Path of Exile 2 | Repeated actions fast, the sound carries the feedback; preview of the next result ([Mobalytics](https://mobalytics.gg/poe-2/guides/0-3-crafting-changes)). |
| Last Epoch forge | The item in the middle, its slots round it; the cost always in the same place, shown before the commit ([Icy Veins](https://www.icy-veins.com/last-epoch/crafting-guide)). |
| Hades I/II | A picture, one lore line, a frame colour by rarity and a chime are enough; the station itself is lore (the Mirror). |
| V Rising | One colour per school, through icons, passives and jewels. |
| Grim Dawn | Devotion: magic as a lit map, star by star with a tone. |
| Destiny 2 | Hold-to-confirm with a radial fill, a stinger and a glint; a finished item keeps a visible mark. |
| Monster Hunter | Before/after columns with arrows; sockets drawn on the item. |
| Skyrim | Learned from finds, like our Inscriptions; its list-only UI is the thing not to copy. |
| Lost Ark | Odds and pity shown live make risk feel fair ([GamesRadar](https://www.gamesradar.com/lost-ark-engravings-ability-stones)). |
| Elden Ring | Clear before/after numbers, one paragraph of lore. |

## The design

- **Tabs** with icons: Purify, Empower, Enchant, Abilities (names kept; the research suggested Cleanse/Kindle/Inscribe/Deepen).
- **Layout:** the subjects as picture cards on the left; **the stage** in the middle (the chosen item large on a slowly turning
  ring of glyphs, its +N pips and its enchantment shown on it); on the right a before → after card with the changed numbers in gold
  and the cost always in the same place.
- **The commit moment** (one `AltarCommit` for every tab): hold to confirm (~0.7 s; a tap for Purify) with a radial fill, the
  item's glow rising and a rising charge sound; letting go early fades it back. On commit: a short freeze, a white-gold flash, a burst
  of sparks, the item punched 1 → 1.12 → 1, a small shake of the stage only; numbers count up with ticks; a result card slides in.
  Three intensities (small: Purify, Empower +1; large: Empower +2 and up, a skill rank; milestone: first enchantment of its kind,
  great or cursed enchantments). A click skips. Reduced motion (Screen Flashes option) keeps the sound and the card, drops bursts.
- **Enchantment cards:** a painted icon (LocalSpawn, dark skill-icon style) in a frame by kind (plain gold, great bright gold,
  cursed red, relic bronze), the name, one lore line on hover, where it is learned when it isn't yet (dark silhouette).
- **Art:** 57 enchantment icons (Tools/LocalSpawn/enchantment_icons.json, `--set enchantments`). Later: tab header paintings, family
  glyphs, essence art.
- **Sound** (Tools/Audio, synthesised first): charge loop, cancel down-sweep, three commit stingers, count tick, select chime.

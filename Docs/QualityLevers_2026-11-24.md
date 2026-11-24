# Where quality is cheapest to win (2026-11-24)

The user: "what system can we improve which will mostly increase the quality of the game? ... no balancing or changes that require a
lot of playtesting." Nothing is built from this yet.

**Short answer:** the game isn't missing systems. It loses quality in three places a player meets every session, and none of them
needs balance data:
1. **Teaching and windows.** Reading replaces doing.
2. **Performance we can't see.** Nothing is measured on friends' PCs.
3. **The most common enemy glides.**

---

## 1. Teaching and windows (recommended first)

**Evidence**
- Windows are about **48% of home time**, and home is 58-72% of play (CampaignAudit_2026-11-23 §2). This is where players spend the
  most time.
- The 11-23 run is the first with the per-window breakdown. In its first 13 minutes at home: Items 65 s, **Tutorial 56 s**,
  Dialogue 38 s, Raid planner 27 s, Journal 25 s.
- `Resources/JournalSettings` holds **58 tutorial pages: median 81 words, the longest 279**, and 30 are over 80 words. That breaks the
  standing rule "pictures with names, words on hover; no text walls" (it was applied to cards, not to tutorials).

**What to build (UI and content only, no numbers change)**
- **Each tutorial as a card:** a picture or short looping clip, a title, at most about 25 words, and the keys as key icons. The
  full text moves behind "More" in the journal, so nothing is lost.
- **Learn by doing:** a page opened by a first action (the first tree, the first cast) shows as a small in-world prompt ("Hold
  {key:Attack} on the tree") that finishes when the player does it. The page then goes to the journal unread-free. Today it waits
  in a window.
- **One-step window trips:** the logger now names the window, so measure the five most-opened windows and cut the clicks in the
  commonest trips: craft from a station, store the haul, plan a raid. Look for confirmations, tab hops and scrolls that could be
  defaults.

**Why first:** the first hour decides whether a player stays. This is also where the most measured time goes, and it is fully
testable without balance (word counts, clicks per trip, `TutorialTests`).

## 2. Performance we can't see (cheap, do alongside)

**Evidence**
- Performance and crashes are the most frequent complaint in survival-crafting reviews: low FPS on recommended hardware, memory
  leaks, crashes. Enshrouded's early-access praise was mostly "feels polished, bugs kept at bay".
- Our only numbers are the 2026-11-08 benchmark on the **RTX 4090 at 4K**:
  - GPU 8-13 ms, CPU main 6-7 ms;
  - **"worst arriving" hitches of 166-191 ms at every spot**: a visible stutter each time the player arrives somewhere.
- The playtest CSV has **no frame time, GPU, resolution or quality preset**. The F8 report reads fps from `Time.smoothDeltaTime`,
  so a paused moment reports **10000 fps** (the 11-21 report).
- Build `Player.log`: `NullReferenceException` in `StationProgressBars.OnDisable` on scene unload (both recent logs), and FiraSans
  has no ellipsis glyph, so text overflow falls back to Truncate.

**What to build**
- **A `Perf` row per scene per minute:** p50, p95 and p99 frame time, hitches over 50 ms, and the scene and place. Add a `Machine` row
  at session start: GPU, VRAM, RAM, resolution, preset. The friends' next CSVs then show who struggles and where.
- **Hunt the 170-190 ms arrival hitch:** profile one arrival, then warm up or spread the work (likely streaming, shader variants or
  instantiation).
- **Small fixes:** the F8 fps (`unscaledDeltaTime`, averaged), the `StationProgressBars` NRE, and an ellipsis in the font atlas or
  fallback.

## 3. The most common enemy glides

**Evidence**
- Wolves are 38% of tier I spawns. The 11-20 animation audit left **"beast glide (wolf clip feet ~1 m/s vs 5.5 m/s chase, even at
  rate 2.2)"** open, along with monster and beast turning in place, wolf hit and stagger loops, and the Rootling glide.
- Players read sliding feet on the thing they fight most as "cheap", whatever else is polished.

**What to build (looks only, chase speed unchanged)**
- **A real run or gallop clip** for the wolf (and Rootling) with a stride matched to its speed: a blend tree by speed, playback
  rate from measured foot speed, and foot planting on the gallop.
- **Turn in place** for beasts: a pivot clip when the facing error is large, instead of the body rotating on planted feet.
- **Checked by measurement:** foot speed against ground speed in a probe, which is the same method the audit used.

---

## Smaller notes found on the way

- The `Stuck` watch flags `SpeedChanged` held 180 s by `HavenPaths` and `PlayerEquipment` (5 of 7 rows in the long run). If the
  paths bonus and carry weight are meant to last, the watch should ignore them. Otherwise the noise hides real stuck states.
- An automated walk through the first hour of quests in a build (beyond `SceneSmokeLiveTests`, which enters scenes but doesn't
  progress quests) would catch integration breaks in the many built-but-unplayed systems. It is bigger work, so it goes after the
  three above.

## Sources

- [ICARUS review summary (vaporlens)](https://vaporlens.app/app/1149460/icarus): optimisation and memory complaints
- [Romestead review summary (vaporlens)](https://vaporlens.app/app/1805320/romestead.md): FPS drops, artifacting
- [Enshrouded review summary (vaporlens)](https://vaporlens.app/app/1203620/enshrouded)
- [Enshrouded early access review (God is a Geek)](https://godisageek.com/2024/01/enshrouded-is-already-huge-and-its-only-going-to-get-bigger-early-access-review/): "decent level of polish that kept bugs at bay"
- [Steam thread: negative reviews on durability, inventory management](https://steamcommunity.com/app/1702260/discussions/0/3424439048262297809)
- [GamesRadar: a survival game at 17% after bugs](https://www.gamesradar.com/games/survival/facing-a-17-percent-review-score-on-steam-survival-game-dev-says-sorry-about-the-bugs-and-promises-to-add-a-way-to-uh-save-the-game/)

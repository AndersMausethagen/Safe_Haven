# Game design research: feel, first hour, living world, tension, folklore, co-op, access (2026-11-17)

The user asked for one agent to research game design and world design methodically and suggest improvements that fit this
game. Today's other reviews already cover the systems economy (`SystemDesignReview_2026-11-17.md`), map structure
(`WorldDesignReview_2026-11-17.md`), the look (`WorldLookPlan_2026-11-17.md`), the Heartroot fight
(`HeartrootBossReview_2026-11-17.md`) and each raid's identity and loot (`RaidIdentityResearch_2026-11-17.md`). This document
leaves those alone and looks at what they didn't cover in depth:

1. moment-to-moment feel and readability;
2. the first hour;
3. emergent stories and the living world;
4. raid tension and sound;
5. folklore as rules;
6. co-op;
7. retention without grind, accessibility and difficulty.

**Method.** Per area: what the best games do ([P] primary or developer source, [S] press, wiki or forum), where Safe Haven
stands (checked read-only in code, data and playtest CSVs), then suggestions with problem, change, data or code, files, impact,
effort (S/M/L) and risk. **[Decision]** marks those needing the user's yes.

**Respected:** the user's standing calls (no functionality removed, no hidden under-gear penalty, quests held for homecomings,
trust only through quests and the bond, the portal is the voice, `Tools/Audio` first, warned home attacks). Decision 8.16 froze
new scope, so most suggestions are small and sharpen what exists.

---

## 0. The short answer

The game is unusually complete for its stage. The gaps are at the seams, where a system exists but the player can't read it
or feel it:

- **Some blows can't be read:** the off-screen grace measures an angle, not the screen, and ranged attacks have none (§1).
- **The camera pulls in when a fight starts** (about 21 m to 16 m), against GDD §10.1.
- **Dying teaches little:** the killer's own advice (`journalAdvice`) is in the data but not on the death screen.
- **The music doesn't know the raid:** no sound for danger stages or the held beacon; the final stretch (last 60 s) is almost
  never heard in 5-minute raids.
- **Weather is secret:** rain already hides noise, but the briefing never says it will rain.
- **Stories happen but aren't kept**, and **folklore is per creature, not laws learned once.**

### Top 12, smallest effort for the biggest gain first

| # | Suggestion | Effort | Impact | Kind |
|---|---|---|---|---|
| 1 | **G1** On-screen strike grace by the real viewport, ranged included, with edge pips | S | High | Code |
| 2 | **G3** Death recap: the last three blows and the killer's own advice | S | High | Code (data exists) |
| 3 | **G2** The fight camera keeps the explore distance (data), then frames engaged foes (code) | S, then M | High | Data, then code. Playtest by eye |
| 4 | **W3** Weather foretold in the briefing, with its one rule | S | Medium-high | Data + code |
| 5 | **A3** Telegraphs that read without colour (dashes, notches, a sound per tier) | S | Medium-high | Code + data |
| 6 | **A1** "Hearth's Grace": Damage Taken that grows a little with each fall | S | Medium | Code |
| 7 | **T1** Raid music follows the danger stages and the held beacon | S-M | High | Data + small code |
| 8 | **T2** Each new stage reveals one richer site on the map: a reason to stay | S-M | Medium-high | Data + code |
| 9 | **C1** The raid invite shows each friend's kit against the tier | S | Medium | UI code |
| 10 | **F1** The Old Ways: folk laws learned once, true everywhere | S-M | High | Mostly data |
| 11 | **W1** Bond scenes: each companion rank opens a personal tale | M | High | Data (writing) + small code |
| 12 | **W2** The raid's tale and the Haven Chronicle | M | High | Code |

These come next: C2 (things that go better with two), O2 (one-line hints in the voice from the stones), A2 (combat speed
assist), A4 (raid length assist), O1 (the first fall as a lesson, a **[Decision]**) and W4 (an audit of home events).

---

## 1. Moment-to-moment feel and readability

### What the best games do
- **Readability is fairness.** A GDC talk on ARPG difficulty frames readability as what makes a fight feel fair. The gap between
  telegraph and impact must leave time to see and answer [S: Game Developer, ARPG readability].
- **Off-screen attacks are the top-down genre's chronic complaint.** Last Epoch and Path of Exile players converge on the same
  rules. Telegraphed attacks from off-screen are acceptable; surprise projectiles are not. An enemy may attack from off-screen
  if it has been on screen once. Ground marks or edge cues let the player answer what they can't see. Some ask instead that
  heavy attacks come only from on screen, or that the camera zoom out [S: Last Epoch forum; PoE forum].
- **Tunic's team** worked after launch on clearer, heavier attacks (shake, vignette, telegraphs) [S: itch.io dev reply]; Death's
  Door's fixed camera is credited with keeping enemies trackable [S: Byteside; player opinion].

### Where we stand (verified)
- Feedback is strong (`CombatFeedback` hit stop, shake, reactions by weight, telegraph tiers, rumble, captions). Keep it all.
- **The off-screen grace measures the wrong thing.** `MonsterAI.OffScreen` counts a foe as off-screen when its flat angle from the
  camera's forward exceeds `offScreenAngle` = 70°. With the camera about 10-13 m behind the player, a foe 15 m ahead
  (beyond the top edge) or 8 m behind (below the bottom edge) is at about 0° and counts as *on* screen. Only foes level
  with the camera, to its sides, get the grace.
- **Ranged attacks have no off-screen rule.** `WaitsToShoot` caps how many shoot at once (`RangedAtOnce`) but never asks whether
  the shooter is in frame. Bog spit, troll stones, wisp bolts and the Heartroot's Thornwave can come from outside the frame.
  Since tonight, creature bolts pass through their own kin, so a pack no longer screens its archers.
- **The camera** (`Data/Camera/PlayerCameraSettings.asset`) explores at 14-28 m and fights at 12-20 m, both at FOV 40. At the
  default zoom share (0.5) that is about 21 m exploring and about 16 m fighting. The world review measured the explore
  frame at roughly 30 × 22 m, so a fight sees less.
- **Death:** `RaidReportText.Describe` writes "Brought down by a {creature}". The `Hurt` rows (blows over 5% of health:
  attacker, damage, attack) are logged for telemetry. Every monster has `journalAdvice` ("Weak to blunt blows… get out, then back
  in"), but the death screen doesn't use it.

### Suggestions

**G1. Strike grace by the real viewport, ranged included, with edge pips** (S · High · Low risk · code)
- *Problem:* blows from out of frame break the GDD promise "risk is legible" (§1.4), and the anti-overwhelm rule misses the two
  edges that matter most top-down, ahead and behind.
- *Change:*
  - Make `OffScreen` use `Camera.WorldToViewportPoint`, with a margin of about 5% of the screen.
  - Give `WaitsToShoot` the same grace: a shooter out of frame holds its shot until it has been seen once, or until
    `OffScreenWait` has passed.
  - Draw a small pip on the screen rim, on the side of any wind-up aimed at the player from out of frame. `StealthReadout`
    already draws an edge arrow, so reuse its look.
- *Where:* `AI/MonsterAI.Fighting.cs` (`OffScreen`, `WaitsToShoot`), `Combat/CombatTechniqueSettings.cs` (a viewport margin),
  a new edge-pip view beside `StealthReadout`.
- *Co-op:* the rule applies only to this game's own player (as today). A friend's frame isn't known on the host, so friends
  keep today's behaviour.
- *Check:* `LivingWorldTests`-style plain tests on the viewport rule, and a probe with a Bog Spitter placed 16 m north.

**G2. The fight camera stops pulling in** (data S, then code M · High · Medium risk: feel)
- *Problem:* the camera comes in about 25% when a fight starts, just when the player needs the most space. GDD §10.1 says it should
  "zoom out slightly for large fights".
- *Change:*
  - Step 1 (data): set the Combat profile's distances to the Explore ones (14-28 m), keeping its steeper 58° pitch and the
    cursor look-ahead.
  - Step 2 (code, after the user has looked): while engaged, frame the player and every foe hunting them, eased and capped at
    1.25 × the player's chosen distance. A boss counts as two foes. This is Cinemachine's target-group idea, done in
    `PlayerCameraController` beside `SetLead`.
- *Where:* `Data/Camera/PlayerCameraSettings.asset`, `Camera/PlayerCameraController.cs`.
- *Risk:* it is a feel change, so the user's eye decides. Camera Zones and dialogue framing are untouched.

**G3. Death recap** (S · High · Low risk · code, the data exists)
- *Problem:* a death teaches only what killed you. The game already knows how, and what the creature's page says to do.
- *Change:*
  - Keep a ring of the last three blows of 5% or more: attacker, attack name, damage, and whether it was a heavy, unblockable,
    from behind or from off-screen (G1's test).
  - On a fall, show them under "Brought down by…", with the killer's `journalAdvice` beneath in the muted colour and a link
    to its journal page.
  - Hades and most extraction games show this; it turns a death into a lesson, which Pillar C needs.
- *Where:* a small `RecentBlows` holder fed by `Damageable.AnyDamaged` (the same feed as the `Hurt` rows), `RaidReport`,
  `UI/RaidReportText.cs`.

---

## 2. Onboarding and the first hour

### What the best games do
- **Valheim's Hugin** appears in the world at the moment a mechanic or milestone comes up, and fades away as the player grows
  [S: GameWith, Mobalytics].
- **Dragonwilds** (1.0, 2026) is criticised for objectives that say *what* but not *how*: one reviewer spent ten minutes on a
  gathering quest. Another needed six to ten hours to see how the systems interact [S: Gfinity; Pocket Tactics].
- **Hunt and ARC Raiders** soften early losses: a free kit, and no permanent loss below Hunt's rank 11 (from the system review's
  sources).

### Where we stand (verified)
- **A real human run** (`20261115-184337.csv`, with caveats: it has 13 `Cheat` rows):
  - the prologue ends at 16.4 min, after seven quests;
  - the four Haven steps to the door take 3 min;
  - the **first raid starts at 20.8 min** and lasts about 3.5 min;
  - the next Haven quests come in pairs.
  This is on target (`QuestRedesign.md`: about 22 min).
- The rule "never ask before teaching" is held by a test (`QuestAuditTests`). The loss rules are taught before the first raid,
  in `ThroughThePortal` and `SafeKeeping`.
- **Tutorials** are journal pages (58 painted images) that queue quietly (`TutorialNoticeHud`) when a mechanic is first
  tried. They are good, but they are reading, and they reach the player after the moment.

### Suggestions

**O1. The first fall is a lesson** (S · Medium · **[Decision]**)
- *Problem:* a new player's first failed raid costs 25% wear on a starter kit they can barely afford to mend, while still
  learning what "at risk" means.
- *Change:* the first failed raid of a journey costs the carried items as usual but no wear. The portal then explains what was
  lost, what was kept, and what the safe pocket would have saved. Hunt and ARC soften the start the same way.
- *Where:* `PlayerSnapshot.AfterFailedRaid`, a flag in `CharacterProgress`, a portal line.
- *Risk:* it softens Pillar C once. The user has said "rules should stay consistent", hence the decision.

**O2. One-line hints in the voice from the stones** (S-M · Medium · data + small code)
- *Problem:* raid mechanics such as a stage change, a beacon contested, "at risk", or a window closing are taught by pages
  or by the HUD's line, never by the world.
- *Change:*
  - For a character's first three raids, the first time each of about eight raid moments happens, Sigrid's or the portal's
    voice speaks one subtitled line through the stones. Sigrid's voice already comes through the stones in §21.3, so there is
    a precedent.
  - Example lines: "Don't stand in the light alone, they come to it." / "The mist is thickening. It's time to think of home."
  - The page follows in the journal as today. After three raids the voice stops, as Hugin does.
- *Where:* new lines in `VoiceBank` (IndexTTS, as the voice-lines note says), a small listener on `RaidDirector.StageChanged`,
  `ExtractionHold` states and `RaidRisk`.

**O3. A first-hour funnel report** (S · Medium · Editor tool): time to each milestone (prologue end, door, first raid, first
extraction, Ingrid home, first breach, Ward I) with deaths and quits per step and cheat runs flagged, from rows already logged.
One table for the slice validation (decision 8.16).

---

## 3. Emergent stories and the living world

### What the best games do
- **RimWorld** is "designed at every level to generate story". Its storyteller is "an algorithm that generates events from a
  categorized database, based on a few statistics about what's happening", following a rising and falling curve so as not to
  crush struggling players, after Left 4 Dead's Director [S: PC Gamer; RimWorld wiki].
- **Stardew Valley's heart events** are short scenes unlocked by friendship at set places and times, and mods show players
  want *more* of them [S: Punished Backlog; mod pages].
- **Valheim's base events** are warned on screen with ominous music and last under three minutes [S: TechRaptor].
- **Hunt's Thundershower** makes weather play: rain masks footsteps, "the storm naturally muffles players' ability to pinpoint
  sounds", fog cuts sight, and ravens are disturbed [P: huntshowdown.com].

### Where we stand (verified)
- **The Haven is alive** (wildlife, glimpses, asides via `ResidentNpc.News()`, outings, seasons, festivals, blight, spirits that
  remember).
- **Companions:** `CompanionBond` ranks bring health and damage shares plus *one memory line* per raid. No relationships; the
  user's call is trust only through quests and the bond.
- **Weather:** raids seed weather from the run, and rain already reduces the player's noise (`PerceptionRules.RainNoiseScale`,
  0.7). The briefing (`RaidPreparationWindow*`) never mentions weather.
- **No chronicle.** The raid report is rich (rescues, companion lost or found, transport, haul, "brought down by"), but it is
  gone once closed.

### Suggestions

**W1. Bond scenes** (M · High · data writing + small code)
- *Problem:* Pillar D promises "small personal stories… and wishes". Bond ranks give stats and a line, but no story.
- *Change:*
  - Each `CompanionBondRank` reached with a named resident offers that resident's own short Tale: a quest of two or three
    steps, a scene at home and one ask on a raid, much like a Stardew heart event. Examples: Bjarne's father's hammer, Runa's
    debt to the man in grey, Sigrid's map.
  - The quests are held for homecomings by the existing Story Pacing, so they don't add to the goal count (K14). Finishing one
    gives a recipe, a decoration or a voice line, never a stat.
  - This fits the user's rule that trust comes through quests and the bond.
- *Where:* `ResidentDefinition` gets a list of quests by rank, and `SettlementState` starts the right one when a rank is
  reached (beside `CompanionMemory.RoseInRank`). Quests go in `Data/Quests`, and `QuestAuditTests` covers them.
- *Risk:* writing load. Start with the three prologue companions.

**W2. The raid's tale and the Haven Chronicle** (M · High · code)
- *Problem:* Pillar B's goal is "a story the player can tell". The game holds the facts and forgets them.
- *Change:*
  - At extraction or a fall, compose one sentence from the report's biggest facts, ranked: a boss, a rescue, a companion lost or
    found, a close call (under 60 s left), the beacon held while contested, the killer, the hour.
  - Example: *"At dusk in the Misty Woods you found Bjarne at the Old Smithy, and held the West Beacon with the Old Moss Troll
    in the circle; twelve breaths were left."*
  - Show it at the top of the report and keep it in a **Chronicle** journal tab, newest first.
  - Feed the best line of the last raid to `ResidentAsides` news, so residents retell it ("They say you held the West Beacon
    with the troll inside it").
  - Co-op: each friend's game writes its own line, naming the friends.
  - This is RimWorld's tales and Dwarf Fortress's legends, at the scale of one sentence.
- *Where:* a pure `RaidTale.Compose(RaidReport)` (testable), `RaidSummaryWindow`, the Journal, `ResidentNpc.News()`.

**W3. Weather foretold, and felt** (S · Medium-high · data + code)
- *Problem:* rain already helps sneaking, but the player can't know it or plan for it. Planning is the GDD's survival goal (§1.3
  "weather influence[s] plans").
- *Change:*
  - The briefing shows the run's weather, pinned to the turn like the hour since K2, with one line of what it does: "Rain: your
    steps carry 70% as far."
  - Give each weather in raids one rule, all from existing knobs:
    - rain hides noise (as built);
    - fog shortens sight for both sides (`SightAtNight`-style);
    - storm: thunder masks the beacon's call, but lightning shows you;
    - snow leaves your tracks for hunters (prints exist).
- *Where:* `RaidPreparationWindow.Briefing.cs`, `WeatherDefinition` (a briefing line and a perception scale), `RaidSky`.

**W4. An audit of home events, not new ones** (S · Medium · test)
- Add one rule and a test: **at most one big home event per homecoming** (a breach, a corruption night or a festival); a second
  waits for the next dawn. RimWorld's rising and falling curve, and no repeat of the 2026-11-15 overload.
- *Where:* `WardBreachDirector`, `CorruptionNights`, `Festivals`, a shared check in `SettlementState`.

---

## 4. Raid flow and tension: sound, signals, a reason to stay

### What the best games do
- **Hunt:** sound rests on "feedback, immersion and emotion"; animals give positions away; "hearing nothing special can create
  more tension" than a firefight [P: PlayStation Blog; S: PC Gamer].
- **ARC Raiders:** calling the lift is loud and lit, and draws ARC and raiders. Embark tightened the Raider Hatch because 40% of
  playtest extractions used it, against "the heart-pounding tension of a desperate sprint". Watkins: give players many chances
  to notice others [S: MP1st; Kotaku interview]. The map marks high-loot areas, which draw traffic (world review).
- **Valheim:** each event has its own warning music [S: TechRaptor].

### Where we stand (verified)
- **Music:** there are two moods (`AudioMood.Exploring`, `Combat`), plus event music and `SceneAudio.finalStretch` for the last
  `finalStretchSeconds` = 60.
  - The Misty Woods, Troll Valley and Moss Marsh scenes all use **the same final-stretch clip and the same ambience clip**.
  - Real raids end at 3.4 to 5.6 min of 12 (system review K8), so the final stretch is almost never heard.
  - The stages (The Mist Stirs 8:00, The Hunt 4:00, Collapse 1:00) post only a message.
  - The user plans music and ambience per raid later.
- **Signals** are good (the beacon's call, bird alarms, troll footfalls at 55 m, captions with sides).
- **Reasons to stay:** the GDD's emotional table wants "one last temptation" near the deadline, and stage loot is invisible
  (K8). Events can wait for a stage (`RaidEventDefinition.minimumStage`), but they appear only when the player stumbles on them.

### Suggestions

**T1. The music follows the raid** (S-M · High · data + small code)
- *Problem:* the soundtrack doesn't track the three things that make an extraction raid: danger rising, the way home, and
  holding the light.
- *Change:*
  - `SceneAudio` gets a playlist (or an intensity layer over the exploring loop) per danger stage, plus a stinger at each stage
    change: a low horn or a fiddle scrape.
  - A **held-beacon layer** plays while `ExtractionHold` fills: a slow frame-drum heartbeat that quickens with the fill and
    drops out when contested.
  - **Deliberate silence:** the exploring music thins out for a minute after The Hunt begins, leaving the ambience. This is
    Hunt's quiet tension, and it makes footfalls and crows the music.
  - The final-stretch rule stays.
- *Where:* `Audio/SceneAudio.cs`, `MusicRules`, `RaidDirector.StageChanged`, `ExtractionHold`. Stingers and drum from
  `Tools/Audio` first; each raid gets its own set when its music comes.

**T2. A new stage marks one richer site** (S-M · Medium-high · data + code)
- *Problem:* players leave at about 5 minutes because nothing shows what staying would earn. The system review's R8 measures
  this; this suggestion gives the measurement something to move.
- *Change:*
  - At each stage start, one site the player hasn't found yet gets a gold mark on the minimap's rim, with a line: "Something
    glints at the Old Barrow. The mist is thickening."
  - Its cache rolls with that stage's loot bonus, so the "+25%" becomes a place.
  - It is a temptation the player can see, away from the way home. ARC's marked high-loot areas work the same way.
- *Where:* `RaidHudView`/`RaidAlertLine`, `MinimapHud` marks (rumour marks exist), `RaidEventDirector` or `LootCache` stage
  roll, data on `RaidStage` (a reveal kind).
- *Risk:* low. It uses the existing rumour mark style, and the playtest heatmaps show whether players go.

---

## 5. Folklore as rules the player learns

### What the best games do
- **Bramble: The Mountain King** set out to avoid Vikings: creatures "heavily inspired by Nordic folklore and stories we were
  told as kids", including Näcken, with a folklore expert consulted [P: Unreal Engine interview; S: Into Indie Games].
- **Kittelsen** painted the forest troll as "an anthropomorphised mountain" and dwarfed people under vast landscapes (*Echo*,
  1888). That is scale as dread [S: Apollo; David's Art of the Day].
- **The tradition is consistent.** Steel in the water binds the nøkk ("Nyk, nyk, nål i vatn"), iron and steel scare the hidden
  folk, naming the nøkk drives him off [S: Wikipedia, Nixie; summaries]. The project's own SNL research adds steel over doors
  (Oskorei), bells for the taken, and daylight stoning trolls.
- **Tunic's** hidden manual makes knowledge the reward, so the player grows, not the numbers [P: Game Developer].

### Where we stand (verified)
- **Folk rules exist one by one:** the Draug's `RisesAgain` ("only a finishing blow or fire ends it"), the Huldra's silk if left in
  peace, the Nøkk's air; planned are Steel Needles for the Nykk, steel over doors for the Ride, bells, and trolls stone at dawn.
- **Missing:** nothing presents these as *one world's laws*. A player who learns "fire ends the dead" has no hint it also
  holds for the next undead.

### Suggestion

**F1. The Old Ways** (S for the frame, M overall · High · mostly data)
- *Problem:* folklore reads as flavour on separate creatures. The GDD promise "folklore shapes play" (§1.4) is strongest when the
  laws are few, consistent and learned once.
- *Change:*
  - Five laws, each a journal page under **The Old Ways**:
    - *Steel binds the hidden folk:* Huldra, Tusse, Nykk/Nøkk, Oskorei;
    - *Fire ends the dead:* Draug, and later Deildegast and Utburd;
    - *Daylight stones a troll;*
    - *A bell calls back the taken;*
    - *Say its name and it loses its hold* (the Nøkk; later the Jaw-keeper?).
  - Each page is learned in the world: from a resident (Liv the Rune Scholar, Ingrid), a carved runestone, or the first time
    the law works in a fight.
  - Every creature page names its law ("The Old Ways: fire ends the dead").
  - **Every new creature must obey the laws.** Add an audit test: each `MonsterDefinition` marked hidden folk, undead or troll
    has the matching response.
- *Mechanics:* most exist or are planned. What's new is small:
  - a Tusse can't take from a bag that carries steel (any iron weapon or tool counts, so this is nearly free);
  - the Huldra breaks off a lure when shown steel (the existing Show Item or interact verb);
  - the Nøkk rule in fishing: a Steel Needle in the water ends the drag.
- *Where:* `Data/Monsters/*` (a `FolkKind` field), `Journal`, `MonsterAI.Thief.cs`, `FishingSettings` (Nøkken), a
  `ContentAudit` test.
- *Impact:* identity and mastery that no grind can buy, which is the retention kind this game wants.

*Kittelsen note:* give the Valley's planned "noise wakes the troll-stones" his scale on purpose: a resting troll reads as a
mossy hill with a tree on its back until it wakes.

---

## 6. Co-op

### What the best games do
- **Deep Rock Galactic** is "co-op first". Its classes are asymmetric and "none of them function as well alone as they do
  together". The developers say they spent little time on class balance because it is co-op [S: Co-Optimus; GamingBolt;
  Games.gg]. It has shared objectives, one drop pod everyone runs to, and a salute everyone joins in ("Rock and Stone").
- **Valheim** does no level scaling: friends bring gear, and the host's world sets the danger (see the system review's
  sources).

### Where we stand (verified)
- It is thorough: personal loot, +60% creature health per friend, revive, spectating, pings, a party panel,
  `TogetherExperience`.
- **Gaps:**
  - Nothing in a raid needs, or goes better with, a second player beyond revive.
  - A guest with a tier I kit can follow a host into tier III with no warning before the portal. The user's rule forbids a
    hidden under-gear penalty, so the answer is to show the gap, not scale it.

### Suggestions

**C1. The invite shows each friend's kit against the tier** (S · Medium · UI code)
- The raid planner and the invite list each player's entry kit (`GearBalance.EntryKitOf`) as a tier: "Runa's kit: tier I · this
  raid: tier III · hard."
- The host can pick knowing it, and nothing is scaled.
- *Where:* `RaidPreparationWindow`, `RaidInvite` (adds the guest's kit tier, a new struct field and wire format, per the
  `CoopMessageTests` rule), `PartyHud`.

**C2. Things that go better with two** (M · Medium-high · data + code)
- Solo play keeps today's numbers. Each extra player only adds:
  - **The beacon fills faster:** +35% per extra player standing in the circle (DRG's run to the pod). Contested still pauses it.
  - **Carts:** a second player at the handle lifts the cart's speed penalty by half (`Transport`).
  - **Two-hand sites:** one new set-piece kind, a fallen gate or a barrow slab that takes two to lift (solo: a slow lever, a
    crowbar item, or a horse hitched to it), with a better cache behind it.
  - **A shared moment at the end:** the extraction tale (W2) names the whole party.
- *Where:* `ExtractionHold` (players inside), `Haulable`/cart rules, a set-piece prefab plus `HavenSync`-free host logic
  (`CoopCreatures`-style host authority).
- *Risk:* none to solo play by construction. It needs a two-machine test, still owed.

---

## 7. Retention without grind, accessibility and difficulty

### What the best games do
- **Hades' God Mode:** 20% less damage taken, plus 2% for each death up to 80%. It can be switched off at any time with no
  consequence, and was built so players have time to learn patterns [S: Inverse interview; Lords of Gaming; guides].
- **The Game Accessibility Guidelines** say a choice of game speed "very effectively" eases timing and fast-processing problems.
  Celeste's Assist Mode offers 50-100% speed from the pause menu. Thorson proposed "combat speed while enemies are aggro'd" for
  Sekiro [P: GAG; S: Game Developer, Vice].
- **Retention that respects time** comes from knowledge (Tunic), stories (W1, W2) and opt-in difficulty with one-time bounties.
  The system review covers the last of these as Pact marks (R15).

### Where we stand (verified)
- **Built:** Damage Taken (Normal, 75%, 50%), Parry and Dodge Timing, Large Raid Timer, captions, Rarity Letters; assists are
  logged as `Assist` rows.
- **Still open in GDD §18.5:** "colour-independent signals". Telegraph tiers are blue, dark-edged and pale green, and
  blue against green is the pair tritanopia confuses.

### Suggestions

**A1. Hearth's Grace** (S · Medium · code)
- A fourth Damage Taken choice, "Grows with falls": it starts at 90% and each fall takes off 2 more, down to 50%. It is logged
  as an assist and can be switched off at any time.
- It answers the player who dies to the same troll five times without lowering the game for anyone else.
- *Where:* `Core/Options/GameOptions.cs` (`DamageTaken`), a falls counter in `CharacterProgress`.

**A2. Combat Speed** (M · Medium · code; single player only)
- An Accessibility slider from 70% to 100%: game speed while something hunts the player, eased in and out.
- It is off in co-op, because time is shared (`GamePause.TimeIsShared`), and logged as an assist.
- *Where:* `OptionsSystem`, `GameState.Combat`, `Time.timeScale` through `GamePause`.

**A3. Telegraphs that read without colour** (S · Medium-high · code + data)
- Shape codes the tiers:
  - interruptible: a dashed rim;
  - heavy: a solid thick rim (as now) plus the swell;
  - opening: a notched ring.
- Each tier also gets its own short sound, captioned with its side.
- This closes the open GDD item and helps everyone in busy fights.
- *Where:* `AttackTelegraph` (its material), `ReelingTell`, `SoundDefinition` captions.

**A4. Raid Length assist** (S · Low-medium · data + code)
- "Longer raids: +50%", logged as an assist. The danger stages count time *left*, so they keep their meaning.
- It helps players who read or move slowly, without touching the loss rules.
- *Where:* `RaidParameters.Calculate`, `GameOptions`.

---

## 8. Decisions for the user

| # | Question | My recommendation |
|---|---|---|
| D1 | **O1:** should a journey's first failed raid spare the gear wear (still losing what was carried)? | Yes, once. It teaches the rule without a repair bill a new player can't pay |
| D2 | **G2:** should the fight camera keep the explore distance (step 1, data), and later frame the foes (step 2)? | Try step 1 now and judge it by eye. Step 2 only if fights still feel cramped |
| D3 | **F1:** adopt the five Old Ways as laws every creature must follow? | Yes. Start with steel and fire, which are already half built |
| D4 | **W1:** a personal Tale per bond rank, starting with Sigrid, Eirik and Runa? | Yes. It is the trust-through-quests rule given a story |
| D5 | **C2:** bonuses for a second player (beacon, cart, two-hand sites)? | Yes, with solo numbers unchanged |
| D6 | **A2:** a combat speed assist (single player only)? | Yes, as an Accessibility option, logged |

---

## 9. Considered, and not recommended

Daily or weekly quests and login rewards (FOMO, against "slow but not grindy"); hidden co-op scaling (the gear rule; C1 shows
the gap instead); more home events (W4 caps them); screen-filling juice by default (reads as arcade); permadeath by default
(the user chose the return of the lost).

## Sources

- Game Developer, *Designing for difficulty readability in ARPGs*: https://www.gamedeveloper.com/game-platforms/designing-for-difficulty-readability-in-arpgs
- Last Epoch forum, off-screen attacks: https://forum.lastepoch.com/t/stop-off-screen-range-attacks/34989 · https://forum.lastepoch.com/t/my-occasional-complaint-about-off-screen-attacks/47185
- Path of Exile forum, off-screen ranged: https://www.pathofexile.com/forum/view-thread/3164667
- Tunic developer reply on clarity (itch.io): https://itch.io/post/14848120 · Game Developer, Tunic: https://www.gamedeveloper.com/road-to-igf-2023/how-tunic-weaves-wondrous-unknowable-worlds-inspired-by-inscrutable-nes-manuals
- Byteside, Acid Nerve on Death's Door: https://www.byteside.com/2021/08/deaths-door-developer-acid-nerve-on-how-it-made-its-sword-wielding-crow-game/
- Valheim Hugin: https://gamewith.net/valheim/article/show/28410 · https://mobalytics.gg/gamebase/guides/valheim-beginners-guide
- Dragonwilds reviews: https://www.gfinityesports.com/article/runescape-dragonwilds-v10-review-a-magical-world-punished-by-brutal-solo-survival · https://www.pockettactics.com/runescape-dragonwilds/review
- RimWorld storyteller: https://www.pcgamer.com/games/strategy/the-challenges-of-developing-the-colony-sim-from-dungeon-keeper-to-dwarf-fortress-and-beyond/ · https://rimworldwiki.com/wiki/About_RimWorld
- Stardew heart events: https://punishedbacklog.com/stardew-valley-heart-events-guide/ · https://www.bisecthosting.com/blog/stardew-valley-spring-schedule-npc-guide-marriage-candidates-locations-time
- Valheim events: https://techraptor.net/gaming/guides/valheim-forest-is-moving-base-raids-guide
- Hunt: Showdown sound: https://blog.playstation.com/?p=221483 · https://www.pcgamer.com/lets-hear-it-for-hunt-showdowns-incredible-sound-design · Thundershower: https://huntshowdown.com/news/behind-the-storm-technical-design-of-thundershower-in-hunt-showdown
- ARC Raiders: https://kotaku.com/arc-raiders-embark-extraction-virgil-watkins-interview-2000639879 · https://mp1st.com/news/embark-looking-to-change-the-raider-hatch-mechanics-in-arc-raiders-to-intensify-extraction-tension-following-overuse-by-players
- Bramble: https://www.unrealengine.com/en-US/developer-interviews/bramble-the-mountain-king-is-an-upcoming-horror-game-inspired-by-nordic-folklore · https://intoindiegames.com/features/the-nordic-folklore-and-history-behind-bramble-the-mountain-king/
- Kittelsen: https://apollo-magazine.com/?p=897801 · https://davidsartoftheday.blogspot.com/2015/01/theodor-kittelsen-echo.html
- Nøkk and steel: https://en.wikipedia.org/wiki/Nixie_(folklore) · https://godsandmonsters.info/nix/ (folk summaries; the exact Norwegian rhyme should be checked against Norsk Folkeminnesamling before it is quoted in game)
- Deep Rock Galactic: https://www.co-optimus.com/editorial/1978/page/1/indie-ana-co-op-and-the-deep-rock-galactic-interview.html · https://gamingbolt.com/deep-rock-galactic-interview-heigh-ho-off-to-plunder-we-go · https://games.gg/deep-rock-galactic/
- Hades God Mode: https://inverse.com/gaming/hades-god-mode-interview · https://lordsofgaming.net/2020/11/hades-godmode-is-an-accessibility-feature
- Game Accessibility Guidelines, game speed: https://gameaccessibilityguidelines.com/?p=3195 · Celeste assist: https://gamedeveloper.com/design/check-out-i-celeste-s-i-remarkably-granular-assist-options · https://www.vice.com/en/article/celeste-assist-mode-change-and-accessibility
- In the project: `MonsterAI.Fighting.cs` (OffScreen, WaitsToShoot), `PlayerCameraSettings.asset`, `RaidReportText.cs`, `SceneAudio` in the three raid scenes, `PerceptionSettings.asset` (noiseInRain 0.7), `CompanionBond.cs`, `RisesAgain.cs`, `GameOptions.cs`, playtest `20261115-184337.csv`.

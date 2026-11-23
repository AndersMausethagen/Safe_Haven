# Campaign and Sandbox: is the story being crowded out? (2026-11-23)

The user's worry: many sandbox mechanics have been added (colony management, RimWorld-style people) to a game that so far has been
curated and story-driven. Are they polluting that experience? Should the current game become a **Campaign** (story, structure,
teaching the mechanics and the lore) beside a separate **Sandbox** mode (open-ended, random people, its own progression, in the
spirit of RimWorld and Oxygen Not Included)?

Short answer: **the worry is partly right.** The kind of systems added is mostly compatible with a story game, but the amount, the
upkeep and how early they arrive are not checked against the story yet. **Two modes is a proven structure**: Gord, Frostpunk, Anno,
Bannerlord and They Are Billions all have it. It works here if the Sandbox gets its own way of unlocking things and its own people, and
if the Campaign is curated in its pacing, not only restricted. Recommended order: tighten the Campaign first (a "campaign budget"), let
the Haven continue after the story ends, and build Sandbox as a later mode on the same systems.

---

## 1. The worry, checked against the game

### 1.1 What the design promises

From `Docs/GameDesignDocument.md`:

- **The raid is the central system** (§7). The settlement is the stable centre; progression bridges the two (§5.1).
- **Pillar D: build a community one person at a time.** NPCs have names, small personal stories, wishes, and are remembered if lost.
- **Pillar E: simple beginning, layered depth.** Depth should emerge from mechanics that interact, "rather than presenting dozens of
  menus on day one".
- **Deliberate exclusion (§2.6):** "no needlessly elaborate life simulator with dozens of constantly draining meters".
- **Pacing (§3.3):** a raid of 10–20 minutes plus preparation; a 45–90 minute session holds several raids and some building.

### 1.2 What has been added

**Curated layer (authored, voiced, story-paced):**
- the prologue and its seven quests;
- Act I to the Heartroot, and Act II;
- cinematics and spoken dialogue;
- eleven written residents, each with a quest line, rescued one at a time;
- Haven quests held for homecomings;
- one building per quest (`BuildingGate`).

**Systemic layer (built 2026-11-10 to 11-23, mostly unplayed by hand):**
- **The Keeper's view colony mode:** hauling, carry weights, plans, felling rings, fields and watering, wishes, guard posts, quarry,
  land parties, the horn.
- **The economy:** meals, flour, field limits, outposts, forays.
- **Haven town:** homes, yards, extensions and paths.
- **Horses:** taming, bond and breeding.
- **Territory and defence:** territory pushes; breaches and corruption nights.
- **The people layer:**
  - talents, passions and traits;
  - spirits and friendships/rivalries;
  - marks, kit and habits;
  - gifts, bond, favourite dishes and the Stablehand.

### 1.3 The evidence so far

- **Home time is above 60%** in the user's own runs (the deep-dive list, item 2). That works against "the raid is the central system".
  It is the clearest sign of drift, and it was measured before most of the people layer existed.
- **The people layer rides on the written cast.** Traits, talents, passions, friends and frictions are written per person, not rolled.
  So far that is closer to Fire Emblem's supports or Wildermyth's handcrafted campaigns than to RimWorld. It deepens Pillar D rather
  than replacing it.
- **The strongest sandbox markers are not in the Campaign:**
  - random people (the rolled strangers were deliberately left for later);
  - a storyteller that throws random disasters;
  - permanent loss of the people (the lost can be found again);
  - an endless game with no ending.
- **Upkeep is creeping in:**
  - meals each dawn;
  - watering fields;
  - torches burning charcoal;
  - tool and kit wear;
  - hauling with small carry limits;
  - a gift a day;
  - spirits that sink when needs go unmet.

  None of these is a "constantly draining meter" on its own. Together they are what §2.6 warned against, and each costs home time that
  isn't story or raids.

### 1.4 Four ways sandbox mechanics pollute a curated game

1. **Load.** Too many systems met too early (Pillar E). The danger is the first two hours, not the late game.
2. **Upkeep.** Chores that must be kept up between story beats. This is the classic "sandbox tax": the story waits while the player
   feeds, waters, hauls and repairs.
3. **Simulation that contradicts the writing.** A cheerful character written warm goes Low for a week; two people written as old
   friends drift into Rivals; a mark changes how someone fights in a way their story never mentions. Authored characters in a
   simulation need guard rails.
4. **Goal dilution.** Open-ended loops (territory pushes, outposts, breeding, a town to perfect) can become the main activity, and the
   raid and the story become a side trip.

### 1.5 Per system: where it stands for the Campaign

| System | Fits the Campaign as built | Fits if gated or optional | Leans to Sandbox |
|---|---|---|---|
| Talents, passions, traits on the written eleven | ✓ (makes each one distinct; written, not rolled) | | |
| Marks from raids, breaches and nights | ✓ (the story of their raids) | | |
| Habits, small scenes, lines in passing, favourite dishes | ✓ (authored flavour, no upkeep) | | |
| Bond with the player, gifts | ✓ | gifts: keep them a choice, never a daily duty | |
| Spirits | | ✓ (keep the −10% cost small; never more) | |
| Friendships and rivalries that drift | | ✓ (keep drift inside a band around the writing) | Free drift |
| Any job for anyone, Who does what | | ✓ (trades still come with rescues, so the story gates them) | |
| Homes, yards, comforts (Haven town) | ✓ (cosy, optional) | | |
| Hauling, carry limits, felling rings, fields with watering | | ✓ (automated by default; the player may manage) | Full logistics |
| Meals, flour, torches burning charcoal, wear on kit and tools | | ✓ (generous defaults: upkeep must not gate the story) | Tight upkeep |
| Keeper's view | ✓ (unlocked by a quest) | | |
| Outposts, forays, land parties, territory pushes | | ✓ (introduced by a person or a quest, mid-game) | As a main loop |
| Horses: taming, breeding | ✓ (taming); breeding as optional depth | | |
| Breaches, corruption nights | ✓ (GDD pillar A, telegraphed) | | Random raids on the Haven |
| Rolled strangers (not built) | | | ✓ |
| A storyteller for random events (not built) | | | ✓ |

**Verdict:** not polluted in kind; at risk in amount, in upkeep and in timing. Nothing built has to be taken out. Some of it should be
automated, gated later or kept optional in the Campaign. That needs the first-two-hours playtest data (deep-dive item 2) to decide
well.

---

## 2. How other games handle story and sandbox

| Game | Structure | What happened |
|---|---|---|
| **Gord** (the colony model for the Haven) | Story **Campaign** (it doubles as the tutorial, bringing in mechanics and buildings step by step) and **Custom Scenarios** (sliders for starting resources, which Horror you meet, win conditions; also a wave-defence scenario) | Reviewers call the campaign the guided route and say colony-sim fans get more from Custom Scenarios. Some found the campaign repetitive. |
| **Mount & Blade II: Bannerlord** | **Story** mode and **Sandbox** mode on the same map and systems; sandbox has no main quest and more character creation | Split opinions. A common complaint: story mode is "just Sandbox but rife with roadblocks". |
| **Frostpunk** | Story **scenarios**; **Endless** mode added after launch | Endless was one of the community's top requests; players wanted to keep playing past the fixed number of days. Frostpunk 2 shipped both from the start (Story and Utopia Builder). Critics call the story the core experience and the sandbox the long-term option, with repetition and weak stakes as its weakness. The first DLC gave the sandbox more structure. |
| **They Are Billions** | **Survival** mode first; a 48-mission campaign added at 1.0 | Survival players gave the campaign very negative reviews; the developers changed its difficulty and went back to survival. The consensus: survival was the heart, the campaign a weaker add-on. |
| **Anno 1800** | **Campaign** and **Sandbox** | The campaign is "a prolonged tutorial"; beginners are steered to it. Players say sandbox "is the real Anno experience". |
| **Factorio** | Had a campaign and a new-player scenario | Before 1.0 the team cancelled the expanding campaign as too risky, and replaced the new scenario with the old tutorial, to focus on the core sandbox. |
| **Dragon Quest Builders 2** | Story islands, plus the **Isle of Awakening** free-build island | The free-build island is part of the story, not a separate mode as in the first game, and reviewers said that made it feel more worthwhile. Rescued NPCs come back to help build. Free build is gated early and fully opens at the end. |
| **Valheim** | One mode | Bosses are fixed points; the path between them is free. Reviewers call it linear progression inside open survival, with "brief, sparing messages" instead of a quest log. Survival systems are softened so the bosses carry the challenge. |
| **Subnautica** | One mode, story told through the world | The story is optional and environmental. The more scripted sequel, Below Zero, was criticised for keeping too much control of a self-guided adventure. |
| **Wildermyth** | **Handcrafted campaigns** and **procedural campaigns** on the same systems | Layers of handcrafted and procedural content: each chapter opens and closes with written scenes, and the middle is procedural. Criticism: procedural events repeat across campaigns. |
| **Darkest Dungeon** | Authored narrator over procedural, disposable heroes with quirks | Works because heroes are expendable and replaceable. The sequel moved to one hero per class, each with their own written path. |
| **Fire Emblem: Three Houses** | Story missions with free days at the monastery between them, on a calendar | The management and social layer lives between fixed story beats; the calendar paces it. |
| **RimWorld** | A "story generator": no authored arc; a storyteller paces random events | Its creator designed it as a story generator rather than a conventional game. Authorial control comes through the storyteller's rules, not through scripts. |
| **Against the Storm** | An authored frame (the Queen, the Blightstorm) over roguelite settlement runs | The story is a frame; every run is a sandbox with goals. |

### 2.1 Patterns

- **A. Campaign as the on-ramp, sandbox as the long tail** (Gord, Anno, Frostpunk, They Are Billions, Bannerlord). This works when
  both share the systems and the sandbox has its own setup (sliders, goals). Most long-term hours end up in the sandbox. The campaign
  is valued for teaching and for its story.
- **B. One mode: a story spine with free time between fixed points** (Valheim, Subnautica, Dragon Quest Builders 2, Three Houses).
  The story introduces systems and paces them. Free play opens up as the story goes on, fully at the end.
- **C. An authored frame around a procedural middle** (Wildermyth, Against the Storm, Darkest Dungeon).

### 2.2 How it goes wrong

- **The campaign is the sandbox with roadblocks** (Bannerlord): curation that only takes things away feels like a restricted
  sandbox. A campaign needs its own pacing, people and moments.
- **A campaign bolted onto a sandbox's audience** (They Are Billions): the players who came for the sandbox resent it.
- **A sandbox with no stakes repeats itself** (Frostpunk 2 Utopia Builder, Wildermyth's generic campaigns): it needs a director,
  goals or variety, and often gets structure added back later.
- **A campaign that is only a tutorial is seen as "10% of the game"** (Anno). That's fine if teaching is the job.
- **Splitting effort before the core is done** (Factorio cut its campaign to focus on the core game).

### 2.3 What this means for Safe Haven

Safe Haven is pattern B today: one mode, a story spine (rescues, acts, homecomings) with the Haven's free time between. Its
strongest asset is the **written, voiced cast of eleven**. That is also the hardest thing for a sandbox to reuse: a Sandbox needs
people of its own.

---

## 3. Would two modes work here?

Yes. The systems are data-driven, and almost all of them would carry over unchanged: raids, combat, building, the people layer, the
Keeper's view, horses, defence, co-op. What a **Sandbox** needs that the Campaign doesn't have:

1. **A way to unlock things without the story.** Today buildings are learned from quests (`BuildingGate`), trades arrive with
   rescues, and the Keeper's view and the portal's tiers open from quests. A Sandbox needs another spine: renown and rank, research
   at the portal, or strangers who bring trades. Ideally this is a rules asset per mode, so the Campaign's tables stay untouched.
2. **People without writing.** Rolled strangers (the review's step 9, kept for later): looks from the modular humans, a name, rolled
   talents, passions and traits, a small set of shared voices for barks, templated lines. The eleven could still turn up as rare
   wanderers, or not at all.
3. **A director for drama.** A storyteller for breaches, corruption nights, wanderers, wishes and raid rumours, like RimWorld's
   storyteller or Frostpunk's Endless storms. Without it the Sandbox repeats itself.
4. **Setup and goals.** Scenario settings in the manner of Gord: starting people and resources, breach frequency, the death rules
   ("Harsh" exists), raid tiers, optional win conditions or milestones.
5. **Separation.**
   - A mode flag on the save.
   - Story content off: the prologue, cinematics, quest lines.
   - Tests for both modes.
   - Co-op rules for both.

Rough size: medium to large. The new work is concentrated in items 1-3. The Sandbox also frees the Campaign: the Campaign can keep
upkeep light and automated, because the "full logistics" experience has a home of its own.

---

## 4. Recommendation

1. **Keep the Campaign the main game, and finish it first.** Frostpunk and They Are Billions added their sandbox and survival modes
   after the core was proven, and Factorio cut a mode to protect its core. Design the Sandbox now (on paper, and a mode flag in the
   data), and build it once Act I has been played through by friends.
2. **Write a campaign budget** and audit every system against it:
   - **Every system enters through a story beat or a person.** A rescued person brings a trade; a quest teaches a building. Nothing
     appears in the first two hours unless the prologue or Act I uses it.
   - **Upkeep never blocks the story.** Defaults are generous, and residents fend for themselves unless the player chooses to manage.
     The Keeper's view is depth, not duty.
   - **The writing wins.** Drift in spirits and standings stays inside a band around who each person is written to be. Scenes and lines
     explain any change that does happen.
   - **The raid stays the centre.** Measure home time against raid time in the playtest logs. Aim at or under half the session at home
     in the first two hours, as §3.3 implies.
3. **Let the Haven continue after the story** (the Dragon Quest Builders 2 pattern). When the Campaign ends, the Haven goes on as an
   open game with the director switched on. This is the cheapest bridge, and it rewards story players with the sandbox.
4. **Then a Sandbox mode on the main menu**, with rolled strangers, the director, scenario settings and its own unlock spine.
5. **Measure before cutting.** The playtest rows exist (PersonLevel, Spirits, Bond, Standing, home/raid time). Decide what to gate,
   automate or move from the first-two-hours data rather than by feel.

## 5. Questions for the user

1. Shall I audit the Campaign against the budget now (each system: when the player meets it, its upkeep, and keep / gate later /
   automate / Sandbox-only)?
2. Does the Haven continue after the story ends (post-game)?
3. When should the Sandbox be built: designed now and built after Act I has been playtested (recommended), or started now?
4. In the Sandbox, do the eleven written people exist (as rare wanderers), or only rolled strangers?

## Sources

- Gord: [GameSkinny review](https://gameskinny.com/reviews/gord-review-villagers-and-villains), [Team17 FAQ](https://support.team17.com/en/articles/317012-gord-faqs), [GameGrin review](https://gamegrin.com/reviews/gord-review)
- Bannerlord: [GamerEmpire, campaign vs sandbox](https://gamerempire.net/mount-blade-2-bannerlord-difference-between-campaign-and-sandbox-mode/), [Steam discussion](https://steamcommunity.com/app/261550/discussions/0/3776868105339837477)
- Frostpunk: [PC Gamer on Endless mode](https://www.pcgamer.com/frostpunks-free-endless-mode-is-due-next-week-and-its-bigger-than-we-thought), [Gamereactor](https://www.gamereactor.eu/an-endless-mode-arrives-in-frostpunk/), [Frostpunk 2 Utopia Builder beta](https://news.xbox.com/en-us/?p=201122), [GameWatcher review](https://www.gamewatcher.com/frostpunk-2/review), [Windows Central review](https://windowscentral.com/gaming/frostpunk-2-review)
- They Are Billions: [PC Gamer, campaign patch](https://pcgamer.com/they-are-billions-campaign-patch-alters-difficulty-time-limits), [Vice](https://vice.com/en_us/article/qv7my5/they-are-billions-gets-the-single-player-campaign-nobody-needed), [Game Informer](https://www.gameinformer.com/review/they-are-billions/turning-back-the-fetid-tide)
- Anno 1800: [PC Gamer on game modes](https://pcgamer.com/anno-1800s-game-modes-offer-unbridled-customisation), [Steam discussion](https://steamcommunity.com/app/916440/discussions/0/508449585248628161)
- Factorio: [FFF #329 Campaign reassessment](https://forums.factorio.com/viewtopic.php?p=473216), [FFF #342 The new old tutorial](https://direct.factorio.com/blog/post/fff-342)
- Dragon Quest Builders 2: [PC Gamer review](https://pcgamer.com/dragon-quest-builders-2-review), [RPGFan review](https://www.rpgfan.com/review/dragon-quest-builders-2/), [Dragon Quest wiki](https://dragon-quest.org/wiki/Dragon_Quest_Builders_2)
- Valheim: [Shacknews 1.0 review](https://www.shacknews.com/article/150866/valheim-10-review-worth-the-wait), [Twinfinite](https://twinfinite.net/pc/4-features-that-set-valheim-apart-from-other-survival-games/3/), [Design deep dive](https://bottomfeeder.substack.com/p/design-deep-dive-valheim)
- Subnautica: [PC Gamer on its storytelling](https://pcgamer.com/subnauticas-hands-off-approach-to-storytelling-is-brilliant), [GamesRadar on Below Zero](https://www.gamesradar.com/subnautica-below-zero-shows-why-not-all-survival-games-should-front-load-their-storytelling)
- Wildermyth: [Wikipedia](https://en.wikipedia.org/wiki/Wildermyth), [RPG Site review](https://rpgsite.net/review/12107-wildermyth-review), [Design journal: Villains](https://wildermyth.itch.io/wildermyth/devlog/81164/design-journal-villains)
- Darkest Dungeon: [Dread Central](https://www.dreadcentral.com/editorials/483362/darkest-dungeon-builds-exciting-characters-from-game-mechanics/), [Game Developer](https://gamedeveloper.com/design/opinion-finding-meaning-in-i-darkest-dungeon-i-s-narrative-design)
- Fire Emblem: Three Houses: [RPG Site monastery guide](https://rpgsite.net/feature/8758-fire-emblem-three-houses-guide-tips-and-in-depth-strategy-for-your-time-at-the-monastery), [Fire Emblem wiki](https://fireemblem.fandom.com/wiki/Fire_Emblem:_Three_Houses)
- RimWorld: [Wikipedia](https://en.wikipedia.org/wiki/RimWorld), [Game Developer](https://gamedeveloper.com/design/rimworld-dwarf-fortress-and-procedurally-generated-story-telling), [DiGRA paper](https://dl.digra.org/index.php/dl/article/download/2781/2765/2825)
- Against the Storm: [Steam page](https://store.steampowered.com/app/1336490/_Against_the_Storm/)

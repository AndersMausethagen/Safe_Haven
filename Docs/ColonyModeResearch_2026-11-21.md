# Colony mode research: running the Haven from above

Research date: 2026-11-21, revised the same day after a second round. Read-only: no game code, data, scenes or
prefabs were changed, and no Unity CLI or Play mode was used.

- **Round one:** three agents did the groundwork (other games on the web; the camera, input and player side of the
  code; the people, work and orders side).
- **Round two:** a second web round (small named casts with a town mode, extraction hideouts, homecomings, light as
  defence) and an independent design critic who read this study against the code and the design document.

I checked the claims the recommendation rests on in the code myself. `path:line` points at `test` as of commit
55177dd9. Paths are under `Assets/_Project/Scripts/` unless they say otherwise.

The user asked first:

> "I feel like [the Haven is] being pulled in two different directions that doesn't quite match. Pressing V or any other
> free key, in Haven, will turn the camera into colony sim mode, allowing the camera to be moved freely along the terrain,
> like Gord does. The player character then moves along in the base like other NPCs do. This will change the UI to focus
> more on the haven and its inhabitants. I can place down buildings that the player or NPCs can start building, interact
> with existing buildings, queuing up items to process and create or start upgrades etc. I could also control NPCs from
> this POV, giving them orders, such as patrol, defend, attack, farm timber, stone or other nodes. At any time I can go
> back to player mode. Only in Haven, not raids. Research if other games do something similar and how they made it work.
> Will this be a good addon? Will it make it better? What works against it? Suggest how we can make this work and how we
> can improve the idea and make it a complete and deeper feature."

And then:

> "Have another round on the result. Look for how we could do this even better, other features to add or deepen. Or
> other mechanics we should adjust further to make this good."

---

## 0. Summary

### The short answer

**Yes, it fits Safe Haven, but only if two things hold.** The people you see do the work you order, and the view is
built around what is actually scarce at home. A free camera on its own would only be a prettier H window.

**Why the Haven feels pulled two ways.** The code shows it: residents have names, homes and routines, but nothing in the
economy depends on where they are or what they do.

| What happens in the Haven | Done by a body in the world? | Where |
|---|---|---|
| Hammering a scaffold while you are home | Only the player | `Building/Construction.cs:388-393` |
| Builders finishing scaffolds | Bookkeeping, and **only while the player is away** | `Settlement/SettlementSystem.cs:60`, `Settlement/ResidentBuilders.cs:125-204` |
| Workers making goods | Bookkeeping: costs out of the stash, goods back in | `Settlement/WorkerProduction.cs:135-160` |
| Gathering wood, stone, forage | Only the player | `ResourceInventory/ResourceNode.cs:287` |
| Defending in a breach | The player, plus Warden and Hunter as companion stand-ins | `Settlement/ResidentLife.cs:227`, `ResidentDefender.cs:35` |
| Residents' days | Real: they walk, work at their yard, eat, sleep in their homes | `Settlement/ResidentRoutine.cs:74` |

**What is scarce at home: round two's main finding.** It changed the design.

- **Not scarce:**
  - Building work is 2-12 s a building (`buildWork` in `Data/Buildings/*.asset`; the Stable is the longest at 12 s).
  - Crafts finish during raids, and staffed stations already carry their goods into the stash.
  - H already moves you to any building or person.

  Orders such as "build this" or "queue that" therefore save seconds.
- **Scarce:**
  - **your minutes between raids**;
  - **wood and stone**: about 13 oak a minute by hand, 888 oak to build everything once, about 770 more for charcoal
    (`Docs/BaseProgression.md:155-180`), and torches now burn charcoal every night (`Environment/TorchFuel.cs`);
  - **being ready for the next raid.**

  So the view earns its place by planning the raid loop, by gathering, and by work that runs **while you are away**.

### What other games show

No shipped game does exactly this. Each part exists somewhere:

- **Fallout 4's workshop** is the nearest switch (a key, settlement only, placing and assigning). Its lesson is that
  "people are what matter".
- **RimWorld drafting** shows how an order overrides the routine and lapses back to it.
- **Kenshi** makes your character one of a small squad under a free camera.
- **Necesse** is a top-down action game with a co-op settlement at Safe Haven's scale.

Round two added the games with a **small named cast and a town mode**:

- Dark Cloud: residents' wishes about where things go.
- Ni no Kuni II and Enshrouded: each recruit brings their own stations and abilities.
- Monster Hunter and ARC Raiders' Scrappy: work counted in outings, paid on return.
- ARC Raiders: track a material and it is tagged in raid loot.
- Darkest Dungeon: one homecoming event.
- Hades: homecoming lines that notice what happened.
- Don't Starve, Gord, Frostpunk: light as territory.
- Thronefall: lanes shown before the night.

### What works against it

1. **Two half-games** (Gord, Brütal Legend, Minecraft Legends). Answer: the view is the same game from further up, not
   an RTS.
2. **The hero becomes optional** (Soulmask). Answer: residents get plain basics at a third of your pace; quality, XP,
   fighting and talking stay yours.
3. **From above, every stuck NPC shows.** Answer: work done while you are away is applied as results, not walked; NPC
   movement is cleaned up first.
4. **Rendering at height.** Sun shadows end at 50 m, grass at 80 m, and the fog of war is flat at the player's height.
   Answer: measure first and cap the height.
5. **Co-op.** Residents are simulated in each game separately, so live work diverges. Answer: away work is state; live
   ring workers get one runner.
6. **Orders with nothing at stake.** Prowlers never enter light, residents can't be hurt, and the night is about 8 minutes of a 24-minute day. Answer: defend
   foretold corruption nights, which raise or lower blight, rather than patrolling.
7. **Headcount comes late.** There are 2-3 residents after about two hours, and 9 of 11 fight the same way. Answer:
   orders unlock person by person, and companion signatures matter.

### The design in brief: the Keeper's view

The working name comes from the Heartroot's last word, "Hearth-keeper".

1. **Getting in:** H stays the Haven window for a quick glance. The view has its own key, a button in the window's
   header and a pad wheel entry. It opens when the Heartroot names you Hearth-keeper (The Road North).
2. **Between raids:**
   - A homecoming sweep shows what changed, with marks on the buildings.
   - The portal card becomes the raid planner: pick the companion by clicking them, pack, set out.
   - Plans: place a building as a ghost that costs nothing yet, and its missing materials are tagged in raid loot.
3. **Standing orders that run while you are away**, applied on return and counted in raids, not hours: clear this
   ring, quarry this knoll, a hunting or fishing party to a held land. The worst result is "came back tired". This is
   the biggest win.
4. **Living work at home:**
   - builders hammer while you are home, mainly along drawn wall rings;
   - felling rings are finite, painted trees included, and a forester replants behind them so the woods stay green;
   - loads are carried home, and a working horse drags logs.
5. **Defence with stakes:** on a foretold corruption night, posts on the foretold side and the Watch Post archer, with
   the lanes shown in the view. A breach's preparation happens in the view and the fight on foot. Posts are capped by
   the defences you have built.
6. **People who matter:**
   - each rescued person brings one order;
   - wishes about where things go, met from above;
   - lines that notice your raid;
   - worker ranks shown as badges;
   - an empty house for someone lost.
7. **Your character:** it waits at the hearth, or keeps hammering if you left it mid-build. No autopilot job: it would
   save seconds for a large cost.

### Phases

| Phase | What | Effort |
|---|---|---|
| 0 | Groundwork: the V clash, residents out of deep water, fewer stuck warps, measure rendering at 35/45/55 m | S-M |
| 1 | The view itself, builders at home, wall rings, gear collected from afar | M-L |
| 2 | Between raids: homecoming sweep, raid planner, plans and the tracked shopping list | M |
| 3 | Standing orders while away, felling rings with the forester, carrying, horses hauling | M-L |
| 4 | People: orders unlocked one person at a time, wishes, lines, rank badges (companion signatures as decided on 2026-11-13) | M |
| 5 | Corruption-night posts, the horn, breach preparation in the view | M |
| Later | Visible stores, paths from worn ground, light overlay and lamplighter, event pins | S-M each |

The eleven questions for you are in Part 7. My recommendation is to build Phases 0 and 1 now, then the rest once you
have used the view, because Phase 3 onwards changes the economy.

### What the second round changed

| Round one said | Round two says | Why |
|---|---|---|
| H at home becomes the view | **H stays the window**; the view has its own key and a header button | A glance at the stores shouldn't need a camera flight, and H's meaning shouldn't change |
| The view saves walks to stations | It saves little walking: "Go there" and staffed stations already cover that. It earns its place by **reading the town, planning raids and gathering** | `HavenTravel.cs:15-40`, `WorkerProduction.cs:154-160` |
| Build orders and a "next" pin | **Builders at home** go into Phase 1 as a small change; **wall rings** are their real work; no "next" pin | Buildings take 2-12 s; a 30-piece wall at 3-5 s a piece adds up |
| Gather rings from scene nodes, painted trees later | **Painted trees first**, rings are finite, a **forester replants**, overgrowth is left out | The Haven has about 9 oak and 8 stone nodes as scene objects; felled Haven trees never regrow (`TerrainTreeFelling.cs:11-12`); overgrowth is the player's early renown |
| Patrol with a lantern, Follow me, Attack | **Cut.** Corruption-night posts on the foretold side instead | Night at home has nothing at stake; a corruption night moves blight |
| Any resident can stand in a breach | **Posts are capped by the defences built** (Watch Post tier, gates) | Eleven guards who can't die would set the breach balance by headcount |
| Phase 4: your character works on autopilot | **Cut.** It waits at the hearth or keeps hammering | L effort to save seconds |
| Thrown out when "anyone on your side" is hunted | Thrown out only when **you** are hunted | `MonsterAI.AnyHuntingPlayerSide` (`AI/MonsterAI.cs:200`) counts friends too |
| Co-op divergence from Phase 3 | **From the first live ring worker.** Away work is state and safe; live ring workers need one runner | Each game paths its own residents |
| No link to raids beyond preparation | **The view is the place between raids**: homecoming, planner, plans, standing orders | Your rule: judge base features by what they do for raids |

---

## Part 1: What the code has today

### 1.1 What is physical and what is bookkeeping

- **People and movement:**
  - 11 residents with 13 jobs (`Data/Prologue/Residents`, `Data/Settlement`). Everyone has a raid companion (Fighter,
    or Archer for Runa and Asa). The §15 row of ARCHITECTURE's status table ("one recruit, one job") is out of date.
  - They walk with a `NavMeshAgent` added at runtime (`ResidentLife.cs:117`): 1.6 m/s walking, 3.8 running, low-quality
    avoidance, a warp after 4 s stuck (`:544`). No area mask is set, so they may path through deep water (inferred, not
    tested).
- **Their day** (`ResidentRoutine.Decide`, `:74`): wake 6.5, work 8-18, meal 12-13, sleep 22, an outing about one day in
  three. Hard cold sends people to the fire in the afternoon (`:63-71`).
  - A Haven day is **24 real minutes** (`Data/Environment/Safe Haven Day Night.asset`), so work hours are about 10
    minutes and the night about 8.
- **Animations:** walking plus emotes (Forging, Use, Fishing, Read, Sit, Sleep, Kneel, Talk) with a tool in the hand.
  There are no resident chop, mine, carry or fight clips. The player's MineWall, MineGround, Hammer and Farm clips
  (`Interaction/InteractionAnimation.cs:16-22`) can become resident emotes, which are clip-based.
- **Residents cannot be hurt:** the resident prefab has no `Damageable` or `Combatant`. In a breach, the Warden and the
  Hunter are swapped for a `Companion` stand-in (`ResidentDefender.Stand`, `:35`); everyone else shelters.
- **No needs or mood.** Home quality and likings only add work speed. Worker ranks still count crafts
  (`Settlement/SettlementSettings.cs:69-72`, `SettlementState.cs:803`); the milestone ranks decided on 2026-11-13 aren't
  built.
- **One stash** (`StashStorage`), read whole by every system. Nobody fetches anything physically.
- **Builders bank work only while you are away** (`SettlementSystem.cs:60`) at 0.5 work-seconds per builder-second
  (`Resources/HomeLifeSettings.asset`), spent on return, oldest scaffold first.

### 1.2 Where the time goes at home

- **Building:**
  - 2-12 s a building (`buildWork`), 7-14 s an upgrade;
  - the player hammers at 1 + Build Speed (`Construction.cs:419-425`).
- **Crafting:** it runs during raids, and speed bonuses barely matter (`Docs/BaseProgression.md:19-21`).
- **Gathering is the real cost:**
  - about 13 oak a minute;
  - 888 oak to build everything once, plus about 770 for charcoal (`BaseProgression.md:155-180`);
  - torches burn Charcoal every night (`Environment/TorchFuel.cs:4-10`).
- **The Haven's wood is painted terrain trees,** about 130,000 of them.
  - Scene-object nodes are few: roughly 9 OakWood, 8 FallenLog, 8 GreyStone and 14 bramble nodes, counted from prefab
    instances in `Scenes/SafeHaven.unity`, so approximate. Much of that is Overgrowth, which gives renown when cleared
    (`Settlement/Overgrowth.cs:11`).
  - **Felled Haven trees stay down:** `TerrainTreeMemory › Keep Felled`, and only saplings planted through
    `PlantedTrees` grow back (`Gathering/TerrainTreeFelling.cs:8-13`, `:64`).
  - Painted trees only get choppable stand-ins within 10 m of the player (`:21`, `:267-304`).

### 1.3 The camera, and everything that follows the player

- **Today's camera:** `Camera/PlayerCameraController.cs` is one Cinemachine camera following a private "Camera Focus"
  (`:287-295`), moved each frame from the player plus cursor lean, shoulder and lead (`:618-661`).
  - The Exploration profile is pitch 50° at 14-28 m, and Combat is 58° at 12-20 m.
  - `HavenLookout` pulls the widest view today, about 56 m (`Settlement/HavenLookout.cs:149-153`).
  - There are no bounds and no terrain logic.
- **The swap precedent is `Cinematics/CinematicView.cs`:** its own camera at priority 1000, the screen canvases hidden,
  the player held, the fog of war faded (`AtmosphereFeature.WarScale`), and a glide back on `End()`. Another precedent
  is `PlayerCameraController.Watch(Transform)` (`:102`).
- **The catch:** the controller writes global state every frame:
  - detail focus (`:381`);
  - near shadows (`:443`);
  - screen-centre aim and cursor lock (`:440`, `:464-482`);
  - the V style toggle (`:433`).

  A second camera needs a "stand down" flag. Disabling the controller would null `PlayerCameraController.Current`,
  which about 15 files call.

| System | Where | With a far camera | Fix |
|---|---|---|---|
| Sun shadows | `Settings/Rendering/PC_RPAsset.asset:60` (50 m) | None beyond 50 m from the camera | M, plus a GPU cost to measure |
| Grass and trees | SafeHaven terrain: detail 80 m, billboards from 50 m | Ground bald from about 80 m | M, plus a cost to measure |
| Fog of war / night veil | `Environment/Atmosphere/AtmosphereFeature.cs:601` | A flat plane at the player's height: black bands over hills | S fade; M on `FogGround` |
| Fog thinning | `AtmosphereFeature.cs:438-442` | Along camera→player | S |
| See-through | `Environment/SeeThrough.cs:52-55` | Holes along camera→player | S (`Active`) |
| Ground relief | `Environment/TerrainDetailFocus.cs` | 15-25 m round the player | S |
| Sky occlusion | `Rendering/SkyOcclusionMap.cs:204` | No roof or canopy shading beyond 80 m from the player | M (about 6 ms per 12 m panned) |
| Reflection probe | `Rendering/FollowReflectionProbe.cs:88` | Above the player | S |
| Hearing | `Audio/AudioManager.cs:493`, `PlacedAmbience.cs`, `AudioSpace.cs:72` | What the body hears | S (`EarPosition`) |
| Minimap | `UI/MinimapHud.cs:290,350` | Centred on the player | S / M |
| Hover outline | `Player/PlayerInteractionOutline.cs:15,55` | 80 m ray; hidden while a window is open | S |
| Painted-tree stand-ins | `Gathering/TerrainTreeFelling.cs:21,274` | Only within 10 m of the player | M (`StandingNear(point, d)` exists) |
| Player-relative spawns | `Level/MobSpawnZone.cs:325`, `NightProwlers.cs:117`, `HorseHerd.cs:257` | May pop in in front of a far camera | S-M |
| Shake, off-screen warnings | `Camera/CameraShake.cs`, `Player/OffScreenWarnings.cs` | Follow the body | S |
| Rain, air life, ripples | `RainEffect.cs:106`, `AmbientLife.cs:139`, `WaterInteractor.cs:181` | Follow the camera; tuned for a 20-40 m view | S tuning |

### 1.4 Keys and the pad

- **V is bound twice on the keyboard:** `Camera/ToggleStyle` and `Player/StackOnStored` are both `<Keyboard>/v`
  (checked in `Input/InputSystem_Actions.inputactions`). This looks like an existing bug, separate from this feature;
  I did not test it in Play.
- **Tab is taken** (`ToggleCombat`). **H opens the Haven window** (`Player/PlayerInputHandler.cs:286,396`; tabs Overview,
  Stores, Buildings, People, Lands, Raids, `UI/HavenWindow.cs:46`).
- **Free keyboard keys:** Q, U, O, P, backquote, Caps Lock, F2-F4, F7-F12, Insert/Home/End/PgUp/PgDn/Delete.
- **Pad:** every button is used. The way in is the windows wheel (`UI/CommandWheel.cs:188`).
- **A new action map fits:** `GameInput.SuspendGameplay` already turns off every map but UI, and the rebinding screen has
  a conflict-domain rule to copy (`Core/Options/InputRebinds.cs:191,210`).
  - **Trap:** window keys (G, H, B, J, M) live in the Player map, which a hold turns off. `PlayerControl.Kept` keeps only
    Move, Interact and Dodge.

### 1.5 Acting from afar

| Action | From afar today? | Notes |
|---|---|---|
| Order an upgrade | Yes, H window | `HavenBuildings.cs:134` → `Construction.TryOrderUpgrade` (`:278`) |
| Place a building | Yes | `BuildPlacer` rays from `Camera.main` to 600 m (`HomePortal.cs:159-170`); the pad aims from the player's position, clamped to `BuildReach` (`BuildPlacer.cs:611-635`) |
| Give a job, pause, a person's page | Yes | `HavenPeople.cs:81/93/146`, `ResidentWindow.Show` |
| Go to a building or person | Yes, a teleport | `HavenTravel.cs:15-46` (not during a breach) |
| Queue a craft | The code can; the UI can't | `StationCrafting.TryQueue` (`Crafting/StationCrafting.cs:75`) has no distance check; `StationWindow` closes past 6 m (`UI/StationWindow.cs:397`) |
| Collect output | Staffed stations carry everything but gear to the stash already | `WorkerProduction.cs:154-160`; gear is collected into the bag, quality rolled then (`StationCrafting.cs:177-192`) |
| Talk, hand in, open a chest, ride | No | Distance checks in `DialogueBox`, `StashChest`, `PlayerInteractor` (`:143`) |

### 1.6 People, work and orders: the gap table

| Capability | Status | Reusable pieces | Missing | Effort |
|---|---|---|---|---|
| Fell or quarry a ring | Missing | `ResourceNode.GatherBy` (`:287`), `ChoppableTree.Roll` (`:229`), `TerrainTreeFelling.RestoreFelled` (`:111`), `PlantedTrees` | Fell a painted tree by index without the player near, yield into the stash, ring state, gather emotes | M-L |
| Work while you are away | Pattern exists for builders | `SettlementSystem.cs:56-69`, `ResidentBuilders.cs:125-204` | Standing orders and results applied on return | M |
| Defend a post | Breach only | `KeepsGuard`/`GuardPost` (`ResidentLife.cs:227/252`), `Companion.Command(order, spot)` (`Companion.cs:201`) | A post you choose, on corruption nights, capped by defences | S-M |
| Builders at home | Missing (away only) | `Construction.AddWork` (`:450`), the Forging emote | Remove the away-only condition; walk and hammer; host only | S |
| Queue or collect from afar | Mostly there | `StationCrafting.TryQueue`, `StationWindow` with a large range | An entry point in the view; gear into the stash | S |
| Your character working on autopilot | Missing | `PlayerControl` holds, `WalkPartyNode.LeadPlayer` (`Story/StoryWorldNodes.cs:658-682`) | NavMesh steering for the CharacterController, `PlayerInteractor.Begin(Interactable)` | L (cut in round two) |

### 1.7 Co-op

- **Local per machine:** camera, UI, fog and minimap. Friends see a `RemotePlayerBody` driven by your poses.
- **Decisions go through the host:** Haven decisions are `[HavenDecision("name")]` methods with string arguments through
  the existing ask/answer messages (`Multiplayer/CoopDecisions.cs:94,109`), so new orders need **no wire-format bump**
  (it is 32).
- **Residents are not networked:** each game simulates them from the settlement state, mirrored as save data every 2 s.
  - Orders that are saved state reach every game for free.
  - Live work must be counted on the host only.
  - Live movement that must match (a ring worker felling a particular tree, a fight) needs the horses' "one runner,
    puppets" pattern (`CoopHorses`), a new message and wire format 33.

### 1.8 Design rules any colony mode must keep

- **GDD:**
  - home is calm and untimed, and attacks are warned (§1.4, §6.3);
  - no elaborate life simulator (§2.6), and no micromanagement chore (§15.4);
  - essential production must not need idle guards, and automated defences must not trivialise breaches (§6.8);
  - "NPC activity should be visible so that hiring someone feels like changing the world" (§14.4).
- **Your rules:**
  - every effect is positive;
  - nothing is lost and nobody dies in a breach;
  - hand-ins happen in person;
  - workplaces are learned through quests (`BuildingGate`);
  - no text walls;
  - the keyboard and mouse layout stays identical;
  - keep default graphics quality;
  - judge base features by what they do for raids.
- **Economy:**
  - Cursed Essence comes only from raids;
  - workers make Plain goods and train nobody's skill (`WorkerProduction.cs:151`);
  - defences act only in breaches, "so the game hunted by day at home stays the player's" (`WatchPostArcher.cs:4`).
- **Pacing:** a 45-90 minute session should fit several raids (GDD §3.3).

---

## Part 2: How other games do it

### 2.1 Key takeaways from round one

1. **Time.**
   - Overhead views used under pressure pause or slow time (RimWorld, Kenshi, Dragon Age: Inquisition, Bannerlord), and
     every one of those is single-player.
   - Co-op base games run in real time.
   - **Safe Haven's view must run in real time.**
2. **The avatar while you look:** anchored (Fallout 4), asleep (Soulmask), absent (Dungeon Keeper) or one of the
   colonists (RimWorld, Kenshi).
3. **Orders scale with headcount:** verbs for 1-3 followers, priorities and zones for 10-30. For about 11 people, orders
   to each person work if they lapse back to the routine.
4. **What only the overview does:** placing, ordering many, seeing the whole base. "Place it, others build it" works
   (Factorio, Dragon Quest Builders 2).
5. **What only the hero does** keeps the hero meaningful (Brütal Legend, Bannerlord); Soulmask shows the opposite.
6. **Gamepad:** snap selection and select-all (Halo Wars), a small order wheel (Mass Effect). Necesse needed a separate
   patch for settlement control on a pad.

### 2.2 The closest models

| Game | How the switch works | The avatar meanwhile | Orders | Lesson |
|---|---|---|---|---|
| **Fallout 4** (workshop) | Hold V, in a settlement only | Stays put | Aim at a settler → Command → Assign | Same idea; warning: clumsy menus, and people must matter |
| **RimWorld / Going Medieval** | Draft or undraft | All AI until drafted | Priorities, zones; drafted move and attack; a bell drafts many | Orders lapse; one "everyone" order for danger |
| **Kenshi** | A free camera always | One of the squad | Follow, bodyguard, attack; Shift queues | Your character as one of the colonists |
| **Necesse** | Settlement panel (C) | Top-down hero, real time, co-op | Follow, guard a spot, clear orders; zones, priorities | Same camera, scale and co-op; plan the pad from the start |

### 2.3 Other games from round one at a glance

| Game | What it does | What reviewers said |
|---|---|---|
| Manor Lords (Visit Mode) | Walk your lord in third person, no interaction | Presence only |
| Dungeon Keeper (Possession) | Take one creature in first person | "Made you feel like the minion"; disorienting |
| Dungeons 3 | Two maps, two control models | One half "quickly gets left behind" |
| Dragon Age: Inquisition (Tactical View) | T or zoom out; pauses | Little used; cut from Veilguard |
| Factorio | Place plans from the map; robots build | Clean "place from above, others build" |
| Bellwright | Hero plus villagers run from menus | Depth praised; stuck NPCs, heavy menus |
| ASKA | One villager per station; Barracks guards; militia | Players want less babysitting |
| Soulmask | Possession, patrols, a 1.0 management mode | Followers make "the player character… optional" |
| Medieval Dynasty | A management tab only | Works because it stays small |
| Palworld | Pals take work; an alarm bell | Pathing and stuck pals |
| Conan Exiles | Thrall stances, patrols | Thralls idle in combat |
| Dragon Quest Builders 2 | Blueprint plus stocked chest, residents build | Closest to Safe Haven's scaffolds |
| Cult of the Lamb | Runs plus followers commanded in person | "Lacked depth… both as a roguelite… and a base manager" |
| State of Decay 2 | Control one survivor, swap to any | Swapping keeps everyone mattering |
| Brütal Legend | The hero fights and commands, flies up to order | Hidden RTS half; weak console control |
| Minecraft Legends | A hero with a banner, no overhead view | Simple on a pad; units ignore enemies |
| Savage / NS2 | One commander per team | NS2's command seat is often empty |
| Bannerlord | Orders while fighting; slow-motion order mode | Commanding while fighting keeps the hero central |
| Halo Wars | A console RTS | Tap, double-tap and hold A for selection |

### 2.4 What reviews say goes wrong

- **Each half is compared with its own genre:** Gord "fails to capture the appeal of any of its component genres".
- **One mode starves the other:** an optional, weaker mode goes unused (Dragon Age: Inquisition, NS2), and a too-strong
  one makes the other pointless (Soulmask).
- **Watching from above exposes the AI:** stuck and idle NPCs come up in nearly every review.
- **Menus multiply:** Fallout 4, Soulmask, Gord, Bellwright.

### 2.5 Round two: small named casts, hideouts, homecomings, light

**Small named cast with a town mode:**

- **Dark Cloud / Dark Chronicle (Georama).** Dungeon finds become building plans laid out on an overhead town. Each
  villager has wishes ("place my house facing east", "near the pond", "away from the mayor"). Meeting one gives a gift;
  all of them is optional.
- **Ni no Kuni II.** Citizens fill facility slots. Some research needs one particular person's skill, and citizens rise
  from Beginner to Veteran to Master by working. Kingdom levels open new land.
- **Enshrouded.** Each rescued craftsperson brings their own stations, and rescuing one unlocks the quest for the next.
- **Octopath Traveler 0.** Residents have perks that work better in certain facilities; residents in houses deliver wood
  and stone regularly.
- **MGS V Mother Base.** Specialists unlock developments. Deployments show the odds and predicted losses before you
  commit. Walking the base raises morale.
- **Spiritfarer.** About 11 spirits, each with requests that drive what you build. The first person's quests unlock the
  first stations.
- **AC Valhalla.** The settlement grows from a longhouse with materials from raids. A feast bell gives timed boosts, and
  a resident advisor says what to do next.
- **Dragon Age: Inquisition's war table.** Missions on timers, with a "preferred" advisor who gets the better reward.
- **Dave the Diver.** Staff are dispatched to gather, with a minimum and maximum set by their stats.

**Extraction hideouts and work while you are out:**

- **ARC Raiders.** Scrappy the rooster brings materials after each raid, whether you extract or not (guides; the
  developers call him a "resource producer… after each raid"). A material you "track" for an upgrade is tagged with an
  eye icon in raid loot.
- **Escape from Tarkov.** The hideout's crafts run in real hours while the generator has fuel found in raids.
- **Monster Hunter World / Rise.** Safaris and Meowcenaries progress by the quests *you* finish, not by the clock, and
  never fail.
- **Escape from Duckov.** Quests tell you which facility to build next.

**Plans and shopping lists:**

- **Satisfactory's to-do list** totals the materials for chosen recipes against what you have.
- **Grounded-style ghosts** cost nothing until the materials arrive.
- **Stardew Valley's bundles** take any few of several items, and a finished room is shown in a short scene at the end
  of the day.
- **Death Stranding:** other players' deposits fill the same project.

**Homecomings:**

- **Darkest Dungeon.** A chance of one town event on return, drawn like a card.
- **Hades.** People say something new on each return: "What would these characters talk about and what would they
  notice?" (Greg Kasavin).
- **Clash of Clans.** A defence log with what happened while you were away.

**Light as defence:**

- **Don't Starve.** Darkness kills; even the very edge of a fire's light protects you.
- **Gord.** Light restores sanity outside the walls, but makes you easier to see.
- **Frostpunk.** Heat zones don't stack where they overlap: one big source or many small ones.
- **Minecraft.** Torches stop monsters spawning nearby.
- **Darkwood.** A night event can black out the lights.
- **Thronefall.** Before each night the map shows where enemies come from and what they are; buildings wrecked at
  night come back by day.

**A village worth watching, without meters:**

- **Wildermyth.** Traits only make people eligible for story events.
- **Going Medieval.** Settler chats come from perks and state.
- **Oxygen Not Included.** "Overjoyed" bursts help the people nearby.
- **Manor Lords.** Daily rhythms and seasons.

---

## Part 3: Is it a good idea?

### 3.1 What it fixes

1. **The split.**
   - Residents get a visible part in the economy: rings they clear, walls they raise, parties they send.
   - Managing and walking happen in the same world, about the same people (Pillar D).
2. **The time between raids.**
   - The view turns "what next" into one place: what changed while you were away, what the next raid should bring
     home, who comes along, what the Haven does while you are gone.
   - It doesn't save much walking, because "Go there" and staffed stations already cover that.
   - It saves deciding time, and turns home into getting ready.
3. **Wood and stone.** These are the real grind at home, and standing orders that run during raids ease it without
   taking the hero's gathering away.
4. **Layout you can read.** The yards, paths, extensions and homes from 2026-11-15 read from above, as in Gord.
5. **Preparation as planning.** "Assign defenders" (GDD §6.7) has no verb today. Foretold corruption nights get an
   evening plan with blight at stake.
6. **It suits the look and the camera.** The atmosphere is modelled on Gord; the camera is already top-down; the Lookout
   already pulls back to about 56 m.

### 3.2 What works against it, and the answer to each

| Risk | How bad | Answer |
|---|---|---|
| Two half-games | High if it becomes an RTS | No box-selected armies, no unit production. The view is the Haven window laid on the world, plus a planner |
| The hero becomes optional | High if residents gather well | Rings at a quarter to a third of your pace, plain goods, no XP. Quality, rare finds, fighting, talking and raids stay yours |
| Orders that save seconds | Certain, for build and queue orders | Build the view around what is scarce: minutes, wood and stone, readiness |
| Stuck NPCs seen from above | Certain | Away work applied as results; Phase 0 cleans up movement first |
| Stripping the woods | Medium | Rings are finite; a forester replants; felled Haven trees otherwise stay down |
| Rendering at height | Medium-high, unmeasured | Measure first, cap the height, raise shadow and grass distances in the view rather than lowering quality |
| Co-op | Medium | Away work is state; live ring workers get one runner |
| Nothing at stake at night | High for patrol | Patrol cut; posts on foretold corruption nights, which move blight |
| Headcount and sameness | Medium | Orders unlock person by person; companion signatures make 9 Fighters different |
| Busywork | Medium | Orders lapse or finish; nothing gets worse if you never open the view |
| Exploits | Low-medium | No Cursed Essence or XP from residents; ring yields don't count for quests that teach the player |

### 3.3 Verdict

**It will make the Haven better if Phases 1-3 come together: the view, the between-raids planner, and standing orders
that run while you are away.**

- Phase 1 alone is a clearer view of the town, and a convenience.
- Phase 2 links the Haven to the next raid.
- Phase 3 is where the people become the base.

---

## Part 4: The design: the Keeper's view

### 4.1 Principles

1. **The same game, from further up.** The camera rises from your character and returns to it.
2. **Built around what is scarce:** your minutes, wood and stone, readiness for the next raid.
3. **The view runs the Haven; the avatar lives in it** (§4.11).
4. **Nothing needs the view.** Routines, the H window and walking all keep working.
5. **Orders lapse or finish.** There is nothing to babysit.
6. **Real time**, in solo and co-op alike.
7. **Pictures, not text walls.**
8. **Haven only.** Never in raids, the prologue or a breach's defence.

### 4.2 Getting in and out

- **Unlock:** when the Heartroot names you "Hearth-keeper" and the road north opens (`Docs/QuestRedesign.md:136,300`). By
  then the Haven has enough in it to look over, and the name fits.
- **Way in:**
  - its own rebindable key (backquote or Caps Lock are free);
  - a "Look over the Haven" button in the H window's header;
  - "Keeper's view" on the pad's windows wheel.
  - The camera glides up in about half a second. H stays the window for a quick glance.
- **Way out:** the same key, Esc, B, or clicking your own portrait. The camera returns to your character.
- **Can't enter:** during raids, the prologue, a breach's defence, a dialogue or cinematic, or while swimming or riding.
- **Thrown out (the camera dives to you, with a horn) when:**
  - **you** are hurt or hunted. This needs a self-only check: `AnyHuntingPlayerSide` counts friends;
  - a breach's defence begins;
  - a cinematic starts.

### 4.3 The camera

- **Position:** about 30 m to start, zooming from 20 m to a cap set by Phase 0's measurements (about 50 m to begin with).
  It tilts from about 50° to 65° as it rises.
- **Controls:** WASD, the screen edge (optional) or middle-drag to pan; Q and E to rotate (in a view-only map); the
  wheel to zoom. Left-click selects, right-click gives the context order, F follows the selected person, Space finds
  your character.
- **Bounds:** Haven Meadows (`HomePortal.InBuildLand`) plus a margin. The height follows the terrain, smoothed.
- **Built from:** a second `CinemachineCamera` at priority about 500, with a "stand down" flag on
  `PlayerCameraController`.
- **Optional:** the height cap rises with the Watch Post's tier. It gives the post a use at home and keeps the
  rendering cost low early.

### 4.4 What the view shows

- **People:**
  - a small activity sign (hammer, axe, pot, bed, spear);
  - names and portraits on hover or selection only;
  - rank badges;
  - a wish picture over a home when someone wants something (§4.9).
- **Buildings:** on hover, an outline (`OutlineCopies`) and a card with the painting, tier, queue progress, output ready
  and who works there.
- **Along the top:** stores as picture cards with counts, the hour, the weather, and the portal's next tier
  (`WantedAtHome`).
- **On the ground:**
  - rings and their progress;
  - posts;
  - plans (ghosts);
  - the ward ring and the edge of the build land;
  - on a foretold night or a warned breach, the lanes and the creatures' kind (as in Thronefall).
- **Night:** lit ground stays readable and the dark stays dark. The flat fog-of-war plane fades, as in cinematics.

### 4.5 Between raids: the homecoming and the planner

- **Homecoming sweep.**
  - On return, the view can open to a short, skippable flight over what changed: scaffolds standing, rings cleared,
    goods made, a party back.
  - Each building with news gets a mark built from `Homecoming`'s lines.
  - At most one "while you were away" card is drawn from a deck filtered by the Haven's state (as in Darkest Dungeon),
    always positive.
- **The portal card is the raid planner.**
  - Click a resident to take them as your companion, and they walk to the portal.
  - Pack the raid kit from the stash with "Pack as last time" (`PackForRaid`).
  - "Set out" dives to your character at the portal.
- **Plans and the tracked shopping list.**
  - Place a building as a **plan**: a ghost that costs nothing until the materials are there.
  - Its missing materials go on a list.
  - Pin it, and those items are tagged with an eye mark in raid loot (as in ARC Raiders).
  - In co-op, anyone's deposits fill the same plan.
  - Some slots accept any of a kind ("any hardwood"), so a plan doesn't stall on one drop.

### 4.6 Standing orders while you are away

Round two's biggest win. `SettlementSystem` already runs workers and banks builder time during raids
(`SettlementSystem.cs:56-69`, `ResidentBuilders.cs:125-204`). Standing orders do the same:

| Order | Opened by | What it does | Paid out |
|---|---|---|---|
| **Clear this ring** (painted trees) | Tor (Carpenter) | Fells trees in a drawn ring; the forester replants behind (`PlantedTrees`) | Each raid, Plain wood |
| **Quarry this knoll** | Hild (Mason) | Breaks stone in a ring | Each raid, Plain stone |
| **Hunting or fishing party to a held land** | Gunnar (Hunter or Fisher) | A small tithe for one raid and slower erosion, never a raise in hold (Territory's "not yet" item) | On return |
| **Scout a held land** | Runa (Tracker) | Shows the land's next push one raid early | On return |

- **Counted in raids, not hours** (Monster Hunter, ARC Raiders), so co-op clocks don't matter.
- **The odds are shown before you send** (a yield range), with a "preferred person" bonus (as in Dragon Age's war
  table).
- **Nobody dies or is lost.** The worst result is "came back tired, rests a day". A failed raid still pays a part (as
  Scrappy does).
- **Applied as state on return:** nobody needs to be watched walking, there is nothing to sync in co-op, and the value
  grows with how much you raid.

### 4.7 Living work at home

- **Builders at home.** Carpenter and Mason, plus anyone ordered, hammer scaffolds in work hours with the Forging emote.
  - This means removing the away-only condition (`SettlementSystem.cs:60`); `AddWork` runs on the host only, at 0.5 a
    second against your 1 + Build Speed.
  - Their real work is **drawn wall rings**: 30 pieces at 3-5 s each.
  - There is no "next" pin.
- **Felling and quarrying rings at home.** These are the same orders as §4.6, done in the open while you are home:
  - painted trees first (they need a fell-by-index call that doesn't need the player within 10 m);
  - finite (the ring is done when it is empty);
  - overgrowth left out (that is your early renown);
  - about a quarter to a third of your pace;
  - Plain yields, no Hardwood or Elderwood, no XP, no Cursed Essence.
- **Carrying.** A gatherer carries a bundle home, and the stash goes up when they arrive. There are no hauler jobs.
- **Horses haul.** The "Haul timber" horse job (`Resources/HorseSettings.asset:138-142`) says it "drags logs up to the
  sawbench" but only adds 30% speed. Make it real: a working horse doubles a gatherer's load and is seen dragging the
  log.
- **The forester loop.** The woodcutter replants saplings behind a ring, so long-run wood is capped by how fast saplings
  grow, and the Haven stays green, as the art direction wants.
- **Co-op:** live ring workers need one runner (the host), with friends seeing copies, or friends see only a ring's
  progress. Away orders need nothing.

### 4.8 Defence: corruption nights and breaches

- **Corruption nights** are already foretold with a direction (`Settlement/CorruptionNights.cs:1-9`).
  - Beaten back, blight falls (`:286`); let through, it rises (`:337-338`).
  - Blight drives land erosion and the next night's odds.
  - Today residents shelter, and the Watch Post archer shoots only in breaches (`WatchPostArcher.cs:59`).
- **The new evening plan:**
  - set posts on the foretold side;
  - the Watch Post archer joins on those nights;
  - the horn sends posts out and everyone else in.
- **Posts are capped by the defences you have built:** Watch Post tier, gates, the Palisade. Without the cap, eleven
  guards who can't die would set the balance.
- **Breaches:**
  - In the warning and preparation steps the view shows the lanes; you mend, place traps and set posts.
  - The defence is on foot, with the same combat as raids (GDD §6.7).
  - Recovery is back in the view.
  - Nobody dies: guards get up by themselves.
- **Cut from round one:** Patrol, Follow me, and the explicit Attack order. Prowlers never enter light, so a patrol has
  nothing at stake. Follow me duplicates the raid companion. Attack has no target outside a defence.

### 4.9 People who matter

- **Each rescued person brings one order** (Enshrouded, Ni no Kuni II, Spiritfarer). Orders nobody can do yet are
  greyed, with "needs someone who…". This paces the view's tutorial at one new verb a homecoming (Pillar E) and fits the
  quest flow: each person's quest teaches their mechanic.

  | Person | Order they bring |
  |---|---|
  | Tor | Builders at home, felling rings |
  | Eirik | Posts |
  | Gunnar | Hunting and fishing parties |
  | Asa | Ploughing with a horse |
  | Hild | Quarry rings |
  | Runa | Scouting a held land |
  | Sigrid | The horn |

- **Wishes met from above** (Dark Cloud).
  - One or two per person: "near the mere", "a bench by my door", "away from the forge's noise".
  - They show over their home, and while you drag a plan.
  - Meeting one gives a line, an emote and a small gift. Likings (`HomeLife.cs:159-162`) already hold half of this.
  - Positive only.
- **Lines that notice the raid** (Hades): one per person per homecoming, about what you brought back or what happened,
  spoken as you pass.
- **Small scenes, not meters** (Wildermyth): two friends singing at the mere, shown as a bubble you can click. Only
  "overjoyed" bursts, each helping the people nearby a little.
- **Ranks as badges:** the milestone ranks decided on 2026-11-13. Decide whether gathering counts as work.
- **Companion signatures** (decided on 2026-11-13, after the playtest), so the nine Fighters fight differently. The view
  puts their sameness on display.
- **An empty house for someone lost** (GDD §15.6): their house goes dark, their station stands idle, and their rescue
  rumour shows on hover.

### 4.10 Your own character while you look

- **It waits:** it sits at the nearest fire or bench, or keeps hammering if you entered mid-build.
- **Friends see a "keeping" mark** over it.
- **Round one's autopilot job is cut:** builds take 2-12 s, so an autopilot builder saves seconds for an L-sized cost.
  - Your first idea ("moves along like other NPCs") is still open in Part 7, with that cost stated.

### 4.11 What stays the avatar's

- All raids.
- Talking, quests, rescues and hand-ins.
- Fighting with skill, at breaches and corruption nights.
- The fastest hammering.
- Gathering with the timing minigame, tool tiers, Hardwood and Elderwood, burst yields and XP.
- Fine-quality crafts.
- Fishing, horses, hunting, lookouts and finds.
- Meals, the sauna and Well Rested.

Residents get the plain basics.

### 4.12 Controller

- **Way in:** the windows wheel.
- **In the view:**

| Control | Does |
|---|---|
| Left stick | Pan |
| Right stick | Rotate (sideways) and zoom (up and down) |
| A | Select or confirm; the cursor snaps to the nearest person or building |
| Double-tap A | Everyone at that workplace |
| X | The order wheel |
| Y | Find your character |
| LB / RB | Step through the side rail |
| B or View | Back, then out |

- **Building:** the pad cursor places, instead of an offset from your character.
- **Keyboard:** the existing layout stays identical; view keys live in their own map.

### 4.13 Rendering at height: measure first

- **Shadows:** sun shadows end 50 m from the camera. In the view, lengthen them (about 90-110 m) and measure; if it
  costs too much, lower the cap rather than the quality.
- **Grass:** it ends at 80 m; the view needs about 110 m. Measure.
- **Things that should follow the view's focus, not the body:** fog of war (fade first), see-through (off), ground
  relief, sky occlusion, the reflection probe and hearing.
- **Spawns:** check that everything spawning relative to the player also hides from the camera (`MobSpawnZone` doesn't
  yet).
- **Where the cost will be:** earlier Haven performance work found the frame goes to the tessellated terrain passes and
  tree shadows. Measure the same way at 35, 45 and 55 m.

### 4.14 Later, if the core works

- **Stores you can see:** woodpiles, stone heaps and sacks at the yards that grow and shrink with what is stored. They
  show the charcoal warning (`TorchFuel.cs:36,173`) without UI. Mostly art.
- **Paths from worn ground:** residents already wear footpaths (`Settlement/Footpaths.cs:1-7`); the view offers "lay a
  path here" along the worn line.
- **Light as territory:**
  - an overlay of lit ground;
  - a lamplighter lighting the wall torches at dusk on a visible round;
  - overlapping light adds nothing, so the choice is one big fire or many lanterns (as in Frostpunk).
  - A resident's lantern (`ResidentLantern.cs:22-27`) counts as lit ground only if an order needs it.
- **Event pins:** a corruption night or breach leaves a pin where something happened; clicking it gives a short recap.
- **Being seen:** residents you walk past at work get a small "seen" lift (MGS V's morale walk). This is a positive
  reason to walk the Haven on foot.

---

## Part 5: Adjustments to existing mechanics

These follow from round two. Each is small on its own; together they decide whether the view feels good.

| Mechanic | Today | Change | Where |
|---|---|---|---|
| Builders | Work only while you are away | Also at home in work hours; host only | `Settlement/SettlementSystem.cs:60`, `HomeLifeSettings.asset` (0.5/s) |
| Wall rings | Drawn by you, hammered by you | Builders take wall pieces | `BuildPlacer` lines, `Construction.AddWork` |
| Felling at home | Trees stay down; stand-ins near the player only | Fell by index for ring workers; a forester replants | `Gathering/TerrainTreeFelling.cs:21,111,267-304`, `PlantedTrees` |
| Wood and charcoal pace | 888 oak plus about 770 for charcoal, by hand | Redo the tables with rings at a quarter to a third of your pace; check torches plus ingots don't turn wood into upkeep | `Docs/BaseProgression.md:155-180`, `TorchFuel.cs` |
| Quest gather objectives | Only bag arrivals count; an event with no actor counts as the player's | Ring yields go to the stash without raising item events, so quests that teach you don't count them. The Haven's asks (`WantedAtHome`) do | `Items/ItemInventory.cs:129,157`, `Events/GameEvents.cs:175-177`, `Items/LootGrant.cs:31` |
| Worker ranks | Count crafts | Milestone ranks (decided 2026-11-13), shown as badges; decide whether rings count | `SettlementSettings.cs:69-72`, `SettlementState.cs:803` |
| Breach guards | Jobs with `DefendsAs` only (Warden, Hunter) | Posts capped by defences built; retune against a breach with posts and no player | `ResidentLife.cs:230`, `WardBreachSettings` |
| Corruption nights | Residents shelter; the archer is breach-only | Posts and the Watch Post archer on those nights | `CorruptionNights.cs`, `WatchPostArcher.cs:59` |
| Being thrown out of the view | — | A self-only "am I hunted" check | `AI/MonsterAI.cs:200` (`AnyHuntingPlayerSide` counts friends) |
| Gear collected from afar | Into your bag, quality rolled then | Into the stash, from the view | `Crafting/StationCrafting.cs:177-192` |
| Territory | Residents working a held land is "not yet" | Hunting and fishing parties and scouting as standing orders | `Docs/Territory.md:117` |
| Horse hauling | +30% speed | A working horse doubles a gatherer's load and is seen dragging the log | `Resources/HorseSettings.asset:138-142` |
| Resident movement | No area mask; warp after 4 s stuck | Keep out of deep water; count and cut warps before the view ships | `ResidentLife.cs:117,544`, `AI/DeepWater.cs` |
| Schedule | Work 8-18 is about 10 real minutes; cold sends people in | Show it in the view; away orders don't depend on hours | `ResidentRoutine.cs:63-95` |

---

## Part 6: Phased plan

| Phase | What | Effort | What it gives | Risk | Co-op | How to verify |
|---|---|---|---|---|---|---|
| **0. Groundwork** | V clash; deep-water mask for residents; count and cut stuck warps; measure frame time, shadows and grass at 35/45/55 m | S-M | A height cap and clean movement | Low | None | `InputActionsTests`; a Play probe counting warps over a game day; frame-time captures |
| **1. The view** | Camera rig plus stand-down; presentation switches; view map and rebinds; key, header button, pad wheel; hover and select past 80 m; station cards with gear into the stash; pad cursor placement; entry and exit rules (self-only hunted); **builders at home and wall rings** | M-L | Read and run the town from above; walls rise while you are home | Low-medium | Local view; builders on the host | `InputActionsTests`; `ConventionTests`; Play probe: enter, queue, upgrade, place, a wall ring finishes with nobody hammering, exit; UI overlap audit |
| **2. Between raids** | Homecoming sweep and marks; one "while you were away" card; portal card as planner (companion by click, Pack as last time, set out); plans and the tracked shopping list | M | Home time becomes getting ready; raids get a shopping list | Low | Plans as decisions; shared deposits | Play probe: raid out and back, the sweep shows the changes; a pinned plan's items marked in raid loot |
| **3. Standing orders** | Fell by index; felling and quarry rings with the forester; away results counted per raid; hunting, fishing and scouting parties; carrying; horses hauling | M-L | The wood and stone grind eased; residents visibly work | Medium: the economy | Away results as state; live ring workers one runner (wire 33) | Pace check against BaseProgression; Play probe: a ring clears over N raids and saplings grow; `CoopWireTests` |
| **4. People** | Orders opened person by person; wishes; homecoming lines; small scenes; rank badges; the empty house; companion signatures (as decided on 2026-11-13) | M | The people are the base | Low | Mirrored state | `QuestAuditTests`/`ResidentQuestsTests` for the order gates; Play probe: a wish met from above |
| **5. Defence** | Posts capped by defences; corruption-night posts plus the archer; the horn; breach preparation in the view; lanes and kinds shown | M | Evenings with something at stake | Medium: breach balance | One runner for fighting posts | Breach and corruption-night probes with posts and no player; the two-copy co-op script |
| **Later** | Visible stores, paths from worn ground, light overlay and lamplighter, event pins, the "seen" lift | S-M each | Depth | Low | — | — |

Effort is S/M/L from the audits, not a measured estimate. Each phase ships on its own and leaves the game working if the
next one never comes.

---

## Part 7: Questions for you

Recommended answers first.

1. **The key.**
   - **Recommended: H stays the window; the view gets its own key (backquote or Caps Lock), a header button and a pad
     wheel entry.**
   - H at home becomes the view.
   - V, moving the camera style and Stack On Stored elsewhere.
2. **When it opens.**
   - **Recommended: when the Heartroot names you Hearth-keeper (The Road North).**
   - From the start.
   - With the Watch Post.
3. **Your character while you look.**
   - **Recommended: it waits at the hearth, or keeps hammering if you left it mid-build.**
   - It works on autopilot like a resident (your first idea; L effort for seconds saved).
4. **Standing orders while you are away.**
   - **Recommended: yes, counted in raids, odds shown, nobody lost, applied on return.**
   - No.
5. **Felling.**
   - **Recommended: finite rings, painted trees, a forester replanting so the woods stay green.**
   - Rings with refilling stock targets (strips the woods).
   - No resident felling.
6. **Defence orders.**
   - **Recommended: posts on foretold corruption nights and breaches, capped by defences; Patrol, Follow and Attack
     cut.**
   - Keep round one's six verbs.
7. **Orders unlock person by person.**
   - **Recommended: yes.**
   - All at once.
8. **Plans and the tracked shopping list.**
   - **Recommended: yes, ghosts that cost nothing until stocked, with marks in raid loot.**
   - No.
9. **Resident yields and quests.**
   - **Recommended: they count for the Haven's asks, not for quests that teach you.**
   - Never.
   - Always.
10. **When to build.**
    - **Recommended: Phases 0 and 1 now, Phase 2 right after, Phases 3-5 once you have used the view.**
    - All now.
    - After the full slice playtest.
11. **The name.**
    - **Recommended: "the Keeper's view".**
    - "Overseer view", "Haven view", or yours.

---

## Sources

Web research, 2026-11-21. Guides and wikis carry most details; developer sources are noted where they exist; forums are
labelled.

**Round one**

- Fallout 4 workshop: https://www.ggrecon.com/guides/fallout-4-assign-workers/ ·
  https://www.gameskinny.com/opinion/why-i-restarted-fallout-4-and-began-completely-ignoring-settlements/ ·
  https://wccftech.com/starfields-outposts-meaningless/amp/
- Manor Lords Visit Mode: https://www.escapistmagazine.com/how-to-enter-third-person-in-manor-lords-using-visit-mode/
- Dungeon Keeper: https://en.wikipedia.org/wiki/Dungeon_Keeper
- Dungeons 3: https://www.gamewatcher.com/dungeons-3/review ·
  https://www.digit.in/features/gaming/dungeons-3-dungeon-management-meets-rts-59078.html
- Dragon Age: Inquisition tactical view: https://opencritic.com/news/3745/dragon-age-the-veilguard-has-removed-the-tactical-camera ·
  https://hothardware.com/reviews/dragon-age-inquisition-reviewed-and-benchmarked?page=3
- Factorio: https://www.factorio.com/blog/post/fff-380
- RimWorld drafting: https://rimworldwiki.com/wiki/Drafting
- Going Medieval: https://techraptor.net/gaming/guides/going-medieval-guide-for-beginners
- Stranded: Alien Dawn: https://www.godisageek.com/2022/10/stranded-alien-dawn-is-a-3d-rimworld-early-access-impressions/
- Kenshi: https://kenshi.fandom.com/wiki/Controls · https://gamersdecide.com/articles/kenshi-beginners-guide-tips
- Gord: https://en.wikipedia.org/wiki/Gord_(video_game) · https://gameskinny.com/reviews/gord-review-villagers-and-villains
- Bellwright: https://noobfeed.com/articles/bellwright-advanced-production-orders-priorities ·
  https://steamdb.info/patchnotes/14721717 · https://opencritic.com/game/20911/bellwright/reviews ·
  https://www.psu.com/reviews/bellwright-ps5-review/
- ASKA: https://low.ms/blog/aska-npc-villager-management-guide ·
  https://steamcommunity.com/app/1898300/discussions/0/4346618033171231496 (forum)
- Soulmask: https://www.gameskinny.com/tips/how-to-recruit-tribesmen-in-soulmask/ ·
  https://www.cgmagonline.com/articles/soulmask-tribe-management-in-2026/ ·
  https://fullcleared.com/reviews/soulmask-review-a-tribe-called-quit/
- Medieval Dynasty / Sengoku Dynasty: https://www.gfinityesports.com/medieval-dynasty/medieval-dynasty-jobs-guide-how-to-assign-roles-to-villagers-and-make-workers/ ·
  https://progameguides.com/sengoku-dynasty/how-to-assign-jobs-in-sengoku-dynasty/
- Palworld: https://www.destructoid.com/what-to-do-when-palworld-pals-wont-listen-to-you-or-work/
- Necesse: https://necessewiki.com/Settlement_Interface · https://steamdb.info/patchnotes/9885846
- Conan Exiles: https://conan-exiles.com/blog/1036/update-40-followers-ii-ai-stances-and-command-system/ ·
  https://forums.funcom.com/t/thralls-idle-in-combat/249532 (forum)
- V Rising: https://segmentnext.com/guides/v-rising-servants
- Sons of the Forest: https://www.corrosionhour.com/sons-of-the-forest-kelvin-companion-guide/
- Dragon Quest Builders 2: https://gematsu.com/2018/12/dragon-quest-builders-2-details-endless-battle-island-setting
- Cult of the Lamb: https://twinfinite.net/guides/how-to-command-followers-in-cult-of-the-lamb/ ·
  https://www.escapistmagazine.com/?p=124330
- State of Decay 2: https://www.windowscentral.com/state-of-decay-2-review
- Brütal Legend: https://www.gamedeveloper.com/pc/postmortem-behind-the-scenes-of-i-brutal-legend-i- ·
  https://en.wikipedia.org/wiki/Br%C3%BCtal_Legend
- Minecraft Legends: https://pcgamer.com/minecraft-legends-brings-down-the-tempo-of-typical-strategy-games-mojang-says ·
  https://pcgamer.com/minecraft-legends-review
- Savage / Natural Selection 2: https://en.wikipedia.org/wiki/Savage:_The_Battle_for_Newerth · https://unknownworlds.com/ns2/?p=2697
- Mount & Blade II: Bannerlord: https://www.gamerguides.com/mount-and-blade-ii-bannerlord/guide/introduction/useful-gameplay-tips/tactics
- Halo Wars: https://waywardstrategy.com/2020/03/23/halo-wars-the-ultimate-design-for-console-rts/
- Overlord II: https://www.gamespot.com/articles/overlord-ii-updated-hands-on/1100-6210175/
- Mass Effect: https://strategywiki.org/wiki/Mass_Effect_3/Controls

**Round two**

- Dark Chronicle / Dark Cloud: https://en.wikipedia.org/wiki/Dark_Chronicle ·
  https://almarsguides.com/retro/walkthroughs/ps2/games/darkcloud/misc/townlayouts/norunevillage/
- Ni no Kuni II: https://ssl.level5.co.jp/game/ninokuni2/pc/manual/en/kingdom02.html (Level-5 manual) ·
  https://www.rpgsite.net/feature/6960-ni-no-kuni-ii-kingdom-building-management-guide-how-to-create-the-best-kingdom
- Suikoden II: https://game8.co/games/Suikoden-2/archives/497281
- MGS V Mother Base: https://segmentnext.com/mgs-v-phantom-pain-mother-base-guide-how-to-expand-recruit-customize ·
  https://www.gameskinny.com/mp7tq/metal-gear-solid-v-the-phantom-pain-combat-unitcombat-deployment-guide
- AC Valhalla: https://news.ubisoft.com/en-us/article/3hiYsX0odqVEi6J3dibAmJ/assassins-creed-valhalla-forge-alliances-build-a-village-and-be-a-kingmaker ·
  https://www.pcgamer.com/assassins-creed-ac-valhalla-settlement-supplies-raw-materials/
- AC Brotherhood: https://www.gamepressure.com/assassinscreedbrotherhood/assassin-guild/z72a2b
- Dragon Age: Inquisition war table: https://primagames.com/eguides/dragon-age-inquisition-eguide/the-war-council/the-war-table
- Octopath Traveler 0: https://www.gematsu.com/2025/09/octopath-traveler-0-details-story-of-revenge-companions-and-key-systems ·
  https://hacktheminotaur.com/octopath-traveler-0/ultimate-octopath-traveler-0-town-building-guide/
- Fantasy Life i: https://game8.co/games/FANTASY-LIFE-i/archives/522911
- Enshrouded: https://enshrouded.wiki.gg/wiki/Craftspeople
- Dave the Diver: https://www.gamepressure.com/dave-the-diver/how-to-use-the-dispatch-command/z410d78
- Spiritfarer: https://www.nme.com/reviews/spiritfarer-review-a-beautiful-management-game-about-dying-too-mired-in-frustrating-busywork-2732038 ·
  https://guidestrats.com/spiritfarer-gwen-guide/
- Escape from Tarkov hideout: https://www.gamepur.com/guides/escape-from-tarkov-hideout
- ARC Raiders: https://www.arcraiders.com/news/the-story-of-scrappy-the-rooster (developer) ·
  https://allthings.how/arc-raiders-how-to-upgrade-scrappy-and-find-every-required-item/ ·
  https://allthings.how/arc-raiders-workshop-upgrades-steps-tracking-and-materials/
- The Cycle: Frontier: https://thecyclefrontier.wiki/wiki/Personal_Quarters
- Escape from Duckov: https://allthings.how/escape-from-duckov-early-raids-quest-order-and-hideout-upgrades
- Satisfactory to-do list: https://satisfactory.fandom.com/wiki/To-Do_List
- Ghost building (Grounded-style, Valheim mod): https://thunderstore.io/c/valheim/p/Azumatt/GhostBuild/
- Death Stranding roads: https://pushsquare.com/guides/death-stranding-how-to-build-roads
- Stardew Valley bundles: https://stardewvalleywiki.com/Bundles
- Darkest Dungeon town events: https://darkestdungeon.wiki.gg/wiki/Town_Event
- Monster Hunter: https://game8.co/games/Monster-Hunter-World/archives/315192 · https://monsterhunterrise.wiki.fextralife.com/Meowcenaries
- Hades: https://gamespot.com/articles/hades-writer-explains-script-philosophy-after-player-discovers-incredible-detail/1100-6482650/
- Clash of Clans: https://supercell.com/en/games/clashofclans/blog/release-notes/get-ready-for-ranked-update
- Gord's darkness: https://shacknews.com/article/136367/gord-is-the-darkest-dungeon-of-colony-sims
- Don't Starve: https://dontstarve.wiki.gg/wiki/Charlie
- Darkwood: https://gamerjournalist.com/how-to-survive-the-night-in-darkwood/
- Frostpunk heat: https://www.gamepressure.com/frostpunk/heat/z8a12d
- Minecraft spawn-proofing: https://minecraft.wiki/w/Tutorial:Spawn-proofing
- Thronefall: https://pcgamer.com/this-smart-yet-simple-kingdom-survival-game-might-be-my-next-strategy-fix
- Wildermyth: https://wildermyth.com/wiki/Event
- Going Medieval social interactions: https://techraptor.net/gaming/news/going-medieval-dev-diary-reveals-new-social-interaction-system
- Oxygen Not Included: https://forums.kleientertainment.com/forums/topic/113965-joy-responses/ (forum)
- Manor Lords daily life: https://pcgamer.com/games/city-builder/a-medievalists-take-on-manor-lords-it-represents-medieval-peasants-and-their-lives-more-fully-than-most-games

Not verified: whether Fallout 4's workshop mode pauses; what Soulmask 1.0's management mode looks like; V Rising
real-time servant orders; the exact Scrappy thresholds; whether AC Brotherhood recruits can die.

---

## Appendix: key file references

- **Camera:** `Camera/PlayerCameraController.cs` (`:102`, `:287-295`, `:381`, `:433`, `:440-482`, `:618-661`),
  `Cinematics/CinematicView.cs`, `Settlement/HavenLookout.cs:149-153`.
- **Input:** `Input/InputSystem_Actions.inputactions`, `Core/GameInput.cs` (`:136-165`, `:203`),
  `Player/PlayerInputHandler.cs:262-298,396`, `Core/Options/InputRebinds.cs:191,210`, `UI/CommandWheel.cs:188`,
  `UI/HavenWindow.cs:46`.
- **Remote actions:** `Crafting/StationCrafting.cs:75-192`, `UI/StationWindow.cs:397`, `Settlement/HavenBuildings.cs:134,239`,
  `Settlement/HavenPeople.cs`, `Settlement/HavenTravel.cs:15-46`.
- **Building:** `Building/BuildPlacer.cs:611-635`, `Settlement/HomePortal.cs:159-170`, `Building/Construction.cs:139,278,388-450`,
  `Data/Buildings/*.asset` (`buildWork`).
- **People and work:** `Settlement/ResidentLife.cs:117,227-252,544`, `Settlement/ResidentRoutine.cs:63-95`,
  `Settlement/ResidentDefender.cs:35`, `Settlement/WorkerProduction.cs:135-160`, `Settlement/ResidentBuilders.cs:125-204`,
  `Settlement/SettlementSystem.cs:56-69`, `Settlement/HomeLife.cs:159-162`, `Settlement/Homecoming.cs`,
  `Settlement/WantedAtHome.cs`, `Items/PackForRaid.cs`.
- **Gathering and trees:** `ResourceInventory/ResourceNode.cs:287`, `Gathering/ChoppableTree.cs:229`,
  `Gathering/TerrainTreeFelling.cs:8-13,21,64,111,267-304`, `Gathering/PlantedTrees.cs`, `Settlement/Overgrowth.cs:11`,
  `Docs/BaseProgression.md:155-180`, `Environment/TorchFuel.cs`.
- **Defence:** `Settlement/CorruptionNights.cs`, `Settlement/WatchPostArcher.cs:4,59`, `Settlement/NightProwlers.cs`,
  `AI/MonsterAI.cs:200`.
- **Quests and events:** `Items/ItemInventory.cs:129,157`, `Events/GameEvents.cs:175-177`, `Items/LootGrant.cs:31`,
  `Docs/QuestRedesign.md:136,300`, `Docs/Territory.md:112-120`.
- **Co-op:** `Settlement/HavenDecisions.cs:63`, `Multiplayer/CoopDecisions.cs:94,109`, `Multiplayer/MultiplayerSettings.cs:85`,
  `CoopHorses`.
- **Rendering:** `Settings/Rendering/PC_RPAsset.asset:60`, `Rendering/SkyOcclusionMap.cs:204`,
  `Environment/Atmosphere/AtmosphereFeature.cs:438-442,601`, `Environment/SeeThrough.cs:41-55`, `Audio/AudioManager.cs:493`.

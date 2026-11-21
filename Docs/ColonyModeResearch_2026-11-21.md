# Colony mode research: running the Haven from above

Research date: 2026-11-21. Read-only: no game code, data, scenes or prefabs were changed, and no Unity CLI or Play mode
was used. Three agents did the groundwork (other games, on the web; the camera, input and player side of the code; the
people, work and orders side of the code). I then checked the claims the recommendation rests on in the code myself.
`path:line` points at `test` as of commit 1b09cedc. Paths are under `Assets/_Project/Scripts/` unless they say otherwise.

The user asked:

> "I feel like [the Haven is] being pulled in two different directions that doesn't quite match. Pressing V or any other
> free key, in Haven, will turn the camera into colony sim mode, allowing the camera to be moved freely along the terrain,
> like Gord does. The player character then moves along in the base like other NPCs do. This will change the UI to focus
> more on the haven and its inhabitants. I can place down buildings that the player or NPCs can start building, interact
> with existing buildings, queuing up items to process and create or start upgrades etc. I could also control NPCs from
> this POV, giving them orders, such as patrol, defend, attack, farm timber, stone or other nodes. At any time I can go
> back to player mode. Only in Haven, not raids. Research if other games do something similar and how they made it work.
> Will this be a good addon? Will it make it better? What works against it? Suggest how we can make this work and how we
> can improve the idea and make it a complete and deeper feature."

---

## 0. Summary

### The short answer

**Yes, it fits Safe Haven, but only if it does one thing: the people you can see do the work you order.** A free camera
on its own would only be a prettier H window. The "two directions" feeling has a cause you can see in the code, and the
camera does not fix it on its own:

| What happens in the Haven | Done by a body in the world? | Where |
|---|---|---|
| Hammering a scaffold while you are home | Only the player | `Building/Construction.cs:388-393` |
| Builders finishing scaffolds | Bookkeeping, and **only while the player is away** | `Settlement/SettlementSystem.cs:60`, `Settlement/ResidentBuilders.cs:125-204` |
| Workers making goods | Bookkeeping: costs out of the stash, goods back in, nobody carries anything | `Settlement/WorkerProduction.cs:135-160` |
| Gathering wood, stone, forage | Only the player | `ResourceInventory/ResourceNode.cs:287` |
| Defending in a breach | The player, plus Warden and Hunter as companion stand-ins | `Settlement/ResidentLife.cs:227`, `ResidentDefender.cs:35` |
| Residents' days | Real: they walk, work at their yard, eat, sleep in their homes | `Settlement/ResidentRoutine.cs:74`, `ResidentLife.cs` |

So the residents are well-behaved scenery. They have names, homes and routines, but nothing in the economy depends on
where they are or what they do. The avatar does every physical thing, and the H window does the managing from a list.
A colony view where you order Hild to clear a ring of birches, and then see her doing it, joins the two halves. A view
where you only click menus from higher up does not.

### What other games show

No shipped game does exactly this: a key that hands an action hero to the AI while the player runs the base from a free
camera. Each part of it does exist somewhere:

- **Fallout 4's workshop mode** is the nearest switch: a key (V on PC), only inside the settlement, used to place
  buildings and assign settlers. Its menus were called "cumbersome", and the lesson its best modder drew was that
  "people are what matter".
- **RimWorld and Going Medieval's drafting** is the right model for the hand-off between AI and direct control: an order
  is an override that lapses back to the routine, and a bell drafts everyone at once.
- **Kenshi** is the nearest to "your character becomes one of the colonists": a free camera, and your character is one
  of a small squad you follow or order.
- **Necesse** is a top-down action game with a co-op settlement at Safe Haven's scale. Its orders are follow, guard
  here and "clear orders" (back to work), plus zones and priorities. It needed a whole extra patch to make settlement
  control work on a controller.

### What works against it

1. **The "two half-games" problem.** Brütal Legend, Gord, Dungeons 3 and Minecraft Legends were all judged against the
   dedicated genre and found shallow.
2. **One mode can starve the other.** Soulmask's followers made the hero "optional"; Dragon Age: Inquisition's tactical
   camera went unused and was cut from the sequel.
3. **A view from above shows every stuck NPC.** Every colony hybrid's reviews name pathing. Safe Haven's residents warp
   after 4 s stuck (`ResidentLife.cs:544`), which nobody notices from 15 m behind the player but everyone notices from
   above.
4. **Rendering at height.** Sun shadows end at 50 m from the camera (`Settings/Rendering/PC_RPAsset.asset:60`), grass at
   80 m, and the fog of war is a flat plane at the player's height. Today's tuning is for a camera 11-21 m up.
5. **Co-op.** Residents are not networked: each game runs its own copy from the mirrored save data. Orders that make
   people fight need live positions.
6. **The controller has no free button**, and on the keyboard **V is already bound twice** (see §1.3).
7. **Scope and timing.** On 2026-11-13 the plan was to validate the slice by playing it before widening the game. This
   would be the largest new system since then.

### Recommendation: "the Keeper's view"

The working name comes from the Heartroot's last word, "Hearth-keeper". Build it in phases, each worth having on its own:

- **Phase 0 (S-M):** groundwork. Settle the V clash, keep residents out of deep water, fewer stuck warps, and measure
  rendering at 35, 45 and 55 m to set the height cap.
- **Phase 1 (M-L): the view.** At home, H lifts the camera off your character into a free view with the Haven window
  docked at the side. You click buildings and people, queue crafts, order upgrades and place buildings from there.
  Nothing the game simulates changes, so there is no balance risk.
- **Phase 2 (M-L): living work.** Builders hammer scaffolds while you are home, and you can mark which scaffold comes
  next. Workers clear a gather ring you draw, the way Gord rings are drawn, and carry each load home.
- **Phase 3 (M): orders and defence.** Guard here, patrol with a lantern, follow me, back to your day, and a horn that
  sends everyone to their posts. Any resident can stand in a breach, and breach preparation happens in the view.
- **Phase 4 (L): your own character works.** You can leave your character a task, such as building, keeping watch or
  resting, and it walks there and does it while you look around.

Six changes to the idea make the result deeper and stop it from splitting the game further:

1. **One place to run the Haven.** At home, the H window and the view become one thing, instead of adding a third way
   to manage the Haven next to the window and the world.
2. **Orders lapse.** Every order goes back to the person's day when it is done or cleared, as in RimWorld and Necesse,
   so there is nothing to babysit.
3. **The hero keeps work nobody else can do.** Raids, talking and hand-ins, skilled fighting, the fastest hammering,
   gathering with the minigame, quality and XP, Fine crafts, fishing, horses and exploration all stay the avatar's.
   Residents get the plain basics.
4. **Preparation from above, the fight on foot.** A breach's warning and preparation happen in the view; when the
   defence starts, the camera dives back to your character.
5. **Patrols carry light.** A patrolling resident is a moving lantern that prowlers keep away from, so night orders mean
   something without becoming a chore.
6. **A person carries their own load home.** There are no hauler jobs and no per-item hauling, which is the colony-sim
   chore that produces most pathing bugs.

The nine open questions for you are in Part 6. My recommendation is to build Phases 0 and 1 now, and Phases 2-4 after you
have used the view, because Phase 2 onwards changes the economy and the pace.

---

## Part 1: What the code has today

### 1.1 What is physical and what is bookkeeping

- **11 residents with 13 jobs** (`Data/Prologue/Residents`, `Data/Settlement`). Every one of them also has a raid
  companion (Fighter, or Archer for Runa and Asa).
  - The §15 row of ARCHITECTURE's status table ("one recruit, one job") is out of date.
- **Movement:** a `NavMeshAgent` added at runtime (`ResidentLife.cs:117`), at 1.6 m/s walking and 3.8 m/s running,
  with low-quality avoidance. After 4 s stuck they warp (`:544`).
  - No area mask is set, so they may path through the deep-water area (inferred from the code, not tested).
- **Their day** (`ResidentRoutine.Decide`, `:74`): wake 6.5, work 8-18, meal 12-13, sleep 22, an outing about one day
  in three. Cold, festivals and danger change the plan.
  - Homes are assigned by `HavenHomes`, and they sleep indoors, hidden.
- **Animations:** walking plus emotes (Forging, Use, Fishing, Read, Sit, Sleep, Kneel, Talk) with a tool in the hand.
  - Residents have no chop, mine, carry or fight clips.
  - The player has MineWall, MineGround, Hammer and Farm (`Interaction/InteractionAnimation.cs:16-22`). Emotes are
    clip-based, so those can become resident emotes cheaply.
- **The resident prefab** (`Prefabs/Characters/Resident.prefab`) has no `Damageable` or `Combatant`: residents cannot be
  hurt.
  - In a breach, the Warden and the Hunter are swapped for a `Companion` stand-in (`ResidentDefender.Stand`, `:35`),
    who holds a post at the Watch Post or a 6 m ring round the portal. Everyone else shelters.
- **No needs or mood.** Home quality (Bare to Grand) and likings only add work speed. Worker ranks, bonds, memories and
  a paused-work flag exist.
- **Storage is one stash** (`StashStorage`, 60-slot blocks per chest). Every system reads the whole stash, and nobody
  fetches anything physically.

### 1.2 The camera, and everything that follows the player

- **Today's camera:** `Camera/PlayerCameraController.cs` is one Cinemachine camera. It follows a private "Camera Focus"
  transform (`:287-295`) that is moved each frame from the player plus cursor lean, shoulder and lead (`:618-661`).
  - The Exploration profile is pitch 50° at 14-28 m, and Combat is 58° at 12-20 m (`Data/Camera/PlayerCameraSettings.asset`).
  - `HavenLookout` pulls the widest view that exists today, about 56 m (`Settlement/HavenLookout.cs:149-153`).
  - There are no camera bounds and no terrain logic.
- **The precedent for a mode swap is `Cinematics/CinematicView.cs`.** It adds its own camera at priority 1000, hides the
  screen canvases, holds the player with `PlayerControl.Hold`, fades the fog of war (`AtmosphereFeature.WarScale`),
  and glides back on `End()`.
  - `PlayerCameraController.Watch(Transform)` (`:102`) is the other precedent; `CoopSpectate` uses it.
- **The catch:** `PlayerCameraController` writes global state every frame:
  - the terrain detail focus (`:381`);
  - near shadows (`:443`);
  - `CameraPointer.UsesScreenCenter` and the cursor lock (`:440`, `:464-482`);
  - the V style toggle (`:433`).

  A second camera needs an explicit "stand down" flag. Disabling the controller would null `PlayerCameraController.Current`,
  which about 15 files call.

What breaks or looks wrong when the camera is 50-200 m from the player's body:

| System | Where | With a far camera | Fix |
|---|---|---|---|
| Sun shadows | `Settings/Rendering/PC_RPAsset.asset:60` (50 m), `GraphicsOptions.cs:138,158` | No sun shadows beyond 50 m from the camera, so a high view loses them all | M, plus a GPU cost to measure |
| Grass and trees | SafeHaven terrain: detail 80 m, billboards from 50 m | The ground goes bald from about 80 m | M, plus a cost to measure |
| Fog of war / night veil | `Environment/Atmosphere/AtmosphereFeature.cs:601` | A flat plane at the player's height, so it shows as black bands over hills | S to fade (as cinematics do), M to lay it on `FogGround` |
| Fog thinning | `AtmosphereFeature.cs:438-442` | Clears fog along camera→player, not where you look | S |
| See-through | `Environment/SeeThrough.cs:52-55` | Cuts holes in roofs and trees along camera→player | S (`Active` flag) |
| Ground relief | `Environment/TerrainDetailFocus.cs`, `PlayerCameraController.cs:381` | Relief only 15-25 m round the player | S |
| Sky occlusion | `Rendering/SkyOcclusionMap.cs:204` (160 m area) | No roof or canopy shading beyond 80 m from the player | M (about 6 ms to redraw per 12 m panned) |
| Reflection probe | `Rendering/FollowReflectionProbe.cs:88` | Stays above the player | S |
| Hearing | `Audio/AudioManager.cs:493`, `PlacedAmbience.cs`, `AudioSpace.cs:72` | You hear what the body hears, not what you see | S (everything reads `EarPosition`) |
| Minimap | `UI/MinimapHud.cs:290,350` | Centred on the player | S, or M with click-to-pan |
| Hover outline | `Player/PlayerInteractionOutline.cs:15,55` | 80 m ray; hidden while any window is open | S |
| Painted-tree stand-ins | `Gathering/TerrainTreeFelling.cs:21,274` | Only within 10 m of the player, so a far painted tree can't be clicked | M (`StandingNear(point, d)` exists) |
| Player-relative spawns | `Level/MobSpawnZone.cs:325`, `NightProwlers.cs:117`, `HorseHerd.cs:257` | Spawn round the body; `MobSpawnZone` hides spawns by player distance only, so they can pop in in front of a far camera | S-M |
| Shake and off-screen warnings | `Camera/CameraShake.cs`, `Player/OffScreenWarnings.cs` | Blows at the far body shake the view; chevrons point at the body | S |
| Rain, air life, ripples | `RainEffect.cs:106`, `AmbientLife.cs:139`, `WaterInteractor.cs:181` | Already follow the camera, but are tuned for a 20-40 m view | S tuning |

### 1.3 Keys and the pad

- **V is bound twice on the keyboard:** `Camera/ToggleStyle` and `Player/StackOnStored` are both `<Keyboard>/v`
  (checked in `Input/InputSystem_Actions.inputactions`).
  - Inside the ward, one press both switches the camera style and stores the bag. This looks like an existing bug,
    separate from this feature, but I did not test it in Play.
- **Tab is taken** (`ToggleCombat`). **H opens the Haven window** (`Player/PlayerInputHandler.cs:286,396`), with tabs
  Overview, Stores, Buildings, People, Lands and Raids (`UI/HavenWindow.cs:46`).
- **Free keyboard keys:** Q, U, O, P, backquote, Caps Lock, F2-F4, F7-F12, Insert/Home/End/PgUp/PgDn/Delete.
- **Pad:** every button is used, including the tap/hold pairs on RB, LB and View. The natural way in is a choice on the
  windows wheel (`UI/CommandWheel.cs:188`).
- **A new action map fits cleanly.** `GameInput.SuspendGameplay` already turns off every map but UI, and the rebinding
  screen has a conflict-domain rule for build keys (`Core/Options/InputRebinds.cs:191,210`) that a view map can copy.
  - **Trap:** the window keys (G, H, B, J, M) live in the Player map. Holding the player turns them off, and
    `PlayerControl.Kept` only keeps Move, Interact and Dodge, so the view must route them itself.

### 1.4 Acting from afar

| Action | From afar today? | Notes |
|---|---|---|
| Order an upgrade | Yes, from the H window | `HavenBuildings.cs:134` → `Construction.TryOrderUpgrade` (`:278`) |
| Place a building | Yes | `BuildPlacer` rays from `Camera.main` to 600 m (`HomePortal.cs:159-170`); the pad aims from the player's position plus the stick, clamped to `BuildReach` (`BuildPlacer.cs:611-635`) |
| Give a job, pause work, see a person's page | Yes | `HavenPeople.cs:81/93/146`, `ResidentWindow.Show` |
| Queue a craft | The code can, the UI can't | `StationCrafting.TryQueue` (`Crafting/StationCrafting.cs:75`) has no distance check, and `MaterialPool` pays from the whole stash at home; only `StationWindow` closes past 6 m (`UI/StationWindow.cs:397`) |
| Collect a station's output | Into the player's bag | `StationCrafting.cs:177-192`: from afar this would be a teleport, so it should go to the stash instead |
| Talk, hand in, open a chest, ride | No | Distance checks in `DialogueBox`, `StashChest`, `PlayerInteractor` (`:143`) |

### 1.5 People, work and orders: the gap table

| Capability | Status | Reusable pieces | Missing | Effort |
|---|---|---|---|---|
| Harvest a chosen tree, rock or ring | Missing | `ResourceNode.GatherBy` (`:287`), `ChoppableTree.Roll` (`:229`), walking, the player's mining clips | Order state, ring pick, yield into the stash, painted trees without the player near, gather emotes | M for scene nodes; L with painted trees |
| Patrol | Missing | The `ResidentDefender` swap, Companion hold points, `MonsterPatrol` as a model | A patrol order cycling points, a save field, a decision | M |
| Defend a spot | Breach only | `KeepsGuard`/`GuardPost` (`ResidentLife.cs:227/252`), `Companion.Command(order, spot)` (`Companion.cs:201`) | A post you choose, outside breaches, any resident | S-M |
| Attack a target | Partial | `CompanionOrder.AttackTarget` | An explicit target setter: today it means "what the player last hit" (`CompanionLeader.cs:9-17`) | S on top of defend |
| Place, and residents build it | Partial | `Construction.AddWork` (`:450`), the Forging emote, builder jobs in `HomeLifeSettings` | Live builders at home, a "next" pin, the host-only rule | M |
| Queue crafting from afar | Mostly there | `StationCrafting.TryQueue`, `StationWindow` with a large range | An entry point in the view, collect into the stash | S |
| Start an upgrade from afar | There | `TryOrderUpgrade`, `ConstructionUpgradeOffer.Show` | Someone to build it while you are home (the row above) | S |
| The player's character works like a resident | Missing | `PlayerControl` holds, `WalkPartyNode.LeadPlayer` steering (`Story/StoryWorldNodes.cs:658-682`), `HavenTravel.SpotBeside` | NavMesh steering for the `CharacterController`, a `PlayerInteractor.Begin(Interactable)`, a bypass for "Close the open window" (`PlayerInteractor.cs:132-136`); the player is not a `ResidentRecord`, so they can't staff a station | L |

### 1.6 Co-op

- **Local per machine:** the camera, the UI, the fog and the minimap. Friends see a `RemotePlayerBody` driven by the
  local player's poses, so an AI-driven body replicates with no new code.
- **Decisions go through the host:** Haven decisions are `[HavenDecision("name")]` methods with string arguments sent
  through the existing ask/answer messages (`Multiplayer/CoopDecisions.cs:94,109`), so new orders need **no wire-format
  bump**. The wire format is 32.
  - Queuing and collecting are refused for visitors on purpose (`HavenVisit.LeftToHost`).
- **Residents are not networked:** every game simulates them from the settlement state, which is mirrored as save data
  every 2 s.
  - An order that is saved state reaches every game for free, and their residents walk to it the same way.
  - Live work (`AddWork`, yields) must run on the host only, or it is counted twice.
  - Fights need live positions: the horses' "one runner, puppets" pattern (`CoopHorses`), a new message and wire format 33.

### 1.7 Design rules any colony mode must keep

- **The ward and the GDD:**
  - Home is calm and untimed, and attacks are warned (GDD §1.4, §6.3).
  - "No needlessly elaborate life simulator" (§2.6). Well-being must not become a micromanagement chore (§15.4).
  - Essential production must not need idle guards, and automated defences must not trivialise breaches (§6.8).
  - "NPC activity should be visible so that hiring someone feels like changing the world" (§14.4). Today this is true
    of where people stand, not of what they produce.
- **Your rules:**
  - Every effect is positive: beds never gate anyone, nothing is lost, nobody dies in a breach.
  - Hand-ins happen in person, at home (2026-11-21).
  - Workplaces are learned through quests (`BuildingGate`, `JobsOpenTo`, `SettlementState.cs:913`).
  - No text walls in windows: pictures and names, words on hover.
  - Keep the keyboard and mouse layout identical when adding pad controls.
  - Keep default graphics quality; optimise rather than downgrade.
- **Economy:**
  - Cursed Essence comes only from raids.
  - Workers make Plain goods and train nobody's skill (`WorkerProduction.cs:151`, `Construction.FinishedByOthers`).
  - Defences act only in breaches, "so the game hunted by day at home stays the player's" (`WatchPostArcher.cs:4`).
- **Pacing:** a 45-90 minute session should fit several raids and real building (GDD §3.3). The Haven must not become
  a time sink.

---

## Part 2: How other games do it

### 2.1 Key takeaways

1. **Time.** Overhead views used for decisions under pressure pause or slow the game: RimWorld, Kenshi, Dragon Age:
   Inquisition, Bannerlord's slow-motion order mode. Every one of those is single-player. The co-op base games
   (Necesse, Palworld, Bellwright, Soulmask, Conan Exiles) run in real time. With four-player co-op, Safe Haven's view
   has to work in real time.
2. **What the avatar does while you look.** There are four tested answers:
   - **Anchored:** the avatar stays put while the build view hovers near it (Fallout 4).
   - **Asleep and vulnerable:** the body lies where you left it (Soulmask's possession; felt as a risk).
   - **Disembodied:** there is no avatar to worry about (Dungeon Keeper).
   - **One of the colonists:** the avatar runs on AI like everyone else (RimWorld undrafted, Kenshi, State of Decay 2).

   Your idea is the last one. It works when the avatar has real work and taking control back is instant.
3. **Orders scale with headcount.**
   - 1-3 followers get verbs: follow, stay, attack, get logs (Sons of the Forest's Kelvin, Conan, Bellwright).
   - 10-30 colonists get priorities and zones (Necesse, ASKA, RimWorld).
   - About 11 named people is small enough for orders to each person, if every order lapses back to the routine:
     RimWorld re-evaluates on undraft, and Necesse has "clear orders".
4. **What only the overview can do:** placing, ordering many people, seeing the whole base. Factorio (plans that robots
   build) and Dragon Quest Builders 2 (blueprints residents build from a stocked chest) show that "place it, others
   build it" works well.
5. **What only the hero can do.** The games that keep the hero meaningful give it work nobody else can do: Brütal
   Legend's own fighting, Bannerlord fighting while ordering, Minecraft Legends' hero as the only way to move the army.
   Soulmask shows the opposite.
6. **Gamepad.** What has worked on a pad:
   - snap selection, double-tap for all of a kind, and a select-all button (Halo Wars);
   - a small order wheel (Mass Effect);
   - a stick sweep for groups (Overlord II).

   Necesse needed a whole extra patch just for settlement tasks on a controller.

### 2.2 The closest models

| Game | How the switch works | The avatar meanwhile | Orders | Lesson for Safe Haven |
|---|---|---|---|---|
| **Fallout 4** (workshop) | Hold V, only in a settlement's build area | Stays put; the view is near it | Aim at a settler → Command → aim at a station → Assign | The same idea and key. Warning: clumsy menus, and the people have to matter |
| **RimWorld / Going Medieval** (drafting) | Draft or undraft a colonist | Colonists are all AI until drafted | Priorities, zones; drafted: move, attack; Going Medieval's Large Bell drafts many to a point | Orders are overrides that lapse; one "everyone" order for danger |
| **Kenshi** | No switch: a free camera always | Your character is one of the squad | Follow, bodyguard, attack; Shift queues jobs | The nearest to "your character becomes a colonist" |
| **Necesse** | Settlement panel (C) | Top-down hero, real time, co-op | Follow, guard a spot, clear orders; restrict zones, work zones, priorities | Same camera, scale and co-op as Safe Haven; plan the pad from the start |

### 2.3 Other games at a glance

| Game | What it does | What reviewers said |
|---|---|---|
| Manor Lords (Visit Mode) | The reverse: a city builder lets you walk your lord in third person, with no interaction or fighting | Presence only; shows the appeal of walking your own town |
| Dungeon Keeper (Possession) | Take one creature in first person | "Made you feel like the minion", but disorienting and of limited use |
| Dungeons 3 | Two maps, two control models | The dungeon half "quickly gets left behind" |
| Dragon Age: Inquisition (Tactical View) | T or zoom all the way out; pauses | Poorly joined to the action controls; little used; cut from Veilguard, which also had a "don't fight inside" rule because of the camera |
| Factorio | Place plans from the map; robots build | The clean precedent for "place from above, others build" |
| Bellwright | A hero, plus villagers run from menus (0-9 priorities), companion Move/Follow/Charge/Hold | Settlement depth praised; stuck NPCs, poor pathing, heavy menus |
| ASKA | One villager per station, pinned tasks, Barracks guards, a militia call-up | Players ask for less babysitting ("work until complete") |
| Soulmask | Possession of a tribesman, patrol checkpoints, a 1.0 management mode | Menus "unnecessarily complex"; followers make "the player character… optional" |
| Medieval Dynasty | A management tab: villager → building → what it makes | No orders and no overview; works because it stays small |
| Palworld | Pals take base work; an alarm bell switches to defence | Pathing, stuck pals, workarounds such as throwing pals at their stations |
| Conan Exiles | Thrall stances: guard me, guard area, attack all; patrols (2022) | Thralls idle in combat |
| Dragon Quest Builders 2 | Draw a blueprint, stock a chest, residents build it | Closest to Safe Haven's scaffolds |
| Cult of the Lamb | Dungeon runs, plus followers commanded in person | "Lacked depth… both as a roguelite… and a base manager" |
| State of Decay 2 | Control one survivor, swap to any; the rest live on AI | Swapping keeps everyone mattering |
| Brütal Legend | The hero fights, orders the army, flies up to command | A hidden RTS half; weak console control; "not enough confidence in any one piece" |
| Minecraft Legends | A hero with a banner; no overhead view by design | Simple on a pad, slow on PC; units ignore nearby enemies; content stopped after about 9 months |
| Savage / Natural Selection 2 | One commander per team in an RTS view | NS2's command seat is often empty: fighting is more fun |
| Mount & Blade II: Bannerlord | Orders from the avatar while fighting; slow-motion order mode | Commanding while fighting keeps the hero central |
| Halo Wars | A console RTS | Tap, double-tap and hold A for selection; fixed build slots |

### 2.4 What reviews say goes wrong

- **The two-half-games problem.** Players compare each half with its dedicated genre. Gord "fails to capture the appeal
  of any of its component genres". Minecraft Legends was called "shallow".
- **Modes starving each other.** A mode that is optional and weaker goes unused (Dragon Age: Inquisition, NS2's
  commander seat). A mode that is too strong makes the other pointless (Soulmask).
- **Watching from above exposes the AI.** Stuck, idle and wandering NPCs come up in nearly every review above.
- **Menus multiply.** Fallout 4's workshop, Soulmask, Gord and Bellwright were all criticised for their menus.
- **Control.** Brütal Legend's targeting on a console, and Dragon Age: Inquisition's camera indoors.

---

## Part 3: Is it a good idea?

### 3.1 What it fixes

1. **The split you feel.** It gives the residents a visible part in the economy (Phase 2), so managing and walking
   happen in the same world, about the same people. That is Pillar D: "the recruited smith should inhabit the forge".
2. **Errands that aren't play.** Today every craft queue means walking to within 6 m of the station. The view turns a
   lap of the Haven into a minute of clicks, and leaves home time for what is play: building, talking, fishing, horses,
   getting ready for a raid.
3. **Layout you can read.** The 2026-11-15 town work (yards, paths, extensions, homes) made placement matter. From above
   you can see the town you are making, the way Gord shows it.
4. **Breach preparation gets its missing verb.** GDD §6.7 lists "assign defenders" in the preparation step; nothing does
   that yet. From above, preparation becomes planning: where the lanes come in, where the guards stand, what needs
   mending.
5. **It suits the look.** The Haven's atmosphere is already modelled on Gord, the camera is already top-down (a short
   step up, not a jump from first person as in Fallout 4), and the Lookout already pulls back to about 56 m.

### 3.2 What works against it, and the answer to each

| Risk | How bad | Answer |
|---|---|---|
| Two half-games | High if it becomes an RTS | It is not a second game: it is the H window laid on the world. No box-selected armies, no unit production, no economy you only see from above |
| The hero becomes optional at home (Soulmask) | High if residents gather well | Residents get plain basics, slower than you, with stock targets that stop them. Quality, rare finds, XP, Fine crafts, fighting and the fastest building stay yours (§4.13) |
| Stuck NPCs seen from above | Certain | Phase 0 fixes the deep-water mask and stuck warps, and checks how often residents warp, before the view ships |
| Rendering at height | Medium-high, unmeasured | Measure first (Phase 0). Start with a lower cap (about 50 m) and raise the shadow and grass distances in the view rather than lowering quality |
| Co-op | Medium | Orders are saved settlement state: mirrored for free. Only fights need live positions, and those come in Phase 3 |
| Controller | Medium | Designed in Phase 1: a windows-wheel entry, a snap cursor, an order wheel |
| Busywork (§2.6, §15.4) | Medium | Orders lapse. Nothing gets worse if you never open the view: the Haven keeps running on routines as now |
| Endless home time | Low-medium | Real time, no fast-forward. The view saves walking; it doesn't add chores |
| Scope before a playtest | Medium | Phases 0-1 change no numbers. Phases 2-4 wait for you to have used the view |
| Exploits | Low-medium | No Cursed Essence or XP from residents. Your character earns XP on autopilot only for bounded work (hammering scaffolds you paid for), never for gathering |

### 3.3 Verdict

**It will make the Haven better, with one condition: Phase 2 has to follow Phase 1.** Phase 1 alone is a real
convenience and a clearer view of the town, but it does not answer "two directions"; it makes the menu half faster.
Phase 2 is what makes the people the base, and Phases 3 and 4 make it deeper. Not doing it leaves the Haven where it is:
a good-looking village whose life is scenery, managed from a list.

---

## Part 4: The design: the Keeper's view

### 4.1 Principles

1. **The same game, from further up.** The camera rises from your character and comes back down to it. Same light, same
   world, no second rule set.
2. **The view runs the Haven; the avatar lives in it.** Admin goes up, life stays down (§4.13).
3. **Nothing needs the view.** Routines, the H window lists and walking to a station all keep working.
4. **Orders are overrides that lapse.** Everyone goes back to their day.
5. **Real time.** The same in solo and co-op.
6. **Pictures, not text walls.** Cards with paintings and names, words on hover, the Hearthline kit.
7. **Haven only.** Never in raids, the prologue or a breach's defence.

### 4.2 Getting in and out

- **Way in:** at home, **H lifts the camera** off your character into the view, with the Haven window docked as a side
  rail (its tabs become the rail's sections).
  - In raids, H opens the window as today.
  - The entry is its own rebindable action, so a player who wants another key can have it.
  - Pad: the windows wheel (hold View) gets "Keeper's view".
- **Way out:** H, Esc or B again, or click your own character's portrait. The camera glides down to wherever your
  character now stands.
- **Can't enter** during raids, the prologue, a breach's defence, a dialogue or cinematic, while swimming or riding.
  Entering cancels a work interaction in progress until Phase 4 can carry it on.
- **Thrown out (the camera dives to you, with a horn sound) when:**
  - a breach's defence begins;
  - your character is hurt or hunted (`MonsterAI.AnyHuntingPlayerSide`);
  - corruption-night creatures reach the ward;
  - a cinematic starts or the scene changes.

### 4.3 The camera

- **Position:** it starts above your character at about 30 m and zooms from 20 m to a cap set by Phase 0's measurements
  (about 50 m to start). It tilts steeper as it rises, about 50° to 65°, as Gord does.
- **Controls:**
  - pan: WASD or arrows, the screen edge (optional) and middle-drag, faster when zoomed out;
  - rotate: Q and E (a view-only map, so the Player map's keys don't change);
  - zoom: the wheel.
  - Left-click selects, right-click gives the context order (§4.6), F follows the selected person, Space finds your
    character.
- **Bounds and ground:** Haven Meadows (`HomePortal.InBuildLand`) plus a margin. Height follows the terrain, smoothed so
  the knolls don't clip.
- **Flying to a place:** clicking a building or person in the rail flies the camera there instead of moving your
  character (`HavenTravel` stays on the rail as "go there yourself").
- **Built from:** a second `CinemachineCamera` at priority about 500, below cinematics, so the brain's blend does the
  transition. `PlayerCameraController` gets a "stand down" flag so it stops writing the focus, shadows, cursor lock and
  screen-centre aim while the view is up.

### 4.4 What the view shows

- **People:**
  - a small name plate with an activity sign (hammer, pot, bed, spear, lantern, axe) and their portrait on hover;
  - people indoors show on their house ("Ingrid is home");
  - the selected person gets an outline (`OutlineCopies`) and a dotted line to where they are going.
- **Buildings:**
  - on hover, an outline and a card: painting, tier, queue progress, an output-ready badge, who works there;
  - scaffolds show their progress and their builders.
- **Along the top:**
  - stores as picture cards with counts (from the Stores tab);
  - the hour and the weather;
  - the portal's next tier (`WantedAtHome`).
- **On the ground:**
  - the ward ring and the edge of the build land;
  - gather rings and guard posts;
  - when a breach is warned, its lanes (`BreachLanes`).
- **Night:**
  - lit ground stays readable and the dark beyond stays dark, so the night's danger still reads from above;
  - the flat fog-of-war plane fades, as in cinematics, until it can lie on the ground.

### 4.5 What you can do from above

- **Buildings:**
  - open their card to queue, cancel and collect, with output going **into the stash**, not your bag;
  - upgrade, fit modules, move, mend;
  - staff them (give the job).
- **Build:** G and placement work as today; on a pad the cursor places instead of an offset from your character. A
  scaffold can be pinned "next" (§4.8).
- **People:** their page, job, home, pause work, orders (§4.6).
- **Not from above:**
  - talking, quests and hand-ins (people are met, not managed; and your rule says hand-ins happen in person);
  - chests and bag moves (taking things into your bag from afar would be a teleport);
  - taming and riding, gathering, fishing, eating, the sauna.

### 4.6 Orders

There are six verbs and a horn, for about 11 named people. Right-clicking gives the obvious order for what is under the
cursor: a scaffold means Build, a tree or rock means Gather, open ground at night means Guard. A small wheel offers the
rest.

| Order | What they do | Lasts | When |
|---|---|---|---|
| **Work here** | Their job at this workplace (exists today) | Until changed | Always |
| **Build this** | Walk to the scaffold and hammer, then back to their day | Until it stands | Work hours |
| **Gather here** | In a ring you draw (like the Gord-style rings for walls): fell object trees, break rocks, pick bushes; each load is carried home; stops at the stock target | Standing, in work hours | Day |
| **Guard here** | Stand at a lit spot; fight what comes in reach | Standing, at night; they sleep late the next morning | Night, breach preparation |
| **Patrol** | Walk two to four points carrying a lantern, a moving light the prowlers keep out of | Standing, at night | Night |
| **Follow me** | Walk with your character as a companion (the existing companion swap) | Until cleared | At home |
| **Back to your day** | Clears any order | | |
| **The horn** | Guards to their posts, everyone else to shelter, early (today's breach behaviour, on your call) | Until the danger passes | A breach is warned, or a corruption night |

- **Who can take an order:**
  - anyone can Build and Gather, faster with the right job;
  - anyone can Guard, Patrol and Follow, because everyone has a raid companion (Fighter or Archer);
  - rescued people with no job yet can take orders too, which gives early residents something to do.
- **Attack:** only against breach attackers and corruption-night creatures, by fighters on guard or following you. There
  are no attack orders on the Haven's game animals: day hunting stays yours, as `WatchPostArcher` already rules.
- **Meals and sleep still come first,** except for night guards, who shift their sleep rather than lose it. There are no
  needs to watch.

### 4.7 Your own character while you look

Your idea was that your character "moves along in the base like other NPCs". There are three ways to do that:

| Option | What it is | Cost | Notes |
|---|---|---|---|
| A. Stays where it stands (Fallout 4) | Held and still; leaving the view returns to it | S | Safe inside the lit ward; the view throws you out if it is hunted |
| B. **A Keeper's task** | Before you look around, pick a task: **build** a scaffold (at your own build speed, the fastest builder), **keep watch** at a post, or **rest** at home; it walks there and does it | L | Uses your skills, so it is worth leaving your character there; Construction XP is bounded by paid scaffolds |
| C. A full routine like a resident | A schedule, meals, a job | L+ | The player isn't a `ResidentRecord`; your character would be the least interesting person in the Haven |

**Recommended: A in Phase 1 and B in Phase 4.** "Work like a resident" is best read as "do the thing you are best at
while you look around", which is building. No autopilot gathering for your character, because that would be unbounded
XP and materials.

### 4.8 Living work: what the view has to watch

- **Builders at home.** Carpenter and Mason, plus anyone ordered, walk to scaffolds in work hours and hammer with the
  Forging emote. They call `Construction.AddWork` on the host only, at 0.5 a second against your 1 + Build Speed. They
  take a pinned scaffold first, then the oldest. Banking builder-seconds while you are away stays as it is.
- **Gather rings.** The Woodcutter's Lodge and Workshop jobs can be given a ring. The yield goes into the stash through
  a new path next to `ResourceNode.GatherBy`.
  - Object trees, rocks and bushes come first; painted terrain trees (about 130,000) come later, once there is a
    fell-by-index call that doesn't need the player within 10 m.
  - The workplace's tier stands in for a tool tier.
  - Yields are Plain, with no Hardwood or Elderwood chance, no XP and no Cursed Essence.
  - Clearing the Haven's overgrowth is a natural first use, since those nodes never regrow.
- **Carrying.** A gatherer carries a bundle prop back to their workplace's yard, and the stash goes up when they arrive.
  That is honest hauling without hauler jobs.
- **Pace.** Ring work is slower than yours and stops at the job's stock target (`WorkOrder` already has one). The
  `Docs/BaseProgression.md` numbers need a fresh check once residents add wood and stone.
- **Trap: quests.** Residents' yields must not count towards "gather X" quest objectives meant for you. Check which
  event those objectives listen to.

### 4.9 Defence: preparation from above, the fight on foot

- **Warning and preparation:** in the view you see the lanes, mend and place walls and traps, set guard posts, choose who
  stands, and sound the horn.
  - Any resident with a raid companion can stand, not only the Warden and the Hunter: `ResidentDefender` already does
    the swap.
  - Nobody dies: guards get up by themselves, as now.
- **Defence:** the camera dives to your character, and the fight is yours, with the same combat as raids (GDD §6.7).
  - A later step could add quick orders on the action wheel during the fight (to me, hold here, fall back), in the
    style of Bannerlord or Mass Effect.
- **Recovery:** back in the view to see the damage and mend it.
- **Breach rules unchanged:** half the attackers still go for the ward and half for buildings. Guards help but must not
  win alone (§6.8), so tune their numbers against a breach you don't fight.

### 4.10 Co-op

- **Who can use the view:** anyone; it is local.
  - Friends see your character where you left it (or at its task), with a small "keeping" mark over it.
- **How orders travel:**
  - Orders are `HavenDecision`s with string arguments, so no wire bump. A guest's order is an ask to the host, as
    queues and builds are today.
  - Orders are saved settlement state, mirrored every 2 s, so every game's residents walk to the same post or ring.
  - Work (`AddWork`, yields) is counted on the host only.
- **Live positions:** fights (guards meeting creatures, follow-me) need the horses' "one runner, puppets" pattern: a
  new message and wire format 33, with `CoopWireTests` fingerprints. That is Phase 3.

### 4.11 Controller

- **Way in:** the windows wheel's "Keeper's view".
- **In the view:**

| Control | Does |
|---|---|
| Left stick | Pan |
| Right stick | Rotate (sideways) and zoom (up and down) |
| A | Select or confirm; the cursor snaps to the nearest person or building under it, as in Halo Wars |
| Double-tap A | Select everyone working there |
| X | The order wheel for whoever is selected |
| Y | Find your character |
| LB / RB | Step through the rail's sections (`PadTabs`) |
| B or View | Back, then out |

- **Building:** the pad cursor places, instead of an offset from your character.
- **Keyboard:** the existing keyboard layout stays identical. All view keys live in their own map.

### 4.12 Rendering at height: measure first

- **Shadows:** sun shadows end 50 m from the camera. From 45-55 m up, the far half of the view loses them. In the view,
  lengthen the shadow distance (about 90-110 m, with the cascades split for it) and measure. If it costs too much, lower
  the height cap rather than the quality.
- **Grass:** it ends at 80 m. The view needs it out to about 110 m, or the meadows read bald from above. Measure.
- **Things that should follow the view's focus, not the body:** fog of war (fade first), see-through (off), ground
  relief, sky occlusion, the reflection probe and hearing (`EarPosition`), with a softer, wider mix from above.
- **Spawns:** check that everything spawning relative to the player also hides from the camera. Prowlers already do;
  `MobSpawnZone` doesn't yet.
- **Tests:** earlier Haven performance work found the frame goes to the tessellated terrain passes and tree shadows,
  so that is where a higher camera will cost. Measure the same way at 35, 45 and 55 m.

### 4.13 What stays the avatar's

- All raids.
- Talking, quests, rescues and hand-ins.
- Fighting with skill, at breaches and at night.
- The fastest hammering.
- Gathering with the timing minigame, tool tiers, Hardwood and Elderwood, burst yields and XP.
- Fine-quality crafts.
- Fishing, horses, hunting, lookouts and finds.
- Meals, the sauna and Well Rested.

Residents get the plain basics. This list is what keeps the hero from becoming optional.

---

## Part 5: Phased plan

| Phase | What | Effort | What it gives | Risk | Co-op | How to verify |
|---|---|---|---|---|---|---|
| **0. Groundwork** | Settle the V clash; deep-water area mask for residents; count and cut stuck warps; measure frame time, shadows and grass at 35/45/55 m | S-M | A height cap and clean NPC movement before anyone watches it | Low | None | `InputActionsTests`; a Play probe counting warps over a game day; frame-time captures |
| **1. The view** | Camera rig plus stand-down flag; presentation switches (§4.12); a view input map plus rebinds; H at home → the view with the docked rail; hover and select past 80 m; remote station card (collect → stash); pad cursor placement; entry and exit rules; controller layout | M-L | Run the Haven from above; read the town | Low: no numbers change | Local only; existing decisions | `InputActionsTests` for the new map; `ConventionTests` (holds, statics); a Play probe: enter, queue, upgrade, place, exit; the UI overlap audit; a breach start throws you out |
| **2. Living work** | Live builders at home plus a "next" pin; gather rings with stock targets; carry walks; gather emotes from your clips | M-L | People visibly build and gather what you ask | Medium: the economy and pace | Orders in the mirrored state; work on the host | A Play probe: a ring empties into the stash at the expected rate; scaffolds finish with nobody hammering; a pace check against BaseProgression |
| **3. Orders and defence** | Guard, Patrol (lantern), Follow, Back to your day, the horn; any resident can stand; breach preparation in the view | M | Planning and defending as a village | Medium: breach balance | One runner, puppets; wire format 33 | Breach probe with guards and no player; `CoopWireTests`/`CoopMessageTests`; the two-copy co-op script |
| **4. The Keeper's task** | Autopilot for the CharacterController (NavMesh path plus steering); `PlayerInteractor.Begin(Interactable)`; tasks: build, keep watch, rest | L | Your character works while you look around | Medium: player state edge cases | `RemotePlayerBody` already replicates | Play probe: leave a build task, the scaffold rises at your rate, the exit lands you where it stands; hurt while on a task → thrown out |

Effort is S/M/L per the audits, not a measured estimate. Each phase ships on its own and leaves the game working if the
next one never comes.

---

## Part 6: Questions for you

Recommended answers first.

1. **The key.**
   - **Recommended: H at home becomes the view**, with the window docked: one place to run the Haven.
   - A separate key (backquote or Caps Lock), with H unchanged.
   - V, moving the camera style and Stack On Stored elsewhere.
2. **Time.**
   - **Recommended: real time always.**
   - Allow a pause in solo play.
3. **Your character while you look.**
   - **Recommended: it stays safe where it stands (Phase 1), then a Keeper's task (Phase 4).**
   - It walks a routine like a resident (your original idea).
   - It is hidden.
4. **Residents gather physically.**
   - **Recommended: yes, in rings, plain basics, stopping at stock targets.**
   - No, production stays bookkeeping.
5. **Hauling.**
   - **Recommended: a gatherer carries their own load home, no hauler jobs.**
   - Full hauling of every item.
6. **Night orders.**
   - **Recommended: guard posts and lantern patrols at night, and the horn.**
   - Orders only during breaches.
7. **Attack orders.**
   - **Recommended: only on breach and corruption-night creatures.**
   - Also on game animals (hunting).
8. **The name.**
   - **Recommended: "the Keeper's view"**, after the Heartroot's "Hearth-keeper".
   - "Overseer view", "Haven view", or yours.
9. **When.**
   - **Recommended: Phases 0 and 1 now; Phases 2-4 after you have used the view.**
   - All four now.
   - After the full slice playtest from the 2026-11-13 review.

---

## Sources

Web research, 2026-11-21. Reviews and forum threads are labelled as such in the text where they carry the claim.

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
- Sons of the Forest: https://www.corrosionhour.com/sons-of-the-forest-kelvin-companion-guide/ ·
  https://steamcommunity.com/app/1326470/discussions/0/3836549485420025961 (forum)
- Dragon Quest Builders 2: https://gematsu.com/2018/12/dragon-quest-builders-2-details-endless-battle-island-setting ·
  https://primagames.com/eguides/dragon-quest-builders-eguide/builder-basics/building-your-base
- Cult of the Lamb: https://twinfinite.net/guides/how-to-command-followers-in-cult-of-the-lamb/ ·
  https://www.escapistmagazine.com/?p=124330
- State of Decay 2: https://www.windowscentral.com/state-of-decay-2-review
- Brütal Legend: https://www.gamedeveloper.com/pc/postmortem-behind-the-scenes-of-i-brutal-legend-i- ·
  https://en.wikipedia.org/wiki/Br%C3%BCtal_Legend · https://gamingbolt.com/brutal-legend-suffered-because-of-poor-messaging-says-tim-schafer
- Minecraft Legends: https://pcgamer.com/minecraft-legends-brings-down-the-tempo-of-typical-strategy-games-mojang-says ·
  https://pcgamer.com/minecraft-legends-review · https://gamespot.com/articles/minecraft-legends-development-is-ending/1100-6520230/
- Savage / Natural Selection 2: https://en.wikipedia.org/wiki/Savage:_The_Battle_for_Newerth ·
  https://forums.unknownworlds.com/discussion/149300 (forum) · https://unknownworlds.com/ns2/?p=2697
- Mount & Blade II: Bannerlord: https://www.gamerguides.com/mount-and-blade-ii-bannerlord/guide/introduction/useful-gameplay-tips/tactics ·
  https://www.bisecthosting.com/ru-ru/blog/mount-blade-bannerlord-1-3-update-patch-notes-release-date-beta (secondary) ·
  https://github.com/Norbivar/RTSCamera (mod)
- The Riftbreaker: https://wccftech.com/review/the-riftbreaker-an-ephemeral-paradise/
- Kingdom Two Crowns: https://www.gamerevolution.com/originals/472959-kingdom-two-crowns-co-op
- Halo Wars: https://waywardstrategy.com/2020/03/23/halo-wars-the-ultimate-design-for-console-rts/
- Overlord II: https://www.gamespot.com/articles/overlord-ii-updated-hands-on/1100-6210175/
- Mass Effect: https://strategywiki.org/wiki/Mass_Effect_3/Controls

Not verified: whether Fallout 4's workshop mode pauses; what Soulmask 1.0's management mode looks like (a menu or a
camera); V Rising real-time servant orders.

---

## Appendix: key file references

- **Camera:** `Camera/PlayerCameraController.cs` (focus `:287-295`, `:618-661`; globals `:381`, `:433`, `:440-482`;
  `Watch` `:102`), `Cinematics/CinematicView.cs` (`:64-121`, `:164-200`, `:215`, `:260`), `Settlement/HavenLookout.cs:149-153`.
- **Input:** `Input/InputSystem_Actions.inputactions` (V on `Camera/ToggleStyle` and `Player/StackOnStored`),
  `Core/GameInput.cs` (`SetPlayerKeys` `:203`, `SuspendGameplay` `:136-165`), `Player/PlayerInputHandler.cs:262-298,396`,
  `Core/Options/InputRebinds.cs:191,210`, `UI/CommandWheel.cs:188`, `UI/PadCursor.cs`.
- **Player control:** `Player/PlayerControl.cs`, `Player/PlayerMovement.cs:12`, `Player/PlayerInteractor.cs:132-143,255-279`,
  `Story/StoryWorldNodes.cs:658-682`, `Settlement/HavenTravel.cs:15-46`.
- **Remote actions:** `Crafting/StationCrafting.cs:75-192`, `Crafting/MaterialPool.cs:36-47`, `UI/StationWindow.cs:180,397`,
  `Settlement/HavenBuildings.cs:134,239`, `Settlement/HavenPeople.cs:81-146`, `UI/HavenWindow.cs:46`.
- **Building:** `Building/BuildPlacer.cs:480,611-635`, `Settlement/HomePortal.cs:159-170`, `Building/Construction.cs:139,278,388-450`.
- **People and work:** `Settlement/ResidentLife.cs:117,227,252,544,587`, `Settlement/ResidentRoutine.cs:74`,
  `Settlement/ResidentDefender.cs:35`, `Settlement/WorkerProduction.cs:13,135-160`, `Settlement/ResidentBuilders.cs:125-204`,
  `Settlement/SettlementSystem.cs:60-65`, `Settlement/JobDefinition.cs:25,75`.
- **Orders:** `Companions/CompanionOrder.cs`, `Companions/CompanionTactics.cs:172`, `Companions/Companion.cs:201,261,591`,
  `Companions/CompanionLeader.cs:9-17`, `AI/MonsterPatrol.cs`.
- **Gathering:** `ResourceInventory/ResourceNode.cs:287,301`, `Gathering/ChoppableTree.cs:229`,
  `Gathering/TerrainTreeFelling.cs:21,111,146,267-304`, `Gathering/NodeStreaming.cs:21,253`.
- **Co-op:** `Settlement/HavenDecisions.cs:63`, `Multiplayer/CoopDecisions.cs:94,109`, `Multiplayer/MultiplayerSettings.cs:85`
  (wire format 32), `CoopHorses` (the one-runner pattern).
- **Rendering:** `Settings/Rendering/PC_RPAsset.asset:60`, `Rendering/SkyOcclusionMap.cs:204`,
  `Environment/Atmosphere/AtmosphereFeature.cs:438-442,601`, `Environment/SeeThrough.cs:41-55`, `Audio/AudioManager.cs:493`.

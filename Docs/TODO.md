# Safe Haven – TODO

Open work and ideas. Tick items off (`[x]`) or delete them when done. Architecture notes for each system live in
[ARCHITECTURE.md](../Assets/_Project/Scripts/ARCHITECTURE.md).

## Second playtest list (2026-11-08)

In the user's order. Fishing first, then the rest.
- [x] Fishing: the fishing animations in use, and a fight in the manner of realistic fishing games, made casual: reel or
      give line by how hard the fish fights, lean the rod against its runs, it tires and is landed; the view comes down
      close, keeping the player and the float in view (`FishFight`, `PlayerFishing`, `PlayerCameraController.BeginCloseUp`).
      A fish brought onto the shore is caught there.
- [x] The fire's warm blur (heat shimmer) is far too strong. The haze drew a half-resolution copy of the scene at full
      opacity above every fire: a third of the ripple now (Strength 0.004), half blended in (`HeatHaze.shader`, Opacity).
- [x] Prologue: the troll walks far too fast towards the camp; it should stop at the camp, not go on. The NPCs run to the
      portal instead of staying to fight. It walks at a third of its speed (Creature Hunts › Walk Speed) and stays within
      10 m of the camp (Stay Within: `MonsterAI.HoldsHome`, `LeashOverride`); Reach Portal's companions Flee to the portal.
- [x] Prologue: Ingrid heals the player when their health is low (alchemists, so Sigrid too: at half health, 35 % back, a
      spoken aside, every 10 s at most).
- [x] Quests: no camera moves and no dialogue boxes before and after fights; a spoken comment from someone is enough.
      A story with a fight node speaks in asides and doesn't turn the camera (`StoryScript.IsFight`); the camp and portal
      warnings are asides.
- [x] Start camera: the map-edge limit (`ViewBounds`) overrides it. Remove the limit; bring back the invisible walls that
      keep the player on the map (the map will be extended so its edge can't be seen). `ViewBounds` is gone; the prologue's
      Boundary walls were still there (12 to 14 m inside the terrain on every side).
- [x] More than 30 arrows: let the stack (or quiver) hold more. Arrows stack to 99 (were 50); the bow comes with 60 in the
      prologue (was 30: the playtest ran out).
- [x] Light: a light slot in the Character window, switched with C. Instead of a torch in hand, a lantern on the belt, so
      both hands are free. Read as: the Light slot is in the Character window (C opens it); L (the light key) lights the
      lantern, as C already opens that window. The prologue hands out the lantern instead of the torch.
- [x] New item, Primitive Firefly Lantern: 6 sticks and 1 firefly, made from the inventory; a pale green, flickering glow,
      as of fireflies inside; light for 24 in-game hours; refilled in the inventory with a firefly.
- [x] Fireflies can be caught with E when close.
- [x] Fireflies gather round lights and now and then come near the player, so one can be caught. Slightly less prominent,
      but not rare.

## Third playtest list (2026-11-08)

After the second list, in this order.
- [x] A stream interactable is still near the Haven, though the stream was moved. The old stream's Drinking Spot,
      kept by the Haven rework, stood on dry ground at (44, 6); removed (any drinkable water serves).
- [x] The portal: visit a friend's Haven or invite a friend to ours, so co-op is part of the lore and the world's things.
      Its menu (once awake): Call friends to Safe Haven, Walk to a friend's Haven (friends whose door is open, from Steam),
      Call another friend / Close the way while hosting, Walk home while visiting. Still to try in a Steam build.
- [x] The things in the base to clear have a flat square round them, like a see-through square on the ground. Remove it.
      It was Overgrowth's patch of dead ground, a flat quad 3-6 m wide made under every piece; gone (the green motes on
      clearing stay).
- [x] The highlight sometimes crosses inside an object instead of following only its outline. The stencil mask was depth
      tested against an object drawn moved (wind, tessellation) and had holes; now Always, and only LOD0 is copied.
      Also a tree's cut-out leaf cards draw no outline (each card's rectangle traced lines through the crown); they still mask.
- [x] Small trees can't be chopped, only broken down to sticks. Below 6 m: "Break down" by hand, 2-3 Sticks and a
      Cedar Sapling (Young Tree node, small tree proxy).
- [x] Trees grow back in the Haven when a save is loaded: they shouldn't. A felled tree gives a sapling, which can be
      planted and grows back (about 5 in-game days; suggest a better pace). Cheap: saved state of each planted tree and its
      growth, no per-frame distance work; must not clash with the interactable tree optimiser. Done: felled trees stay
      down at home (they came back after 15 minutes); felling or breaking down gives a Cedar Sapling; planted at home it
      grows in 3 in-game days (72 minutes at the usual clock: 5 days would be 2 hours of play), as a painted tree of the
      terrain's own, so the felling stand-ins (the optimiser) work on it unchanged; growth set every 4 s from the day.
- [x] Small trees and bushes bend for the player as the grass does; some details don't yet. The grass's detail plants
      all bend already (proper bend weights); the painted shrubs, saplings and young cedars (Tree Wind) now push aside
      too, fully under 4 m, fading out by 9 m. Not seen clearly in a screenshot: check in play.
- [x] Too many sounds from finished quests and skill levels early on: make the first skill levels a little slower and the
      sounds a little quieter. The first six levels take 80 each (were 50 to 80; level 10 at 780, was 685); Quest
      Completed 1 to 0.6, Quest Started 1 to 0.45, Level Up 0.75 to 0.45, Milestone 0.9 to 0.7.
- [x] Debris falls off trees from time to time: in clear weather it should fall nearly straight down, and drift with the
      wind when breezy or stormy. Calm (tree wind up to 0.9: clear, mist, fog): half as many, no sideways push, a little
      turning; breezy and stormy carry them harder (2.2x as many fell in clear weather before).
- [ ] Quest "They are still out there": how to reactivate the portal isn't said. Meanwhile building quests pile up. New
      order: quests that teach the mechanics first (a chest for safe storage, gear to gather for it, a basic weapon), then
      opening the portal for the first raid. Saving the first NPC unlocks the building they work at.
- [ ] An NPC, Hild, appeared in the Haven with no explanation. NPCs don't just appear: each is saved on a raid, in a designed
      order that unlocks buildings as the player needs them, each one solving a problem. The user's outline: good gear and
      repairing it; refining (wood to planks); better tools; moving goods (wheelbarrows, boats); copper (stronger armour, tools,
      weapons). Research how similar games bring in a problem and its answer through their mechanics, and suggest the flow.

## Prologue playtest (2026-11-08)

From the user's run through the prologue in a build. Build work is paused until these are done.
- [x] Main menu: the buttons and notes sit at the far edges on a super-ultrawide screen. Keep the layout within 16:9, centred
      (`UIAspectLimit`), the picture still full width.
- [x] Dialogue blurred the whole screen: the backdrop took its high canvas for a full-screen window. Dialogue is left out
      (`UIBackdrop.Dims`). Check in a conversation.
- [x] Crafting tabs (All, Tools, Gear...) don't answer the pointer: brighten under it (`UITabHover`). Check at a station.
- [x] Woodcutting minigame doesn't work: holding doesn't move the axe, and tapping fast chops fast. Wanted: hold to raise the
      axe or pick gradually with the animation; at the top a glimmer and a sound; let go there for a critical; after a
      blow, a ready stance until the next hold. Found: only the mining states use the WorkSpeed raise; a release at any point
      swings at once.
- [x] Water reflects a plain light blue, no clouds, and the sun shows as a big yellow ball in it. The sky's sun disc is
      softened (the water draws its own glint); check the clouds in the reflection. Found: realtime
      reflection probes were off in the PC quality level, so the follow probe never rendered.
- [x] Fishing line doesn't start at the rod: it came from a fixed point by the shoulder. Now the held rod's tip
      (`FishingLine.RodTip`). Fishing clips exist in the
      player controller (Fish Begin/Cast/Wait/Fight/Catch/Haul) but PlayerFishing never plays them: still to hook up.
- [x] Prologue boar too tough: one boar, a regular one (not the blight boar), and the hunt asks for one. `Boar`
      (70 health, 2 armour, 6 damage; BuildPrologueBoar); the prefab's Combatant takes BoarStats.
- [ ] Archery: the aim doesn't hold on an enemy when the mouse is held over it. Not reproduced in the editor (2026-11-08):
      a ray anywhere over the body of a Boar, Small Moss Troll or Rootling picks it (`PlayerArchery.ChooseTarget`), and it
      held on every frame while drawing, loosing and being charged, through the real mouse path too; the outline sits on
      it. Need from the user: what fails (no red outline, outline flickers, arrows miss an outlined target), which creature.
- [x] The portal's sleeping stones lost the model's own textures (2026-11-03, `BuildPortalHeart.SleepingStones` painted them
      flat Base Stone): flat, blue at night, low-detail. `BuildPortalHeart.RestoreSleepingStones` gives them back.

## Faster builds (2026-11-08)

Every build re-imports all textures at full size and then back to the Editor's 2K cap (`EditorTextureCap`), twice per
build; with the forest and mountain packs' 4K and 8K textures that is most of a build's time. Agreed to wait; in order:
- [ ] Build from a second checkout (a git worktree with its own Library): textures import full-size once and stay, later
      builds redo only what changed, the Editor keeps its cap, and the build runs from the command line while the Editor works.
- [ ] Take the test scenes out of the build list: `SampleScene`, `GameplayTestScene`, `Demo/StoryDemo`.
- [ ] Or, instead of the second checkout: Unity Accelerator (local import cache; the project has caching off), so flipping the
      cap reuses both imports.
- [ ] Faster texture compression for development builds only (release keeps full quality).
- [ ] Script-only development builds when only code changed (`BuildOptions.BuildScriptsOnly`).
- [ ] Shader compile time. Measured from the 2026-11-08 development build's log: about 8,100 variants compiled (19,500 from
      the cache) in 44.7 CPU-hours, nearly all in a few slow shaders: Terrain (World) 1,467 variants in 17.7 h (~43 s each:
      tessellation, 14 layers in one pass), its Add pass 906 in 9.1 h, URP Lit 1,430 in 4.8 h, Terrain Base 500 in 3.0 h,
      Mesh Surface (Tessellated) 999 in 2.8 h, URP's stock Terrain/Lit (three passes) ~1,100 in 3.8 h. So: strip unused
      keywords in the terrain shaders first (an IPreprocessShaders step or the generator). The character look added the most:
      Lit (See-Through) went to 9,792 variants (~12 CPU-s each) once the 87 character materials wore it with skin/cloth
      keywords; align them on a few keyword sets, or carry skin and cloth without a keyword (a material property the shader
      reads, if the material buffer allows, or a rendering-layer bit). Keep URP's stock terrain shader out
      of the build (probably the test scenes); avoid editing includes the terrain uses; then the Mobile quality level's
      pipeline asset (URP keeps the union of both) and DX12 in the API list (every pass compiles for both).
- [ ] The user's call, not a default: cap 8K textures at 4K in shipping builds too (the cap note says 2K and 4K look the same
      from the top-down camera); smaller download and less video memory, but less source detail.

## Playtest feedback (2026-11-05)

From the user's playtest. Grouped; nothing here is built yet unless ticked. Decided with the user: bugs and tuning
first; the gathering swing's crit deals 2× and a glancing blow 0.5× (normal = today's hit); night corruption is rare at
first (about 1 night in 6) and grows with time, a small wave walls and NPCs can mostly hold; hand-painted ground always
wins over World Builder features.

### Bugs
- [x] NullReferenceExceptions in a build: `WorldMapHud` (its shader was stripped from builds; now referenced from MinimapSettings) and `TreeImpacts.Strike` (a neighbour tree destroyed mid-fall). Check the next build's Player.log for others.
- [x] No map when pressing M in a build (same stripped shader). Check it in the next build.
- [x] Main-menu and loading-screen pictures cropped on super-ultrawide: `ArtCover` crops at most 20 %, the background shows at the sides. Check on the 32:9 screen.
- [x] NPCs floated 6–15 cm: navmesh agents stand on the navmesh, baked above the terrain. Build Height Mesh is on (Haven, prologue and raid surfaces, rebaked); measured in Play mode, residents and creatures then stood level with the player. The tessellated ground can still sit a little above or below anyone's feet, the player's too.
- [x] Punching hits one enemy: Fists Cross, Hook and Rush now have Max Targets 1 (the jab already had).

### Tuning
- [x] Enemies +30 % max health (their stats assets) and +20 % damage (base and every attack) on all 14 MonsterDefinitions. Raid tier multipliers still apply on top; replay a tier to check it isn't now too hard.
- [x] Wolves reached the camp unseen: they spawned in the same frame as the "Wolves!" line, and crossed the 39 m while the camera was on the speaker. They now spawn when the line closes (`PrologueDirector.wolvesComing`; a checkpoint load still spawns at once). Play the camp to check they're seen coming.
- [x] Combat music starts 15 s in (`MusicTrack.startAt`, set on Audio Settings and the Haven's Scene Audio; the Haven scene's change goes in when the scene is next saved).
- [x] Workshop scaled to 70 % (4.5 × 3.7 m, about a cottage), with its collider and NavMesh obstacle.
- [x] Building/repair reach: measured from the building's middle (its site is a trigger, which reach skipped), so large buildings couldn't be worked. Now 2.2 m from the edge of the site's box (`ConstructionSite` is the interaction shape).

### Feel
- [x] Camera shake when trees crash and ores burst: it existed but was barely visible (shake is the square of the amount). Tree landing 0.6 → 1.0, felt to 40 m; ore and stone bursts 0.22 → 0.45.
- [x] NPCs leave footprints (`WalkerFootprints` on residents, companions and prologue companions): a print every 0.7 m at the foot bones, only inside the player's footprint window (`TerrainFootprints.StampInWindow`), which they never move. Checked in Play mode.
- [x] Arrows: the bow already snapped to a target near the crosshair; the snap area is wider now (5° → 9° with the action camera, 1.5 m → 2.5 m top-down, on the player prefab).
- [x] Options › Accessibility › Vitals Over Player: small health and stamina bars above the player while fighting (the combat music's rule), in the HUD's colours (`OverheadVitalsView`). Off by default. The stamina colour is the HUD's muted one; brighten if it's hard to read.
- [ ] Trees looked pixelated: textures are full size (4096) and the build's settings are High (same as Ultra for trees), so it's most likely the leaf cutout edges, which MSAA can't smooth. The tree shader's colour pass now uses alpha to coverage. Check the misty forest in the next build; if trees far off still look poor, their LOD4 is a 4-triangle card from ~5 % of screen height.

### Gathering
- [x] Gathering swing: hold Attack (or E) to raise the tool, which rises with the hold; a white glint on the tool's head and a small ping (`Tool Glint`, Tools/Audio/glint.py) mark the critical moment; letting go swings. In the critical zone a critical (counts as 2 swings, keeps its bonus yield and quiet), near it normal (1), too early or held to the top glancing (0.5, wears the tool). All in GatheringMinigameSettings › Hold and release / Rewards; the timing bar is off unless Show Timing Bar is on, and Auto Gather is unchanged. Session logic tested; play it to judge the rise speed and the glint's brightness from the game camera.

### Prologue
- [x] Optional prologue quests open once Sigrid has given the morning quest (new quest field Started First: MorningCamp only needs to have begun).
- [x] Optional prologue quests are a chain: Wood for the Road → Fish for Breakfast → Hunt for the Pot. Eirik's lesson still waits for the road chapter.
- [x] The prologue waystone is the Haven portal's arch (dormant stone look, Haven scale), in place of the two standing stones and their lintel; pillar colliders, the Waking Connection moved into the opening. Check the waking effect fits the arch when played.
- [x] The old troll now wakes when it appears and walks to the camp (Rock Shelter), fighting whoever is there or on the way, with its normal chase and leash (new story step Creature Hunts). The portal finale's Returns scene puts it back to a puppet first (Creature Stops Hunting). Not played yet: check it reaches the fire and how hard it is at 1,820 health.

### Haven
- [x] First quests on arriving, one at a time: Gifts of the Clearing (10 sticks, 7 small rocks) → Tools of the Clearing (new: craft the Stone Hatchet and Pickaxe) → A Light in the Wood (12 oak, 6 grey stone, 6 sticks, the fire and the workshop) → The Woodcutter's Lodge (new) → Raise the Walls (new: 4 palisade) → Strength for the Road and on. A Growing Home no longer asks for the lodge. Saves already past these steps will be offered the new quests once; building quests count what's already built, the tools quest wants them crafted again.
- [x] Waterskin: one skin holds up to 100 water (ItemDefinition Water Capacity; the stack's condition is its level, kept through moves and saves; it doesn't stack). Right-click drinks what thirst lacks and keeps the skin; every water source refills the skins in the bag and hands out one if there is none (was three). Old saves' stacks of three load as full skins.
- [x] Corruption nights (`CorruptionNights`, Resources/CorruptionSettings): from Haven rank 1, each nightfall has a 17 % chance (+3 % per rank, at most 45 %) of a warning and, 20 s later, a band of 3 (+1 per two ranks, at most 8) Misty Woods creatures out of the dark from one direction, going for the nearest building and anyone close (`HomeAssault`, shared with ward breaches). Survivors leave at dawn; never during a breach. Cheat menu: Corruption creeps in. Checked in Play mode: spawned 45–60 m out and went for the shed.

### Editor (2026-11-05)
- [x] Terrain went flat after a TerrainData reimport (`TerrainSurfaceReimport`).
- [x] Painting stalled for seconds per stroke (grass colour map now previews in memory, PNG written on save).
- [x] World Builder rebuilds took 50 s; forest floors now paint in under a second (14 s for a full rebuild). Left: the grass colour map bake (4 s, the water test) and feature heights (3 s).
- [x] Hand paint always wins: ground painted by hand where a World Builder feature paints is kept through every build (`WorldTerrainStash` hand-painted cells); Safe Haven › World › Give Hand Paint Back to Features lets features repaint it (under the selected features, or everywhere).
- [ ] Soft brush edges: the terrain's height blend (sharpness 1.76) lets tall layers such as moss show through where a stroke leaves partial weights. Full-strength strokes win. If edges still look wrong, try a lower Height Blend Sharpness on the terrain material.

## World tools (2026-11-05)

Built:
- [x] Drop and Drop + Align rest objects by their mesh (lowest points of the nearest LOD, each against the ground under it), not their bounding box under the pivot. On a 24° slope a boulder went from 29–50 % of its base hovering (up to 28 cm) to 0–1 %.
- [x] Drop with Physics (Ctrl+End, World Tools › Physics): selected objects fall and settle, then the temporary bodies and hulls are removed and they're seated on the ground by their mesh. Other rigidbodies are held still; one undo.
- [x] Biome Brush (Safe Haven › World › Paint › Biome Brush, World Tools › Biome Brush): BiomePreset assets (Data/Level/Biomes) paint a ground layer, plants with their own density, slopes and patches, and prop palettes in one stroke; Shift erases. Presets: Haven Meadow, Misty Woods Floor, Riverbank (only near water).
- [x] Ravines (GameObject › Safe Haven › World › Ravine / Ravine (Haven)): a spline carves a floor the depth below the ground with steep walls rising to the ground on each side (ragged, noisy, optional ledges), rock and floor paint, crag pieces along both walls and rubble on the floor; depth and width per knot. Cliffs gained Ledges and Face Noise (off by default). The crag fitting is shared (CragWall).
- [x] A build on one terrain no longer records the others for undo.

Next:
- [ ] Draw a real ravine in the Haven or a raid map and tune its defaults (5 m floor, 2.5 m wall run, 12 m deep) by eye; add a River on its line for a stream.
- [x] Biome Brush: on/off toggles per plant and prop palette (for the session), the plants and their share at the current strength shown under the brush, and the grass colour follows brush strokes live.
- [x] Biome areas: a Biome Area with a Biome (a BiomePreset) paints its ground, grows its plants as terrain grass and scatters its props over the loop, faded in from the edge, non-destructively: moving or deleting it gives the ground and grass back, paths and rivers still clear through it, editing the preset rebuilds the areas using it. World Tools › Build › Biomes › Draw Area, or GameObject › Safe Haven › World › Biome Area. New Dark Forest areas are painted with Misty Woods Floor.
- [x] Drop with Physics on a selected folder drops everything in it; Drop with Physics (Upright), Ctrl+Shift+End, keeps their tilt.
- [x] World Builder fingerprints count only settings that differ from the feature type's defaults, so a new setting no longer rebuilds every feature. (Switching to it rebuilt everything once: Haven and prologue rebuilt and saved on 2026-11-05.)
- [x] The automatic build is scheduled only by world features being added, removed, moved or edited (and spline and terrain edits), not by any hierarchy change.
- [x] Builds go scene by scene. The Haven and the prologue overlap in world space, and with both open the builder let Haven features (grove floors, paths, river) cut and paint into the prologue's terrains. Repaired by rebuilding the prologue with its own features: its ground matches the 13:27 commit in 99.9 % of cells. That 0.1 % was the road pit below. Backups of both scenes and all terrain files from before the repair: session scratchpad Backup_2106.

## World Builder round 2 (2026-11-05)

Fixed:
- [x] Prologue: a pit 10–14 m deep where the Old Road crosses from the meadow terrain into the forest. A height layer refused points on its last row and column, so a feature ending on a terrain's edge read the built terrain instead, which held its own last build: every rebuild dug deeper. The edge counts as inside now; the prologue was rebuilt and a second rebuild leaves it unchanged.
- [x] Prologue: 323 generated trees and stones stood under the ground. With the Haven open too, the builder found the Haven's terrain under them first (the scenes overlap). Ground lookups now stay in the scene being built (and prefer the active scene otherwise); drawing a feature only clicks on its own scene's terrain. Rebuilt; the Haven's generated objects checked and fine.
- [x] Builds no longer bake the grass colour map every time (5.4 s of a 9.5 s prologue build): the grass follows the paint through the quick preview and the full bake waits for the scene to be saved.

World Tools:
- [x] Tabs Place · Build · Ground · Water & Plants · Sky · Scene. Build and Ground have a look tab each (Haven · Forest Lake, Dark Forest · Mossy Forest), so every feature has one button instead of a Haven and a forest copy.
- [x] Build: Draw River, Path, Cliff, Ravine, Grove/Forest, Ground Paint ▾ (the look's paint rules); the look's biomes with Draw Area, Brush and Edit, New Biome; Build Now, Rebuild Everything, Build as I edit (per terrain), hand-painted cells with Give Back; the last build's report; every feature in the scene with its colour, an on/off toggle and Select.
- [x] Ground: sculpt, Use the look, Auto-Paint, Rock on Steep Ground, Add Moss, the look's grounds to paint with, relief, NavMesh. (The old Path Painter stays in the menu only.)
- [x] Drawing a feature: a dotted line from the last point to the mouse (and back to the first for areas), click the first point to close an area, and selecting something else finishes the drawing instead of leaving the loop open.
- [x] Grass touched by hand where a feature grows or clears it stays as it is through builds (as ground paint does); Give Hand Paint Back releases it too.
- [x] Drawing works from the Scene view itself (clicks used to select the terrain), nothing builds until the drawing is finished (Enter, or clicking an area's first point), and a cliff or ravine shows its face, height, plateau or floor and depth as a see-through preview while drawn and while selected, following the Inspector settings live.
- [x] Cliffs and ravines no longer stand crag pieces by default (the lake crags' slabs lay stacked and hanging off the face): the face is the shaped terrain in Cliff Rock with rubble at the foot. A Kit can still be set per feature.
- [x] Rock on steep ground and cliffs no longer repeats every 7 m: each of the three projections is sampled twice (as is and turned 63°, resized) and slow noise picks between them, for colour, normals and the tessellated relief alike (Hide Rock Repetition on the terrain material, on by default).

Next:
- [ ] A floor biome for the Haven's groves (old-growth litter, ferns, moss) so Grove areas paint their floor as Dark Forest areas do.
- [ ] Biome areas regrow their props when a path moves through them, but their ground paint only fades by edge distance: a Keep Off Paths distance for the ground and plants would leave a cleaner verge.

## Tools overview and UX pass (2026-11-05)

Done (overview of every tool: Docs/EditorTools.md):
- [x] Safe Haven menu: the hubs first (World Tools, Content Editor, Systems Editor, Sound Editor, Quest Map, Story Graph, Playtest Runs); the five "X Creator" menus became Content › Items / Monsters / Buildings / Resources / Prefab Browser (with Held Item Grips, Raid Layout Preview, Import Planner Data); Scene › gained Assign Missing Save Ids and Play Haven / Prologue (isolated saves); 37 run-once build and setup steps moved under Setup › (Art Pipeline, Characters, Combat, UI, Audio, Raids, Resources, World, Rendering). Docs and tooltips follow.
- [x] GameObject › Safe Haven › World: one River, Path, Cliff, Ravine, Forest Area and Biome Area in the look of the terrain in view (was a Haven and a forest copy of each); Ground Paint is a submenu.
- [x] The old Path Painter is gone (World Builder paths do it and stay editable).
- [x] World Tools: Water & Plants shows the selected-object tools only with a selection, one Add Plants ▾ for every look, no Rebake (automatic now); Sky leads with the Sky Controller and fog presets, and folds the once-per-scene setup and its health checks into Scene setup (its title counts problems); Ground links Terrain Surface and Mesh Surface; Scene adds Tidy Hierarchy and Play with a test save. The Scene view panel's buttons no longer cut their labels.
- [x] Every custom editor window opens and draws without errors (smoke test of all 22).

Found, not tools (Content Editor › Needs Attention):
- [ ] 17 raid events give the player no warning that they happened.
- [ ] 3 monsters' Animator controllers differ from their data (Update From Data on each).
- [ ] Elder Tree (building) has no combat stats; Awakening the Portal has a Quest Giver that neither starts nor takes it.

Next:
- [ ] Two prefab browsers: World Tools › Place (to place) and Content Editor › All Prefabs (to edit). One catalogue with both actions would be simpler.
- [ ] Sky Controller, Terrain Surface and Mesh Surface are still windows of their own; they could become World Tools panels.

## Suggested improvements (2026-11-05)

Gameplay:
- [x] Gathering: each tool tier above the node's rises 8 % faster (Rise Speed Per Tier), as well as widening the critical window.
- [x] Waterskin: its water shows in the slot's corner (blue; red when empty). Parched now slows stamina regeneration everywhere (×0.75, ×0.375 in combat), so running dry in a raid is felt.
- [x] Corruption nights: a wolf howl from the side the band comes from and the game's own Fog weather rolling in (the weather before it returns once the band is beaten or at dawn); beating the whole band gives 40 + 10 per creature experience (Cursed Essence stays raid-only).
- [x] Vitals Over Player also shows a health bar over each companion in the fight (dark red while downed).
- [x] Crafting objectives can count items already owned; Tools of the Clearing does, so saves that already have the stone tools complete it at once (the two older crafting objectives with the flag set were switched off to keep their behaviour).
- [x] The prologue troll: after "Leave the fire! Take the old path north…" the camera turns to the old path (a marker 16 m north of the camp) for 3.5 s.

Look:
- [ ] A CPU sampler of the tessellated terrain's height (the shader's displacement in C#), so Drop, footprints and character feet can sit on the drawn ground rather than the physics ground.
- [x] Ravines and cliffs: Overhangs (rim blocks leaning out over the face; ravines 25 %, cliffs off) and moss on north-facing ravine walls. Tune the overhang tilt by eye (they read as slabs on the rim). Hanging roots wait for a root model.

Tech:
- [ ] Handle the Pipeline/MCP polling: each CLI call leaks ~14 Token handles; batch agent work into fewer calls until it's fixed upstream.
- [ ] Tests that build terrains run in the open scene and leave traces there (an undo record on the first open scene for in-memory TerrainData, AtmosphereSetup adding Terrain Mist, the test runner's cleanup bringing a deleted terrain back). The ravine build test was removed for this; build such tests in a dedicated saved test scene loaded by the test.

## Improvements found in review (2026-11-04)

From reading the code, data and playtest logs, not from playing. Nothing here is built. The logs in `LocalLow/.../Playtests` mix real runs with test runs, so treat their counts as a hint. Most important first within each group.

### First: play it

- [ ] **Make a build and play one full raid without cheats.** `D:\Builds` is from 30 October, so everything since (companions on raids, the boss, bodies, journal and more) has never run in a build. Check there that the Heartroot's vein glow survives shader stripping and doesn't flash cyan. In the logs since 27 October, no raid without cheats lasted longer than 175 s, against a 16:40 clock.

### Small faults

- [ ] "Brought down by a The Heartroot" and "a Old Moss Troll": the words are fixed in `RaidReportText.cs:29` and `JournalContent.cs:156`. One helper for the article, shared with `JournalNotices.CreatureWritten`.
- [ ] Death recap can be blank: `CreatureRecords` ignores damage with no source, so a death by poison or the like names nothing. 4 of the 8 raid deaths logged since 28 October have no killer in the log either. Remember who applied the poison, or say what it was.
- [ ] Raids tab shows the number twice ("2  2. Misty Woods"): the row's letter comes from its name.
- [ ] Journal lines in the middle of a fight: three "New in your journal" stood in the feed while being attacked in a test raid. Hold them until the fight is over, or show one quiet mark on the HUD.
- [ ] Check the 20% find chance per cache against how many caches a raid really opens: the ten found pages could run out within a few raids, which would waste them as a reason to raid.
- [ ] Two tests have been failing for a while. `AudioSystemTests.LongClips_AreNotHeldDecompressedInMemory`: Heartroot Fight.wav is 5.8 MB on Decompress On Load, so run `AgentScripts/BuildAudio.cs`. `HearthlineTests.VitalsCanBeKeptLit` reads the Editor's own Always Show Vitals option: the test should set it and put it back.
- [ ] The status table in ARCHITECTURE.md is out of date: it still says "No companions in raids", "One recruit (Ingrid) and one job", a 300 s raid, and that M4 companions come next.

### Knowing how it runs

- [ ] **Editor handle findings (2026-11-05, 30 s log over 5 h).** The dialog failures ("DialogBoxParamW failed … Window Manager objects") are about USER objects, whose per-process limit is 10,000: they rose from ~330 to 9,382 at 14:36 and 9,999 at 14:56, then dropped back to 334 by 15:06; entering Play mode took them to 3,970. Kernel Token handles grew 3.8k → 52.8k in bursts of up to +4,000 per 30 s around heavy Editor work (recompiles, World Builder rebuilds, Play mode) and stayed flat in between; each CLI call adds ~14, rejected HTTP requests ~1.4, thread creation none. Next: measure USER objects and Tokens around one recompile, one Play-mode entry and one World Builder rebuild each. The logger is `HandleLogger.ps1` + `HandleTypes.ps1` (in `Tools/Diagnostics`; run `HandleLogger.ps1 -ProcessId <Unity pid> -Csv <file>`).
- [ ] **Unity Editor handle leak.** The Editor's Windows handle count keeps growing until it can no longer open dialogs or windows ("DialogBoxParamW failed … Window Manager objects" in Editor.log); then it freezes on the next dialog (a test run's "save modified scenes" prompt locked it on 2026-11-05 at ~530,000 handles and needed a restart, which also briefly left the terrain without tessellation). Seen: ~26,000 handles right after a restart, ~46,000 three minutes after another restart with nothing run, ~78,000 after an afternoon of Editor commands, renders and tests. Find the source by measuring the handle count (Task Manager › Details › Handles column, or `Get-Process Unity`) around one kind of operation at a time: idle, opening the terrain inspector, World Tools open, a Scene view paint stroke, a CLI command, a test run, entering Play mode. Suspects: an editor window or overlay repainting (World Tools' ground pictures, Terrain Tools overlay, which also throws a NullReferenceException in `BrushSizeVariator.OnEnterToolMode`), the Unity Pipeline/MCP bridges, `TerrainLayerWatcher` (polls every second). Until found, restart the Editor every few hours of heavy use.
- [ ] Log frame rate and hitches per scene in `PlaytestLogger`. Nothing records them, so nobody knows how a large raid runs on real hardware. Do this before spending anything on the items below.
- [ ] Record who made each kill in `Killed` rows, and write creatures by name (`ExpeditionWolf(Clone)`, `Packmate1`). Thin out `LessonDefended`: 895 of 4,998 rows since 1 November.
- [ ] Mark runs started by agents or the Editor in the log, so real play can be picked out without guessing from file times.
- [ ] The memory freeze on full test runs: find what the suite leaks, so all of it can be run again. Only chosen classes have been run since, so a break elsewhere can go unnoticed.

### Creature performance (only if the frame rate says so)

- [ ] No creature has an LOD, and the camera is usually 19 m away. Measured triangles: Mistwing 7,732 (the most of any, for a 0.75 m bat), Heartroot 7,340, Old Troll 6,570, Root Guardian and Rootling 5,438 each (a knee-high Rootling shares the 2.9 m body's mesh), Bog Spitter 4,368.
- [ ] Small Moss Troll and Moss Shaman are 7 skinned meshes each (329 bones between them, 2 meshes hidden): merge them in Blender or delete the hidden ones.
- [ ] Every varied spawn makes its own copies of its materials (`CreatureLook`), which last until the scene ends. Count them after a large raid. A property block is the alternative, but `HitReaction` clears those, so it would have to learn to put the look back.
- [ ] `Treant1Clips.fbx` and `Treant2Clips.fbx` carry a whole mesh just for their clips: strip them to the skeleton.

### Distant gathering objects (only if the frame rate says so)

Placed trees use `NodeStreaming` for now: far from the player each is a bare mesh with no scripts, and a live copy is made (`Instantiate`) within 10 m or when hovered, then destroyed past 14 m. Not measured yet. If creating copies shows up as hitches, or the work around it becomes a burden, try instead:

- [ ] **Turn scripts off rather than remove them.** Keep every tree whole, switch its gathering components off (`enabled = false`) when far and on when near. No objects made or destroyed, no garbage, and save ids, quest links and references stay valid, so trees with their own saved state could join in. Costs a little memory (a few KB for ~1,100 trees) and the components' load time.
- [ ] **Reuse a pool of live copies.** Keep the bare meshes, but move a small pool of ready live trees into place instead of making and destroying them. Removes the `Instantiate`/`Destroy` cost, keeps the low memory.
- [ ] Either way, cover rocks and other nodes too (now only trees with no saved state and no model variants are streamed).

### Creature looks

- [ ] The Huldra, Mistwing and Bog Spitter have no eyes in the dark, and the Huldra is the one that should unsettle most. Her eyes could light only as she turns on you.
- [ ] Eyes shine the same at noon as at midnight. Dim them by `DayNightCycle`, so that night is when they mean something.
- [ ] The Heartroot's veins are patches from a mask, not lines: paint a proper vein map when it gets its own look, and make the last stage split the bark as its line says.

### The loop between raids and home

- [ ] **A fuller homecoming.** "While you were gone your people made..." covers only residents' work (`HavenProgress.cs:158`). Add finished station queues, ripe crops and what the haul now lets you build, so a raid pays off the moment you are home.
- [ ] **A village that remembers.** Nobody at home reacts to a rescue, a lost companion or a boss, though the GDD asks for it. Mostly writing on what exists: a line each, and an empty workplace while someone is lost.
- [ ] **The first fight.** Nearly half the deaths logged since 27 October are in the prologue, to the wolves and small trolls (11 of 23). Falling there costs little, but it's the first impression: see in play whether the wolves are too much for someone who hasn't learned to dodge yet.
- [ ] **Dying at home.** Logs show three deaths in the Haven early in a run, to wolves and a blight boar (1 and 3 November). Decide if something called a safe haven should kill a new player, and what it costs them.
- [ ] **A ladder of bosses.** There is one boss, and beating it opens a Troll Valley with the Woods' scene and no boss of its own. The Old Moss Troll is the natural second. Look at similar games first.

### Ideas from the journal

- [ ] **Bargain with a troll.** The journal now says a troll can be bargained with, and "The Troll Toll" says what it takes: a stone carried from far off, and don't run. The GDD lists bargains as unbuilt, and standing still in front of something that size would be a memorable, very folkloric moment.
- [ ] Let some found pages lead somewhere: "Something Runa Left" could mark a rumour, and "The Last Smith's Note" a cache of barrow iron. Today a find is only something to read.
- [ ] A Rumours shelf on the Quests tab, for what is known and where, as these are only on the map during a raid.

### To decide

- [ ] Controller support: bindings exist and the journal can be moved through, but on-screen prompts name keys and there is no rumble. A big job, and only worth it if the game will ship with it.

## Combat

- [x] **A finishing blow on staggered enemies**: Draug and Barrow Shieldbearer. More person-shaped creatures are a tick on Can Be Finished.

## Reasons to raid (relics, rumours, people, finds)

- [x] **Authored set-pieces in the generated maps (GDD §9.1, §9.2)**: six so far, on every rumour site and a few camps. Still open: a troll bridge over a ravine and a flooded hollow, which need the ground shaped for them, and rumours that name a place and a direction instead of an exact distance on the HUD.
- [ ] Play them for feel: every number is a first estimate (relic powers, 2 rumours a run, job bonuses, 6% relics in caches).
- [ ] Real art for the rumour sites: they are placeholder shapes (`Prefabs/Raids/Events/Sites`), as is the Elder Tree.
- [ ] Icons for the plans and living things: the plans use the Forge Blueprint's, the others borrow from materials.
- [ ] Give the expedition their own models: everyone is IngridModel, at home and in raids.
- [x] Troll Valley has its own Layout Rules, creatures, tiers and rumour pool, opened by beating the Heartroot. Still the Woods' scene and art: it needs a look, its bridges and its boss (GDD §10.7).
- [ ] A goat (or other animal) for the Haven: bees, seed and the Elder are in, a living animal isn't.
- [ ] Relics in the raid report: they are named in "On the way", but the haul lists the plain item.

## Raid maps (Misty Woods forest terrain)

### Art
- [ ] Replace the placeholder forest kit (spruces, pine, dead spruce, boulders, log in `Assets/_Project/Art/Forest`)
      with real tree and rock art. Swap the prefab mesh/material, then "Raid Map" › Bake Map. No layout changes needed.

### Designer tools
- [ ] Add a "Raid Maps" page to the designer window: preview of the marker image over the height map, legend,
      site counts per group and a Bake button.

### Content
- [ ] More event-site variety (new prefab in `Prefabs/Raids/Events` + an entry in `MistyWoods_Events`).
- [x] Dedicated wolf pack prefab for enemy camps (one howler + one or two packmates), with staggered attacks.

### Polish and fixes
- [ ] Ground event prefab children individually; pieces sit at local y = 0 and may float on slopes.
- [ ] Check that the Campfire in the Abandoned Camp event doesn't carry Safe Haven station/building logic into raids.

### Verification still to do
- [ ] Play-test the 180° and 270° map rotations (only 0° in the editor and 90° in play mode are verified).
- [ ] Play a full raid from the Safe Haven portal through the loading screen into the rotated map and back.

## Art still owed by the new systems (2026-11-03)

- [ ] The Heartroot has a body of its own now (Treant 2, 2026-11-04), but a treant shaped like the Root Guardian's. Its stump is still a placeholder.
- [ ] Heartroot's Grasp has Frost Rune's icon and effect, with roots on the hit.
- [ ] Heartroot Fight is a synthesised drum loop (`Tools/Audio/boss.py`), 33 s and never heard by whoever made it: listen to it, and a composed track may be better.
- [ ] Haul Sack has the Hide Rucksack's icon.
- [ ] Set-pieces are the forest kit and primitives. The Old Barrow's mound most of all: from the gameplay camera it is a smooth green shape.
- [ ] Finished goods at a workplace, growing as residents make them: tools and sounds are in, the goods are not.

## Play these (2026-11-03)

Everything below was driven by scripts, not played by hand: the numbers are first estimates.

- [ ] The Heartroot's 1100 health, 320 poise and damage, against kits a third raid really has.
- [ ] Companion strength per tier, and whether 45 seconds is fair in a real fight.
- [ ] Guards: two killed a first breach wave with the player standing still.
- [ ] Whether set-piece guards are too much on top of the raid's own camps, as every rumour now has some.
- [ ] Large raids: is 29:10 right for four times the ground? Are 12 set-pieces and 533 resource nodes too much or too little to find, and is the walk to an exit fair?
- [ ] Companion ranks: does +30% health and +20% damage at Sworn take too much of the fight?
- [ ] Whether a rumour's rough direction is enough to find it by, on a normal raid and a large one.
- [ ] A Day raid (11:00, sun 1.36) reads as deep blue night on screen. Kept as it is for now (your call, 3 November); come back to it after playtests. The fog of war is not the cause.
- [ ] Raid HUD: does one way line, the maps and M give enough, or do you miss seeing every exit at a glance? (`RaidHudView`; the old list is in git.)
- [ ] A large raid has six exits, spread out (nearest 283 m from the start on the seed tried). Is the walk fair? `Large Raid › Extra Exits` is a list of names.
- [ ] Long messages moved beside the vitals, pickups stayed by the character (1 Nov playtest fix). Is that the right split?
- [ ] Amnesia: is one memory back every 2 raids right, and does a lost companion turning up on the very next raid make losing them cost too little? `Memory Returns Every`; a wait would be a rule on the rumour.
- [ ] Someone lost stands still until found. An idle or wandering animation, and their own lines (`ResidentDefinition › Returning Lines`, only general ones are written).
- [ ] Heavy bag: 80% speed at a full bag, and whether the Haul Sack's noise is a real price.
- [ ] Listen to the creature voices (`Tools/Audio/creatures.py`; 2026-11-04): measured and seen on spectrograms, never heard. Wolves and boars are the likeliest to want real recordings. Levels and reach are in `AgentScripts/BuildCreatureVoices.cs`.
- [ ] Whether a voice on every wind-up, and on each blow of a combo, is too much in a large fight, and whether the treads get in the way.
- [ ] Time the window fade (0.12 s, `UIStyle`) by hand: Inventory and Character get opened constantly, so it mustn't feel slow.

## Creature bodies (2026-11-04)

Bodies came from CC0 packs: see `Art/Enemies/Downloaded/CREDITS.md` and "Creature bodies" in the architecture notes.

- [ ] Play them: retargeted clips were checked as still frames and in a short spawned fight, not over a full raid. Look for feet sliding, the Draug's blade in its hand and the shield held up.
- [ ] Marsh Light stays a glowing orb on purpose: check that it still looks right beside the rest.
- [ ] The Wolf (Quaternius, now with a run, an attack and hit reactions) is still flat low-poly, and the Boar is 284 polygons, beside painted Kelgar models. Look for painted CC0 ones if they stand out in play.
- [ ] The Forest Monster and Goblin came with clips of their own, but on IK feet that Humanoid can't use, so they keep the shared ones. Bringing them over means baking them in Blender.
- [ ] Judge in play, none of it has been played by hand: the Huldra's hollow back and the Draug's mail are rigid and may cut through in big swings; the mail read blue in a preview scene; the Draug's shamble; the Heartroot's vein glow at each stage; a resting creature's crouch; how the wolf's stride matches its speed; elite tints. Body variation and collision sizes are guesses from renders: check the Heartroot's reach still lands now its capsule fits it.
- [ ] Draug's blade and shield keep the old primitive-looking items.

## Journal (2026-11-04)

- [ ] Read the words in the game: all of it is first draft, in particular the backstory pages and which creature opens which.
- [ ] Read the new pages too: the people, the ten found in raids, the notes people leave on creatures. Some claim things about characters (Bjarne's bargain with a troll, who knew what) that the story doesn't say yet: keep or cut them.
- [ ] People's notes cover only some creatures, and Bjarne has just one: add more as their voices settle.
- [ ] Moving through the journal with a controller is built but untested, as none was connected. Try it with one.
- [ ] Mechanics pages are hand-written from `Docs/Combat.md`: keep them in step when rules change.

## Loading screens (2026-11-04)

- [ ] The art (`Art/UI/Loading`, from LocalSpawn's Safe Haven styles) shows places the game doesn't look like yet: the Haven's picture has a finished village and a rougher stone arch than the portal. Regenerate when the Haven has its look, or if it promises too much.
- [ ] Raid Results and the main menu use the general picture. Give the raid results one of its own, ideally different for a haul and a loss.
- [ ] Troll Valley shares the Misty Woods' scene, so its picture and tips too.

## Materials as items (weight and carrying)

- [x] Soft encumbrance: past 80% of the bag's budget speed, stamina and noise ease toward their full-bag values (`PlayerBurden`).
- [x] Haul Sack: more weight, fewer slots, slower and loud. Still open: a pack animal or wheelbarrow (GDD §12.4).
- [ ] Refine at home pays off: tune item value per weight so ingots/planks/blocks are clearly better to carry than raw ore/logs.
- [x] Store all materials in one press at the stash (`StashStorage.StoreMaterials`), with stations still pulling from storage.
- [ ] Decide essence loss on a failed raid (e.g. lose the essences gathered this raid, keep banked ones).
- [ ] Ground drop prefabs for materials (a generic sack/bundle prefab) so dropping a material spawns something to pick up.
- [ ] Per-material stack size tuning after playtest (hides 20, trollstone 10, the rest 50).
- [ ] Show the weight of a full stack in the raid preparation window, so the loadout's weight is visible before leaving.

## Saved games (2026-10-30)

- [ ] Show a picture of the place or the settlement on each save's row.
- [ ] Saves with the same name are allowed; decide if that should stay.
- [ ] Save list: confirm it stays quick with many saves (each row reads its whole file when the list opens).

## Options (2026-10-30)

- [x] Key prompts in text follow a rebind (`GameInput.WithRebinds`).
- [x] Sound captions, rarity letters, damage and timing assists. Still open from GDD §18.5: full controller support for prompts.

## New content (2026-10-30)

Raid events, gear, food, buildings and creatures are in, built by `AgentScripts/BuildGear.cs`, `BuildFood.cs`,
`BuildCreatures.cs` and `BuildRaidEvents.cs` (run them in that order). Open:

- [ ] Balance everything by playing: numbers are first guesses against the existing gear and monsters.
- [x] Balance pass 2026-11-06 (`AgentScripts/BuildBalancePass.cs`, run last): every building and level costs twice its original amount (trophies excepted; 888 oak for everything once, was 444), A Light in the Wood gathers the doubled Workshop; charcoal at the Woodcutter's Lodge (3 oak for 2, 10 sticks for 1) fuels every ingot (2 / 3 / 4), Tor keeps 40; night creatures 1.1x health, no extra damage (were 1.3x / 1.25x). Calculated, not yet played. See BaseProgression and GearScoreAndRaidTiers.
- [ ] Charcoal: a real icon and a dropped-item look of its own (it borrows grey stone's model); play the first smelt to see whether players find the Lodge recipe.
- [ ] Wood sinks still open from the 2026-11-06 suggestions: hearth firewood for comfort (a worker refills it, empty only loses the bonus), Trading Post timber orders.
- [ ] Play the raid length: `MistyWoods.asset` is at 1000 s (16:40) while its stages and exits are written for 720.
      Event windows are in seconds, so they fit either way, but fewer events fit in 300 s.
- [ ] Real models and icons: new creatures, buildings and event sites are placeholder shapes.
- [ ] Fit shields and new weapons in the hand (Safe Haven › Content › Held Item Grips): grips are copied from similar items.
- [ ] Status icons: conditions show as words above the vitals (GDD §10.6 asks for icons).
- [x] Watch Post upgrades name a raid's coming events in the briefing, in place of a separate Scout Post (`RaidForecast`). Decide if the GDD's Scout Post should still be its own building.
- [x] Defender roles for ward breaches: Warden and Hunter stand guard (`ResidentDefender`). Check it doesn't take the fight from the player: two guards killed a first wave unaided.
- [ ] Settlers (Hild, Asa, Gunnar, Yrsa) borrow expedition models: give them their own, and icons for the Boar Pie, Trout in Cream, Haven Feast and Moon Broth, which show the stew's for now.
- [ ] Trollstone armor copies the moonsilver worn look: give it one of its own.
- [ ] Listen to the portal's voice in the game: its takes were measured, never heard. `Tools/Audio/portal.py`, level on Audio/Sounds/Library/Portal Voice.
- [ ] Playtest the base progression pace: raids per tier, Purified Essence against ward, altar and upgrade costs, Trading Post prices.

## Code quality: SOLID refactor (analysis 2026-10-29)

Rules written as special cases instead of following the existing model. Work top to bottom.

1. [x] **Interaction kinds.** `Interactable` gets an explicit Kind: **Use** (completes on the press: talking, opening
       a chest, station, portal, lighting an exit; no player state, animation or work bar) or **Work** (today's timed
       work; duration 0 still means "until Complete()"). Set Use in data (prefabs, scenes, prefab builders), and remove
       the `Instant` flag set in four `Awake`s, the fake 0.2-0.4 s durations and ExtractionPoint's Complete-on-Started.
   - [x] **Talkable**: one shared conversation component (open, camera framing, walk-away close) that gathers pages
         from `IConversationTopics` sources, replacing the copies in QuestGiver, PrologueCompanion and ResidentNpc.
2. [x] **IInteractionName** instead of the type checks in `WorldInteractionIndicator.NameOf`.
3. [x] **Target priority as data** (`Combatant`) instead of `Player.IsPlayer` in MonsterAI target choice; arrow
       recovery as a `Launch` parameter instead of checking whether the owner is the player.
4. [x] **Split large classes**: `Companion` (movement/combat, downed, orders, looks) and the target choice out of
       `MonsterAI`.
5. [x] **Non-blocking lines in combat**: lines without answers during a fight show as subtitles and don't hold attacks,
       so the prologue's wait-for-the-line rule and the Troll Ambush reorder become unnecessary.
6. [x] Smaller: companions stop only when they themselves are talked to (not on any open dialogue); companion grips as
       rig data instead of the player's HeldItemView; one encounter definition per prologue chapter (camp wolves and
       portal enemies the same way).

## Code quality: quests and events (analysis 2026-10-29)

Same review for Quests/ and Events/. Work top to bottom.

1. [x] **One "by the player" rule.** Done: `GameEvent.ForPlayerSide` (quests, kill experience) and the strict
       `GameEvent.ByPlayer` (sounds, the prologue's own fire, gather experience). `GameEvent.ByPlayer` (`Player.IsPlayer`; no actor counts as the player) and
       `ExperienceTracker.ByPlayer` (no actor does not count) disagree. Decide once, on the side (`Faction.IsPlayerSide`
       of the actor's Combatant), so a companion's kill counts for "kill 5 wolves" and a wolf burnt by nobody doesn't.
2. [x] **Objective rules in one place.** Done: every per-type rule is in `QuestObjective` (Progress, Describe,
       FindProblem, Owned, FieldsOf for the drawer). Fixed on the way: the Inspector hid a gather objective's material
       item. Left for when a type needs it: one class per type (`[SerializeReference]`, like `StoryNode`) with an asset
       migration.
3. [x] **Death subject without type checks.** Done: `IEventSubject` (MonsterAI, BuildingObject); the kind comes from
       the Damageable's category. `GameEvents.OnAnyDied` asks for MonsterAI, then BuildingObject; give the
       dying object a small interface for its definition (like `IInteractionName`).
4. [x] **No Portal inside the generic trigger.** Done: `TriggerWhen.PlayerInsideArea` watches any `IArea`
       (HomePortal is one); the SafeHaven scene's Portal Bloom points at the tree. `GameEventTrigger`'s PlayerInsideWard polls `HomePortal.Current`;
       an area interface (or a ward trigger component) keeps the trigger generic.
5. [x] Smaller. Done: `IDisplayName` (ItemDefinition, ResourceDefinition) replaces the type switches in
       QuestObjective, StationMenu and StationWindow. Kept on purpose: `QuestGiver.CanInteract` advancing its waiting
       lines only happens for givers without a Talkable (notice boards), whose prompt is the line; a clean fix needs a
       "focused" hook on Interactable. HomePortal's talked event is raised when its menu (ward, breach) closes, not on
       talk, so it is not Talkable's.

## Code quality: other systems (analysis 2026-10-30)

Same review for the rest of the runtime code (SOLID, Unity practice, no special-case rules). Work top to bottom.

1. [x] **MonsterAI is still 1404 lines.** Done: `MonsterMovement`, `MonsterAttacks` and `MonsterPose` (1063 lines
       left: the state machine with perception and investigation, which drive its states). Was: targeting is out (`MonsterTargeting`); perception and investigation
       (Hear, Sense, TrackTarget, the investigation states, idle gaze, ambush, indicator) and attack actions
       (BeginAttack, UpdateAction, charge, howl, projectile, retreat) are two more plain C# parts, as `Companion` was
       split. Behaviour unchanged; the existing AI tests cover it.
2. [x] **Item or resource is one idea.** Done: `IStackDefinition` (id, name, icon, stack size, slot tint) for
       recipe output, station output and saves. Giving stays with each inventory (bag, essence storage, material pool).
       Was: "Is it an ItemDefinition or a ResourceDefinition?" is asked in
       StationCrafting (deliver output), ResourceSource (harvest), SaveMapper (output id), ItemTooltipView,
       StationWindow (icon, tint, stack) and the quest/station names (now `IDisplayName`). One interface for what both
       share (id, name, icon, tint) and one place that gives an amount of either to someone (bag or material pool).
3. [x] **Monster checks outside AI.** Done: `ICombatSounds` on Monster/BuildingDefinition (reached through
       `IEventSubject`), `IStaggerable` (MonsterAI) for parries, and "a fight is on" from the hit's faction (a Neutral
       creature never fights back). CompanionLeader keeps MonsterAI: companions fight monsters, like the spawners. Was: AudioManager (death sound, "a fight is on"), CombatFeedback (hurt sound),
       PlayerGuard (parry stagger) and CompanionLeader ask for `MonsterAI`. Sounds come from the definition the
       object reports (`IEventSubject` + a small sounds interface, like BuildingObject already has); stagger is an
       `IStaggerable`, so a companion or a future NPC can be staggered too.
4. [x] **UI binds to the player, not to whatever is found.** Done: the windows take `Player.Current` (MessageFeedView
       also rebinds on `Player.Spawned`, like PlayerVitalsView); `PlayerCameraController.Current` replaces the Finds. Was: CharacterWindow, ItemInventoryWindow, MessageFeedView,
       ResourceInventoryPanel and SkillWindow fall back to `FindAnyObjectByType<ItemInventory>()` and the like, which
       can pick a chest's inventory. Bind to `Player.Current` (and rebind when the player arrives). The
       `PlayerCameraController` is looked up with Find in five places (DialogueBox, NokkenEncounter twice,
       ArcheryCameraAim, StoryNodes): one owner exposes it.
5. [x] **One rule for spawned encounters and saving.** Done: SaveableEntity leaves copies without an id unsaved (no
       warning; Level Check reports scene objects missing one); TransientSpawn is gone. Was: `TransientSpawn` strips SaveableEntity from copies at runtime,
       and the prologue rejects saveable enemy prefabs in validation. Let SaveableEntity decide: a copy made while
       playing that wasn't spawned through `SaveableEntity.Spawn` is simply not persistent (no warning, nothing to
       strip).
6. [x] **BuildPlacer (981 lines)** Done (763 left): `BuildGhost` for the preview, and `IBuildArea` replaces the
       nine "prologue or Portal?" branches (HomePortal and PrologueDirector are build areas). Was: holds the build menu pages, the placement rules (grid, snap, lines, CheckSpot)
       and the ghost visuals. Split the menu and the ghost out; the rules become testable without a scene.

Fine as they are: the six `Instance` singletons (AudioManager, CombatFeedback, GameStateManager, UIStateManager,
ExperienceTracker, QuestManager) are scene services with one owner each; spawners configuring `MonsterAI` directly
(NightProwlers, WardBreachDirector, PrologueDirector) are spawning monsters, so knowing the type is their job; raid
generation's Find calls run once per raid build.

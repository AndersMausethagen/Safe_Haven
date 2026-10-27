# Atmosphere: fog, fog of war, light and night

The world's mood in the way of Gord: mist that lies in the low ground and glows around torches, ground the player has
never seen lost in darkness, and at night a grey, colourless veil over everything beyond the reach of sight and light.
Safe Haven stays bright inside the ward; torches push the dark back; creatures prowl the night outside.

## What the player sees

| | Day | Night |
|---|---|---|
| Ground never explored | Dark, slowly turning fog | Same |
| Explored, far from the player | Plain to see | Grey, dim veil |
| Within the player's sight (12 m) | Plain | Plain, cold moonlight |
| Inside the ward, around a campfire or torch | Plain | Plain, lit warm by the fire |

Volumetric fog lies over all of it: thin morning mist at home, thick drifting mist in the Misty Woods, darker and
denser at night, lit through the shadow maps by the sun or moon (shafts between trees) and glowing around fire. It can
lie as a blanket that follows the ground and spills down slopes, rolls in and out in large banks, thickens with the
weather, gathers in fog areas (hollows, marshes) and makes monsters see less far.

## How it is built

- **`AtmosphereFeature`** (renderer feature on `Settings/Rendering/PC_Renderer`, shader `Shaders/Atmosphere.shader`).
  Game cameras only; the Scene view stays clear. Two Render Graph passes before post-processing:
  1. *March* at half resolution: each view ray is sampled (`steps`) up to the scene depth or `maxDistance`; density =
     `density` × e^(−height above `baseHeight` × `heightFalloff`) × drifting value noise, thinned by `clearing` inside
     visibility sources; light = ambient + sun/moon × its shadow cascades × Henyey-Greenstein phase + the nearest 8
     `AtmosphereLight`s. Output: in-scattered light and transmittance.
  2. *Composite*: scene × transmittance + fog light, then the night veil (× `_SafeHavenNight` from `DayNightCycle`)
     outside every `VisibilitySource`, then the unexplored fog from the `ExplorationMap` texture. The fog of war is
     laid on the ground the player stands on: a pixel of something tall near the camera takes the ground behind it.
- **Volume overrides** (Add Override › Safe Haven): **Atmospheric Fog** and **Fog of War**. Set in `SafeHaven_Volume`
  (thin mist, fog of war), `SafeHaven_Night` (denser, darker mist, stronger moon and fire in the fog) and
  `MistyWoods_Volume` (thick mist, fog of war). Because they are volume components, weather or event volumes can
  change them too.
- **`VisibilitySource`**: the player (`PlayerArmature`: sight 12 m, reveals 20 m, not a light), the Heart Tree (follows
  the ward radius + 4 m), campfires (10 m) and torches (9 m), a lit extraction beacon. `IsLight` sources count as light
  for night creatures (`VisibilitySource.IsLit`).
- **`AtmosphereLight`** on a `Light`: it glows in the fog with the light's own colour, intensity and range (flicker
  included). On torches, campfires and lit beacons.
- **`ExplorationMap`** (one per scene, on the "Atmosphere" object): a grid (`ExplorationGrid`, plain C#, tested) over all
  terrain tiles, revealed around every revealing source five times a second and uploaded as an R8 texture. In
  SafeHaven it is saved (`Persist`, a `SaveableEntity`, one bit per cell); in raids it starts dark every run.
- **`LightFlicker`**: perlin flicker for fire lights.
- **`FogGround`** (on "Atmosphere", an `ISceneSetup` after the raid map turns): bakes every terrain tile's height into a
  small texture, so the fog can follow the ground. **`FogArea`**: pockets of extra fog (up to 8 nearest the camera).
  **`AtmosphereConditions`**: the weather's fog factor (from `DayNightCycle`) and the fog's density as drawn, read by
  monster sight (`PerceptionRules.FogSightScale`).
- **Designer tools** (`Editor/Environment/AtmosphereSetup`): Set Up Open Scene, Apply Preset, Fog Area (below).
- **See-through occluders** (`SeeThrough`, `Shaders/SeeThrough/`, `Editor/Environment/SeeThroughMaterials`): below.
- **Torch** (`Data/Buildings/Torch`, 2 Sticks + 1 Nettle Fiber, known from the start): a post with a flame, a warm
  flickering light, `AtmosphereLight` and a `VisibilitySource`. Built inside the ward like any structure. Rebuilt by
  `AgentScripts/BuildTorch.cs`.
- **`NightProwlers`** (SafeHaven, "Night Prowlers"): see GDD §6.3. Weak wolves, at most three, appear out of view 30–48 m
  from the player and outside light at night once the first breach is won (never during one). They avoid light
  (`MonsterAI.AvoidArea`): they don't notice anyone standing in it and lose a hunt that leads into it. Every 8–16 s an
  idle prowler closes in, on the player in the dark or to the edge of the light nearest it. At dawn they walk off and
  are removed. Spawned through `TransientSpawn`, so they are never saved.

## For designers: fog on a new map in a few minutes

1. **Open the scene** and run **Safe Haven › World › Sky and Weather › Fog › Set Up Open Scene**. It adds what is missing
   and tells you what it did: the renderer feature (once for the project), an "Atmosphere" object with the
   Exploration Map (saved at home, fresh every raid: it decides by the scene's name, `Raid_…` = raid) and Fog Ground,
   a global Volume with a profile beside the scene if there is none, and Atmospheric Fog + Fog of War overrides on it
   (a raid gets the Misty Woods look, other scenes Morning Mist). Save the scene.
2. **Pick a look**: **… › Atmosphere › Apply Preset › …** on the selected Volume Profile (or else the scene's base
   volume): *Clear*, *Morning Mist* (thin, home), *Ground Blanket* (thick, low, follows the ground), *Misty Woods*,
   *Marsh* (dense, green, close clumps), *Night Mist* (for a night volume). Then fine-tune on the profile (below).
3. **Add pockets of fog**: **GameObject › Safe Haven › World › Fog Area** drops one at the Scene view's centre, on the ground
   (in a raid, under Map Content so it turns with the map). Move and size it; the gizmo shows its shape and soft edge.
   Good places: hollows, marshes, waterfalls, a cursed grove. Misty Woods has one, "Misty Hollow", as an example.
4. **Night**: add the sky rig (Safe Haven › World › Sky and Weather › Add Day, Night and Weather to Scene) and put a
   *Night Mist* override on its night volume: the veil, darker mist and stronger fire glow come with it.
5. **Check in Play mode** (the effect is drawn in the Game view only, so the Scene view stays clear for editing).

### Atmospheric Fog, field by field

| Field | Does | Try |
|---|---|---|
| Density | How thick the fog is at its base. 0 = no base fog (fog areas still show) | 0.01 thin, 0.03 misty, 0.06 dense |
| Base Height | Where it is thickest: a world height, or metres above the ground with Follow Terrain | 0–2 |
| Follow Terrain | 0: fog lies at a fixed height and pools in valleys. 1: a blanket over the ground, as deep on hills as in hollows, spilling down slopes | 1 at home, 0.6 in forests with valleys |
| Height Falloff | How quickly it thins upward. High = a thin layer on the ground | 0.2 tall, 0.6 low |
| Noise Size / Strength | Size of the drifting clumps, and how patchy they are | 10–20 m / 0.5–0.8 |
| Wind | Drift direction and speed of the clumps (m/s) | (0.6, 0.05, 0.3) |
| Bank Size / Strength / Speed | Large banks rolling over the land: size, how much they matter (0 = even fog), speed × the wind | 120 m / 0.5 / 0.5 |
| Weather Influence | How much the weather (Clear 1, Mist thicker) thickens it | 0.6 |
| Albedo, Ambient | Fog colour and the light it has everywhere | darker ambient at night |
| Main Light, Anisotropy | How strongly sun/moon lights it (through shadows: light shafts) and how much it glows toward the light | 1.2–2 / 0.5 |
| Local Lights | How strongly torches, campfires and lit beacons glow in it | 1.5–2.5 |
| Clearing | How much it thins around the player, torches and the ward | 0.4–0.6 |
| Max Distance, Steps | How far and how finely it is gathered (cost) | 90 m / 24–28 |

### Fog Area, field by field

| Field | Does |
|---|---|
| Density | Extra fog at its heart, on top of the scene's fog |
| Radius / Height | Half its width / half its height (an upright ellipsoid: low and wide lies like a blanket) |
| Softness | Share of it over which it fades out at the edge |
| Follows Weather | Thickens with the weather's fog like the scene's fog |

### Fog changes gameplay

Monsters see less far in fog: at **Senses & Stealth › Fog › Dense Fog** density and above they keep **Sight In Dense
Fog** of their sight range (55%), linearly in between (Systems Editor › World › Senses & Stealth). The fog's density
comes from the scene's volume times the weather, so a misty raid is a sneakier raid.

## See-through occluders (Gord)

Trees, roofs and rocks between the camera and the player dissolve in a dithered circle around the player; the rest of
the object and its shadow stay, and nothing behind the player is touched.

- `SeeThrough` on the PlayerArmature sets where (radius 3.5 m at the player, 1.2 m at the camera, soft edge, a 1.6 m
  margin so the player's own body stays). Turn it off for a moment with `SeeThrough.Active` (it fades).
- The cut is in the shaders (`Shaders/SeeThrough/SeeThrough.hlsl`): Tree Wind and Layered Lit have it, and URP Lit and
  Unlit materials use the project's copies **Safe Haven/Lit (See-Through)** and **Safe Haven/Unlit (See-Through)**,
  which keep every property and keyword (so a material looks the same). Shadow casters never cut.
- **Safe Haven › Rendering › Make Materials See-Through › Environment** converts every URP Lit/Unlit material in the
  environment folders (forest kit, resource nodes, stations, Haven props, structures); **› Selected** converts the
  selected materials or prefabs (a new tree, a new building); **› Revert Selected** goes back. Characters, creatures,
  effects and UI are left alone.
- The two copies come from URP 17.6's own shaders: when URP is upgraded, regenerate them (the header in each says how).
- Amplify shaders (Forest Ground, Mossy Stone) don't cut yet; boulders rarely hide the player.

## Raids at day, dusk or night

A raid's **Time of Day** list (Content Editor › Raids › Rules) holds the hours it can take place at, each with a chance,
a loot multiplier and a briefing line. The hour is rolled from the run's seed when the portal's briefing opens, shown
there ("Time of day: Night (23:30) +40% cache loot"), and kept for the raid (`RaidTimeOfDay`, `RaidSession.TimeOfDay`).
The raid scene's sky rig has a `RaidSky` that sets that hour and stops the clock. At night the veil and the darker
night volume (`MistyWoods_Night`) come with the day/night cycle, and monsters see less far (**Sight At Night**, 75%,
on top of fog); they hear as well as ever. Misty Woods and Troll Valley: Day 55%, Dusk 25% (+15% loot), Night 20%
(+40% loot). A new raid scene gets the rig with Safe Haven › World › Sky and Weather › Add Day, Night and Weather to
Scene, then a `RaidSky` on it.

## Tuning

| To change | Edit |
|---|---|
| How thick, how high, how patchy the mist is, how it drifts | **Atmospheric Fog** on the scene's volume (and the night volume) |
| How bright fire looks in the fog | **Local Lights** on Atmospheric Fog; a light's own **Fog Strength** on `AtmosphereLight` |
| How dark unexplored ground is | **Fog of War › Unexplored** and its colour |
| The night veil | **Fog of War › Night Veil**, desaturation, darkening and **Veil Haze** (the grey shimmer) |
| How far the player sees at night / reveals | `VisibilitySource` on the PlayerArmature prefab |
| A torch's reach | The Torch prefab's `Light` (range) and `VisibilitySource` |
| Night creatures: what, how many, how often, how far | `NightProwlers` in SafeHaven |
| Cost / quality | **Steps** and **Max Distance** on Atmospheric Fog, **Fog Resolution** on the renderer feature |

## Not yet

- A hand-held torch for the player, and night spawns in raids.
- Some resource-node props (Elderwood) use an unlit material, so they stay dark at night; a lit material would let
  moonlight and torches reach them.

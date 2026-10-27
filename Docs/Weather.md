# Day, night and weather

A scene's **Sky and Weather** rig lights it through a 24-hour day and changes its weather. It is visual only: no
gameplay reads the time or the weather yet, and neither is saved (a loaded game starts at the start hour and start
weather).

## Use

1. Open the scene and run **Safe Haven > Environment > Weather > Add Day, Night and Weather to Scene**. It creates
   the data assets if they are missing and adds a `Sky and Weather` object wired to the scene's directional light and
   Moss Wind. SafeHaven.unity already has one.
2. Preview in the editor:
   - **Day Night › Start Hour** shows any time of day.
   - **Weather › Start Weather** shows a weather (Clear, Breezy, Mist, Rain, Storm).
   - In Play mode the clock runs (24 real minutes per day) and the climate changes the weather every few minutes.
3. From code:
   - `DayNightCycle.Hour` (read it, or set it to jump the clock, e.g. after sleeping), `IsNight`, `ClockRuns`.
   - `WeatherSystem.SetWeather(weather, seconds)` (e.g. a storm before a ward breach); the `Changed` event; `Current`.

## Developer tools

- **Safe Haven > Environment > Sky Controller** (window), in Play mode or as an edit-mode preview:
  - drag the hour, or jump to dawn, morning, noon, golden hour, dusk or night;
  - run the clock ×1, ×10, ×60 or ×240, or pause it;
  - switch weather instantly or blended over a few seconds;
  - stop the climate changing the weather;
  - strike lightning.
  In edit mode, **Make This the Start Hour / Start Weather** saves the preview as the scene's start.
- **Unity CLI:** `unity command sh_sky -- --hour 21 --weather Rain --transition 10 --speed 60 --lightning true`. Run
  it with no arguments to show the current time and weather.

## Player HUD

SafeHaven's **Screen Canvas > Environment HUD** reads the existing scene clock and weather; it does not run a second
simulation. The top-centred strip shows the current phase and clock, a small vector weather icon and the weather's
authored display name. The thin 24-hour line has a moving dot, coloured segments and ticks at each phase boundary.
Labels below each segment give its name and start time. The two night segments join across midnight.

- Dawn starts at the profile's Sunrise; day starts two game hours later. Dusk starts two hours before Sunset, and
  night starts at Sunset. On short days, dawn/dusk each take at most a quarter of daylight. These are presentation
  labels, not new gameplay rules; the light still blends continuously using the existing profile.
- Clear conditions use a sun by day and moon by night. Wind, mist, cloud, rain and storm symbols are chosen from
  the blended weather values, not filenames. During transitions the text says **To [weather]**, while the icon follows
  the conditions currently visible. There is no forecast.
- The strip does not intercept clicks and sits below inventory/pause windows. It inherits Screen Canvas scaling.
- **Safe Haven > UI > Add Environment HUD to Scene** adds it to another scene that already has a Screen Canvas and
  configured sky rig. Running it again returns the existing HUD without duplicating it or resetting sky settings.

## What each part does

| Part | Does |
|---|---|
| `DayNightCycle` + **Safe Haven Day Night** (`Data/Environment`) | Moves the directional light (the sun by day; the same light is the moon by night) and sets its colour and strength, ambient light, fog colour and density, sky reflections and the Night / Golden Hour volume weights from the profile's curves over the 24 hours. Sets `_SafeHavenNight` for glowing plants |
| `WeatherSystem` + **Safe Haven Climate** + weathers (`Data/Environment/Weather`) | Blends from one weather to the next (Climate › Transition Seconds) and picks the next weather by chance when one has lasted its duration. Drives the Moss Wind's strength and gusts (grass, trees, moss and water chop follow), the rain, the Overcast volume and lightning |
| `RainEffect` (Art/Generated/Weather/Rain prefab) | Rain streaks and ground splashes around what the camera looks at, slanted by the wind. Sets `_SafeHavenRain` (rain rings on water) |
| Volumes (`Settings/Rendering/Volumes`) | `SafeHaven_Night` (cool, desaturated, vignette, stronger bloom so lights and glowing plants stand out), `SafeHaven_GoldenHour` (warm, soft split toning), `SafeHaven_Overcast` (grey, flat). They only override what they change, on top of the scene's own `SafeHaven_Volume` |

## Tuning

- **Day length, sunrise/sunset, sun height and the noon direction:** the profile. The noon direction should cast
  readable shadows from the game camera.
- **Colours over the day:** the profile's gradients (0..1 = 0..24 h) and curves (in hours). Keep their dawn and
  dusk keys near sunrise and sunset.
- **Night brightness:** Moon Intensity and the night ends of the ambient gradients. Raise them if nights are too dark
  to play.
- **Shadow readability:** the profile's **Shadow Fill** multiplies all the ambient light (the only light in shade;
  1.35) and **Shadow Darkness** sets how dark the sun's and moon's shadows get in clear weather (0.8; clouds soften
  them further). Raise Shadow Fill or lower Shadow Darkness if things in shade are hard to read.
- **Golden hour:** the sun's colour near sunrise and sunset is a warm gold, not deep orange, because green grass under
  orange light turns muddy. **Lowest Light Angle** (12°) keeps the light from grazing flat ground while the sun is
  low. The light still fades out at sunrise and sunset.
- **A weather's look:**
  - Wind multiplies the Moss Wind.
  - Clouds dim the sun and soften shadows.
  - Fog multiplies the hour's fog, and Fog Tint mixes in by its alpha.
  - Lightning Per Minute sets the strike rate.
  - Min/Max Minutes set how long it lasts.
- **How often each weather comes up:** the climate's Chance. Make another climate (Create › Safe Haven ›
  Environment › Climate) for a raid region and assign it to that scene's Weather.

The ambient light is applied as a computed ambient probe each frame, because Unity only rebuilds the probe when
lighting is baked. Fog here is Unity's distance fog. The volumetric fog, fog of war and night veil are separate (see
[atmosphere](Atmosphere.md)); the veil follows `_SafeHavenNight`, set here.

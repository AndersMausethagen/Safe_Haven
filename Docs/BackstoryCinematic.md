# Opening cinematic: "The Fire at the Stones"

**Status:** built 2026-11-16 from the user's script below ("ta en titt på introen, og endre den til dette forslaget,
generer bilder som passer til hver scene"; music as before). It replaced "The Old Story" (2026-11-10, twelve shots, 1:56),
whose script and paintings are in git history. It plays on New Game, before the prologue, and from the journal's Backstory
tab and the main menu's Cinematics. Hold Esc, Space or Enter to skip. How it was built, and what differs from the script,
is in [As built](#as-built) at the end. Nobody has watched or heard it yet.

## The script (the user's, unchanged)

THE FIRE AT THE STONES
Opening cinematic · Target length: approximately 1:35

Narrator: The Portal. Close and quiet, as if recalling something it has nearly lost. It never names itself.

**1. A Small Fire**

Picture: Black frame. An ember glows. It catches into a small fire beside a carved stone. A thin ring of light travels
outward from the rune.

> A fire is a small thing, little one.
> Until it is all that stands between home and the dark.

Sound: Fire, close. A low stone hum begins beneath it.

**2. The Bargain**

Picture: The old clearing in summer. People stand on one side of the stones; root-and-moss guardians wait at the tree
line. Keep the dark beyond the guardians indistinct.

> Your people made a bargain with the woods.
> They would leave it room to live.
> Its guardians would hold the older hunger beyond the trees.

Sound: Wind in the pines. Birdsong, warm but restrained.

**3. The Ward**

Picture: The hearth beside the stones. The flame brightens a rune; a soft line of light reaches toward the village homes.

> Beside my stones, they kept a hearth.
> Their flame fed my ward.
> My ward kept the night from their doors.

Sound: A log settles. The stone answers with a gentle pulse.

**4. The Years**

Picture: The same clearing in summer, autumn and winter. Keep the hearth and the rune in the same place through every
season. Let small signs of life appear in the homes.

> For generations, each kept their part.

Sound: Children in the distance; harvest sounds; winter wind. No extra narration.

**5. The Fire Dies**

Picture: Match the hearth's earlier framing on a winter night. The flame disappears. The rune dims. No person is shown
tending or abandoning the fire.

> Then, one winter, the fire died.

Sound: The music cuts. A small hiss from the ember. Hold silence.

**6. The Road Changes**

Picture: The marker stone on the forest path. Roots cover the trail; mist closes across it.

> The ward thinned.
> The old road began to turn.

Sound: Wind and a creak of wood. A low note returns, cold and uncertain.

**7. The Name**

Picture: A carved mark beside a doorway, with the notches of a child's height below it. Moss obscures the mark; the
village behind is empty.

> The lost were still out there.
> But little by little, the way to them was forgotten.

Sound: Let the wind fall away after "forgotten."

**8. The Clearing**

Picture: Pull back from the stones. The green clearing remains, ringed by dark forest. The ward pulses more slowly, then
fades.

> I held one clearing for as long as I could.
> A hundred winters passed.
> My light grew thin.
>
> *A pause.*
>
> Then I slept.

Sound: The stone hum slows to silence.

**9. The First Four**

Picture: Four travellers enter the woods: a satchel of bottles, a hammer, a saw, a staff. Their lanterns disappear one at
a time.

> Much later, four walked the old road to learn what had changed.
> Their lights went in.
> None came back.

Sound: Footsteps recede. Lanterns go out. No musical swell.

**10. The Ones Who Follow**

Picture: Four more travellers gather at a small morning fire. The hooded figure nearest the viewer is the player. Beyond
them, the road leads to the forest.

> Four more prepared to follow.
> One of them was you.
>
> *A pause.*
>
> Why you came is yours to say.

Sound: Birds return. Keep one faint, uneasy note beneath them.

**11. The Road East**

Picture: Match the campfire and framing to the opening of the playable prologue. Beyond the group, the trees stand
still. A broad shadow passes briefly over the standing stones—or the fire bends toward them.

> At dawn, you will take the old road east.
> The woods have already begun to change.

Sound: Fire crackle joins the morning ambience. Cut directly into gameplay.

## As built

2026-11-16. Checked in Play from captured frames of every shot (not watched in motion, not heard).

- **Length:** 2:06 (target 1:35). The narration is 62 s; the portal's voice speaks briskly (about 190 words a minute), so
  the quiet comes from the pauses between lines. It was 1:45 until the violin was given room (below).
- **Voice:** your lines word for word, one clip a sentence, in the portal's own voice (Qwen3-TTS, `portal_g_mysterious`, as
  its dialogue at home), best of three takes each (all 78 takes said their words). `Tools/Audio/opening_voice.py`.
- **Paintings:** LocalSpawn's Safe Haven styles, as the other cinematics (`Tools/LocalSpawn/generate_backstory.py`).
  Shots 10 and 11 are painted without the Home style, which always adds cabins. Shot 11 is painted over the game's own
  first view of the prologue (captured in Play, the HUD hidden), so the cinematic ends on the picture the game opens on.
- **1. A Small Fire:** black with one ember; the painting comes up as the fire catches (1.5 to 3 s), then three widening
  glows from the rune make the ring of light. Fire, ignition and the stone's hum.
- **2. The Bargain:** grown villagers by the stones, three tall root-and-moss guardians at the tree line. The runes breathe.
- **3. The Ward:** the rune brightens on "Their flame fed my ward" with the stone's pulse; on the next line a soft light runs
  along the path to the cabins and their doors warm.
- **4. The Years:** three whole paintings fading summer to autumn to winter, hearth and arch in place. No children's
  voices (there is no recording, and they can't be synthesised here): birds, wood split, a sack, a cart, winter wind.
- **5. The Fire Dies:** the painted flame and the rune's glow go out as the line ends: an unlit copy of the hearth, made
  from the painting's own layers (`Tools/LocalSpawn/opening_unlit.py`), fades in over it, leaving smoke and a few embers.
  The ember's hiss, then silence: the music stops before the shot.
- **6. The Road Changes:** roots across the path, marker stones, the mist closing in. The cold music comes in under it.
- **7. The Name:** marks carved on a door with moss over them and an empty village behind; the child's-height notches
  don't read clearly in the painting.
- **8. The Clearing:** the largest move, pulling back from a ring of standing stones (not the arch) to a green clearing in
  dark forest; the ward pulses slower and is gone on "Then I slept", as the stone's hum slows to nothing.
- **9. The First Four:** three lanterns (the staff-bearer carries none), out one at a time, each with its own small sound,
  the last on "None came back". Footsteps fade.
- **10. The Ones Who Follow:** four at the morning fire, the hooded one nearest, the road to the forest beyond. Birds,
  and the Misty Woods drone held very low as the uneasy note.
- **11. The Road East:** a dark band sweeps across once on the last line (the broad shadow), with a rush of air. The fire
  does not bend.
- **Into the game:** a 1.2 s fade to black, the loading screen (the prologue has been loading behind the cinematic all
  along), then the prologue opens on the same view. A straight cut would need the loading screen to stay behind the
  cinematic's black; not done.
- **Music:** as the opening had it: Nightsong under 1 to 4, silence at 5, Eerie raid (quieter) under 6 to 9, Nightsong
  from 10. **The violin between the lines** (the user: "start the music from the game in the beginning. I really like that
  violin sound that comes in between the dialogue. try and time it with that"): Nightsong's first 22 s are a near-silent
  build, so it now starts 21.35 s in, from the first second, and its first violin phrase enters as the fire catches. A
  phrase enters every 6 to 9 s; where (seconds of the track, the 500-3000 Hz share of its harmonic part): 23.15, 31.14,
  38.77, 45.42, 54.43, 61.61, 68.23, 76.28, 85.14. Every line's pause is set so that nothing is said from 0.25 s before an
  entry until 1.6 s into it; a line may run on over a phrase's tail, where the music dips under the voice. The second
  Nightsong starts 61 s in, so a phrase enters 0.6 s after the cut to the morning fire. `CinematicTests.TheBackstory_
  LeavesRoomForNightsongsViolin` keeps it so when a line is spoken again.
- **Made sounds:** stone hum, its pulse, the hum slowing to silence, the ember's hiss, a lantern going out, the shadow
  (`Tools/Audio/opening_cinematic.py`). The fire, wind, birds, creaks and footsteps are the game's own.
- **How it was put together:** the asset (`Data/Cinematics/Backstory.asset`) was written once by a one-off script from
  `Editor/Cinematics/CinematicBuilder`'s parts (builders are retired); change the asset now. The old cinematic's
  paintings and voice clips are still in the project, unused.

# First-raid cinematic: "The Misty Woods"

**Status:** built 2026-11-14. It plays the first time a character enters the Misty Woods (once per save), as the
loading screen lifts and before the raid clock starts. Checked in Play on throwaway saves; nobody has listened to it or
watched it whole yet.

The user's brief: in the style of the opening, show the Misty Woods and explain in lore terms, not mechanics, why the
player is there, what the place is and why they can only stay a short while: it grows more dangerous and draws worse
things the longer they stay. Not a place to stay: take what you need, keep out of trouble, get back before it is too late.

## 1. The frame

| | |
|---|---|
| **Plays** | First visit to the Misty Woods, per save. Not for a test raid (a raid scene played straight from the Editor) and not in co-op, where a pause does not stop the others. Hold Esc, Space or Enter to skip. |
| **Narrator** | The portal, in its dialogue voice (portal_g_mysterious), as the opening and The Catch are. |
| **Length** | 54 s (34 s of speech). |
| **Ends on** | A fade from black into the game: the player at the forest entrance, fog rolling round them, a faint unsettling sound, and nothing noticing them for 8 seconds. |
| **Shows** | No countdown, timer or mechanics. The raid's own "reach an extraction" message comes after it, in the game. |

## 2. The shots

The words are the user's, unchanged.

| # | Painting | Narration | Moving details |
|---|---|---|---|
| 1 | The last marker stone at the edge of the forest, a path into fog | "Beyond the reach of the Ward, lies a place where the light has long since faded." | The stone's light (the Ward) dies as the line ends; the camera walks in; moonlight comes and goes. |
| 2 | Rows of trunks, each paler than the last, until only fog | "The Misty Woods swallow all who wander too deep." "Its shadows hide things best left undisturbed." | A slow track past the trunks. A glint of eyes at the very edge early on, and a pair in the dark as the shadows are named. A wolf, far off. |
| 3 | Blackened trees, sick glow in the fog, two hooded shapes far back | "The longer you remain, the deeper its corruption grows." "And the more it draws the attention of what lurks within." | The camera creeps in; fog and dark close; the sick glow grows; three pairs of eyes open one after another on the second line. The strings close into a cluster under it. |
| 4 | A split boulder of glinting ore; a cage of branches with someone in it | "You are here for what the forest holds." "Its resources, or its prisoners." | A pan from the ore to the cage. |
| 5 | A traveller crouched behind a log; something vast moves behind the trunks | "Take what you need, save who you can." "Avoid what you cannot fight." | Fog drifts over the shape so it is never wholly there; far footfalls. |
| 6 | The path home, a far warm light at its end | "For this is no place to call home." "Return to Haven… before the forest claims you." | The far light warms as Haven is named (and a faint warm chord), then fog and dark close over it and the picture goes to black. |

Enemies are only shapes in fog and glints at the edges. The painted shot 5 first stood a troll in the middle of the
frame, plain to see (seed 4105): repainted with the shape pushed behind the trunks.

## 3. Sound

- **Bed:** a low drone loop under every shot (sub hum, a low bowed fifth, far wind) and the raid's own *Misty woods -
  Eerie raid* far beneath it for the distant strings. Both duck under the voice.
- **Uneasy at the corruption line:** low strings closing into a semitone and a tritone, a tremolo that quickens, glass
  harmonics rubbing, a rumble rising (shot 3), with a draug's moan and a huldra's wail far off.
- **Warm at "Return to Haven":** a soft, far D major chord of strings (shot 6).
- **Woods:** the wilds' wind in every shot, an owl, a tree creak, a far wolf howl, wings, troll footfalls, a gust as the forest closes.
- **Arrival:** a slow breath through the trees, a far creak and a thin falling tone, all round the player as the picture comes up.

## 4. How it is made

| What | With | Where |
|---|---|---|
| Paintings and layers | `Tools/LocalSpawn/generate_misty_woods.py` (LocalSpawn's "Safe Haven - Raid" style, z-image, cut by depth like the opening) | `Tools/LocalSpawn/MistyWoods/`, `Assets/_Project/Art/Cinematics/MistyWoods/` |
| Narration | `Tools/Audio/misty_woods_voice.py` (Qwen3-TTS "shaped", portal_g_mysterious, three takes, `narration.py` picks; the last line's halves are spoken apart and joined round a pause, as the model ran through the ellipsis) | `Assets/_Project/Audio/Voice/MistyWoods/` |
| Drone, unease, warmth, arrival | `Tools/Audio/misty_woods_cinematic.py` (synthesised) | `Assets/_Project/Audio/SFX/Safe Haven Cinematics/` |
| The cinematic | made once with `CinematicBuilder`'s parts; the asset is the source of truth now (builders are retired): edit it | `Assets/_Project/Data/Cinematics/MistyWoods.asset` |
| The hook | `RaidDefinition` › First Visit: the cinematic, Calm After Cinematic (8 s), Arrival Effect (`Prefabs/Raids/Arrival Mist`), Arrival Sound (`Audio/Sounds/Misty Woods Arrival`) | `Assets/_Project/Data/Raids/MistyWoods.asset` |

The visit is kept in the save (`CharacterProgress.RaidsVisited`, `SaveData.raidsVisited`). Saves from before have none,
so a character who raided before this was added sees it once on their next raid.

## 5. Left to judge by ear and eye

1. **Pace.** The portal reads some lines quickly (m01a and m06a at over 200 words a minute); it is the model's own pace,
   which the user asked never to slow. If a line feels hurried, another take can be picked from the raw takes.
2. **Levels** of the drone, the unease swell and the warm chord against the voice (measured, not heard).
3. **Length.** 54 s against the 26 s sketched: the pauses between lines and the holds can be trimmed in the asset.

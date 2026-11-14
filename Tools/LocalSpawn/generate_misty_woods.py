"""Paint the first-raid cinematic's shots with LocalSpawn and cut each painting into layers (Docs/MistyWoodsCinematic.md).

The work is generate_backstory.py's (the same phases, size and layer cutting); this file only says what is painted and
where it goes:
  paintings -> Tools/LocalSpawn/MistyWoods/<shot>.png
  layers    -> Assets/_Project/Art/Cinematics/MistyWoods/<shot>_<n>_<name>.png and layers.json

The look is the user's direction: desaturated greens, dark blues and grey, very little light and now and then moonlight,
fog in layers that hides more than it shows, never the whole forest at once, and the creatures only as shapes in the
fog. Every shot is in LocalSpawn's "Safe Haven - Raid" style. Glints of eyes, the Ward's light and Haven's far glow are
added in the cinematic, not painted.

Run with LocalSpawn's Python (the image model runs in VRAM: Unity can stay open):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/generate_misty_woods.py [paintings|layers|all] [shot ...] [--force]
"""
import os

import generate_backstory as base

RAID, EMPTY = base.RAID, base.EMPTY
COLD = " Desaturated dark greens, deep blues and greys, very little light."
RUNES = (" The carvings on the stone are a few simple angular rune marks made of straight scratched strokes, worn and "
         "half hidden by moss: no alphabet letters, no words.")

base.PAINTINGS = os.path.join(base.HERE, "MistyWoods")
base.ART = os.path.join(base.ROOT, "Assets", "_Project", "Art", "Cinematics", "MistyWoods")
base.SHOTS = {
    # "Beyond the reach of the Ward...": the last marker stone, and the dark the path walks into.
    "mw1_ward": dict(style=RAID, seed=4101, layers=4, prompt=(
        "Night at the very edge of a vast ancient forest. In the near foreground on the left stands a single tall "
        "weathered standing stone, the last pale moonlight on its side. A narrow footpath runs past it into the forest "
        "beyond: enormous dark mossy trunks, and between them thick grey-blue mist that swallows the path after a few "
        "steps. Ferns and long grass at the bottom edge." + COLD + EMPTY + RUNES)),
    # "The Misty Woods swallow all who wander too deep": rows of trunks, each paler than the last, until only fog.
    "mw2_swallow": dict(style=RAID, seed=4102, layers=4, prompt=(
        "Deep inside a dark ancient forest. Enormous mossy pine trunks stand in rows that fade one behind another into "
        "thick layered fog, each row paler and greyer than the last, until only fog remains. Tangled roots and ferns "
        "across the ground, a faint narrow path disappearing into the fog. One thin shaft of cold moonlight falls "
        "through the canopy." + COLD + EMPTY)),
    # "...the deeper its corruption grows. And the more it draws the attention of what lurks within."
    "mw3_corruption": dict(style=RAID, seed=4103, layers=4, prompt=(
        "A corrupted part of a dark forest at night: twisted blackened trees with bark split open, black veins running "
        "through the moss and the roots, pale sickly fungus on the trunks, a faint sickly grey-green glow low in the "
        "thick fog between the trunks. Far back in the deep fog, between the trunks, two tall hunched dark shapes, "
        "only vague silhouettes blurred by the fog. Oppressive and cold." + COLD)),
    # "You are here for what the forest holds. Its resources, or its prisoners."
    "mw4_holds": dict(style=RAID, seed=4104, layers=4, prompt=(
        "A small misty hollow in a dark forest at night. In the foreground a mossy boulder split open, veins of pale "
        "blue-white crystal ore glinting faintly inside it, wild herbs growing at its foot. Further back in the mist, "
        "under a huge dark tree, a crude cage of lashed branches with a small hunched figure sitting inside, only a "
        "dark shape. Cold moonlight, thick mist." + COLD)),
    # "Take what you need, save who you can. Avoid what you cannot fight." The first painting (seed 4105) stood the
    # troll in the middle of the frame, big and plain to see: the user wants enemies only as shapes at the edges.
    "mw5_avoid": dict(style=RAID, seed=4115, layers=4, prompt=(
        "Night in a dark misty forest. In the near foreground on the left a small hooded traveller in a wool cloak "
        "with a pack crouches low among ferns behind a mossy fallen log, seen from behind, watching the fog ahead. "
        "Far off on the right, behind two enormous dark trunks and deep in thick fog, a vast hulking shape moves past: "
        "only the faint outline of a huge shoulder and a long arm, grey on grey, mostly hidden, easy to miss. "
        "No face, no eyes. The middle of the picture is empty fog. Cold blue moonlight." + COLD)),
    # "For this is no place to call home. Return to Haven... before the forest claims you."
    "mw6_return": dict(style=RAID, seed=4106, layers=4, prompt=(
        "Night. Looking back along a narrow path through a dark misty forest. Far, far away at the end of the path, "
        "through layers of grey fog, a tiny faint warm golden glow between the trees, the only warmth in the picture. "
        "Enormous dark trunks close in on both sides in the near foreground, black branches reaching inward. Cold "
        "blue-grey mist everywhere else." + COLD + EMPTY)),
}

if __name__ == "__main__":
    base.main()

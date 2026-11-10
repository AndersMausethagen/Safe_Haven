"""Paint the prologue-end cinematic's shots with LocalSpawn and cut each painting into layers (Docs/PrologueEndCinematic.md).

The work is generate_backstory.py's (the same phases, styles, size and layer cutting); this file only says what is
painted and where it goes:
  paintings -> Tools/LocalSpawn/PrologueEnd/<shot>.png
  layers    -> Assets/_Project/Art/Cinematics/PrologueEnd/<shot>_<n>_<name>.png and layers.json,
               which AgentScripts/BuildPrologueEndCinematic.cs reads.

Run with LocalSpawn's Python (Unity closed for the paintings: the image model needs about 13 GB of RAM):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/generate_prologue_end.py [paintings|layers|all] [shot ...] [--force]
"""
import os

import generate_backstory as base

HOME, RAID, EMPTY = base.HOME, base.RAID, base.EMPTY
RING = "a ring of tall cracked standing stones carved with runes"
ARCH = "an ancient arch of tall cracked standing stones carved with runes"
WILD = " A wild place: no buildings, no cabin, no house, no fence, no road."
# LocalSpawn's "Safe Haven - Home" style as it is written in its styles.json, without the cabins.
HOME_LOOK = (" Stylized but tangible Nordic fairy-tale game art for the game Safe Haven. Hand-painted look with soft, "
             "readable shapes and tactile materials: mossy pines, birch trees, weathered stones. Fresh greens, soft "
             "golden sunlight, cozy and inviting storybook atmosphere. Painterly concept art, not photorealistic, no "
             "modern objects.")
RUNES = (" The carvings on the stones are a few simple angular rune marks made of straight scratched strokes, worn and "
         "half hidden by moss: no alphabet letters, no words.")

base.PAINTINGS = os.path.join(base.HERE, "PrologueEnd")
base.ART = os.path.join(base.ROOT, "Assets", "_Project", "Art", "Cinematics", "PrologueEnd")
base.SHOTS = {
    "c01_hand": dict(style=RAID, seed=3201, layers=3, prompt=(
        "Close-up at night in a rainstorm. A traveller's bare hand, seen from the side, pressed flat against a tall "
        "weathered standing stone. The runes carved in the stone under and around the palm glow warm gold; the rest "
        "of the stone is dark and wet. A wool sleeve at the wrist. Rain streaks, deep blue-black forest behind, soft "
        "and out of focus.")),
    "c02_old_one": dict(style=RAID, seed=3202, layers=4, prompt=(
        f"Night, a storm. A forest clearing with {RING}, four small travellers with torches standing inside the ring, "
        "seen from behind. Far behind them, towering above the tops of the enormous pines, the huge dark silhouette "
        "of a mountain troll, hunched, its head and shoulders against a pale flash of lightning in the clouds. "
        "Rain, blue-black sky, dark tree trunks in the near foreground on both sides.")),
    "c03_break": dict(style=RAID, seed=3203, layers=3, prompt=(
        f"Night. {RING.capitalize()} breaking apart: a blinding burst of white-gold light at the centre of the ring, "
        "the stones cracking and leaning outwards, shards of rock and torn roots flung through the air, four small "
        "dark silhouettes of travellers thrown backwards against the light. Rain lit gold by the burst, black forest "
        "all around.")),
    "c04_scattered": dict(style=RAID, seed=3204, layers=2, prompt=(
        "Aerial view from very high above an endless dark pine forest at night, blue-black treetops to every edge, "
        "long pale bands of mist lying between the hills, a faint winding river. No lights anywhere, cold moonlight "
        "through storm clouds." + EMPTY)),
    "c05_catch": dict(style=RAID, seed=3205, layers=3, prompt=(
        "A small hooded traveller in a wool cloak falling through darkness, seen from the side, arms out. Dark "
        "grasping roots reach in from the edges of the picture. Below and behind the falling figure a tall arch of "
        "warm golden light opens in the dark, soft rays reaching up towards the figure like open hands. Drifting "
        "golden motes. Black and deep blue everywhere else.")),
    # The clearing is empty when the player wakes, and the Home style's own words name "timber cabins with turf
    # roofs": the first painting had one cabin, and telling it "no cabin" gave three. So this shot is painted
    # without a saved style, with the Home look written out here minus its buildings. The first also wrote
    # near-letters on the arch ("K?S ROVE"): the carvings are described by their strokes.
    "c06_clearing": dict(style="None", seed=3226, layers=4, prompt=(
        f"Dawn. A small green forest clearing ringed by enormous dark ancient pines. In the middle stands {ARCH}, and "
        "at its foot a small hooded traveller in a wool cloak lies asleep on their side in the grass. A narrow stream, "
        "wildflowers, loose stones and fallen branches in the grass. Soft pink and gold morning light, a little mist "
        "at the tree line. Tall grass in the near foreground. Untouched wilderness: only trees, grass, stones and "
        "water." + RUNES + HOME_LOOK)),
    "c07_lost": dict(style=RAID, seed=3207, layers=4, prompt=(
        "Deep inside a vast misty forest in cold blue-grey light. Far away between enormous mossy tree trunks stands "
        "one small lone traveller, seen from behind, holding a dim lantern, lost. Thick pale mist, tangled roots "
        "across the ground, huge dark trunks in the near foreground on both sides.")),
    # A cabin at the edge is welcome here: this is the hearth to light and the home to build.
    "c08_hearth": dict(style=HOME, seed=3218, layers=4, prompt=(
        f"Dusk in a small forest clearing. A small campfire burns brightly in a ring of stones in the grass beside "
        f"{ARCH}. Its warm amber light reaches the trunks of the enormous dark pines at the clearing's edge, and "
        "pale mist stands back between them. Deep blue evening sky with the first stars. Grass in the near "
        "foreground." + EMPTY + WILD + RUNES)),
}

if __name__ == "__main__":
    base.main()

"""Paint the opening cinematic's shots ("The Fire at the Stones") with LocalSpawn and cut each painting into layers (Docs/BackstoryCinematic.md).

Phases (each skips what already exists, so it can be stopped and rerun; --force repaints the shots named):
  paintings  one 1920x1088 painting per shot, in LocalSpawn's Safe Haven styles        -> Tools/LocalSpawn/Backstory/<shot>.png
             A shot with "from" is redrawn from another shot's painting, so the two share a composition
             (the hearth lit and cold, the village through the seasons).
  layers     a depth estimate splits each painting into layers by distance; what a nearer layer hid is filled in,
             so layers can slide over each other                                        -> Assets/_Project/Art/Cinematics/Backstory/<shot>_<n>_<name>.png
             plus layers.json (each layer's depth), which AgentScripts/BuildBackstoryCinematic.cs reads.

Run with LocalSpawn's Python (Unity may stay open: the image model runs in VRAM):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/generate_backstory.py [paintings|layers|all] [shot ...] [--force]

The paintings phase keeps LocalSpawn's ComfyUI up between shots and stops it when done.
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
AGENT = r"D:\ComfyUI\agent"
PYTHON = r"D:\ComfyUI\.venv\Scripts\python.exe"
PAINTINGS = os.path.join(HERE, "Backstory")
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Cinematics", "Backstory")
WIDTH, HEIGHT = 1920, 1088
MODEL = "z-image"
HOME, RAID = "Safe Haven - Home", "Safe Haven - Raid"
# Said of every shot, so the paintings read as one set.
LOOK = ("Storybook gouache illustration, visible brushwork, soft edges, very large trees, clear depth: a distinct "
        "foreground, middle and far distance. No text, no letters, no writing, no border, no frame.")
# The model likes to add a wanderer to an empty place. Said of the shots where nobody may be.
EMPTY = " Nobody is there: no people, no figures, no animals."
STONES = "an ancient arch of tall cracked standing stones carved with runes"

# name -> style, seed, what is painted; "source"/"strength" redraw another painting (another shot's, or o11_reference, the
# game's own first view of the prologue); "layers" is how many distances it is cut into (1 = left whole).
# The opening "The Fire at the Stones" (the user's script, 2026-11-16: Docs/BackstoryCinematic.md).
SHOTS = {
    "o01_small_fire": dict(style=RAID, seed=3101, layers=2, prompt=(
        "Close, low view at night, almost all darkness. In the grass a small fire of a few sticks has just caught, "
        "beside a low weathered standing stone carved with one rune. Warm amber firelight falls only on the stone, "
        "the rune and the grass around the fire; everything beyond fades into deep black." + EMPTY)),
    # Seed 3102 painted three small children and two small moss figures; the grown people and the guardians' height
    # are said twice over.
    "o02_bargain": dict(style=HOME, seed=3122, layers=4, prompt=(
        f"Wide view of a sunlit summer forest clearing ringed by enormous ancient pines. In the centre stands {STONES}, "
        "the runes glowing faintly gold. On the left side of the stones stand grown men and women of a village, adults "
        "in long wool cloaks, seen from behind. On the right side, at the tree line, wait three towering guardians made "
        "of roots, bark and moss, each twice as tall as a man, heads bowed, still as trees. Behind the guardians the "
        "forest goes deep and dark and cannot be made out. Shafts of golden sunlight fall onto the stones between the "
        "two sides. Dark tree trunks and ferns frame the left and right edges in the near foreground.")),
    "o03_ward": dict(style=HOME, seed=3103, layers=4, prompt=(
        "Low view at dusk across a small ring of hearth stones with a warm fire burning in it, at the foot of a tall "
        "weathered standing stone carved with runes, the runes catching a soft gold glow. Behind, a little way off and "
        "softly lit, a few timber cabins with turf roofs and warm glowing doorways, and a footpath through the grass "
        "from the hearth to them. Grass blades in the near foreground. Deep blue dusk sky.")),
    "o04_summer": dict(style=HOME, seed=3104, layers=1, prompt=(
        f"Seen from a little above: a small village of a few timber cabins with turf roofs around {STONES} and a "
        "burning hearth fire beside the stones, in the middle of a forest clearing. Signs of life: washing on a line, a "
        "handcart, firewood stacked by a door, a vegetable patch. High summer, green grass, chimney smoke, warm light.")),
    "o04_autumn": dict(style=HOME, seed=3105, layers=1, source="o04_summer", strength=0.55, prompt=(
        f"Seen from a little above: a small village of a few timber cabins with turf roofs around {STONES} and a "
        "burning hearth fire beside the stones, in a forest clearing. Autumn: birches gold and red, fallen leaves, "
        "baskets of harvest and sheaves by the doors, low golden light, chimney smoke.")),
    # At 0.8 (seed 3106) the arch changed shape between the seasons; the script keeps hearth and rune in place.
    "o04_winter": dict(style=HOME, seed=3116, layers=1, source="o04_summer", strength=0.7, prompt=(
        f"Seen from a little above: a small village of a few timber cabins around {STONES} and a burning hearth fire "
        "beside the stones, in a forest clearing. Deep midwinter: thick white snow on the ground and every roof, bare "
        "birches, trodden paths, firewood stacked high by the doors, long blue shadows. The hearth fire still burns "
        "warm and orange in the white, chimney smoke rises.")),
    # The hearth of shot 3 on a winter night, redrawn from it so the framing holds. Its flames may stay: the shot opens
    # with the fire burning, and Unity puts it out.
    "o05_fire_dies": dict(style=RAID, seed=3107, layers=4, source="o03_ward", strength=0.62, prompt=(
        "The same low view on a winter night: a small ring of hearth stones at the foot of a tall weathered standing "
        "stone carved with runes, snow on the grass and on the stones. Behind, timber cabins with snow on their turf "
        "roofs and dark windows. Deep blue moonlight, cold and still, falling snowflakes." + EMPTY)),
    "o06_road": dict(style=RAID, seed=3108, layers=4, prompt=(
        "A forest path running away from the viewer between dense mossy pines. Beside it stands a squat old marker "
        "stone carved with a single rune. Thick roots have grown right across the path, almost covering it. Pale mist "
        "is closing across the far end of the path. Cold blue-green light, dark trunks in the near foreground." + EMPTY)),
    "o07_name": dict(style=RAID, seed=3109, layers=3, prompt=(
        "Close view of the weathered timber frame beside a cabin doorway. A family's mark is carved deep into the wood, "
        "and below it a row of small notches marks a child's height, year by year. Moss has grown over the carving, "
        "half hiding it. Past the doorway, an empty abandoned village of dark timber cabins in cold dim light, "
        "a wisp of mist." + EMPTY)),
    "o08_clearing": dict(style=RAID, seed=3110, layers=2, prompt=(
        f"High aerial view down on an endless dark pine forest at dusk, blue-black treetops to every edge. In the "
        f"middle, one small round clearing of green grass with {STONES} in it, a faint golden glow around the "
        "clearing's rim. The clearing is the only light." + EMPTY)),
    "o09_first_four": dict(style=RAID, seed=3111, layers=4, prompt=(
        "Dusk at the edge of a vast misty forest. Four small travellers seen from behind walk in a line into pale mist "
        "between enormous tree trunks, each holding up a lit lantern. One carries a satchel of bottles, one a smith's "
        "hammer, one a saw, one a tall staff. Huge dark trunks in the near foreground on both sides.")),
    # Without the Home style, which paints cabins (seed 3112 had six people round the fire and two cabins); its warm
    # storybook look is written out instead.
    "o10_who_follow": dict(style="None", seed=3132, layers=4, prompt=(
        "Early morning at the edge of an open meadow, painted as a warm storybook gouache illustration in soft greens "
        "and golds. Exactly four travellers in wool cloaks stand around a small campfire, packing their gear: a tall "
        "woman, a broad man, a woman with a bow, and nearest the viewer, seen from behind, a fourth in a hood. Beyond "
        "them a dirt road runs away across the empty meadow to the dark edge of an old pine forest. Soft gold dawn "
        "light, dew on the grass, low mist. Only the four travellers, the fire, the meadow, the road and the forest.")),
    # The game's own first view of the prologue (o11_reference.png, captured in Play), painted over so the cinematic's
    # last picture is the view the game opens on.
    # Without the Home style, which added cabins the game's view doesn't have (seed 3113).
    "o11_road_east": dict(style="None", seed=3123, layers=2, source="o11_reference", strength=0.58, prompt=(
        "A warm storybook gouache illustration in soft greens and golds. Seen from high above at an angle: an open, empty, sunny morning meadow of tall golden-green grass with purple and white "
        "wildflowers and ferns. In the middle a ring of round stones holding a small campfire, a fallen log beside it. "
        "Four travellers stand around the fire: a woman with a long gun, a tall man, a woman with a bow, and nearest "
        "the viewer, seen from behind, a traveller in a wide-brimmed hat. Warm morning sunlight.")),
}


def run(args, timeout=1800):
    result = subprocess.run([PYTHON] + args, cwd=AGENT, capture_output=True, text=True, timeout=timeout,
                            encoding="utf-8", errors="replace")
    return result.returncode, (result.stdout or "") + (result.stderr or "")


def paint(name, shot, force):
    target = os.path.join(PAINTINGS, name + ".png")
    if os.path.isfile(target) and not force:
        return "kept"
    os.makedirs(PAINTINGS, exist_ok=True)
    prompt_file = os.path.join(PAINTINGS, name + ".prompt.txt")
    with open(prompt_file, "w", encoding="utf-8") as stream:
        stream.write(shot["prompt"] + " " + LOOK)
    scratch = os.path.join(PAINTINGS, "_new")
    shutil.rmtree(scratch, ignore_errors=True)
    args = ["generate_image.py", "--prompt-file", prompt_file, "--style", shot["style"], "--model", MODEL,
            "--width", str(WIDTH), "--height", str(HEIGHT), "--seed", str(shot["seed"]), "--name", name,
            "--out-dir", scratch, "--keep-loaded", "--timeout", "900"]
    if shot.get("source"):
        source = os.path.join(PAINTINGS, shot["source"] + ".png")
        if not os.path.isfile(source):
            raise RuntimeError(f"{name} is redrawn from {shot['source']}, which has not been painted yet")
        args += ["--source", source, "--strength", str(shot["strength"])]
    code, output = run(args)
    made = [f for f in (os.listdir(scratch) if os.path.isdir(scratch) else []) if f.lower().endswith(".png")]
    if not made:
        raise RuntimeError(f"{name}: no painting came back (exit {code}).\n{output[-1500:]}")
    shutil.move(os.path.join(scratch, made[0]), target)
    shutil.rmtree(scratch, ignore_errors=True)
    return "painted"


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    force = "--force" in sys.argv
    phase = args[0] if args and args[0] in ("paintings", "layers", "all") else "all"
    names = [a for a in args if a in SHOTS] or list(SHOTS)
    if phase in ("paintings", "all"):
        try:
            # A shot redrawn from another comes after it.
            for name in sorted(names, key=lambda n: bool(SHOTS[n].get("source"))):
                print(f"{name}: {paint(name, SHOTS[name], force)}", flush=True)
        finally:
            code, output = run(["generate_image.py", "--stop"], timeout=120)
            print("ComfyUI:", output.strip().splitlines()[-1] if output.strip() else f"stop exit {code}", flush=True)
    if phase in ("layers", "all"):
        import backstory_layers
        os.makedirs(ART, exist_ok=True)
        record_path = os.path.join(ART, "layers.json")
        record = json.load(open(record_path, encoding="utf-8")) if os.path.isfile(record_path) else {}
        for name in names:
            painting = os.path.join(PAINTINGS, name + ".png")
            if not os.path.isfile(painting):
                print(f"{name}: not painted yet", flush=True)
                continue
            if name in record and not force and all(os.path.isfile(os.path.join(ART, l["file"])) for l in record[name]):
                print(f"{name}: layers kept", flush=True)
                continue
            record[name] = backstory_layers.split(painting, ART, name, SHOTS[name]["layers"])
            print(f"{name}: {len(record[name])} layers " + ", ".join(f"{l['name']} depth {l['depth']}" for l in record[name]),
                  flush=True)
            with open(record_path, "w", encoding="utf-8") as stream:
                json.dump(record, stream, indent=2)


if __name__ == "__main__":
    main()

"""Paint the backstory cinematic's shots with LocalSpawn and cut each painting into layers (Docs/BackstoryCinematic.md).

Phases (each skips what already exists, so it can be stopped and rerun; --force repaints the shots named):
  paintings  one 1920x1088 painting per shot, in LocalSpawn's Safe Haven styles        -> Tools/LocalSpawn/Backstory/<shot>.png
             A shot with "from" is redrawn from another shot's painting, so the two share a composition
             (the hearth lit and cold, the village through the seasons).
  layers     a depth estimate splits each painting into layers by distance; what a nearer layer hid is filled in,
             so layers can slide over each other                                        -> Assets/_Project/Art/Cinematics/Backstory/<shot>_<n>_<name>.png
             plus layers.json (each layer's depth), which AgentScripts/BuildBackstoryCinematic.cs reads.

Run with LocalSpawn's Python (Unity closed for the paintings: the image model needs about 13 GB of RAM):
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

# name -> style, seed, what is painted; "from"/"strength" redraw another shot's painting; "layers" is how many
# distances it is cut into (1 = left whole).
SHOTS = {
    "s02_promise": dict(style=HOME, seed=2101, layers=4, prompt=(
        f"Wide view of a sunlit summer forest clearing ringed by enormous ancient pines. In the centre stands {STONES}, "
        "the runes glowing faintly gold. On the left a small group of villagers in wool cloaks, one of them a child, "
        "seen from behind. On the right, at the tree line, three tall gentle guardians made of roots and moss, heads "
        "bowed. Shafts of golden sunlight fall between the trees onto the stones. Dark tree trunks and ferns frame the "
        "left and right edges in the near foreground.")),
    "s03_hearth": dict(style=HOME, seed=2102, layers=4, prompt=(
        "Close, low view at dusk. A small ring of hearth stones with a warm fire burning in it, in the grass at the "
        "foot of a tall weathered standing stone carved with runes. Behind, soft and out of focus, a timber cabin "
        "with a turf roof and a warmly lit open doorway. Blades of grass in the near foreground. Amber firelight on "
        "the stone, deep blue dusk sky.")),
    "s04_summer": dict(style=HOME, seed=2103, layers=1, prompt=(
        f"Seen from a little above: a small village of a few timber cabins with turf roofs around {STONES} and a "
        "burning hearth fire in the middle of a forest clearing. Trails lead out between the cabins into the trees. "
        "High summer, green grass, chimney smoke, warm light. A pine branch crosses the top corner in the near foreground.")),
    "s04_autumn": dict(style=HOME, seed=2104, layers=1, source="s04_summer", strength=0.55, prompt=(
        f"Seen from a little above: a small village of a few timber cabins with turf roofs around {STONES} and a "
        "burning hearth fire in the middle of a forest clearing. Autumn: birches turned gold and red, fallen leaves on "
        "the trails, low golden light, chimney smoke.")),
    "s04_winter": dict(style=HOME, seed=2105, layers=1, source="s04_summer", strength=0.8, prompt=(
        f"Seen from a little above: a small village of a few timber cabins around {STONES} and a burning hearth fire "
        "in the middle of a forest clearing. Deep midwinter: everything lies under thick white snow, white ground, "
        "white roofs, snow on every branch, bare birches, trodden paths in the snow, long blue shadows, pale "
        "winter sky. The hearth fire still burns warm and orange in the white, chimney smoke rises.")),
    # Painted afresh, not redrawn from the lit hearth: a redraw kept the flames however cold the words were. The
    # words avoid fire altogether, for the same reason.
    "s05_cold_hearth": dict(style=RAID, seed=2126, layers=4, prompt=(
        "Close, low view on a winter night. In the grass at the foot of a tall weathered standing stone carved with "
        "dark runes lies a small ring of round stones holding only cold grey ash and a few blackened, charred logs "
        "dusted with snow. Behind, soft and out of focus, a dark timber cabin with snow on its turf roof, its doorway "
        "black, its windows black. Snow on the grass, a few falling snowflakes. Deep blue moonlight, cold and still, "
        "everything in shades of blue and grey." + EMPTY)),
    "s06a_trail": dict(style=RAID, seed=2107, layers=4, prompt=(
        "A forest trail running away from the viewer between dense mossy pines. Beside it stands a small "
        "moss-covered waymarker stone carved with a single rune. Thick roots have grown across the path. Pale mist "
        "stands at the far end of the trail. Cold blue-green light, dark trunks in the near foreground." + EMPTY)),
    "s06b_names": dict(style=RAID, seed=2108, layers=3, prompt=(
        "Close-up of an old weathered timber doorpost. Runes carved into the wood spell a name, and below them a row "
        "of small notches marks a child's height year by year. Moss creeps over the carving from one side. Behind the "
        "post, a dark empty doorway. Cold dim light, a wisp of mist." + EMPTY)),
    "s06c_empty_village": dict(style=RAID, seed=2109, layers=3, prompt=(
        f"Seen from a little above, deep in the night: an abandoned village of a few timber cabins around {STONES}, in "
        "a forest clearing. Sagging turf roofs, black empty doorways, no fire, no light in any window, knee-high pale "
        "mist drifting between the cabins. Three tall overgrown guardians made of roots and moss stand still among "
        "the houses. Everything is dark blue and grey under cold moonlight." + EMPTY)),
    "s07_clearing": dict(style=RAID, seed=2110, layers=2, prompt=(
        f"Aerial view straight down on an endless dark pine forest at night, blue-black treetops to every edge. In the "
        f"middle, one small round clearing of green grass with {STONES} in it, a faint golden glow around the "
        "clearing's rim. Roots creep towards the stones. The clearing is the only light." + EMPTY)),
    "s08_four_went_in": dict(style=RAID, seed=2111, layers=4, prompt=(
        "Dusk at the edge of a vast misty forest. Four small travellers seen from behind, each carrying a lit lantern, "
        "walk in a line into pale mist between enormous tree trunks. One carries a satchel of bottles, one a smith's "
        "hammer, one an axe and a saw, one a tall staff. Huge dark trunks in the near foreground on both sides.")),
    "s09_four_more": dict(style=HOME, seed=2112, layers=4, prompt=(
        "Open rolling meadow in late-afternoon golden sun, wildflowers, a big sky with clouds, a path over a low hill. "
        "Four travellers in wool cloaks seen from behind walk along the path towards a dark line of forest on the "
        "horizon. A tall woman leads. The last and nearest traveller wears a hood. Tall grass and flowers in the near "
        "foreground.")),
    "s10_morning_fire": dict(style=HOME, seed=2113, layers=4, prompt=(
        "Dawn at the edge of a meadow. A small campfire in the foreground with bedrolls and packs around it, four "
        "travellers in wool cloaks packing up. Beyond them the first huge dark trees of an old forest, mist between "
        "the trunks. Soft pink and gold dawn sky, dew on the grass.")),
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

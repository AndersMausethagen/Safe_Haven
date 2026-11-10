"""Speaks the backstory cinematic's narration with LocalSpawn's Voice generator (Qwen3-TTS), several takes a line.

The voice is Tools/Audio/Voices/portal_f_british_northern (its reference clip and what made it): the portal as an
old woman telling a folktale. It is copied into LocalSpawn's voices if it isn't there. Each sentence is its own clip,
because the model reads a run of short sentences faster than a narrator would and has no speed control: the pauses
between sentences are set where the clips are placed (AgentScripts/BuildBackstoryCinematic.cs).

Run with LocalSpawn's Python, then pick and prepare the takes:
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/Audio/backstory_voice.py [--takes 3] [line id ...]
  D:\\ComfyUI\\qwen-tts\\.venv\\Scripts\\python.exe Tools/Audio/narration.py <takes folder> Assets/_Project/Audio/Voice/Backstory --prefix Backstory_

The takes folder is printed at the end. Then run the builder in Unity again: shot lengths follow the clips.
"""
import json
import os
import shutil
import sys

AGENT = r"D:\ComfyUI\agent"
VOICE = "portal_f_british_northern"
HERE = os.path.dirname(os.path.abspath(__file__))

# One sentence a line: also the subtitles (the builder holds the same text beside each shot).
LINES = [
    ("s01a", "Listen, little one."),
    ("s01b", "This is an old story, and I am nearly the last who remembers it."),
    ("s02a", "Long ago, your people made a promise with the forest."),
    ("s02b", "They would leave it room to live."),
    ("s02c", "And its guardians would keep older, hungrier things at bay."),
    ("s03a", "Beside my stones they built a hearth."),
    ("s03b", "They kept the fire, and I kept the night from their door."),
    ("s03c", "That was the pact."),
    ("s04a", "It held longer than anyone has counted."),
    ("s05a", "Then, one winter, the fire went out."),
    ("s06a", "The forest does not kill the lost."),
    ("s06b", "It forgets them."),
    ("s06c", "First the way home."),
    ("s06d", "Then their names."),
    ("s06e", "Then there is no one left to find."),
    ("s07a", "A hundred winters I kept one small clearing, alone."),
    ("s07b", "I grew thin."),
    ("s07c", "And at last, I slept."),
    ("s08a", "Then four of your people walked in under the trees, to learn what the woods had become."),
    ("s08b", "They did not walk out."),
    ("s09a", "So four more came looking."),
    ("s09b", "One of them was you."),
    ("s10a", "That is the old story."),
    ("s10b", "Yours begins here: a bright morning, a small fire, and the trees waiting."),
]


def main():
    sys.path.insert(0, AGENT)
    import generate_voice as gv

    args = sys.argv[1:]
    takes = int(args[args.index("--takes") + 1]) if "--takes" in args else 3
    wanted = [a for a in args if a in dict(LINES)]
    lines = [line for line in LINES if not wanted or line[0] in wanted]

    # The voice lives with the project; LocalSpawn gets a copy when it has none.
    folder = gv.voice_dir(VOICE)
    if not (folder / "voice.json").is_file():
        shutil.copytree(os.path.join(HERE, "Voices", VOICE), folder, dirs_exist_ok=True)
        print("Copied the voice into", folder)

    out = os.path.join(str(folder), "cinematic")
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "lines.json"), "w", encoding="utf-8") as stream:
        json.dump([{"id": key, "text": text} for key, text in LINES], stream, indent=2)
    jobs = [{"voice": VOICE, "seed": 3000 + 1000 * (take - 1), "lines": [
        {"text": text, "output": os.path.join(out, f"{key}_t{take}.wav")} for key, text in lines]}
        for take in range(1, takes + 1)]
    spoken = gv.speak_many(jobs, timeout=3600, progress=lambda f, m: print(f"[{f:4.0%}] {m}", flush=True))
    print("OK", sum(len(v) for v in spoken.values()), "clips in", out)


if __name__ == "__main__":
    main()

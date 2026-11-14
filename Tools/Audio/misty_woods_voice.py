"""The narration of the first-raid cinematic, "The Misty Woods" (Docs/MistyWoodsCinematic.md), in the portal's voice.

The words are the user's, unchanged. Each sentence is its own clip, so the pauses between them are set in the
cinematic. The portal is spoken as its dialogue is (and as the opening and The Catch were re-spoken on 2026-11-14):
Qwen3-TTS "shaped", voice portal_g_mysterious, mood Neutral, no effects (never slowed down). Three takes a line
(take 1 = the voice's own seed), then Tools/Audio/narration.py keeps the take that says the words, ends cleanly and
stays nearest the voice's usual pitch, trims it and sets its level.

  speak   all takes          -> Tools/Audio/Voices/raw/misty_woods/<id>_t<n>.wav + lines.json (kept out of Assets)
  pick    the chosen takes   -> Tools/Audio/Voices/raw/misty_woods/picked/<id>.wav (narration.py)
  finish  into the game      -> Assets/_Project/Audio/Voice/MistyWoods/MistyWoods_<line>.wav

"Return to Haven... before the forest claims you." is one line on screen, but the model ran straight through the
ellipsis (no gap over 0.06 s), so its two halves are spoken apart and joined with a held breath between (JOINS).

Run speak with LocalSpawn's Python, pick with the voice generator's (it has Whisper), then finish:
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/Audio/misty_woods_voice.py speak [m03a ...] [--takes 3]
  D:\\ComfyUI\\qwen-tts\\.venv\\Scripts\\python.exe Tools/Audio/narration.py Tools/Audio/Voices/raw/misty_woods Tools/Audio/Voices/raw/misty_woods/picked
  python Tools/Audio/misty_woods_voice.py finish
"""
import json
import os
import re
import shutil
import sys

AGENT = r"D:\ComfyUI\agent"
HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "Voices", "raw", "misty_woods")
VOICE = "portal_g_mysterious"

# id -> sentence. m01 to m06 are the cinematic's six shots.
LINES = {
    "m01a": "Beyond the reach of the Ward, lies a place where the light has long since faded.",
    "m02a": "The Misty Woods swallow all who wander too deep.",
    "m02b": "Its shadows hide things best left undisturbed.",
    "m03a": "The longer you remain, the deeper its corruption grows.",
    "m03b": "And the more it draws the attention of what lurks within.",
    "m04a": "You are here for what the forest holds.",
    "m04b": "Its resources, or its prisoners.",
    "m05a": "Take what you need, save who you can.",
    "m05b": "Avoid what you cannot fight.",
    "m06a": "For this is no place to call home.",
    "m06b1": "Return to Haven\u2026",
    "m06b2": "before the forest claims you.",
}

# A line on screen made of clips spoken apart: line id -> (parts, seconds of silence between them).
JOINS = {"m06b": (["m06b1", "m06b2"], 0.75)}
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Audio", "Voice", "MistyWoods"))


def finish():
    import numpy as np
    from scipy.io import wavfile
    picked = os.path.join(RAW, "picked")
    os.makedirs(OUT, exist_ok=True)
    joined = {part for parts, _ in JOINS.values() for part in parts}
    for key in LINES:
        if key not in joined:
            shutil.copyfile(os.path.join(picked, key + ".wav"), os.path.join(OUT, f"MistyWoods_{key}.wav"))
    for key, (parts, gap) in JOINS.items():
        pieces = []
        for i, part in enumerate(parts):
            rate, data = wavfile.read(os.path.join(picked, part + ".wav"))
            pieces.append(data)
            if i < len(parts) - 1:
                pieces.append(np.zeros((int(gap * rate),) + data.shape[1:], dtype=data.dtype))
        wavfile.write(os.path.join(OUT, f"MistyWoods_{key}.wav"), rate, np.concatenate(pieces))
    print("\n".join(sorted(os.listdir(OUT))))


def speak():
    sys.path.insert(0, AGENT)
    import generate_voice as gv
    named = [a for a in sys.argv[2:] if a in LINES]
    takes = int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 3
    os.makedirs(RAW, exist_ok=True)
    with open(os.path.join(RAW, "lines.json"), "w", encoding="utf-8") as stream:
        json.dump([{"id": key, "text": text} for key, text in LINES.items()], stream, indent=2)
    entries = []
    for take in range(1, takes + 1):
        for key, text in LINES.items():
            output = os.path.join(RAW, f"{key}_t{take}.wav")
            if (named and key in named) or (not named and not os.path.isfile(output)):
                # A line break after a full stop measured the longest pause (Qwen skips short ones otherwise).
                entries.append({"voice": VOICE, "engine": "shaped", "mood": "Neutral", "effects": {}, "take": take,
                                "text": re.sub(r"(?<=[.!?])\s+(?=\S)", "\n", text), "output": output})
    if not entries:
        print("Every take exists. Name a line to speak it again.")
        return
    spoken = gv.speak_script(entries, check=True, timeout=7200,
                             progress=lambda fraction, message: print(f"[{fraction:4.0%}] {message}", flush=True))
    for entry, clip in zip(entries, spoken):
        wrong = "" if clip.get("says_the_words") is not False else f"  WORDS DIFFER: {clip.get('heard')}"
        print(f"{os.path.basename(entry['output'])}: {clip['seconds']:.2f} s, seed {clip.get('seed')}, tries {clip.get('attempts')}{wrong}")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "speak":
        speak()
    elif len(sys.argv) > 1 and sys.argv[1] == "finish":
        finish()
    else:
        print(__doc__)

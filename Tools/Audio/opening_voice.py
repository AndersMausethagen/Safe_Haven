"""The narration of the opening cinematic, "The Fire at the Stones" (Docs/BackstoryCinematic.md), in the portal's voice.

The words are the user's script (2026-11-16), unchanged. Each sentence is its own clip, so the pauses between them,
and the longer ones the script marks, are set where the cinematic places the clips. The portal is spoken as its
dialogue is: Qwen3-TTS "shaped", voice portal_g_mysterious, mood Neutral, never slowed down, and no line breaks in the
text (they cut a line into parts: the user, 2026-11-16). Three takes a line (take 1 = the voice's own seed), then
Tools/Audio/narration.py keeps the take that says the words, ends cleanly and stays nearest the voice's usual pitch,
trims it and sets its level.

  speak   all takes          -> Tools/Audio/Voices/raw/opening/<id>_t<n>.wav + lines.json (kept out of Assets)
  pick    the chosen takes   -> Tools/Audio/Voices/raw/opening/picked/<id>.wav (narration.py)
  finish  into the game      -> Assets/_Project/Audio/Voice/Opening/Opening_<id>.wav

  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/Audio/opening_voice.py speak [o03b ...] [--takes 3]
  D:\\ComfyUI\\qwen-tts\\.venv\\Scripts\\python.exe Tools/Audio/narration.py Tools/Audio/Voices/raw/opening Tools/Audio/Voices/raw/opening/picked
  python Tools/Audio/opening_voice.py finish
"""
import json
import os
import shutil
import sys

AGENT = r"D:\ComfyUI\agent"
HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "Voices", "raw", "opening")
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Audio", "Voice", "Opening"))
VOICE = "portal_g_mysterious"

# id -> sentence; o01 to o11 are the script's eleven shots.
LINES = {
    "o01a": "A fire is a small thing, little one.",
    "o01b": "Until it is all that stands between home and the dark.",
    "o02a": "Your people made a bargain with the woods.",
    "o02b": "They would leave it room to live.",
    "o02c": "Its guardians would hold the older hunger beyond the trees.",
    "o03a": "Beside my stones, they kept a hearth.",
    "o03b": "Their flame fed my ward.",
    "o03c": "My ward kept the night from their doors.",
    "o04a": "For generations, each kept their part.",
    "o05a": "Then, one winter, the fire died.",
    "o06a": "The ward thinned.",
    "o06b": "The old road began to turn.",
    "o07a": "The lost were still out there.",
    "o07b": "But little by little, the way to them was forgotten.",
    "o08a": "I held one clearing for as long as I could.",
    "o08b": "A hundred winters passed.",
    "o08c": "My light grew thin.",
    "o08d": "Then I slept.",
    "o09a": "Much later, four walked the old road to learn what had changed.",
    "o09b": "Their lights went in.",
    "o09c": "None came back.",
    "o10a": "Four more prepared to follow.",
    "o10b": "One of them was you.",
    "o10c": "Why you came is yours to say.",
    "o11a": "At dawn, you will take the old road east.",
    "o11b": "The woods have already begun to change.",
}


def finish():
    picked = os.path.join(RAW, "picked")
    os.makedirs(OUT, exist_ok=True)
    for key in LINES:
        shutil.copyfile(os.path.join(picked, key + ".wav"), os.path.join(OUT, f"Opening_{key}.wav"))
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
                entries.append({"voice": VOICE, "engine": "shaped", "mood": "Neutral", "effects": {}, "take": take,
                                "text": text, "output": output})
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

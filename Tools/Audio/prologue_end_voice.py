"""The narration of the prologue-end cinematic (Docs/PrologueEndCinematic.md), spoken by LocalSpawn in the portal's voice.

Each sentence is its own clip, so the pauses between them can be set in the cinematic
(AgentScripts/BuildPrologueEndCinematic.cs). The acting voice model (IndexTTS-2.5) reads them: the portal is calm
throughout, with the feeling given per line below. Lines whose clip exists are skipped; name a line to speak it again.

Run with LocalSpawn's Python:
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/Audio/prologue_end_voice.py [c05c c09c ...] [--take 2]
"""
import os
import sys

AGENT = r"D:\ComfyUI\agent"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Audio", "Voice", "PrologueEnd"))
VOICE = "portal_f_british_northern"
# Short sentences each start and end in a breath, so at the pace of the portal's dialogue (0.84) these came to 120
# words a minute and a cinematic of over two minutes. This keeps her unhurried and the whole under 1:50.
PACE = 0.97
RAW = os.path.join(HERE, "Voices", "raw", "prologue_end")
CALM = {"calm": 0.5}
GRAVE = {"calm": 0.3, "melancholic": 0.2}
SORRY = {"sad": 0.3, "calm": 0.2}
WARM = {"happy": 0.25, "calm": 0.3}

# id -> the sentence, and the feeling it is played with.
LINES = {
    "c01a": ("You put your hand on the stone, little one.", CALM),
    "c01b": ("And far away, in my sleep, I felt it.", CALM),
    "c02a": ("The old one felt it too.", GRAVE),
    "c02b": ("It was already walking towards the stones.", GRAVE),
    "c03a": ("Its blow fell as the way opened.", GRAVE),
    "c03b": ("The stones broke, and the road tore.", GRAVE),
    "c04a": ("Everyone in that ring was thrown into the dark.", SORRY),
    "c04b": ("Seven of your people are out there now, scattered like sparks across the Misty Woods.", SORRY),
    "c05a": ("I am old, and thin, and I had slept a hundred winters.", GRAVE),
    "c05b": ("But I reached.", CALM),
    "c05c": ("I caught one. I could only catch one.", SORRY),
    "c06a": ("So here you are: my clearing.", WARM),
    "c06b": ("The last place the forest has not taken.", CALM),
    "c07a": ("The others are alive.", CALM),
    "c07b": ("But the forest does not kill the lost. It forgets them.", SORRY),
    "c07c": ("And it has already begun.", GRAVE),
    "c08a": ("There is one thing it cannot forget: a hearth that someone tends.", WARM),
    "c08b": ("Light one beside my stones, and they will see it, and know which way is home.", WARM),
    "c09a": ("Then build them something to come home to.", WARM),
    "c09b": ("Wake me, and I will open the way, and you will go and bring them back.", CALM),
    "c09c": ("Now wake up, little one.", WARM),
}


def main():
    sys.path.insert(0, AGENT)
    import generate_voice as gv
    named = [a for a in sys.argv[1:] if a in LINES]
    take = int(sys.argv[sys.argv.index("--take") + 1]) if "--take" in sys.argv else 1
    entries = []
    for key, (text, feeling) in LINES.items():
        output = os.path.join(OUT, f"Catch_{key}.wav")
        if (named and key in named) or (not named and not os.path.isfile(output)):
            entries.append({"voice": VOICE, "text": text, "engine": "acted", "emotion": feeling, "take": take,
                            "effects": {"speed": PACE}, "output": output})
    if not entries:
        print("Every line has its clip. Name a line to speak it again.")
        return
    takes = gv.speak_script(entries, check=True, timeout=3600,
                            progress=lambda fraction, message: print(f"[{fraction:4.0%}] {message}", flush=True))
    # LocalSpawn's untouched recordings would be imported by Unity as clips of their own: they are kept outside Assets.
    os.makedirs(RAW, exist_ok=True)
    for clip in takes:
        if clip.get("raw") and os.path.isfile(clip["raw"]):
            os.replace(clip["raw"], os.path.join(RAW, os.path.basename(clip["raw"])))
    stray = os.path.join(OUT, "raw")
    if os.path.isdir(stray) and not os.listdir(stray):
        os.rmdir(stray)
    words = 0
    for entry, clip in zip(entries, takes):
        words += len(entry["text"].split())
        wrong = "" if clip.get("says_the_words") is not False else f"  WORDS DIFFER: {clip.get('heard')}"
        print(f"{os.path.basename(clip['path'])}: {clip['seconds']:.1f} s, tries {clip['attempts']}{wrong}")
    seconds = sum(clip["seconds"] for clip in takes)
    print(f"{len(takes)} lines, {seconds:.0f} s of speech, {words / max(1.0, seconds) * 60:.0f} words a minute.")


if __name__ == "__main__":
    main()

"""Gives every line of the game's dialogue a voice and a mood, ready for LocalSpawn's Voice tab to speak.

The lines come from Unity (AgentScripts/BuildDialogueVoices.cs, entry Harvest -> Tools/Audio/Voices/dialogue_lines.json).

  voices   Design a voice for each character that has none yet (LocalSpawn's Voice generator), check it came out a
           man's or a woman's as asked, and keep it in Tools/Audio/Voices/<name>/ so it can never be lost.
  moods    Decide how each line is said: by the kind of line it is (a shout in a fight, a whisper of the portal's),
           then, with --llm, by a local language model reading the line (Ollama). Kept in dialogue_moods.json,
           where a line's mood can be changed by hand: a mood written there is never overwritten.
  script   Write Tools/Audio/Voices/voice_script.json: every line with its voice, mood, the words to say and
           where its clip goes (Assets/_Project/Audio/Voice/Dialogue/<speaker>/<id>.wav).
  speak    Have LocalSpawn speak the script's lines that have no clip yet.
  new      moods + script + speak: everything a new line needs. What Unity's Voice Lines window runs.
  again    The same for the lines named by id, spoken over the clips they have: `again 8a4f5935addb 17f7d4...`.
  pauses   The voice changer run again over every clip from its untouched recording, with the script's dials (the
           Longest Pause, 2026-11-16): no new takes, the clips written in place.

The easy way is in Unity: Safe Haven > Voice Lines lists every line, sets a line's mood, and has buttons for
"new" and "again" and for adding the clips to the game. By hand it is: Harvest in Unity (AgentScripts/
BuildDialogueVoices.cs), `new` here, then Apply in Unity. Or, in LocalSpawn: Voice tab > "Speak a script (many
voices)" > choose voice_script.json > Speak the script; lines that already have a clip are skipped.

How a line is said, first that applies: what the Voice Lines window set (dialogue_direction.json), a mood written
by hand in dialogue_moods.json, the DIRECTION table below, the kind of line, the language model (--llm), plainly.
To hear a character in another voice, redesign it in LocalSpawn's Voice tab under the same name (Replace), delete
its folder under Audio/Voice/Dialogue, and speak again.

Run with LocalSpawn's Python:
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/Audio/dialogue_voices.py voices|moods [--llm]|script|speak|new|again ID...|pauses|all
"""
import json
import os
import re
import shutil
import sys
import urllib.request
import wave

import numpy as np

AGENT = r"D:\ComfyUI\agent"
HERE = os.path.dirname(os.path.abspath(__file__))
VOICES = os.path.join(HERE, "Voices")
LINES = os.path.join(VOICES, "dialogue_lines.json")
MOODS = os.path.join(VOICES, "dialogue_moods.json")
SCRIPT = os.path.join(VOICES, "voice_script.json")
# What Unity's Voice Lines window set for single lines: {"lines": [{"id", "mood", "feeling", "strength", "take"}]}.
DIRECTION_FILE = os.path.join(VOICES, "dialogue_direction.json")
RAW = os.path.join(VOICES, "raw")
CLIPS =os.path.join("..", "..", "..", "Assets", "_Project", "Audio", "Voice", "Dialogue")
# Who speaks the lines: "acted" is LocalSpawn's IndexTTS-2.5, which plays the mood as it speaks; "shaped" is
# Qwen3-TTS with the mood made afterwards by the voice changer. The voices are the same for both.
ENGINE = "acted"
# The longest silence left inside a line (the voice changer's Longest Pause): IndexTTS leaves 0.6-1.5 s between two
# sentences after the long reference clips, which the user heard as lines "cut into parts" (2026-11-16). About a
# natural pause; the words and the voice are untouched, only the silence is shortened.
LONGEST_PAUSE = 0.4
OLLAMA = "http://127.0.0.1:11434/api/chat"
OLLAMA_MODEL = "qwen3:4b-instruct"

# Who speaks in which voice: the name the dialogue box shows -> the voice, whether it is a woman's (checked by its
# pitch when it is designed), the model that speaks their lines, and what the voice is designed from.
# Nobody is slowed down: every line is said at the voice model's own pace. Slowing them (to 0.84-0.94) made the
# whole cast sound "drunk and slow in the head" in the game (playtest, 2026-11-10).
#
# A voice's reference clip is what its lines are cloned from, so it is long (about 15 seconds, all of it that the
# acting model reads) and says something in the character's own manner with some range in it: a question, an
# exclamation, a quiet close. The first designs said one or two sentences, and their lines came out flatter than the
# clips. Each voice is designed from its description with three seeds (`seeds`), and the one most like the voice the
# user first chose by ear, of the right sex and saying all its words, is kept (Tools/Audio/redesign_voices.py).
#
# The portal is spoken by Qwen3-TTS (better for its longer, quieter reads); everyone else by the acting model.
CAST = {
    "The Portal": dict(voice="portal_g_mysterious", woman=True, engine="shaped", seeds=(1101, 1601, 2101), description=(
        "An old woman's voice, low and dry, warm, slightly hoarse, slow and calm, telling a folktale to a child by a "
        "fire. Soft northern English accent, from rural Yorkshire. Mysterious and hushed, as if she speaks from "
        "somewhere very old and far away, with long thoughtful pauses, gentle and a little sad."),
        reference=("Listen, little one. Do you hear the wind in the stones? It remembers. Long ago, people kept a fire "
                   "beside me, and I kept the night from their door. Then the fire went out. Come closer. There is "
                   "more to tell, and not much time to tell it."),
        was="portal_f_british_northern"),
    "Sigrid": dict(voice="sigrid", woman=True, seeds=(3201, 3701, 4201), description=(
        "A woman in her forties who leads an expedition. A low, firm, steady voice with calm authority, a little "
        "rough from years outdoors, measured and sure."),
        reference=("Listen to me, all of you. We keep to the road, we keep together, and we keep our torches high. Did "
                   "anyone see where the path turned? No? Then we wait for Runa. Steady now! Nobody goes into those "
                   "trees alone. Not tonight.")),
    "Eirik": dict(voice="eirik", woman=False, seeds=(3102, 3602, 4102, 5102, 5602, 6102, 6602, 7102, 7602), description=(
        "A man in his late forties, an old soldier. A gruff, weathered baritone voice, slightly gravelly, with dry "
        "humour, unhurried."),
        reference=("Twenty winters I have carried a shield, and I have never once had a proper breakfast. Is that "
                   "porridge? Gods help us. Stand behind me and keep your blade low. Here they come! Hold! Good. Now we "
                   "eat, and nobody complains about the salt.")),
    "Runa": dict(voice="runa", woman=True, seeds=(3103, 3603, 4103), description=(
        "A young woman in her twenties, a tracker and hunter. A quiet, alert, light and clear voice, soft-spoken "
        "but sure, quick."),
        reference=("Quiet. Do you hear that? Something heavy, moving through the ferns to the east. Wolves don't walk "
                   "like that. Stay low and follow my steps, exactly my steps. There! Behind the birch! Don't run. "
                   "Never run. Just breathe, and let me take the shot.")),
    "Ingrid": dict(voice="ingrid", woman=True, seeds=(3104, 3604, 4104), description=(
        "A woman in her fifties, an alchemist. A dry, precise, thoughtful voice, medium-low, careful with words, "
        "a little tired."),
        reference=("Hold still, this will sting. Willow bark, a little honey, and something I would rather not name. "
                   "There. Does that feel any better? I thought not. I have been awake for three nights, and the roots "
                   "out there are still warm. Something is feeding them.")),
    "Bjarne": dict(voice="bjarne", woman=False, seeds=(3105, 3605, 4105), description=(
        "A big man in his forties, a blacksmith. A deep, warm, rumbling bass voice, slow, kind and plain-spoken."),
        reference=("Good iron doesn't lie to you. You heat it, you strike it, and it tells you what it wants to be. "
                   "Hand me that hammer, would you? Careful, it's heavier than it looks! There. Now we make something "
                   "that will still be here when we're gone.")),
    "Tor": dict(voice="tor", woman=False, seeds=(3106, 3606, 4106), description=(
        "An older man in his sixties, a carpenter. A gentle, soft, slow voice, patient and warm, with a craftsman's calm."),
        reference=("Measure twice, cut once. My father said that every morning, and I never listened. Now look at me, "
                   "eighty planks and not one of them crooked. Could you hold this end? Mind your fingers! Good. A roof "
                   "is only a promise, but it's a good one to keep.")),
    "Liv": dict(voice="liv", woman=True, seeds=(3107, 3607, 4107), description=(
        "A young woman in her late twenties, a scholar of runes. A bright, sharp, confident voice, quick and precise, "
        "a little impatient."),
        reference=("Look at this mark. No, closer. See how the line breaks and turns back on itself? That is not wear, "
                   "that is writing! Somebody cut this on purpose, a long time ago. Why does nobody ever listen to me? "
                   "Bring the lantern. We are not leaving yet.")),
    "Asa": dict(voice="asa", woman=True, seeds=(3108, 3608, 4108), description=(
        "A woman in her thirties, a farmer. A plain, sturdy, warm voice, practical and down to earth."),
        reference=("Good black soil, under all this moss. Who left it to go wild like this? A shame, that's what it is. "
                   "Give me a spade and one dry summer, and I'll have barley standing taller than you. Don't laugh! "
                   "I've done it before, and I'll do it again.")),
    "Gunnar": dict(voice="gunnar", woman=False, seeds=(3109, 3609, 4109, 5109, 5609, 6109, 6609, 7109, 7609), description=(
        "A man in his fifties, a hunter who lives alone. A low, quiet, gruff voice of few words, slow and wary."),
        reference=("Tracks are fresh. A day, no more. You see where the moss is torn? That's a boar, a big one, and "
                   "it's not alone. Keep the wind in your face. If it charges, step aside, don't run! Let it pass, and "
                   "strike behind the shoulder. Simple.")),
    "Hild": dict(voice="hild", woman=True, seeds=(3110, 3610, 4110), description=(
        "A woman in her forties, a stonemason. A strong, blunt, dry voice, full of opinions, medium pitch."),
        reference=("Whoever built this wall should be ashamed of themselves. Look at it! Not one stone sits true. You "
                   "want it to stand through a winter? Then you set the big ones at the bottom, you fill the gaps, and "
                   "you never, ever hurry. Hand me the level. Thank you.")),
    "Yrsa": dict(voice="yrsa", woman=True, seeds=(3111, 3611, 4111), description=(
        "An older woman in her sixties, a proud cook. A warm, full, lively voice, bossy and fond, medium-low."),
        reference=("Out of my kitchen, all of you! No, not you, you can stay and stir. Slowly! It's a stew, not a "
                   "battle. A pinch of salt, a little thyme, and patience. Do you smell that? That is what a home "
                   "smells like. Now taste it, and tell me I'm wrong.")),
}
# A voice below this is a man's, above it a woman's (median pitch, Hz). A design that comes out wrong is made again.
SPLIT_HZ = 150.0
TRIES = 5

# How each kind of line is said when nothing better is known (the kind is the harvester's: where the line was found).
BY_KIND = [
    ("bark: spotted", "Yelling"), ("resident: spotted", "Yelling"), ("bark: behindYou", "Yelling"), ("resident: behindYou", "Yelling"),
    ("bark: hurt", "Scared"), ("resident: hurt", "Scared"), ("bark: won", "Glad"), ("resident: won", "Glad"),
    ("bark: helpedUp", "Glad"), ("resident: helpedUp", "Glad"), ("fallenLine", "Tired"), ("healLines", "Stern"),
    ("portal whisper", "Calm"), ("nextWaveText", "Yelling"),
]
# The direction: how a line is said, by how it begins (the words as spoken, any speaker). A mood, or a mood and the
# feelings to play instead of that mood's own (weights of happy, angry, sad, afraid, disgusted, melancholic,
# surprised, calm; kept to about 0.55 in all, where a voice still measures as its speaker). Written by reading the
# scenes; it wins over the kind of line and over the language model, and loses only to dialogue_moods.json by hand.
# A line not listed here is read by its kind, then by the model (--llm), then plainly.
N, C, G, J, S, T, ST, A, SC, W, Y = ("Neutral", "Calm", "Glad", "Joyful", "Sad", "Tired", "Stern", "Angry", "Scared",
                                     "Whisper", "Yelling")
DIRECTION = {
    # --- said by everyone who fights beside you ---
    "I'll hold this spot.": N, "I'll keep back.": N, "Nothing gets to you without going through me.": ST,
    "Point the way.": N, "Right behind you.": N, "Anything else?": N, "Come back when it's done.": N,
    "We came to learn why this forest": N, "Stay near the stones. We can still": ST,
    # --- Asa, Gunnar, Hild, Yrsa ---
    "There's good dirt under all this moss": N, "You're not from the village": N,
    "You've been frightening my deer": N, "Took you long enough. That boar": T,
    "Whoever laid this hearth": N, "Mind the coping": N, "Show me your kitchen": N, "Mind the pot": N,
    # --- Bjarne ---
    "I came to examine the old iron markers": N, "Where's the anvil?": N, "You took your time. Mind the hammer": G,
    "I blazed the trees going in": (T, {"afraid": 0.25, "melancholic": 0.25}),
    # --- Eirik ---
    "I trust that she wants to get out": C, "My sister's farm is two valleys": (S, {"sad": 0.35}),
    "Once, as a boy, with my sister": (C, {"melancholic": 0.3, "calm": 0.2}),
    "Twenty winters in the shield wall at Granheim. I stopped": (C, {"melancholic": 0.35}),
    "Wolves, probably.": N, "Eat something, even if you are not hungry": C, "Stay between me and the trees": ST,
    "Eat something. It is a long road": N, "Go! I will hold them": Y, "Before we set out? Gladly": G,
    "Dry bread, unless you fancy a walk": N, "Now that is a fine idea": G, "We always need firewood": N,
    "Breakfast!": J, "Hot food.": G, "That will keep us warm": N, "You'll do. Better than most": G,
    "Dummy first, then me": N, "Five berries from the clearing": N, "Six logs from the slope": N,
    "Two fish from the lake": N, "Show me the wall": ST, "Don't look at me like that": T, "My knees are too old": T,
    "That many, and I'm still here": G, "Twenty winters I've been telling": N, "Took you long enough. How many": ST,
    "A soldier's choice": N, "Eirik. Twenty winters": N, "Long reach, and it wants both hands": N, "The axe. Good.": G,
    "Then we are after the same thing": C, "A bewitched forest. My grandmother": N,
    "A whole day gone in a morning's walk": (N, {"afraid": 0.3}), "And the kindling you cut": N,
    "And there is only dry bread": N, "Down you go. Catch your breath": G, "Enough, enough! I yield": J,
    "Good. Now me.": N, "I salted what was left": N, "Is it past noon already?": (N, {"surprised": 0.25}),
    "It is this place. At the tree line": (N, {"afraid": 0.3, "melancholic": 0.2}),
    "Nothing gets worse than this porridge": N, "Sigrid. The sun is going down": (SC, {"afraid": 0.4}),
    "Steady now. It is a fine morning": N, "That trout this morning": N,
    "Twenty years a soldier, and I have never seen": (N, {"afraid": 0.35}), "Whoever you came for": C,
    # --- Ingrid ---
    "The plants are wrong": (N, {"afraid": 0.3}),
    "I kept saying the word": (T, {"melancholic": 0.3, "afraid": 0.3}),
    "It's you! I saw a light": (G, {"happy": 0.4, "surprised": 0.2}),
    "I watched your fire for three nights": (T, {"melancholic": 0.3, "afraid": 0.2}),
    # --- Liv ---
    "There are old paths here": N, "The stones are answering you": ST, "Your ward is a promise too": N,
    "The stones broke along a line": (N, {"surprised": 0.35, "happy": 0.15}),
    # --- Runa ---
    "Deer, boar, wolf, all running west": N, "Poaching on the jarl's land": N, "Someone is paying": N,
    "Troll tracks. Small ones": (N, {"afraid": 0.25}), "Walk where the moss is thick": C,
    "I counted three wolves circling": (C, {"afraid": 0.3, "calm": 0.2}), "Tracks everywhere, all running": N,
    "Nice morning. That is usually": N, "I will take the ones on the stones": ST, "Wolves! They are not hunting": Y,
    "As it happens. There are blight boar": N, "Clean enough. Keep the bow": G, "Two boar, on the grass": N,
    "This place can be found": N, "I counted our steps to the exit": T,
    "I've tracked things my whole life": (N, {"afraid": 0.3}),
    "The forest doesn't hate us": (N, {"melancholic": 0.3}), "You crossed open ground": ST,
    "I've watched you for two days": N, "A bow! Then there are two of us": G, "An honest answer": G,
    "Down! Moss trolls in the bushes": Y, "It followed us!": (SC, {"afraid": 0.5, "surprised": 0.15}),
    "It hasn't seen us yet": (C, {"afraid": 0.3, "calm": 0.2}), "Liv.": C, "Off the road, on your left": Y,
    "Runa. I find the tracks": N, "Simple. I respect that": G, "Small ones never come alone": ST,
    "A shepherd told me his dogs": (N, {"afraid": 0.2}), "Boar, running flat out": Y,
    "Hold up. That's Bjarne's handcart": ST, "No birds. Have you noticed": (C, {"afraid": 0.3, "calm": 0.2}),
    "Sigrid. Look behind us": SC, "Still think the coin is worth it": N,
    "The shadows are wrong": (N, {"afraid": 0.35}), "There. Feel that? The wind turned": (N, {"afraid": 0.3}),
    "Wolves behind them!": Y, "You shot straight this morning": N, "Your grandmother never saw wolves": N,
    # --- Sigrid ---
    "Because the only old things out there": N, "Eirik sees a great deal": N,
    "I know what it feels like: a door left open": (C, {"melancholic": 0.25, "calm": 0.2}),
    "Ingrid, Bjarne, Tor and Liv. An alchemist": N, "The cause. Trolls do not leave": ST, "They did not send me": N,
    "Words for things your tongue": C, "The villages think we came for wolves": N, "Keep the torch high": C,
    "Take what time you need": N, "Stand inside the ring": ST, "Dry stone overhead. Good work": N,
    "That was no weather": ST, "The packs are tied": N, "The stones are drawing power": Y,
    "There you are. Come and stand by the fire": (G, {"happy": 0.25}), "Hold together! Nobody gets left behind": Y,
    "Finish your porridge and listen": ST, "Stay on the road. We keep a steady pace": ST,
    "Seven went in. Tell me who's home": ST, "A retreat is only a defeat": ST, "I gave you an order once": ST,
    "I've buried good people": (C, {"melancholic": 0.35}), "I've served under worse than you": N,
    "You came back. I told them you would": G,
    "...can anyone hear": (SC, {"afraid": 0.35, "sad": 0.2}), "can anyone hear": (SC, {"afraid": 0.35, "sad": 0.2}),
    "A fire. Sit down, all of you": T, "At first light we find the four": (N, {"afraid": 0.2, "melancholic": 0.2}),
    "Careful. The ones who go looking": ST, "For anyone who slept through": N, "It will not wake for me": ST,
    "Leave the fire! Take the old path north": Y,
    "Liv. Of course. Eleven years": (N, {"afraid": 0.35, "sad": 0.15}), "Roots do not set ambushes": ST,
    "That is no thunder": (ST, {"afraid": 0.35, "angry": 0.15}), "We leave when you are ready": N,
    "Whatever you see, do not let go of the stone. There are things": (ST, {"afraid": 0.3, "sad": 0.25}),
    "You asked me by the fire, so here it is": (C, {"melancholic": 0.3}), "I can see that. Light your torches": ST,
    "Keep walking.": ST, "Not alone, and not before we are ready": ST, "Rumours grow in the telling": N,
    "Save it for the road": N, "Wolves that hunt by day": ST, "You wanted to know if the stories were true": N,
    "And you. You came to us with a strong back": N, "Then one thing first. Nobody walks that road unarmed": N,
    # --- Tor ---
    "This stone is dry enough": N, "It's built crooked, you know": N,
    "Still measuring. I knew someone would come": (T, {"melancholic": 0.2, "happy": 0.2}),
    "There's a hollow in the deep woods": (N, {"afraid": 0.3, "melancholic": 0.2}),
    # --- The Portal: old and gentle, so calm unless a line says otherwise ---
    "Here is the one nobody tells": C,
    "You came to see if the stories were true. Here is": (C, {"sad": 0.3, "calm": 0.2}),
    "Little one, behind you": (SC, {"afraid": 0.5, "surprised": 0.15}), "Rootlings, in my clearing": ST,
    "That is what you killed. I am sorry": (S, {"sad": 0.4}),
    "I have not told you everything": (C, {"sad": 0.3, "calm": 0.2}),
    "Would you have woken it, knowing": (C, {"sad": 0.25, "calm": 0.25}),
    "You brought it down. Lay what you carried": (C, {"sad": 0.3, "calm": 0.2}),
    "Seven sparks, scattered through the Misty Woods": (C, {"sad": 0.25, "calm": 0.25}),
    "There. A hearth beside my stones": (C, {"happy": 0.3, "calm": 0.2}),
    "I remember this. A fire beside my stones": (C, {"happy": 0.25, "calm": 0.25}),
    "Do you feel that? The ward has remembered": (C, {"happy": 0.3, "calm": 0.2}),
    "Listen. Far off, at dusk, a horn": (C, {"happy": 0.2, "surprised": 0.15, "calm": 0.2}),
    "Then she is still herself": (C, {"happy": 0.2, "calm": 0.3}),
    "Little one. You fell out of the dark": (C, {"sad": 0.3, "calm": 0.25}),
    "I am a door, little one, and I have gone to sleep": (C, {"melancholic": 0.35, "calm": 0.2}),
    "Something tests my ward": ST,
}
# Whoever is not in the direction above and has this for a mood by the model is given it anyway: she is very old.
ALWAYS_CALM = {"The Portal"}
# Names the voice model reads wrong, and a spelling it reads right. Tried on two readings each (2026-11-10): written
# "Bjarne" it said "Bajarn", "Bijarn" and once "Barney"; written "Byarna" a recogniser heard "Bjarne" every time.
# Eirik, Sigrid, Liv, Ingrid, Runa, Granheim and Heartroot come out recognisable as they are written.
SPOKEN_AS = {"Bjarne": "Byarna"}
MOOD_NAMES = ["Neutral", "Calm", "Glad", "Joyful", "Sad", "Tired", "Stern", "Angry", "Scared", "Whisper", "Yelling"]


def directed(line):
    """(mood, emotion or None) the direction gives a line, or None."""
    words = say(line["text"])
    best = max((start for start in DIRECTION if words.startswith(start)), key=len, default=None)
    if best is None:
        return None
    how = DIRECTION[best]
    return how if isinstance(how, tuple) else (how, None)
ASK = ("You direct voice actors for a Nordic folktale game. Pick the ONE delivery that fits the line best. "
       "Neutral: ordinary talk. Calm: gentle, reassuring, reflective. Glad: pleased, relieved, friendly. "
       "Joyful: delighted, laughing. Sad: grief, regret, loss. Tired: weary, worn out, wounded. "
       "Stern: firm orders, warnings, serious instruction. Angry: furious, bitter. Scared: fearful, panicked. "
       "Whisper: secretive or hushed, said under the breath. Yelling: shouted across a fight or a distance. "
       "Most lines are Neutral, Calm or Stern: choose a strong delivery only when the words plainly call for it. "
       'Reply with JSON: {"mood": "<one of the names>"}')


def read_json(path, default):
    return json.load(open(path, encoding="utf-8")) if os.path.isfile(path) else default


def folder(speaker):
    """A speaker's clip folder, as AgentScripts/BuildDialogueVoices.Folder names it."""
    return re.sub(r"[^a-z0-9]", "_", speaker.strip().lower()).strip("_")


def say(text):
    """The words as they are spoken: no markup, no key hints such as "(G)" or "[E]", no stage quotes. Where a line
    is part narration and part speech in italics ("Warmth stirs in the old stone. <i>Little one...</i>"), only the
    speech is said."""
    spoken = re.findall(r"<i>(.*?)</i>", text, re.S)
    if spoken and re.sub(r"<i>.*?</i>", "", text, flags=re.S).strip():
        text = " ".join(spoken)
    text = re.sub(r"<[^>]*>", "", text)
    # As VoiceBank.Plain leaves them out of what a line is found by.
    text = re.sub(r"\s*[\(\[][^\(\)\[\]]{1,14}[\)\]]", "", text)
    return re.sub(r"\s+", " ", text.replace("\u2014", ", ").replace("\u2013", ", ")).strip().strip('"\u201c\u201d')


def spoken(text):
    """The words as they are given to the voice model: say(text), with the names it reads wrong spelt as they sound."""
    words = say(text)
    for name, sound in SPOKEN_AS.items():
        words = re.sub(rf"\b{re.escape(name)}\b", sound, words)
    return words


def pitch_of(path):
    """Median pitch of a clip in Hz, by autocorrelation over its voiced frames."""
    with wave.open(path) as clip:
        rate = clip.getframerate()
        x = np.frombuffer(clip.readframes(clip.getnframes()), "<i2").astype(np.float32) / 32768.0
    size, hop, found = int(0.04 * rate), int(0.01 * rate), []
    low, high = int(rate / 400), int(rate / 60)
    for start in range(0, len(x) - size, hop):
        frame = x[start:start + size] * np.hanning(size)
        if np.sqrt(np.mean(frame ** 2)) < 0.02:
            continue
        corr = np.correlate(frame, frame, "full")[size - 1:]
        lag = low + int(np.argmax(corr[low:high]))
        if corr[lag] > 0.45 * corr[0]:
            found.append(rate / lag)
    return float(np.median(found)) if found else 0.0


def design_voices(only=None):
    sys.path.insert(0, AGENT)
    import generate_voice as gv
    made = []
    for speaker, role in CAST.items():
        name = role["voice"]
        kept = os.path.join(VOICES, name)
        if only and name not in only:
            continue
        if not only and os.path.isfile(os.path.join(kept, "voice.json")):
            # Already made: LocalSpawn gets its copy back if it lost it.
            if os.path.isfile(os.path.join(kept, "voice.json")) and not (gv.voice_dir(name) / "voice.json").is_file():
                shutil.copytree(kept, gv.voice_dir(name), dirs_exist_ok=True)
                print(f"{name}: copied into LocalSpawn")
            continue
        for attempt in range(TRIES):
            # A new voice with no likeness to keep: its first seed, then others until it is of the right sex. To
            # redesign voices that exist, closest to how they sound now, use redesign_voices.py.
            voice = gv.design(name, role["description"], role["reference"], seed=role["seeds"][0] + 100 * attempt, replace=True)
            pitch = pitch_of(voice["reference"])
            right = pitch > 0 and (pitch >= SPLIT_HZ) == role["woman"]
            print(f"{name}: seed {voice['seed']}, {pitch:.0f} Hz, {'a woman' if role['woman'] else 'a man'} wanted: "
                  f"{'kept' if right else 'made again'}", flush=True)
            if right:
                break
        os.makedirs(kept, exist_ok=True)
        for file in ("reference.wav", "voice.json"):
            shutil.copyfile(gv.voice_dir(name) / file, os.path.join(kept, file))
        made.append(name)
    return made


def mood_by_kind(line):
    for part, mood in BY_KIND:
        if part in line["kind"]:
            return mood
    text = say(line["text"])
    if text.endswith("!") and len(text.split()) <= 8:
        return "Yelling"
    return "Neutral"


def mood_by_model(line):
    """What a local language model makes of the line, or None when it can't be asked or answers something else."""
    body = {"model": OLLAMA_MODEL, "stream": False, "format": "json", "keep_alive": "5m",
            "options": {"temperature": 0.0, "num_ctx": 2048, "num_predict": 30},
            "messages": [{"role": "system", "content": ASK},
                         {"role": "user", "content": f"Speaker: {line['speaker']} ({line['kind']})\nLine: {say(line['text'])}"}]}
    try:
        request = urllib.request.Request(OLLAMA, json.dumps(body).encode(), {"Content-Type": "application/json"})
        with urllib.request.urlopen(request, timeout=120) as response:
            answer = json.loads(json.loads(response.read())["message"]["content"])
    except (OSError, ValueError, KeyError) as exc:
        print(f"  the model could not be asked ({type(exc).__name__}: {exc})", flush=True)
        return None
    mood = str(answer.get("mood", "")).strip().capitalize()
    return mood if mood in MOOD_NAMES else None


def decide_moods(use_model):
    lines = read_json(LINES, [])
    moods = read_json(MOODS, {})
    asked = 0
    # What Unity's Voice Lines window set for single lines. It is laid over the automatic direction, so a line given
    # only another take keeps its mood, and a line set back to automatic there is simply decided afresh.
    window = {row["id"]: row for row in read_json(DIRECTION_FILE, {}).get("lines", []) if row.get("id")}
    for line in lines:
        known = moods.get(line["id"])
        if known and known.get("by") == "hand" and known.get("from") != "unity":
            continue  # written into dialogue_moods.json by hand: left alone
        entry = {"speaker": line["speaker"], "text": say(line["text"])[:90]}
        direction = directed(line)
        if direction:
            entry.update(mood=direction[0], by="direction", **({"emotion": direction[1]} if direction[1] else {}))
        else:
            if known and known.get("by") == "model":
                mood, by = known["mood"], "model"
            else:
                mood, by = mood_by_kind(line), "kind"
                # A shout or a whisper the kind of line already settles is not put to the model.
                if use_model and mood == "Neutral":
                    read = mood_by_model(line)
                    asked += 1
                    if read is None and asked == 1:
                        use_model = False
                    elif read is not None:
                        mood, by = read, "model"
                    if asked % 50 == 0:
                        print(f"  {asked} lines read", flush=True)
            if line["speaker"] in ALWAYS_CALM and mood == "Neutral":
                mood = "Calm"
            entry.update(mood=mood, by=by)
        row = window.get(line["id"])
        if row:
            if row.get("mood") in MOOD_NAMES:
                entry["mood"] = row["mood"]
                entry.pop("emotion", None)  # the direction's own mix of feelings went with its mood
            if str(row.get("feeling") or "").strip():
                entry["feeling"] = row["feeling"].strip()
                entry.pop("emotion", None)  # words for the feeling replace the weights
            if row.get("strength") not in (None, 0, 1, 1.0):
                entry["strength"] = float(row["strength"])
            if int(row.get("take") or 1) > 1:
                entry["take"] = int(row["take"])
            entry.update({"by": "hand", "from": "unity"})
        moods[line["id"]] = entry
    json.dump(moods, open(MOODS, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    counts = {}
    for line in lines:
        counts[moods[line["id"]]["mood"]] = counts.get(moods[line["id"]]["mood"], 0) + 1
    print("Moods:", ", ".join(f"{mood} {count}" for mood, count in sorted(counts.items(), key=lambda kv: -kv[1])))


def write_script():
    lines = read_json(LINES, [])
    moods = read_json(MOODS, {})
    script, nobody = [], {}
    for line in lines:
        role = CAST.get(line["speaker"])
        words = say(line["text"])
        if role is None or not words:
            nobody[line["speaker"]] = nobody.get(line["speaker"], 0) + 1
            continue
        how = moods.get(line["id"], {})
        engine = role.get("engine", ENGINE)
        entry = {"id": line["id"], "voice": role["voice"], "text": spoken(line["text"]), "engine": engine,
                 "mood": how.get("mood", "Neutral"), "effects": character_dials(role, how.get("mood"), engine),
                 "output": os.path.join(CLIPS, folder(line["speaker"]), line["id"] + ".wav").replace("\\", "/")}
        if engine == "shaped":
            # Qwen3-TTS reads plainly: its moods are made afterwards on the sound, which "sounds bad" (the user,
            # 2026-11-10), so none is applied; the line's delivery is the reference clip's. The line is read as written,
            # in one go: a sentence on a line of its own gave longer pauses (0.70 s against 0.45), but the user heard the
            # portal's lines come out cut into parts (2026-11-16), so line breaks are never put in.
            entry.update(mood="Neutral", effects={}, direction=how.get("mood", "Neutral"))
        entry["effects"] = dict(entry["effects"], pause=LONGEST_PAUSE)
        # A line's own feeling in dialogue_moods.json is played instead of its mood's: "emotion" (weights), "feeling"
        # (words: "bitter, holding back tears") or "emotion_audio" (a recording beside the script whose way of
        # speaking is borrowed); "strength" turns any of them up or down.
        entry.update({key: how[key] for key in ("emotion", "feeling", "emotion_audio", "strength")
                      if how.get(key) is not None and engine == "acted"})
        if how.get("take"):  # another reading of the same line: 2, 3...
            entry["take"] = how["take"]
        script.append(entry)
    json.dump(script, open(SCRIPT, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    todo = sum(1 for entry in script if not os.path.isfile(os.path.join(VOICES, entry["output"])))
    print(f"{len(script)} lines in {SCRIPT} ({todo} without a clip yet).")
    if nobody:
        print("No voice for: " + ", ".join(f"{name} ({count})" for name, count in nobody.items()) + ". They stay text only.")


def character_dials(role, mood, engine=None):
    """A character's own dials laid over a mood's: their pace multiplies the mood's, the rest the mood keeps."""
    sys.path.insert(0, AGENT)
    import generate_voice as gv
    table = gv.ACTED_MOODS if (engine or ENGINE) == "acted" else gv.MOODS
    dials = {key: value for key, value in table.get(gv.mood_name(mood) or "Neutral", {}).items() if key != "emotion"}
    own = dict(role.get("dials") or {})
    if "speed" in own:
        dials["speed"] = round(dials.get("speed", 1.0) * own.pop("speed"), 3)
    dials.update(own)
    return dials


def tidy_raw():
    """LocalSpawn keeps each clip's untouched recording in a "raw" folder beside it, which here would be inside
    Assets: Unity would import every one as a second clip. They are moved to Tools/Audio/Voices/raw/<speaker>/
    (not committed), where the Voice tab's "Apply mood and changer" can still be pointed at them."""
    clips = os.path.normpath(os.path.join(VOICES, CLIPS))
    moved = 0
    for folder, _, files in os.walk(clips):
        if os.path.basename(folder) != "raw":
            continue
        target = os.path.join(RAW, os.path.basename(os.path.dirname(folder)))
        os.makedirs(target, exist_ok=True)
        for name in files:
            if name.lower().endswith(".wav"):
                os.replace(os.path.join(folder, name), os.path.join(target, name))
                moved += 1
        shutil.rmtree(folder, ignore_errors=True)
        if os.path.isfile(folder + ".meta"):
            os.remove(folder + ".meta")
    return moved


def pauses():
    """The voice changer run again over every clip from its untouched recording (Tools/Audio/Voices/raw/<speaker>/),
    with the script's dials: after a change to them that needs no new take (the Longest Pause, 2026-11-16). The
    clips are written in place, so Unity keeps every reference to them."""
    sys.path.insert(0, AGENT)
    import generate_voice as gv
    items, missing = [], []
    for entry in gv.read_script_file(SCRIPT):
        raw = os.path.join(RAW, os.path.basename(os.path.dirname(entry["output"])), os.path.basename(entry["output"]))
        if not os.path.isfile(raw) or not os.path.isfile(entry["output"]):
            missing.append(entry["id"])
            continue
        if entry.get("engine", ENGINE) == "acted":
            effects = gv.acted_effects(entry.get("mood"), entry.get("effects"), entry.get("character"))[0]
        else:
            effects = gv.effects_for(entry.get("mood"), entry.get("effects"), entry.get("character"))
        items.append({"input": raw, "output": entry["output"], "effects": effects})
    if items:
        gv.run_worker({"mode": "effects", "items": items}, 3600, runner=gv.FX_RUNNER, gpu=False)
    print(f"PAUSES {len(items)} clips made again from their recordings"
          + (f"; {len(missing)} have none to make them from: {' '.join(missing)}" if missing else "."))
    return 0


def speak(only=None, every=False):
    """Has LocalSpawn speak the script's lines that have no clip yet, or the lines named, or every line (both over the
    clips they have). This is the Voice tab's "Speak a script", run from here so Unity's Voice Lines window can start it."""
    sys.path.insert(0, AGENT)
    import generate_voice as gv
    design_voices()  # nothing is designed that exists; LocalSpawn gets back a voice it has lost
    entries = gv.read_script_file(SCRIPT)
    if only:
        chosen = [entry for entry in entries if entry["id"] in only]
        unknown = sorted(set(only) - {entry["id"] for entry in chosen})
        if unknown:
            print("Not in the script (no voice for the speaker, or the line is gone): " + ", ".join(unknown))
    elif every:
        chosen = list(entries)
    else:
        chosen = [entry for entry in entries if not os.path.isfile(entry["output"])]
    if not chosen:
        print("SPOKEN 0: every line of the script has its clip.")
        return 0
    print(f"Speaking {len(chosen)} of {len(entries)} lines with LocalSpawn ({ENGINE}).", flush=True)
    takes = gv.speak_script(chosen, check=True, timeout=36000,
                            progress=lambda fraction, message: print(f"[{fraction:4.0%}] {message}", flush=True))
    tidy_raw()
    wrong =[(entry, take) for entry, take in zip(chosen, takes) if take.get("says_the_words") is False]
    for entry, take in wrong:
        print(f"WORDS DIFFER {entry['id']} ({entry['voice']}): asked \"{entry['text'][:70]}\", heard \"{str(take.get('heard'))[:70]}\"")
    print(f"SPOKEN {len(takes)}: {len(wrong)} did not say their words after three tries (listen to those).")
    return 0


if __name__ == "__main__":
    step = sys.argv[1] if len(sys.argv) > 1 else "all"
    named = [a for a in sys.argv[2:] if not a.startswith("--")]
    if step in ("voices", "all"):
        design_voices(named or None)
    if step in ("moods", "all", "new", "again", "redo"):
        decide_moods("--llm" in sys.argv)
    if step in ("script", "all", "new", "again", "redo"):
        write_script()
    # new: every line without a clip. again: the lines named by id, spoken over their clips. redo: every line, over
    # its clip (after a change to how all of them are said: a voice, the pace).
    if step == "tidy":
        print(f"{tidy_raw()} untouched recordings moved out of Assets.")
    if step == "pauses":
        write_script()
        sys.exit(pauses())
    if step in ("speak", "new"):
        sys.exit(speak())
    if step == "again":
        sys.exit(speak(named))
    if step == "redo":
        sys.exit(speak(every=True))

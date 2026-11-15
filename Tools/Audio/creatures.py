"""Synthesises the creatures' own voices: what each one sounds like winding up a strike, being hurt and dying, plus the howl, the shaman's
call and two kinds of tread. A throat where there is one (foley.voice), and where there isn't, what the thing is made of: creaking wood for what
grows, wet bubbles for the bog, glass and whisper for a marsh light. The strike's cue has to read in the first tenth of a second, as it is a warning.

The project has no recordings of animals or voices, so these are all modelled. Size comes from the Sound Definition's pitch, so a Rootling is the
Root Guardian's voice sped up and the Heartroot the same one slowed down.

Run from the project root: python Tools/Audio/creatures.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Creatures"


def norm(signal):
    return signal / max(1e-9, np.abs(signal).max())


def throat(seconds, pitch, tracks, rng, rough=0.0, breath=0.1, under=0.0):
    """A cry: pitch is a function of time, tracks the formants as foley.vowel takes them, under an octave below it for the chest."""
    t = times(seconds)
    n = len(t)
    p = pitch(t)
    shape = vowel(*tracks)(n)
    out = voice(p, shape, rng, rough=rough, breath=breath)
    if under > 0:
        out = norm(out) + norm(voice(p * 0.5, shape, rng, rough=1.0, breath=0.2)) * under
    return norm(out)


def arch(t, seconds, start, top, end, peak=0.3):
    """A pitch that climbs to its top a share of the way through, then falls away."""
    x = np.clip(t / seconds, 0, 1)
    return np.where(x < peak, start + (top - start) * np.sin(x / peak * np.pi / 2), end + (top - end) * np.cos((x - peak) / (1 - peak) * np.pi / 2))


def noise_breath(seconds, low, high, q, rng):
    t = times(seconds)
    return norm(sweep(pink(len(t), rng), np.geomspace(low, high, len(t)), q))


def creak(seconds, rng, low=240, high=1500, rate=(14, 36)):
    """Wood under strain: rubbing that catches and lets go, quicker as the load comes on."""
    t = times(seconds)
    rub = band(pink(len(t), rng), low, high)
    judder = 0.35 + 0.65 * np.abs(np.sin(TAU * np.cumsum(rate[0] + (rate[1] - rate[0]) * t / seconds) / RATE))
    return norm(rub * judder)


def crack(rng, base=None):
    return norm(strike(wood(base or rng.uniform(170, 420), rng, damp=0.06), 0.7, 0.002, rng))


def mail(seconds, count, rng):
    """Rings of old mail shifting: tiny bright ticks with a little metal in them."""
    return scatter(seconds, count, lambda: chip(rng, 2600, 6500, 0.03), rng, early=1.2)


def bubbles(seconds, count, rng, low=170, high=520):
    """Wet: each bubble a little rising tone that is gone in a moment, as they are."""
    out = blank(seconds)
    for _ in range(count):
        length = rng.uniform(0.03, 0.09)
        t = times(length)
        f = rng.uniform(low, high) * (1 + 1.1 * t / length)
        bubble = np.sin(TAU * np.cumsum(f) / RATE) * bump(t, 0, 0.004, length * 0.35)
        place(out, bubble, rng.uniform(0, max(0.001, seconds - length)), rng.uniform(0.3, 1))
    return out


def glass(seconds, base, rng, end=None):
    """A thin light with no throat: a few inharmonic partials that beat, gliding from base to end."""
    t = times(seconds)
    glide = np.geomspace(base, end or base, len(t))
    out = np.zeros(len(t))
    for ratio, level in [(1.0, 1.0), (1.006, 0.8), (2.32, 0.5), (3.71, 0.28), (5.13, 0.14)]:
        out += level * np.sin(TAU * np.cumsum(glide * ratio) / RATE + rng.uniform(0, TAU))
    return norm(out)


def fall(rng, weight=1.0):
    """The body reaching the ground."""
    return norm(thump(rng, 0.7, 42, 120, 0.1 * weight) + band(pink(int(0.7 * RATE), rng), 240, 1600) * decay(times(0.7), 0.05) * 0.3)


# ----- Wolf -----

def wolf_snarl(rng):
    seconds = 0.62
    t = times(seconds)
    growl = throat(seconds, lambda t: arch(t, seconds, 125, 185, 150, 0.5), [(620, 1050, 5, 1.0), (1500, 1950, 6, 0.8), (2900, 3100, 7, 0.4)], rng, rough=1.0, breath=0.3)
    flutter = 1 + 0.5 * np.sin(TAU * 30 * t)
    return fade_end(drive(growl * flutter, 2.4) * bump(t, 0, 0.05, 0.3))


def wolf_yelp(rng):
    seconds = 0.32
    t = times(seconds)
    top = rng.uniform(980, 1150)
    cry = throat(seconds, lambda t: arch(t, seconds, 620, top, 480, 0.28), [(900, 1300, 6, 1.0), (2100, 2300, 7, 0.5), (3300, 3400, 8, 0.2)], rng, rough=0.25, breath=0.15)
    return fade_end(cry * bump(t, 0, 0.012, 0.09))


def wolf_dies(rng):
    seconds = 1.5
    t = times(seconds)
    whine = throat(seconds, lambda t: np.geomspace(rng.uniform(760, 840), 230, len(t)) * (1 + 0.025 * np.sin(TAU * 7 * t)),
                   [(820, 480, 6, 1.0), (1900, 1200, 7, 0.45), (3000, 2400, 8, 0.15)], rng, rough=0.35, breath=0.3)
    return place(fade_end(whine * bump(t, 0, 0.02, 0.42), 0.3), fall(rng, 0.7), 0.85, 0.45)


def wolf_howl(rng):
    seconds = 3.0
    t = times(seconds)
    top = rng.uniform(440, 490)
    pitch = lambda t: arch(t, seconds, 300, top, 350, 0.3) * (1 + 0.012 * np.sin(TAU * 5.2 * t) * np.clip((t - 0.7) / 0.5, 0, 1))
    call = throat(seconds, pitch, [(500, 420, 7, 1.0), (1050, 880, 8, 0.45), (2500, 2300, 9, 0.12)], rng, rough=0.05, breath=0.06)
    return fade_end(call * bump(t, 0, 0.3, 1.1), 0.25)


def paw_step(rng):
    seconds = 0.22
    t = times(seconds)
    pad = band(pink(len(t), rng), 180, 1400) * decay(t, 0.03)
    return fade_end(norm(pad) + thump(rng, seconds, 60, 150, 0.03) * 0.5)


# ----- Troll -----

def troll_roar(rng):
    seconds = 0.95
    t = times(seconds)
    roar = throat(seconds, lambda t: arch(t, seconds, 72, rng.uniform(102, 114), 80, 0.45), [(470, 640, 4, 1.0), (950, 1180, 5, 0.9), (2300, 2500, 6, 0.35)], rng, rough=1.0, breath=0.35, under=0.5)
    return fade_end(drive(roar, 3.0) * bump(t, 0, 0.09, 0.42), 0.3)


def troll_hurt(rng):
    seconds = 0.5
    t = times(seconds)
    grunt = throat(seconds, lambda t: np.geomspace(rng.uniform(112, 128), 68, len(t)), [(540, 410, 5, 1.0), (1050, 850, 6, 0.7), (2300, 2100, 7, 0.2)], rng, rough=0.9, breath=0.35, under=0.35)
    return fade_end(drive(grunt, 2.5) * bump(t, 0, 0.018, 0.13))


def troll_dies(rng):
    seconds = 2.2
    t = times(seconds)
    groan = throat(seconds, lambda t: np.geomspace(rng.uniform(102, 114), 42, len(t)), [(560, 360, 5, 1.0), (1050, 700, 6, 0.65), (2200, 1700, 7, 0.2)], rng, rough=1.0, breath=0.55, under=0.5)
    return place(fade_end(drive(groan, 2.2) * bump(t, 0, 0.06, 0.62), 0.35), fall(rng, 1.6), rng.uniform(1.15, 1.35), 0.8)


def stone_skin(rng):
    """A troll turning to stone: a low groan held while grit grinds and the skin cracks over, ending in a set, heavy knock."""
    seconds = 1.7
    t = times(seconds)
    groan = throat(seconds, lambda t: rng.uniform(64, 70) * (1 - 0.08 * t / seconds), [(520, 470, 5, 1.0), (1000, 900, 6, 0.6), (2200, 2000, 7, 0.15)], rng, rough=1.0, breath=0.4, under=0.45)
    grind = band(np.abs(white(len(t), rng)) * pink(len(t), rng), 300, 2600) * np.clip(t / 1.1, 0, 1)
    out = drive(groan, 2.0) * bump(t, 0, 0.15, 0.9) * 0.8 + norm(grind) * bump(t, 0.1, 0.5, 0.7) * 0.5
    out = out + scatter(seconds, 26, lambda: chip(rng, 900, 3200, 0.05), rng, early=0.6) * 0.45
    place(out, norm(strike(stone(rng.uniform(140, 190), rng), 0.5, 0.003, rng)), 1.15, 0.7)
    return fade_end(out, 0.25)


def shaman_call(rng):
    """Throat singing: one low held note, and the shape of the mouth drawing a thin high tone over it."""
    seconds = 1.9
    t = times(seconds)
    chant = throat(seconds, lambda t: rng.uniform(70, 76) * (1 + 0.01 * np.sin(TAU * 4 * t)), [(400, 420, 5, 1.0), (850, 1900, 24, 1.5), (2800, 2600, 8, 0.2)], rng, rough=0.7, breath=0.2, under=0.3)
    ground = band(pink(len(t), rng), 40, 200) * np.clip((t - 0.5) / 1.0, 0, 1)
    return fade_end(drive(chant, 1.8) * bump(t, 0, 0.2, 0.9) + norm(ground) * 0.35, 0.25)


# ----- The barrow dead: more air than voice, and their mail -----

def draug_moan(rng):
    seconds = 0.9
    t = times(seconds)
    moan = throat(seconds, lambda t: arch(t, seconds, 60, rng.uniform(92, 104), 76, 0.6), [(410, 720, 4, 1.0), (1080, 1520, 5, 0.85), (2700, 2900, 6, 0.45)], rng, rough=0.8, breath=1.5)
    return fade_end(moan * bump(t, 0, 0.08, 0.4) + mail(seconds, 16, rng) * 0.3, 0.3)


def draug_hurt(rng):
    seconds = 0.5
    t = times(seconds)
    rasp = noise_breath(seconds, 1500, 600, 3, rng) * bump(t, 0, 0.012, 0.12)
    return fade_end(rasp + crack(rng, rng.uniform(420, 640))[:len(t)] * 0.45 + mail(seconds, 14, rng) * 0.35)


def draug_dies(rng):
    seconds = 2.2
    t = times(seconds)
    last = throat(seconds, lambda t: np.geomspace(rng.uniform(78, 88), 34, len(t)), [(620, 340, 4, 1.0), (1300, 760, 5, 0.75), (2700, 1900, 6, 0.35)], rng, rough=0.9, breath=1.7)
    out = fade_end(last * bump(t, 0, 0.1, 0.65), 0.3)
    place(out, mail(0.8, 44, rng), 0.75, 0.6)
    return place(out, fall(rng, 1.0), 1.0, 0.6)


def mail_tread(rng):
    seconds = 0.5
    t = times(seconds)
    return fade_end(thump(rng, seconds, 50, 130, 0.07) + band(pink(len(t), rng), 150, 900) * decay(t, 0.05) * 0.5)


def mail_tread_with_rings(rng):
    out = mail_tread(rng)
    return place(out, mail(0.28, 10, rng), 0.012, 0.28)


# ----- What grows: the Root Guardian, a Rootling, the Heartroot -----

def root_strain(rng):
    seconds = 1.0
    t = times(seconds)
    load = creak(seconds, rng) * np.clip(t / 0.45, 0, 1) ** 1.3 * np.clip((seconds - t) / 0.14, 0, 1)
    moan = throat(seconds, lambda t: arch(t, seconds, 54, rng.uniform(70, 80), 62, 0.7), [(250, 340, 5, 1.0), (690, 820, 6, 0.45)], rng, rough=0.8, breath=0.25)
    out = load + moan * bump(t, 0, 0.1, 0.5) * 0.7
    # It starts at once, so a warning is a warning.
    return fade_end(place(out, crack(rng), 0.0, 0.5), 0.2)


def root_hurt(rng):
    seconds = 0.55
    t = times(seconds)
    out = crack(rng)[:len(t)] * 0.9 + creak(seconds, rng, 300, 1800, (30, 12)) * bump(t, 0.02, 0.02, 0.14) * 0.6
    return fade_end(place(out, scatter(0.3, 12, lambda: chip(rng, 800, 2600), rng), 0.0, 0.35))


def root_dies(rng):
    seconds = 2.6
    t = times(seconds)
    out = creak(seconds, rng, 180, 1100, (32, 9)) * np.clip(t / 0.1, 0, 1) * np.clip((seconds - t) / 1.2, 0, 1) * 0.75
    moan = throat(seconds, lambda t: np.geomspace(66, 30, len(t)), [(270, 180, 5, 1.0), (680, 470, 6, 0.4)], rng, rough=0.9, breath=0.3)
    out = out + moan * bump(t, 0, 0.1, 0.8) * 0.6
    for _ in range(6):
        place(out, crack(rng), rng.uniform(0, 1.6), rng.uniform(0.25, 0.7))
    return place(fade_end(out, 0.25), fall(rng, 1.6), rng.uniform(1.55, 1.8), 0.85)


# ----- Blight Boar -----

def root_regrowth(rng):
    """Wood growing back: a slow creak that stretches and swells, new shoots rustling, and a soft creaking settle at the end."""
    seconds = 2.0
    t = times(seconds)
    stretch = creak(seconds, rng, 160, 1100, (5, 16)) * bump(t, 0.05, 0.6, 1.2)
    rustle = norm(band(pink(len(t), rng), 1800, 6000)) * (0.5 + 0.5 * np.abs(np.sin(TAU * 3.1 * t))) * bump(t, 0.4, 0.4, 0.7) * 0.35
    out = stretch + rustle
    for at in (0.7, 1.05, 1.5):
        place(out, crack(rng, rng.uniform(140, 220)), at, 0.18)
    return fade_end(out, 0.3)


def boar_charge(rng):
    seconds = 0.72
    t = times(seconds)
    snort = noise_breath(0.2, 500, 1500, 2, rng) * bump(times(0.2), 0, 0.012, 0.05)
    squeal = throat(seconds, lambda t: arch(t, seconds, 340, rng.uniform(640, 720), 520, 0.5), [(900, 1400, 5, 1.0), (2000, 2500, 6, 0.75), (3400, 3700, 7, 0.3)], rng, rough=0.85, breath=0.25)
    out = fade_end(drive(squeal, 2.2) * bump(t, 0.07, 0.09, 0.3), 0.25)
    return place(out, snort, 0.0, 0.9)


def boar_hurt(rng):
    seconds = 0.36
    t = times(seconds)
    squeal = throat(seconds, lambda t: arch(t, seconds, 660, rng.uniform(900, 1020), 520, 0.25), [(1000, 1300, 5, 1.0), (2200, 2500, 6, 0.75), (3500, 3700, 7, 0.3)], rng, rough=0.8, breath=0.25)
    return fade_end(drive(squeal, 2.0) * bump(t, 0, 0.012, 0.1))


def boar_dies(rng):
    seconds = 1.7
    t = times(seconds)
    squeal = throat(seconds, lambda t: np.geomspace(rng.uniform(780, 860), 190, len(t)), [(1050, 560, 5, 1.0), (2300, 1400, 6, 0.7), (3500, 2500, 7, 0.25)], rng, rough=0.9, breath=0.35)
    return place(fade_end(drive(squeal, 2.0) * bump(t, 0, 0.015, 0.45), 0.3), fall(rng, 1.2), 0.95, 0.7)


# ----- Bog Spitter -----

def bog_spit(rng):
    seconds = 0.8
    t = times(seconds)
    gulp = throat(seconds, lambda t: np.geomspace(66, 110, len(t)), [(300, 620, 5, 1.0), (800, 1400, 6, 0.5)], rng, rough=1.0, breath=0.5) * bump(t, 0, 0.03, 0.35)
    hawk = sweep(pink(len(t), rng), np.geomspace(500, 2200, len(t)), 3) * np.clip(t / seconds, 0, 1) ** 2
    out = gulp + norm(hawk) * 0.5 + norm(bubbles(seconds, 26, rng)) * 0.45
    return place(fade_end(out, 0.15), norm(bubbles(0.2, 8, rng, 400, 900)), 0.62, 0.7)


def bog_hurt(rng):
    seconds = 0.42
    t = times(seconds)
    squelch = band(pink(len(t), rng), 240, 1300) * bump(t, 0, 0.012, 0.09)
    return fade_end(norm(squelch) + norm(bubbles(seconds, 22, rng)) * 0.75)


def bog_dies(rng):
    seconds = 1.9
    t = times(seconds)
    sigh = sweep(pink(len(t), rng), np.geomspace(1500, 220, len(t)), 3) * bump(t, 0, 0.05, 0.6)
    out = norm(sigh) * 0.7 + norm(bubbles(seconds, 70, rng, 90, 340)) * 0.8 * np.clip((seconds - t) / 0.9, 0, 1)
    return place(fade_end(out, 0.2), fall(rng, 1.0), 1.15, 0.55)


# ----- Marsh Light -----

def light_rises(rng):
    seconds = 0.85
    t = times(seconds)
    tone = glass(seconds, rng.uniform(820, 960), rng, rng.uniform(1450, 1700)) * (0.65 + 0.35 * np.sin(TAU * np.cumsum(6 + 26 * t / seconds) / RATE))
    whisper = band(pink(len(t), rng), 2400, 7500)
    return fade_end(tone * bump(t, 0, 0.05, 0.5) + norm(whisper) * bump(t, 0, 0.03, 0.4) * 0.35, 0.25)


def light_hurt(rng):
    seconds = 0.38
    t = times(seconds)
    tone = glass(seconds, rng.uniform(1300, 1600), rng, rng.uniform(800, 1000)) * (0.5 + 0.5 * np.sign(np.sin(TAU * 34 * t)))
    return fade_end(lowpass(tone, 7000) * bump(t, 0, 0.004, 0.1) + scatter(seconds, 14, lambda: chip(rng, 3000, 8000), rng) * 0.3)


def light_dies(rng):
    seconds = 1.4
    t = times(seconds)
    tone = glass(seconds, rng.uniform(1300, 1500), rng, rng.uniform(240, 300)) * bump(t, 0, 0.01, 0.45)
    puff = norm(band(pink(len(t), rng), 300, 2400)) * bump(t, 0.2, 0.05, 0.3) * 0.3
    return fade_end(tone + puff + scatter(seconds, 34, lambda: chip(rng, 3500, 9000), rng, early=0.8) * 0.28, 0.3)


def light_splits(rng):
    """A marsh light making its decoys: one glassy tone that wavers, then breaks into three that drift apart, with a whisper."""
    seconds = 1.5
    t = times(seconds)
    base = rng.uniform(900, 1000)
    one = glass(0.55, base, rng, base * 1.06) * (0.7 + 0.3 * np.sin(TAU * 9 * times(0.55)))
    out = blank(seconds)
    place(out, one * bump(times(0.55), 0, 0.05, 0.6), 0.0, 0.9)
    for ratio in (0.84, 1.0, 1.19):
        tail = seconds - 0.45
        tt = times(tail)
        place(out, glass(tail, base * 1.06, rng, base * ratio * 1.3) * bump(tt, 0, 0.03, 0.45), 0.45, 0.55)
    whisper = norm(band(pink(len(t), rng), 2600, 8000)) * bump(t, 0.35, 0.06, 0.4) * 0.3
    return fade_end(out + whisper + scatter(seconds, 18, lambda: chip(rng, 3500, 9000), rng, early=0.7) * 0.2, 0.3)


# ----- Mistwing -----

def wing_screech(rng):
    seconds = 0.62
    t = times(seconds)
    screech = throat(seconds, lambda t: arch(t, seconds, 1250, rng.uniform(1650, 1850), 1050, 0.2), [(2200, 1800, 6, 1.0), (3600, 3200, 7, 0.6), (5200, 4800, 8, 0.2)], rng, rough=0.75, breath=0.4)
    beat = whoosh(seconds, 300, 1400, 1.6, rng, peak=0.5, flutter=0.4, flutter_rate=15)
    return fade_end(drive(screech, 1.8) * bump(t, 0, 0.012, 0.22) + norm(beat) * 0.4, 0.2)


def wing_chitter(rng):
    seconds = 0.32
    out = blank(seconds)
    at = 0.0
    while at < seconds - 0.06:
        length = rng.uniform(0.035, 0.06)
        t = times(length)
        chirp = throat(length, lambda t: np.full(len(t), rng.uniform(1300, 1900)), [(2400, 2200, 6, 1.0), (3700, 3400, 7, 0.5)], rng, rough=0.6, breath=0.35)
        place(out, chirp * bump(t, 0, 0.004, 0.018), at, rng.uniform(0.6, 1))
        at += length + rng.uniform(0.008, 0.024)
    return fade_end(out)


def wing_dies(rng):
    seconds = 1.5
    t = times(seconds)
    screech = throat(seconds, lambda t: np.geomspace(rng.uniform(1500, 1700), 420, len(t)), [(2300, 1000, 6, 1.0), (3700, 2100, 7, 0.55), (5100, 3500, 8, 0.2)], rng, rough=0.8, breath=0.45)
    out = fade_end(drive(screech, 1.7) * bump(t, 0, 0.012, 0.32), 0.3)
    place(out, cloth(0.5, rng, 1500), 0.28, 0.55)
    return place(out, fall(rng, 0.6), 0.75, 0.45)


def wing_alarm(rng):
    """The screech that calls the woods in: three harsh cries, each higher and longer than the last, with the wings beating hard."""
    seconds = 1.7
    out = blank(seconds)
    at = 0.0
    for i, length in enumerate((0.3, 0.36, 0.55)):
        t = times(length)
        top = rng.uniform(1700, 1850) * (1 + 0.08 * i)
        cry = throat(length, lambda t: arch(t, length, top * 0.75, top, top * 0.82, 0.25), [(2300, 1900, 6, 1.0), (3700, 3300, 7, 0.6), (5300, 4900, 8, 0.22)], rng, rough=0.8, breath=0.35)
        place(out, drive(cry, 2.0) * bump(t, 0, 0.01, length * 0.6), at, 0.75 + 0.1 * i)
        at += length + rng.uniform(0.07, 0.1)
    beat = whoosh(seconds, 300, 1400, 1.6, rng, peak=0.5, flutter=0.5, flutter_rate=17)
    return fade_end(out + norm(beat) * 0.3, 0.2)


# ----- Huldra: a woman's voice, and the rotten tree in her back -----

def huldra_wail(rng):
    seconds = 1.0
    t = times(seconds)
    hiss = noise_breath(0.28, 3000, 6500, 2, rng) * bump(times(0.28), 0, 0.03, 0.1)
    wail = throat(seconds, lambda t: arch(t, seconds, 390, rng.uniform(540, 600), 470, 0.6) * (1 + 0.012 * np.sin(TAU * 5.6 * t)), [(760, 1100, 9, 1.0), (1450, 2150, 10, 0.6), (3000, 3200, 12, 0.25)], rng, rough=0.1, breath=0.6)
    out = wail * bump(t, 0.05, 0.1, 0.5) + creak(seconds, rng, 200, 900) * bump(t, 0, 0.3, 0.5) * 0.28
    return place(fade_end(out, 0.25), hiss, 0.0, 0.8)


def huldra_call(rng):
    """Her lokk, the herding call (kulning) that draws the player in: a bright head voice scooping up to a high held note, breaking down a
    step and rising again. Purer than the wail (little breath, no hiss), so the pull is heard apart from her strike."""
    seconds = 2.6
    t = times(seconds)
    high = rng.uniform(800, 860)
    keys = [0.0, 0.22, 1.25, 1.36, 1.85, 2.2, seconds]
    notes = [high * 0.66, high, high, high * 0.75, high * 0.75, high * 0.89, high * 0.86]
    swell = np.clip((t - 0.35) / 0.6, 0, 1) * (1 - np.clip((t - 1.2) / 0.1, 0, 1)) + np.clip((t - 1.5) / 0.4, 0, 1)
    pitch = lambda t: np.interp(t, keys, notes) * (1 + 0.014 * np.sin(TAU * 5.4 * t) * swell)
    song = throat(seconds, pitch, [(900, 1000, 9, 1.0), (1500, 1700, 10, 0.5), (3100, 3300, 12, 0.28)], rng, rough=0.04, breath=0.18)
    # Held, as a call carries: a dip at the break, the second phrase nearly as strong as the first.
    held = np.interp(t, [0.0, 0.14, 1.2, 1.3, 1.42, 2.0, seconds], [0.0, 1.0, 0.86, 0.55, 0.82, 0.72, 0.0])
    # The lower note's harmonics fall between her formants, so it comes out about a third as loud: made up after the break.
    held = held * np.interp(t, [1.28, 1.42], [1.0, 3.2])
    return fade_end(song * held, 0.15)


def huldra_hurt(rng):
    seconds = 0.5
    t = times(seconds)
    gasp = throat(seconds, lambda t: arch(t, seconds, 500, rng.uniform(600, 680), 380, 0.2), [(850, 700, 9, 1.0), (1650, 1400, 10, 0.55), (3000, 2800, 12, 0.3)], rng, rough=0.25, breath=0.8)
    return fade_end(gasp * bump(t, 0, 0.012, 0.13) + crack(rng, rng.uniform(150, 240))[:len(t)] * 0.32)


def huldra_dies(rng):
    seconds = 2.3
    t = times(seconds)
    sigh = throat(seconds, lambda t: np.geomspace(rng.uniform(540, 590), 165, len(t)), [(820, 460, 8, 1.0), (1550, 900, 9, 0.5), (2950, 2100, 10, 0.25)], rng, rough=0.25, breath=1.0)
    out = fade_end(sigh * bump(t, 0, 0.03, 0.6) + creak(seconds, rng, 150, 800, (22, 8)) * bump(t, 0.4, 0.3, 0.8) * 0.3, 0.3)
    for at in (0.9, 1.25, 1.42):
        place(out, crack(rng, rng.uniform(110, 170)), at, 0.32)
    return out


# How much of the top end a throat keeps: a growl's roughness makes noise far above what a big chest would let out. By the first word of the name.
DARKER = {"Wolf": 1.0, "Troll": 3.0, "Stone": 3.0, "Shaman": 2.0, "Draug": 2.5, "Root": 1.5, "Boar": 1.0, "Bog": 1.5}

# Name, recipe, takes, how much of it is the forest's echo.
SOUNDS = [
    ("Wolf Snarl", wolf_snarl, 4, 0.12), ("Wolf Yelp", wolf_yelp, 4, 0.12), ("Wolf Dies", wolf_dies, 3, 0.14), ("Wolf Howl", wolf_howl, 3, 0.4), ("Paw Step", paw_step, 4, 0.05),
    ("Troll Roar", troll_roar, 4, 0.16), ("Troll Hurt", troll_hurt, 4, 0.14), ("Troll Dies", troll_dies, 3, 0.18), ("Shaman Call", shaman_call, 2, 0.3),
    ("Draug Moan", draug_moan, 4, 0.22), ("Draug Hurt", draug_hurt, 4, 0.14), ("Draug Dies", draug_dies, 3, 0.2), ("Mail Tread", mail_tread_with_rings, 4, 0.07),
    ("Root Strain", root_strain, 4, 0.14), ("Root Hurt", root_hurt, 4, 0.12), ("Root Dies", root_dies, 3, 0.18),
    ("Boar Charge", boar_charge, 4, 0.12), ("Boar Hurt", boar_hurt, 4, 0.1), ("Boar Dies", boar_dies, 3, 0.14),
    ("Bog Spit", bog_spit, 4, 0.1), ("Bog Hurt", bog_hurt, 4, 0.08), ("Bog Dies", bog_dies, 3, 0.12),
    ("Light Rises", light_rises, 4, 0.3), ("Light Hurt", light_hurt, 4, 0.22), ("Light Dies", light_dies, 3, 0.3),
    ("Wing Screech", wing_screech, 4, 0.18), ("Wing Chitter", wing_chitter, 4, 0.14), ("Wing Dies", wing_dies, 3, 0.16),
    ("Huldra Wail", huldra_wail, 4, 0.3), ("Huldra Hurt", huldra_hurt, 4, 0.2), ("Huldra Dies", huldra_dies, 3, 0.28),
    ("Huldra Call", huldra_call, 2, 0.45),
    ("Stone Skin", stone_skin, 2, 0.2), ("Light Splits", light_splits, 2, 0.3), ("Wing Alarm", wing_alarm, 2, 0.35), ("Root Regrowth", root_regrowth, 2, 0.25),
]


def main(only=None):
    for stem, recipe, count, wet in SOUNDS:
        if only and stem not in only:
            continue
        for i in range(1, count + 1):
            name = f"{stem} {i}"
            dry = recipe(seeded(name))
            weight = DARKER.get(stem.split()[0], 0.0)
            save(OUT, name, space(darken(dry, weight=weight) if weight else dry, FOREST, wet), mono=True)


if __name__ == "__main__":
    # Names after the script make only those (python Tools/Audio/creatures.py "Huldra Call"); none makes them all.
    main(sys.argv[1:] or None)

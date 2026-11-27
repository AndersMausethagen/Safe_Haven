"""The skills' big sounds (the user, 2026-11-27: "make sure that ultimates look, feel and sound satisfying ... Give them that AAA-feel"):
each ultimate in layers that play on top of its picked cast and release sounds (SkillLook.sounds): a riser under the wind-up, an impact
with a sub drop under it, and a tail that hangs after; War Cry's horn and drum; the payoffs' bursts, one for each status a skill uses up
(SkillDefinition.Combos); Raven's Mark's caw and Bear-Hide's growl. Two takes of each, so a repeat isn't the same sound.

  python Tools/Audio/skill_moments.py          every sound -> Assets/_Project/Audio/SFX/Safe Haven Skills/<name>_v<take>.wav
  python Tools/Audio/skill_moments.py NAME...  only those

Run with LocalSpawn's Python (numpy, scipy): D:\\ComfyUI\\.venv\\Scripts\\python.exe
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from foley import *  # noqa: E402,F403
from combat import flesh  # noqa: E402

OUT = os.path.join(HERE, "..", "..", "Assets", "_Project", "Audio", "SFX", "Safe Haven Skills")
TAKES = 2


# ----- Shared layers -----

def sub_drop(seconds, start=72.0, end=27.0, tau=0.9, drop=0.35):
    """The weight under an impact: a sine falling from start to end Hz, driven so small speakers still feel its harmonics."""
    t = times(seconds)
    pitch = end + (start - end) * np.exp(-t / drop)
    tone = np.sin(TAU * np.cumsum(pitch) / RATE) * np.exp(-t / tau) * np.clip(t / 0.004, 0, 1)
    return drive(tone, 2.2)


def rumble(seconds, rng, rise=True, low=25.0, high=140.0):
    """The ground answering: low noise swelling (or falling away)."""
    t = times(seconds)
    body = lowpass(band(pink(len(t), rng), low, high), high)
    body /= max(1e-9, np.abs(body).max())
    shape = (t / seconds) ** 2.2 if rise else np.exp(-t / (seconds * 0.35))
    return body * shape


def riser(seconds, rng, low, high, q=2.0):
    """Air drawn tight before the blow: a noise band climbing, louder and brighter to the end, with a tone gliding up under it."""
    t = times(seconds)
    x = t / seconds
    centre = low * (high / low) ** (x ** 1.6)
    air = sweep(pink(len(t), rng), centre, q) * x ** 2.4
    air /= max(1e-9, np.abs(air).max())
    glide = np.sin(TAU * np.cumsum(60 + 140 * x ** 2) / RATE) * x ** 3 * 0.5
    return air + glide


def ice_crack(rng, seconds=0.5, base=1700.0):
    """One crack of ice: a hard click and a short glassy ring."""
    t = times(seconds)
    click = band(white(len(t), rng), 2200, 9000) * decay(t, 0.004)
    glass = strike(iron(base * rng.uniform(0.8, 1.25), rng, hold=0.12, count=10), seconds, 0.0006, rng) * 0.6
    return click + glass


def crackle(seconds, rng, density=60, early=1.0):
    """Fire: pops and ticks scattered through it."""
    pops = scatter(seconds, density, lambda: chip(rng, 1800, 6500, 0.02), rng, early)
    knots = scatter(seconds, density // 5, lambda: chip(rng, 300, 900, 0.05), rng, early)
    return pops + knots * 0.6


def roar(seconds, rng, low=140.0, high=1600.0, swell=0.3, tau=None):
    """A wall of flame: band noise breathing slowly."""
    t = times(seconds)
    flame = band(pink(len(t), rng), low, high)
    flame /= max(1e-9, np.abs(flame).max())
    slow = lowpass(white(len(t), rng), 3)
    breath = 0.65 + 0.35 * slow / max(1e-9, np.abs(slow).max())
    env = bump(t, 0, swell, tau if tau else seconds * 0.5)
    return flame * breath * env


def wind(seconds, rng, rise=0.4, tau=None):
    """A howl: a narrow band wandering in pitch over a broad hiss."""
    t = times(seconds)
    wander = lowpass(white(len(t), rng), 1.5)
    wander /= max(1e-9, np.abs(wander).max())
    howl = sweep(pink(len(t), rng), 650 + 380 * wander, 9.0)
    howl /= max(1e-9, np.abs(howl).max())
    hiss = band(pink(len(t), rng), 300, 4000)
    hiss /= max(1e-9, np.abs(hiss).max())
    env = bump(t, 0, rise, tau if tau else seconds * 0.45)
    return (howl * 0.8 + hiss * 0.5) * env


# ----- The sounds -----

def ult_frost_rise(rng):
    s = 1.6
    out = riser(s, rng, 300, 4200, 3.0) * 0.8
    out += wind(s, rng, rise=1.2, tau=2.0) * 0.6
    out += scatter(s, 40, lambda: chip(rng, 3000, 9000, 0.015), rng, early=0.35) * 0.35
    out += rumble(s, rng) * 0.6
    return space(out, VALE, 0.25)


def ult_frost_impact(rng):
    s = 2.8
    out = sub_drop(s) * 1.1
    out += thump(rng, s, 45, 160, 0.25) * 0.7
    for _ in range(5):
        place(out, ice_crack(rng, 0.9), rng.uniform(0.0, 0.08), rng.uniform(0.5, 1))
    out += scatter(s, 120, lambda: chip(rng, 2500, 10000, 0.03), rng, early=2.6) * 0.5
    out += scatter(s, 18, lambda: ice_crack(rng, 0.4, 2600), rng, early=2.2) * 0.35
    return space(out, VALE, 0.3)


def ult_frost_tail(rng):
    s = 3.6
    out = wind(s, rng, rise=0.25, tau=1.6)
    out += scatter(s, 30, lambda: ice_crack(rng, 0.3, 3200) * 0.4, rng, early=1.4) * 0.3
    return space(out, VALE, 0.35)


def ult_fire_rise(rng):
    s = 1.3
    out = rumble(s, rng) * 0.9
    out += roar(s, rng, 120, 1400, swell=1.1, tau=2.0) * 0.8
    out += riser(s, rng, 200, 2600, 2.0) * 0.5
    out += crackle(s, rng, 40, early=0.4) * 0.4
    return space(out, VALE, 0.2)


def ult_fire_impact(rng):
    s = 3.2
    t = times(s)
    out = sub_drop(s, 80, 26, 1.1) * 1.2
    blast = lowpass(pink(len(t), rng), 1800) * bump(t, 0, 0.008, 0.45)
    out += drive(blast / max(1e-9, np.abs(blast).max()), 3.0) * 0.9
    out += roar(s, rng, 160, 2200, swell=0.05, tau=0.9) * 0.8
    out += crackle(s, rng, 140, early=1.8) * 0.5
    return space(out, VALE, 0.3)


def ult_fire_tail(rng):
    s = 3.2
    out = roar(s, rng, 100, 900, swell=0.2, tau=1.2) * 0.6
    out += crackle(s, rng, 90, early=1.2) * 0.6
    return space(out, FOREST, 0.3)


def ult_earth_rise(rng):
    s = 1.3
    out = rumble(s, rng, low=20, high=110) * 1.0
    out += scatter(s, 45, lambda: strike(stone(rng.uniform(500, 1400), rng), 0.12, 0.0008, rng) * 0.4, rng, early=0.4) * 0.5
    out += riser(s, rng, 120, 1400, 1.6) * 0.35
    return space(out, VALE, 0.2)


def ult_earth_impact(rng):
    s = 2.6
    out = sub_drop(s, 66, 24, 1.0) * 1.2
    out += thump(rng, s, 40, 150, 0.3) * 0.9
    out += strike(stone(90, rng, 24), s, 0.004, rng) * 0.8
    for _ in range(4):
        place(out, strike(stone(rng.uniform(220, 520), rng), 0.6, 0.002, rng), rng.uniform(0, 0.06), rng.uniform(0.4, 0.8))
    # The ground splitting: a broad crunch, and stones thrown up falling back.
    t = times(s)
    crunch = band(white(len(t), rng), 700, 6000) * bump(t, 0, 0.002, 0.07)
    out += crunch / max(1e-9, np.abs(crunch).max()) * 0.8
    place(out, scatter(1.8, 70, lambda: strike(stone(rng.uniform(700, 2400), rng), 0.08, 0.0006, rng), rng, early=1.3) * 0.45, 0.15)
    return space(out, VALE, 0.3)


def ult_earth_tail(rng):
    s = 2.6
    out = scatter(s, 80, lambda: strike(stone(rng.uniform(600, 2200), rng), 0.1, 0.0006, rng), rng, early=1.6) * 0.5
    out += rumble(s, rng, rise=False) * 0.5
    return space(out, VALE, 0.3)


def war_horn(rng):
    """A war horn's blast over a struck drum: a buzzing lip tone through a horn's resonance, swelling and holding."""
    s = 2.4
    t = times(s)
    f0 = 98 * (1 + 0.03 * np.clip(t / 0.15, 0, 1)) * (1 + 0.006 * np.sin(TAU * 5.2 * t))
    phase = np.cumsum(f0) / RATE
    buzz = sum(np.sin(TAU * k * phase) / k ** 0.8 for k in range(1, 14))
    horn = sweep(buzz, 520 * np.ones(len(t)), 1.4) + sweep(buzz, 1300 * np.ones(len(t)), 2.5) * 0.5 + lowpass(buzz, 2600) * 0.35
    horn /= max(1e-9, np.abs(horn).max())
    env = np.clip(t / 0.22, 0, 1) * np.clip((s - t) / 0.7, 0, 1)
    out = drive(horn * env, 1.6) * 0.8
    place(out, drum(62, 1.4, rng, tension=0.2) * 1.2, 0.0)
    place(out, sub_drop(1.2, 60, 30, 0.5) * 0.6, 0.0)
    return space(out, VALE, 0.3)


def payoff_bleed(rng):
    s = 0.8
    out = flesh(rng, weight=1.6, wet=1.6, seconds=s)
    out += sub_drop(s, 90, 40, 0.18) * 0.5
    out += bite_of_blade_like(rng, s) * 0.5
    return space(out, FOREST, 0.15)


def bite_of_blade_like(rng, s):
    t = times(s)
    return sweep(white(len(t), rng), 1800 + 4700 * np.exp(-t / 0.012), 1.6) * bump(t, 0, 0.0012, 0.02)


def payoff_sunder(rng):
    s = 1.0
    out = strike(iron(rng.uniform(700, 1000), rng, hold=0.35), s, 0.0008, rng) * 0.8
    t = times(s)
    out += band(white(len(t), rng), 900, 5000) * decay(t, 0.03) * 0.9
    place(out, scatter(0.4, 16, lambda: chip(rng, 3000, 9000, 0.02), rng, early=2.0), 0.0, 0.6)
    out += thump(rng, s, 60, 180, 0.08) * 0.7
    out += sub_drop(s, 80, 35, 0.2) * 0.5
    return space(out, FOREST, 0.15)


def payoff_roots(rng):
    s = 0.9
    out = blank(s)
    for _ in range(3):
        place(out, strike(wood(rng.uniform(180, 320), rng, damp=0.04), 0.5, 0.0005, rng), rng.uniform(0, 0.05), rng.uniform(0.6, 1))
    t = times(s)
    out += band(white(len(t), rng), 1500, 7000) * decay(t, 0.006) * 0.6
    out += thump(rng, s, 50, 160, 0.12) * 0.8
    return space(out, FOREST, 0.15)


def payoff_cowed(rng):
    s = 0.9
    t = times(s)
    out = thump(rng, s, 40, 140, 0.18) * 1.0
    out += sub_drop(s, 70, 30, 0.3) * 0.7
    whoomp = sweep(pink(len(t), rng), 900 * np.exp(-t / 0.12) + 120, 1.5) * bump(t, 0, 0.01, 0.15)
    out += whoomp / max(1e-9, np.abs(whoomp).max()) * 0.6
    return space(out, FOREST, 0.2)


def payoff_burn(rng):
    s = 1.1
    t = times(s)
    whoompf = lowpass(pink(len(t), rng), 2500) * bump(t, 0, 0.02, 0.18)
    out = drive(whoompf / max(1e-9, np.abs(whoompf).max()), 2.0) * 0.9
    out += roar(s, rng, 200, 2400, swell=0.04, tau=0.35) * 0.7
    out += crackle(s, rng, 40, early=1.5) * 0.5
    out += sub_drop(s, 80, 35, 0.2) * 0.5
    return space(out, FOREST, 0.15)


def raven_caw(rng):
    """Two harsh caws and the beat of wings."""
    s = 1.1
    out = blank(s)
    for start, length in ((0.0, 0.26), (0.36, 0.3)):
        n = int(length * RATE)
        x = np.linspace(0, 1, n)
        pitch = 560 - 160 * x
        formants = [(1150, 5, 1.0), (2300, 7, 0.55), (3300, 9, 0.2)]
        caw = voice(pitch, formants, rng, rough=0.7, breath=0.25) * np.sin(np.pi * x) ** 0.5
        place(out, caw / max(1e-9, np.abs(caw).max()), start)
    for k in range(3):
        place(out, whoosh(0.16, 300, 1400, 1.2, rng) * 0.4, 0.62 + k * 0.13)
    return space(out, FOREST, 0.2)


def bear_growl(rng):
    """A bear's growl rising to a short roar, and a thump of weight."""
    s = 1.7
    n = int(s * RATE)
    x = np.linspace(0, 1, n)
    pitch = 70 + 40 * np.clip((x - 0.45) / 0.3, 0, 1) - 20 * np.clip((x - 0.8) / 0.2, 0, 1)
    formants = [(420, 3, 1.0), (850, 4, 0.6), (1900, 6, 0.25)]
    growl = voice(pitch, formants, rng, rough=1.0, breath=0.3)
    env = np.clip(x / 0.15, 0, 1) * np.clip((1 - x) / 0.2, 0, 1) * (0.6 + 0.4 * np.clip((x - 0.4) / 0.3, 0, 1))
    out = growl / max(1e-9, np.abs(growl).max()) * env
    out += thump(rng, s, 50, 150, 0.15) * 0.5
    return space(out, FOREST, 0.2)


SOUNDS = {
    "ult_frost_rise": ult_frost_rise, "ult_frost_impact": ult_frost_impact, "ult_frost_tail": ult_frost_tail,
    "ult_fire_rise": ult_fire_rise, "ult_fire_impact": ult_fire_impact, "ult_fire_tail": ult_fire_tail,
    "ult_earth_rise": ult_earth_rise, "ult_earth_impact": ult_earth_impact, "ult_earth_tail": ult_earth_tail,
    "war_horn": war_horn,
    "payoff_bleed": payoff_bleed, "payoff_sunder": payoff_sunder, "payoff_roots": payoff_roots, "payoff_cowed": payoff_cowed,
    "payoff_burn": payoff_burn,
    "raven_caw": raven_caw, "bear_growl": bear_growl,
}


if __name__ == "__main__":
    wanted = [a for a in sys.argv[1:] if a in SOUNDS] or list(SOUNDS)
    for name in wanted:
        for take in range(1, TAKES + 1):
            rng = seeded(f"{name} {take}")
            sound = SOUNDS[name](rng)
            save(OUT, f"skill_{name}_v{take}", sound, mono=True)
            print("wrote", f"skill_{name}_v{take}")

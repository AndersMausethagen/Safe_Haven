"""Synthesises the sounds of a fight: blades through the air, into bodies and against each other.

A blow into a body is mixed from recordings (Tools/Audio/Recorded, CC0: Kenney's punches and knife, OpenGameArt's hits and wet steps) because
synthesis never sounded like flesh: a real thump, a real blade and a real squelch, their attacks lined up, varied in every variant. A critical is
more of the same rather than a louder sting. The rest is synthesised. A parry has to cut through a brawl, so it stays the
brightest thing here: two iron edges, sharp contact, long dark ring, a scrape as they slide apart. Hits vary in every
variant, since these are the sounds a player hears thousands of times.

Run from the project root: python Tools/Audio/combat.py [stem ...] (all, or only the named sounds)
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Combat"


def flesh(rng, weight=1.0, wet=1.0, seconds=0.5):
    t = times(seconds)
    body = thump(rng, seconds, 70, 200, 0.06 * weight) * weight * 0.8
    crack = band(white(len(t), rng), 1400, 6000) * decay(t, 0.0035) * 0.6
    meat = band(pink(len(t), rng), 260, 1500) * decay(t, 0.045 * weight) * 2.6
    squelch = np.zeros(len(t))
    for _ in range(rng.integers(3, 7)):
        start = rng.uniform(0.006, 0.1)
        centre = rng.uniform(450, 1900)
        squelch += band(white(len(t), rng), centre * 0.75, centre * 1.3) * decay(t, rng.uniform(0.006, 0.02), start) * rng.uniform(0.4, 1)
    return body + crack + meat + squelch * 1.3 * wet


def bite_of_blade(rng, seconds=0.5):
    t = times(seconds)
    return sweep(white(len(t), rng), 1800 + 4700 * np.exp(-t / 0.012), 1.6) * bump(t, 0, 0.0012, 0.018)


def swing(rng):
    length = rng.uniform(0.3, 0.38)
    air = whoosh(length, 400, 2600, 1.4, rng) + 0.6 * whoosh(length, 180, 520, 1.0, rng)
    edge = whoosh(length, 3000, 4600, 9, rng, peak=0.5) * 0.12
    return darken(air + edge)


def heavy_swing(rng):
    length = rng.uniform(0.56, 0.66)
    air = whoosh(length, 120, 1100, 1.1, rng, peak=0.55, flutter=0.5, flutter_rate=32)
    return darken(air + 0.8 * whoosh(length, 70, 240, 1.0, rng, peak=0.55), weight=2)


def axe_swing(rng):
    length = rng.uniform(0.46, 0.54)
    air = whoosh(length, 140, 1500, 1.2, rng, peak=0.5, flutter=0.7, flutter_rate=22)
    return darken(air + 0.6 * whoosh(length, 90, 300, 1.0, rng, peak=0.5), weight=2)


def thrust_swing(rng):
    """A dagger's stab or a spear's thrust (Docs/CombatFeelReview_2026-11-19.md §10): short and narrow, rising, ending sharp."""
    length = rng.uniform(0.16, 0.2)
    t = times(length)
    x = t / length
    air = sweep(pink(len(t), rng), 1200 + 2800 * x ** 1.5, 6) * x ** 1.2 * np.clip((1 - x) / 0.08, 0, 1)
    return darken(air / max(1e-9, np.abs(air).max()) + 0.25 * cloth(length, rng, 2200), top=-3, weight=2)


def blunt_swing(rng):
    """A hammer, club or maul: a slow heavy mass, low and fluttering as it turns over."""
    length = rng.uniform(0.48, 0.52)
    air = whoosh(length, 70, 600, 1.0, rng, peak=0.6, flutter=0.9, flutter_rate=18)
    return darken(air + 0.7 * whoosh(length, 50, 160, 1.0, rng, peak=0.6), weight=3)


def great_blade_swing(rng):
    """A greatsword: the axe's body of air and a long steel edge singing over it."""
    length = rng.uniform(0.5, 0.56)
    air = whoosh(length, 140, 1500, 1.2, rng, peak=0.5, flutter=0.5, flutter_rate=22)
    edge = whoosh(length, 3000, 4500, 10, rng, peak=0.55) * 0.18
    return darken(air + 0.5 * whoosh(length, 90, 300, 1.0, rng, peak=0.5) + edge, weight=2)


def punch_swing(rng):
    length = rng.uniform(0.2, 0.25)
    return darken(whoosh(length, 300, 1300, 1.2, rng, peak=0.55) + 0.4 * cloth(length, rng, 1500))


def dodge(rng):
    length = rng.uniform(0.36, 0.44)
    air = whoosh(length, 180, 900, 1.0, rng, peak=0.4)
    t = times(length)
    scuff = band(pink(len(t), rng), 200, 1400) * bump(t, length * 0.55, 0.02, 0.05) * 0.35
    return darken(air * 0.8 + cloth(length, rng, 1300) * 0.9 + scuff)


def perfect_dodge(rng):
    """Time slips: the air drops out under a hollow fall, with a dark ring left behind."""
    length = 1.0
    t = times(length)
    drop = thump(rng, length, 42, 200, 0.2, sweep_tau=0.09) * 0.6
    pass_by = whoosh(0.45, 260, 2000, 1.3, rng, peak=0.3)
    shimmer = lowpass(strike(iron(620, rng, hold=0.5), length, 0.0015, rng), 3200) * np.clip(t / 0.06, 0, 1) * 0.12
    mix = place(place(blank(length), drop, 0), pass_by, 0, 0.8) + shimmer
    pan = np.clip(t / 0.35, 0, 1)
    return space(np.stack([mix * (1.2 - 0.4 * pan), mix * (0.8 + 0.4 * pan)], axis=1), FOREST, 0.5)


def recording(name, rng, pitch=(0.92, 1.08), seconds=None):
    """A recorded stem from Tools/Audio/Recorded (Kenney and OpenGameArt, CC0): at full level, its attack at the very start,
    pitched a little differently each time and, if asked, cut short with a quick fade."""
    rate, data = wavfile.read(os.path.join(os.path.dirname(__file__), "Recorded", name + ".wav"))
    signal = data.astype(np.float64) / 32768
    signal = signal[max(0, np.argmax(np.abs(signal) > np.abs(signal).max() * 0.1) - 16):]
    step = rate / RATE * rng.uniform(*pitch)
    signal = np.interp(np.arange(0, len(signal) - 1, step), np.arange(len(signal)), signal)
    if seconds is not None:
        signal = signal[:int(seconds * RATE)]
        fade = min(len(signal) // 2, int(0.04 * RATE))
        signal[-fade:] *= np.linspace(1, 0, fade) ** 2
    return signal / max(1e-9, np.abs(signal).max())


def one_of(rng, *names):
    return names[rng.integers(len(names))]


def thud(rng, heavy=False):
    """The body taking the blow."""
    size = "heavy" if heavy else "medium"
    return recording(f"impactPunch_{size}_00{rng.integers(5)}", rng)


def meat(rng):
    """The flesh of it, with some mid-range for it to sit in."""
    return recording(one_of(rng, "sfx100v2_hit_01", "sfx100v2_hit_02"), rng)


def wet(rng, seconds=0.2):
    """What gives way: a real squelch, cut short."""
    return recording(f"sfx100v2_footstep_wet_0{rng.integers(1, 4)}", rng, (0.8, 1.05), seconds)


def blade(rng, seconds=0.14, pitch=(0.9, 1.1)):
    """Steel going through: the first moments of a knife's slice."""
    return recording(one_of(rng, "knifeSlice", "knifeSlice2"), rng, pitch, seconds)


def tick(rng):
    """The steel itself, barely there: a blade doesn't ring once it is buried in something soft."""
    return lowpass(recording(f"impactMetal_light_00{rng.integers(5)}", rng, (0.9, 1.1), 0.07), 5000)


def layers(seconds, *parts):
    """Each part as (sound, level, milliseconds after the contact). The small delays make it a blow rather than a stack of samples."""
    mix = blank(seconds)
    for sound, level, later in parts:
        mix = place(mix, sound, later / 1000, level)
    return mix


def finish(mix, amount=1.5):
    """Holds it together and takes the peak off, so the weight still reads under the attack's transient."""
    return drive(mix / max(1e-9, np.abs(mix).max()), amount)


def hit(rng):
    """A blade into a body: the steel touches, it slices, the flesh takes the weight, the wet follows."""
    return finish(layers(0.5, (blade(rng), 0.42, 0), (tick(rng), 0.05, 0), (thud(rng), 0.85, 3), (meat(rng), 0.5, 2), (wet(rng), 0.5, 12)))


def critical_hit(rng):
    """The same, deeper and harder, with something breaking."""
    return finish(layers(0.8, (blade(rng, 0.2, (0.8, 0.95)), 0.55, 0), (tick(rng), 0.06, 0), (thud(rng, True), 0.7, 2), (recording("chop", rng, (0.8, 0.95)), 0.6, 6),
                         (meat(rng), 0.8, 3), (wet(rng, 0.3), 0.7, 14)), 1.8)


def axe_hit(rng):
    """A heavy edge biting deep: more of the body and the bite than of the blade."""
    return finish(layers(0.6, (recording("chop", rng, (0.72, 0.92)), 0.85, 0), (thud(rng, True), 0.65, 2), (meat(rng), 0.8, 3), (blade(rng, 0.09, (0.7, 0.85)), 0.25, 0), (wet(rng, 0.26), 0.6, 16)), 1.7)


def stab_hit(rng):
    """A point going in: quick and narrow, little weight, and a lot of wet."""
    return finish(layers(0.4, (blade(rng, 0.09, (1, 1.2)), 0.42, 0), (recording("sfx100v2_hit_03", rng), 0.28, 1), (thud(rng), 0.55, 3), (wet(rng, 0.26), 0.75, 8)))


def blunt_hit(rng):
    """A hammer or a club: no edge to it at all, all weight."""
    return finish(layers(0.7, (thud(rng, True), 0.7, 0), (recording(f"impactSoft_heavy_00{rng.integers(5)}", rng), 0.4, 0), (meat(rng), 1, 1), (recording("chop", rng, (0.55, 0.7)), 0.3, 2), (wet(rng, 0.18), 0.35, 14)), 1.8)


def punch_hit(rng):
    return finish(layers(0.4, (thud(rng), 1, 0), (meat(rng), 0.45, 1), (wet(rng, 0.1), 0.12, 8)))


def glancing_blow(rng):
    seconds = 0.34
    skid = scrape(rng.uniform(0.14, 0.22), rng.uniform(1100, 1500), rng)
    tick = strike(iron(rng.uniform(1500, 1900), rng, hold=0.08, count=14), seconds, 0.0004, rng)
    return darken(place(blank(seconds), skid, 0.004, 0.8) + tick * 0.6 + thump(rng, seconds, 80, 170, 0.03) * 0.5, top=-3)


def block(rng):
    seconds = 0.7
    t = times(seconds)
    planks = strike(wood(rng.uniform(240, 330), rng, damp=0.05, count=16), seconds, 0.0007, rng)
    shudder = band(pink(len(t), rng), 300, 2000) * decay(t, 0.03) * 1.1
    rim = strike(iron(rng.uniform(1100, 1400), rng, hold=0.13), seconds, 0.0004, rng) * 0.6
    return darken(drive(planks + shudder + rim + thump(rng, seconds, 70, 160, 0.05) * 0.6, 1.8), top=-3)


def parry(rng):
    seconds = 1.3
    contact = strike(iron(rng.uniform(1250, 1550), rng, hold=0.8), seconds, 0.00025, rng)
    weight = strike(iron(rng.uniform(380, 460), rng, hold=0.35, count=16), seconds, 0.0005, rng) * 0.7
    slide = scrape(0.12, rng.uniform(1600, 2000), rng)
    mix = place(contact + weight + thump(rng, seconds, 75, 170, 0.05) * 0.55, slide, 0.05, 0.3)
    return darken(mix, top=-2, weight=2)


def parry_cue(rng):
    """The moment to raise the guard, heard a breath before it (Docs/CombatFeelReview_2026-11-19.md §2): two small bright iron partials
    that die in 70 ms, so it never reads as a parry itself. One take only: a cue must sound the same every time."""
    seconds = 0.3
    modes = [(2400.0, 1.0, 0.07), (2400.0 * 1.0016, 0.55, 0.06), (3600.0, 0.6, 0.05), (3600.0 * 1.0021, 0.35, 0.045)]
    return strike(modes, seconds, 0.00018, rng)


def interrupt_break(rng):
    """A strike broken off (§10): a dry crack, not the clang of a guard, and its gear rattling as the body is thrown off its blow."""
    seconds = 0.3
    t = times(seconds)
    crack = band(white(len(t), rng), 900, 5200) * decay(t, 0.006) + 0.6 * band(pink(len(t), rng), 300, 1400) * decay(t, 0.02)
    fall = place(blank(seconds), rattle(rng, 0.2, 5) * 0.45, 0.02)
    return darken(crack / max(1e-9, np.abs(crack).max()) + fall + thump(rng, seconds, 70, 160, 0.04) * 0.35, top=-3, weight=2)


def charge_rise(rng):
    """A heavy held to charge (§11): air and grip tightening for 0.95 s, so letting go early or late is heard, not counted."""
    seconds = 0.95
    t = times(seconds)
    x = t / seconds
    air = sweep(pink(len(t), rng), 200 + 1600 * x ** 1.3, 3) * (0.25 + 0.75 * x ** 1.5)
    creak = scatter(seconds, 7, lambda: chip(rng, 300, 900, 0.05), rng, early=0.8) * 0.2
    hum = np.sin(TAU * np.cumsum(90 + 60 * x) / RATE) * x ** 2 * 0.2
    return fade_end(air / max(1e-9, np.abs(air).max()) * 0.8 + creak + hum, 0.05)


def fight_won(rng):
    """The last foe of a fight down (§8): one low drum stroke and a long breath of wind after it. One take: a beat, not a variation."""
    seconds = 1.6
    t = times(seconds)
    stroke = drum(rng.uniform(62, 68), seconds, rng, tension=0.08) * 0.9
    breath = band(pink(len(t), rng), 180, 900) * bump(t, 0.12, 0.45, 0.5) * 0.35
    return darken(fade_end(stroke + breath, 0.3), top=-6, weight=3)


def guard_break(rng):
    seconds = 1.2
    split = strike(wood(rng.uniform(150, 200), rng, damp=0.1, count=16), seconds, 0.0012, rng)
    clang = strike(iron(rng.uniform(560, 720), rng, hold=0.32), seconds, 0.0005, rng) * 0.6
    splinters = scatter(0.5, 24, lambda: chip(rng, 700, 2600), rng) * 0.6
    sub = thump(rng, seconds, 44, 150, 0.16, sweep_tau=0.04) * 0.7
    return darken(drive(place(split + clang + sub, splinters, 0.01), 2.4), top=-3, weight=2)


def rattle(rng, seconds=0.4, count=9):
    """A body's gear shaken: buckles and mail settling."""
    return scatter(seconds, count, lambda: strike(iron(rng.uniform(1700, 3400), rng, hold=0.03, count=6), 0.05, 0.0002, rng), rng, early=1.3)


def stagger(rng):
    seconds = 0.8
    gear = place(blank(seconds), rattle(rng, 0.45), 0.02, 0.2)
    return darken(drive(flesh(rng, 1.4, 0.5, seconds) + gear, 1.6), weight=2)


def heavy_windup(rng):
    """A great blow gathering: a low swell under the wind-up, so a heavy strike is heard coming before it is seen."""
    seconds = rng.uniform(0.62, 0.72)
    t = times(seconds)
    x = t / seconds
    swell = x ** 1.6 * np.clip((1 - x) / 0.12, 0, 1)
    rumble = band(pink(len(t), rng), 55, 260) * swell
    sub = np.sin(TAU * np.cumsum(rng.uniform(44, 52) + 10 * x) / RATE) * swell * 0.35
    grit = scatter(seconds, 14, lambda: chip(rng, 160, 520, 0.06), rng, early=0.5) * 0.25
    return darken(drive(rumble / max(1e-9, np.abs(rumble).max()) + sub + grit, 1.4), top=-6, weight=3)


def player_hurt(rng):
    """A blow landing on you, close, and the breath going out."""
    seconds = 0.7
    n = int(seconds * RATE)
    gasp_length = rng.uniform(0.2, 0.28)
    m = int(gasp_length * RATE)
    t = times(gasp_length)
    pitch = rng.uniform(105, 130) * np.exp(-t / 0.5)
    tract = [(rng.uniform(480, 560), 5, 1.0), (rng.uniform(1000, 1200), 6, 0.6), (2400.0, 8, 0.2)]
    gasp = fade_end(voice(pitch, tract, rng, rough=0.3, breath=0.9) * bump(t, 0, 0.018, gasp_length * 0.3), 0.3)
    mix = flesh(rng, 1.15, 0.5, seconds) + band(pink(n, rng), 100, 400) * decay(times(seconds), 0.05) * 0.4
    return space(darken(place(mix, gasp / max(1e-9, np.abs(gasp).max()) * 0.32, 0.025)), HUT, 0.25)


def land(rng):
    seconds = 0.45
    t = times(seconds)
    soil = band(pink(len(t), rng), 250, 1600) * bump(t, 0.002, 0.004, 0.045) * 0.5
    gear = place(blank(seconds), rattle(rng, 0.2, 5), 0.015, 0.05)
    return darken(thump(rng, seconds, 62, 130, 0.07) + soil + gear, weight=2)


# Which recipe, how many variants and the file name's stem; anything played from a fixed point on screen stays stereo.
SOUNDS = [
    ("Swing", swing, 4, True), ("Heavy Swing", heavy_swing, 3, True), ("Axe Swing", axe_swing, 3, True), ("Punch Swing", punch_swing, 4, True),
    ("Thrust Swing", thrust_swing, 4, True), ("Blunt Swing", blunt_swing, 3, True), ("Great Blade Swing", great_blade_swing, 3, True),
    ("Dodge", dodge, 4, True), ("Perfect Dodge", perfect_dodge, 2, False),
    ("Hit", hit, 8, True), ("Critical Hit", critical_hit, 5, True), ("Axe Hit", axe_hit, 5, True), ("Stab Hit", stab_hit, 5, True), ("Blunt Hit", blunt_hit, 5, True), ("Punch Hit", punch_hit, 5, True), ("Glancing Blow", glancing_blow, 4, True),
    ("Block", block, 5, True), ("Parry", parry, 5, True), ("Parry Cue", parry_cue, 1, True), ("Guard Break", guard_break, 4, True), ("Stagger", stagger, 4, True), ("Heavy Windup", heavy_windup, 3, True),
    ("Interrupt Break", interrupt_break, 4, True), ("Charge Rise", charge_rise, 1, False), ("Fight Won", fight_won, 1, False),
    ("Player Hurt", player_hurt, 6, False), ("Land", land, 4, True),
]


def main(only=None):
    """Makes every sound, or only those whose stems are named on the command line."""
    for stem, recipe, count, positional in SOUNDS:
        if only and stem not in only:
            continue
        for i in range(1, count + 1):
            name = f"{stem} {i}"
            sound = recipe(seeded(name))
            if positional:
                sound = space(sound, FOREST, 0.1)
            save(OUT, name, sound, mono=positional)


if __name__ == "__main__":
    main(sys.argv[1:])

"""Unlit copies of the flames the opening cinematic puts out (Docs/BackstoryCinematic.md), made from the painting's own
layer so they sit exactly over it: the hearth fire of shot 5 ("The flame disappears") and the three lanterns of shot 9
("Their lanterns disappear one at a time"). A dark patch over a painted flame left it showing round the edges.

Inside a soft circle round each flame, the layer's fire colours become what is left when it is out: cold ash on the hearth,
dark glass in a lantern, and the firelight on what is near goes too. Everything else is transparent. The cinematic lays
each copy over its layer, at the same depth and size, and fades it in when the flame dies.

  -> Assets/_Project/Art/Cinematics/Backstory/o05x_cold_hearth(_middle).png, o09x_lantern_1..3.png

Run with LocalSpawn's Python after generate_backstory.py layers: D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/opening_unlit.py
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Cinematics", "Backstory"))


def unlit(layer, out, spots, ash, keep_light=0.25):
    """spots: (u, v, radius) with u, v from the bottom left as shares of the picture and the radius a share of its height."""
    image = np.asarray(Image.open(os.path.join(ART, layer)).convert("RGBA")).astype(np.float32) / 255.0
    h, w = image.shape[:2]
    rgb, alpha = image[..., :3], image[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    # How much a pixel is fire or firelight: warm (red over blue) and bright.
    warm = np.clip((r - b) * 1.8, 0, 1) * np.clip((r + g) * 0.7 - 0.2, 0, 1)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    region = np.zeros((h, w), np.float32)
    for u, v, radius in spots:
        distance = np.hypot((xs / w - u) * w / h, (ys / h - (1 - v))) / radius
        region = np.maximum(region, np.clip(1 - distance, 0, 1) ** 0.5)
    luminance = 0.3 * r + 0.59 * g + 0.11 * b
    cold = np.stack([luminance * ash[0], luminance * ash[1], luminance * ash[2]], axis=-1) + np.array(ash[3:], np.float32)
    # The flame itself all the way; what it lit, partly (it goes darker and colder, not black).
    cover = region * alpha * np.clip(keep_light + (1 - keep_light) * warm * 1.4, 0, 1)
    result = np.concatenate([np.clip(cold, 0, 1), cover[..., None]], axis=-1)
    Image.fromarray((result * 255).astype(np.uint8), "RGBA").save(os.path.join(ART, out))
    return out


def main():
    # Ash: a dark blue-grey, about a sixth as bright, like the moonlit snow round it.
    # The flame spans two layers (the depth split put its top in the middle one): each gets its own unlit copy.
    hearth = [(0.589, 0.27, 0.11), (0.587, 0.34, 0.08), (0.583, 0.41, 0.07)]
    ash = (0.15, 0.17, 0.21, 0.025, 0.03, 0.045)
    made = [unlit("o05_fire_dies_2_near.png", "o05x_cold_hearth.png", hearth, ash),
            unlit("o05_fire_dies_1_middle.png", "o05x_cold_hearth_middle.png", hearth, ash)]
    # Unlit glass: dark, a little green from the mist.
    for i, (u, v) in enumerate([(0.520, 0.274), (0.429, 0.262), (0.352, 0.260)], start=1):
        made.append(unlit("o09_first_four_1_middle.png", f"o09x_lantern_{i}.png", [(u, v, 0.034)],
                          (0.12, 0.14, 0.14, 0.04, 0.05, 0.05), keep_light=0.4))
    print("\n".join(os.path.join(ART, name) for name in made))


if __name__ == "__main__":
    main()

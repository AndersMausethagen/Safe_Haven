"""Cuts a painting into layers by distance, so a camera move can slide the near things over the far ones.

A depth estimate (LocalSpawn's Depth Anything V2) says how near every pixel is. The painting is split where the
depths fall into natural groups. Each layer keeps everything at its distance and nearer. What the nearer layers
cover has to be painted in, because a sliver of it shows when they slide aside: it is filled with its own
surroundings mirrored across the edge, so grass continues as grass with its brushwork, where a smooth fill would
show as a blurred band. Edges are feathered.

Used by generate_backstory.py; run that, not this.
"""
import os
import sys

import numpy as np
from PIL import Image

AGENT = r"D:\ComfyUI\agent"
NAMES = {1: ["whole"], 2: ["far", "near"], 3: ["far", "middle", "near"], 4: ["far", "middle", "near", "front"],
         5: ["sky", "far", "middle", "near", "front"]}
# Where each layer stands for the cinematic camera: 0 is the subject, larger is further, negative is in front of
# it. Fixed by the layer's place in the row, not taken from the estimate: its distances are only relative, and a
# painting needs its layers a readable step apart more than it needs them measured.
DEPTHS = {1: [0.0], 2: [2.0, 0.0], 3: [3.0, 0.5, -0.3], 4: [4.0, 1.0, 0.0, -0.45], 5: [8.0, 3.0, 1.0, 0.0, -0.45]}
# How far a nearer layer's edge is grown before the layer behind is painted in under it, and its own soft rim (pixels
# at 1920 wide). The estimate's edges are a few pixels off; a thin rim of background riding on the near layer hides it.
GROW_NEAR = 3
GROW_FILL = 11
FEATHER = 1.6
KEEP_COLOUR = 28
# How far into a covered area the mirrored fill is trusted before it gives way to the smooth one (never seen that deep).
MIRROR_REACH = 150


def fill_covered(rgb, covered, scale):
    """The picture with the covered area painted in: mirrored surroundings near its edge, a smooth fill deep inside."""
    import cv2
    from scipy import ndimage
    height, width = covered.shape
    small = cv2.resize(rgb, (width // 2, height // 2), interpolation=cv2.INTER_AREA)
    mask = cv2.resize(covered.astype(np.uint8) * 255, (width // 2, height // 2), interpolation=cv2.INTER_NEAREST)
    smooth = cv2.resize(cv2.inpaint(small, mask, 5, cv2.INPAINT_TELEA), (width, height), interpolation=cv2.INTER_CUBIC)

    # For every covered pixel, the nearest pixel that is not, and the point as far beyond that again.
    distance, (near_y, near_x) = ndimage.distance_transform_edt(covered, return_indices=True)
    yy, xx = np.mgrid[0:height, 0:width]
    from_y = np.clip(2 * near_y - yy, 0, height - 1)
    from_x = np.clip(2 * near_x - xx, 0, width - 1)
    mirrored = rgb[from_y, from_x]
    usable = covered & ~covered[from_y, from_x]
    trust = np.clip(1.0 - distance / (MIRROR_REACH * scale), 0.0, 1.0) * usable
    trust = ndimage.gaussian_filter(trust.astype(np.float32), 1.5 * scale)[..., None]
    filled = mirrored * trust + smooth * (1.0 - trust)
    soft = ndimage.gaussian_filter(covered.astype(np.float32), 1.2 * scale)[..., None]
    return (rgb * (1.0 - soft) + filled * soft).astype(np.uint8)


def estimate_depth(image):
    """Nearness of every pixel, 0 far to 1 near."""
    if AGENT not in sys.path:
        sys.path.insert(0, AGENT)
    import controlnet
    return np.asarray(controlnet.depth(image), dtype=np.float32) / 255.0


def split(painting_path, out_dir, name, count):
    """Writes <name>_<n>_<layer>.png (far first) and returns [{file, name, depth, share}]."""
    from scipy import ndimage
    from skimage.filters import threshold_multiotsu

    image = Image.open(painting_path).convert("RGB")
    rgb = np.asarray(image)
    height, width = rgb.shape[:2]
    scale = width / 1920.0
    names = NAMES[count]
    if count == 1:
        file = f"{name}_0_whole.png"
        image.save(os.path.join(out_dir, file))
        return [{"file": file, "name": "whole", "depth": 0.0, "share": 1.0}]

    near = ndimage.gaussian_filter(estimate_depth(image), 2.0 * scale)
    cuts = threshold_multiotsu(near, classes=count)
    layers = []
    for index in range(count):
        lower = cuts[index - 1] if index > 0 else -1.0
        upper = cuts[index] if index < count - 1 else 2.0
        own = (near >= lower) & (near < upper)
        colour = rgb
        if index < count - 1:
            # Paint in what the nearer layers cover.
            covered = ndimage.binary_dilation(near >= upper, iterations=max(1, round(GROW_FILL * scale)))
            colour = fill_covered(rgb, covered, scale)

        entry = {"name": names[index], "depth": DEPTHS[count][index], "share": round(float(own.mean()), 3)}
        if index == 0:
            entry["file"] = f"{name}_{index}_{names[index]}.png"
            Image.fromarray(colour).save(os.path.join(out_dir, entry["file"]))
        else:
            shown = ndimage.binary_dilation(near >= lower, iterations=max(1, round(GROW_NEAR * scale)))
            alpha = np.clip(ndimage.gaussian_filter(shown.astype(np.float32), FEATHER * scale), 0.0, 1.0)
            # Colour is only kept near what shows: the rest is flat, which a PNG stores in almost nothing.
            kept = ndimage.binary_dilation(shown, iterations=max(1, round(KEEP_COLOUR * scale)))
            flat = colour[shown].mean(axis=0).astype(np.uint8) if shown.any() else np.zeros(3, np.uint8)
            colour = np.where(kept[..., None], colour, flat)
            rgba = np.dstack([colour, (alpha * 255).astype(np.uint8)])
            entry["file"] = f"{name}_{index}_{names[index]}.png"
            Image.fromarray(rgba, "RGBA").save(os.path.join(out_dir, entry["file"]))
        layers.append(entry)

    return layers

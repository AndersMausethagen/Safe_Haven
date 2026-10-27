"""Turns downloaded CC0 ground textures (fetch_polyhaven.py) into terrain layer maps:

    python AgentScripts/PolyHaven/make_terrain_layers.py <work dir> <Assets/_Project/Art/Terrain/World>

For each layer below: <Layer>/<Layer>_Color.jpg, <Layer>_Normal.jpg (OpenGL, as Unity expects) and <Layer>_Mask.png in
Unity's terrain mask layout (R metallic, G occlusion, B height for height blending, A smoothness). WorldAssets
(Safe Haven › World › Set Up World Assets) makes the TerrainLayer assets from them.
"""
import os
import shutil
import sys

from PIL import Image, ImageOps

LAYERS = {
    # Raid forests (dark, Gord-like)
    "Pine Needle Floor": "forrest_ground_03",
    "Mossy Forest Floor": "forest_leaves_02",
    "Muddy Bank": "brown_mud_leaves_01",
    "Gravel": "rocky_trail",  # river beds and the scree at the foot of cliffs
    "Cliff Rock": "mossy_rock",
    # The Haven (light and green)
    "Meadow Grass": "Grass004",
    "Lush Grass": "Grass001",
    "Trodden Path": "Ground030",
    "Damp Earth": "Ground037",
}


def find(folder, asset, kind):
    for name in os.listdir(folder):
        if name.startswith(f"{asset}_{kind}_"):
            return os.path.join(folder, name)
    return None


def main(work, out_root):
    for layer, asset in LAYERS.items():
        source = os.path.join(work, asset)
        folder = os.path.join(out_root, layer)
        os.makedirs(folder, exist_ok=True)
        shutil.copyfile(find(source, asset, "diff"), os.path.join(folder, f"{layer}_Color.jpg"))
        shutil.copyfile(find(source, asset, "nor_gl"), os.path.join(folder, f"{layer}_Normal.jpg"))
        arm = Image.open(find(source, asset, "arm")).convert("RGB")
        ao, rough, metal = arm.split()
        disp_path = find(source, asset, "disp")
        height = Image.open(disp_path).convert("L").resize(arm.size) if disp_path else Image.new("L", arm.size, 128)
        mask = Image.merge("RGBA", (metal, ao, ImageOps.autocontrast(height), ImageOps.invert(rough)))
        mask.resize((1024, 1024), Image.LANCZOS).save(os.path.join(folder, f"{layer}_Mask.png"))
        print("layer", layer, "<-", asset)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])

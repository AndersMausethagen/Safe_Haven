"""Turns downloaded CC0 ground textures (fetch_polyhaven.py) into terrain layer maps:

    python AgentScripts/PolyHaven/make_terrain_layers.py <work dir> <Assets/_Project/Art/Terrain/World> [layer ...]

For each layer below (or only those named): <Layer>/<Layer>_Color.jpg, <Layer>_Normal.jpg (OpenGL, as Unity expects)
and <Layer>_Mask.png in Unity's terrain mask layout (R metallic, G occlusion, B height for height blending, A
smoothness), plus every source map on its own: <Layer>_Height.png (the displacement), <Layer>_AO.jpg and
<Layer>_Roughness.jpg. WorldAssets (Safe Haven › World › Set Up World Assets) makes the TerrainLayer assets from them.
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
    # Old-growth forest floor (the groves round the Haven): moss carpet, dark humus, litter; Muddy Bank is the mossy mud
    "Forest Moss": "Moss004",
    "Dark Humus": "mud_forest",
    "Forest Litter": "forest_ground_06",
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


def main(work, out_root, only=None):
    for layer, asset in LAYERS.items():
        if only and layer not in only:
            continue
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
        height.save(os.path.join(folder, f"{layer}_Height.png"))
        ao.save(os.path.join(folder, f"{layer}_AO.jpg"), quality=92)
        rough.save(os.path.join(folder, f"{layer}_Roughness.jpg"), quality=92)
        print("layer", layer, "<-", asset)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], set(sys.argv[3:]) or None)

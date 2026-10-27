"""Downloads CC0 assets from Poly Haven (https://polyhaven.com) and ambientCG (https://ambientcg.com), both public
APIs, into a work folder.

    python AgentScripts/PolyHaven/fetch_polyhaven.py <work dir> model:rock_face_01 texture:cliff_side acg:Grass004 tree:fir_tree_01 ...

Models come as glTF (mesh + diffuse, OpenGL normal and ARM maps), textures as diffuse / normal / ARM (+ height) images,
at 2k; ambientCG materials are unpacked to the same names. Both sites are CC0: free for any use, no credit required.
"""
import io
import json
import os
import sys
import urllib.request
import zipfile

API = "https://api.polyhaven.com/files/"
HEADERS = {"User-Agent": "SafeHavenDev/1.0"}
RESOLUTION = "2k"


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=120) as response:
        return response.read()


def save(url, path):
    if os.path.exists(path):
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = get(url)
    with open(path, "wb") as f:
        f.write(data)


def fetch_model(asset, root):
    files = json.loads(get(API + asset))
    gltf = files["gltf"][RESOLUTION]["gltf"]
    folder = os.path.join(root, asset)
    save(gltf["url"], os.path.join(folder, os.path.basename(gltf["url"])))
    for relative, info in gltf.get("include", {}).items():
        save(info["url"], os.path.join(folder, relative))
    make_arm_from_roughness(os.path.join(folder, "textures"), asset)
    return folder


def make_arm_from_roughness(textures, asset):
    """Some sets ship only a roughness map: build the ARM (white occlusion, roughness, no metal) the importer expects."""
    arm = os.path.join(textures, f"{asset}_arm_{RESOLUTION}.jpg")
    rough = os.path.join(textures, f"{asset}_rough_{RESOLUTION}.jpg")
    if os.path.exists(arm) or not os.path.exists(rough):
        return
    from PIL import Image
    r = Image.open(rough).convert("L")
    Image.merge("RGB", (Image.new("L", r.size, 255), r, Image.new("L", r.size, 0))).save(arm, quality=92)


def fetch_texture(asset, root):
    files = json.loads(get(API + asset))
    folder = os.path.join(root, asset)
    for kind, out in (("Diffuse", "diff"), ("nor_gl", "nor_gl"), ("arm", "arm"), ("Displacement", "disp")):
        if kind not in files:
            continue
        maps = files[kind][RESOLUTION]
        info = maps.get("jpg") or maps.get("png")
        save(info["url"], os.path.join(folder, f"{asset}_{out}_{RESOLUTION}.jpg"))
    return folder


def fetch_tree(asset, root):
    """A Poly Haven tree for bake_conifer.py: the 1k glTF (millions of needle cards), the needle alpha the glTF's JPEGs
    leave out, and every bark map at 2k for the game mesh."""
    files = json.loads(get(API + asset))
    gltf = files["gltf"]["1k"]["gltf"]
    folder = os.path.join(root, asset)
    save(gltf["url"], os.path.join(folder, os.path.basename(gltf["url"])))
    for relative, info in gltf.get("include", {}).items():
        save(info["url"], os.path.join(folder, relative))
    for key, maps in files.items():
        if not isinstance(maps, dict) or key in ("gltf", "fbx", "blend", "usd"):
            continue
        if key.endswith("_alpha") or key.endswith(("_diff", "_nor_gl", "_arm")) and not key.startswith("twig"):
            resolution = "1k" if key.endswith("_alpha") else RESOLUTION
            # Alpha as PNG (exact edges), the bark maps as JPG (a tenth of the size)
            info = maps[resolution].get("png") if key.endswith("_alpha") else maps[resolution].get("jpg")
            ext = os.path.splitext(info["url"])[1]
            save(info["url"], os.path.join(folder, "maps", f"{asset}_{key}_{resolution}{ext}"))
    return folder


def fetch_ambientcg(asset, root):
    """An ambientCG material (2K JPG zip), renamed like a Poly Haven texture: diff, nor_gl, arm, disp."""
    folder = os.path.join(root, asset)
    if os.path.exists(os.path.join(folder, f"{asset}_arm_{RESOLUTION}.jpg")):
        return folder
    os.makedirs(folder, exist_ok=True)
    data = get(f"https://ambientcg.com/get?file={asset}_2K-JPG.zip")
    names = {"_Color": "diff", "_NormalGL": "nor_gl", "_Roughness": "rough", "_AmbientOcclusion": "ao", "_Displacement": "disp"}
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for name in archive.namelist():
            for suffix, out in names.items():
                if name.endswith(suffix + ".jpg"):
                    with open(os.path.join(folder, f"{asset}_{out}_{RESOLUTION}.jpg"), "wb") as f:
                        f.write(archive.read(name))
    from PIL import Image
    rough = Image.open(os.path.join(folder, f"{asset}_rough_{RESOLUTION}.jpg")).convert("L")
    ao_path = os.path.join(folder, f"{asset}_ao_{RESOLUTION}.jpg")
    ao = Image.open(ao_path).convert("L").resize(rough.size) if os.path.exists(ao_path) else Image.new("L", rough.size, 255)
    Image.merge("RGB", (ao, rough, Image.new("L", rough.size, 0))).save(
        os.path.join(folder, f"{asset}_arm_{RESOLUTION}.jpg"), quality=92)
    return folder


if __name__ == "__main__":
    work = sys.argv[1]
    for spec in sys.argv[2:]:
        kind, asset = spec.split(":", 1)
        fetch = {"model": fetch_model, "texture": fetch_texture, "acg": fetch_ambientcg, "tree": fetch_tree}[kind]
        folder = fetch(asset, work)
        print("fetched", kind, asset, "->", folder)

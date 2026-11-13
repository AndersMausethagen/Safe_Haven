"""Replay the game's animations on a rig in Blender, to check skinning and tweak weights against real motion.

Unity side: Safe Haven > Tools > Export Clips For Blender Preview (Selected Model), or
ClipPreviewExport.Export(modelPath), plays every clip of MonsterHumanoid.controller on the model through its own Animator
(Unity's Humanoid retargeting, exactly as in game) and writes Tools/Blender/ClipPreview/<model>.json.

Blender side (this file): load(json_path) adds one action per clip ("Unity <state>") to the armature whose bone names
match, and makes the first one active. Switch clips with play("Unity Walk...") or in the Action Editor. Nothing is saved.

How the poses map: Unity's rest positions of the shared bones are fitted to the armature's rest heads (an orthogonal
map with uniform scale, reflection allowed), which yields Unity->Blender axes and handedness without assuming any
import convention. Each bone's rotation away from its Unity rest is conjugated into armature space and applied on top
of the Blender rest, so bone rolls do not matter. The fit's error is reported: a large one means the .blend's rest
pose is not the one Unity imported (re-export, or re-import the FBX into Unity).

From the MCP: exec this file, then load(r"...json"). Headless: blender rig.blend --python preview_unity_clips.py -- <json>
"""
import json
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

PREFIX = "Unity "


def _armature_for(names, armature_name=None):
    if armature_name:
        return bpy.data.objects[armature_name]
    best, hits = None, 0
    for ob in bpy.data.objects:
        if ob.type == 'ARMATURE':
            n = sum(1 for b in ob.data.bones if b.name in names)
            if n > hits:
                best, hits = ob, n
    if best is None:
        raise RuntimeError("No armature shares bone names with the clip file")
    return best


def _fit(rest, idx, bones):
    """Orthogonal Procrustes with scale, reflection allowed: blender_head = s * Q @ unity_pos + t."""
    shared = [b.name for b in bones if b.name in idx]
    U = np.array([rest[idx[n] * 7 + 4: idx[n] * 7 + 7] for n in shared])
    B = np.array([list(bones[n].head_local) for n in shared])
    mu, mb = U.mean(0), B.mean(0)
    Uc, Bc = U - mu, B - mb
    W, S, Vt = np.linalg.svd(Uc.T @ Bc)
    Q = Vt.T @ W.T
    s = S.sum() / (Uc ** 2).sum()
    t = mb - s * Q @ mu
    err = np.linalg.norm(B - (s * (Q @ U.T).T + t), axis=1)
    size = np.linalg.norm(B.max(0) - B.min(0))
    worst = shared[int(err.argmax())]
    return Q, s, t, dict(bones=len(shared), max_err_pct=round(100 * err.max() / size, 2), worst_bone=worst,
                         reflected=bool(np.linalg.det(Q) < 0))


def _q_to_m(v, i):
    x, y, z, w = v[i * 7: i * 7 + 4]
    return np.array(Quaternion((w, x, y, z)).normalized().to_matrix())


def _ordered(bones):
    out = []
    def walk(b):
        out.append(b)
        for c in b.children:
            walk(c)
    for b in bones:
        if b.parent is None:
            walk(b)
    return out


def _bake(arm, clip, data, idx, Q, s, t, fps):
    bones = _ordered(arm.data.bones)
    rest = data["rest"]
    rest_m = {b.name: _q_to_m(rest, idx[b.name]) for b in bones if b.name in idx}
    frames = clip["frames"]
    n = len(frames)
    rot = {b.name: np.zeros((n, 4)) for b in bones}
    loc = {b.name: np.zeros((n, 3)) for b in bones if b.parent is None}

    for f, pose in enumerate(frames):
        P = {}
        for b in bones:
            rest_rot = np.array(b.matrix_local.to_3x3())
            if b.name in idx:
                d = _q_to_m(pose, idx[b.name]) @ rest_m[b.name].T
                R = Q @ d @ Q.T @ rest_rot
            else:
                R = None
            if b.parent is None:
                i = idx.get(b.name)
                head = s * Q @ np.array(pose[i * 7 + 4: i * 7 + 7]) + t if i is not None else np.array(b.head_local)
                full = Matrix.Translation(Vector(head)) @ Matrix((R if R is not None else rest_rot).tolist()).to_4x4()
                local = b.matrix_local.inverted() @ full
                loc[b.name][f] = list(local.to_translation())
            else:
                rel = b.parent.matrix_local.inverted() @ b.matrix_local
                Pp = P[b.parent.name]
                if R is None:
                    local = Matrix.Identity(4)
                else:
                    lr = np.array(rel.to_3x3()).T @ np.array(Pp.to_3x3()).T @ R
                    local = Matrix(lr.tolist()).to_4x4()
                full = Pp @ rel @ local
            P[b.name] = full
            q = local.to_quaternion().normalized()
            v = np.array([q.w, q.x, q.y, q.z])
            if f and np.dot(v, rot[b.name][f - 1]) < 0:
                v = -v
            rot[b.name][f] = v

    action = bpy.data.actions.new(PREFIX + clip["name"])
    action.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = action
    xs = np.arange(1, n + 1, dtype=float)

    def curve(path, i, group, values):
        fc = action.fcurve_ensure_for_datablock(arm, path, index=i, group_name=group)
        fc.keyframe_points.add(n)
        fc.keyframe_points.foreach_set("co", np.column_stack([xs, values]).ravel())
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
        fc.update()

    for b in bones:
        pb = arm.pose.bones[b.name]
        pb.rotation_mode = 'QUATERNION'
        base = f'pose.bones["{b.name}"]'
        for i in range(4):
            curve(base + ".rotation_quaternion", i, b.name, rot[b.name][:, i])
        if b.name in loc:
            for i in range(3):
                curve(base + ".location", i, b.name, loc[b.name][:, i])
    action.use_frame_range = True
    action.frame_start, action.frame_end = 1, n
    action.use_cyclic = bool(clip.get("loop"))
    return action


def play(name):
    """Make the named clip action (with or without the 'Unity ' prefix) active and fit the timeline to it."""
    act = bpy.data.actions.get(name) or bpy.data.actions[PREFIX + name]
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o.animation_data
               and o.animation_data.action and o.animation_data.action.name.startswith(PREFIX))
    arm.animation_data.action = act
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = int(act.frame_range[0]), int(act.frame_range[1])
    sc.frame_set(sc.frame_start)
    return act.name


def load(json_path, armature_name=None, replace=True):
    """Add every clip in json_path as an action on the matching armature. Returns a report dict."""
    with open(json_path, encoding="utf-8") as fh:
        data = json.load(fh)
    idx = {n: i for i, n in enumerate(data["bones"])}
    arm = _armature_for(idx, armature_name)
    Q, s, t, fit = _fit(data["rest"], idx, arm.data.bones)
    if replace:
        for a in [a for a in bpy.data.actions if a.name.startswith(PREFIX)]:
            bpy.data.actions.remove(a)
    made = [_bake(arm, c, data, idx, Q, s, t, data["fps"]).name for c in data["clips"]]
    sc = bpy.context.scene
    sc.render.fps = int(round(data["fps"]))
    play(made[0])
    return dict(armature=arm.name, fit=fit, actions=made)


if __name__ == "__main__" and "--" in sys.argv:
    print(load(sys.argv[sys.argv.index("--") + 1]))

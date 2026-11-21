"""Joins a rendered shot's frames and its sounds into an MP4 with Blender's own FFmpeg (no ffmpeg needed on the PATH).

  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python Tools/Blender/Cinematics/assemble_shot.py -- <frames folder> <out.mp4> <sounds.json>

sounds.json: [{"file": "...wav", "start": seconds, "volume": 0.0-1.0, "fade_in": s, "fade_out": s}, ...]. 24 fps, H.264 + AAC.
"""
import json
import os
import sys

import bpy

FPS = 24


def main():
    frames_dir, out_path, sounds_path = sys.argv[sys.argv.index("--") + 1:][:3]
    frames = sorted(f for f in os.listdir(frames_dir) if f.lower().endswith(".png"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start = 1
    scene.frame_end = len(frames)
    editor = scene.sequence_editor_create()
    strips = editor.strips if hasattr(editor, "strips") else editor.sequences
    image = strips.new_image("Frames", os.path.join(frames_dir, frames[0]), 1, 1)
    for name in frames[1:]:
        image.elements.append(name)
    from_png = image
    scene.render.resolution_x, scene.render.resolution_y = from_png.elements[0].orig_width or 1920, from_png.elements[0].orig_height or 1080
    scene.render.resolution_percentage = 100

    channel = 2
    for sound in json.load(open(sounds_path, encoding="utf-8")):
        start = 1 + round(sound.get("start", 0.0) * FPS)
        strip = strips.new_sound(os.path.basename(sound["file"]), sound["file"], channel, start)
        channel += 1
        # Ends with the shot.
        if strip.frame_final_end > scene.frame_end + 1:
            strip.frame_final_end = scene.frame_end + 1
        volume = sound.get("volume", 1.0)
        fade_in = round(sound.get("fade_in", 0.0) * FPS)
        fade_out = round(sound.get("fade_out", 0.0) * FPS)
        first, last = strip.frame_final_start, strip.frame_final_end
        strip.volume = 0.0 if fade_in else volume
        strip.keyframe_insert("volume", frame=first)
        if fade_in:
            strip.volume = volume
            strip.keyframe_insert("volume", frame=first + fade_in)
        if fade_out:
            strip.volume = volume
            strip.keyframe_insert("volume", frame=last - fade_out)
            strip.volume = 0.0
            strip.keyframe_insert("volume", frame=last)

    settings = scene.render.image_settings
    if hasattr(settings, "media_type"):
        settings.media_type = "VIDEO"
    settings.file_format = "FFMPEG"
    ff = scene.render.ffmpeg
    ff.format = "MPEG4"
    ff.codec = "H264"
    ff.constant_rate_factor = "HIGH"
    ff.ffmpeg_preset = "GOOD"
    ff.audio_codec = "AAC"
    ff.audio_bitrate = 192
    ff.audio_channels = "STEREO"
    scene.render.use_sequencer = True
    scene.render.filepath = out_path
    bpy.ops.render.render(animation=True)


main()

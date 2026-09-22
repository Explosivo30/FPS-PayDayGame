import bpy, json, pathlib
root = pathlib.Path("D:/Unity/FPS-PayDayGame/Artifacts/CombatFeel")
info = json.loads((root / "recording.json").read_text())
frames = sorted((root / "Frames").glob("frame-*.jpg"))[:info["frames"]]
scene = bpy.context.scene
scene.sequence_editor_clear()
editor = scene.sequence_editor_create()
strip = editor.sequences.new_image("Combat", str(frames[0]), channel=1, frame_start=1)
for frame in frames[1:]:
    strip.elements.append(frame.name)
strip.frame_final_duration = len(frames)
seconds = info["audioSeconds"] if info["audioSeconds"] > 1 else info["seconds"]
if info["audioSeconds"] > 1 and info["audioPeak"] > .0001:
    editor.sequences.new_sound("Game audio", str(root / "combat-audio.wav"), channel=2, frame_start=1)
scene.frame_start = 1
scene.frame_end = len(frames)
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.fps = 30
scene.render.fps_base = seconds * 30 / len(frames)
scene.render.image_settings.file_format = "FFMPEG"
scene.render.ffmpeg.format = "MPEG4"
scene.render.ffmpeg.codec = "H264"
scene.render.ffmpeg.constant_rate_factor = "HIGH"
scene.render.ffmpeg.ffmpeg_preset = "GOOD"
scene.render.ffmpeg.audio_codec = "AAC"
scene.render.ffmpeg.audio_bitrate = 192
scene.render.filepath = str(root / "Combat-feel.mp4")
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.render.use_sequencer = True
bpy.ops.render.render(animation=True)
print("COMBAT VIDEO COMPLETE", len(frames), seconds)

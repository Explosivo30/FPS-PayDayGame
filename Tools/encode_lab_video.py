import bpy, os
root="D:/Unity/FPS-PayDayGame/Artifacts"
frames=sorted(f for f in os.listdir(root+"/Frames") if f.endswith(".jpg"))
scene=bpy.context.scene
scene.sequence_editor_create()
strip=scene.sequence_editor.sequences.new_image("HELIX gameplay",root+"/Frames/"+frames[0],channel=1,frame_start=1)
for frame in frames[1:]:strip.elements.append(frame)
scene.frame_start=1;scene.frame_end=len(frames)
scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
scene.render.fps=30;scene.render.image_settings.file_format="FFMPEG"
scene.render.ffmpeg.format="MPEG4";scene.render.ffmpeg.codec="H264";scene.render.ffmpeg.constant_rate_factor="HIGH"
scene.render.ffmpeg.ffmpeg_preset="GOOD";scene.render.filepath=root+"/HELIX-gameplay.mp4"
scene.render.use_sequencer=True
scene.view_settings.view_transform="Standard";scene.view_settings.look="None";scene.view_settings.exposure=0;scene.view_settings.gamma=1
bpy.ops.render.render(animation=True)
print("HELIX VIDEO COMPLETE",len(frames))

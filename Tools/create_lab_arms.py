import bpy, math, os
from mathutils import Vector
ROOT = "D:/Unity/FPS-PayDayGame"
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
def xyz(p): return Vector((p[0], -p[2], p[1]))
def material(name, color, metal=0, rough=.5):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
    return m
sleeve=material('Sleeve | graphite weave',(.065,.095,.125),0,.82)
glove=material('Glove | midnight rubber',(.018,.028,.045),0,.7)
armor=material('Armor | ceramic blue',(.11,.27,.34),.35,.34)
trim=material('Cuff | amber',(.95,.40,.055),.3,.32)
rigdata=bpy.data.armatures.new('OperatorRig')
rig=bpy.data.objects.new('OperatorRig',rigdata); bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bones={}
def bone(name,a,b,parent=None):
    e=rigdata.edit_bones.new(name); e.head=xyz(a); e.tail=xyz(b)
    if parent: e.parent=rigdata.edit_bones[parent]
    bones[name]=(xyz(a),xyz(b))
for side,sign in [('R',1),('L',-1)]:
    shoulder=(sign*.27,-.37,.02); elbow=(sign*.27,-.29,.24); wrist=(sign*.16,-.19,.48)
    bone('UpperArm_'+side,shoulder,elbow)
    bone('Forearm_'+side,elbow,wrist,'UpperArm_'+side)
    bone('Hand_'+side,wrist,(sign*.16,-.19,.575),'Forearm_'+side)
    for i in range(5):
        x=sign*.16+(i-1.5)*.019
        start=(x,-.19,.55 if i<4 else .505)
        if i==4: start=(sign*.16-sign*.049,-.19,.52)
        for j in range(3):
            length=.026 if i<4 else .023
            end=(start[0],start[1]-.012,start[2]+length)
            bone('Finger_%s_%d_%d'%(side,i,j),start,end,'Hand_'+side if j==0 else 'Finger_%s_%d_%d'%(side,i,j-1))
            start=end
bpy.ops.object.mode_set(mode='OBJECT')
rig.select_set(False)
parts=[]
def meshpart(name,a,b,r1,r2,mat,bone_name,vertices=8):
    a,b=xyz(a),xyz(b); d=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r1,radius2=r2,depth=d.length, location=(a+b)/2)
    ob=bpy.context.object; ob.name=name; ob.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    ob.data.materials.append(mat)
    vg=ob.vertex_groups.new(name=bone_name); vg.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    parts.append(ob)
def panel(name,center,scale,mat,bone_name,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(center)); ob=bpy.context.object; ob.name=name; ob.scale=(scale[0],scale[2],scale[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevelmod=ob.modifiers.new('Soft machined edges','BEVEL'); bevelmod.width=bevel; bevelmod.segments=2
    bpy.context.view_layer.objects.active=ob; bpy.ops.object.modifier_apply(modifier=bevelmod.name)
    ob.data.materials.append(mat); vg=ob.vertex_groups.new(name=bone_name); vg.add(list(range(len(ob.data.vertices))),1,'REPLACE'); parts.append(ob)
for side,sign in [('R',1),('L',-1)]:
    meshpart('Sleeve upper '+side,(sign*.27,-.37,.02),(sign*.27,-.29,.24),.075,.064,sleeve,'UpperArm_'+side)
    meshpart('Sleeve forearm '+side,(sign*.27,-.29,.24),(sign*.16,-.19,.47),.062,.042,sleeve,'Forearm_'+side)
    meshpart('Cuff '+side,(sign*.171,-.201,.445),(sign*.16,-.19,.478),.048,.045,armor,'Forearm_'+side)
    meshpart('Cuff seam '+side,(sign*.163,-.193,.464),(sign*.16,-.19,.478),.046,.046,trim,'Forearm_'+side)
    panel('Glove palm '+side,(sign*.16,-.19,.53),(.083,.045,.102),glove,'Hand_'+side)
    panel('Glove armor '+side,(sign*.16,-.164,.525),(.067,.014,.058),armor,'Hand_'+side,.005)
    for i in range(5):
        for j in range(3):
            bn='Finger_%s_%d_%d'%(side,i,j); a,b=bones[bn]
            # xyz is involutive except forward sign; convert from Blender back.
            conv=lambda v:(v.x,v.z,-v.y)
            meshpart(bn,conv(a),conv(b),.0105-j*.0015,.01-j*.0015,glove,bn)
    for i in range(3): panel('Knuckle '+side,(sign*.16+(i-1)*.021,-.164,.562),(.016,.013,.018),armor,'Hand_'+side,.004)
bpy.ops.object.select_all(action='DESELECT')
for p in parts:p.select_set(True)
bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
skin=bpy.context.object; skin.name='Operator_Arms_Gloves'
mod=skin.modifiers.new('Operator skin','ARMATURE'); mod.object=rig; skin.parent=rig
rig.animation_data_create()
for name,length,gesture in [('Idle',48,0),('Fire',8,.12),('Reload',40,.55),('Draw',14,.22),('Holster',12,.32),('KnifeSlash',20,.4),('Land',12,.14)]:
    action=bpy.data.actions.new(name); rig.animation_data.action=action
    for frame,weight in [(1,0),(max(2,length//3),1),(length,0)]:
        for side in ['L','R']:
            for i in range(5):
                for j in range(3):
                    pb=rig.pose.bones['Finger_%s_%d_%d'%(side,i,j)]
                    pb.rotation_mode='XYZ'
                    curl=.58 if i!=4 else .3
                    release=gesture*weight*(1 if side=='L' else .15)
                    pb.rotation_euler=(curl-release,0,0)
                    pb.keyframe_insert(data_path='rotation_euler',frame=frame)
    action.use_fake_user=True
rig.animation_data.action=bpy.data.actions['Idle']
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=48;bpy.context.scene.render.fps=30
os.makedirs(ROOT+'/ArtSource',exist_ok=True); os.makedirs(ROOT+'/Assets/Art/Laboratory',exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/OperatorArms.blend')
bpy.ops.object.select_all(action='DESELECT');skin.select_set(True);rig.select_set(True)
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/Laboratory/OperatorArms.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
print('OPERATOR ARMS EXPORTED',len(skin.data.vertices),'vertices',len(rigdata.bones),'bones')

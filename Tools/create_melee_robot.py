"""Original rigid-part robot, authored in metres. Unity poses the articulated joints."""
import bpy, math, os
from mathutils import Vector
ROOT="D:/Unity/FPS-PayDayGame"
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def xyz(p): return Vector((p[0],p[2],p[1]))
def mat(name,c,metal=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=.58
    return m
ivory=mat('Ivory',(.72,.81,.8),.12)
orange=mat('Orange',(.95,.25,.045),.25)
dark=mat('Graphite',(.022,.033,.048),.3)
steel=mat('Steel',(.13,.20,.24),.65)
cyan=mat('Optic',(.035,.95,.9))
amber=mat('Warning',(1,.56,.025))
def box(name,p,size,m,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p));o=bpy.context.object;o.name=name
    o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Chamfer','BEVEL');mod.width=bevel;mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(m);return o
def cylinder(name,a,b,r,m):
    a,b=xyz(a),xyz(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=r,depth=d.length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);o.data.materials.append(m);return o
# Broad, readable humanoid silhouette: compact chest and oversized industrial gloves.
box('Pelvis_Frame',(0,.98,0),(.51,.25,.34),dark)
box('Pelvis_Belt',(0,1.06,.035),(.6,.12,.41),orange)
cylinder('Torso_Spine',(0,1.04,0),(0,1.38,0),.13,steel)
box('Torso_Chest',(0,1.43,.01),(.79,.49,.44),ivory,.07)
box('Torso_Bib',(0,1.44,.251),(.54,.28,.055),dark)
box('Torso_Core',(0,1.45,.285),(.115,.115,.025),amber,.012)
for x in [-.185,.185]:
    box('Torso_Hazard',(x,1.46,.286),(.065,.17,.018),orange,.005)
for y in [1.30,1.35]:
    box('Torso_Vent',(0,y,.254),(.24,.018,.03),steel,.002)
box('Torso_BackPack',(0,1.4,-.28),(.46,.36,.19),dark)
for x in [-.15,.15]:
    cylinder('Torso_Battery',(x,1.27,-.29),(x,1.56,-.29),.062,orange)
cylinder('Head_Neck',(0,1.64,0),(0,1.77,0),.09,steel)
box('Head_Helmet',(0,1.87,.025),(.43,.33,.36),ivory,.055)
box('Head_Mask',(0,1.86,.21),(.38,.19,.08),dark,.025)
box('Head_Optic',(0,1.88,.26),(.28,.047,.027),cyan,.008)
box('Head_Chin',(0,1.76,.14),(.26,.1,.18),orange,.02)
for s,side in [(-1,'L'),(1,'R')]:
    x=s*.49
    cylinder('UpperArm_'+side+'_Bearing',(s*.35,1.58,0),(s*.60,1.58,0),.145,steel)
    box('UpperArm_'+side+'_Shoulder',(s*.51,1.58,0),(.29,.3,.37),orange,.045)
    box('UpperArm_'+side+'_Stripe',(s*.52,1.61,.19),(.18,.07,.022),ivory,.006)
    cylinder('UpperArm_'+side+'_Piston',(x,1.54,0),(s*.59,1.26,.01),.085,dark)
    cylinder('Forearm_'+side+'_Elbow',(s*.49,1.23,0),(s*.68,1.23,0),.11,steel)
    box('Forearm_'+side+'_Gauntlet',(s*.60,1.07,.06),(.3,.31,.33),orange,.045)
    box('Forearm_'+side+'_Panel',(s*.60,1.10,.239),(.21,.19,.045),dark,.018)
    box('Forearm_'+side+'_Signal',(s*.60,1.13,.268),(.13,.035,.021),amber,.005)
    box('Forearm_'+side+'_Fist',(s*.60,.85,.11),(.34,.2,.37),dark,.045)
    for n in range(3):
        box('Forearm_'+side+'_Knuckle',(s*.60+(n-1)*.094,.87,.305),(.073,.1,.055),ivory,.012)
    box('Forearm_'+side+'_Thumb',(s*.60-s*.185,.86,.14),(.09,.14,.17),steel,.02)
    x=s*.20
    cylinder('Thigh_'+side+'_Hip',(x,.9,0),(x,.61,0),.085,steel)
    box('Thigh_'+side+'_Plate',(x,.76,.06),(.23,.29,.25),ivory,.035)
    cylinder('Shin_'+side+'_Knee',(x-.13,.57,0),(x+.13,.57,0),.10,dark)
    box('Shin_'+side+'_Guard',(x,.53,.12),(.24,.18,.11),orange)
    cylinder('Shin_'+side+'_Piston',(x,.52,0),(x,.19,.005),.067,steel)
    box('Shin_'+side+'_Armor',(x,.34,.06),(.23,.3,.25),ivory,.03)
    box('Shin_'+side+'_Boot',(x,.10,.1),(.3,.2,.47),dark,.035)
    box('Shin_'+side+'_Toe',(x,.13,.3),(.27,.10,.12),orange,.015)
# All mesh origins are retained in world space; the importer attaches meshes to authored Unity pivots.
bpy.ops.object.select_all(action='SELECT')
os.makedirs(ROOT+'/ArtSource',exist_ok=True);os.makedirs(ROOT+'/Assets/Art/MeleeRobot',exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/MeleeRobot.blend')
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/MeleeRobot/MeleeRobot.fbx',use_selection=True,
    object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y')
print('MELEE ROBOT EXPORTED',sum(len(o.data.vertices) for o in bpy.context.scene.objects if o.type=='MESH'),'vertices')

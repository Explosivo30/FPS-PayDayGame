"""Ariete (concept 1) and Aguja (concept 2). Original articulated meshes, Unity metres.
The reference images are preserved in ArtSource/References; geometry is authored here,
not approximated by adding accessories to the existing normal enemies.
"""
import bpy, math, os
from mathutils import Vector, Quaternion
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__))).replace('\\','/')
OUT=ROOT+'/Assets/Art/Veterans'
PREVIEW=ROOT+'/Artifacts/Veterans'
os.makedirs(OUT,exist_ok=True);os.makedirs(PREVIEW,exist_ok=True)

def v(p): return Vector((p[0],p[2],p[1]))
def reset():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for m in list(bpy.data.materials):
        if not m.users:bpy.data.materials.remove(m)
def material(name,c,metal=0,rough=.5,emission=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1)
    bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
    if emission:
        bs.inputs['Emission Color'].default_value=(*c,1);bs.inputs['Emission Strength'].default_value=emission
    return m

def palette():
    return { 'Ivory':material('Ivory',(.83,.77,.65),.12,.44),
        'Graphite':material('Graphite',(.045,.055,.068),.3,.48),
        'Orange':material('Orange',(.99,.275,.025),.15,.42),
        'Steel':material('Steel',(.13,.15,.18),.6,.36),
        'Amber':material('Amber',(1,.32,.015),.05,.3,.6),
        'Black':material('Black',(.009,.013,.018),.1,.65) }

def finish(o,name,m,bevel=0):
    o.name=name
    if m:o.data.materials.append(m)
    if bevel:
        mod=o.modifiers.new('Machined edge chamfer','BEVEL');mod.width=bevel;mod.segments=1
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=40
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def box(name,p,size,m,bevel=.02,rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p));o=bpy.context.object
    o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(o,name,m,bevel)
    if rotation:
        # Unity XYZ Euler axes expressed in Blender's XZY coordinate convention.
        for axis,angle in rotation:
            q=Quaternion(v(axis),math.radians(angle));o.rotation_mode='QUATERNION';o.rotation_quaternion=q@o.rotation_quaternion
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
    return o

def cylinder(name,a,b,r,m,n=12,bevel=.006):
    a,b=v(a),v(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d.length,location=(a+b)*.5)
    o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    return finish(o,name,m,bevel)

def hull(name,points,faces,m,bevel=.012):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata([v(p) for p in points],[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    # Consistent outside normals, including mirrored left/right faceted armor.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    finish(o,name,m,bevel);o.select_set(False);return o

def plate(name,outline,depth,m,bevel=.012):
    # Extrude a front-facing polygon along Unity Z.
    n=len(outline);pts=[(x,y,z) for x,y,z in outline]+[(x,y,z-depth) for x,y,z in outline]
    faces=[tuple(range(n)),tuple(range(2*n-1,n-1,-1))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return hull(name,pts,faces,m,bevel)

def bolt(group,p,r=.025):
    x,y,z=p;cylinder(group+'_Bolt',(x,y,z-.006),(x,y,z+.007),r,M['Graphite'],8,.002)
    box(group+'_Bolt slot',(x,y,z+.009),(r*.8,.006,.003),M['Steel'],0)

def socket_frame(name,outer,inner,z,depth,m):
    # Continuous armor with a real recessed opening, rather than a dark surface decal.
    n=len(outer);pts=[(x,y,z) for x,y in outer]+[(x,y,z) for x,y in inner]
    pts+= [(x,y,z-depth) for x,y in outer]+[(x,y,z-depth) for x,y in inner]
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,j+n,i+n),(i,i+2*n,j+2*n,j),(i+n,j+n,j+3*n,i+3*n),(i+2*n,i+3*n,j+3*n,j+2*n)])
    return hull(name,pts,faces,m,.009)

def ariete():
    global M;reset();M=palette();W=M['Ivory'];D=M['Graphite'];O=M['Orange'];S=M['Steel'];B=M['Black'];A=M['Amber']
    # Full articulated body: broad press chest, recessed small head, asymmetrical hammer fist.
    box('Pelvis_Frame',(0,1.08,-.015),(.68,.29,.45),D,.055)
    box('Pelvis_Waist',(0,1.23,.025),(.72,.12,.48),S,.035)
    plate('Pelvis_Codpiece',[(-.26,1.15,.285),(.26,1.15,.285),(.18,.94,.285),(-.13,.91,.285)],.12,D,.035)
    box('Pelvis_Amber tab',(0,1.13,.35),(.12,.17,.035),O,.009)
    cylinder('Torso_Spine',(0,1.18,-.08),(0,1.75,-.08),.18,S)
    box('Torso_Press frame',(0,1.70,-.04),(1.01,.69,.62),D,.10)
    box('Torso_Rear block',(0,1.70,-.39),(.72,.49,.22),D,.045)
    for x in [-.23,0,.23]:box('Torso_Back rib',(x,1.70,-.525),(.105,.43,.05),S,.012)
    box('Torso_Chest foundation',(0,1.72,.075),(.94,.59,.61),W,.095)
    outer=[(-.40,2.00),(.40,2.00),(.47,1.94),(.44,1.56),(.36,1.48),(-.36,1.48),(-.46,1.55),(-.48,1.94)]
    inner=[(.17,1.91),(.26,1.91),(.28,1.88),(.28,1.62),(.25,1.59),(.17,1.59),(.15,1.62),(.15,1.88)]
    socket_frame('Torso_Chest press armor',outer,inner,.45,.15,W)
    plate('Torso_Chest orange stripe',[(-.31,1.999,.461),(-.17,1.999,.461),(-.17,1.49,.461),(-.31,1.49,.461)],.012,O,.003)
    box('Torso_Vent depth',(.215,1.75,.355),(.12,.33,.035),D,.008)
    box('Torso_Vent darkness',(.215,1.75,.377),(.065,.26,.008),B,.002)
    for p in [(-.39,1.92,.465),(-.35,1.55,.465),(.355,1.91,.465),(.34,1.56,.465)]:bolt('Torso',p,.024)
    box('Torso_Collar back',(0,2.05,-.18),(.95,.15,.44),D,.04)
    box('Torso_Collar front',(0,2.01,.21),(.97,.135,.21),D,.033)
    for s in [-1,1]:box('Torso_Collar side',(s*.48,2.06,.0),(.14,.22,.52),D,.025)
    cylinder('Head_Neck',(0,1.98,0),(0,2.16,0),.115,S)
    box('Head_Helmet',(0,2.20,.0),(.44,.30,.41),W,.055)
    box('Head_Optic socket',(0,2.18,.223),(.35,.15,.048),D,.022)
    box('Head_Optic deep',(0,2.18,.251),(.27,.105,.014),B,.006)
    box('Head_Visor',(0,2.18,.263),(.22,.067,.018),A,.004)
    box('Head_Brow',(0,2.30,.21),(.40,.08,.07),W,.015)
    box('Head_Chin',(0,2.08,.20),(.35,.07,.15),W,.013)
    for s in [-1,1]:
        cylinder('Head_Side hinge',(s*.22,2.21,-.025),(s*.265,2.21,-.025),.095,D)
        cylinder('Head_Side cap',(s*.267,2.21,-.025),(s*.278,2.21,-.025),.056,S)
    for s,side in [(-1,'L'),(1,'R')]:
        arm='UpperArm_'+side;f='Forearm_'+side
        sx=s*.65;ex=s*(.79 if side=='R' else .75)
        cylinder(arm+'_Shoulder axle',(s*.43,1.94,-.015),(s*.78,1.94,-.015),.19,D)
        box(arm+'_Shoulder steel',(sx,1.94,.0),(.45,.46,.52),D,.075)
        plate(arm+'_Shoulder shell',[(s*.49,2.18,.31),(s*.84,2.19,.31),(s*.93,2.10,.31),(s*.86,1.78,.31),(s*.54,1.81,.31)],.18,W,.028)
        box(arm+'_Shoulder top',(s*.69,2.18,-.015),(.38,.10,.51),W,.035)
        plate(arm+'_Shoulder orange panel',[(s*.80,2.18,.343),(s*.87,2.12,.343),(s*.81,1.88,.343),(s*.74,1.90,.343)],.018,O,.004)
        for dx,dy in [(-.13,-.13),(.13,.13)]:bolt(arm,(s*.69+dx,1.99+dy,.35),.02)
        cylinder(arm+'_Upper actuator',(sx,1.80,-.015),(ex,1.45,.015),.12,D)
        cylinder(arm+'_Piston',(s*.76,1.80,-.12),(s*.86,1.48,-.09),.045,S)
        cylinder(f+'_Elbow axle',(ex-.13,1.43,.01),(ex+.13,1.43,.01),.145,S)
        cylinder(f+'_Elbow cap',(ex+s*.13,1.43,.01),(ex+s*.165,1.43,.01),.105,D)
        big=side=='R';x=s*(.92 if big else .79);cy=1.20 if big else 1.21
        dims=(.59,.65,.64) if big else (.33,.40,.40)
        box(f+'_Gauntlet frame',(x,cy,.09),(dims[0]+.07,dims[1]+.02,dims[2]+.06),D,.065 if big else .045)
        hw=dims[0]/2;hh=dims[1]/2;zf=.16+dims[2]/2
        plate(f+'_Gauntlet shell',[(x-hw+.05,cy+hh,zf),(x+hw-.05,cy+hh,zf),(x+hw,cy+hh-.06,zf),(x+hw,cy-hh+.06,zf),(x+hw-.05,cy-hh,zf),(x-hw+.05,cy-hh,zf),(x-hw,cy-hh+.06,zf),(x-hw,cy+hh-.06,zf)],.15 if big else .11,W,.018)
        box(f+'_Gauntlet orange stripe',(x+s*(.18 if big else .09),cy+.02,zf+.012),(.105 if big else .065,dims[1]-.035,.018),O,.003)
        for dx,dy in [(-.21,-.23),(.21,.23)] if big else [(-.10,-.12),(.10,.12)]:bolt(f,(x+dx,cy+dy,.16+dims[2]/2+.02),.028 if big else .017)
        box(f+'_Inner wrist',(x,.86 if big else .94,.13),(.43 if big else .26,.17,.41 if big else .28),S,.025)
        box(f+'_Closed palm',(x,.82 if big else .91,.17),(.51 if big else .31,.23,.49 if big else .34),D,.05 if big else .035)
        spacing=.112 if big else .070
        for k in range(4):
            fx=x+(k-1.5)*spacing
            box(f+'_Finger knuckle',(fx,.78 if big else .88,.39 if big else .315),(.098 if big else .061,.13 if big else .09,.15 if big else .105),D,.022 if big else .013)
            box(f+'_Curled finger',(fx,.72 if big else .835,.32 if big else .275),(.092 if big else .057,.09 if big else .07,.11 if big else .085),D,.018 if big else .011)
        box(f+'_Thumb',(x-s*(.26 if big else .16),.84 if big else .94,.30),(.13 if big else .085,.20 if big else .14,.18 if big else .13),D,.025)
        # Strong thigh and calf armor, ankle block and broad dark sole match the reference.
        tx=s*.27;kx=s*.38;ax=s*.48
        cylinder('Thigh_'+side+'_Hip joint',(tx,1.05,0),(tx,.74,0),.135,S)
        box('Thigh_'+side+'_Thigh backing',(s*.29,.87,0),(.40,.42,.43),D,.035)
        box('Thigh_'+side+'_Thigh armor',(s*.30,.89,.09),(.37,.40,.39),W,.055)
        box('Thigh_'+side+'_Orange strip',(s*.30,.91,.295),(.115,.23,.02),O,.005)
        cylinder('Shin_'+side+'_Knee',(kx-.18,.63,.0),(kx+.18,.63,.0),.145,D)
        cylinder('Shin_'+side+'_Knee hub',(kx+s*.18,.63,.0),(kx+s*.20,.63,.0),.085,S)
        box('Shin_'+side+'_Knee cap',(kx,.65,.20),(.32,.17,.18),W,.035)
        cylinder('Shin_'+side+'_Shin piston',(kx,.61,-.06),(ax,.24,-.02),.09,S)
        box('Shin_'+side+'_Shin armor',(s*.43,.41,.08),(.37,.40,.40),W,.055)
        box('Shin_'+side+'_Shin dark recess',(s*.44,.45,.291),(.16,.20,.025),D,.012)
        box('Shin_'+side+'_Shin stripe',(s*.32,.42,.292),(.075,.25,.021),O,.005)
        box('Shin_'+side+'_Ankle',(ax,.20,.02),(.29,.16,.34),S,.025)
        box('Shin_'+side+'_Boot',(ax,.12,.15),(.52,.22,.67),D,.05)
        box('Shin_'+side+'_Sole',(ax,.055,.15),(.55,.105,.71),D,.02)
        plate('Shin_'+side+'_Toe armor',[(ax-.22,.22,.49),(ax+.22,.22,.49),(ax+.19,.10,.53),(ax-.19,.10,.53)],.15,W,.018)
    for o in list(bpy.context.scene.objects):
        if o.type=='MESH' and (o.name.startswith('Forearm_R_') or o.name.startswith('Forearm_L_')):
            right=o.name.startswith('Forearm_R_');pivot=v((.79 if right else -.75,1.43,.01))
            q=Quaternion(v((0,0,1)),math.radians(12 if right else -8))
            for vertex in o.data.vertices:
                world=o.matrix_world@vertex.co
                vertex.co=o.matrix_world.inverted()@(pivot+q@(world-pivot))
    for o in list(bpy.context.scene.objects):
        if o.type!='MESH':continue
        if o.name.startswith(('Thigh_','Shin_')):
            s=-1 if '_L_' in o.name else 1
            for vert in o.data.vertices:
                world=o.matrix_world@vert.co;world.x+=s*(1.05-world.z)*.13
                vert.co=o.matrix_world.inverted()@world
        o.location.x*=1.10;o.location.z*=.90;o.scale.x*=1.10;o.scale.z*=.90
        bpy.context.view_layer.objects.active=o;o.select_set(True)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.select_set(False)
    return 'Ariete'

def aguja():
    global M;reset();M=palette();W=M['Ivory'];D=M['Graphite'];O=M['Orange'];S=M['Steel'];B=M['Black'];A=M['Amber']
    # A genuinely replacement diamond chassis with a thick central band and full underside.
    plan=[(0,.90),(-1.10,-.05),(0,-.87),(1.10,-.05)]
    pts=[(x,1.31,z) for x,z in plan]+[(x*.83,.98,z*.84) for x,z in plan]+[(0,1.58,0),(0,.87,.05)]
    faces=[(8,i,(i+1)%4) for i in range(4)]+[(i,i+4,(i+1)%4+4,(i+1)%4) for i in range(4)]+[(9,(i+1)%4+4,i+4) for i in range(4)]
    hull('Body_Chassis',pts,faces,D,.025)
    for side in [-1,1]:
        s=side
        # Broad folded, swept shell: two front-to-back facets and a deliberate central groove.
        top=[(s*.25,1.50,.75),(s*1.085,1.40,-.04),(s*.28,1.80,-.68),(s*.055,1.71,-.14)]
        lower=[(x,y-.17,z) for x,y,z in top]
        hull('Body_Upper armor '+str(s),top+lower,[(0,1,2),(0,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],W,.014)
        # Lower white panels wrap the dark visor belt without becoming wings.
        p=[(s*.26,1.30,.84),(s*1.035,1.28,-.02),(s*.79,1.01,-.015),(s*.22,.90,.72),(s*.18,1.20,.86)]
        plate('Body_Lower armor '+str(s),p,.085,W,.018)
        # Shallow orange identification stripe lies on the broad upper plane, not on an antenna.
        stripe=[(s*.76,1.51,.035),(s*.94,1.445,-.02),(s*.49,1.72,-.48),(s*.41,1.755,-.50)]
        hull('Body_Orange shell marking '+str(s),stripe,[(0,1,2,3)],O,0)
        box('Body_Side optical socket',(s*.79,1.29,.20),(.15,.12,.08),B,.014)
        box('Body_Side amber port',(s*.79,1.29,.247),(.09,.035,.016),A,.003)
    plate('Body_Visor socket',[(-.28,1.48,.88),(.28,1.48,.88),(.38,1.39,.88),(.28,1.30,.88),(-.28,1.30,.88),(-.38,1.39,.88)],.13,D,.012)
    box('Body_Optic recess',(0,1.39,.894),(.55,.10,.015),B,.008)
    box('Body_Amber visor',(0,1.39,.907),(.46,.055,.018),A,.003)
    # Rear center exhaust and faceted underside provide a complete finished reverse view.
    box('Body_Rear vent',(0,1.24,-.735),(.40,.19,.13),S,.025)
    for i in range(4):box('Body_Rear vent slot',((i-1.5)*.075,1.24,-.806),(.032,.11,.015),B,.002)
    cylinder('Body_Gun gimbal',(0,.84,.30),(0,1.11,.30),.25,D,12,.018)
    cylinder('Body_Gimbal trim',(0,.87,.30),(0,.93,.30),.27,S,12,.009)
    cylinder('Gun_Pitch joint',(-.25,1.095,.57),(.25,1.095,.57),.19,D,12,.012)
    for s in [-1,1]:cylinder('Gun_Pitch hub',(s*.25,1.095,.57),(s*.27,1.095,.57),.10,S,12,.005)
    # Every gun component shares the authored Gun pivot; a single octagonal open muzzle.
    box('Gun_Barrel core',(0,1.105,1.02),(.27,.27,.94),D,.035)
    # Wedge-section ivory armor around the real barrel, with two long facets.
    p=[(-.20,1.28,.60),(.20,1.28,.60),(.15,1.245,1.49),(-.15,1.245,1.49),(-.20,1.03,.60),(.20,1.03,.60),(.15,1.01,1.49),(-.15,1.01,1.49)]
    hull('Gun_Armor shroud',p,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(3,7,4,0)],W,.018)
    box('Gun_Top graphite rail',(0,1.285,.93),(.09,.04,.62),D,.008)
    for side in [-1,1]:box('Gun_Orange stripe',(side*.174,1.13,1.29),(.022,.17,.065),O,.003)
    # Hollow octagon, actual bore depth; no black decal substituted for the opening.
    points=[];n=8
    for z,r in [(1.66,.18),(1.49,.18),(1.66,.105),(1.45,.105)]:
        for i in range(n):
            a=2*math.pi*(i+.5)/n;points.append((math.cos(a)*r,1.115+math.sin(a)*r,z))
    faces=[]
    for i in range(n):
        j=(i+1)%n;faces.extend([(i,j,j+8,i+8),(i,i+16,j+16,j),(i+16,i+24,j+24,j+16)])
    hull('Gun_Open octagonal muzzle',points,faces,D,.004)
    box('Gun_Bore darkness',(0,1.115,1.515),(.20,.20,.015),B,0)
    return 'Aguja'

def export_model(name):
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/'+name+'.blend')
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',add_leaf_bones=False)
    print('EXPORTED',name,'parts',len(meshes),'vertices',sum(len(o.data.vertices) for o in meshes))

def preview(name):
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.use_gtao=True;scene.eevee.gtao_distance=3;scene.eevee.gtao_factor=1.25;scene.eevee.use_soft_shadows=True
    floor=material('Studio floor',(.40,.39,.37),0,.85)
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));bpy.context.object.data.materials.append(floor)
    world=bpy.data.worlds.new('Neutral studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.53,.50,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.world=world
    for pos,power,size in [((3,4,6),450,5),((-4,3,4),180,4),((1,-4,5),300,3)]:
        bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(Vector((0,0,1.2))-l.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(4.0,7.5,3.25) if name=='Ariete' else (-4.5,7.5,3.6));cam=bpy.context.object
    target=Vector((0,.1,1.20 if name=='Ariete' else 1.25));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=3.05 if name=='Ariete' else 3.25;scene.camera=cam
    scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=-.5;scene.view_settings.gamma=1
    scene.render.resolution_x=1280;scene.render.resolution_y=1120;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.filepath=PREVIEW+'/'+name+'-studio.png';bpy.ops.render.render(write_still=True)
    # Authored model stays in its .blend; studio is a reproducible review artifact only.

for build in [ariete,aguja]:
    name=build();export_model(name);preview(name)

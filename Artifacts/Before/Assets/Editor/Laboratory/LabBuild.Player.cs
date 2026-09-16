using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static partial class LabBuild
{
    static void ImportArms()
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/OperatorArms.fbx");
        importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
    }
    static GameObject CreatePlayer()
    {
        var root=PrefabUtility.LoadPrefabContents(PlayerPath);
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var ps=root.GetComponentInChildren<PlayerStateMachine>();
        var head=ps.headCam;head.localPosition=new Vector3(0,.72f,0);
        var cc=ps.GetComponent<CharacterController>();cc.radius=.4f;cc.height=2;cc.stepOffset=.32f;cc.slopeLimit=45;
        var duplicate=ps.GetComponent<CapsuleCollider>();if(duplicate!=null) Object.DestroyImmediate(duplicate);
        ps.jumpForce=6.5f;ps.maxGroundSpeed=8;ps.maxAirSpeed=5;ps.groundAcceleration=80;ps.maxCrouchSpeed=4;
        Set(ps,"_castRadius",.36f);Set(ps,"_height",0f);Set(ps,"_castLength",.74f);Set(ps,"_gravityForce",22f);Set(ps,"_groundMask",1<<3);
        var skin=ps.transform.Find("Skin");if(skin!=null) skin.gameObject.SetActive(false);
        var shield=ps.GetComponent<Shield>();Set(shield,"_maxShield",50f);
        var oldHands=head.Find("Hands");if(oldHands!=null) Object.DestroyImmediate(oldHands.gameObject);
        var cameras=head.GetComponentsInChildren<Camera>(true);
        var cam=cameras.First(c=>c.name=="Game Camera");var wc=cameras.First(c=>c.name=="Camera Gun");
        foreach(var camera in cameras)
        {
            camera.fieldOfView=70;camera.nearClipPlane=.025f;camera.farClipPlane=120;
            foreach(var t in camera.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("AimPos")).ToArray()) Object.DestroyImmediate(t.gameObject);
        }
        cam.tag="MainCamera";wc.tag="Untagged";wc.gameObject.layer=8;wc.cullingMask=1<<8;cam.cullingMask=~(1<<8);
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.018f,.032f,.055f);
        var cd=cam.GetUniversalAdditionalCameraData();var wd=wc.GetUniversalAdditionalCameraData();
        cd.renderType=CameraRenderType.Base;wd.renderType=CameraRenderType.Overlay;cd.cameraStack.Clear();cd.cameraStack.Add(wc);
        cd.renderPostProcessing=true;wd.renderPostProcessing=false;wd.clearDepth=true;
        var hands=new GameObject("Hands").transform;hands.SetParent(head,false);
        var weapons=new MonoBehaviour[4];
        weapons[0]=Weapon<Pistol>(hands,head,"PULSO","Assets/Prefabs/Weapons/PISTOLA_PLASMA/PISTOLA_PLASMA_FIX.prefab",Vector3.zero,.48f,"Pistol",30,12,5,1.3f);
        weapons[1]=Weapon<MachineGun>(hands,head,"CARABINA","Assets/Prefabs/Weapons/Metralleta/Survival Carbine/Carbine.prefab",new Vector3(0,270,0),.85f,"MachineGun",12,30,10,1.8f);
        weapons[2]=Weapon<ShotGun>(hands,head,"BENELLI M4","Assets/Prefabs/Shotgun Set/Prefabs/BenelliM4.prefab",new Vector3(0,180,0),1f,"ShotGun",10,6,1.25f,3.35f);
        var knifeRoot=new GameObject("CUCHILLO");knifeRoot.transform.SetParent(hands,false);
        var knife=knifeRoot.AddComponent<Knife>();knife.damage=50;knife.attackCooldown=.65f;knife.range=1.8f;
        var knifeModel=new GameObject("Knife model").transform;knifeModel.SetParent(knifeRoot.transform,false);
        Box("Grip",knifeModel,new Vector3(0,-.07f,0),new Vector3(.035f,.12f,.035f),dark,false);
        var blade=Box("Ceramic blade",knifeModel,new Vector3(0,.065f,.018f),new Vector3(.03f,.16f,.009f),white,false);
        blade.transform.localRotation=Quaternion.Euler(0,0,-8);
        Box("Guard",knifeModel,Vector3.zero,new Vector3(.065f,.014f,.05f),steel,false);
        var knifep=knifeRoot.AddComponent<WeaponPresentation>();knifep.hipPosition=new Vector3(.22f,-.20f,.42f);knifep.hipEuler=new Vector3(-20,-12,-20);
        GripsAndArms(knifep,head,false);
        knife.swayData=Asset<SwayData>("Knife Sway",()=>ScriptableObject.CreateInstance<SwayData>());
        weapons[3]=knife;
        var controller=root.GetComponentInChildren<GunController>();Set(controller,"weaponObjects",weapons);
        foreach(var weapon in weapons) { Layers(weapon.gameObject,8);weapon.gameObject.SetActive(weapon==weapons[0]); }
        var result=PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);
        PrefabUtility.UnloadPrefabContents(root);return result;
    }
    static T Weapon<T>(Transform parent,Transform head,string name,string modelPath,Vector3 rotation,float length,string id,float damage,int capacity,float rate,float reload) where T:BaseGun
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        var gun=go.AddComponent<T>();gun.GunTypeID=id;gun.damage=damage;gun.ammo=gun.currentAmmo=capacity;gun.fireRate=rate;gun.maxRangeGun=80;gun.layerMask=~(1<<7|1<<8|1<<2);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        model.transform.SetParent(go.transform,false);model.name="Model";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(rotation);model.transform.localScale=Vector3.one;
        foreach(var light in model.GetComponentsInChildren<Light>()) if(light!=null) Object.DestroyImmediate(light.gameObject);
        foreach(var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        Normalize(model,go.transform,length);
        if(typeof(T)==typeof(MachineGun)) { var suppressor=model.transform.Find("Suppressor");if(suppressor!=null) suppressor.gameObject.SetActive(false); }
        var action=go.GetComponent<WeaponAction>();action.reloadDuration=reload;action.shellReload=typeof(T)==typeof(ShotGun);
        var presentation=go.AddComponent<WeaponPresentation>();
        presentation.hipPosition=new Vector3(typeof(T)==typeof(Pistol)?.19f:.21f,-.19f,.43f);
        presentation.aimPosition=new Vector3(0,-.04f,.46f);
        presentation.magazine=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Magazine");
        presentation.bolt=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Bolt"||t.name=="Reload");
        if(typeof(T)==typeof(Pistol)) presentation.magazine=Box("Plasma cell",go.transform,new Vector3(0,-.11f,.02f),new Vector3(.045f,.07f,.08f),cyan,false).transform;
        var recoil=Asset<RecoilData>(id+" Recoil",()=>ScriptableObject.CreateInstance<RecoilData>());
        recoil.recoilKickUp=typeof(T)==typeof(Pistol)?1.45f:typeof(T)==typeof(ShotGun)?3.1f:.55f;
        recoil.recoilKickSide=typeof(T)==typeof(ShotGun)?.32f:.17f;recoil.maxVerticalRecoil=6;recoil.maxHorizontalRecoil=1;
        recoil.recoilSnappiness=38;recoil.recoilReturnSpeed=10;recoil.recoilReturnDelay=.14f;
        recoil.weaponKickUp=typeof(T)==typeof(Pistol)?5.5f:typeof(T)==typeof(ShotGun)?8:2.4f;
        recoil.kickbackDistance=typeof(T)==typeof(ShotGun)?.085f:.048f;recoil.playerImpulseForce=0;
        recoil.spreadHip=typeof(T)==typeof(ShotGun)?4:1.1f;recoil.spreadADS=typeof(T)==typeof(ShotGun)?3:.18f;
        Set(gun,"recoilData",recoil);
        gun.swayData=Asset<SwayData>(id+" Sway",()=>ScriptableObject.CreateInstance<SwayData>());
        var muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(go.transform,false);muzzle.localPosition=new Vector3(0,0,length*.5f+.10f);Set(gun,"muzzleTransform",muzzle);
        var flash=EffectAsset("Muzzle",true);var impact=EffectAsset("Impact",false);
        Set(gun,"muzzleFlashPrefab",flash);
        foreach(string prop in new[]{"stoneImpactPrefab","metalImpactPrefab","woodImpactPrefab","fleshImpactPrefab"}) Set(gun,prop,impact);
        var lr=go.AddComponent<LineRenderer>();ConfigureTracer(lr,.008f);
        GripsAndArms(presentation,head,true);
        return gun;
    }
    static void Normalize(GameObject model,Transform pivot,float length)
    {
        Bounds bounds=LocalBounds(model,pivot);
        float factor=length/Mathf.Max(.001f,bounds.size.z);
        model.transform.localScale*=factor;
        bounds=LocalBounds(model,pivot);
        model.transform.localPosition+=new Vector3(-bounds.center.x,.04f-bounds.max.y,.1f-bounds.center.z);
    }
    static Bounds LocalBounds(GameObject go,Transform pivot)
    {
        var renderers=go.GetComponentsInChildren<Renderer>().Where(r=>!(r is ParticleSystemRenderer)&&!(r is LineRenderer)).ToArray();
        var b=new Bounds(pivot.InverseTransformPoint(renderers[0].bounds.center),Vector3.zero);
        foreach(var r in renderers)
            for(int i=0;i<8;i++) b.Encapsulate(pivot.InverseTransformPoint(r.bounds.center+Vector3.Scale(r.bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
        return b;
    }
    static void GripsAndArms(WeaponPresentation weapon,Transform head,bool twoHands)
    {
        weapon.rightGrip=new GameObject("Right grip").transform;weapon.rightGrip.SetParent(weapon.transform,false);
        weapon.rightGrip.localPosition=new Vector3(.015f,-.10f,-.04f);weapon.rightGrip.localRotation=Quaternion.Euler(75,0,-10);
        weapon.leftGrip=new GameObject("Left grip").transform;weapon.leftGrip.SetParent(weapon.transform,false);
        weapon.leftGrip.localPosition=twoHands?new Vector3(-.025f,-.08f,.20f):new Vector3(-.35f,-.15f,.05f);
        weapon.leftGrip.localRotation=Quaternion.Euler(65,0,40);
        var arms=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/OperatorArms.fbx"),weapon.transform);arms.name="Operator arms";
        arms.transform.localPosition=Vector3.zero;arms.transform.localRotation=Quaternion.identity;arms.transform.localScale=Vector3.one;
        foreach(var r in arms.GetComponentsInChildren<Renderer>()) {
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++) { string n=mats[i]!=null?mats[i].name:"";mats[i]=n.Contains("amber")?amber:n.Contains("ceramic")?steel:dark; }
            r.sharedMaterials=mats;
        }
        var anim=arms.GetComponent<Animation>();if(anim==null) anim=arms.AddComponent<Animation>();
        foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(Folder+"/OperatorArms.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")))
        {
            string name=clip.name.Split('|').Last();anim.AddClip(clip,name);
            if(name=="Idle") { anim.clip=clip;anim.playAutomatically=true;anim.wrapMode=WrapMode.Loop; }
        }
        var ik=arms.AddComponent<OperatorHands>();ik.weapon=weapon;ik.head=head;
    }
}

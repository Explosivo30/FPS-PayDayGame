using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static partial class LabBuild
{
    static void Arena()
    {
        Box("Foundation",world,new Vector3(0,-.3f,0),new Vector3(48,.6f,40),dark);
        for(int x=-22;x<=22;x+=4)for(int z=-18;z<=18;z+=4) Box("Ceramic floor module",world,new Vector3(x,.012f,z),new Vector3(3.96f,.025f,3.96f),steel,false);
        Box("North wall",world,new Vector3(0,4,20),new Vector3(48,8,.6f),white);
        Box("South wall",world,new Vector3(0,4,-20),new Vector3(48,8,.6f),white);
        Box("West wall",world,new Vector3(-24,4,0),new Vector3(.6f,8,40),white);
        Box("East wall",world,new Vector3(24,4,0),new Vector3(.6f,8,40),white);
        Box("Ceiling",world,new Vector3(0,8.1f,0),new Vector3(48,.4f,40),dark);
        for(int side=-1;side<=1;side+=2)
        {
            for(int z=-16;z<=16;z+=8)
            {
                Box("Wall rib",world,new Vector3(side*23.65f,4,z),new Vector3(.45f,8,.35f),dark);
                Box("Vertical light",world,new Vector3(side*23.35f,3.5f,z),new Vector3(.12f,4,.13f),cyan,false);
            }
            Box("Perimeter datum",world,new Vector3(side*23.55f,.35f,0),new Vector3(.12f,.08f,39),cyan,false);
            Box("Bridge deck",world,new Vector3(side*13,2.8f,9),new Vector3(8,.4f,5),steel);
            for(int edge=-1;edge<=1;edge+=2)
            {
                Box("Bridge guard",world,new Vector3(side*13+edge*3,3.55f,6.5f),new Vector3(2,.85f,.14f),glass);
                Box("Bridge rail",world,new Vector3(side*13+edge*3,4,6.5f),new Vector3(2,.08f,.16f),cyan,false);
            }
            var ramp=Box("Access ramp "+side,world,new Vector3(side*13,1.4f,2),new Vector3(3.5f,.25f,9.5f),steel);
            ramp.transform.localRotation=Quaternion.Euler(-Mathf.Atan2(3,9)*Mathf.Rad2Deg,0,0);
            for(int i=0;i<9;i++) Box("Ramp tread",world,new Vector3(side*13,.05f+i/3f,-2.25f+i),new Vector3(3.3f,.028f,.08f),amber,false);
            Box("Side corridor divider",world,new Vector3(side*17.8f,1.4f,-6),new Vector3(.4f,2.8f,6),white);
            for(int z=-12;z<=13;z+=25)
            {
                Box("Entry sight screen",world,new Vector3(side*20,1.5f,z),new Vector3(.4f,3,4.5f),dark);
                Box("Entry header",world,new Vector3(side*22,3.15f,z-2.3f),new Vector3(3.3f,.3f,.3f),amber);
            }
            for(int z=-10;z<=10;z+=10)
            {
                Box("Equipment bench",world,new Vector3(side*8,.65f,z),new Vector3(3,1.3f,1.4f),white);
                Box("Bench inset",world,new Vector3(side*8,1.32f,z),new Vector3(2.8f,.08f,1.2f),dark,false);
                Box("Bench status strip",world,new Vector3(side*8,1.15f,z-.71f),new Vector3(2.5f,.045f,.025f),cyan,false);
            }
        }
        Box("Observation bridge",world,new Vector3(0,2.8f,15),new Vector3(34,.4f,7),steel);
        Box("Observation glass",world,new Vector3(0,3.6f,11.5f),new Vector3(17,1.1f,.12f),glass);
        Box("Observation handrail",world,new Vector3(0,4.2f,11.5f),new Vector3(17,.08f,.14f),cyan,false);
        Cylinder("Core plinth",new Vector3(0,.4f,2),new Vector3(5,.4f,5),dark);
        Cylinder("Ion chamber",new Vector3(0,2.8f,2),new Vector3(3.4f,2.2f,3.4f),glass);
        Cylinder("Suspended reactor",new Vector3(0,2.8f,2),new Vector3(1.2f,1.7f,1.2f),cyan,false);
        Cylinder("Chamber crown",new Vector3(0,5.1f,2),new Vector3(4,.2f,4),steel);
        for(int i=0;i<4;i++)
        {
            float a=i*Mathf.PI*.5f;Vector3 p=new Vector3(Mathf.Cos(a)*2,2.7f,2+Mathf.Sin(a)*2);
            Box("Containment pillar",world,p,new Vector3(.25f,5,.25f),white);
        }
        for(int x=-18;x<=18;x+=12)
        {
            Box("Overhead beam",world,new Vector3(x,7.7f,0),new Vector3(.3f,.4f,39),steel,false);
            for(int z=-14;z<=14;z+=14) Box("Ceiling light panel",world,new Vector3(x,7.45f,z),new Vector3(4,.1f,1.1f),cyan,false);
        }
        for(int side=-1;side<=1;side+=2)
        {
            Box("Guidance stripe",world,new Vector3(side*4,.045f,-8),new Vector3(.07f,.015f,14),amber,false);
            Box("Containment boundary",world,new Vector3(side*3.5f,.045f,2),new Vector3(.06f,.015f,8),cyan,false);
        }
        // Repeated wall panels and service islands break up the long arena silhouettes.
        for(int side=-1;side<=1;side+=2)
        {
            for(int z=-12;z<=12;z+=8)
            {
                Box("Recessed wall panel",world,new Vector3(side*23.55f,2.8f,z),new Vector3(.16f,3.8f,5.8f),steel,false);
                Box("Panel top trim",world,new Vector3(side*23.42f,4.75f,z),new Vector3(.06f,.06f,5.8f),cyan,false);
                for(int j=0;j<3;j++) Box("Panel vent",world,new Vector3(side*23.38f,1.35f+j*.2f,z),new Vector3(.04f,.05f,3),dark,false);
            }
            for(int z=-10;z<=10;z+=10)
            {
                var screen=Box("Console display",world,new Vector3(side*8,1.5f,z+.3f),new Vector3(1.6f,.45f,.08f),cyan,false);
                screen.transform.localRotation=Quaternion.Euler(25,0,0);
                Box("Console frame",world,new Vector3(side*8,1.47f,z+.36f),new Vector3(1.8f,.6f,.08f),dark,false);
                for(int j=0;j<3;j++) Box("Console key",world,new Vector3(side*8-.3f+j*.3f,1.38f,z-.35f),new Vector3(.13f,.035f,.08f),amber,false);
            }
        }
        Sign("H E L I X   /   0 7",new Vector3(0,6.2f,19.62f),Quaternion.identity,14,new Color(.08f,.45f,.55f));
        Sign("CONTAINMENT RESEARCH",new Vector3(0,5.15f,19.6f),Quaternion.identity,5,new Color(.2f,.3f,.4f));
        Sign("A / BIO-SYSTEMS",new Vector3(-23.5f,5,-3),Quaternion.Euler(0,-90,0),5,Color.white);
        Sign("B / ENERGY SYSTEMS",new Vector3(23.5f,5,-3),Quaternion.Euler(0,90,0),5,Color.white);
        Sign("REARM  /  E",new Vector3(0,2.7f,-18.6f),Quaternion.Euler(0,180,0),5,new Color(1,.65f,.2f));
        var terminal=Box("Upgrade terminal",world,new Vector3(0,.8f,-18),new Vector3(2,1.6f,.9f),dark);terminal.layer=0;terminal.AddComponent<ShopOpener>();
        Box("Terminal display",world,new Vector3(0,1.4f,-17.51f),new Vector3(1.6f,.5f,.05f),cyan,false);
        Sign("MEJORAS",new Vector3(0,1.45f,-17.45f),Quaternion.Euler(0,180,0),2.8f,Color.white);
        for(int side=-1;side<=1;side+=2)for(int z=-4;z<=4;z+=4)
        {
            Cylinder("Sample capsule",new Vector3(side*22,1.3f,z),new Vector3(1,1.3f,1),glass);
            Cylinder("Sample base",new Vector3(side*22,.15f,z),new Vector3(1.2f,.15f,1.2f),steel);
            Cylinder("Sample emitter",new Vector3(side*22,2.7f,z),new Vector3(1.1f,.08f,1.1f),cyan,false);
        }
    }
    static void ConfigureLighting()
    {
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.62f,.7f);RenderSettings.ambientIntensity=1;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.035f,.08f,.11f);RenderSettings.fogDensity=.012f;
        var key=new GameObject("Soft laboratory key").AddComponent<Light>();key.type=LightType.Directional;key.transform.rotation=Quaternion.Euler(55,-25,0);key.color=new Color(.77f,.87f,1);key.intensity=1.2f;key.shadows=LightShadows.Soft;
        for(int x=-16;x<=16;x+=16)for(int z=-12;z<=12;z+=12)
        {
            var light=new GameObject("Ceiling wash").AddComponent<Light>();light.type=LightType.Point;light.transform.position=new Vector3(x,6,z);light.color=new Color(.7f,.86f,1);light.intensity=14;light.range=17;light.shadows=LightShadows.None;
        }
        var volume=new GameObject("Lab grade").AddComponent<Volume>();volume.isGlobal=true;
        var profile=Asset<VolumeProfile>("Lab grading",()=>ScriptableObject.CreateInstance<VolumeProfile>());
        if(!profile.TryGet<Bloom>(out var bloom))bloom=profile.Add<Bloom>();bloom.intensity.Override(.22f);bloom.threshold.Override(1.1f);
        if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
        if(!profile.TryGet<ColorAdjustments>(out var color))color=profile.Add<ColorAdjustments>();
        color.postExposure.Override(.7f);
        foreach(var component in profile.components)
            if(string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(component))) UnityEditor.AssetDatabase.AddObjectToAsset(component,profile);
        volume.sharedProfile=profile;
    }
    static LabHUD CreateHUD(PlayerStateMachine player)
    {
        var go=new GameObject("Laboratory HUD");var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=5;
        var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var hud=go.AddComponent<LabHUD>();hud.player=player;hud.controller=player.GetComponentInChildren<GunController>();
        hud.waveText=Label(go.transform,"Wave",new Vector2(0,1),new Vector2(50,-44),new Vector2(650,50),25);
        Label(go.transform,"HELIX  /  CONTENCIÓN 07",new Vector2(0,1),new Vector2(50,-88),new Vector2(600,32),15).color=new Color(.35f,.75f,.85f);
        hud.scoreText=Label(go.transform,"Credits",new Vector2(1,1),new Vector2(-250,-44),new Vector2(200,45),23);
        hud.healthText=Label(go.transform,"Health",Vector2.zero,new Vector2(50,80),new Vector2(650,40),20);
        hud.weaponText=Label(go.transform,"Weapon",new Vector2(1,0),new Vector2(-360,155),new Vector2(320,40),22);
        hud.ammoText=Label(go.transform,"Ammo",new Vector2(1,0),new Vector2(-360,105),new Vector2(320,65),46);
        hud.stateText=Label(go.transform,"State",new Vector2(.5f,0),new Vector2(-150,170),new Vector2(300,40),20);
        Label(go.transform,"R  RECARGAR     Q  CAMBIAR     E  TIENDA",new Vector2(1,0),new Vector2(-480,34),new Vector2(450,30),13).color=new Color(.5f,.65f,.7f);
        Label(go.transform,"+",new Vector2(.5f,.5f),new Vector2(-8,10),new Vector2(16,22),18).color=new Color(.75f,.95f,1,.7f);
        hud.hitMarker=Icon(go.transform,"Hit",new Vector2(10,10));hud.hitMarker.rectTransform.localRotation=Quaternion.Euler(0,0,45);
        hud.killMarker=Icon(go.transform,"Kill",new Vector2(18,18));hud.killMarker.rectTransform.localRotation=Quaternion.Euler(0,0,45);
        hud.reloadBar=Icon(go.transform,"Reload",new Vector2(180,3));hud.reloadBar.rectTransform.anchoredPosition=new Vector2(0,-360);hud.reloadBar.color=new Color(.1f,.8f,1);hud.reloadBar.type=Image.Type.Filled;hud.reloadBar.fillMethod=Image.FillMethod.Horizontal;
        return hud;
    }
    static TextMeshProUGUI Label(Transform parent,string text,Vector2 anchor,Vector2 position,Vector2 size,float fontSize)
    {
        var go=new GameObject(text);go.transform.SetParent(parent,false);var t=go.AddComponent<TextMeshProUGUI>();t.text=text;t.fontSize=fontSize;t.color=new Color(.83f,.93f,1);t.raycastTarget=false;
        t.rectTransform.anchorMin=t.rectTransform.anchorMax=anchor;t.rectTransform.pivot=new Vector2(0,1);t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=size;return t;
    }
    static Image Icon(Transform parent,string name,Vector2 size)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var im=go.AddComponent<Image>();im.raycastTarget=false;im.rectTransform.anchorMin=im.rectTransform.anchorMax=new Vector2(.5f,.5f);im.rectTransform.sizeDelta=size;im.color=Color.clear;return im;
    }
}

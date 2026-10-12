using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Styles existing bound HUD widgets; gameplay remains in their original presenters.</summary>
public static class InterfaceHUD
{
    static void Text(TMP_Text t,Vector2 position,Vector2 size,float font,Color color,bool display=true)
    {
        if(t==null)return;InterfaceStyle.Place(t.rectTransform,position,size);
        t.alignment=TextAlignmentOptions.MidlineLeft;t.enableAutoSizing=true;t.fontSizeMin=font*.8f;t.fontSizeMax=font;
        if(display)InterfaceStyle.Display(t,font,color);else{t.fontSize=font;t.color=color;}
    }
    public static void Lab(LabHUD hud)
    {
        var parent=hud.transform;var scaler=hud.GetComponent<CanvasScaler>();
        if(scaler!=null)scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var mission=InterfaceStyle.Panel(parent,"Mission glass",new Vector2(44,870),new Vector2(584,166),InterfaceStyle.Cyan,true);mission.transform.SetAsFirstSibling();
        var wallet=InterfaceStyle.Panel(parent,"Wallet glass",new Vector2(1596,943),new Vector2(280,85),InterfaceStyle.Gold);wallet.transform.SetAsFirstSibling();
        var weapon=InterfaceStyle.Panel(parent,"Weapon glass",new Vector2(1454,40),new Vector2(422,166),InterfaceStyle.Cyan);weapon.transform.SetAsFirstSibling();
        InterfaceStyle.Line(parent,"Weapon rail",new Vector2(1480,150),new Vector2(366,1),new Color(.05f,.65f,.85f,.4f));
        Text(hud.waveText,new Vector2(68,976),new Vector2(542,36),29,InterfaceStyle.Cyan);
        Text(hud.scoreText,new Vector2(1618,963),new Vector2(235,48),32,InterfaceStyle.Gold);
        Text(hud.weaponText,new Vector2(1480,160),new Vector2(366,33),26,InterfaceStyle.Cyan);
        Text(hud.ammoText,new Vector2(1480,87),new Vector2(366,65),53,InterfaceStyle.TextColor);
        Text(hud.stateText,new Vector2(1480,59),new Vector2(366,23),18,InterfaceStyle.Gold);
        if(hud.reloadBar!=null){InterfaceStyle.Place(hud.reloadBar.rectTransform,new Vector2(1480,48),new Vector2(366,4));hud.reloadBar.color=InterfaceStyle.Cyan;}
        foreach(var text in hud.GetComponentsInChildren<TMP_Text>())
            if(text.text.Contains("R  RECARGAR")){text.text="R  RECARGAR     Q  CAMBIAR";Text(text,new Vector2(1480,213),new Vector2(366,25),15,InterfaceStyle.Muted,false);}
        InterfaceStyle.CenterDesign(hud.GetComponent<Canvas>());
    }
    public static void Status(Canvas canvas)
    {
        var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var plate=InterfaceStyle.Panel(canvas.transform,"Operator glass",new Vector2(44,40),new Vector2(390,166),InterfaceStyle.Cyan);
        plate.transform.SetAsFirstSibling();
        InterfaceStyle.Label(canvas.transform,"Operator ID",new Vector2(70,169),new Vector2(338,25),19,InterfaceStyle.Muted,true).text="OPERADOR  /  ESTADO VITAL";
        foreach(var label in canvas.GetComponentsInChildren<TMP_Text>())
        {
            if(label.name=="Health")Text(label,new Vector2(70,120),new Vector2(330,37),24,InterfaceStyle.TextColor);
            if(label.name=="Shield")Text(label,new Vector2(70,73),new Vector2(330,34),22,InterfaceStyle.Cyan);
        }
        foreach(var image in canvas.GetComponentsInChildren<Image>())
        {
            bool health=image.name=="Health back"||image.name=="Health fill"||image.name=="Damage trail";
            bool shield=image.name=="Shield back"||image.name=="Shield fill";
            if(health||shield)InterfaceStyle.Place(image.rectTransform,new Vector2(70,health?113:63),new Vector2(336,6));
        }
        for(int i=1;i<12;i++)
        {
            InterfaceStyle.Line(canvas.transform,"Health segment",new Vector2(70+i*28,112),new Vector2(3,8),new Color(.006f,.02f,.04f));
            InterfaceStyle.Line(canvas.transform,"Shield segment",new Vector2(70+i*28,62),new Vector2(3,8),new Color(.006f,.02f,.04f));
        }
        InterfaceStyle.CenterDesign(canvas);
    }
    public static void Tower(TowerOverlay hud)
    {
        var scaler=hud.GetComponent<CanvasScaler>();if(scaler!=null)scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        Text(hud.floorText,new Vector2(68,936),new Vector2(536,32),21,InterfaceStyle.TextColor);
        Text(hud.objectiveText,new Vector2(68,891),new Vector2(536,42),16,InterfaceStyle.Muted,false);
        Text(hud.messageText,new Vector2(697,934),new Vector2(795,80),19,InterfaceStyle.TextColor,false);hud.messageText.alignment=TextAlignmentOptions.Center;
        Text(hud.interactionText,new Vector2(610,268),new Vector2(700,58),22,InterfaceStyle.Cyan);hud.interactionText.alignment=TextAlignmentOptions.Center;
        InterfaceStyle.CenterDesign(hud.GetComponent<Canvas>(),hud.fade.transform);
    }
}

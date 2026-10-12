using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>Shared visual language derived from the original Level1 terminal.</summary>
public static class InterfaceStyle
{
    public static Color Cyan=>InterfaceTheme.Current!=null?InterfaceTheme.Current.cyan:new Color(.02f,.91f,1);
    public static Color Magenta=>InterfaceTheme.Current!=null?InterfaceTheme.Current.magenta:new Color(.92f,.15f,1);
    public static Color Gold=>InterfaceTheme.Current!=null?InterfaceTheme.Current.gold:new Color(1,.71f,.24f);
    public static Color TextColor=>InterfaceTheme.Current!=null?InterfaceTheme.Current.text:new Color(.86f,.95f,1);
    public static Color Muted=>InterfaceTheme.Current!=null?InterfaceTheme.Current.muted:new Color(.44f,.65f,.76f);
    public static Color Surface=>InterfaceTheme.Current!=null?InterfaceTheme.Current.surface:new Color(.017f,.048f,.079f,.98f);
    public static void Place(RectTransform r,Vector2 position,Vector2 size)
    {r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=position;r.sizeDelta=size;}
    public static void CenterDesign(Canvas canvas,Transform excluded=null)
    {
        var holder=new GameObject("Interface safe area",typeof(RectTransform)).GetComponent<RectTransform>();
        var children=new System.Collections.Generic.List<Transform>();
        foreach(Transform child in canvas.transform)if(child!=excluded)children.Add(child);
        holder.SetParent(canvas.transform,false);holder.anchorMin=holder.anchorMax=holder.pivot=new Vector2(.5f,.5f);holder.sizeDelta=new Vector2(1920,1080);
        foreach(var child in children)
        {
            var rect=child as RectTransform;var position=rect!=null?rect.anchoredPosition:Vector2.zero;
            child.SetParent(holder,false);if(rect!=null)rect.anchoredPosition=position;
        }
        if(excluded!=null)excluded.SetAsLastSibling();
    }
    public static HoloPanel Panel(Transform parent,string name,Vector2 position,Vector2 size,Color accent,bool doubleRail=false)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var p=go.AddComponent<HoloPanel>();Place(p.rectTransform,position,size);p.fill=Surface;p.accent=accent;p.doubleRail=doubleRail;p.raycastTarget=false;return p;
    }
    public static Image Line(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var i=go.AddComponent<Image>();Place(i.rectTransform,position,size);i.color=color;i.raycastTarget=false;return i;
    }
    public static TMP_Text Label(Transform parent,string name,Vector2 position,Vector2 size,float font,Color color,bool display=false)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var t=go.AddComponent<TextMeshProUGUI>();Place(t.rectTransform,position,size);t.fontSize=font;t.color=color;t.raycastTarget=false;
        if(display&&InterfaceTheme.Current!=null&&InterfaceTheme.Current.displayFont!=null)t.font=InterfaceTheme.Current.displayFont;
        t.enableAutoSizing=true;t.fontSizeMin=font*.83f;t.fontSizeMax=font;return t;
    }
    public static void Display(TMP_Text text,float size,Color color)
    {
        text.fontSize=size;text.color=color;
        if(InterfaceTheme.Current!=null&&InterfaceTheme.Current.displayFont!=null)text.font=InterfaceTheme.Current.displayFont;
    }
    public static void Modal(Transform parent,string section)
    {
        var back=Line(parent,"Opaque backdrop",Vector2.zero,new Vector2(1920,1080),new Color(.002f,.006f,.015f,.985f));back.raycastTarget=true;
        var shell=Panel(parent,"Terminal chassis",new Vector2(68,42),new Vector2(1784,996),Cyan,true);
        shell.fill=InterfaceTheme.Current!=null?InterfaceTheme.Current.panel:new Color(.009f,.026f,.052f,.985f);shell.cut=28;
        for(int x=108;x<1820;x+=80)Line(parent,"Glass grid",new Vector2(x,80),new Vector2(1,920),new Color(.04f,.5f,.68f,.04f));
        for(int y=80;y<1000;y+=80)Line(parent,"Glass grid",new Vector2(100,y),new Vector2(1720,1),new Color(.04f,.5f,.68f,.04f));
        Line(parent,"Header rail",new Vector2(112,856),new Vector2(1696,1),new Color(.05f,.5f,.7f,.45f));
        Line(parent,"Footer rail",new Vector2(112,171),new Vector2(1696,1),new Color(.05f,.5f,.7f,.4f));
        Label(parent,"Brand",new Vector2(119,982),new Vector2(700,26),19,Muted,true).text="HELIX  /  "+section;
        var status=Label(parent,"Link",new Vector2(1390,982),new Vector2(400,26),16,Cyan);
        status.text="●  CONEXIÓN ESTABLE";status.alignment=TextAlignmentOptions.Right;
    }
    public static Button Button(Transform parent,string name,Vector2 position,Vector2 size,UnityAction callback,out TMP_Text label,Color? accent=null)
    {
        var plate=Panel(parent,name,position,size,accent??Cyan);plate.raycastTarget=true;
        var button=plate.gameObject.AddComponent<Button>();button.targetGraphic=plate;
        var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1.35f,1.45f,1.5f);c.selectedColor=c.highlightedColor;
        c.pressedColor=new Color(.8f,1,1);c.disabledColor=new Color(.45f,.55f,.65f,1);c.fadeDuration=.12f;button.colors=c;
        button.onClick.AddListener(callback);
        label=Label(plate.transform,"Label",new Vector2(20,9),size-new Vector2(40,18),23,accent??TextColor,true);
        label.alignment=TextAlignmentOptions.MidlineLeft;
        return button;
    }
}

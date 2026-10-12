using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName="HELIX/Interface theme")]
public sealed class InterfaceTheme : ScriptableObject
{
    public TMP_FontAsset displayFont;
    public Color cyan=new Color(.02f,.91f,1f);
    public Color magenta=new Color(.92f,.15f,1f);
    public Color gold=new Color(1f,.71f,.24f);
    public Color text=new Color(.86f,.95f,1f);
    public Color muted=new Color(.44f,.65f,.76f);
    public Color panel=new Color(.009f,.026f,.052f,.985f);
    public Color surface=new Color(.017f,.048f,.079f,.98f);
    static InterfaceTheme loaded;
    public static InterfaceTheme Current
    {
        get {if(loaded==null)loaded=Resources.Load<InterfaceTheme>("HelixInterface");return loaded;}
    }
}

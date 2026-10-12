using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent chamfered glass and illuminated rails, no baked text.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HoloPanel : MaskableGraphic
{
    public Color fill=new Color(.01f,.03f,.06f,.97f);
    public Color accent=new Color(.02f,.8f,1f);
    public float cut=16,stroke=1.2f;
    public bool doubleRail;
    public void SetAccent(Color value){if(accent==value)return;accent=value;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=GetPixelAdjustedRect();
        var outline=Points(r,0);var tint=fill*color;
        int center=vh.currentVertCount;vh.AddVert(r.center,tint,Vector2.zero);
        foreach(var p in outline)vh.AddVert(p,tint,Vector2.zero);
        for(int i=0;i<8;i++)vh.AddTriangle(center,center+1+i,center+1+(i+1)%8);
        Rail(vh,r,0,stroke+4,.06f);Rail(vh,r,0,stroke+2,.13f);Rail(vh,r,0,stroke,.8f);
        if(doubleRail)Rail(vh,r,7,.8f,.42f);
    }
    Vector2[] Points(Rect r,float inset)
    {
        float l=r.xMin+inset,b=r.yMin+inset,t=r.yMax-inset,q=r.xMax-inset;
        float c=Mathf.Max(1,Mathf.Min(cut-inset,Mathf.Min(q-l,t-b)*.22f));
        return new[]{new Vector2(l+c,b),new Vector2(q-c,b),new Vector2(q,b+c),new Vector2(q,t-c),
            new Vector2(q-c,t),new Vector2(l+c,t),new Vector2(l,t-c),new Vector2(l,b+c)};
    }
    void Rail(VertexHelper vh,Rect r,float inset,float width,float opacity)
    {
        var a=Points(r,inset);var b=Points(r,inset+width);Color tint=accent*color;tint.a*=opacity;
        for(int i=0;i<8;i++)
        {
            int j=(i+1)%8,n=vh.currentVertCount;
            vh.AddVert(a[i],tint,Vector2.zero);vh.AddVert(a[j],tint,Vector2.zero);
            vh.AddVert(b[j],tint,Vector2.zero);vh.AddVert(b[i],tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}

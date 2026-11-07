Shader "Torbellino/Characters/Woodcut Relief"
{
 Properties {
  [MainTexture] _BaseMap("Original color texture",2D)="white"{}
  [MainColor] _BaseColor("Original material color",Color)=(1,1,1,1)
  _InkColor("Blue-black ink",Color)=(.016,.022,.035,1)
  _EmissionColor("Emission",Color)=(0,0,0,1)
  _InkStrength("Ink strength",Range(0,1))=1
  _PaperColor("Unprinted paper",Color)=(.9,.77,.57,1)
  _AccentColor("Vermilion printing ink",Color)=(.48,.075,.035,1)
  _PaletteStrength("Reference palette / original colors",Range(0,1))=.85
  _Carving("Light cuts into dark ink",Range(0,1))=.65
  _Roughness("Print wear",Range(0,1))=.35
  _Density("Strokes per mesh unit (import scale matters)",Float)=90
  _StrokeWidth("Gouge width",Range(.04,.5))=.21
  _Wear("Broken ink",Range(0,.4))=.12
  [Enum(ArtistUV,0,CurvedObject,1,PlanarObject,2)] _Mapping("Hatch mapping",Float)=0
  _HatchTiling("Artist UV scale / rotation in radians",Vector)=(32,32,.3,0)
  _HatchAxis("Object flow axis",Vector)=(0,1,0,0)
  _HatchOrigin("Object flow center",Vector)=(0,0,0,0)
  _WrapCount("Strokes around curved part (integer)",Float)=40
  [Toggle] _UseRestCoordinates("Use baked rest position in UV3",Float)=0
 }
 SubShader {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Pass {
   Name "EngravedSurface"
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _BaseColor, _InkColor, _EmissionColor, _PaperColor, _AccentColor;
   float4 _HatchTiling, _HatchAxis, _HatchOrigin;
   float _InkStrength,_Density,_StrokeWidth,_Wear,_Mapping,_WrapCount,_UseRestCoordinates,_PaletteStrength,_Carving,_Roughness;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 rest:TEXCOORD2;};
   struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float3 rest:TEXCOORD3;float fog:TEXCOORD4;float3 localNormal:TEXCOORD5;};
   V Vert(A v){V o;VertexPositionInputs pos=GetVertexPositionInputs(v.p.xyz);o.p=pos.positionCS;o.world=pos.positionWS;o.normal=TransformObjectToWorldNormal(v.n);o.localNormal=v.n;o.uv=v.uv;o.rest=_UseRestCoordinates>.5?v.rest.xyz:v.p.xyz;o.fog=ComputeFogFactor(pos.positionCS.z);return o;}
   float Hash(float2 p){float3 q=frac(float3(p.xyx)*.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
   float ValueNoise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
   // Irregular, tapered gouges rather than uniform infinite parallel lines.
   float Gouge(float2 p,float seed,float width){
    float warp=(ValueNoise(p*float2(.17,.12)+seed)-.5)*.55;
    float phase=p.x+warp;
    float row=floor(phase),segment=floor((p.y+Hash(float2(row,seed))*9)/9);
    float random=Hash(float2(row+seed,segment));
    float along=frac((p.y+Hash(float2(row,seed))*9)/9);
    float taper=smoothstep(0,.15,along)*(1-smoothstep(.72,1,along));
    float widthHere=width*(.55+random*.8)*taper;
    float d=abs(frac(phase)-.5)*2;
    float fw=max(fwidth(phase),.008);
    float mask=(1-smoothstep(widthHere-fw,widthHere+fw,d))*step(_Wear*.5,random);
    return lerp(mask,width*.65,saturate((fw-.32)*2));
   }
   float2 Flow(float3 rest,float2 uv,out float capBlend,out float2 cap){
    float s=sin(_HatchTiling.z),c=cos(_HatchTiling.z);
    float2 artist=uv*_HatchTiling.xy;
    artist=float2(c*artist.x-s*artist.y,s*artist.x+c*artist.y);
    float3 axis=normalize(_HatchAxis.xyz);
    float3 t=normalize(cross(axis,abs(axis.y)<.9?float3(0,1,0):float3(1,0,0)));
    float3 b=cross(axis,t);float3 p=rest-_HatchOrigin.xyz;
    float u=dot(p,t),v=dot(p,b),height=dot(p,axis);
    // Quantized turns make the angular seam periodic, independent of camera orientation.
    float around=atan2(v,u)/6.2831853*max(round(_WrapCount),1);
    cap=float2(u,v)*_Density;
    capBlend=0;
    return _Mapping>.5?float2(around,height*_Density):artist;
   }
   half4 Frag(V i):SV_Target{
    float3 n=normalize(i.normal);
    Light key=GetMainLight(TransformWorldToShadowCoord(i.world));
    float lighting=saturate(dot(n,key.direction))*key.shadowAttenuation;
    float3 lightColor=key.color;
    #if defined(_ADDITIONAL_LIGHTS)
    uint count=GetAdditionalLightsCount();
    for(uint li=0;li<count;li++){Light l=GetAdditionalLight(li,i.world);float d=saturate(dot(n,l.direction))*l.distanceAttenuation*l.shadowAttenuation;lighting+=d;lightColor+=l.color*d;}
    #endif
    lighting=saturate(lighting);
    float capBlend;float2 cap;float2 f=Flow(i.rest,i.uv,capBlend,cap);
    if(_Mapping>1.5){
        float3 an=abs(normalize(i.localNormal));
        float2 plane=an.x>an.y&&an.x>an.z?i.rest.zy:(an.y>an.z?i.rest.xz:i.rest.xy);
        f=plane*_Density;
        f=float2(f.x*.866-f.y*.5,f.x*.5+f.y*.866);
    }
    // Cap strokes use local planar coordinates rather than a singular angular field.
    float3 axisWS=normalize(TransformObjectToWorldDir(_HatchAxis.xyz));
    float capWeight=smoothstep(.78,.97,abs(dot(n,axisWS)))*step(.5,_Mapping)*(1-step(1.5,_Mapping));
    f=lerp(f,cap,capWeight);
    float2 flowing=float2(f.x+f.y*.28,f.y);
    float2 crossing=float2(f.x-f.y*.8,f.y*.75+f.x*.3);
    float broad=ValueNoise(f*.065+float2(17,31));
    // Distinct paper, light hatch, dense hatch and solid ink regions.
    float shadow=1-smoothstep(.24,.42,lighting+(broad-.5)*.16);
    float halfTone=1-smoothstep(.33,.59,lighting+(broad-.5)*.16);
    halfTone=max(halfTone,.32*smoothstep(.55,.78,broad)*(1-smoothstep(.65,.82,lighting)));
    float primary=Gouge(flowing,1.7,_StrokeWidth);
    float secondary=Gouge(crossing,6.1,_StrokeWidth*.76);
    float carved=Gouge(float2(f.x+f.y*.12+3.2,f.y*1.15),12.4,_StrokeWidth*.5);
    float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,TRANSFORM_TEX(i.uv,_BaseMap)).rgb*_BaseColor.rgb;
    float y=dot(base,float3(.2126,.7152,.0722));
    float darkMaterial=1-smoothstep(.045,.16,y);
    float warm=smoothstep(.06,.32,base.r-base.g)*smoothstep(.05,.2,base.r-base.b);
    float3 paper=_PaperColor.rgb;
    float3 limited=lerp(paper,_AccentColor.rgb,warm);
    limited=lerp(limited,_InkColor.rgb,darkMaterial);
    float3 pigment=lerp(base,limited,_PaletteStrength);
    // Large ink silhouettes with carved paper slivers are central to relief printing.
    float ink=max(primary*halfTone,secondary*halfTone*halfTone*.7);
    ink=max(ink,shadow*.96);
    ink=max(ink,darkMaterial*.96);
    float3 result=lerp(pigment,_InkColor.rgb,saturate(ink*_InkStrength));
    float cutZone=max(shadow*.8,darkMaterial*(.35+.65*lighting));
    float cutMask=carved*cutZone*_Carving*lerp(.3,1,smoothstep(.2,.65,broad));
    float3 cutPigment=lerp(paper,lerp(paper,_AccentColor.rgb,.6),warm);
    result=lerp(result,cutPigment,cutMask);
    // Small clusters of ink loss; noise remains in material coordinates.
    float grain=Hash(floor(f*float2(11,19)));
    float fleck=step(.985,grain)*_Roughness*lerp(.5,1,ink);
    result=lerp(result,paper,fleck);
    float paperNoise=(ValueNoise(f*2.3)-.5)*.045*_Roughness;
    result=max(0,result+paperNoise*(1-ink));
    result+=_EmissionColor.rgb*.15;
    return half4(MixFog(result,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
 Fallback Off
}

Shader "Torbellino/Characters/Engraved Ink"
{
 Properties {
  [MainTexture] _BaseMap("Original color texture",2D)="white"{}
  [MainColor] _BaseColor("Original material color",Color)=(1,1,1,1)
  _InkColor("Blue-black ink",Color)=(.016,.022,.035,1)
  _EmissionColor("Emission",Color)=(0,0,0,1)
  _InkStrength("Ink strength",Range(0,1))=.93
  _Density("Strokes per mesh unit (import scale matters)",Float)=90
  _StrokeWidth("Stroke weight",Range(.04,.5))=.16
  _Wear("Broken ink",Range(0,.4))=.12
  _ShadowLift("Color remaining between strokes",Range(.3,1))=.84
  [Enum(ArtistUV,0,CurvedObject,1,PlanarObject,2)] _Mapping("Hatch mapping",Float)=0
  _HatchTiling("Artist UV scale / rotation in radians",Vector)=(32,32,.3,0)
  _HatchAxis("Object flow axis",Vector)=(0,1,0,0)
  _HatchOrigin("Object flow center",Vector)=(0,0,0,0)
  _WrapCount("Strokes around curved part (integer)",Float)=40
  [Toggle] _UseRestCoordinates("Use baked rest position in UV3",Float)=0
  _BandSoftness("Light band transition",Range(.001,.1))=.035
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
   float4 _BaseMap_ST, _BaseColor, _InkColor, _EmissionColor;
   float4 _HatchTiling, _HatchAxis, _HatchOrigin;
   float _InkStrength,_Density,_StrokeWidth,_Wear,_ShadowLift,_Mapping,_WrapCount,_BandSoftness,_UseRestCoordinates;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 rest:TEXCOORD2;};
   struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float3 rest:TEXCOORD3;float fog:TEXCOORD4;float3 localNormal:TEXCOORD5;};
   V Vert(A v){V o;VertexPositionInputs pos=GetVertexPositionInputs(v.p.xyz);o.p=pos.positionCS;o.world=pos.positionWS;o.normal=TransformObjectToWorldNormal(v.n);o.localNormal=v.n;o.uv=v.uv;o.rest=_UseRestCoordinates>.5?v.rest.xyz:v.p.xyz;o.fog=ComputeFogFactor(pos.positionCS.z);return o;}
   float Hash(float2 p){float3 q=frac(float3(p.xyx)*.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
   float Stroke(float phase,float width,float along,float seed){
    float cell=floor(phase);
    float jitter=.045*sin(along*1.9+seed)+.025*sin(along*5.7+seed*3);
    float wave=sin((phase+jitter)*6.2831853);
    // Derivative AA evaluated before wrapping. Unresolvable strokes become their average tone.
    float fw=max(length(float2(ddx(phase),ddy(phase))),.001);
    float threshold=cos(width*3.14159265);
    float lineInk=smoothstep(threshold-max(fw*2,.01),threshold+max(fw*2,.01),wave);
    float broken=lerp(1, smoothstep(.08,.3,Hash(float2(cell,floor(along*1.4)))),_Wear);
    return lerp(lineInk*broken,width,saturate((fw-.35)*2));
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
    float s=_BandSoftness;
    float dark=1-smoothstep(.12-s,.12+s,lighting);
    float mid=1-smoothstep(.4-s,.4+s,lighting);
    float light=1-smoothstep(.7-s,.7+s,lighting);
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
    float primary=Stroke(f.x+f.y*.18,_StrokeWidth,f.y,1.7);
    float secondary=Stroke(f.x-f.y*.55,_StrokeWidth*.82,f.y,4.1);
    float tertiary=Stroke(f.x+f.y*.85,_StrokeWidth*.7,f.y,8.4);
    float capH=Stroke(cap.x+cap.y*.3,_StrokeWidth,cap.y,2.3);
    primary=lerp(primary,capH,capWeight);
    secondary=lerp(secondary,Stroke(cap.x-cap.y*.65,_StrokeWidth*.8,cap.y,5.1),capWeight);
    float ink=1-(1-primary*light)*(1-secondary*mid)*(1-tertiary*dark);
    ink=max(ink,dark*.45);
    float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,TRANSFORM_TEX(i.uv,_BaseMap)).rgb*_BaseColor.rgb;
    // Keep original hue. Shading comes primarily from the amount of ink, not a grey ramp.
    float3 pigment=base*lerp(_ShadowLift,1,lighting)*lerp(float3(1,1,1),max(lightColor,.2),.16);
    float3 inkPigment=lerp(_InkColor.rgb,base*.14,.2);
    float3 result=lerp(pigment,inkPigment,saturate(ink*_InkStrength));
    result+=_EmissionColor.rgb;
    return half4(MixFog(result,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
 Fallback Off
}

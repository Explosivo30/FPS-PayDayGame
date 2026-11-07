#ifndef TE_HATCHING
#define TE_HATCHING
float2 TERotate(float2 p,float degrees){float s,c;sincos(radians(degrees),s,c);return float2(c*p.x-s*p.y,s*p.x+c*p.y);}
float2 TEStrokeUV(float2 uv){
 // Inverse of the periodic warp authored in generate_hatch_texture.py.
 // Reduce large waves without discarding the atlas's fine ink imperfections.
 // PNG rows run opposite to the shader's texture V: the sampled atlas warp
 // has negative sine phase, so cancellation adds the positive phase to U.
 if(_HatchWaviness<.999){float warp=.10*sin(6.2831853*uv.y*2)+.05*sin(6.2831853*uv.y*5);uv.x+=(1-_HatchWaviness)*warp/8;}
 return uv;
}
// Distance-field strokes use base LOD; derivative AA and visibility fade suppress undersampling.
float3 TEPatternAt(TEVaryings v,float angle,float scale){
 if(_Projection<.5)return SAMPLE_TEXTURE2D_LOD(_HatchTex,sampler_HatchTex,TEStrokeUV(TERotate(v.uv*_HatchScale*scale,angle)*_HatchTex_ST.xy+_HatchTex_ST.zw),0).rgb;
 float3 p=v.positionOS*_HatchScale*scale;float3 w=pow(abs(normalize(v.normalOS)),8);w/=max(w.x+w.y+w.z,.0001);
 float3 x=SAMPLE_TEXTURE2D_LOD(_HatchTex,sampler_HatchTex,TEStrokeUV(TERotate(p.yz,angle)*_HatchTex_ST.xy+_HatchTex_ST.zw),0).rgb;
 float3 y=SAMPLE_TEXTURE2D_LOD(_HatchTex,sampler_HatchTex,TEStrokeUV(TERotate(p.zx,angle)*_HatchTex_ST.xy+_HatchTex_ST.zw),0).rgb;
 float3 z=SAMPLE_TEXTURE2D_LOD(_HatchTex,sampler_HatchTex,TEStrokeUV(TERotate(p.xy,angle)*_HatchTex_ST.xy+_HatchTex_ST.zw),0).rgb;
 return x*w.x+y*w.y+z*w.z;
}
float3 TEPattern(TEVaryings v,float angle){return TEPatternAt(v,angle,1);}
float TEInkCoverage(float3 pattern){float aa=max(fwidth(pattern.r)*.6,.015);float coverage=1-smoothstep(_HatchThickness-aa,_HatchThickness+aa,pattern.r);return coverage*lerp(1,pattern.g,_InkVariation);}
float3 TEHatch(TEVaryings v,float3 shaded,float lightLevel,float albedoLuma){
 if(_HatchEnable<.5&&_GrainIntensity<=0)return shaded;
 float3 primary=TEPattern(v,_HatchAngle);
 float footprint=_Projection<.5?max(length(ddx(v.uv*_HatchScale)),length(ddy(v.uv*_HatchScale))):max(length(ddx(v.positionOS*_HatchScale)),length(ddy(v.positionOS*_HatchScale)));
 float visibility=1-smoothstep(.06,.16,footprint);
 float shadow=saturate((_HatchStart-lightLevel)/max(_HatchStart,.01));shadow=pow(shadow,_HatchContrast);
 float deep=1-smoothstep(.08,.28,lightLevel);
 float ink=TEInkCoverage(primary)*shadow;
 if(_CrossStrength>.001&&deep>.001){float3 second=TEPattern(v,_CrossAngle);ink=1-(1-ink)*(1-TEInkCoverage(second)*deep*_CrossStrength);}
 // No hatch in direct highlights, even on dark albedo. Material darkness only changes ink weight.
 ink*=lerp(1,1-albedoLuma,_TextureInfluence)*_HatchIntensity*visibility*_HatchEnable;
 float3 result=lerp(shaded,min(shaded*.38,_InkColor.rgb),saturate(ink));
 if(_GrainIntensity>0){float grain=TEPatternAt(v,0,_GrainScale).b;result*=1+(grain-.5)*_GrainIntensity*2;}
 return result;
}
#endif


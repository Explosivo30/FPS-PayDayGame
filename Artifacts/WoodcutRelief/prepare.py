from pathlib import Path
s=Path('Assets/Rendering/EngravingCharacter/EngravedInk.shader').read_text(encoding='utf-8-sig')
s=s.replace('Torbellino/Characters/Engraved Ink','Torbellino/Characters/Woodcut Relief')
s=s.replace('_InkStrength("Ink strength",Range(0,1))=.93','''_InkStrength("Ink strength",Range(0,1))=1
  _PaperColor("Unprinted paper",Color)=(.9,.77,.57,1)
  _AccentColor("Vermilion printing ink",Color)=(.48,.075,.035,1)
  _PaletteStrength("Reference palette / original colors",Range(0,1))=.85
  _Carving("Light cuts into dark ink",Range(0,1))=.65
  _Roughness("Print wear",Range(0,1))=.35''')
s=s.replace('_BaseMap_ST, _BaseColor, _InkColor, _EmissionColor;','_BaseMap_ST, _BaseColor, _InkColor, _EmissionColor, _PaperColor, _AccentColor;')
s=s.replace('_BandSoftness,_UseRestCoordinates;','_BandSoftness,_UseRestCoordinates,_PaletteStrength,_Carving,_Roughness;')
a=s.index('   float Stroke(');b=s.index('   float2 Flow(',a)
s=s[:a]+'''   float ValueNoise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
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
    float mask=(1-smoothstep(widthHere-fw,widthHere+fw,d))*step(.06,random);
    return lerp(mask,width*.65,saturate((fw-.32)*2));
   }
''' +s[b:]
a=s.index('    float primary=');b=s.index('    return half4(',a)
s=s[:a]+'''    f=lerp(f,cap,capWeight);
    float2 flowing=float2(f.x+f.y*.28,f.y);
    float2 crossing=float2(f.x-f.y*.8,f.y*.75+f.x*.3);
    float broad=ValueNoise(f*.065+float2(17,31));
    // Distinct paper, light hatch, dense hatch and solid ink regions.
    float shadow=1-smoothstep(.24,.42,lighting+(broad-.5)*.16);
    float halfTone=1-smoothstep(.48,.68,lighting+(broad-.5)*.11);
    float primary=Gouge(flowing,1.7,.21);
    float secondary=Gouge(crossing,6.1,.16);
    float carved=Gouge(float2(f.x+f.y*.12+3.2,f.y*1.15),12.4,.105);
    float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,TRANSFORM_TEX(i.uv,_BaseMap)).rgb*_BaseColor.rgb;
    float y=dot(base,float3(.2126,.7152,.0722));
    float darkMaterial=1-smoothstep(.045,.16,y);
    float warm=smoothstep(.06,.32,base.r-base.g)*smoothstep(.05,.2,base.r-base.b);
    float3 paper=_PaperColor.rgb;
    float3 limited=lerp(paper,_AccentColor.rgb,warm);
    limited=lerp(limited,_InkColor.rgb,darkMaterial);
    float3 pigment=lerp(base,limited,_PaletteStrength);
    // Large ink silhouettes with carved paper slivers are central to relief printing.
    float ink=max(primary*halfTone,secondary*halfTone*.65);
    ink=max(ink,shadow*.96);
    ink=max(ink,darkMaterial*.96);
    float3 result=lerp(pigment,_InkColor.rgb,saturate(ink*_InkStrength));
    float cutZone=max(shadow*.8,darkMaterial*(.35+.65*lighting));
    float cutMask=carved*cutZone*_Carving;
    float3 cutPigment=lerp(paper,lerp(paper,_AccentColor.rgb,.6),warm);
    result=lerp(result,cutPigment,cutMask);
    // Small clusters of ink loss; noise remains in material coordinates.
    float grain=Hash(floor(f*float2(11,19)));
    float fleck=step(.985,grain)*_Roughness*lerp(.5,1,ink);
    result=lerp(result,paper,fleck);
    float paperNoise=(ValueNoise(f*2.3)-.5)*.045*_Roughness;
    result=max(0,result+paperNoise*(1-ink));
    result+=_EmissionColor.rgb*.15;
''' +s[b:]
Path('Assets/Rendering/WoodcutRelief/WoodcutRelief.shader').write_text(s,encoding='utf-8')
# A separate generator and scene; previous trial remains available for A/B.
s=Path('Assets/Editor/EngravingCharacter/EngravingCharacterBuild.cs').read_text(encoding='utf-8-sig')
s=s.replace('EngravingCharacterBuild','WoodcutReliefBuild').replace('Assets/Rendering/EngravingCharacter','Assets/Rendering/WoodcutRelief').replace('Artifacts/EngravingCharacter','Artifacts/WoodcutRelief')
s=s.replace('Generate character comparison','Generate woodcut relief comparison').replace('Torbellino/Characters/Engraved Ink','Torbellino/Characters/Woodcut Relief')
s=s.replace('outline.SetFloat("_Width",1.6f)','outline.SetFloat("_Width",2.4f)')
s=s.replace('m.SetFloat("_Density",90*unitScale)','m.SetFloat("_Density",100*unitScale)').replace('radius*unitScale*2*Mathf.PI*90','radius*unitScale*2*Mathf.PI*100')
s=s.replace('EngravingCharacterComparison.unity','WoodcutReliefComparison.unity').replace('EngravingRisoComparison.Generate();','')
s=s.replace('Object-space per-part curved/planar flow, cap projection, four light regions. Original material colors retained.','New relief material: navy ink masses, tapered paper cuts, vermilion/paper preset, local pattern coordinates. PaletteStrength=0 retains source colors.')
Path('Assets/Editor/WoodcutRelief/WoodcutReliefBuild.cs').write_text(s,encoding='utf-8')
# Existing isolated Unity lab, same packages, same imported Ariete and original materials.
import shutil
lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
for folder in ['Assets/Rendering/WoodcutRelief','Assets/Editor/WoodcutRelief']:
 (lab/folder).mkdir(parents=True,exist_ok=True)
 for f in Path(folder).iterdir():
  if f.is_file():
   if f.suffix=='.cs':(lab/f).write_text(f.read_text(encoding='utf-8-sig').replace('"Artifacts/WoodcutRelief"','"D:/Unity/FPS-PayDayGame/Artifacts/WoodcutRelief"'),encoding='utf-8')
   else:shutil.copy2(f,lab/f)

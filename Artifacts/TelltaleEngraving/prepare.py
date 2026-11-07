from pathlib import Path
s=Path('Assets/Rendering/EngravingCharacter/EngravingShowcase.cs').read_text(encoding='utf-8-sig').replace('EngravingShowcase','TelltaleViewer').replace('GRABADO DE PERSONAJE / misma geometria, misma luz','TELLTALE ENGRAVING / material 3D').replace('2 · Grabado','2 · Telltale')
Path('Assets/Rendering/TelltaleEngraving/TelltaleViewer.cs').write_text(s,encoding='utf-8')
s=Path('Assets/Editor/EngravingCharacter/EngravingCharacterBuild.cs').read_text(encoding='utf-8-sig')
s=s.replace('EngravingCharacterBuild','TelltaleBuild').replace('EngravingShowcase','TelltaleViewer').replace('Assets/Rendering/EngravingCharacter','Assets/Rendering/TelltaleEngraving')
s=s.replace('const string Output="Artifacts/EngravingCharacter";','static string Output;\n    static int Stage;')
s=s.replace('[MenuItem("Tools/Rendering/Engraving/Generate character comparison")]\n    public static void Generate(){','''[MenuItem("Tools/Rendering/Telltale Engraving/Generate final test scene")]
    public static void Final(){Generate(8);}
    public static void Lighting(){Generate(2);}
    public static void Hatch(){Generate(3);}
    public static void Cross(){Generate(4);}
    public static void Outline(){Generate(5);}
    public static void Ink(){Generate(6);}
    static void Generate(int stage){
        Stage=stage; Output="Artifacts/TelltaleEngraving/"+stage.ToString("D2");''')
s=s.replace('var surface=Shader.Find("Torbellino/Characters/Engraved Ink");var outlineShader=Shader.Find("Torbellino/Characters/Engraved Outline");','var surface=Shader.Find("Torbellino/Telltale Engraving/Character");var outlineShader=Shader.Find("Torbellino/Telltale Engraving/Outline");')
s=s.replace('var outline=new Material(outlineShader);outline.SetFloat("_Width",1.6f);AssetDatabase.CreateAsset(outline,Art+"/Materials/Contour.mat");','Material outline=null; if(Stage>=5){outline=new Material(outlineShader);AssetDatabase.CreateAsset(outline,Art+"/Materials/Contour.mat");}')
a=s.index('                    var sorted=');b=s.index('                    AssetDatabase.CreateAsset(m',a)
s=s[:a]+'''                    m.SetFloat("_UseEmission",source.IsKeywordEnabled("_EMISSION")?1:0);
                    foreach(string property in new[]{"_BumpMap","_OcclusionMap","_EmissionMap"})if(source.HasProperty(property)&&source.GetTexture(property))m.SetTexture(property,source.GetTexture(property));
                    m.SetFloat("_UseNormalMap",source.IsKeywordEnabled("_NORMALMAP")?1:0);
                    if(source.HasProperty("_Metallic"))m.SetFloat("_Metallic",source.GetFloat("_Metallic"));
                    if(source.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",source.GetFloat("_Smoothness"));
                    float unitScale=(Mathf.Abs(filter.transform.lossyScale.x)+Mathf.Abs(filter.transform.lossyScale.y)+Mathf.Abs(filter.transform.lossyScale.z))/3;
                    m.SetFloat("_Projection",1);m.SetFloat("_HatchScale",50*unitScale);
                    m.SetFloat("_HatchEnable",Stage>=3?1:0);m.SetFloat("_CrossStrength",Stage>=4?.25f:0);
                    m.SetFloat("_GrainIntensity",Stage>=6?.008f:0);
                    var hatch=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/HatchAtlas.png");if(hatch)m.SetTexture("_HatchTex",hatch);
''' +s[b:]
s=s.replace('var hull=new GameObject("Ink contour");','if(Stage>=5){var hull=new GameObject("Ink contour");').replace('hullRenderer.shadowCastingMode=ShadowCastingMode.Off;','hullRenderer.shadowCastingMode=ShadowCastingMode.Off;}')
s=s.replace('EngravingCharacterComparison.unity','TelltaleComparison.unity')
s=s.replace('EngravingRisoComparison.Generate();','')
s=s.replace('var errors=new[]{surface,outlineShader}.SelectMany','var errors=new[]{surface,outlineShader}.Where(s=>s!=null).SelectMany')
s=s.replace('Object-space per-part curved/planar flow, cap projection, four light regions. Original material colors retained.','Telltale Engraving stage "+Stage+". Source colors and maps copied into independent materials.')
s=s.replace('Curve guide is analytic per part, not principal-curvature extraction. No organic hero model in this test. Rigid articulated source, not a skinned deformation validation.','Ariete rigid articulated source. Object-triplanar test mapping. Organic facial readability requires a separate human character test.')
s=s.replace('ENGRAVING CHARACTER COMPARISON COMPLETE','TELLTALE STAGE COMPLETE')
s=s.replace('for(int i=0;i<24;i++)','for(int i=0;i<(Stage==8?24:0);i++)')
Path('Assets/Rendering/TelltaleEngraving/Editor/TelltaleBuild.cs').write_text(s,encoding='utf-8')
# Copy just this system to the already configured minimal Unity lab.
import shutil
lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
for f in Path('Assets/Rendering/TelltaleEngraving').rglob('*'):
 if f.is_file():
  dst=lab/f;dst.parent.mkdir(parents=True,exist_ok=True)
  if f.suffix=='.cs' and f.parent.name=='Editor':dst.write_text(f.read_text(encoding='utf-8-sig').replace('"Artifacts/TelltaleEngraving/"','"D:/Unity/FPS-PayDayGame/Artifacts/TelltaleEngraving/"'),encoding='utf-8')
  else:shutil.copy2(f,dst)

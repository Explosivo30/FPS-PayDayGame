using UnityEngine;
using UnityEngine.Rendering;
namespace Torbellino.Rendering{
public sealed class RisographGameShowcase:MonoBehaviour{
 public GameObject original,telltale,woodcut;public Camera view;public Light keyLight;public RenderPipelineAsset plainPipeline,gamePipeline;
 public Vector3 target;public float distance=4.4f,yaw=155,pitch=6;public int mode=1;public bool autoOrbit;
 RenderPipelineAsset previousDefault,previousQuality;bool started;
 void Start(){previousDefault=GraphicsSettings.defaultRenderPipeline;previousQuality=QualitySettings.renderPipeline;started=true;Apply(mode);}
 void OnDisable(){if(!started)return;if(GraphicsSettings.defaultRenderPipeline==plainPipeline||GraphicsSettings.defaultRenderPipeline==gamePipeline)GraphicsSettings.defaultRenderPipeline=previousDefault;if(QualitySettings.renderPipeline==plainPipeline||QualitySettings.renderPipeline==gamePipeline)QualitySettings.renderPipeline=previousQuality;started=false;}
 public void Apply(int value){mode=value;original.SetActive(mode<2);telltale.SetActive(mode==2);woodcut.SetActive(mode==3);GraphicsSettings.defaultRenderPipeline=mode==1?gamePipeline:plainPipeline;QualitySettings.renderPipeline=mode==1?gamePipeline:plainPipeline;}
 public void PositionCamera(){view.transform.position=target+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);view.transform.LookAt(target);}
 void Update(){for(int i=0;i<4;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))Apply(i);if(Input.GetKeyDown(KeyCode.Space))autoOrbit=!autoOrbit;if(Input.GetMouseButton(0)){yaw+=Input.GetAxis("Mouse X")*3;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-20,65);}distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.2f,1.2f,7);if(autoOrbit)yaw+=Time.deltaTime*18;if(Input.GetKey(KeyCode.L))keyLight.transform.Rotate(Vector3.up,Time.deltaTime*40,Space.World);PositionCamera();}
 void OnGUI(){GUI.Box(new Rect(16,16,740,100),"HELIX / COMPARACION DE ESTILOS");string[] labels={"1 Original","2 Risograph Game","3 Telltale","4 Woodcut Relief"};for(int i=0;i<4;i++)if(GUI.Button(new Rect(28+i*178,46,170,28),labels[i]))Apply(i);GUI.Label(new Rect(28,82,710,25),"Arrastrar: orbita · Rueda: zoom · Espacio: giro · L: luz. Risograph: efecto de camara; otros: materiales.");}
}
}

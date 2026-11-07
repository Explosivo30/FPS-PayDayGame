using UnityEngine;
using UnityEngine.Rendering;

namespace Torbellino.Rendering
{
    // A reversible visual viewer; it has no gameplay or global renderer changes.
    public sealed class EngravingShowcase : MonoBehaviour
    {
        public GameObject original, engraved;
        public Camera view;
        public RenderPipelineAsset previewPipeline;
        RenderPipelineAsset oldPipeline,oldQuality;
        bool changedPipeline;
        public Light keyLight;
        public Vector3 target = new Vector3(0,1.1f,0);
        public float distance=4.4f, yaw=160, pitch=8;
        public bool useEngraving=true, autoOrbit;
        void Start(){
            if(previewPipeline){oldPipeline=GraphicsSettings.defaultRenderPipeline;oldQuality=QualitySettings.renderPipeline;GraphicsSettings.defaultRenderPipeline=previewPipeline;QualitySettings.renderPipeline=previewPipeline;changedPipeline=true;}
            Apply();
        }
        void OnDisable(){if(changedPipeline){if(GraphicsSettings.defaultRenderPipeline==previewPipeline)GraphicsSettings.defaultRenderPipeline=oldPipeline;if(QualitySettings.renderPipeline==previewPipeline)QualitySettings.renderPipeline=oldQuality;changedPipeline=false;}}
        void Update(){
            if(Input.GetKeyDown(KeyCode.Alpha1)){useEngraving=false;Apply();}
            if(Input.GetKeyDown(KeyCode.Alpha2)){useEngraving=true;Apply();}
            if(Input.GetKeyDown(KeyCode.Space))autoOrbit=!autoOrbit;
            if(Input.GetMouseButton(0)){yaw+=Input.GetAxis("Mouse X")*3;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-20,65);}
            distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.2f,1.2f,7);
            if(autoOrbit)yaw+=Time.deltaTime*18;
            PositionCamera();
            if(Input.GetKey(KeyCode.L))keyLight.transform.Rotate(Vector3.up,Time.deltaTime*40,Space.World);
        }
        public void Apply(){original.SetActive(!useEngraving);engraved.SetActive(useEngraving);}
        public void PositionCamera(){view.transform.position=target+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);view.transform.LookAt(target);}
        void OnGUI(){
            GUI.Box(new Rect(16,16,540,94),"GRABADO DE PERSONAJE / misma geometria, misma luz");
            if(GUI.Button(new Rect(28,44,160,28),"1 · Original")){useEngraving=false;Apply();}
            if(GUI.Button(new Rect(198,44,160,28),"2 · Grabado")){useEngraving=true;Apply();}
            GUI.Label(new Rect(28,79,520,24),"Arrastrar: orbita · Rueda: zoom · Espacio: giro · L: mover luz");
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Project.Scripts.Fractures;
using UnityEngine;
using UnityEngine.SceneManagement;

// Self-contained toy scene: no tower session, progression or profile writes.
public sealed class DestructionPlayground : MonoBehaviour
{
    public Camera view;
    public Transform cannon;
    public Material pulseMaterial;
    CharacterController motor;
    float pitch, vertical, nextShot, nextBlast, flash, shake;
    int shots, hits, blasts;
    AudioSource sound;
    AudioClip shotSound, blastSound;
    readonly List<string> errors = new List<string>();
    bool validating;
    void Start()
    {
        RunPause.Reset(); motor = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 0;
        shotSound = Tone(100, .09f); blastSound = Tone(44, .3f);
        Application.logMessageReceived += Error;
        validating=System.Environment.GetCommandLineArgs().Contains("-destruction-qa");
        if (validating) StartCoroutine(Validate());
    }
    AudioClip Tone(float hz, float duration)
    {
        int count = (int)(22050 * duration); var data = new float[count]; var random = new System.Random(17);
        for (int i=0;i<count;i++) { float t=(float)i/count; data[i]=(Mathf.Sin(i*hz*2*Mathf.PI/22050)*(1-t)+(float)(random.NextDouble()-.5)*.35f)*(1-t)*.5f; }
        var clip=AudioClip.Create("Sandbox impact",count,1,22050,false);clip.SetData(data,0);return clip;
    }
    void Error(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception) errors.Add(message); }
    void OnDestroy() { Application.logMessageReceived -= Error; if(shotSound)Destroy(shotSound);if(blastSound)Destroy(blastSound); }
    void Update()
    {
        if(validating) return;
        if(Input.GetKeyDown(KeyCode.Escape)) { bool locked=Cursor.lockState==CursorLockMode.Locked;Cursor.lockState=locked?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=locked; }
        if(Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        if(Cursor.lockState!=CursorLockMode.Locked) return;
        transform.Rotate(0,Input.GetAxisRaw("Mouse X")*2,0); pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-80,80);
        view.transform.localRotation=Quaternion.Euler(pitch,0,0);
        float dt=Time.deltaTime;
        var move=Vector3.ClampMagnitude(transform.right*Input.GetAxisRaw("Horizontal")+transform.forward*Input.GetAxisRaw("Vertical"),1);
        if(motor.isGrounded&&vertical<0)vertical=-2;
        if(motor.isGrounded&&Input.GetKeyDown(KeyCode.Space))vertical=7;
        vertical-=20*dt;motor.Move((move*(Input.GetKey(KeyCode.LeftShift)?10:6)+Vector3.up*vertical)*dt);
        if(transform.position.y < -8) { motor.enabled=false;transform.position=new Vector3(0,1,-12);motor.enabled=true;vertical=0; }
        if(Input.GetMouseButton(0)&&Time.time>=nextShot) { Fire(false);nextShot=Time.time+.12f; }
        if(Input.GetMouseButton(1)&&Time.time>=nextBlast) { Fire(true);nextBlast=Time.time+1.1f; }
        flash=Mathf.Max(0,flash-dt*5);shake=Mathf.Max(0,shake-dt*3);
        cannon.localPosition=new Vector3(.3f,-.28f,.65f-flash*.13f);
        view.transform.localPosition=new Vector3(Mathf.Sin(Time.time*57)*shake*.025f,.65f+Mathf.Cos(Time.time*43)*shake*.025f,0);
    }
    public bool Fire(bool blast)
    {
        shots++; if(blast)blasts++;
        flash=1;shake=blast?1:.2f;sound.PlayOneShot(blast?blastSound:shotSound,blast?.7f:.35f);
        var origin=view.transform.position;var direction=view.transform.forward;
        if(!Physics.Raycast(origin,direction,out var hit,70,~(1<<7|1<<2),QueryTriggerInteraction.Ignore))return false;
        var detonator=hit.collider.GetComponentInParent<ImpactDetonator>();
        if(detonator!=null) { detonator.HandleRaycastHit(hit,blast?100:40);hits++; }
        if(blast)
        {
            foreach(var collider in Physics.OverlapSphere(hit.point,3.5f))
            {
                if(collider.GetComponent<FractureThis>()!=null)collider.GetComponent<ImpactDetonator>()?.DetonateRaycast(hit,24);
            }
            foreach(var node in Object.FindObjectsByType<ChunkNode>(FindObjectsSortMode.None))
            {
                var body=node.GetComponent<Rigidbody>();
                if(body.isKinematic || Vector3.Distance(node.GetComponent<Collider>().ClosestPoint(hit.point),hit.point)>3.5f) continue;
                var delta=body.worldCenterOfMass-hit.point;
                node.ReleaseFromImpact((delta.normalized+Vector3.up*.45f)*Mathf.Lerp(18,5,Mathf.Clamp01(delta.magnitude/3.5f)),hit.point);
            }
        }
        Pulse(hit.point,blast?new Color(1,.4f,.08f):new Color(.3f,1,.9f),blast?2.5f:.25f);
        return detonator!=null;
    }
    void Pulse(Vector3 point,Color color,float scale)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(go.GetComponent<Collider>());go.layer=2;
        go.transform.position=point;go.transform.localScale=Vector3.one*scale;
        var material=new Material(pulseMaterial);material.SetColor("_BaseColor",color);go.GetComponent<Renderer>().sharedMaterial=material;
        StartCoroutine(FadePulse(go,material));
    }
    IEnumerator FadePulse(GameObject go,Material material)
    { var size=go.transform.localScale;for(float t=0;t<.16f;t+=Time.deltaTime){go.transform.localScale=size*(1-t/.16f);yield return null;}Destroy(go);Destroy(material); }
    void OnGUI()
    {
        GUI.color=new Color(.035f,.07f,.1f,.88f);GUI.DrawTexture(new Rect(18,18,620,106),Texture2D.whiteTexture);
        GUI.color=Color.white;var title=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold};
        GUI.Label(new Rect(34,24,600,36),"ROMPE TODO  /  PATIO VORONOI",title);
        GUI.Label(new Rect(34,62,600,25),"WASD mover · Espacio saltar · Shift correr · R reconstruir");
        GUI.Label(new Rect(34,87,600,25),"Mantén clic: perforadora ∞   |   Clic derecho: impacto explosivo   |   Esc: ratón");
        GUI.color=new Color(.4f,1,.85f);GUI.Label(new Rect(Screen.width/2-6,Screen.height/2-12,30,25),"+");
        GUI.color=Color.white;GUI.Label(new Rect(24,Screen.height-45,950,30),$"Impactos destructivos: {hits}   ·   Explosiones: {blasts}   ·   Munición infinita. Haz agujeros, corta soportes, derriba torres.");
    }
    IEnumerator Validate()
    {
        yield return new WaitForSeconds(3);
        string folder=Path.GetFullPath("Artifacts/DestructionPlayground");Directory.CreateDirectory(folder);
        int total=Object.FindObjectsByType<ChunkNode>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        var target=GameObject.Find("01 PERFORA");view.transform.LookAt(target.transform.position);Physics.SyncTransforms();
        ScreenCapture.CaptureScreenshot(folder+"/before.png");yield return new WaitForSeconds(.4f);
        bool hit=Fire(false);yield return new WaitForSeconds(.5f);
        view.transform.LookAt(GameObject.Find("02 ABRE PASO").transform.position);
        bool blast=Fire(true);yield return new WaitForSeconds(1);
        int released=Object.FindObjectsByType<ChunkNode>(FindObjectsSortMode.None).Count(n=>!n.GetComponent<Rigidbody>().isKinematic&&n.GetComponent<Rigidbody>().useGravity);
        ScreenCapture.CaptureScreenshot(folder+"/after.png");yield return new WaitForSeconds(.5f);
        bool materials=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null&&m.shader.isSupported));
        File.WriteAllText(folder+"/results.txt",$"Chunks generated: {total}\nPlayer ray hit Voronoi: {hit}\nExplosion ray: {blast}\nReleased bodies: {released}\nSupported assigned materials: {materials}\nErrors: {string.Join(" | ",errors)}\n");
        Application.Quit(total>0&&hit&&blast&&released>0&&materials&&errors.Count==0?0:1);
    }
}

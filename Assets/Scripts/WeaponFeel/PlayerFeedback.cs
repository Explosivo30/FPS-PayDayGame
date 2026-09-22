using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(95)]
[DisallowMultipleComponent]
public class PlayerFeedback : MonoBehaviour
{
    [Range(0,1)] public float soundVolume=.26f;
    [Range(0,1)] public float cameraMotion=.65f;
    PlayerStateMachine player;
    Shield shield;
    Transform motion;
    Canvas canvas;
    Sprite barSprite;
    Image healthFill,shieldFill,healthTrail;
    Image[] borders;
    RectTransform direction;
    Image directionImage;
    TMP_Text healthText,shieldText,statusText;
    AudioSource audioSource;
    AudioClip[] sounds;
    float hitTime,healTime,landOffset,landVelocity,healthDelayed=1,damageDelay;
    Vector3 damageSource;
    Color damageColor;
    bool recovering;
    public float CameraOffset=>landOffset;
    public bool DirectionVisible=>hitTime>0;
    void Awake()
    {
        player=GetComponent<PlayerStateMachine>();shield=GetComponent<Shield>();
        var pivot=new GameObject("Player view motion").transform;pivot.SetParent(player.headCam,false);
        var children=new System.Collections.Generic.List<Transform>();
        foreach(Transform child in player.headCam)if(child!=pivot)children.Add(child);
        foreach(var child in children)child.SetParent(pivot,false);
        motion=pivot;
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
        sounds=new AudioClip[7];for(int i=0;i<sounds.Length;i++)sounds[i]=MakeSound(i);
        CreateHUD();
    }
    void OnEnable()
    { player.Damaged+=OnDamage;player.Landed+=OnLand;player.Jumped+=OnJump;player.Stepped+=OnStep;player.Healed+=OnHeal; }
    void OnDisable()
    {
        player.Damaged-=OnDamage;player.Landed-=OnLand;player.Jumped-=OnJump;player.Stepped-=OnStep;player.Healed-=OnHeal;
        ResetFeedback();if(canvas!=null)canvas.gameObject.SetActive(false);
    }
    void Start()
    {
        foreach(var hud in FindObjectsByType<LabHUD>(FindObjectsSortMode.None))
            if(hud.player==player&&hud.healthText!=null)hud.healthText.enabled=false;
    }
    void OnDestroy(){foreach(var clip in sounds)if(clip!=null)Destroy(clip);if(canvas!=null)Destroy(canvas.gameObject);if(barSprite!=null)Destroy(barSprite);}
    void Play(int index,float strength=1)
    { audioSource.pitch=1;audioSource.PlayOneShot(sounds[index],soundVolume*strength); }
    void OnDamage(PlayerDamage hit)
    {
        damageColor=hit.HealthLoss>0?new Color(1,.24f,.12f):new Color(.1f,.8f,1);
        damageSource=hit.Source;hitTime=.6f;damageDelay=.35f;
        Play(hit.ShieldBroken?2:hit.HealthLoss>0?1:0,hit.ShieldBroken?1:.65f);
        if(hit.ShieldBroken){statusText.text="ESCUDO ROTO";hitTime=.9f;}
        else statusText.text=hit.HealthLoss>0?"DAÑO A LA SALUD":"IMPACTO EN EL ESCUDO";
        landVelocity-=.09f;
    }
    void OnLand(float speed){landVelocity-=Mathf.Clamp((speed-2)*.055f,.08f,.6f);Play(4,Mathf.Clamp01(speed/12));}
    void OnJump(){landVelocity+=.13f;Play(3,.2f);}
    void OnStep(){Play(3,player.IsCrouched?.22f:.42f);}
    void OnHeal(float amount){healTime=.65f;Play(5,.7f);}
    public void NotifyAmmo(){Play(5,.3f);}
    public void ResetFeedback(){landOffset=landVelocity=hitTime=healTime=0;if(motion!=null)motion.localPosition=Vector3.zero;}
    void LateUpdate()
    {
        if(canvas==null||player==null)return;
        canvas.enabled=!(TowerSession.Instance?.ModalVisible??false);
        if(!canvas.gameObject.activeSelf)canvas.gameObject.SetActive(true);
        if(!WeaponAction.CombatPaused&&!player.IsDead)
        {
            float dt=Time.deltaTime;
            float j=landVelocity+18*landOffset,e=Mathf.Exp(-18*dt);
            landOffset=(landOffset+j*dt)*e;landVelocity=(landVelocity-18*j*dt)*e;
            landOffset=Mathf.Clamp(landOffset,-.045f,.025f);
            motion.localPosition=Vector3.up*landOffset*cameraMotion;
            hitTime=Mathf.Max(0,hitTime-dt);healTime=Mathf.Max(0,healTime-dt);damageDelay-=dt;
            if(damageDelay<=0)healthDelayed=Mathf.MoveTowards(healthDelayed,player.Health/player.MaxHealth,dt*.8f);
            bool regen=shield!=null&&shield.IsRegenerating;
            if(regen&&!recovering)Play(6,.45f);recovering=regen;
        }
        float health=player.Health/player.MaxHealth;
        healthFill.fillAmount=health;healthTrail.fillAmount=Mathf.Max(health,healthDelayed);
        healthFill.color=healTime>0?new Color(.3f,1,.65f):health<=.3f?new Color(1,.3f,.18f):new Color(.85f,.94f,.97f);
        shieldFill.fillAmount=shield!=null&&shield.Max>0?shield.Current/shield.Max:0;
        healthText.text="SALUD  "+Mathf.CeilToInt(player.Health)+" / "+Mathf.CeilToInt(player.MaxHealth);
        shieldText.text="ESCUDO  "+Mathf.CeilToInt(shield!=null?shield.Current:0)+
            (shield!=null&&shield.IsRegenerating?"  RECUPERANDO":"");
        float edge=Mathf.Max(hitTime*.2f,health<=.3f?.035f:0);
        foreach(var border in borders)border.color=new Color(damageColor.r,damageColor.g,damageColor.b,edge);
        Vector3 delta=damageSource-player.transform.position;
        directionImage.color=new Color(damageColor.r,damageColor.g,damageColor.b,delta.sqrMagnitude>.01f?Mathf.Clamp01(hitTime*2):0);
        float angle=Vector3.SignedAngle(player.transform.forward,Vector3.ProjectOnPlane(delta,Vector3.up),Vector3.up);
        direction.localRotation=Quaternion.Euler(0,0,-angle);
        if(hitTime<=0)statusText.text=health<=.3f?"SALUD CRÍTICA":player.IsSliding?"DESLIZAMIENTO":"";
        statusText.color=hitTime>0?damageColor:new Color(1,.68f,.28f,.8f);
    }
    void CreateHUD()
    {
        barSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f));
        var root=new GameObject("Player status");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,gameObject.scene);canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=8;
        var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        healthText=Label(root.transform,"Health",new Vector2(48,116),new Vector2(320,24),18);
        shieldText=Label(root.transform,"Shield",new Vector2(48,66),new Vector2(360,24),16);
        Bar(root.transform,"Health back",new Vector2(48,102),new Vector2(240,8),new Color(.04f,.08f,.1f,.9f));
        healthTrail=Bar(root.transform,"Damage trail",new Vector2(48,102),new Vector2(240,8),new Color(1,.4f,.14f));
        healthFill=Bar(root.transform,"Health fill",new Vector2(48,102),new Vector2(240,8),Color.white);
        Bar(root.transform,"Shield back",new Vector2(48,54),new Vector2(240,5),new Color(.04f,.08f,.1f,.9f));
        shieldFill=Bar(root.transform,"Shield fill",new Vector2(48,54),new Vector2(240,5),new Color(.1f,.8f,1));
        statusText=Label(root.transform,"Player status",new Vector2(0,125),new Vector2(600,32),20);
        statusText.rectTransform.anchorMin=statusText.rectTransform.anchorMax=new Vector2(.5f,0);
        statusText.rectTransform.pivot=new Vector2(.5f,0);statusText.alignment=TextAlignmentOptions.Center;
        borders=new Image[4];
        borders[0]=Bar(root.transform,"Top",new Vector2(0,1067),new Vector2(1920,13),Color.clear);
        borders[1]=Bar(root.transform,"Bottom",Vector2.zero,new Vector2(1920,13),Color.clear);
        borders[2]=Bar(root.transform,"Left",Vector2.zero,new Vector2(13,1080),Color.clear);
        borders[3]=Bar(root.transform,"Right",new Vector2(1907,0),new Vector2(13,1080),Color.clear);
        direction=new GameObject("Incoming direction",typeof(RectTransform)).GetComponent<RectTransform>();
        direction.SetParent(root.transform,false);direction.anchorMin=direction.anchorMax=direction.pivot=new Vector2(.5f,.5f);direction.sizeDelta=Vector2.zero;
        directionImage=Bar(direction,"Source",new Vector2(-18,82),new Vector2(36,4),Color.clear);
        directionImage.rectTransform.anchorMin=directionImage.rectTransform.anchorMax=new Vector2(.5f,.5f);
    }
    Image Bar(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var image=go.AddComponent<Image>();image.sprite=barSprite;image.color=color;image.raycastTarget=false;
        var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;
        image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillAmount=1;return image;
    }
    static TMP_Text Label(Transform parent,string name,Vector2 position,Vector2 size,float font)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var text=go.AddComponent<TextMeshProUGUI>();text.fontSize=font;text.color=new Color(.82f,.93f,.96f);text.raycastTarget=false;
        var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;return text;
    }
    static AudioClip MakeSound(int kind)
    {
        const int rate=22050;float length=kind==2?.28f:kind==5||kind==6?.32f:.12f;
        var samples=new float[Mathf.CeilToInt(rate*length)];var random=new System.Random(490+kind);float filtered=0;
        for(int i=0;i<samples.Length;i++)
        {
            float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;filtered+=(noise-filtered)*.16f;
            float frequency=kind==0?650:kind==1?115:kind==2?420-700*t:kind==3?85:kind==4?65:600+1100*t;
            float envelope=Mathf.Exp(-t*(kind==2?18:kind>=5?12:45));
            samples[i]=(Mathf.Sin(2*Mathf.PI*frequency*t)*.28f+filtered*(kind==2?.65f:kind>=5?.05f:.55f))*envelope;
        }
        var clip=AudioClip.Create("Player feedback "+kind,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
    }
}

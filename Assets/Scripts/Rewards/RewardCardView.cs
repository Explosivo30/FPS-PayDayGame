using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation only. Selection is a request to RunRewards, never a stat mutation.</summary>
public class RewardCardView : MonoBehaviour
{
    public AudioClip revealSound,selectSound,confirmSound;
    RunRewards rewards;
    GameObject panel;
    CanvasGroup canvas;
    TMP_Text heading,kicker,detail,confirmation;
    Button confirm;
    readonly GameObject[] cards=new GameObject[3],specialFrames=new GameObject[3];
    readonly Image[] plates=new Image[3],borders=new Image[3],icons=new Image[3];
    readonly TMP_Text[] titles=new TMP_Text[3],targets=new TMP_Text[3],descriptions=new TMP_Text[3],previews=new TMP_Text[3],ranks=new TMP_Text[3];
    AudioSource audioSource;
    RewardState shown=RewardState.Idle;
    float stateAt;
    int selected;
    bool inputsReleased;
    static readonly Color Ink=new Color(.025f,.065f,.09f),Paper=new Color(.85f,.90f,.88f),Cyan=new Color(.1f,.87f,.86f),Amber=new Color(1,.62f,.12f);
    public bool Visible=>panel!=null&&panel.activeSelf;
    void Awake()
    {
        rewards=GetComponent<RunRewards>();
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;audioSource.priority=12;
        rewards.Changed+=Refresh;
    }
    void OnDestroy(){if(rewards!=null)rewards.Changed-=Refresh;if(panel!=null)Destroy(panel);}
    void Ensure()
    {
        if(panel!=null)return;
        panel=new GameObject("Wave reward cards",typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(panel,gameObject.scene);
        var root=panel.AddComponent<Canvas>();root.renderMode=RenderMode.ScreenSpaceOverlay;root.sortingOrder=120;
        panel.AddComponent<GraphicRaycaster>();
        var scaler=panel.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var bg=panel.AddComponent<Image>();bg.color=new Color(.012f,.03f,.045f,.94f);canvas=panel.AddComponent<CanvasGroup>();
        kicker=Text(panel.transform,"Kicker",new Vector2(160,927),new Vector2(1600,36),22,Amber);
        heading=Text(panel.transform,"Heading",new Vector2(160,851),new Vector2(1600,70),46,Paper);heading.fontStyle=FontStyles.Bold;
        detail=Text(panel.transform,"Detail",new Vector2(160,795),new Vector2(1600,42),22,new Color(.55f,.73f,.77f));
        for(int i=0;i<3;i++)
        {
            int index=i;
            borders[i]=Box(panel.transform,"Card "+(i+1),new Vector2(160+i*546,238),new Vector2(508,538),Cyan);
            cards[i]=borders[i].gameObject;
            plates[i]=Box(cards[i].transform,"Ceramic",new Vector2(3,3),new Vector2(502,532),Paper);
            specialFrames[i]=Box(cards[i].transform,"Protocol frame",new Vector2(9,9),new Vector2(490,520),Color.clear).gameObject;
            Box(specialFrames[i].transform,"Left rail",Vector2.zero,new Vector2(2,520),Amber).raycastTarget=false;
            Box(specialFrames[i].transform,"Right rail",new Vector2(488,0),new Vector2(2,520),Amber).raycastTarget=false;
            Box(specialFrames[i].transform,"Top rail",new Vector2(0,518),new Vector2(490,2),Amber).raycastTarget=false;
            Box(specialFrames[i].transform,"Bottom rail",Vector2.zero,new Vector2(490,2),Amber).raycastTarget=false;
            var button=cards[i].AddComponent<Button>();button.targetGraphic=plates[i];button.onClick.AddListener(()=>Select(index));
            var colors=button.colors;colors.highlightedColor=new Color(.9f,1,1);colors.pressedColor=Color.white;button.colors=colors;
            var art=Box(cards[i].transform,"Icon field",new Vector2(18,317),new Vector2(472,201),Ink);
            var stripe=Box(art.transform,"Circuit line",new Vector2(24,26),new Vector2(424,2),new Color(.14f,.3f,.34f));
            Text(art.transform,"Index",new Vector2(20,153),new Vector2(120,34),20,Cyan).text="0"+(i+1)+" /";
            icons[i]=Box(art.transform,"Symbol",new Vector2(185,43),new Vector2(102,102),Cyan);icons[i].preserveAspect=true;
            targets[i]=Text(cards[i].transform,"Target",new Vector2(27,275),new Vector2(450,28),18,new Color(.16f,.34f,.37f));
            titles[i]=Text(cards[i].transform,"Name",new Vector2(27,211),new Vector2(454,59),29,Ink);titles[i].fontStyle=FontStyles.Bold;
            descriptions[i]=Text(cards[i].transform,"Description",new Vector2(27,125),new Vector2(454,80),20,new Color(.17f,.26f,.29f));
            previews[i]=Text(cards[i].transform,"Preview",new Vector2(27,42),new Vector2(454,76),24,Ink);previews[i].fontStyle=FontStyles.Bold;
            ranks[i]=Text(cards[i].transform,"Rank",new Vector2(27,10),new Vector2(454,28),16,new Color(.16f,.34f,.37f));
        }
        var buttonImage=Box(panel.transform,"Confirm",new Vector2(1208,106),new Vector2(552,76),Amber);
        confirm=buttonImage.gameObject.AddComponent<Button>();confirm.targetGraphic=buttonImage;
        confirm.onClick.AddListener(Confirm);
        confirmation=Text(buttonImage.transform,"Label",new Vector2(18,8),new Vector2(516,60),24,Ink);
        confirmation.alignment=TextAlignmentOptions.Center;
        Text(panel.transform,"Help",new Vector2(160,116),new Vector2(1000,55),20,Paper).text="1 / 2 / 3  SELECCIONAR     ENTER  ELEGIR\nGRATIS · DURA ESTE INTENTO · EL COMBATE ESTÁ EN PAUSA";
    }
    void Refresh()
    {
        if(!rewards.Pending){if(panel!=null)panel.SetActive(false);shown=RewardState.Idle;return;}
        Ensure();panel.SetActive(true);
        if(shown!=rewards.State)
        {
            shown=rewards.State;stateAt=Time.unscaledTime;inputsReleased=false;
            if(shown==RewardState.Choosing){selected=0;Play(revealSound,.5f);}
            if(shown==RewardState.Confirming)Play(confirmSound,.65f);
        }
        kicker.text="OLEADA "+GameManager.Instance.CompletedWaves.ToString("00")+" COMPLETADA  /  RECOMPENSA";
        heading.text=shown==RewardState.Celebrating?"PRUEBA SUPERADA":shown==RewardState.Confirming?"MEJORA INSTALADA":"ELIGE TU PRÓXIMA VENTAJA";
        detail.text=shown==RewardState.Confirming?rewards.Chosen.title+" · "+rewards.LastResult.Replace("\n"," / "):
            "Una elección. Tu forma de combatir. Las otras cartas se descartan.";
        for(int i=0;i<3;i++)
        {
            bool exists=i<rewards.Offer.Count;
            cards[i].SetActive(exists&&shown!=RewardState.Celebrating);
            if(!exists)continue;
            var card=rewards.Offer[i];bool special=card.special;
            specialFrames[i].SetActive(special);
            icons[i].sprite=card.icon;icons[i].color=special?Amber:Cyan;
            targets[i].text=(special?"PROTOCOLO ESPECIAL  /  ":"")+RewardContext.Name(card.weaponId).ToUpperInvariant();
            titles[i].text=card.title;descriptions[i].text=card.description;
            previews[i].text=shown==RewardState.Confirming&&card==rewards.Chosen?rewards.LastResult:rewards.Preview(card);
            int rank=rewards.Level(card)+(shown==RewardState.Confirming&&card==rewards.Chosen?0:1);
            ranks[i].text=card.maxLevel==0?"SUMINISTRO INMEDIATO":"NIVEL "+Mathf.Min(rank,card.maxLevel)+" / "+card.maxLevel;
            borders[i].color=i==selected?Amber:special?new Color(.62f,.38f,.1f):new Color(.13f,.3f,.34f);
        }
        confirm.gameObject.SetActive(shown==RewardState.Choosing);
        confirmation.text="ELEGIR CARTA "+(selected+1)+"    /    ENTER";
    }
    public void Select(int index)
    {
        if(!inputsReleased||rewards.State!=RewardState.Choosing||index<0||index>=rewards.Offer.Count)return;
        selected=index;Play(selectSound,.22f);Refresh();
    }
    public void Confirm()
    {
        if(inputsReleased&&rewards.State==RewardState.Choosing)rewards.Confirm(selected);
    }
    void Update()
    {
        if(!Visible)return;
        float t=Time.unscaledTime-stateAt;
        if(!inputsReleased&&t>.2f&&!Input.GetMouseButton(0)&&!Input.GetMouseButton(1)&&!Input.GetKey(KeyCode.Return))inputsReleased=true;
        if(rewards.State==RewardState.Choosing)
        {
            if(Input.GetKeyDown(KeyCode.Alpha1))Select(0);if(Input.GetKeyDown(KeyCode.Alpha2))Select(1);if(Input.GetKeyDown(KeyCode.Alpha3))Select(2);
            if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))Confirm();
        }
        canvas.alpha=Mathf.Clamp01(t/.16f);
        for(int i=0;i<3;i++)if(cards[i]!=null&&cards[i].activeSelf)
        {
            float targetScale=i==selected?1.015f:1;
            if(rewards.State==RewardState.Confirming)targetScale=rewards.Offer[i]==rewards.Chosen?1.035f:.97f;
            cards[i].transform.localScale=Vector3.Lerp(cards[i].transform.localScale,Vector3.one*targetScale,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
        }
    }
    void Play(AudioClip clip,float volume){if(clip!=null)audioSource.PlayOneShot(clip,volume);}
    static Image Box(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;
        var image=go.AddComponent<Image>();image.color=color;return image;
    }
    static TMP_Text Text(Transform parent,string name,Vector2 position,Vector2 size,int font,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;
        var text=go.AddComponent<TextMeshProUGUI>();text.fontSize=font;text.color=color;text.raycastTarget=false;
        text.enableAutoSizing=true;text.fontSizeMin=font-3;text.fontSizeMax=font;return text;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The view selects an offer; all rewards and pauses remain owned by RunRewards.</summary>
public class RewardCardView : MonoBehaviour
{
    public AudioClip revealSound,selectSound,confirmSound;
    RunRewards rewards;GameObject panel;CanvasGroup canvas;
    TMP_Text heading,kicker,detail,confirmation;
    Button confirm;AudioSource audioSource;
    readonly GameObject[] cards=new GameObject[3],specialFrames=new GameObject[3];
    readonly HoloPanel[] borders=new HoloPanel[3];
    readonly Image[] icons=new Image[3],selectionRails=new Image[3];
    readonly TMP_Text[] titles=new TMP_Text[3],targets=new TMP_Text[3],descriptions=new TMP_Text[3],previews=new TMP_Text[3],ranks=new TMP_Text[3];
    RewardState shown=RewardState.Idle;float stateAt;int selected;bool inputsReleased;
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
        var scaler=panel.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        InterfaceStyle.Modal(panel.transform,"MÓDULOS DE RECOMPENSA");canvas=panel.AddComponent<CanvasGroup>();
        kicker=InterfaceStyle.Label(panel.transform,"Wave complete",new Vector2(132,861),new Vector2(1500,30),18,InterfaceStyle.Gold);
        heading=InterfaceStyle.Label(panel.transform,"Heading",new Vector2(126,896),new Vector2(1630,74),56,InterfaceStyle.Cyan,true);
        detail=InterfaceStyle.Label(panel.transform,"Detail",new Vector2(132,812),new Vector2(1620,32),20,InterfaceStyle.Muted);
        for(int i=0;i<3;i++)
        {
            int index=i;
            borders[i]=InterfaceStyle.Panel(panel.transform,"Card "+(i+1),new Vector2(132+i*568,236),new Vector2(512,550),InterfaceStyle.Cyan);
            borders[i].raycastTarget=true;cards[i]=borders[i].gameObject;
            var button=cards[i].AddComponent<Button>();button.targetGraphic=borders[i];button.onClick.AddListener(()=>Select(index));
            var colors=button.colors;colors.highlightedColor=new Color(1.2f,1.3f,1.35f);colors.selectedColor=Color.white;button.colors=colors;
            InterfaceStyle.Label(cards[i].transform,"Index",new Vector2(26,498),new Vector2(120,30),24,InterfaceStyle.Cyan,true).text="0"+(i+1)+" /";
            var special=InterfaceStyle.Label(cards[i].transform,"Protocol",new Vector2(254,499),new Vector2(230,28),20,InterfaceStyle.Magenta,true);
            special.text="PROTOCOLO ESPECIAL";special.alignment=TextAlignmentOptions.Right;specialFrames[i]=special.gameObject;
            var chip=InterfaceStyle.Panel(cards[i].transform,"Module socket",new Vector2(193,370),new Vector2(126,112),new Color(.04f,.4f,.55f,.6f));
            chip.cut=22;chip.fill=new Color(.006f,.02f,.04f,.8f);
            icons[i]=InterfaceStyle.Line(cards[i].transform,"Module icon",new Vector2(216,387),new Vector2(80,80),InterfaceStyle.Cyan);
            icons[i].preserveAspect=true;
            InterfaceStyle.Line(cards[i].transform,"Circuit L",new Vector2(29,425),new Vector2(130,1),new Color(.03f,.55f,.7f,.35f));
            InterfaceStyle.Line(cards[i].transform,"Circuit R",new Vector2(353,425),new Vector2(130,1),new Color(.03f,.55f,.7f,.35f));
            targets[i]=InterfaceStyle.Label(cards[i].transform,"Target",new Vector2(28,326),new Vector2(456,29),18,InterfaceStyle.Muted);
            titles[i]=InterfaceStyle.Label(cards[i].transform,"Name",new Vector2(25,254),new Vector2(462,68),40,InterfaceStyle.Cyan,true);
            descriptions[i]=InterfaceStyle.Label(cards[i].transform,"Description",new Vector2(28,181),new Vector2(456,70),21,InterfaceStyle.TextColor);
            var readout=InterfaceStyle.Panel(cards[i].transform,"Effect readout",new Vector2(22,78),new Vector2(468,96),new Color(.08f,.34f,.45f,.65f));
            readout.fill=new Color(.008f,.055f,.08f,.95f);readout.cut=8;
            previews[i]=InterfaceStyle.Label(cards[i].transform,"Preview",new Vector2(38,91),new Vector2(436,70),25,InterfaceStyle.TextColor);
            previews[i].fontStyle=FontStyles.Bold;
            ranks[i]=InterfaceStyle.Label(cards[i].transform,"Rank",new Vector2(28,31),new Vector2(450,28),18,InterfaceStyle.Muted);
            selectionRails[i]=InterfaceStyle.Line(cards[i].transform,"Selected rail",new Vector2(29,12),new Vector2(454,3),InterfaceStyle.Cyan);
        }
        confirm=InterfaceStyle.Button(panel.transform,"Confirm",new Vector2(1268,93),new Vector2(512,64),Confirm,out confirmation,InterfaceStyle.Cyan);
        confirmation.alignment=TextAlignmentOptions.Center;
        InterfaceStyle.Label(panel.transform,"Help",new Vector2(132,96),new Vector2(1050,61),20,InterfaceStyle.TextColor).text=
            "1 / 2 / 3  SELECCIONAR     ENTER  INSTALAR\nELECCIÓN GRATUITA  /  COMBATE EN PAUSA";
        InterfaceStyle.CenterDesign(root);
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
        kicker.text="OLEADA "+GameManager.Instance.CompletedWaves.ToString("00")+" COMPLETADA  /  ACCESO A MEJORAS";
        heading.text=shown==RewardState.Celebrating?"PRUEBA SUPERADA":shown==RewardState.Confirming?"MÓDULO INSTALADO":"ELIGE TU PRÓXIMA MEJORA";
        detail.text=shown==RewardState.Confirming?rewards.Chosen.title+" · "+rewards.LastResult.Replace("\n"," / "):
            "Instala un módulo. Se conserva durante este intento, incluso al cambiar de piso.";
        for(int i=0;i<3;i++)
        {
            bool exists=i<rewards.Offer.Count;cards[i].SetActive(exists&&shown!=RewardState.Celebrating);
            if(!exists)continue;
            var card=rewards.Offer[i];Color accent=card.special?InterfaceStyle.Magenta:InterfaceStyle.Cyan;
            specialFrames[i].SetActive(card.special);borders[i].doubleRail=card.special;
            borders[i].SetAccent(i==selected?accent:accent*new Color(.4f,.4f,.4f,1));
            borders[i].SetVerticesDirty();
            selectionRails[i].color=accent;selectionRails[i].gameObject.SetActive(i==selected);
            icons[i].sprite=card.icon;icons[i].color=accent;
            targets[i].text=RewardContext.Name(card.weaponId).ToUpperInvariant()+"  /  "+(card.group==RewardGroup.Offense?"OFENSIVA":card.group==RewardGroup.Utility?"MANEJO":"SUPERVIVENCIA");
            titles[i].text=card.title.ToUpperInvariant();titles[i].color=accent;descriptions[i].text=card.description;
            previews[i].text=shown==RewardState.Confirming&&card==rewards.Chosen?rewards.LastResult:rewards.Preview(card);
            int rank=rewards.Level(card)+(shown==RewardState.Confirming&&card==rewards.Chosen?0:1);
            ranks[i].text=card.maxLevel==0?"CONSUMIBLE  /  EFECTO INMEDIATO":"NIVEL "+Mathf.Min(rank,card.maxLevel)+" / "+card.maxLevel;
        }
        confirm.gameObject.SetActive(shown==RewardState.Choosing);
        confirmation.text="ELEGIR CARTA "+(selected+1)+"    /    ENTER";
    }
    public void Select(int index)
    {
        if(!inputsReleased||rewards.State!=RewardState.Choosing||index<0||index>=rewards.Offer.Count)return;
        selected=index;Play(selectSound,.22f);Refresh();
    }
    public void Confirm(){if(inputsReleased&&rewards.State==RewardState.Choosing)rewards.Confirm(selected);}
    void Update()
    {
        if(!Visible)return;float t=Time.unscaledTime-stateAt;
        if(!inputsReleased&&t>.2f&&!Input.GetMouseButton(0)&&!Input.GetMouseButton(1)&&!Input.GetKey(KeyCode.Return))inputsReleased=true;
        if(rewards.State==RewardState.Choosing)
        {
            if(Input.GetKeyDown(KeyCode.Alpha1))Select(0);if(Input.GetKeyDown(KeyCode.Alpha2))Select(1);if(Input.GetKeyDown(KeyCode.Alpha3))Select(2);
            if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))Confirm();
        }
        canvas.alpha=Mathf.Clamp01(t/.16f);
        for(int i=0;i<3;i++)if(cards[i].activeSelf)
        {
            float size=i==selected?1.012f:1;
            if(rewards.State==RewardState.Confirming)size=rewards.Offer[i]==rewards.Chosen?1.026f:.98f;
            cards[i].transform.localScale=Vector3.Lerp(cards[i].transform.localScale,Vector3.one*size,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
        }
    }
    void Play(AudioClip clip,float volume){if(clip!=null)audioSource.PlayOneShot(clip,volume);}
}

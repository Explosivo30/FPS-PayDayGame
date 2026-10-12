using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerRunMenu : MonoBehaviour
{
    TowerSession session;
    GameObject panel;
    TMP_Text title,summary,wallet,footnote;
    readonly List<Button> cards=new List<Button>();
    readonly List<TMP_Text> labels=new List<TMP_Text>();
    Button main,ammo,heal,historyButton;
    TMP_Text toast;
    bool history;
    readonly List<GameObject> sections=new List<GameObject>();
    CanvasGroup reveal;float openedAt;
    float toastUntil;
    AudioSource purchaseAudio;AudioClip purchaseClip;
    UpgradeManager observedUpgrades;
    bool death;
    public bool Visible=>panel!=null&&panel.activeSelf;
    public bool ShowingDeath=>Visible&&death;
    void Awake(){session=GetComponent<TowerSession>();}
    void Start()
    {
        observedUpgrades=UpgradeManager.Instance;if(observedUpgrades!=null)observedUpgrades.Purchased+=OnPurchased;
        purchaseAudio=gameObject.AddComponent<AudioSource>();purchaseAudio.playOnAwake=false;purchaseAudio.spatialBlend=0;
        purchaseClip=ImpactFeedbackPlayer.MakeSound("Mejora comprada",1);
    }
    void OnPurchased(StatUpgrade upgrade){PurchaseFeedback("INSTALADO · "+upgrade.displayName);}
    void PurchaseFeedback(string message)
    {
        if(toast!=null){toast.text=message;toastUntil=Time.unscaledTime+2.5f;}
        if(purchaseAudio!=null&&purchaseClip!=null)purchaseAudio.PlayOneShot(purchaseClip,.5f);
    }
    void EnsureUI()
    {
        if(panel!=null)return;
        cards.Clear();labels.Clear();
        panel=new GameObject("Run decisions",typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(panel,gameObject.scene);
        var canvas=panel.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
        panel.AddComponent<GraphicRaycaster>();
        var scaler=panel.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        InterfaceStyle.Modal(panel.transform,"TERMINAL DE EQUIPO");
        panel.transform.Find("Link").gameObject.SetActive(false);
        reveal=panel.AddComponent<CanvasGroup>();
        for(int column=0;column<3;column++)
        {
            string[] names={"ARMAS","OPERADOR","PROTOCOLOS"};
            string[] notes={"POTENCIA / CAPACIDAD","DEFENSA / MOVILIDAD","INVESTIGACIÓN PERMANENTE"};
            var section=InterfaceStyle.Label(panel.transform,"Section "+column,new Vector2(132+column*568,769),new Vector2(510,45),32,column==2?InterfaceStyle.Magenta:InterfaceStyle.Cyan,true);
            section.text=names[column];sections.Add(section.gameObject);
            var note=InterfaceStyle.Label(panel.transform,"Section note",new Vector2(132+column*568,738),new Vector2(510,25),15,InterfaceStyle.Muted);
            note.text=notes[column];sections.Add(note.gameObject);
        }
        title=Text(panel.transform,"Title",new Vector2(126,897),new Vector2(1190,70),54);
        summary=Text(panel.transform,"Summary",new Vector2(132,812),new Vector2(1630,34),19);
        wallet=Text(panel.transform,"Resources",new Vector2(1300,900),new Vector2(488,66),23);
        footnote=Text(panel.transform,"Note",new Vector2(132,51),new Vector2(1630,25),16);
        footnote.color=InterfaceStyle.Muted;
        InterfaceStyle.Display(title,54,InterfaceStyle.Cyan);wallet.color=InterfaceStyle.Gold;wallet.alignment=TextAlignmentOptions.Right;
        for(int i=0;i<9;i++)
        {
            int captured=i;
            var button=ButtonAt("Upgrade "+i,new Vector2(132+(i<3?1:i<6?0:2)*568,574-i%3*174),new Vector2(512,154),()=>Select(captured),out var label);
            cards.Add(button);labels.Add(label);
            var plate=button.GetComponent<HoloPanel>();if(i>=6)plate.SetAccent(InterfaceStyle.Magenta);
            label.fontSize=19;label.fontSizeMin=16;
            if(i<UpgradeManager.Instance.catalog.Length&&session.Rewards!=null)
            {
                var upgrade=UpgradeManager.Instance.catalog[i];
                var icon=session.Rewards.catalog.FirstOrDefault(c=>upgrade.target==UpgradeTarget.Weapon?
                    c.kind==RewardKind.WeaponStat&&c.stat==(upgrade.weaponStat==WeaponStat.RecoilKickUp||upgrade.weaponStat==WeaponStat.Spread?WeaponStat.FireRate:upgrade.weaponStat):
                    c.kind==(upgrade.playerStat==PlayerStat.Shield?RewardKind.Shield:RewardKind.Health));
                if(icon!=null)
                {
                    var symbol=InterfaceStyle.Line(button.transform,"Upgrade symbol",new Vector2(22,95),new Vector2(30,30),i>=6?InterfaceStyle.Magenta:InterfaceStyle.Cyan);
                    symbol.sprite=icon.icon;symbol.preserveAspect=true;
                }
            }
        }
        main=ButtonAt("Continue",new Vector2(1268,93),new Vector2(512,64),()=>{if(death)session.RestartRun();else ShopManager.Instance.CloseShop();},out var mainLabel);
        mainLabel.text="VOLVER";
        ammo=ButtonAt("Ammo",new Vector2(132,93),new Vector2(512,64),()=>PurchaseSupply(false),out var ammoLabel);
        ammoLabel.text="REPONER MUNICIÓN  /  40 CHIPS";
        heal=ButtonAt("Heal",new Vector2(700,93),new Vector2(512,64),()=>PurchaseSupply(true),out var healLabel);
        healLabel.text="RECUPERAR 40 SALUD  /  60 CHIPS";
        historyButton=ButtonAt("Collected cards",new Vector2(1508,976),new Vector2(274,40),()=>{history=!history;Refresh();},out var historyLabel);
        historyLabel.text="VER MIS CARTAS";historyLabel.fontSize=18;
        toast=Text(panel.transform,"Purchase feedback",new Vector2(132,184),new Vector2(1630,35),21);toast.color=new Color(1,.65f,.16f);
        InterfaceStyle.CenterDesign(canvas);
        panel.SetActive(false);
    }
    public void ShowShop(){EnsureUI();death=false;history=false;openedAt=Time.unscaledTime;panel.SetActive(true);Refresh();}
    public void ShowDeath(){EnsureUI();death=true;history=false;openedAt=Time.unscaledTime;panel.SetActive(true);Refresh();}
    public void Hide(){if(panel!=null)panel.SetActive(false);}
    void Update(){if(Visible){reveal.alpha=Mathf.Clamp01((Time.unscaledTime-openedAt)/.16f);Refresh();}}
    void OnDestroy(){if(observedUpgrades!=null)observedUpgrades.Purchased-=OnPurchased;if(purchaseClip!=null)Destroy(purchaseClip);if(panel!=null)Destroy(panel);}
    void Refresh()
    {
        var progression=session.Progression;var manager=UpgradeManager.Instance;
        if(toast!=null&&Time.unscaledTime>toastUntil)toast.text="";
        historyButton.gameObject.SetActive(!death&&session.Rewards!=null);
        historyButton.GetComponentInChildren<TMP_Text>().text=history?"VOLVER A TIENDA":"VER MIS CARTAS";
        foreach(var section in sections)section.SetActive(!history&&!death);
        toast.gameObject.SetActive(!history);
        summary.rectTransform.anchoredPosition=history?new Vector2(132,226):death?new Vector2(132,745):new Vector2(132,812);
        summary.rectTransform.sizeDelta=history?new Vector2(1630,570):death?new Vector2(1630,100):new Vector2(1630,34);
        summary.fontSize=history?23:19;
        if(history)
        {
            title.text="CARTAS DE ESTE INTENTO";summary.text=session.Rewards.Summary();
            wallet.text="ARCHIVO DEL INTENTO";
            foreach(var card in cards)card.gameObject.SetActive(false);
            ammo.gameObject.SetActive(false);heal.gameObject.SetActive(false);
            footnote.text="Las cartas y las compras de tienda se combinan. Sus niveles son independientes.";
            return;
        }
        title.text=death?"INTENTO TERMINADO":"TERMINAL DE MEJORAS";
        wallet.text=(death?"":"<size=32>"+GameManager.Instance.GetPlayerPoints()+" CHIPS</size>\n")+"NÚCLEOS  "+progression.Data.cores;
        if(death)
        {
            summary.text="Piso alcanzado: "+session.CurrentFloor.displayName+"     •     Bajas: "+session.RunKills+"     •     Oleadas: "+session.RunWaves+
                "\nCausa: "+session.player.LastDamageCause+"     •     Núcleos obtenidos en este intento: "+progression.EarnedThisRun+".";
            footnote.text="Los chips y las mejoras del intento se reinician. Los descubrimientos y las investigaciones se conservan.";
        }
        else
        {
            summary.text="Invierte los chips de este intento. Investigar con núcleos abre nuevas opciones para comprarlas durante las partidas.";
            footnote.text="E  VOLVER AL COMBATE     •     Reservas limitadas. Las mejoras también afectan al equipo guardado.";
        }
        if(progression.SaveError!=null)footnote.text="No se pudo guardar el progreso: "+progression.SaveError;
        for(int i=0;i<cards.Count;i++)
        {
            if(death)
            {
                cards[i].gameObject.SetActive(i<3);
                InterfaceStyle.Place(cards[i].GetComponent<RectTransform>(),new Vector2(132+i*568,520),new Vector2(512,174));
                if(i>=3)continue;
                bool unlocked=progression.IsUnlocked(RunProgression.RecipeIds[i]);
                labels[i].text="<size=23>"+RunProgression.RecipeNames[i]+"</size>\n<size=17>Desbloquea una opción en la tienda.</size>\n"+
                    (unlocked?"INVESTIGADO":"INVESTIGAR  /  "+RunProgression.RecipeCosts[i]+" NÚCLEOS");
                cards[i].interactable=!unlocked&&progression.Data.cores>=RunProgression.RecipeCosts[i]&&progression.SaveError==null;
            }
            else
            {
                InterfaceStyle.Place(cards[i].GetComponent<RectTransform>(),new Vector2(132+(i<3?1:i<6?0:2)*568,574-i%3*174),new Vector2(512,154));
                bool exists=manager!=null&&i<manager.catalog.Length;cards[i].gameObject.SetActive(exists);
                if(!exists)continue;
                var upgrade=manager.catalog[i];
                bool unlocked=progression.IsUnlocked(upgrade.persistentUnlockId);
                int recipe=System.Array.IndexOf(RunProgression.RecipeIds,upgrade.persistentUnlockId);
                string cost=manager.GetNextCost(upgrade)<0?"MÁXIMO":"COMPRAR  /  "+manager.GetNextCost(upgrade)+" CHIPS";
                if(!unlocked&&recipe>=0)cost="INVESTIGAR  /  "+RunProgression.RecipeCosts[recipe]+" NÚCLEOS";
                labels[i].text="<indent=43><size=24><b>"+upgrade.displayName.ToUpperInvariant()+"</b></size>  <size=16>"+manager.GetLevel(upgrade)+"/"+upgrade.MaxLevel+"</size></indent>\n"+
                    "<size=17>"+ShopUpgradePreview.Describe(upgrade,session.player)+"</size>\n"+cost;
                cards[i].interactable=unlocked?manager.CanUpgrade(upgrade):recipe>=0&&progression.Data.cores>=RunProgression.RecipeCosts[recipe]&&progression.SaveError==null;
            }
        }
        main.GetComponentInChildren<TMP_Text>().text=death?"VOLVER A INTENTAR":"VOLVER AL COMBATE";
        ammo.gameObject.SetActive(!death);heal.gameObject.SetActive(!death);
        if(!death)
        {
            bool needsAmmo=false;foreach(var weapon in session.weapons.Weapons)
                if(weapon is BaseGun gun)needsAmmo|=gun.reserveAmmo<gun.maxReserveAmmo;
            ammo.interactable=needsAmmo&&CurrencyManager.Instance.SpendCheck(40);
            heal.interactable=session.player.Health<session.player.MaxHealth&&CurrencyManager.Instance.SpendCheck(60);
        }
    }
    void Select(int index)
    {
        if(death){session.Progression.TryUnlock(index);Refresh();return;}
        var upgrade=UpgradeManager.Instance.catalog[index];
        int recipe=System.Array.IndexOf(RunProgression.RecipeIds,upgrade.persistentUnlockId);
        if(!session.Progression.IsUnlocked(upgrade.persistentUnlockId)&&recipe>=0)session.Progression.TryUnlock(recipe);
        else UpgradeManager.Instance.BuyUpgrade(upgrade);
        Refresh();
    }
    public bool PurchaseSupply(bool medical)
    {
        if(death||session.player.IsDead||(session.Rewards!=null&&session.Rewards.Pending))return false;
        bool needed=medical?session.player.Health<session.player.MaxHealth:false;
        if(!medical)foreach(var weapon in session.weapons.Weapons)if(weapon is BaseGun gun)needed|=gun.reserveAmmo<gun.maxReserveAmmo;
        if(!needed||!CurrencyManager.Instance.Spend(medical?60:40))return false;
        if(medical)session.player.Heal(40);
        else foreach(var weapon in session.weapons.Weapons)if(weapon is BaseGun gun)gun.AddReserve(gun.ammo*2);
        PurchaseFeedback(medical?"SUMINISTRO · SALUD RECUPERADA":"SUMINISTRO · RESERVAS REPUESTAS");Refresh();return true;
    }
    Button ButtonAt(string name,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action,out TMP_Text label)
    {
        var button=InterfaceStyle.Button(panel.transform,name,position,size,action,out label);
        if(name.StartsWith("Upgrade"))label.font=TMPro.TMP_Settings.defaultFontAsset;
        return button;
    }
    static TMP_Text Text(Transform parent,string name,Vector2 position,Vector2 size,int font)
    {
        return InterfaceStyle.Label(parent,name,position,size,font,InterfaceStyle.TextColor);
    }
}

using System.Collections.Generic;
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
        var backdrop=panel.AddComponent<Image>();backdrop.color=new Color(.015f,.035f,.055f,.985f);
        title=Text(panel.transform,"Title",new Vector2(180,912),new Vector2(1500,62),42);
        summary=Text(panel.transform,"Summary",new Vector2(180,788),new Vector2(1560,112),23);
        wallet=Text(panel.transform,"Resources",new Vector2(180,742),new Vector2(1500,36),24);
        footnote=Text(panel.transform,"Note",new Vector2(180,42),new Vector2(1560,36),17);
        footnote.color=new Color(.5f,.73f,.79f);
        for(int i=0;i<9;i++)
        {
            int captured=i;
            var button=ButtonAt("Upgrade "+i,new Vector2(180+i%3*528,548-i/3*166),new Vector2(504,146),()=>Select(captured),out var label);
            cards.Add(button);labels.Add(label);
        }
        main=ButtonAt("Continue",new Vector2(1236,96),new Vector2(504,70),()=>{if(death)session.RestartRun();else ShopManager.Instance.CloseShop();},out var mainLabel);
        mainLabel.text="VOLVER";
        ammo=ButtonAt("Ammo",new Vector2(180,96),new Vector2(504,70),()=>PurchaseSupply(false),out var ammoLabel);
        ammoLabel.text="REPONER MUNICIÓN  /  40 CHIPS";
        heal=ButtonAt("Heal",new Vector2(708,96),new Vector2(504,70),()=>PurchaseSupply(true),out var healLabel);
        healLabel.text="RECUPERAR 40 SALUD  /  60 CHIPS";
        historyButton=ButtonAt("Collected cards",new Vector2(1476,733),new Vector2(264,46),()=>{history=!history;Refresh();},out var historyLabel);
        historyLabel.text="VER MIS CARTAS";historyLabel.fontSize=18;
        toast=Text(panel.transform,"Purchase feedback",new Vector2(180,190),new Vector2(1560,35),21);toast.color=new Color(1,.65f,.16f);
        panel.SetActive(false);
    }
    public void ShowShop(){EnsureUI();death=false;history=false;panel.SetActive(true);Refresh();}
    public void ShowDeath(){EnsureUI();death=true;history=false;panel.SetActive(true);Refresh();}
    public void Hide(){if(panel!=null)panel.SetActive(false);}
    void Update(){if(Visible)Refresh();}
    void OnDestroy(){if(observedUpgrades!=null)observedUpgrades.Purchased-=OnPurchased;if(purchaseClip!=null)Destroy(purchaseClip);if(panel!=null)Destroy(panel);}
    void Refresh()
    {
        var progression=session.Progression;var manager=UpgradeManager.Instance;
        if(toast!=null&&Time.unscaledTime>toastUntil)toast.text="";
        historyButton.gameObject.SetActive(!death&&session.Rewards!=null);
        historyButton.GetComponentInChildren<TMP_Text>().text=history?"VOLVER A TIENDA":"VER MIS CARTAS";
        summary.rectTransform.anchoredPosition=history?new Vector2(180,232):new Vector2(180,788);
        summary.rectTransform.sizeDelta=history?new Vector2(1560,490):new Vector2(1560,112);
        summary.fontSize=history?19:23;
        if(history)
        {
            title.text="CARTAS DE ESTE INTENTO";summary.text=session.Rewards.Summary();
            wallet.text="Se conservan al cambiar de piso. Se reinician al morir.";
            foreach(var card in cards)card.gameObject.SetActive(false);
            ammo.gameObject.SetActive(false);heal.gameObject.SetActive(false);
            footnote.text="Las cartas y las compras de tienda se combinan. Sus niveles son independientes.";
            return;
        }
        title.text=death?"INTENTO TERMINADO":"MEJORAR EQUIPO";
        wallet.text="NÚCLEOS GUARDADOS  "+progression.Data.cores+
            (death?"":"     /     CHIPS  "+GameManager.Instance.GetPlayerPoints());
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
                if(i>=3)continue;
                bool unlocked=progression.IsUnlocked(RunProgression.RecipeIds[i]);
                labels[i].text="<size=23>"+RunProgression.RecipeNames[i]+"</size>\n<size=17>Desbloquea una opción en la tienda.</size>\n"+
                    (unlocked?"INVESTIGADO":"INVESTIGAR  /  "+RunProgression.RecipeCosts[i]+" NÚCLEOS");
                cards[i].interactable=!unlocked&&progression.Data.cores>=RunProgression.RecipeCosts[i]&&progression.SaveError==null;
            }
            else
            {
                bool exists=manager!=null&&i<manager.catalog.Length;cards[i].gameObject.SetActive(exists);
                if(!exists)continue;
                var upgrade=manager.catalog[i];
                bool unlocked=progression.IsUnlocked(upgrade.persistentUnlockId);
                int recipe=System.Array.IndexOf(RunProgression.RecipeIds,upgrade.persistentUnlockId);
                string cost=manager.GetNextCost(upgrade)<0?"MÁXIMO":"COMPRAR  /  "+manager.GetNextCost(upgrade)+" CHIPS";
                if(!unlocked&&recipe>=0)cost="INVESTIGAR  /  "+RunProgression.RecipeCosts[recipe]+" NÚCLEOS";
                labels[i].text="<size=23>"+upgrade.displayName+"</size>  <size=16>"+manager.GetLevel(upgrade)+"/"+upgrade.MaxLevel+"</size>\n"+
                    "<size=16>"+ShopUpgradePreview.Describe(upgrade,session.player)+"</size>\n"+cost;
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
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(panel.transform,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;
        var image=go.AddComponent<Image>();image.color=new Color(.065f,.14f,.18f);
        var button=go.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.55f,.95f,1);colors.pressedColor=new Color(1,.68f,.3f);colors.disabledColor=new Color(.5f,.57f,.6f,.5f);button.colors=colors;
        button.onClick.AddListener(action);
        label=Text(go.transform,name+" text",new Vector2(18,13),size-new Vector2(36,26),20);
        label.alignment=TextAlignmentOptions.Left;label.verticalAlignment=VerticalAlignmentOptions.Middle;
        return button;
    }
    static TMP_Text Text(Transform parent,string name,Vector2 position,Vector2 size,int font)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var text=go.AddComponent<TextMeshProUGUI>();text.fontSize=font;text.color=new Color(.83f,.94f,.97f);text.raycastTarget=false;
        var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;return text;
    }
}

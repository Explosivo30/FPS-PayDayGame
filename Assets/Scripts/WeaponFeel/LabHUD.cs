using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LabHUD : MonoBehaviour
{
    public GunController controller;
    public PlayerStateMachine player;
    public TextMeshProUGUI weaponText,ammoText,stateText,waveText,scoreText,healthText;
    public Image hitMarker,killMarker,reloadBar;
    private float hitTime,killTime;
    private Canvas hudCanvas;
    private Image[] hitStrokes,killStrokes;
    private void Awake()
    {
        hudCanvas=GetComponentInParent<Canvas>();
        hitStrokes=CreateStrokes(hitMarker,false);killStrokes=CreateStrokes(killMarker,true);
    }
    private static Image[] CreateStrokes(Image root,bool kill)
    {
        if(root==null)return new Image[0];
        root.enabled=false;root.rectTransform.localRotation=Quaternion.identity;
        var strokes=new Image[4];int i=0;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
        {
            var go=new GameObject(kill?"Kill stroke":"Hit stroke");go.transform.SetParent(root.transform,false);
            var stroke=go.AddComponent<Image>();stroke.raycastTarget=false;stroke.color=Color.clear;
            stroke.rectTransform.sizeDelta=new Vector2(kill?2.8f:2f,kill?9f:6f);
            stroke.rectTransform.anchoredPosition=new Vector2(x,y)*(kill?11:8);
            stroke.rectTransform.localRotation=Quaternion.Euler(0,0,x==y?-45:45);
            strokes[i++]=stroke;
        }
        return strokes;
    }
    private static void ShowStrokes(Image[] strokes,Image root,float alpha,bool kill)
    {
        if(strokes==null||root==null)return;
        Color color=kill?new Color(1,.62f,.2f,alpha):new Color(.88f,1,1,alpha);
        foreach(var stroke in strokes)stroke.color=color;
        root.rectTransform.localScale=Vector3.one*(1+alpha*(kill?.35f:.18f));
    }
    private void OnEnable() { CombatFeedback.Hit+=OnHit;CombatFeedback.Kill+=OnKill; }
    private void OnDisable() { CombatFeedback.Hit-=OnHit;CombatFeedback.Kill-=OnKill; }
    private void OnHit() { hitTime=.12f; }
    private void OnKill() { killTime=.3f; }
    private void Update()
    {
        if(hudCanvas!=null)hudCanvas.enabled=!(TowerSession.Instance?.ModalVisible??false);
        var gun=controller!=null?controller.currentWeapon as BaseGun:null;
        var component=controller!=null?controller.currentWeapon as MonoBehaviour:null;
        var action=component!=null?component.GetComponent<WeaponAction>():null;
        if(weaponText!=null) weaponText.text=gun!=null?(gun is Pistol?"01 / PULSO":gun is MachineGun?"02 / CARABINA":"03 / BENELLI M4"):"04 / CUCHILLO";
        if(ammoText!=null) ammoText.text=gun!=null?$"{gun.currentAmmo:00} <size=18>/ {(gun.limitedReserve?gun.reserveAmmo:gun.ammo):00}"+(gun.limitedReserve?" RESERVA</size>":"</size>"):"—";
        if(stateText!=null) stateText.text=action!=null&&action.State==WeaponActionState.Reloading?"RECARGANDO":controller!=null&&controller.IsSwitching?"CAMBIANDO ARMA":"";
        if(reloadBar!=null) { reloadBar.gameObject.SetActive(action!=null&&action.State==WeaponActionState.Reloading);reloadBar.fillAmount=action!=null?action.Progress:0; }
        if(GameManager.Instance!=null)
        {
            var gm=GameManager.Instance;
            if(waveText!=null) waveText.text=gm.NextWaveIn>0?$"SIGUIENTE OLEADA  /  {Mathf.CeilToInt(gm.NextWaveIn):00}":$"OLEADA {gm.CurrentWave:00}  /  {gm.EnemiesRemaining:00} HOSTILES";
            if(scoreText!=null) scoreText.text=$"{gm.GetPlayerPoints():0000}  "+(TowerSession.Instance!=null?"CHIPS":"CR");
        }
        if(healthText!=null&&player!=null)
        {
            var shield=player.GetComponent<Shield>();
            healthText.text=$"SALUD  {Mathf.CeilToInt(player.Health):000}     ESCUDO  {(shield!=null?Mathf.CeilToInt(shield.Current):0):000}";
        }
        hitTime=Mathf.Max(0,hitTime-Time.deltaTime);killTime=Mathf.Max(0,killTime-Time.deltaTime);
        ShowStrokes(hitStrokes,hitMarker,killTime>0?0:hitTime/.12f,false);
        ShowStrokes(killStrokes,killMarker,killTime/.3f,true);
    }
}

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
    private void OnEnable() { CombatFeedback.Hit+=OnHit;CombatFeedback.Kill+=OnKill; }
    private void OnDisable() { CombatFeedback.Hit-=OnHit;CombatFeedback.Kill-=OnKill; }
    private void OnHit() { hitTime=.12f; }
    private void OnKill() { killTime=.3f; }
    private void Update()
    {
        var gun=controller!=null?controller.currentWeapon as BaseGun:null;
        var component=controller!=null?controller.currentWeapon as MonoBehaviour:null;
        var action=component!=null?component.GetComponent<WeaponAction>():null;
        if(weaponText!=null) weaponText.text=gun!=null?(gun is Pistol?"01 / PULSO":gun is MachineGun?"02 / CARABINA":"03 / BENELLI M4"):"04 / CUCHILLO";
        if(ammoText!=null) ammoText.text=gun!=null?$"{gun.currentAmmo:00} <size=18>/ {gun.ammo:00}</size>":"—";
        if(stateText!=null) stateText.text=action!=null&&action.State==WeaponActionState.Reloading?"RECARGANDO":controller!=null&&controller.IsSwitching?"CAMBIANDO ARMA":"";
        if(reloadBar!=null) { reloadBar.gameObject.SetActive(action!=null&&action.State==WeaponActionState.Reloading);reloadBar.fillAmount=action!=null?action.Progress:0; }
        if(GameManager.Instance!=null)
        {
            var gm=GameManager.Instance;
            if(waveText!=null) waveText.text=gm.NextWaveIn>0?$"SIGUIENTE OLEADA  /  {Mathf.CeilToInt(gm.NextWaveIn):00}":$"OLEADA {gm.CurrentWave:00}  /  {gm.EnemiesRemaining:00} HOSTILES";
            if(scoreText!=null) scoreText.text=$"{gm.GetPlayerPoints():0000}  CR";
        }
        if(healthText!=null&&player!=null)
        {
            var shield=player.GetComponent<Shield>();
            healthText.text=$"SALUD  {Mathf.CeilToInt(player.Health):000}     ESCUDO  {(shield!=null?Mathf.CeilToInt(shield.Current):0):000}";
        }
        hitTime=Mathf.Max(0,hitTime-Time.deltaTime);killTime=Mathf.Max(0,killTime-Time.deltaTime);
        if(hitMarker!=null) hitMarker.color=new Color(.7f,1,1,hitTime/.12f);
        if(killMarker!=null) killMarker.color=new Color(1,.58f,.18f,killTime/.3f);
    }
}

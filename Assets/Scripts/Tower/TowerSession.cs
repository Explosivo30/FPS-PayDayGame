using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-250)]
public class TowerSession : MonoBehaviour
{
    public static TowerSession Instance { get; private set; }
    public PlayerStateMachine player;
    public GunController weapons;
    public TowerOverlay overlay;
    public string firstScene = "LaboratoryFloor";
    public TowerFloor CurrentFloor { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool Ready { get; private set; }
    public int FloorsCleared { get; private set; }
    public int RunKills { get; private set; }
    public int RunWaves { get; private set; }
    public bool RunEnded { get; private set; }
    public RunProgression Progression { get; private set; }
    public PlayerRunMenu Menu { get; private set; }
    public RunRewards Rewards { get; private set; }
    public bool ModalVisible=>(Menu!=null&&Menu.Visible)||(Rewards!=null&&Rewards.Pending);
    public string RunId=>runToken;
    public int FloorVisit { get; private set; }
    readonly System.Collections.Generic.HashSet<string> completedTokens=new System.Collections.Generic.HashSet<string>();
    System.IDisposable transitionPause,deathPause;
    readonly string runToken=System.Guid.NewGuid().ToString("N");
    bool restarting;
    public void RecordKill(Vector3 position)
    {
        if(RunEnded)return;
        RunKills++;
        if(RunKills%3==0)TowerAmmoPickup.Spawn(position);
    }
    public void RecordWaveCompleted()
    {
        string completedKey=FloorVisit+"/"+GameManager.Instance.CompletedWaves;
        if(!completedTokens.Add(completedKey))return;
        RunWaves++;
        Rewards?.QueueWave(GameManager.Instance.CompletedWaves,RunWaves,CurrentFloor!=null&&GameManager.Instance.CompletedWaves==CurrentFloor.mandatoryWaves);
        if(CurrentFloor!=null&&GameManager.Instance.CompletedWaves==CurrentFloor.mandatoryWaves)
        {
            bool saved=Progression.BankFloor(runToken+"/"+FloorsCleared,CurrentFloor.gameObject.scene.name,CurrentFloor.floorNumber);
            ShowMessage(saved?"OBJETIVO COMPLETADO · +1 NÚCLEO GUARDADO · ASCENSOR DISPONIBLE":"OBJETIVO COMPLETADO · ASCENSOR DISPONIBLE",8);
        }
    }
    void Update()
    {
        if(!IsTransitioning&&!WeaponAction.CombatPaused&&GameManager.Instance!=null&&GameManager.Instance.WaitingForExtraWave&&Input.GetKeyDown(KeyCode.F))
            GameManager.Instance.RequestExtraWave();
    }
    public void EndRun(string cause)
    {
        if(RunEnded||IsTransitioning)return;
        RunEnded=true;
        deathPause=RunPause.Acquire(true);Rewards?.Abort();
        ShopManager.Instance?.CloseShop();
        IsTransitioning=true;player.enabled=false;weapons.enabled=false;
        foreach(var weapon in weapons.Weapons)weapon.GetComponent<WeaponAction>()?.Cancel();
        Menu.ShowDeath();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public bool ExitUnlocked => CurrentFloor != null && GameManager.Instance != null &&
        GameManager.Instance.CompletedWaves >= CurrentFloor.mandatoryWaves;
    public string Message { get; private set; }
    float messageUntil;
    void Awake() { Instance=this;Progression=gameObject.AddComponent<RunProgression>();Menu=gameObject.AddComponent<PlayerRunMenu>();Rewards=GetComponent<RunRewards>(); }
    IEnumerator Start()
    {
        yield return null;
        var recoil=GunRecoil.EnsureExists();
        if(recoil.gameObject.scene!=gameObject.scene && recoil.transform.parent==null)
            SceneManager.MoveGameObjectToScene(recoil.gameObject, gameObject.scene);
        GameManager.Instance.AutomaticWaves = false;
        Ready = true;
    }
    void OnDestroy() { transitionPause?.Dispose();deathPause?.Dispose();if (Instance == this) { Instance = null;RunPause.Reset(); } }
    public void EnterFloor(TowerFloor floor)
    {
        CurrentFloor = floor;FloorVisit++;
        SceneManager.SetActiveScene(floor.gameObject.scene);
        player.TeleportTo(floor.arrival.position, floor.arrival.rotation);
        GameManager.Instance.ConfigureFloor(floor);
        floor.elevator.SetOpen(true);
        ShowMessage(floor.briefing, 10);
    }
    public void ShowMessage(string text, float seconds = 3) { Message = text; messageUntil = Time.unscaledTime + seconds; }
    public string VisibleMessage => Time.unscaledTime < messageUntil ? Message : "";
    public bool RequestTravel(TowerElevator elevator)
    {
        if (IsTransitioning || (Rewards!=null&&Rewards.Pending) || !Ready || CurrentFloor == null || elevator != CurrentFloor.elevator) return false;
        if (!ExitUnlocked) { ShowMessage("ASCENSOR BLOQUEADO · Completa " + CurrentFloor.mandatoryWaves + " oleadas."); return false; }
        if (!elevator.Contains(player.transform.position)) { ShowMessage("Entra en la cabina para subir."); return false; }
        if (!Application.CanStreamedLevelBeLoaded(CurrentFloor.nextScene)) { ShowMessage("El siguiente piso no está disponible."); return false; }
        StartCoroutine(Travel());
        return true;
    }
    IEnumerator Travel()
    {
        IsTransitioning = true;
        transitionPause=RunPause.Acquire(false);
        ShopManager.Instance?.CloseShop();
        var previous = CurrentFloor;
        bool playerEnabled = player.enabled;
        bool weaponsEnabled = weapons.enabled;
        weapons.enabled = false;
        player.enabled = false;
        foreach (var weapon in weapons.Weapons)
        {
            weapon.GetComponent<WeaponAction>()?.Cancel();
            if (weapon is IAimable aim) aim.StopAiming();
        }
        previous.elevator.SetOpen(false);
        ShowMessage("ASCENDIENDO", 30);
        yield return new WaitForSecondsRealtime(1);
        yield return Fade(1);
        var operation = SceneManager.LoadSceneAsync(previous.nextScene, LoadSceneMode.Additive);
        if (operation == null)
        {
            transitionPause?.Dispose();transitionPause=null; player.enabled = playerEnabled; weapons.enabled = weaponsEnabled; previous.elevator.SetOpen(true);
            yield return Fade(0); IsTransitioning = false; ShowMessage("No se ha podido cargar el piso."); yield break;
        }
        yield return operation;
        var nextScene = SceneManager.GetSceneByName(previous.nextScene);
        TowerFloor next = null;
        foreach (var root in nextScene.GetRootGameObjects())
        {
            next = root.GetComponentInChildren<TowerFloor>();
            if (next != null) break;
        }
        if (next == null)
        {
            yield return SceneManager.UnloadSceneAsync(nextScene);
            transitionPause?.Dispose();transitionPause=null; player.enabled = playerEnabled; weapons.enabled = weaponsEnabled; previous.elevator.SetOpen(true);
            yield return Fade(0); IsTransitioning = false; ShowMessage("Falta la configuración del piso."); yield break;
        }
        GameManager.Instance.StopFloor();
        FloorsCleared++;
        EnterFloor(next);
        next.elevator.SetOpen(false, true);
        yield return SceneManager.UnloadSceneAsync(previous.gameObject.scene);
        yield return null;
        GunRecoil.EnsureExists().ResetRecoil();
        yield return Fade(0);
        next.elevator.SetOpen(true);
        yield return new WaitForSecondsRealtime(.9f);
        transitionPause?.Dispose();transitionPause=null;
        player.enabled = playerEnabled; weapons.enabled = weaponsEnabled;
        IsTransitioning = false;
        ShowMessage(next.briefing, 10);
    }
    IEnumerator Fade(float target)
    {
        float initial = overlay.fade.alpha;
        for (float t = 0; t < .3f; t += Time.unscaledDeltaTime)
        { overlay.fade.alpha = Mathf.Lerp(initial, target, t / .3f); yield return null; }
        overlay.fade.alpha = target;
    }
    public void RestartRun()
    {
        if ((!IsTransitioning||RunEnded)&&!restarting) { restarting=true;Menu.Hide();StartCoroutine(Restart()); }
    }
    IEnumerator Restart()
    {
        IsTransitioning = true; transitionPause=RunPause.Acquire(false);Rewards?.Abort();player.enabled = false;
        GameManager.Instance.StopFloor();
        ShowMessage("PRUEBA TERMINADA · REINICIANDO", 10);
        yield return Fade(1);
        yield return new WaitForSecondsRealtime(.8f);
        // Single unloads the persistent core too: equipment and currency start fresh.
        yield return SceneManager.LoadSceneAsync(firstScene, LoadSceneMode.Single);
    }
}

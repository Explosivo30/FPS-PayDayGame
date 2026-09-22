using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local configuration. Open any floor directly to start a fresh run there.</summary>
public class TowerFloor : MonoBehaviour
{
    public string displayName = "BOSQUE ROBÓTICO";
    [TextArea] public string briefing = "Completa 3 oleadas para desbloquear el ascensor.";
    public int floorNumber = 2;
    public int mandatoryWaves = 3;
    public string nextScene = "CrystalGarden";
    public Transform arrival;
    public Transform[] spawnPoints;
    public TowerElevator elevator;
    public float enemyHealth = 105f;
    public float enemyDamage = 10f;
    public float introductionSeconds = 18f;
    static bool loadingCore;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loadingCore = false; }
    IEnumerator Start()
    {
        if (TowerSession.Instance == null && !loadingCore)
        {
            loadingCore = true;
            yield return SceneManager.LoadSceneAsync("TowerCore", LoadSceneMode.Additive);
            loadingCore = false;
        }
        while (TowerSession.Instance == null || !TowerSession.Instance.Ready) yield return null;
        if (!TowerSession.Instance.IsTransitioning && TowerSession.Instance.CurrentFloor == null)
            TowerSession.Instance.EnterFloor(this);
    }
}

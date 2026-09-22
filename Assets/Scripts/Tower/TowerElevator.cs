using UnityEngine;
using TMPro;

public class TowerElevator : MonoBehaviour
{
    public Transform leftDoor, rightDoor;
    public TMP_Text statusText;
    public float travel = 1.65f;
    public bool IsOpen { get; private set; }
    Vector3 leftClosed, rightClosed;
    float openness;
    AudioSource audioSource;
    AudioClip motor;
    void Awake()
    {
        leftClosed = leftDoor.localPosition; rightClosed = rightDoor.localPosition;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1; audioSource.maxDistance = 18; audioSource.volume = .18f;
        int count = 22050;
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / 22050f;
            samples[i] = (Mathf.Sin(t * 240) + .35f * Mathf.Sin(t * 480)) * Mathf.Sin(Mathf.PI * t) * .22f;
        }
        motor = AudioClip.Create("Elevator motor", count, 1, 22050, false); motor.SetData(samples, 0);
    }
    void OnDestroy() { if (motor != null) Destroy(motor); }
    public bool Contains(Vector3 point)
    {
        var local = transform.InverseTransformPoint(point);
        return Mathf.Abs(local.x) < 2 && local.z < -.35f && local.z > -3.9f && local.y > 0 && local.y < 3;
    }
    public void SetOpen(bool open, bool immediate = false)
    {
        IsOpen = open;
        if (immediate) { openness = open ? 1 : 0; Pose(); }
        else if (audioSource != null) audioSource.PlayOneShot(motor);
    }
    void Pose()
    {
        float smooth = openness * openness * (3 - 2 * openness);
        leftDoor.localPosition = leftClosed + Vector3.left * travel * smooth;
        rightDoor.localPosition = rightClosed + Vector3.right * travel * smooth;
    }
    void Update()
    {
        openness = Mathf.MoveTowards(openness, IsOpen ? 1 : 0, Time.unscaledDeltaTime / .85f);
        Pose();
        var session = TowerSession.Instance;
        if (session == null || session.CurrentFloor == null || session.CurrentFloor.elevator != this) return;
        bool inside = Contains(session.player.transform.position);
        // The arrival cabin stays open until the player has left it.
        if (!inside && transform.InverseTransformPoint(session.player.transform.position).z > .8f && !session.IsTransitioning && IsOpen && !session.ExitUnlocked) SetOpen(false);
        if (session.ExitUnlocked && !session.IsTransitioning && !IsOpen) SetOpen(true);
        if (statusText != null)
            statusText.text = session.IsTransitioning ? "ASCENDIENDO" : session.ExitUnlocked ?
                "ASCENSOR LISTO\n<size=55%>ENTRA Y PULSA E EN EL PANEL</size>" :
                "PRUEBA EN CURSO\n<size=55%>" + GameManager.Instance.CompletedWaves + " / " + session.CurrentFloor.mandatoryWaves + " OLEADAS</size>";
    }
}

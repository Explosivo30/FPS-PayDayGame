using UnityEngine;

public class GunRecoil : MonoBehaviour
{
    public static GunRecoil Instance;

    private Vector2 currentRecoil;
    private Vector2 recoilVelocity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ApplyRecoil(RecoilData data)
    {
        float up = data.recoilKickUp;
        float side = Random.Range(-data.recoilKickSide, data.recoilKickSide);
        currentRecoil += new Vector2(side, up);
    }

    private void Update()
    {
        currentRecoil = Vector2.SmoothDamp(currentRecoil, Vector2.zero, ref recoilVelocity, 1f / 10f);
    }

    public Vector2 GetRecoilOffset()
    {
        return currentRecoil * Time.deltaTime * 10f;
    }
}

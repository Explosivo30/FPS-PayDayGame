using UnityEngine;

public class MachineGun : BaseGun
{
    public override bool IsAutomatic => true;

    private float lastShotTime = 0.0f;
    public override void Use()
    {
        if (Time.time - lastShotTime >= fireRate)
        {
            Debug.Log("Ratatatat!");
            lastShotTime = Time.time;
        }
    }

    public override void Reload() { currentAmmo = ammo; }


}

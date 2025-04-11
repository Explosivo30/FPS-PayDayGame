using UnityEngine;

public class Pistol : BaseGun
{
    public override void Use()
    {
        if (ammo <= 0)
        {
            Debug.Log("No ammo!");
            return;
        }

        ammo--;
        Debug.Log("Pistol fired! Damage: " + damage);
        // Add sound, muzzle flash, raycast etc.
    }

    public override void Reload()
    {
        ammo = 30;
        Debug.Log("Pistol reloaded.");
    }
}

using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [SerializeField] private List<BaseGun> weaponInventory; // Assigned via inspector
    private int currentWeaponIndex = 0;

    private IWeapon currentWeapon;

    void Start()
    {
        EquipWeapon(currentWeaponIndex);
    }

    void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            currentWeapon?.Fire();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentWeapon?.Reload();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            SwitchWeapon();
        }
    }

    private void EquipWeapon(int index)
    {
        if (index >= 0 && index < weaponInventory.Count)
        {
            foreach (var weapon in weaponInventory)
                weapon.gameObject.SetActive(false);

            weaponInventory[index].gameObject.SetActive(true);
            currentWeapon = weaponInventory[index];
        }
    }

    private void SwitchWeapon()
    {
        currentWeaponIndex = (currentWeaponIndex + 1) % weaponInventory.Count;
        EquipWeapon(currentWeaponIndex);
    }
}

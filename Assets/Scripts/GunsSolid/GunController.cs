using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [SerializeField] private List<MonoBehaviour> weaponObjects; // All must implement IWeapon
    private List<IWeapon> weaponInventory = new List<IWeapon>();
    private IWeapon currentWeapon;
    private int currentIndex = 0;

    void Start()
    {
        // Cast and store all IWeapon implementations
        foreach (var obj in weaponObjects)
        {
            if (obj is IWeapon weapon)
                weaponInventory.Add(weapon);
        }

        EquipWeapon(0);
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
            currentWeapon?.Use();

        if (Input.GetKeyDown(KeyCode.R) && currentWeapon is IReloadable reloadable)
            reloadable.Reload();

        if (Input.GetKeyDown(KeyCode.Q))
            SwitchWeapon();
    }

    void EquipWeapon(int index)
    {
        currentIndex = index;
        currentWeapon = weaponInventory[index];

        for (int i = 0; i < weaponObjects.Count; i++)
            weaponObjects[i].gameObject.SetActive(i == index);
    }

    void SwitchWeapon()
    {
        int next = (currentIndex + 1) % weaponInventory.Count;
        EquipWeapon(next);
    }
}

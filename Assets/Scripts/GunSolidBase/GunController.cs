using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GunController : MonoBehaviour
{
    [SerializeField] private List<MonoBehaviour> weaponObjects; // All must implement IWeapon
    private List<IWeapon> weaponInventory = new List<IWeapon>();
    [NonSerialized] public IWeapon currentWeapon;
    private int currentIndex = 0;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked; // Locks the cursor to the center
        Cursor.visible = false;
    }

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

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (currentWeapon.IsAutomatic)
        {
            if (Input.GetButton("Fire1"))
                currentWeapon?.Use();
        }
        else
        {
            if (Input.GetButtonDown("Fire1"))
                currentWeapon?.Use();
        }


        // Aiming
        if (currentWeapon is IAimable aimable)
        {
            if (Input.GetButtonDown("Fire2"))
                aimable.StartAiming();
            else if (Input.GetButtonUp("Fire2"))
                aimable.StopAiming();
        }

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

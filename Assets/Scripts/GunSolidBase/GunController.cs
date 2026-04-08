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

    private InputReader inputReader;
    private bool wasAttacking = false;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked; // Locks the cursor to the center
        Cursor.visible = false;
    }

    void Start()
    {
        // Try to find the InputReader on this GameObject or a parent (like the Player)
        inputReader = GetComponentInParent<InputReader>();

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

        if (currentWeapon != null)
        {
            // If we have an InputReader, check isAttacking. Otherwise fallback to false.
            bool currentAttacking = inputReader != null ? inputReader.isAttacking : false;

            if (currentWeapon.IsAutomatic)
            {
                if (currentAttacking)
                    currentWeapon.Use();
            }
            else
            {
                // For non-automatic weapons, only fire on the frame the button was pressed
                if (currentAttacking && !wasAttacking)
                    currentWeapon.Use();
            }

            wasAttacking = currentAttacking;
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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-60)]
public class GunController : MonoBehaviour
{
    [SerializeField] private List<MonoBehaviour> weaponObjects = new List<MonoBehaviour>();
    [NonSerialized] public IWeapon currentWeapon;
    public event Action<IWeapon> WeaponChanged;
    public IReadOnlyList<MonoBehaviour> Weapons => weaponObjects;
    public bool IsSwitching { get; private set; }
    public int CurrentIndex { get; private set; }
    private InputReader inputReader;
    private bool wasAttacking;
    private bool requireRelease;
    public void RequireTriggerRelease(){requireRelease=true;wasAttacking=true;}
    public void CancelForModal()
    {
        if(switchRoutine!=null)StopCoroutine(switchRoutine);
        switchRoutine=null;IsSwitching=false;
        foreach(var weapon in weaponObjects)
        {
            if(weapon is IAimable aim)aim.StopAiming();
            if(weapon is Knife knife)knife.CancelPendingStrike();
            weapon.GetComponent<WeaponAction>()?.Cancel();
        }
        if(worldCamera!=null)worldCamera.fieldOfView=normalFOV;
        if(weaponCamera!=null)weaponCamera.fieldOfView=normalFOV;
        RequireTriggerRelease();
    }
    private Coroutine switchRoutine;
    private Camera worldCamera, weaponCamera;
    private float normalFOV = 70f;
    public bool AutomatedInput { get; set; }

    private void Awake()
    {
        inputReader = GetComponentInParent<InputReader>();
        if(GetComponent<ImpactFeedbackPlayer>()==null)gameObject.AddComponent<ImpactFeedbackPlayer>();
        foreach (var cam in transform.root.GetComponentsInChildren<Camera>(true))
            if (cam.gameObject.layer == LayerMask.NameToLayer("Weapon")) weaponCamera = cam; else worldCamera = cam;
        if (worldCamera != null) normalFOV = worldCamera.fieldOfView;
    }
    private void Start()
    {
        weaponObjects.RemoveAll(w => w == null || !(w is IWeapon));
        if (weaponObjects.Count > 0) EquipImmediate(0);
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    private void Update()
    {
        if (WeaponAction.CombatPaused) { wasAttacking = inputReader != null && inputReader.isAttacking; return; }
        if (AutomatedInput) return;
        if(requireRelease)
        {
            if(Input.GetMouseButton(0)||Input.GetMouseButton(1)||(inputReader!=null&&inputReader.isAttacking))return;
            requireRelease=false;wasAttacking=false;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; wasAttacking = true; }
            return;
        }
        if (Input.GetKeyDown(KeyCode.Q)) SwitchWeapon();
        if (currentWeapon == null || IsSwitching) { wasAttacking = inputReader != null && inputReader.isAttacking; return; }
        if (currentWeapon is IAimable aimable)
        {
            if (Input.GetMouseButton(1)) aimable.StartAiming(); else aimable.StopAiming();
        }
        if (Input.GetKeyDown(KeyCode.R) && currentWeapon is IReloadable reloadable) reloadable.Reload();
        bool attacking = inputReader != null && inputReader.isAttacking;
        if (attacking && (currentWeapon.IsAutomatic || !wasAttacking)) currentWeapon.Use();
        wasAttacking = attacking;
    }
    private void LateUpdate()
    {
        bool aiming = !IsSwitching && currentWeapon is IAimable aimable && aimable.IsAiming;
        float target = aiming ? 52f : normalFOV;
        float blend = 1f - Mathf.Exp(-14f * Time.deltaTime);
        if (worldCamera != null) worldCamera.fieldOfView = Mathf.Lerp(worldCamera.fieldOfView, target, blend);
        if (weaponCamera != null) weaponCamera.fieldOfView = Mathf.Lerp(weaponCamera.fieldOfView, aiming ? 52f : normalFOV, blend);
    }
    public void SwitchWeapon() { SelectWeapon((CurrentIndex + 1) % Mathf.Max(1, weaponObjects.Count)); }
    public void SelectWeapon(int index)
    {
        if (IsSwitching || WeaponAction.CombatPaused || index < 0 || index >= weaponObjects.Count || index == CurrentIndex) return;
        switchRoutine = StartCoroutine(SwitchRoutine(index));
    }
    private IEnumerator SwitchRoutine(int index)
    {
        IsSwitching = true;
        if (currentWeapon is IAimable aimable) aimable.StopAiming();
        var old = weaponObjects[CurrentIndex].GetComponent<WeaponAction>();
        if (old != null) { old.BeginHolster(); yield return new WaitForSeconds(old.holsterDuration); }
        EquipImmediate(index);
        var next = weaponObjects[index].GetComponent<WeaponAction>();
        if (next != null) yield return new WaitForSeconds(next.drawDuration);
        IsSwitching = false; switchRoutine = null;
    }
    public void EquipImmediate(int index)
    {
        if (index < 0 || index >= weaponObjects.Count) return;
        if (currentWeapon is IAimable oldAim) oldAim.StopAiming();
        CurrentIndex = index;
        for (int i = 0; i < weaponObjects.Count; i++) weaponObjects[i].gameObject.SetActive(i == index);
        currentWeapon = (IWeapon)weaponObjects[index];
        weaponObjects[index].GetComponent<WeaponAction>()?.BeginDraw();
        wasAttacking = inputReader != null && inputReader.isAttacking;
        WeaponChanged?.Invoke(currentWeapon);
    }
    private void OnDisable()
    {
        if (switchRoutine != null) StopCoroutine(switchRoutine);
        IsSwitching = false;
        if (currentWeapon is IAimable aimable) aimable.StopAiming();
        if (worldCamera != null) worldCamera.fieldOfView = normalFOV;
        if (weaponCamera != null) weaponCamera.fieldOfView = normalFOV;
    }
}

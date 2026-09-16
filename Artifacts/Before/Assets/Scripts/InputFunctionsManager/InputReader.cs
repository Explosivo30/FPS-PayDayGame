using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour,InputSystem_Actions.IPlayerActions
{

    public Vector2 MovementValue { get; private set; }
    public Vector2 LookValue { get; private set; }

    public event Action JumpEvent;
    public event Action DodgeEvent;
    public event Action TargetEvent;
    public event Action InteractEvent;

    public bool isAttacking = false;
    public bool isCrouching = false;

    InputSystem_Actions controls;
    void Start()
    {
        
        controls = new InputSystem_Actions();
        controls.Player.SetCallbacks(this);

        controls.Player.Enable();
    }

    void OnDestroy()
    {
        controls.Player.Disable();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if(context.started) isAttacking = true;

        if(context.canceled) isAttacking = false;

    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.started) isCrouching = true;

        if (context.canceled) isCrouching = false;
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started) InteractEvent.Invoke();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if(context.started)
        {
            JumpEvent.Invoke();
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        LookValue = context.ReadValue<Vector2>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
       MovementValue = context.ReadValue<Vector2>();
    }

    public void OnNext(InputAction.CallbackContext context)
    {
        
    }

    public void OnPrevious(InputAction.CallbackContext context)
    {
        
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        
    }


}

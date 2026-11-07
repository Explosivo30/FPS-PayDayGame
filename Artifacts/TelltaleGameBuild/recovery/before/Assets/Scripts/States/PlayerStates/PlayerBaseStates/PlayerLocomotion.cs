using UnityEngine;

public partial class PlayerStateMachine
{
    [Header("Player response")]
    [Min(0)] public float coyoteTime=.10f,jumpBuffer=.12f;
    [Min(1)] public float braking=55,turnAcceleration=100;
    [Min(0)] public float slideCooldown=.9f;
    public float maxSlideSpeed=12;
    public bool IsCrouched=>targetHeight<originalHeight-.01f;
    public bool IsSliding=>slideRemaining>0;
    public float HorizontalSpeed=>new Vector2(PlayerVelocity.x,PlayerVelocity.z).magnitude;
    float motorClock,lastGrounded=-100,jumpQueuedUntil=-100,ignoreGroundUntil=-100;
    float slideRemaining,nextSlideAt,stepDistance,fallSpeed;
    bool crouchWasPressed,jumpConsumed;
    public void QueueJump()
    {
        if(IsDead||WeaponAction.CombatPaused)return;
        jumpQueuedUntil=motorClock+jumpBuffer;
    }
    void ResetMovement()
    {
        _grounded=false;_sliding=false;slideRemaining=0;stepDistance=fallSpeed=0;
        lastGrounded=jumpQueuedUntil=ignoreGroundUntil=-100;
        nextSlideAt=motorClock;crouchWasPressed=jumpConsumed=false;
    }
    public void TickLocomotion()
    {
        if(IsDead||WeaponAction.CombatPaused)
        { jumpQueuedUntil=-100;cameraTilt?.DoTilt(0);return; }
        PlayerLook();
        StepMovement(Time.deltaTime,GetInput(),controls!=null&&controls.isCrouching);
    }
    public void StepMovement(float dt,Vector2 input,bool crouch)
    {
        if(IsDead||WeaponAction.CombatPaused||dt<=0||!cc.enabled)return;
        dt=Mathf.Min(dt,.05f);motorClock+=dt;input=Vector2.ClampMagnitude(input,1);
        GroundDetection();
        if(Grounded)
        {
            lastGrounded=motorClock;
            if(PlayerVelocity.y<=0)jumpConsumed=false;
        }
        float speed=HorizontalSpeed;
        if(crouch&&!crouchWasPressed&&Grounded&&speed>=maxGroundSpeed*.72f&&motorClock>=nextSlideAt)
        {
            slideRemaining=slideDuration;nextSlideAt=motorClock+slideCooldown;
            var horizontal=new Vector3(PlayerVelocity.x,0,PlayerVelocity.z);
            horizontal=horizontal.normalized*Mathf.Min(maxSlideSpeed,horizontal.magnitude+Mathf.Min(slideBoost,3));
            PlayerVelocity.x=horizontal.x;PlayerVelocity.z=horizontal.z;
        }
        crouchWasPressed=crouch;
        if(slideRemaining>0)
        {
            slideRemaining=Mathf.Max(0,slideRemaining-dt);
            if(!crouch||!Grounded)slideRemaining=0;
        }
        if(crouch||IsSliding)SetCrouchedScale(true);
        else if(CanStandUp())SetCrouchedScale(false);
        if(jumpQueuedUntil>=motorClock&&(!jumpConsumed&&motorClock-lastGrounded<=coyoteTime)&&CanStandUp())
            PerformJump();
        var direction=(transform.forward*input.y+transform.right*input.x);
        var horizontalVelocity=new Vector3(PlayerVelocity.x,0,PlayerVelocity.z);
        if(IsSliding)
        {
            float slideSpeed=Mathf.Max(0,horizontalVelocity.magnitude-8f*dt);
            var steering=horizontalVelocity.normalized+direction*.7f*dt;
            horizontalVelocity=steering.normalized*slideSpeed;
        }
        else if(Grounded)
        {
            float desiredSpeed=IsCrouched?maxCrouchSpeed:maxGroundSpeed;
            Vector3 desired=direction*desiredSpeed;
            float acceleration=input.sqrMagnitude<.001f?braking:
                Vector3.Dot(horizontalVelocity,direction)<0?turnAcceleration:groundAcceleration;
            horizontalVelocity=Vector3.MoveTowards(horizontalVelocity,desired,acceleration*dt);
        }
        else if(input.sqrMagnitude>.001f)
        {
            // Retain take-off momentum, with bounded air steering and no diagonal speed exploit.
            float airLimit=Mathf.Max(maxGroundSpeed,maxAirSpeed);
            float preservedSpeed=Mathf.Min(maxSlideSpeed,Mathf.Max(airLimit,horizontalVelocity.magnitude));
            horizontalVelocity=Vector3.MoveTowards(horizontalVelocity,direction*preservedSpeed,airAcceleration*dt);
        }
        PlayerVelocity.x=horizontalVelocity.x;PlayerVelocity.z=horizontalVelocity.z;
        float previousY=PlayerVelocity.y;
        if(Grounded&&previousY<=0)PlayerVelocity.y=-2;
        else PlayerVelocity.y=Mathf.Max(-35,previousY-GravityForce*dt);
        fallSpeed=Mathf.Max(fallSpeed,-PlayerVelocity.y);
        UpdateHeight(dt);
        var displacement=PlayerVelocity*dt;
        if(!Grounded)displacement.y=(previousY+PlayerVelocity.y)*.5f*dt;
        var before=transform.position;
        var collisions=cc.Move(displacement);
        if((collisions&CollisionFlags.Above)!=0&&PlayerVelocity.y>0)PlayerVelocity.y=0;
        bool wasGrounded=Grounded;
        GroundDetection();
        if(Grounded&&PlayerVelocity.y<=0)
        {
            if(!wasGrounded&&fallSpeed>3) Landed?.Invoke(fallSpeed);
            fallSpeed=0;PlayerVelocity.y=-2;lastGrounded=motorClock;jumpConsumed=false;
        }
        if(Grounded&&!IsSliding)
        {
            stepDistance+=Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude;
            if(stepDistance>=(IsCrouched?2.5f:2.1f))
            { stepDistance=0;Stepped?.Invoke(); }
        }
        else stepDistance=0;
        cameraTilt?.DoTilt(-input.x*(IsSliding?1.4f:.65f));
    }
    public void GroundDetection()
    {
        _grounded=false;_sliding=false;_groundNormal=Vector3.up;
        if(!cc.enabled||motorClock<ignoreGroundUntil||PlayerVelocity.y>.05f)return;
        float radius=cc.radius*.9f;
        Vector3 feet=transform.TransformPoint(cc.center)-Vector3.up*(cc.height*.5f);
        Vector3 origin=feet+Vector3.up*(radius+.12f);
        if(Physics.SphereCast(origin,radius,Vector3.down,out var hit,.23f,GroundMask,QueryTriggerInteraction.Ignore))
        {
            _groundNormal=_slideNormal=hit.normal;
            _grounded=Vector3.Angle(hit.normal,Vector3.up)<=_maxAngle;
            _sliding=!_grounded;
        }
    }
    public void Jump()
    {
        if(IsDead||WeaponAction.CombatPaused||!CanStandUp())return;
        if(Grounded||(!jumpConsumed&&motorClock-lastGrounded<=coyoteTime))PerformJump();
    }
    void PerformJump()
    {
        PlayerVelocity.y=jumpForce;_grounded=false;jumpConsumed=true;
        jumpQueuedUntil=lastGrounded=-100;ignoreGroundUntil=motorClock+.08f;
        slideRemaining=0;SetCrouchedScale(false);fallSpeed=0;Jumped?.Invoke();
    }
    public void SetCrouchedScale(bool crouched)
    { targetHeight=crouched?Mathf.Max(cc.radius*2+.04f,originalHeight*crouchHeightMultiplier):originalHeight; }
    public bool CanStandUp()
    {
        if(cc.height>=originalHeight-.025f)return true;
        Vector3 feet=transform.TransformPoint(cc.center)-Vector3.up*(cc.height*.5f);
        float radius=cc.radius*.94f;
        Vector3 lower=feet+Vector3.up*(cc.height-radius+.025f);
        Vector3 upper=feet+Vector3.up*(originalHeight-radius);
        return !Physics.CheckCapsule(lower,upper,radius,GroundMask,QueryTriggerInteraction.Ignore);
    }
    void UpdateHeight(float dt)
    {
        cc.height=Mathf.Lerp(cc.height,targetHeight,1-Mathf.Exp(-heightTransitionSpeed*dt));
        if(Mathf.Abs(cc.height-targetHeight)<.001f)cc.height=targetHeight;
        // Move the capsule center, preserving its feet instead of lifting the whole player.
        cc.center=originalCenter-Vector3.up*((originalHeight-cc.height)*.5f);
        var local=headCam.localPosition;local.y=originalCamLocalY-(originalHeight-cc.height);
        headCam.localPosition=local;
    }
    // Compatibility with legacy movement states and external impulse callers.
    public void ApplyFriction(float amount)
    {
        float factor=Mathf.Exp(-Mathf.Max(0,amount)*Time.deltaTime);
        PlayerVelocity.x*=factor;PlayerVelocity.z*=factor;
    }
    public void Accelerate(Vector3 direction,float speed,float acceleration)
    {
        Vector3 horizontal=new Vector3(PlayerVelocity.x,0,PlayerVelocity.z);
        horizontal=Vector3.MoveTowards(horizontal,Vector3.ClampMagnitude(direction,1)*speed,acceleration*Time.deltaTime);
        PlayerVelocity.x=horizontal.x;PlayerVelocity.z=horizontal.z;
    }
    public void ApplyGravityCustom(){PlayerVelocity.y=Mathf.Max(-35,PlayerVelocity.y-GravityForce*Time.deltaTime);}
    public void MovePlayer(){if(!cc.enabled)return;UpdateHeight(Time.deltaTime);cc.Move(PlayerVelocity*Time.deltaTime);}
}

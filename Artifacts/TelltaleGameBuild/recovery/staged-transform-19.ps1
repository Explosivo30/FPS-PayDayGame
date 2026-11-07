$taskEncoding = [System.Text.UTF8Encoding]::new($false)
function Set-TaskFile([string]$path,[string]$content) { [IO.File]::WriteAllText((Join-Path 'D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged' $path),$content.Replace("`r`n","`n"),$taskEncoding) }
Set-TaskFile 'Assets/Scripts/AI/RangedRobotCombat.cs' @'
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public sealed class RangedRobotCombat : MonoBehaviour
{
    public const float TurnSpeed = 360f, AimTolerance = 5f;
    NormalEnemyStateMachine owner;
    EnemyPerception senses;
    EnemyNavigation navigation;
    RobotPresentation presentation;
    NavMeshAgent agent;
    LineRenderer tracer;
    float damage, accuracy, spread, cooldownDuration, warning, range, cooldown;
    int shotsSinceMove;
    bool navigationRotation;
    public bool IsAttacking { get; private set; }
    public bool IsAligning { get; private set; }
    public int ShotsFired { get; private set; }
    public Vector3 CommittedTarget { get; private set; }
    public Vector3 LastShotOrigin { get; private set; }
    public Vector3 LastShotDirection { get; private set; }
    public float LastShotAimError { get; private set; }

    public void Configure(float hitDamage, float precision, float spreadAngle, float interval, float anticipation, float attackRange)
    {
        owner = GetComponent<NormalEnemyStateMachine>(); senses = GetComponent<EnemyPerception>();
        navigation = GetComponent<EnemyNavigation>(); presentation = GetComponent<RobotPresentation>();
        agent = GetComponent<NavMeshAgent>(); tracer = GetComponent<LineRenderer>();
        navigationRotation = agent != null && agent.updateRotation;
        damage = hitDamage; accuracy = precision; spread = spreadAngle;
        cooldownDuration = interval; warning = anticipation; range = attackRange;
        cooldown = Random.Range(.6f, 1.5f);
    }
    public void Balance(float hitDamage, float precision) { damage = hitDamage; accuracy = precision; }
    public void Tick()
    {
        if (owner.IsDead || WeaponAction.CombatPaused) { navigation.Stop(); return; }
        if (IsAttacking) { Face(CommittedTarget); return; }
        senses.Tick(); cooldown -= Time.deltaTime;
        Vector3 target = senses.LastKnown + Vector3.up * .6f;
        presentation?.SetTarget(target);
        float distance = Vector3.Distance(transform.position, senses.LastKnown);
        bool canAttack = SquadManager.Instance == null || SquadManager.Instance.CanAttack(owner);
        if (senses.Visible && distance <= range && distance >= senses.Profile.retreatDistance && cooldown <= 0 && canAttack)
        {
            IsAligning = true; navigation.Stop(); Face(target);
            if (!Aligned(target) || !ClearShot(target)) return;
            if (SquadManager.Instance != null && !SquadManager.Instance.TryAttack(owner)) return;
            IsAligning = false; IsAttacking = true; CommittedTarget = target;
            StartCoroutine(Fire());
            return;
        }
        RestoreNavigationRotation();
        navigation.Tick(false, canAttack);
    }
    void Face(Vector3 point)
    {
        if (agent != null) agent.updateRotation = false;
        Vector3 direction = point - transform.position; direction.y = 0;
        if (direction.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), TurnSpeed * Time.deltaTime);
        presentation?.SetTarget(point);
        presentation?.UpdateAim();
    }
    bool Aligned(Vector3 point)
    {
        Vector3 flat = point - transform.position; flat.y = 0;
        return flat.sqrMagnitude > .0001f && Vector3.Angle(transform.forward, flat) <= AimTolerance
            && presentation != null && presentation.IsAimAligned(point, AimTolerance);
    }
    bool ClearShot(Vector3 point)
    {
        Vector3 origin = presentation != null ? presentation.MuzzlePosition : owner.AimPoint;
        Vector3 delta = point - origin;
        if (delta.sqrMagnitude < .001f || delta.magnitude > range) return false;
        return !Physics.Raycast(origin, delta.normalized, out var hit, delta.magnitude, owner.layerMask, QueryTriggerInteraction.Ignore)
            || hit.collider.GetComponentInParent<PlayerStateMachine>() != null;
    }
    IEnumerator CombatDelay(float seconds)
    {
        for (float elapsed = 0; elapsed < seconds;)
        {
            yield return null;
            if (!WeaponAction.CombatPaused) elapsed += Time.deltaTime;
        }
    }
    IEnumerator Fire()
    {
        try
        {
            var p = senses.Profile;
            float lead = owner.IsVeteran ? Mathf.Max(p.minimumRangedWarning, p.burstWarning) : Mathf.Max(p.minimumRangedWarning, warning);
            presentation.Telegraph(lead); GetComponent<EnemySignals>()?.Attack(owner.IsVeteran);
            yield return CombatDelay(lead);
            int count = owner.IsVeteran ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                while (WeaponAction.CombatPaused && !owner.IsDead) yield return null;
                if (owner.IsDead) yield break;
                // Reconcile the visible barrel after recoil/hit animation before testing or emitting a ray.
                presentation.UpdateAim();
                if (!Aligned(CommittedTarget) || !ClearShot(CommittedTarget)) yield break;
                Vector3 origin = presentation.MuzzlePosition;
                Vector3 direction = presentation.AimDirection;
                LastShotAimError = Vector3.Angle(direction, CommittedTarget - origin);
                float variance = (1 - accuracy) * spread;
                direction = Quaternion.AngleAxis(Random.Range(-variance, variance), presentation.muzzle.up)
                    * Quaternion.AngleAxis(Random.Range(-variance, variance), presentation.muzzle.right) * direction;
                Vector3 end = origin + direction * range;
                if (Physics.Raycast(origin, direction, out var hit, range, owner.layerMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    hit.collider.GetComponentInParent<PlayerStateMachine>()?.TakeDamage(damage, origin,
                        owner.IsVeteran ? "Ráfaga de centinela veterano" : "Disparo de robot centinela");
                }
                LastShotOrigin = origin; LastShotDirection = direction;
                ShotsFired++; shotsSinceMove++; presentation.Fire();
                if (tracer != null) { tracer.SetPosition(0, origin); tracer.SetPosition(1, end); tracer.enabled = true; }
                yield return CombatDelay(.07f);
                if (tracer != null) tracer.enabled = false;
                if (i + 1 < count) yield return CombatDelay(Mathf.Max(0, p.burstInterval - .07f));
            }
        }
        finally { FinishAttack(); }
    }
    void RestoreNavigationRotation()
    {
        IsAligning = false;
        if (agent != null) agent.updateRotation = navigationRotation;
    }
    void FinishAttack()
    {
        bool wasAttacking = IsAttacking;
        IsAttacking = false; RestoreNavigationRotation();
        if (tracer != null) tracer.enabled = false;
        if (owner != null) SquadManager.Instance?.ReleaseAttack(owner);
        if (!wasAttacking) return;
        cooldown = cooldownDuration;
        if (owner != null && !owner.IsDead && (owner.IsVeteran || shotsSinceMove >= 2))
        { shotsSinceMove = 0; navigation.Reposition(); }
    }
    public void Cancel() { StopAllCoroutines(); FinishAttack(); }
    void OnDisable() { Cancel(); }
}
'@
$taskPath = 'Assets/Scripts/WeaponFeel/RobotPresentation.cs'
$taskText = [IO.File]::ReadAllText((Join-Path 'D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged' $taskPath)).Replace("`r`n","`n")
$taskText = $taskText.Replace('    public Vector3 MuzzlePosition=>muzzle!=null?muzzle.position:transform.position+Vector3.up;',@'
    public Vector3 MuzzlePosition=>muzzle!=null?muzzle.position:transform.position+Vector3.up;
    public Vector3 AimDirection=>muzzle!=null?muzzle.forward:transform.forward;
    public Vector3 AimTarget=>target;
    public bool IsAimAligned(Vector3 point,float tolerance)=>muzzle!=null&&Vector3.Angle(AimDirection,point-MuzzlePosition)<=tolerance;

    // Imported joints have arbitrary axes. A normalized parent lets the physical barrel,
    // muzzle flash and shot ray share the same forward direction without editing the mesh.
    public void ConfigureRangedRig()
    {
        if(turret==null||muzzle==null||turret.name=="Ranged aim pivot")return;
        var imported=turret;
        var pivot=new GameObject("Ranged aim pivot").transform;
        pivot.SetParent(visual,false);pivot.position=imported.position;pivot.rotation=transform.rotation;
        imported.SetParent(pivot,true);
        muzzle.SetParent(pivot,true);muzzle.rotation=transform.rotation;
        turret=pivot;
    }
    public void UpdateAim()
    {
        if(turret==null||muzzle==null||deathTime>=0||WeaponAction.CombatPaused)return;
        // Solve muzzle parallax as well as pitch. Limit yaw until the chassis has turned.
        for(int i=0;i<3;i++)
        {
            Vector3 direction=target-MuzzlePosition;
            if(direction.sqrMagnitude<.0001f)return;
            var desired=Quaternion.FromToRotation(muzzle.forward,direction.normalized)*turret.rotation;
            Vector3 forward=Quaternion.Inverse(transform.rotation)*(desired*Vector3.forward);
            float yaw=Mathf.Clamp(Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg,-30,30);
            float pitch=Mathf.Clamp(-Mathf.Atan2(forward.y,new Vector2(forward.x,forward.z).magnitude)*Mathf.Rad2Deg,-35,35);
            turret.rotation=transform.rotation*Quaternion.Euler(pitch,yaw,0);
        }
    }
'@)
$taskText = $taskText.Replace('        home=visual.localPosition;rotation=moveRotation=visual.localRotation;', '        ConfigureRangedRig();' + "`n" + '        home=visual.localPosition;rotation=moveRotation=visual.localRotation;')
$taskOld = @'
            if(turret!=null)
            {
                Vector3 direction=target-MuzzlePosition;
                float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                turret.localRotation=turretRotation*Quaternion.Euler(Mathf.Clamp(pitch,-35,35)-firePulse*8,0,0);
            }
'@
if (!$taskText.Contains($taskOld)) { throw 'Aim block not found' }
$taskText = $taskText.Replace($taskOld,'            UpdateAim();')
Set-TaskFile $taskPath $taskText
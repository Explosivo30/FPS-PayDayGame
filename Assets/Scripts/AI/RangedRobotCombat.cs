using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum RangedAttackPhase { Idle, Tracking, Locked, Volley, Laser, Recovery }

public sealed class RangedRobotCombat : MonoBehaviour
{
    public const float TurnSpeed = 360f, AimTolerance = 5f;
    public const float ProjectileSpeed = 12f, ProjectileRadius = .12f, SalvoSpacing = .16f;
    public const float LaserTrackingSeconds = .5f, LaserLockSeconds = .5f;
    public const float RepositionSeconds = .65f;
    NormalEnemyStateMachine owner;
    EnemyPerception senses;
    EnemyNavigation navigation;
    RobotPresentation presentation;
    NavMeshAgent agent;
    LineRenderer tracer;
    float damage, accuracy, spread, cooldownDuration, warning, range, cooldown;
    int shotsSinceMove;
    bool navigationRotation;
    CombatHazardBudget.Lease primaryLease;
    EnemyProjectile.Volley volley;
    Color tracerStart, tracerEnd;
    float tracerStartWidth, tracerEndWidth;
    Vector3 laserHitPoint;
    public bool IsAttacking { get; private set; }
    public bool IsAligning { get; private set; }
    public RangedAttackPhase Phase { get; private set; }
    public bool IsTargetLocked => Phase == RangedAttackPhase.Locked || Phase == RangedAttackPhase.Volley || Phase == RangedAttackPhase.Laser;
    public int ProjectilesLaunched { get; private set; }
    public int LaserShotsFired { get; private set; }
    public EnemyProjectile.Volley ActiveVolley => volley;
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
        if (tracer != null)
        {
            tracerStart = tracer.startColor; tracerEnd = tracer.endColor;
            tracerStartWidth = tracer.startWidth; tracerEndWidth = tracer.endWidth;
        }
        navigationRotation = agent != null && agent.updateRotation;
        damage = hitDamage; accuracy = precision; spread = spreadAngle;
        cooldownDuration = interval; warning = anticipation; range = attackRange;
        cooldown = Random.Range(.6f, 1.5f);
    }
    public void Balance(float hitDamage, float precision) { damage = hitDamage; accuracy = precision; }
    public void SetRange(float value) { range = Mathf.Max(1, value); }
    public void Tick()
    {
        if (owner.IsDead || WeaponAction.CombatPaused) { navigation.Stop(); return; }
        if (IsAttacking)
        {
            if (owner.IsVeteran && (primaryLease == null || !primaryLease.IsValid)) { Cancel(); return; }
            if (Phase == RangedAttackPhase.Tracking)
            { senses.Tick(); if (senses.Visible) CommittedTarget = senses.LastKnown + Vector3.up * .6f; }
            Face(CommittedTarget);
            // A petal closing during lock cancels the whole warning. Reopening cover
            // must not expose the player to a nearly-expired, previously hidden laser.
            if (owner.IsVeteran && Phase == RangedAttackPhase.Locked && !ClearShot(CommittedTarget)) { Cancel(); return; }
            if (owner.IsVeteran && (Phase == RangedAttackPhase.Tracking || Phase == RangedAttackPhase.Locked)) ShowLaserLine(false);
            return;
        }
        senses.Tick(); cooldown -= Time.deltaTime;
        Vector3 target = senses.LastKnown + Vector3.up * .6f;
        presentation?.SetTarget(target);
        float distance = Vector3.Distance(transform.position, senses.LastKnown);
        bool canAttack = SquadManager.Instance == null || SquadManager.Instance.CanAttack(owner);
        if (senses.Visible && distance <= range && distance >= senses.Profile.retreatDistance && cooldown <= 0 && canAttack)
        {
            IsAligning = true; navigation.Stop(); Face(target);
            if (!Aligned(target)) return;
            if (!ClearShot(target))
            {
                RestoreNavigationRotation(); navigation.Reposition(); navigation.Tick(false, canAttack);
                cooldown = .25f; return;
            }
            CombatHazardBudget.Lease reservation;
            bool reserved = owner.IsVeteran ? CombatHazardBudget.TryReservePrimary(owner, out reservation)
                : CombatHazardBudget.TryReserveSalvo(owner, out reservation);
            if (!reserved) { RestoreNavigationRotation(); navigation.Tick(false, canAttack); return; }
            if (SquadManager.Instance != null && !SquadManager.Instance.TryAttack(owner)) { reservation.Dispose(); return; }
            if (owner.IsVeteran) primaryLease = reservation;
            else volley = new EnemyProjectile.Volley(reservation);
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
        // A long barrel can protrude through nearby cover. Its tip must also be reachable
        // from the chassis, otherwise a ray starting outside the wall would bypass it.
        if (Physics.Linecast(owner.AimPoint, origin, out var obstruction, owner.layerMask, QueryTriggerInteraction.Ignore)
            && obstruction.collider.GetComponentInParent<PlayerStateMachine>() == null && obstruction.collider.GetComponentInParent<MutationObject>()==null) return false;
        Vector3 delta = point - origin;
        if (delta.sqrMagnitude < .001f || delta.magnitude > range) return false;
        return !MutationObject.EnemyRay(origin, delta.normalized, out var hit, delta.magnitude, owner.layerMask)
            || hit.collider.GetComponentInParent<PlayerStateMachine>() != null || hit.collider.GetComponentInParent<MutationObject>()!=null;
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
            GetComponent<EnemySignals>()?.Attack(owner.IsVeteran);
            if (owner.IsVeteran)
            {
                Phase = RangedAttackPhase.Tracking; presentation.Telegraph(LaserTrackingSeconds + LaserLockSeconds);
                ShowLaserLine(false); yield return CombatDelay(LaserTrackingSeconds);
                if (owner.IsDead || primaryLease == null || !primaryLease.IsValid) yield break;
                // Tracking ends here. Every later frame aims at this world-space point.
                Phase = RangedAttackPhase.Locked; ShowLaserLine(false);
                yield return CombatDelay(LaserLockSeconds);
                presentation.UpdateAim();
                if (owner.IsDead || !primaryLease.IsValid || !Aligned(CommittedTarget) || !ClearShot(CommittedTarget)) yield break;
                Phase = RangedAttackPhase.Laser;
                Vector3 origin = presentation.MuzzlePosition, direction = presentation.AimDirection;
                LastShotAimError = Vector3.Angle(direction, CommittedTarget - origin);
                laserHitPoint = origin + direction * range;
                if (MutationObject.EnemyRay(origin, direction, out var hit, range, owner.layerMask))
                {
                    laserHitPoint = hit.point;
                    var defense=hit.collider.GetComponentInParent<MutationObject>();
                    if(defense==null||!defense.CaptureEnemyShot())hit.collider.GetComponentInParent<PlayerStateMachine>()?.TakeDamage(damage, origin,
                        "Láser fijado de Aguja");
                }
                LastShotOrigin = origin; LastShotDirection = direction; ShotsFired++; LaserShotsFired++;
                presentation.Fire(); ShowLaserLine(true); yield return CombatDelay(.1f);
                if (tracer != null) tracer.enabled = false;
            }
            else
            {
                Phase = RangedAttackPhase.Locked;
                float lead = Mathf.Max(.45f, Mathf.Max(senses.Profile.minimumRangedWarning, warning));
                presentation.Telegraph(lead); yield return CombatDelay(lead);
                Phase = RangedAttackPhase.Volley;
                for (int i = 0; i < 3; i++)
                {
                    if (owner.IsDead || volley == null || !volley.IsValid) yield break;
                    presentation.UpdateAim();
                    if (!Aligned(CommittedTarget) || !ClearShot(CommittedTarget)) yield break;
                    Vector3 origin = presentation.MuzzlePosition;
                    Vector3 direction = Quaternion.AngleAxis((i - 1) * 6f, Vector3.up) * presentation.AimDirection;
                    var bullet = EnemyProjectile.Launch(owner, origin, direction, ProjectileSpeed, damage,
                        range / ProjectileSpeed, ProjectileRadius, owner.layerMask, volley, tracer != null ? tracer.sharedMaterial : null);
                    if (bullet == null) yield break;
                    LastShotOrigin = origin; LastShotDirection = direction;
                    LastShotAimError = Vector3.Angle(direction, CommittedTarget - origin);
                    ShotsFired++; ProjectilesLaunched++; shotsSinceMove++; presentation.Fire();
                    if (i < 2) yield return CombatDelay(SalvoSpacing);
                }
                volley.FinishEmission();
            }
            Phase = RangedAttackPhase.Recovery;
            yield return CombatDelay(owner.IsVeteran ? 1.5f : 1.2f);
        }
        finally { FinishAttack(); }
    }
    void ShowLaserLine(bool fired)
    {
        if (tracer == null || presentation == null) return;
        Vector3 origin = fired ? LastShotOrigin : presentation.MuzzlePosition, direction = presentation.AimDirection;
        // Capturing the third shot saturates the coil. Draw this shot's actual hit,
        // rather than recasting against the now-transparent saturated defense.
        Vector3 end = fired ? laserHitPoint : origin + direction * range;
        if (!fired && MutationObject.EnemyRay(origin, direction, out var hit, range, owner.layerMask)) end = hit.point;
        tracer.positionCount = 2; tracer.useWorldSpace = true;
        tracer.SetPosition(0, origin); tracer.SetPosition(1, end);
        bool locked = Phase != RangedAttackPhase.Tracking;
        tracer.startWidth = tracer.endWidth = fired ? .12f : locked ? .055f : .018f;
        tracer.startColor = tracer.endColor = fired ? Color.white : locked ? new Color(1, .15f, .02f) : new Color(1, .65f, .05f);
        tracer.enabled = true;
    }
    void RestoreNavigationRotation()
    {
        IsAligning = false;
        if (agent != null) agent.updateRotation = navigationRotation;
    }
    void FinishAttack()
    {
        bool wasAttacking = IsAttacking;
        IsAttacking = false; Phase = RangedAttackPhase.Idle; RestoreNavigationRotation();
        if (tracer != null)
        {
            tracer.enabled = false; tracer.startColor = tracerStart; tracer.endColor = tracerEnd;
            tracer.startWidth = tracerStartWidth; tracer.endWidth = tracerEndWidth;
        }
        primaryLease?.Dispose(); primaryLease = null;
        volley?.FinishEmission();
        if (owner != null) SquadManager.Instance?.ReleaseAttack(owner);
        if (!wasAttacking) return;
        cooldown = 0;
        if (owner != null && !owner.IsDead && (owner.IsVeteran || shotsSinceMove >= 2))
        {
            // Give navigation an exclusive movement window after the full visible
            // recovery, before aim can stop its forced-move request again.
            shotsSinceMove = 0; navigation.Reposition(); cooldown = RepositionSeconds;
        }
    }
    public void Cancel()
    {
        StopAllCoroutines(); volley?.Cancel(); EnemyProjectile.CancelOwned(owner); FinishAttack();
        cooldown = Mathf.Max(.25f, cooldownDuration);
    }
    void OnDisable() { Cancel(); }
}
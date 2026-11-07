#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public partial class AIPlaytest
{
    IEnumerator Polish()
    {
        var profile = session.Threat.Profile;
        foreach (int wave in new[] { 1, 3, 6, 9 })
        {
            var plan = new WaveThreat(profile, wave, 100, 10);
            foreach (bool melee in new[] { false, true }) foreach (bool veteran in new[] { false, true })
            {
                var enemy = Spawn(melee, 8, false, veteran);
                enemy.ConfigureDifficulty(plan.Health * (veteran ? profile.veteranHealth : 1), plan.Damage, plan.Accuracy);
                float expected = (melee ? 150 : 100) * (veteran ? 1.2f : 1) * (1 + .06f * (wave - 1));
                Check(Mathf.Abs(enemy.Health - expected) < .01f, "Polish wave " + wave + " health " + (melee ? "melee" : "ranged") + (veteran ? " veteran" : " normal"));
            }
            Clear(); yield return null;
        }
        for (int i = 0; i < 4; i++)
        {
            var enemy = Spawn(i >= 2, 12, false, i % 2 == 1);
            enemy.transform.position += Vector3.right * (-4.8f + i * 3.2f);
            enemy.GetComponent<RobotPresentation>().enabled = false;
            Check(enemy.GetComponent<EnemySignals>().HasVeteranModel == (i % 2 == 1), "Authored model only on veteran " + i);
            if (i % 2 == 1)
            {
                Check(enemy.GetComponent<EnemySignals>().VeteranParts >= 5 && enemy.GetComponentsInChildren<Light>().Length == 0, "Veteran has complete authored mesh without dynamic lights " + i);
                var authored=enemy.GetComponentInChildren<VeteranRig>();
                enemy.ConfigureVeteran(true);
                Check(enemy.GetComponentsInChildren<VeteranRig>().Length==1 && authored.modelName==(i>=2?"Ariete":"Aguja"), "Correct chosen model installed once " + i);
                var surface=authored.GetComponentsInChildren<MeshCollider>().First(c=>c.name==(i>=2?"Forearm_R_Ivory":"Body_Ivory"));
                Physics.SyncTransforms();
                var ray=new Ray(surface.bounds.center+enemy.transform.forward*5,-enemy.transform.forward);
                bool hit=surface.Raycast(ray,out var contact,10);
                Check(hit && contact.collider.GetComponentInParent<IDamageable>()==enemy, "Authored armor receives real weapon raycasts " + i);
                if (i >= 2)
                {
                    var pose = enemy.GetComponent<MeleeRobotPresentation>();
                    var rig=enemy.GetComponentInChildren<VeteranRig>();
                    Check(pose.leftForearm==rig.leftForearm && pose.rightForearm==rig.rightForearm, "Ariete animated forearms use authored model");
                    Check(pose.fistTrail.sharedMaterial!=null && pose.fistTrail.sharedMaterial.shader.isSupported, "Ariete animated fist trail retains a valid material");
                }
            }
        }
        yield return null;
        Check(fixtures.All(e => e.GetComponentsInChildren<Collider>().Count(c => c.enabled) == (e.IsVeteran?e.GetComponentsInChildren<MeshCollider>().Length:1)), "Veteran hit surfaces follow new geometry; normal capsule unchanged");
        Aim(new Vector3(0, 1.1f, -4)); yield return new WaitForSeconds(.2f); Capture("variants-front.png");
        foreach (var enemy in fixtures) enemy.transform.rotation = Quaternion.identity;
        yield return new WaitForSeconds(.2f); Capture("variants-back.png");
        Clear(); yield return null;
        var ariete=Spawn(true,4.8f,false,true);
        var aguja=Spawn(false,4.8f,false,true);
        ariete.transform.position+=Vector3.left*1.5f;aguja.transform.position+=Vector3.right*1.5f;
        foreach(var hero in fixtures)hero.GetComponent<RobotPresentation>().SetTarget(player.transform.position+Vector3.up*.35f);
        Aim(new Vector3(0,1.1f,-11.2f));
        yield return new WaitForSeconds(.2f);Capture("chosen-models-front.png");
        foreach(var hero in fixtures){hero.transform.rotation=Quaternion.identity;hero.GetComponent<RobotPresentation>().enabled=false;}
        yield return new WaitForSeconds(.2f);Capture("chosen-models-back.png");
        Clear();yield return null;
        foreach (bool veteran in new[] { false, true }) foreach (float yaw in new[] { 180f, 90f, 0f })
            yield return FacingCase(veteran, yaw, 0);
        foreach (float height in new[] { -1.2f, 3f }) yield return FacingCase(true, 90, height);

        // A new obstacle during the committed windup cancels the shot and releases its slot.
        player.TeleportTo(home, Quaternion.identity);
        NormalEnemyStateMachine ranged;
        foreach (float offset in new[] { 4f, .7f })
        {
            ranged = Spawn(false, 8, true, true); Set(ranged.Ranged, "cooldown", 0f);
            yield return Until(() => ranged.Ranged.IsAttacking, 4, "Obstacle fixture starts windup " + offset);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 3;
            wall.transform.position = ranged.transform.position + new Vector3(0, 1.5f, -offset);
            wall.transform.localScale = new Vector3(3, 5, .25f); Physics.SyncTransforms();
            yield return Until(() => !ranged.Ranged.IsAttacking, 3, "Obstacle cancels blocked burst " + offset);
            Check(ranged.Ranged.ShotsFired == 0 && SquadManager.Instance.ActiveAttackers == 0,
                "Blocked shot releases attack budget, including cover behind muzzle " + offset);
            Destroy(wall); Clear(); yield return null;
        }
        ranged = Spawn(false, 8, true); Set(ranged.Ranged, "cooldown", 0f);
        yield return Until(() => ranged.Ranged.IsAttacking, 4, "Misalignment fixture starts windup");
        ranged.enabled = false; ranged.transform.rotation *= Quaternion.Euler(0, 180, 0);
        yield return Until(() => !ranged.Ranged.IsAttacking, 3, "Misaligned barrel cancels attack");
        Check(ranged.Ranged.ShotsFired == 0 && SquadManager.Instance.ActiveAttackers == 0, "No backwards shot or leaked permit after forced rotation");
        Clear(); yield return null;

        var brawler = Spawn(true, 2.7f, true, true);
        yield return Until(() => brawler.GetComponent<MeleeRobotCombat>().Phase == MeleeRobotPhase.Windup, 4, "Ariete winds up");
        Aim(brawler.AimPoint); yield return new WaitForSeconds(.15f); Capture("ariete-windup.png");
        yield return Until(() => brawler.GetComponent<MeleeRobotCombat>().Phase == MeleeRobotPhase.Strike, 3, "Ariete extends articulated hammer fist");
        yield return new WaitForSeconds(.075f); Capture("ariete-strike.png");
        yield return Until(() => brawler.GetComponent<MeleeRobotCombat>().Phase == MeleeRobotPhase.Recover, 3, "Ariete recovers after punch");
        Capture("ariete-recovery.png");
        Clear(); yield return null; player.TeleportTo(home, Quaternion.identity);
    }
    IEnumerator FacingCase(bool veteran, float yaw, float height)
    {
        player.TeleportTo(home + Vector3.up * height, Quaternion.identity);
        var enemy = Spawn(false, 8, false, veteran);
        enemy.transform.rotation = Quaternion.Euler(0, yaw, 0);
        enemy.Ranged.Balance(2, 1); Set(enemy.Ranged, "cooldown", 0f); Physics.SyncTransforms();
        enemy.Ranged.Tick();
        if (yaw != 180) Check(enemy.Ranged.ShotsFired == 0 && !enemy.Ranged.IsAttacking && enemy.Ranged.IsAligning, "Turns before warning from yaw " + yaw);
        float end = Time.realtimeSinceStartup + 4;
        while (!enemy.Ranged.IsAttacking && Time.realtimeSinceStartup < end) { enemy.Ranged.Tick(); yield return null; }
        Check(enemy.Ranged.IsAttacking, "Aligned windup " + veteran + "/" + yaw + "/" + height);
        var committed = enemy.Ranged.CommittedTarget;
        var rotation = enemy.transform.rotation;
        using (var pause = RunPause.Acquire(false))
        {
            enemy.Ranged.Tick(); yield return new WaitForSecondsRealtime(.1f);
            Check(enemy.Ranged.ShotsFired == 0 && Quaternion.Angle(rotation, enemy.transform.rotation) < .01f, "Pause freezes turning and shot " + veteran + "/" + yaw + "/" + height);
        }
        int count = veteran ? 1 : 3;
        end = Time.realtimeSinceStartup + 3;
        while (enemy.Ranged.ShotsFired < count && Time.realtimeSinceStartup < end) { enemy.Ranged.Tick(); yield return null; }
        var presentation = enemy.GetComponent<RobotPresentation>();
        // Remove the final salvo bullet's deliberate +6-degree fan before checking
        // the same barrel-to-target tolerance used by the precision laser.
        Vector3 barrelDirection=Quaternion.AngleAxis(veteran?0:-6,Vector3.up)*enemy.Ranged.LastShotDirection;
        float barrelError=Vector3.Angle(barrelDirection,committed-enemy.Ranged.LastShotOrigin);
        Check(enemy.Ranged.ShotsFired == count && barrelError < 5,
            "Aligned shot count and barrel direction " + veteran + "/" + yaw + "/" + height + " error=" + enemy.Ranged.LastShotAimError);
        Check(enemy.Ranged.CommittedTarget == committed && barrelError < 5,
            "Ray uses committed target and muzzle " + veteran + "/" + yaw + "/" + height);
        Check(presentation.muzzle.IsChildOf(presentation.turret) && presentation.muzzleFlash.transform.IsChildOf(presentation.muzzle), "Muzzle and flash follow gun joint");
        var barrel = presentation.turret.GetComponentsInChildren<MeshFilter>().First(m => m.name == (enemy.IsVeteran?"Gun_Graphite":"MachineGunRobot_Gun"));
        var bounds = barrel.sharedMesh.bounds;
        Vector3 axis = bounds.size.x > bounds.size.y && bounds.size.x > bounds.size.z ? Vector3.right : bounds.size.y > bounds.size.z ? Vector3.up : Vector3.forward;
        Vector3 barrelAxis = barrel.transform.TransformVector(axis).normalized;
        if (Vector3.Dot(barrelAxis, presentation.AimDirection) < 0) barrelAxis = -barrelAxis;
        Check(Vector3.Angle(barrelAxis, presentation.AimDirection) < .1f, "Muzzle follows physical barrel mesh axis");
        Vector3 localMuzzle = barrel.transform.InverseTransformPoint(presentation.MuzzlePosition);
        Check(Mathf.Abs(Vector3.Dot(localMuzzle - bounds.center, axis)) >= Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))) - .02f,
            "Shot originates at end of barrel rather than body");
        if (veteran && yaw == 90 && height == 0) { Aim(enemy.AimPoint); Capture("aguja-shot.png"); }
        Clear(); yield return null; player.TeleportTo(home, Quaternion.identity);
    }
}
#endif

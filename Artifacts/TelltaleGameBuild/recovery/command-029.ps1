Stop-Process -Id 12492 -ErrorAction SilentlyContinue
$taskPath='D:\Unity\FPS-PayDayGame\Assets\Scripts\AI\RangedRobotCombat.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskOld='        Vector3 delta = point - origin;'
$taskNew=@'
        // A long barrel can protrude through nearby cover. Its tip must also be reachable
        // from the chassis, otherwise a ray starting outside the wall would bypass it.
        if (Physics.Linecast(owner.AimPoint, origin, out var obstruction, owner.layerMask, QueryTriggerInteraction.Ignore)
            && obstruction.collider.GetComponentInParent<PlayerStateMachine>() == null) return false;
        Vector3 delta = point - origin;
'@
$taskText=$taskText.Replace($taskOld,$taskNew)
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskPath='D:\Unity\FPS-PayDayGame\Assets\Scripts\Diagnostics\AIPlaytest.Polish.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskStart=$taskText.IndexOf('        var ranged = Spawn(false, 8, true, true);')
$taskEnd=$taskText.IndexOf('        ranged = Spawn(false, 8, true);',$taskStart)
$taskNew=@'
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

'@
$taskText=$taskText.Substring(0,$taskStart)+$taskNew+$taskText.Substring($taskEnd)
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe' -ArgumentList @('-batchmode','-projectPath','"D:\Unity\FPS-PayDayGame"','-executeMethod','EnemyPolishBuild.Play','-ai-qa','-ai-output','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\EditorAI"','-player-profile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai-profile.json"','-logFile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai.log"') -WindowStyle Hidden -PassThru
$taskScript=[IO.File]::ReadAllText('D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\verify.ps1').Replace('Get-Process -Id 30668',('Get-Process -Id '+$taskProcess.Id))
$taskScript=[regex]::Replace($taskScript,'(?m)^    Invoke-TaskUnity ''editor-(melee|feel)''.*\r?\n','')
[IO.File]::WriteAllText('D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\verify-final.ps1',$taskScript,[Text.UTF8Encoding]::new($false))
$taskRunner=Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\verify-final.ps1"') -WindowStyle Hidden -PassThru
Write-Output "Cover regression AI: $($taskProcess.Id); runner: $($taskRunner.Id)"
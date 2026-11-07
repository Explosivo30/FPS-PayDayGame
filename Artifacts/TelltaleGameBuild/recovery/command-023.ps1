$taskEncoding=[Text.UTF8Encoding]::new($false)
$taskPath='D:\Unity\FPS-PayDayGame\Assets\Scripts\AI\EnemySignals.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskText=$taskText.Replace('float x = side * .32f;','float x = side * .48f;')
$taskText=$taskText.Replace('new Vector3(x, 1.35f, -.32f), new Vector3(.25f, .88f, .25f), plate, PrimitiveType.Cube','new Vector3(x, 1.90f, -.32f), new Vector3(.30f, .60f, .30f), plate, PrimitiveType.Cylinder')
$taskText=$taskText.Replace('new Vector3(x, 1.35f, -.46f), new Vector3(.08f, .70f, .06f)','new Vector3(x, 1.90f, -.49f), new Vector3(.08f, 1.03f, .06f)')
$taskText=$taskText.Replace('new Vector3(x, 1.13f + i * .23f, -.32f), .18f','new Vector3(x, 1.50f + i * .35f, -.32f), .21f')
$taskText=$taskText.Replace('new Vector3(x, 1.84f, -.32f)','new Vector3(x, 2.58f, -.32f)')
$taskText=$taskText.Replace('new Vector3(x, 1.04f, -.30f)','new Vector3(x, 1.30f, -.30f)')
$taskText=$taskText.Replace('new Vector3(0, 1.28f, .35f), new Vector3(.60f, .19f, .13f)','new Vector3(0, 1.30f, .88f), new Vector3(.60f, .16f, .10f)')
$taskText=$taskText.Replace('new Vector3(0, 1.28f, .43f), new Vector3(.48f, .065f, .025f)','new Vector3(0, 1.30f, .94f), new Vector3(.48f, .065f, .025f)')
[IO.File]::WriteAllText($taskPath,$taskText,$taskEncoding)
$taskPath='D:\Unity\FPS-PayDayGame\Assets\Scripts\Diagnostics\AIPlaytest.Polish.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskNeedle='        Check(presentation.muzzle.IsChildOf(presentation.turret) && presentation.muzzleFlash.transform.IsChildOf(presentation.muzzle), "Muzzle and flash follow gun joint");'
$taskExtra=@'
        var barrel = presentation.turret.GetComponentsInChildren<MeshFilter>().First(m => m.name == "MachineGunRobot_Gun");
        var bounds = barrel.sharedMesh.bounds;
        Vector3 axis = bounds.size.x > bounds.size.y && bounds.size.x > bounds.size.z ? Vector3.right : bounds.size.y > bounds.size.z ? Vector3.up : Vector3.forward;
        Vector3 barrelAxis = barrel.transform.TransformVector(axis).normalized;
        if (Vector3.Dot(barrelAxis, presentation.AimDirection) < 0) barrelAxis = -barrelAxis;
        Check(Vector3.Angle(barrelAxis, presentation.AimDirection) < .1f, "Muzzle follows physical barrel mesh axis");
        Vector3 localMuzzle = barrel.transform.InverseTransformPoint(presentation.MuzzlePosition);
        Check(Mathf.Abs(Vector3.Dot(localMuzzle - bounds.center, axis)) >= Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))) - .02f,
            "Shot originates at end of barrel rather than body");
'@
$taskText=$taskText.Replace($taskNeedle,$taskNeedle+"`n"+$taskExtra)
[IO.File]::WriteAllText($taskPath,$taskText,$taskEncoding)
$taskProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe' -ArgumentList @('-batchmode','-projectPath','"D:\Unity\FPS-PayDayGame"','-executeMethod','EnemyPolishBuild.Play','-ai-qa','-ai-output','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\EditorAI"','-player-profile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai-profile.json"','-logFile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai.log"') -WindowStyle Hidden -PassThru
Write-Output "Final AI validation: $($taskProcess.Id)"
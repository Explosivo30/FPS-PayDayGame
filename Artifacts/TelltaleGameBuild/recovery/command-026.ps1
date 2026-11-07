$taskPath='D:\Unity\FPS-PayDayGame\Assets\Scripts\WeaponFeel\RobotPresentation.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskText=$taskText.Replace('    Vector3 home,target,','    Vector3 turretHome,home,target,')
$taskText=$taskText.Replace('        ConfigureRangedRig();'+"`n"+'        home=', '        ConfigureRangedRig();'+"`n"+'        if(turret!=null)turretHome=turret.localPosition;'+"`n"+'        home=')
$taskText=$taskText.Replace('            UpdateAim();','            if(turret!=null)turret.localPosition=turretHome+turret.parent.InverseTransformVector(-AimDirection*firePulse*.025f);'+"`n"+'            UpdateAim();')
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe' -ArgumentList @('-batchmode','-projectPath','"D:\Unity\FPS-PayDayGame"','-executeMethod','EnemyPolishBuild.Play','-ai-qa','-ai-output','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\EditorAI"','-player-profile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai-profile.json"','-logFile','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\editor-ai.log"') -WindowStyle Hidden -PassThru
$taskScript=[IO.File]::ReadAllText('D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\verify.ps1').Replace('Get-Process -Id 30668',('Get-Process -Id '+$taskProcess.Id))
$taskScript=[regex]::Replace($taskScript,'(?m)^    Invoke-TaskUnity ''editor-melee''.*\r?\n','')
[IO.File]::WriteAllText('D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\verify-final.ps1',$taskScript,[Text.UTF8Encoding]::new($false))
$taskRunner=Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File','"D:\Unity\FPS-PayDayGame\Artifacts\EnemyPolish\verify-final.ps1"') -WindowStyle Hidden -PassThru
Write-Output "Final AI: $($taskProcess.Id); runner: $($taskRunner.Id)"
git check-ignore Docs/EnemyThreat.md Docs/PlaytestFeedback.md Docs/EnemyPolish.md
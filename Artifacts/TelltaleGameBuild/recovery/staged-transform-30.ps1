$taskPath='D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged\Assets\Scripts\AI\RangedRobotCombat.cs'
$taskText=[IO.File]::ReadAllText($taskPath).Replace("`r`n","`n")
$taskText=$taskText.Replace('            if (!Aligned(target) || !ClearShot(target)) return;',@'
            if (!Aligned(target)) return;
            if (!ClearShot(target))
            {
                RestoreNavigationRotation(); navigation.Reposition(); navigation.Tick(false, canAttack);
                cooldown = .25f; return;
            }
'@)
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe' -ArgumentList @('-batchmode','-projectPath','"D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged"','-executeMethod','EnemyPolishBuild.Play','-ai-qa','-ai-output','"D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged\Artifacts\EnemyPolish\EditorAI"','-player-profile','"D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged\Artifacts\EnemyPolish\editor-ai-profile.json"','-logFile','"D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged\Artifacts\EnemyPolish\editor-ai.log"') -WindowStyle Hidden -PassThru
$taskScript=[IO.File]::ReadAllText('D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged\Artifacts\EnemyPolish\verify-final.ps1').Replace('Get-Process -Id 22980',('Get-Process -Id '+$taskProcess.Id))
[IO.File]::WriteAllText('D:\Unity\FPS-PayDayGame\Artifacts\TelltaleGameBuild\recovery\staged\Artifacts\EnemyPolish\verify-final.ps1',$taskScript,[Text.UTF8Encoding]::new($false))
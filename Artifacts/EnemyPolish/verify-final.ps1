$ErrorActionPreference = 'Stop'
$taskRoot = 'D:\Unity\FPS-PayDayGame'
$taskOutput = Join-Path $taskRoot 'Artifacts\EnemyPolish'
$taskUnity = 'C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe'
function Invoke-TaskUnity([string]$name, [string[]]$extra) {
    Add-Content -LiteralPath "$taskOutput\validation-progress.txt" -Value "START $name $(Get-Date -Format o)"
    $taskArgs = @('-batchmode','-projectPath','"D:\Unity\FPS-PayDayGame"','-logFile',('"'+$taskOutput+'\'+$name+'.log"')) + $extra
    $taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
    if (!$taskProcess.WaitForExit(360000)) { Stop-Process -Id $taskProcess.Id; throw "$name timeout" }
    Add-Content -LiteralPath "$taskOutput\validation-progress.txt" -Value "END $name exit=$($taskProcess.ExitCode)"
    if ($taskProcess.ExitCode -ne 0) { throw "$name failed: $($taskProcess.ExitCode)" }
}
try {
    $taskAI = Get-Process -Id 22292 -ErrorAction SilentlyContinue
    if ($null -ne $taskAI -and !$taskAI.WaitForExit(360000)) { throw 'AI timeout' }
    $taskAIResult = Get-Content -LiteralPath "$taskOutput\EditorAI\results.txt" -Raw
    if ($taskAIResult -match '(?m)^FAIL ' -or $taskAIResult.Split(@('ERRORS'),[StringSplitOptions]::None)[1].Trim().Length -gt 0) { throw 'AI validation failed' }
    Add-Content -LiteralPath "$taskOutput\validation-progress.txt" -Value 'AI PASSED'
    Invoke-TaskUnity 'windows-build' @('-quit','-nographics','-executeMethod','EnemyPolishBuild.Build','-share-output','"D:\Unity\FPS-PayDayGame\Builds\EnemyPolish\RobotTower.exe"')
    Add-Content -LiteralPath "$taskOutput\validation-progress.txt" -Value 'ALL EDITOR TESTS AND WINDOWS BUILD PASSED'
} catch {
    Add-Content -LiteralPath "$taskOutput\validation-progress.txt" -Value "FAILED $_"
    exit 1
}

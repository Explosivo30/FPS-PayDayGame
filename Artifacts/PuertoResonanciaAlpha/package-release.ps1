param([string]$ExtractionName = 'PortableCheck')
$ErrorActionPreference = 'Stop'
$districtRoot = 'D:/Unity/FPS-PayDayGame'
$districtBuild = Join-Path $districtRoot 'Builds/PuertoResonanciaAlpha'
$districtEvidence = Join-Path $districtRoot 'Artifacts/PuertoResonanciaAlpha'
$districtZip = Join-Path $districtRoot 'Builds/PuertoResonancia-Alpha-Windows-2026-10-26.zip'
$districtExtract = Join-Path $districtEvidence $ExtractionName
if (Test-Path -LiteralPath $districtExtract) { throw 'Independent extraction must use a new directory.' }

Copy-Item -LiteralPath (Join-Path $districtRoot 'Docs/AlphaDistrict/LEEME.md') -Destination (Join-Path $districtBuild 'REVISION.md')
Copy-Item -LiteralPath (Join-Path $districtRoot 'Docs/AlphaDistrict/RESEARCH.md') -Destination (Join-Path $districtBuild 'RESEARCH.md')
Copy-Item -LiteralPath (Join-Path $districtRoot 'Docs/AlphaDistrict/DOCUMENTACION.md') -Destination (Join-Path $districtBuild 'DOCUMENTACION.md')
$districtHistory = Join-Path $districtBuild 'Documentacion'
New-Item -ItemType Directory -Path $districtHistory -Force | Out-Null
foreach ($districtFolder in @('Plan-Maestro-2026-10-18', 'Gameplay-2026-10-21', 'FundicionReactiva-2026-10-22', 'FundicionReactiva-2026-10-23')) {
    Copy-Item -LiteralPath (Join-Path $districtRoot "Docs/$districtFolder") -Destination $districtHistory -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $districtRoot 'Docs/Veteranos-Ariete-Aguja.md') -Destination $districtHistory -Force
$districtPdf = Join-Path $districtHistory 'PDF'
New-Item -ItemType Directory -Path $districtPdf -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $districtRoot 'output/pdf') -Directory | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $districtPdf -Recurse -Force
}

$districtItems = @(Get-ChildItem -LiteralPath $districtBuild -Force | Where-Object {
    $_.Name -notlike '*DoNotShip*' -and $_.Name -notlike '*BackUpThisFolder*'
})
$districtFiles = @($districtItems | ForEach-Object {
    if ($_.PSIsContainer) { Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Force } else { $_ }
})
$districtManifest = @($districtFiles | ForEach-Object {
    [pscustomobject]@{ Path=[IO.Path]::GetRelativePath($districtBuild, $_.FullName); SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$districtManifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $districtEvidence 'release-manifest.json')
Compress-Archive -LiteralPath $districtItems.FullName -DestinationPath $districtZip -CompressionLevel Optimal -Force
Expand-Archive -LiteralPath $districtZip -DestinationPath $districtExtract
$districtChecks = @($districtManifest | ForEach-Object {
    $districtFile = Join-Path $districtExtract $_.Path
    [pscustomobject]@{ Path=$_.Path; Matches=((Test-Path -LiteralPath $districtFile) -and (Get-FileHash -LiteralPath $districtFile -Algorithm SHA256).Hash -eq $_.SHA256) }
})
$districtChecks | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $districtEvidence 'portable-integrity.json')
$districtExtractedFiles = @(Get-ChildItem -LiteralPath $districtExtract -Recurse -File -Force)
if (@($districtChecks | Where-Object { -not $_.Matches }).Count -or $districtExtractedFiles.Count -ne $districtManifest.Count) { throw 'ZIP extraction differs from the release payload.' }
$districtSummary = [pscustomobject]@{ Zip=$districtZip; Bytes=(Get-Item -LiteralPath $districtZip).Length; SHA256=(Get-FileHash -LiteralPath $districtZip -Algorithm SHA256).Hash; PayloadFiles=$districtManifest.Count; ExtractedFiles=$districtExtractedFiles.Count; MatchingFiles=@($districtChecks | Where-Object Matches).Count; PdfCount=@(Get-ChildItem -LiteralPath $districtPdf -Recurse -Filter *.pdf -File).Count; Extraction=$districtExtract }
$districtSummary | ConvertTo-Json | Tee-Object -FilePath (Join-Path $districtEvidence 'package-result.json')

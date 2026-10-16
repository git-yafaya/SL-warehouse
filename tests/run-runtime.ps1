param([Parameter(Mandatory = $true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
if (Get-Process Stacklands -ErrorAction SilentlyContinue) { throw 'Save and exit Stacklands before running this test.' }
& (Join-Path $projectPath 'build.ps1') -GamePath $GamePath
$managedPath = Join-Path $GamePath 'Stacklands_Data\Managed'
$testBuildPath = Join-Path $projectPath 'dist\runtime-test'
$modsPath = Join-Path $env:USERPROFILE 'AppData\LocalLow\sokpop\Stacklands\Mods'
$testInstallPath = Join-Path $modsPath 'slw_runtime_smoke'
$modInstallPath = Join-Path $modsPath 'sl_warehouse'
$backupPath = Join-Path $projectPath 'dist\runtime-install-backup'
if ((Test-Path -LiteralPath $testInstallPath) -or (Test-Path -LiteralPath $backupPath)) {
    throw 'Existing test installation or backup needs inspection before testing.'
}
New-Item -ItemType Directory -Force -Path $testBuildPath, $modsPath | Out-Null
$arguments = @('/nologo', '/noconfig', '/nostdlib+', '/target:library')
$arguments += '/out:' + (Join-Path $testBuildPath 'slw_runtime_smoke.dll')
$arguments += '/reference:' + (Join-Path $projectPath 'dist\sl_warehouse\sl_warehouse.dll')
foreach ($name in @('mscorlib','System','System.Core','netstandard','GameScripts','0Harmony','UnityEngine.CoreModule','UnityEngine.PhysicsModule','UnityEngine.TextRenderingModule','Unity.TextMeshPro','UnityEngine.UI')) {
    $arguments += '/reference:' + (Join-Path $managedPath ($name + '.dll'))
}
$arguments += Join-Path $PSScriptRoot 'RuntimeSmoke.cs'
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
'{"id":"slw_runtime_smoke","name":"Warehouse Runtime Smoke","version":"0.0.0","dependencies":["sl_warehouse"]}' |
    Set-Content -LiteralPath (Join-Path $testBuildPath 'manifest.json') -Encoding utf8

$testProcess = $null
$originalMoved = $false
$testInstallationStarted = $false
try {
    if (Test-Path -LiteralPath $modInstallPath) {
        if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($modInstallPath)) -ne [IO.Path]::GetFullPath($modsPath)) { throw 'Unexpected backup source.' }
        if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($backupPath)) -ne (Join-Path $projectPath 'dist')) { throw 'Unexpected backup destination.' }
        Move-Item -LiteralPath $modInstallPath -Destination $backupPath
        $originalMoved = $true
    }
    $testInstallationStarted = $true
    Copy-Item -LiteralPath (Join-Path $projectPath 'dist\sl_warehouse') -Destination $modsPath -Recurse
    Copy-Item -LiteralPath $testBuildPath -Destination $testInstallPath -Recurse
    foreach ($phase in @('defaults', 'configured')) {
        $logPath = Join-Path $projectPath ('dist\runtime-' + $phase + '.log')
        $launchArguments = @('-batchmode', '-nographics', '-slw-smoke-test', '-logFile', ('"' + $logPath + '"'))
        $resultName = 'results.txt'
        if ($phase -eq 'configured') {
            $launchArguments += '-slw-config-reload'
            $resultName = 'results.config.txt'
        }
        $testProcess = Start-Process -FilePath (Join-Path $GamePath 'Stacklands.exe') -WorkingDirectory $GamePath -WindowStyle Hidden -PassThru `
            -ArgumentList $launchArguments `
            -Environment @{ SteamAppId = '1948280' }
        $resultPath = Join-Path $testInstallPath $resultName
        $deadline = [DateTime]::UtcNow.AddSeconds(90)
        while (-not $testProcess.HasExited -and -not (Test-Path -LiteralPath $resultPath) -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 250
        }
        if (-not (Test-Path -LiteralPath $resultPath)) { throw "Game exited before tests ran; see $logPath." }
        Copy-Item -LiteralPath $resultPath -Destination (Join-Path $projectPath ('dist\runtime-' + $phase + '-results.txt'))
        $results = Get-Content -LiteralPath $resultPath
        $results
        if ($results -notcontains 'PASS all runtime smoke checks') { throw 'Runtime smoke checks failed.' }
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id; $testProcess.WaitForExit() }
    }
}
finally {
    if ($testProcess -and -not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id }
    # 仅清理本次创建且已验证处于 Mods 下的两个目录。
    foreach ($createdPath in @($testInstallPath, $modInstallPath)) {
        $absolutePath = [IO.Path]::GetFullPath($createdPath)
        if ([IO.Path]::GetDirectoryName($absolutePath) -ne [IO.Path]::GetFullPath($modsPath)) { throw 'Unexpected cleanup path.' }
        if ($testInstallationStarted -and (Test-Path -LiteralPath $absolutePath)) { Remove-Item -LiteralPath $absolutePath -Recurse -Force }
    }
    if ($originalMoved) { Move-Item -LiteralPath $backupPath -Destination $modInstallPath }
}

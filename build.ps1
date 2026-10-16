param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'
$managedPath = Join-Path $GamePath 'Stacklands_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputPath = Join-Path $PSScriptRoot 'dist\sl_warehouse'
$references = @('mscorlib', 'System', 'System.Core', 'netstandard', 'GameScripts', '0Harmony', 'UnityEngine.CoreModule', 'SokLoc')
$compilerArguments = @('/nologo', '/noconfig', '/target:library', '/nostdlib+', '/optimize+', '/utf8output')
foreach ($reference in $references) {
    $referencePath = Join-Path $managedPath ($reference + '.dll')
    if (-not (Test-Path -LiteralPath $referencePath)) { throw "Missing game assembly: $referencePath" }
    $compilerArguments += '/reference:' + $referencePath
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler is required.' }
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$compilerArguments += '/out:' + (Join-Path $outputPath 'sl_warehouse.dll')
$compilerArguments += Join-Path $PSScriptRoot 'Mod.cs'
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }

foreach ($name in @('manifest.json', 'localization.tsv', 'Cards', 'Blueprints')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $outputPath -Recurse -Force
}
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Icons')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Icons') -Destination $outputPath -Recurse -Force
}
Compress-Archive -LiteralPath $outputPath -DestinationPath (Join-Path $PSScriptRoot 'dist\sl_warehouse.zip') -Force
Write-Output "Built: $outputPath"

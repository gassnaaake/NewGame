param(
    [string]$Godot = 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe',
    [int]$RenderFps = 120
)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
Push-Location $projectDirectory
try {
    & dotnet build NewGame.csproj -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    New-Item -ItemType Directory -Force '.godot/test-results' | Out-Null
    foreach ($suite in @('self-test', 'stage-a', 'stage-b', 'platform-tests', 'stage-c')) {
        $scene = if ($suite -eq 'stage-c') { 'res://Scenes/RouteLab.tscn' } else { 'res://Scenes/Lab.tscn' }
        $logPath = ".godot/test-results/p002-$suite-$RenderFps.log"
        & $Godot --headless --path $projectDirectory $scene --fixed-fps $RenderFps -- "--$suite" 2>&1 |
            Tee-Object -FilePath $logPath
        if ($LASTEXITCODE -ne 0) { throw "$suite failed (see $logPath)." }
        if (Select-String -Path $logPath -Pattern '^ERROR:|^FAIL' -Quiet) { throw "$suite emitted engine errors." }
    }
} finally { Pop-Location }

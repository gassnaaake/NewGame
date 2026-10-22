param(
    [string]$Godot = '',
    [int]$RenderFps = 120
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Resolve-Godot.ps1"
$Godot = Resolve-GodotPath -Godot $Godot
$projectDirectory = Split-Path $PSScriptRoot -Parent
Push-Location $projectDirectory
try {
    # P-002.2 regression (includes P-001) on the same build.
    & "$PSScriptRoot\Run-P002.ps1" -Godot $Godot -RenderFps $RenderFps
    if ($LASTEXITCODE -ne 0) { throw 'P-002 regression failed.' }

    New-Item -ItemType Directory -Force '.godot/test-results' | Out-Null
    $logPath = ".godot/test-results/p0023-playground-$RenderFps.log"
    & $Godot --headless --path $projectDirectory res://Scenes/GravityPlayground.tscn `
        --fixed-fps $RenderFps -- --playground-tests 2>&1 |
        Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "playground tests failed (see $logPath)." }
    if (Select-String -Path $logPath -Pattern '^ERROR:|^FAIL' -Quiet) { throw 'playground tests emitted engine errors.' }
} finally { Pop-Location }

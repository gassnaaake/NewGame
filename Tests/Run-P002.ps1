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
    & dotnet build NewGame.csproj -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    New-Item -ItemType Directory -Force '.godot/test-results' | Out-Null
    foreach ($suite in @('self-test', 'stage-a', 'stage-b', 'platform-tests', 'gravity-tests', 'stage-c')) {
        $scene = switch ($suite) {
            'stage-c' { 'res://Scenes/RouteLab.tscn' }
            'gravity-tests' { 'res://Scenes/GravityLab.tscn' }
            default { 'res://Scenes/Lab.tscn' }
        }
        $logPath = ".godot/test-results/p0022-$suite-$RenderFps.log"
        & $Godot --headless --path $projectDirectory $scene --fixed-fps $RenderFps -- "--$suite" 2>&1 |
            Tee-Object -FilePath $logPath
        if ($LASTEXITCODE -ne 0) { throw "$suite failed (see $logPath)." }
        if (Select-String -Path $logPath -Pattern '^ERROR:|^FAIL' -Quiet) { throw "$suite emitted engine errors." }
    }
} finally { Pop-Location }

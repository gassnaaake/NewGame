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
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    New-Item -ItemType Directory -Force '.godot/test-results' | Out-Null
    & $Godot --headless --path $projectDirectory res://Scenes/Lab.tscn --fixed-fps $RenderFps -- --self-test 2>&1 |
        Tee-Object -FilePath ".godot/test-results/physics-$RenderFps.log"
    if ($LASTEXITCODE -ne 0) { throw 'Godot physics tests failed.' }
} finally {
    Pop-Location
}

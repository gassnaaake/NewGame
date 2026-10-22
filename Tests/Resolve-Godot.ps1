# Resolve the Godot .NET console executable used by the test entry scripts.
# Precedence: explicit -Godot, GODOT_EXE, then the known 4.7.2 install paths.
# Do not fall back to 4.6.1: the migration must be verified with 4.7.2.
function Resolve-GodotPath {
    [CmdletBinding()]
    param([string]$Godot = '')

    $candidates = @(
        $Godot,
        $env:GODOT_EXE,
        'D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe',
        'E:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
    )
    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw 'Godot 4.7.2 .NET console executable not found. Pass -Godot <absolute path> or set GODOT_EXE.'
}

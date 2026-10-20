using Godot;

namespace PlanetLab;

public partial class Lab : Node2D
{
    private Player _player = null!;
    private Label _debug = null!;
    public override void _Ready()
    {
        _player = GetNode<Player>("Player");
        _debug = GetNode<Label>("HUD/Debug");
        if (!InputMap.HasAction("jump"))
        {
            InputMap.AddAction("jump");
            InputMap.ActionAddEvent("jump", new InputEventKey { PhysicalKeycode = Key.Space });
        }
        if (System.Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--self-test"))
            CallDeferred(MethodName.AddChild, new LabTests());
        if (System.Array.Exists(OS.GetCmdlineUserArgs(), arg => arg.StartsWith("--stage-a")))
            CallDeferred(MethodName.AddChild, new MovementTests());
        if (System.Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--stage-b"))
            CallDeferred(MethodName.AddChild, new DashTests());
        if (System.Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--stage-c"))
            CallDeferred(MethodName.AddChild, new RouteTests());
        if (System.Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--platform-tests"))
            CallDeferred(MethodName.AddChild, new PlatformTests());
        if (System.Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture"))
            CapturePreview();
    }

    private async void CapturePreview()
    {
        for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/test-results");
        Error result = GetViewport().GetTexture().GetImage().SavePng("res://.godot/test-results/preview.png");
        GD.Print($"Preview capture: {result}");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.R)
            GetTree().ReloadCurrentScene();
    }

    public override void _Process(double delta)
    {
        Vector2 g = _player.Gravity;
        float angle = g.IsZeroApprox() ? 0 : Mathf.RadToDeg(Mathf.Abs(_player.BodyDown.AngleTo(g)));
        _debug.Text = $"PLANET LAB / P-002\n" +
            $"State: {_player.State}  Body down: {_player.BodyDown.ToString("F2")}  Gravity angle: {(g.IsZeroApprox() ? "--" : angle.ToString("F1"))}\n" +
            $"Dash: {_player.DashDirection.ToString("F2")}  speed {(_player.State == Player.MovementState.DownDash ? _player.Velocity.Length() : 0):F0}  End: {_player.LastDashEndReason}\n" +
            $"Gravity source: {_player.GravitySource?.DisplayName ?? "None (inertia)"}\n" +
            $"Gravity: {g.Length():F1}   direction {g.Normalized().ToString("F2")}\n" +
            $"Velocity: {_player.Velocity.ToString("F1")}   speed {_player.Velocity.Length():F1}\n" +
            $"Grounded: {_player.Grounded}   support: {_player.SupportBody?.Name.ToString() ?? "None"}\n" +
            $"Support normal: {(_player.Grounded ? _player.SupportNormal.ToString("F2") : "-- (airborne)")}\n" +
            $"Launch: {_player.LaunchVelocity.Length():F0}/{_player.MaxLaunchSpeed:F0}   direction {_player.LaunchVelocity.Normalized().ToString("F2")}\n" +
            (_player.Aiming ? "AIMING — motion paused / release to launch / Esc cancel" : "A/D move  |  Space jump  |  hold LMB + drag backward to launch (ground only)") +
            "\nJ down-dash (airborne)  |  R reset  |  red: gravity  green: support  white: launch";
    }
}

using Godot;
using System;
using System.Collections.Generic;

namespace PlanetLab;

public partial class DashTests : Node
{
    private Player _player = null!;
    private IEnumerator<int> _steps = null!;
    private int _wait, _checks;
    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        _player = GetParent().GetNode<Player>("Player");
        _player.TestMode = true;
        _steps = Run().GetEnumerator();
    }
    private void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }
    public override void _PhysicsProcess(double delta)
    {
        try
        {
            if (_wait-- > 0) return;
            if (_steps.MoveNext()) _wait = Math.Max(0, _steps.Current - 1);
            else { GD.Print($"STAGE B PASS: {_checks} checks"); GetTree().Quit(); SetPhysicsProcess(false); }
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); SetPhysicsProcess(false); }
    }
    private IEnumerable<int> Run()
    {
        yield return 120;
        Check(!_player.TryBeginDash(), "grounded dash rejected");
        _player.TestJump = true;
        yield return 10;
        Vector2 down = _player.BodyDown;
        Check(_player.TryBeginDash(), "airborne dash accepted");
        Check(_player.DashDirection.DistanceTo(down) < 0.0001f, "dash uses body down at activation");
        Check(!_player.TryBeginDash(), "dash cannot retrigger while active");
        _player.TestAxis = 1;
        yield return 1;
        Check(_player.Velocity.DistanceTo(down * _player.DownDashSpeed) < 0.01f, "gravity and input do not bend dash");
        _player.TestAxis = 0;
        yield return 60;
        Check(_player.State == Player.MovementState.Normal && _player.Grounded, "planet blocks dash and restores grounded state");
        Check(_player.Velocity.Length() < 1, "dash impact does not rebound");

        _player.ResetAt(new Vector2(-3000, -3000));
        Check(_player.TryBeginDash(), "isolated flight starts");
        Vector2 start = _player.GlobalPosition;
        yield return 600;
        Check(_player.State == Player.MovementState.DownDash, "dash has no timeout or distance cap");
        Check(_player.GlobalPosition.DistanceTo(start + Vector2.Down * 6000) < 1, "five seconds of locked straight flight");

        var blocker = new StaticBody2D { Name = "ThinBlocker", Position = new Vector2(-3000, 3100) };
        blocker.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(180, 2) } });
        GetParent().AddChild(blocker);
        yield return 1;
        _player.DownDashSpeed = 100000;
        yield return 1;
        Check(_player.State == Player.MovementState.Normal, "sweep blocks 100000 px/s dash on 2px solid");
        Check(_player.GlobalPosition.Y < 3100 && _player.Velocity.IsZeroApprox(), "thin blocker impact stops before penetration");
        Check(_player.LastDashEndReason.Contains("ThinBlocker"), "dash end reason names blocker");
        blocker.QueueFree();
        _player.DownDashSpeed = 1200;
    }
}

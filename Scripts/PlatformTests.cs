using Godot;
using System;
using System.Collections.Generic;

namespace PlanetLab;

public partial class PlatformTests : Node
{
    private Player _player = null!;
    private IEnumerator<int> _steps = null!;
    private int _wait, _checks;
    private void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }
    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        _player = GetParent().GetNode<Player>("Player");
        _player.TestMode = true;
        _steps = Run().GetEnumerator();
    }
    public override void _PhysicsProcess(double delta)
    {
        try
        {
            if (_wait-- > 0) return;
            if (_steps.MoveNext()) _wait = Math.Max(0, _steps.Current - 1);
            else { GD.Print($"PLATFORMS PASS: {_checks} checks"); GetTree().Quit(); SetPhysicsProcess(false); }
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); SetPhysicsProcess(false); }
    }
    private DashPlatform Platform(string name, Vector2 position, Vector2 size, DashPlatform.PlatformKind kind)
    {
        var p = new DashPlatform { Name = name, Position = position, Size = size, Kind = kind };
        GetParent().AddChild(p);
        return p;
    }
    private IEnumerable<int> Run()
    {
        foreach (var kind in new[] { DashPlatform.PlatformKind.Breakable, DashPlatform.PlatformKind.DashPass })
        {
            // Use the sphere's upward gravity frame to verify real standing and jumping.
            var platform = Platform("Standing" + kind, new Vector2(-400, -150), new Vector2(100, 8), kind);
            yield return 1;
            _player.ResetAt(new Vector2(-400, -220));
            yield return 120;
            Check(_player.Grounded && _player.SupportBody == platform, $"{kind}: normal movement lands and stands");
            Check(!platform.Destroyed, $"{kind}: normal landing does not destroy it");
            _player.TestJump = true;
            yield return 8;
            Check(!_player.Grounded, $"{kind}: normal jump works");
            _player.TryBeginDash();
            yield return 20;
            Check(_player.Position.Y > platform.Position.Y + platform.Size.Y / 2 + Player.Radius, $"{kind}: dash crosses full platform thickness");
            Check(platform.Destroyed == (kind == DashPlatform.PlatformKind.Breakable), $"{kind}: destruction rule");
            platform.QueueFree();
            yield return 1;
        }
        // Adversarial overlap: a solid stops the dash while the player is still inside
        // a pass platform. Restore only after actual movement clears the volume.
        var pass = Platform("OverlappingPass", new Vector2(-3000, 0), new Vector2(100, 100), DashPlatform.PlatformKind.DashPass);
        var solid = Platform("EmbeddedSolid", new Vector2(-3000, 30), new Vector2(100, 2), DashPlatform.PlatformKind.Solid);
        yield return 1;
        _player.ResetAt(new Vector2(-3000, -100));
        _player.TryBeginDash();
        yield return 20;
        Check(_player.State == Player.MovementState.Normal && _player.Velocity.IsZeroApprox(), "solid ends dash inside pass platform");
        Check(pass.OverlapsPlayer(_player.Position, Player.Radius), "overlap scenario really ends inside pass platform");
        Check(_player.PendingCollisionRestores == 1, "collision stays excluded while embedded");
        _player.Velocity = Vector2.Right * 400;
        yield return 30;
        Check(!pass.OverlapsPlayer(_player.Position, Player.Radius), "normal motion can exit overlapping pass platform");
        Check(_player.PendingCollisionRestores == 0, "normal collision restored once fully clear");
        solid.QueueFree();
        yield return 1;
        _player.Velocity = Vector2.Left * 400;
        yield return 30;
        Check(_player.Position.X >= -2938.5f, "restored pass platform blocks normal re-entry");
        Check(!pass.Destroyed, "pass platform remains intact after recovery");
        pass.QueueFree();
        yield return 1;

        // Several thin barriers within one physics step: spend the remaining swept
        // motion after each permitted hit, and stop at the first ordinary solid.
        var breakable = Platform("FastBreak", new Vector2(-3000, 0), new Vector2(100, 2), DashPlatform.PlatformKind.Breakable);
        var fastPass = Platform("FastPass", new Vector2(-3000, 20), new Vector2(100, 2), DashPlatform.PlatformKind.DashPass);
        var stop = Platform("FastStop", new Vector2(-3000, 40), new Vector2(100, 2), DashPlatform.PlatformKind.Solid);
        yield return 1;
        _player.ResetAt(new Vector2(-3000, -100));
        _player.DownDashSpeed = 100000;
        _player.TryBeginDash();
        yield return 1;
        Check(breakable.Destroyed && !fastPass.Destroyed, "same-step break and pass at extreme speed");
        Check(_player.State == Player.MovementState.Normal && _player.Position.Y < 40, "remaining sweep finds thin solid behind special platforms");
        Check(_player.LastDashEndReason.Contains("FastStop"), "correct final solid recorded");
        breakable.QueueFree(); fastPass.QueueFree(); stop.QueueFree();
    }
}

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
        // 中文：测试平台也使用真实 CollisionShape2D 子节点，不依赖运行时隐藏生成。
        var p = new DashPlatform { Name = name, Position = position, Kind = kind };
        p.AddChild(new CollisionShape2D
        {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = size }
        });
        GetParent().AddChild(p);
        return p;
    }
    private IEnumerable<int> Run()
    {
        // 中文：检查平台现在是“节点 + 真实碰撞子节点”的组合，且尺寸来自 Shape。
        var probe = Platform("ShapeProbe", new Vector2(-3000, -500), new Vector2(100, 10),
            DashPlatform.PlatformKind.Solid);
        yield return 1;
        Check(probe.GetChildCount() == 1 && probe.CollisionShapeNode?.Shape is RectangleShape2D,
            "platform has one real RectangleShape2D child");
        Check(probe.Size.DistanceTo(new Vector2(100, 10)) < 0.01f,
            "platform Size reads the child collision shape");
        var probeShape = (RectangleShape2D)probe.CollisionShapeNode!.Shape!;
        probeShape.Size = new Vector2(20, 10);
        Check(probe.Size.DistanceTo(new Vector2(20, 10)) < 0.01f,
            "editing the child shape changes the derived size");
        Check(!probe.OverlapsPlayer(new Vector2(-2970, -500), 1),
            "collision query follows the edited child shape");

        bool helpFound = false, helpReadOnly = false, helpStored = false, helpGroupFound = false;
        foreach (Godot.Collections.Dictionary property in probe.GetPropertyList())
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            string propertyName = property["name"].AsStringName().ToString();
            if (propertyName == "UsageNote")
            {
                helpFound = true;
                helpReadOnly = usage.HasFlag(PropertyUsageFlags.ReadOnly);
                helpStored = usage.HasFlag(PropertyUsageFlags.Storage);
            }
            if (propertyName == "① 平台玩法（常用）" && usage.HasFlag(PropertyUsageFlags.Group))
                helpGroupFound = true;
        }
        Check(helpFound && helpReadOnly && !helpStored,
            "platform UsageNote is read-only and not serialized");
        Check(helpGroupFound, "platform Chinese gameplay group is present");
        probe.QueueFree();
        yield return 1;

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

using Godot;
using System;
using System.Collections.Generic;

namespace PlanetLab;

public partial class MovementTests : Node
{
    private Player _player = null!;
    private IEnumerator<int> _steps = null!;
    private int _wait;
    protected int Checks;
    protected void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Checks++;
        GD.Print("PASS: " + message);
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
            else { GD.Print($"STAGE A PASS: {Checks} checks"); GetTree().Quit(); SetPhysicsProcess(false); }
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); SetPhysicsProcess(false); }
    }
    private IEnumerable<int> Run()
    {
        bool probe = Array.Exists(OS.GetCmdlineUserArgs(), s => s == "--stage-a-probe");
        foreach (string name in new[] { "Sphere", "Capsule" })
        {
            Planet planet = GetParent().GetNode<Planet>(name);
            for (int i = 0; i < 8; i++)
            {
                Vector2 n = Vector2.FromAngle(i * Mathf.Tau / 8);
                SurfaceSample surface = planet.Sample(planet.Position + n * 600);
                Vector2 tangent = new(-surface.Normal.Y, surface.Normal.X);
                _player.ResetAt(surface.Point + surface.Normal * 80, -surface.Normal * 650 + tangent * 170);
                bool landed = false;
                for (int tick = 0; tick < 300; tick++)
                {
                    Vector2 before = _player.Velocity;
                    yield return 1;
                    if (!_player.Grounded) continue;
                    landed = true;
                    float outward = _player.Velocity.Dot(_player.SupportNormal);
                    GD.Print($"LAND {name}/{i} incoming={before} outgoing={_player.Velocity} outward={outward:F3} tangent={_player.Velocity.Slide(_player.SupportNormal).Length():F3}");
                    if (!probe)
                    {
                        Check(Mathf.Abs(outward) < 0.1f, $"{name}/{i}: landing normal speed removed");
                        Check(_player.Velocity.Slide(_player.SupportNormal).Length() > 100, $"{name}/{i}: landing retains tangent momentum");
                    }
                    break;
                }
                Check(landed, $"{name}/{i}: oblique landing");
                yield return 120;
                if (!probe) Check(_player.Grounded && _player.Velocity.Length() < 5, $"{name}/{i}: settles without rebound");
            }
        }
        _player.ResetAt(new Vector2(-400, -82.1f));
        yield return 60;
        Vector2 heldUp = _player.BodyUp;
        _player.TestJump = true;
        yield return 8;
        Check(!_player.Grounded && _player.BodyUp.DistanceTo(heldUp) < 0.0001f, "jump preserves departure body orientation");
        // Diagnostic relocation only: sample a different gravity region without touching ground.
        _player.GlobalPosition = new Vector2(720, 70);
        _player.Velocity = Vector2.Zero;
        yield return 1;
        Check(_player.GravitySource?.Name == "Capsule", "airborne gravity source changes");
        Check(_player.BodyUp.DistanceTo(heldUp) < 0.0001f, "gravity switch cannot rotate airborne body");
    }
}

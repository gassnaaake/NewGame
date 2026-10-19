using Godot;
using System;
using System.Collections.Generic;

namespace PlanetLab;

// Runs the real scene and physics, without external test frameworks.
public partial class LabTests : Node
{
    private Player _player = null!;
    private Planet _sphere = null!;
    private Planet _capsule = null!;
    private IEnumerator<int> _steps = null!;
    private int _wait;
    private int _checks;

    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        _player = GetParent().GetNode<Player>("Player");
        _sphere = GetParent().GetNode<Planet>("Sphere");
        _capsule = GetParent().GetNode<Planet>("Capsule");
        _player.TestMode = true;
        _steps = Run().GetEnumerator();
    }

    public override void _PhysicsProcess(double delta)
    {
        try
        {
            if (_wait-- > 0) return;
            if (_steps.MoveNext()) _wait = Math.Max(0, _steps.Current - 1);
            else { GD.Print($"PASS: {_checks} checks; all physics scenarios complete."); GetTree().Quit(); SetPhysicsProcess(false); }
        }
        catch (Exception error)
        {
            GD.PushError($"FAIL after {_checks} checks: {error}");
            GetTree().Quit(1);
            SetPhysicsProcess(false);
        }
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
        GD.Print($"PASS: {message}");
    }

    private void CheckClear()
    {
        foreach (Planet p in new[] { _sphere, _capsule })
        {
            float distance = p.Sample(_player.GlobalPosition).SignedDistance;
            if (distance < Player.Radius - 0.8f)
                throw new Exception($"Penetration into {p.Name}: distance={distance} position={_player.GlobalPosition}");
        }
        if (!float.IsFinite(_player.Velocity.Length())) throw new Exception("Non-finite velocity");
    }

    private IEnumerable<int> Run()
    {
        var circle = new CircleShape2D { Radius = 180 };
        Check(Mathf.Abs(SurfaceGeometry.Sample(circle, new Vector2(200, 0)).SignedDistance - 20) < 0.001f, "circle distance");
        var cap = new CapsuleShape2D { Radius = 130, Height = 560 };
        SurfaceSample a = SurfaceGeometry.Sample(cap, new Vector2(142, 149.99f));
        SurfaceSample b = SurfaceGeometry.Sample(cap, new Vector2(142, 150.01f));
        Check(a.Normal.DistanceTo(b.Normal) < 0.001f, "capsule tangent/arc normal continuity");
        Check(_capsule.Sample(new Vector2(400, -72)).Normal.DistanceTo(Vector2.Up) < 0.001f, "rotated capsule world-space normal");
        Check(GravitySelector.Select(new[] { _sphere, _capsule }, new Vector2(0, -3000)) == null, "outside all influence regions");
        Vector2 savedPosition = _player.GlobalPosition;
        Vector2 savedVelocity = _player.Velocity;
        GravitySelector.Select(new[] { _sphere, _capsule }, _capsule.Position + Vector2.Up * 160);
        Check(_player.GlobalPosition == savedPosition && _player.Velocity == savedVelocity, "selection has no player mutation");

        yield return 120;
        Check(_player.Grounded && _player.SupportPlanet == _sphere, "spawn settles on sphere");
        Vector2 rest = _player.GlobalPosition;
        yield return 600;
        Check(_player.GlobalPosition.DistanceTo(rest) < 0.5f, "five-second standing stability");

        foreach (Planet planet in new[] { _sphere, _capsule })
        foreach (int direction in new[] { 1, -1 })
        {
            _player.ResetAt(planet.Position + Vector2.Up * (planet == _sphere ? 192.1f : 142.1f));
            yield return 60;
            Check(_player.Grounded, $"{planet.Name} walking setup");
            _player.TestAxis = direction;
            float rotation = 0;
            float previous = (_player.GlobalPosition - planet.Position).Angle();
            int groundedFrames = 0;
            for (int i = 0; i < 2400; i++)
            {
                yield return 1;
                CheckClear();
                float angle = (_player.GlobalPosition - planet.Position).Angle();
                rotation += Mathf.Wrap(angle - previous, -Mathf.Pi, Mathf.Pi);
                previous = angle;
                if (_player.Grounded) groundedFrames++;
            }
            _player.TestAxis = 0;
            GD.Print($"WALK {planet.Name} dir={direction} turns={rotation / Mathf.Tau:F2} grounded={groundedFrames}/2400");
            Check(Mathf.Abs(rotation) > Mathf.Tau * 2, $"{planet.Name} direction {direction}: two complete laps");
            Check(groundedFrames > 2280, $"{planet.Name} direction {direction}: >95% grounded");
        }

        foreach (Planet planet in new[] { _sphere, _capsule })
        for (int i = 0; i < 8; i++)
        {
            Vector2 outward = Vector2.FromAngle(Mathf.Tau * i / 8);
            SurfaceSample boundary = planet.Sample(planet.Position + outward * 600);
            _player.ResetAt(boundary.Point + boundary.Normal * 12.1f);
            yield return 60;
            Check(_player.Grounded, $"{planet.Name} jump {i} setup");
            _player.TestJump = true;
            yield return 8;
            Check(!_player.Grounded, $"{planet.Name} jump {i} leaves support");
            bool landed = false;
            for (int frame = 0; frame < 300; frame++)
            {
                yield return 1;
                CheckClear();
                if (_player.Grounded) { landed = true; break; }
            }
            Check(landed && _player.SupportPlanet == planet, $"{planet.Name} jump {i} returns to same planet");
        }

        _player.ResetAt(new Vector2(0, -2500), new Vector2(170, -40));
        yield return 1;
        Vector2 inertialStart = _player.GlobalPosition;
        yield return 120;
        Check(_player.GravitySource == null && _player.Velocity.DistanceTo(new Vector2(170, -40)) < 0.01f, "zero-gravity inertia preserves velocity");
        Check((_player.GlobalPosition - inertialStart).DistanceTo(new Vector2(170, -40)) < 0.1f, "one-second inertial displacement");
        Check(!_player.BeginAim(Vector2.Zero), "airborne aiming rejected");

        foreach (bool returning in new[] { false, true })
        {
            Planet source = returning ? _capsule : _sphere;
            Planet target = returning ? _sphere : _capsule;
            Vector2 outward = returning ? Vector2.Left : Vector2.Right;
            SurfaceSample edge = source.Sample(source.Position + outward * 600);
            _player.ResetAt(edge.Point + edge.Normal * 12.1f);
            yield return 60;
            Check(_player.Grounded, $"{source.Name} launch setup");
            Check(_player.BeginAim(new Vector2(500, 400)), "grounded aiming accepted");
            _player.UpdateAim(new Vector2(-5000, 400));
            Check(Mathf.Abs(_player.LaunchVelocity.Length() - _player.MaxLaunchSpeed) < 0.01f, "launch speed is capped");
            Vector2 aimPosition = _player.GlobalPosition;
            Vector2 aimVelocity = _player.Velocity;
            yield return 120;
            Check(_player.GlobalPosition == aimPosition && _player.Velocity == aimVelocity, "aiming freezes position and velocity");
            Vector2 launch = new(returning ? -650 : 650, -100);
            _player.ReleaseAim(new Vector2(500, 400) - launch / _player.LaunchSpeedPerPixel);
            Check(_player.Velocity.DistanceTo(launch) < 0.01f, "release sets repeatable initial velocity");
            bool arrived = false;
            int switches = 0;
            Planet? last = source;
            for (int frame = 0; frame < 1200; frame++)
            {
                yield return 1;
                CheckClear();
                if (_player.GravitySource != last) { switches++; last = _player.GravitySource; }
                if (_player.Grounded && _player.SupportPlanet == target) { arrived = true; break; }
            }
            GD.Print($"TRAVEL {source.Name}->{target.Name} switches={switches} position={_player.GlobalPosition}");
            Check(arrived, $"{source.Name} -> {target.Name} lands");
            Check(switches == 1, "travel switches gravity once without chatter");
            _player.TestAxis = 1;
            Vector2 landingPosition = _player.GlobalPosition;
            yield return 120;
            Check(_player.GlobalPosition.DistanceTo(landingPosition) > 50, "movement remains available after landing");
        }

        _player.ResetAt(_sphere.Position + Vector2.Up * 192.1f);
        yield return 60;
        _player.BeginAim(Vector2.Zero);
        _player.ReleaseAim(new Vector2(-225, 0)); // 900 px/s tangent launch.
        yield return 15;
        Check(!_player.Grounded && _sphere.Sample(_player.GlobalPosition).SignedDistance > 20, "high-speed tangent motion naturally detaches");
    }
}

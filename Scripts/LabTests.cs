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
    private static bool _awaitingReload;
    private static int _checksBeforeReload;

    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        _player = GetParent().GetNode<Player>("Player");
        _sphere = GetParent().GetNode<Planet>("Sphere");
        _capsule = GetParent().GetNode<Planet>("Capsule");
        _player.TestMode = true;
        _checks = _awaitingReload ? _checksBeforeReload : 0;
        _steps = (_awaitingReload ? ValidateReload() : Run()).GetEnumerator();
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
            int airRun = 0;
            int longestAirRun = 0;
            float maxGap = 0;
            for (int i = 0; i < 2400; i++)
            {
                yield return 1;
                CheckClear();
                float angle = (_player.GlobalPosition - planet.Position).Angle();
                rotation += Mathf.Wrap(angle - previous, -Mathf.Pi, Mathf.Pi);
                previous = angle;
                if (_player.Grounded) groundedFrames++;
                airRun = _player.Grounded ? 0 : airRun + 1;
                longestAirRun = Math.Max(longestAirRun, airRun);
                maxGap = Mathf.Max(maxGap, planet.Sample(_player.GlobalPosition).SignedDistance - Player.Radius);
            }
            _player.TestAxis = 0;
            GD.Print($"WALK {planet.Name} dir={direction} turns={rotation / Mathf.Tau:F2} grounded={groundedFrames}/2400 maxAirRun={longestAirRun} maxGap={maxGap:F3}");
            Check(Mathf.Abs(rotation) > Mathf.Tau * 2, $"{planet.Name} direction {direction}: two complete laps");
            Check(groundedFrames > 2280, $"{planet.Name} direction {direction}: >95% grounded");
            Check(longestAirRun <= 2 && maxGap < 0.5f, $"{planet.Name}: contact gaps stay under two ticks and half a pixel");
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
            _player.ReleaseAim(new Vector2(500, 400) - launch.Rotated(-GetViewport().GetCamera2D().GlobalRotation) / _player.LaunchSpeedPerPixel);
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
        _player.ReleaseAim(new Vector2(-225, 0).Rotated(-GetViewport().GetCamera2D().GlobalRotation)); // 900 px/s tangent launch.
        yield return 15;
        Check(!_player.Grounded && _sphere.Sample(_player.GlobalPosition).SignedDistance > 20, "high-speed tangent motion naturally detaches");

        float oldMaximum = _player.MaxGroundSpeed;
        float oldAcceleration = _player.GroundAcceleration;
        _player.ResetAt(_sphere.Position + Vector2.Up * 192.1f);
        yield return 60;
        _player.MaxGroundSpeed = 900;
        _player.GroundAcceleration = 6000;
        _player.TestAxis = 1;
        bool walkedOff = false;
        for (int frame = 0; frame < 90; frame++)
        {
            yield return 1;
            CheckClear();
            if (!_player.Grounded && _sphere.Sample(_player.GlobalPosition).SignedDistance > 20) { walkedOff = true; break; }
        }
        Check(walkedOff, "high-speed ground acceleration detaches without jumping or launching");
        _player.MaxGroundSpeed = oldMaximum;
        _player.GroundAcceleration = oldAcceleration;

        _player.ResetAt(_sphere.Position + Vector2.Up * 192.1f);
        yield return 60;
        Vector2 cancelPosition = _player.GlobalPosition;
        _player.BeginAim(Vector2.Zero);
        _player.UpdateAim(new Vector2(100, 0));
        _player.CancelAim();
        Check(!_player.Aiming && _player.LaunchVelocity.IsZeroApprox(), "aim cancellation clears preview");
        _player.BeginAim(Vector2.Zero);
        _player.ReleaseAim(new Vector2(2, 0));
        yield return 1;
        Check(_player.Grounded && _player.GlobalPosition.DistanceTo(cancelPosition) < 0.2f, "small mouse drag does not launch");

        // Continuous round trip: no reset/teleport between the outbound and return legs.
        _player.ResetAt(_sphere.Position + Vector2.Right * 192.1f);
        yield return 60;
        foreach (Planet target in new[] { _capsule, _sphere })
        {
            Check(_player.BeginAim(Vector2.Zero), "continuous round trip can aim from actual landing");
            Vector2 shot = (target.Position - _player.GlobalPosition).Normalized() * 650;
            _player.ReleaseAim(-shot.Rotated(-GetViewport().GetCamera2D().GlobalRotation) / _player.LaunchSpeedPerPixel);
            bool arrived = false;
            for (int frame = 0; frame < 1000; frame++)
            {
                yield return 1;
                CheckClear();
                if (_player.Grounded && _player.SupportPlanet == target) { arrived = true; break; }
            }
            Check(arrived, $"continuous round trip lands on {target.Name}");
        }

        // Exercise real keyboard dispatch and scene reload, not just ResetAt().
        _checksBeforeReload = _checks;
        _awaitingReload = true;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.R, Keycode = Key.R, Pressed = true });
        yield return 120;
        throw new Exception("R input did not reload the scene.");
    }

    private IEnumerable<int> ValidateReload()
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.R, Keycode = Key.R, Pressed = false });
        yield return 120;
        Check(_player.Grounded && _player.SupportPlanet == _sphere, "R input reloads scene and restores sphere spawn");
        Check(_player.GlobalPosition.DistanceTo(new Vector2(-400, -82)) < 0.5f, "reset restores initial location");
        Check(!_player.Aiming && _player.Velocity.Length() < 0.1f, "reset clears aiming and travel velocity");
        _player.TestMode = false;
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(500, 400) }, true);
        yield return 8;
        Check(_player.Aiming, "mouse press reaches aim handler");
        GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(500, 500) }, true);
        yield return 8;
        Check(_player.LaunchVelocity.DistanceTo(new Vector2(0, -400)) < 0.01f, "mouse motion updates reverse drag preview");
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(500, 500) }, true);
        yield return 8;
        Check(!_player.Aiming && !_player.Grounded && _player.Velocity.Y < -300, "mouse release launches through real input dispatch");
    }
}


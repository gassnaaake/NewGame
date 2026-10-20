using Godot;
using System;
using System.Collections.Generic;

namespace PlanetLab;

public partial class RouteTests : Node
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
            else { GD.Print($"STAGE C PASS: {_checks} checks"); GetTree().Quit(); SetPhysicsProcess(false); }
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); SetPhysicsProcess(false); }
    }
    private IEnumerable<int> Run()
    {
        yield return 120;
        bool reachedPad = false;
        _player.TestAxis = 1;
        int lastJump = -100;
        string lastBody = "";
        for (int frame = 0; frame < 2400; frame++)
        {
            string body = _player.SupportBody?.Name.ToString() ?? "";
            if (_player.Grounded && body != lastBody)
            {
                GD.Print($"ROUTE frame={frame} support={body} pos={_player.Position} speed={_player.Velocity}");
                lastBody = body;
            }
            if (_player.Grounded && body == "LaunchPad") { reachedPad = true; break; }
            if (_player.Grounded && frame - lastJump >= 35 && frame > 14)
            {
                _player.TestJump = true;
                lastJump = frame;
            }
            yield return 1;
        }
        Check(reachedPad, "reach tilted pad by normal movement and jumps, no launch or relocation");
        DashPlatform pad = GetParent().GetNode<DashPlatform>("LaunchPad");
        for (int frame = 0; frame < 180 && pad.ToLocal(_player.Position).X < -25; frame++)
        {
            _player.TestAxis = 1;
            yield return 1;
        }
        _player.TestAxis = 0;
        yield return 60;
        Check(_player.Grounded && _player.SupportBody == pad, "walk to pad center before takeoff");
        Capture("p002-pad");
        Vector2 down = _player.BodyDown;
        _player.TestJump = true;
        yield return 12;
        Check(!_player.Grounded, "leave inclined pad by jumping");
        Check(_player.TryBeginDash(), "activate down dash after inclined departure");
        Check(Mathf.Abs(Mathf.RadToDeg(down.AngleTo(_player.Gravity))) > 30, "body-down route is distinct from gravity direction");
        Vector2 origin = _player.Position;
        bool arrived = false, hadZeroGravity = false;
        for (int frame = 0; frame < 1200; frame++)
        {
            yield return 1;
            if (_player.GravitySource == null) hadZeroGravity = true;
            if (_player.State == Player.MovementState.DownDash)
            {
                if (_player.BodyDown.DistanceTo(down) > 0.0001f)
                    throw new Exception("Gravity changed airborne body orientation.");
                if (Mathf.Abs((_player.Position - origin).Cross(down)) > 0.5f)
                    throw new Exception("Dash deviated from its locked line.");
            }
            else
            {
                GD.Print($"DASH END pos={_player.Position} reason={_player.LastDashEndReason}");
                arrived = _player.SupportPlanet?.Name == "Capsule";
                break;
            }
        }
        Check(arrived, "locked dash reaches destination capsule by actual collision");
        Check(hadZeroGravity, "route crosses space outside all gravity regions");
        Check(GetParent().GetNode<DashPlatform>("Breakable").Destroyed, "dash destroys breakable gate");
        Check(!GetParent().GetNode<DashPlatform>("PassGate").Destroyed, "pass gate survives traversal");
        Check(_player.Velocity.IsZeroApprox(), "destination impact does not rebound");
        if (Array.Exists(OS.GetCmdlineUserArgs(), s => s == "--route-preview"))
        {
            yield return 60;
            Capture("p002-arrival");
        }
        yield return 2;
        Check(_player.PendingCollisionRestores == 0, "collision exceptions restored after leaving gates");
        _player.TestAxis = 1;
        Vector2 landed = _player.Position;
        yield return 90;
        Check(_player.Position.DistanceTo(landed) > 40, "normal movement works at destination");
        _player.TestAxis = 0;
        _player.TestJump = true;
        yield return 8;
        Check(!_player.Grounded, "jump works after dash arrival");
    }
    private void Capture(string name)
    {
        if (!Array.Exists(OS.GetCmdlineUserArgs(), s => s == "--route-preview")) return;
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/test-results");
        Error error = GetViewport().GetTexture().GetImage().SavePng($"res://.godot/test-results/{name}.png");
        if (error != Error.Ok) throw new Exception($"Screenshot failed: {error}");
    }
}

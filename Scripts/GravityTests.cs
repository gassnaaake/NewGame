using Godot;
using System;
using System.Collections.Generic;

namespace PlanetLab;

// P-002.2 integration checks. Runs in the real GravityLab scene and physics loop.
public partial class GravityTests : Node
{
    private Player _player = null!;
    private Planet _sphere = null!;
    private Planet _capsule = null!;
    private GravityRegion _regionA = null!;
    private GravityRegion _regionCLeft = null!;
    private GravityRegion _regionCRight = null!;
    private DashPlatform _deck = null!;
    private IEnumerator<int> _steps = null!;
    private int _wait;
    private int _checks;

    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        _player = GetParent().GetNode<Player>("Player");
        _sphere = GetParent().GetNode<Planet>("Sphere");
        _capsule = GetParent().GetNode<Planet>("Capsule");
        _regionA = GetParent().GetNode<GravityRegion>("RegionA");
        _regionCLeft = GetParent().GetNode<GravityRegion>("RegionCLeft");
        _regionCRight = GetParent().GetNode<GravityRegion>("RegionCRight");
        _deck = GetParent().GetNode<DashPlatform>("Deck");
        _player.TestMode = true;
        _steps = Run().GetEnumerator();
    }

    public override void _PhysicsProcess(double delta)
    {
        try
        {
            if (_wait-- > 0) return;
            if (_steps.MoveNext()) _wait = Math.Max(0, _steps.Current - 1);
            else { GD.Print($"GRAVITY PASS: {_checks} checks"); GetTree().Quit(); SetPhysicsProcess(false); }
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
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

    private IEnumerable<int> Run()
    {
        yield return 120;

        Check(_player.Grounded && _player.SupportBody == _deck, "spawn settles on gravity test deck");
        Check(_player.CurrentGravityLayer == 0, "spawn uses highest-priority layer 0");
        Check(_player.HasActiveGravity, "lower-priority planet is still an active region");
        Check(_player.ActiveGravityRegions.Count >= 2, "overlapping local and planet regions are both reported");
        Check(_player.ContributingGravityRegions.Count == 1, "only the minimum layer contributes");
        Check(_player.GravityTotal.DistanceTo(Vector2.Down * 550) < 0.1f, "L0 region gravity is applied alone");
        Check(_player.BodyUp.Dot(Vector2.Up) > 0.999f, "grounded body aligns to deck support");

        GravitySelection atA = GravitySelector.Select(_player.GravitySources, new Vector2(-500, -170));
        Check(atA.HighestPriorityLayer == 0, "region A selects layer 0");
        Check(atA.Active.Count >= 2, "region A overlaps lower planet gravity");
        Check(atA.Contributing.Count == 1, "region A suppresses lower layers");
        Check(atA.Total.DistanceTo(Vector2.Down * 550) < 0.1f, "region A alone produces the total");

        GravitySelection inGap = GravitySelector.Select(_player.GravitySources, new Vector2(-375, -170));
        Check(inGap.HighestPriorityLayer == 1, "leaving A falls back to layer 1");
        Check(inGap.Contributing.Count == 1 && inGap.Contributing[0].Name == "Sphere",
            "gap recomputes the current planet source instead of retaining A");
        Check(inGap.Total.Length() > 1, "gap has finite non-zero planet gravity");

        var planetLayerBoost = new GravityRegion
        {
            Name = "PlanetLayerBoost", DisplayName = "L1 boost", GravityLayer = 1,
            Size = new Vector2(300, 300), Direction = Vector2.Down, GravityStrength = 100,
            Position = new Vector2(-375, -170)
        };
        GetParent().AddChild(planetLayerBoost);
        _player.RefreshGravitySources();
        GravitySelection planetPlusZone = GravitySelector.Select(_player.GravitySources, new Vector2(-375, -170));
        Check(planetPlusZone.Contributing.Count == 2, "planet and fixed region can share a layer");
        Check(planetPlusZone.Total.DistanceTo(inGap.Total + Vector2.Down * 100) < 0.1f,
            "planet and fixed region same-layer vectors add");
        planetLayerBoost.QueueFree();
        _player.RefreshGravitySources();
        yield return 1;

        GravitySelection atC = GravitySelector.Select(_player.GravitySources, new Vector2(-220, -170));
        Check(atC.HighestPriorityLayer == 0, "overlap C selects layer 0");
        Check(atC.Contributing.Count == 2, "both same-layer C regions contribute");
        Check(atC.Total.Length() < 0.001f, "equal and opposite same-layer vectors cancel to zero");
        Check(float.IsFinite(atC.Total.X) && float.IsFinite(atC.Total.Y), "zero total is finite");
        var reversedSources = new List<IGravitySource>(_player.GravitySources);
        reversedSources.Reverse();
        GravitySelection atCReversed = GravitySelector.Select(reversedSources, new Vector2(-220, -170));
        Check(atCReversed.HighestPriorityLayer == atC.HighestPriorityLayer,
            "source order does not change selected layer");
        Check(atCReversed.Total.DistanceTo(atC.Total) < 0.001f,
            "source order does not change same-layer sum");

        GravitySelection leftOnly = GravitySelector.Select(_player.GravitySources, new Vector2(-330, -170));
        Check(leftOnly.HighestPriorityLayer == 0 && leftOnly.Contributing.Count == 1,
            "left C-only area has one contributing region");
        Check(leftOnly.Total.DistanceTo(Vector2.Down * 550) < 0.1f, "left C-only vector is down");

        GravitySelection rightOnly = GravitySelector.Select(_player.GravitySources, new Vector2(-150, -170));
        Check(rightOnly.HighestPriorityLayer == 0 && rightOnly.Contributing.Count == 1,
            "right C-only area has one contributing region");
        Check(rightOnly.Total.DistanceTo(Vector2.Up * 550) < 0.1f, "right C-only vector is up");

        GravitySelection none = GravitySelector.Select(_player.GravitySources, new Vector2(0, -3000));
        Check(!none.HasActive && none.Total == Vector2.Zero, "no active region is zero gravity");

        float oldRegionRotation = _regionA.Rotation;
        _regionA.Rotation = Mathf.Pi / 2;
        bool rotatedInside = _regionA.TryGetGravity(_regionA.GlobalPosition, out Vector2 rotatedGravity);
        _regionA.Rotation = oldRegionRotation;
        Check(rotatedInside && rotatedGravity.DistanceTo(Vector2.Left * 550) < 0.1f,
            "fixed direction rotates with its region");

        // A: normal jump must return to the same platform under local flat gravity.
        _player.ResetAt(new Vector2(-500, -169));
        yield return 60;
        Check(_player.Grounded && _player.SupportBody == _deck, "A setup is grounded");
        Vector2 jumpStart = _player.GlobalPosition;
        _player.TestJump = true;
        yield return 180;
        Check(_player.Grounded && _player.SupportBody == _deck, "A jump lands back on the same deck");
        Check(Mathf.Abs(_player.GlobalPosition.X - jumpStart.X) < 4,
            "A jump does not drift along the platform");
        Check(_player.BodyUp.Dot(Vector2.Up) > 0.99f, "A landing body aligns to support");
        Camera2D? landingCamera = GetViewport().GetCamera2D();
        Check(landingCamera != null &&
              Mathf.Abs(Mathf.AngleDifference(landingCamera.GlobalRotation, Vector2.Up.AngleTo(_player.BodyUp))) < 0.2f,
            "A landing camera settles to body orientation");

        // Landing projection: no outward normal velocity after contact.
        _player.ResetAt(new Vector2(-500, -230), new Vector2(0, 300));
        bool landed = false;
        int contactFrame = -1;
        float maxOutward = 0;
        for (int i = 0; i < 240; i++)
        {
            yield return 1;
            if (!_player.Grounded) continue;
            if (!landed)
            {
                landed = true;
                contactFrame = i;
            }
            maxOutward = Mathf.Max(maxOutward, _player.Velocity.Dot(_player.SupportNormal));
            if (i - contactFrame >= 30) break;
        }
        Check(landed, "A drop makes contact");
        Check(maxOutward < 0.5f, "landing leaves no outward normal rebound");

        // C: zero net gravity keeps body/camera stable and preserves inertia.
        _player.ResetAt(new Vector2(-220, -169));
        yield return 60;
        Check(_player.Grounded, "C zero-g setup is grounded");
        Check(_player.Gravity.IsZeroApprox(), "C applies zero net gravity");
        Check(_player.ActiveGravityRegions.Count >= 3, "C still reports active same-layer and lower regions");
        Check(_player.ContributingGravityRegions.Count == 2, "C has two same-layer contributors");
        Vector2 bodyBefore = _player.BodyUp;
        float cameraBefore = GetViewport().GetCamera2D()?.GlobalRotation ?? 0;
        _player.TestJump = true;
        yield return 20;
        Check(_player.Gravity.IsZeroApprox(), "C remains zero gravity while still inside the overlap");
        Check(_player.Velocity.Length() > 100, "C jump preserves inertial velocity");
        Check(_player.BodyUp.DistanceTo(bodyBefore) < 0.01f, "C body orientation stays stable");
        Check(_player.UpDirection.Dot(Vector2.Up) > 0.99f,
            "C physical up stays stable independently of visual body");
        Check(Mathf.Abs((GetViewport().GetCamera2D()?.GlobalRotation ?? 0) - cameraBefore) < 0.01f,
            "C camera orientation stays stable");
        Check(Mathf.Abs(_player.GlobalRotation) < 0.0001f, "visual body rotation does not rotate collision body");

        // Two non-equal same-layer vectors must add, not cancel or override.
        var sumDown = new GravityRegion
        {
            Name = "SumDown", DisplayName = "Sum down", GravityLayer = 0,
            Size = new Vector2(300, 300), Direction = Vector2.Down, GravityStrength = 550,
            Position = new Vector2(0, -2500)
        };
        var sumRight = new GravityRegion
        {
            Name = "SumRight", DisplayName = "Sum right", GravityLayer = 0,
            Size = new Vector2(300, 300), Direction = Vector2.Right, GravityStrength = 300,
            Position = new Vector2(0, -2500)
        };
        GetParent().AddChild(sumDown);
        GetParent().AddChild(sumRight);
        _player.RefreshGravitySources();
        GravitySelection sumSelection = GravitySelector.Select(_player.GravitySources, new Vector2(0, -2500));
        Check(sumSelection.Contributing.Count == 2, "non-equal same-layer regions both contribute");
        Check(sumSelection.Total.DistanceTo(new Vector2(300, 550)) < 0.1f,
            "non-equal same-layer vectors add correctly");
        sumDown.QueueFree();
        sumRight.QueueFree();
        _player.RefreshGravitySources();
        yield return 1;

        // A tiny non-zero same-layer sum must be applied as zero and stay stable.
        var nearDown = new GravityRegion
        {
            Name = "NearDown", DisplayName = "Near down", GravityLayer = 0,
            Size = new Vector2(300, 300), Direction = Vector2.Down, GravityStrength = 550,
            Position = new Vector2(0, -2000)
        };
        var nearUp = new GravityRegion
        {
            Name = "NearUp", DisplayName = "Near up", GravityLayer = 0,
            Size = new Vector2(300, 300), Direction = Vector2.Up, GravityStrength = 549.2f,
            Position = new Vector2(0, -2000)
        };
        GetParent().AddChild(nearDown);
        GetParent().AddChild(nearUp);
        _player.RefreshGravitySources();
        _player.ResetAt(new Vector2(0, -2000));
        yield return 1;
        float nearTotal = _player.GravityTotal.Length();
        Check(nearTotal > 0.1f && nearTotal < 1.0f, "near-zero setup produces a small non-zero raw sum");
        Check(_player.Gravity.IsZeroApprox(), "near-zero raw sum is applied as zero gravity");
        Vector2 nearBody = _player.BodyUp;
        float nearCamera = GetViewport().GetCamera2D()?.GlobalRotation ?? 0;
        yield return 30;
        Check(_player.BodyUp.DistanceTo(nearBody) < 0.01f, "near-zero gravity keeps body stable");
        Check(Mathf.Abs((GetViewport().GetCamera2D()?.GlobalRotation ?? 0) - nearCamera) < 0.01f,
            "near-zero gravity keeps camera stable");
        nearDown.QueueFree();
        nearUp.QueueFree();
        _player.RefreshGravitySources();
        yield return 1;

        // A deliberate direction flip must turn body quickly and camera independently slower.
        var flip = new GravityRegion
        {
            Name = "FlipRegion",
            DisplayName = "Flip up",
            GravityLayer = 0,
            Size = new Vector2(300, 300),
            Direction = Vector2.Up,
            GravityStrength = 550,
            Position = new Vector2(0, -2000)
        };
        GetParent().AddChild(flip);
        _player.RefreshGravitySources();
        _player.ResetAt(new Vector2(0, -2000));
        yield return 1;
        Check(_player.Gravity.DistanceTo(Vector2.Up * 550) < 0.1f, "flip region supplies upward gravity");
        Check(_player.UpDirection.Dot(Vector2.Down) > 0.99f,
            "physical up follows new gravity immediately");
        Check(_player.BodyUp.Dot(Vector2.Down) < 0.5f,
            "visual body transitions separately from physical up");
        float bodyStart = Mathf.Abs(Vector2.Up.AngleTo(_player.BodyUp));
        float cameraStart = Mathf.Abs(GetViewport().GetCamera2D()?.GlobalRotation ?? 0);
        yield return 6;
        float bodyTurn = Mathf.Abs(Vector2.Up.AngleTo(_player.BodyUp));
        float cameraTurn = Mathf.Abs((GetViewport().GetCamera2D()?.GlobalRotation ?? 0) - cameraStart);
        Check(bodyTurn > bodyStart, "body starts continuous rotation after gravity flip");
        Check(bodyTurn > cameraTurn, "body turns faster than camera");
        yield return 60;
        Check(_player.BodyUp.Dot(Vector2.Down) > 0.98f, "body settles along flipped gravity");
        Check(float.IsFinite(_player.BodyUp.X) && float.IsFinite(_player.BodyUp.Y), "body orientation stays finite");
        flip.QueueFree();
        _player.RefreshGravitySources();
        yield return 1;

        // Dash direction is captured at activation and does not follow later orientation.
        _player.ResetAt(new Vector2(0, -3000));
        yield return 1;
        Vector2 beforeDash = _player.BodyDown;
        Check(_player.TryBeginDash(), "dash starts in isolated free space");
        Vector2 dashDirection = _player.DashDirection;
        Check(dashDirection.DistanceTo(beforeDash) < 0.0001f, "dash captures current body down");
        yield return 5;
        Check(_player.BodyDown.DistanceTo(beforeDash) < 0.0001f, "dash keeps body orientation locked");
        Check(_player.DashDirection.DistanceTo(dashDirection) < 0.0001f, "dash direction remains locked");
        Check(_player.Velocity.DistanceTo(dashDirection * _player.DownDashSpeed) < 0.01f,
            "dash ignores gravity and input while moving");

        // Spatial boundary sweep: selection must follow the current position, not history.
        _player.ResetAt(new Vector2(-500, -170));
        yield return 2;
        float[] sweepX = { -500, -460, -420, -380, -375, -340, -300, -260, -220, -180, -150 };
        int previousLayer = _player.CurrentGravityLayer;
        bool sawGapLayer = false;
        bool sawZeroOverlap = false;
        foreach (float x in sweepX)
        {
            _player.GlobalPosition = new Vector2(x, -170);
            _player.Velocity = Vector2.Zero;
            yield return 1;
            if (x == -375 && _player.CurrentGravityLayer == 1) sawGapLayer = true;
            if (x == -220 && _player.Gravity.IsZeroApprox() &&
                _player.ContributingGravityRegions.Count == 2) sawZeroOverlap = true;
            previousLayer = _player.CurrentGravityLayer;
        }
        Check(sawGapLayer, "high-speed boundary sweep observes the L1 gap");
        Check(sawZeroOverlap, "high-speed boundary sweep observes zero overlap");
        Check(_player.CurrentGravityLayer == 0, "boundary sweep ends in the C layer");
        Check(previousLayer == 0, "boundary sweep does not retain stale layer");

        // Physical high-speed crossing through L0 -> L2 -> L0 without teleporting.
        var speedA = new GravityRegion
        {
            Name = "SpeedA", DisplayName = "Speed A", GravityLayer = 0,
            Size = new Vector2(200, 2000), Direction = Vector2.Down, GravityStrength = 550,
            Position = new Vector2(-200, -3000)
        };
        var speedLow = new GravityRegion
        {
            Name = "SpeedLow", DisplayName = "Speed Low", GravityLayer = 2,
            Size = new Vector2(200, 2000), Direction = Vector2.Down, GravityStrength = 550,
            Position = new Vector2(0, -3000)
        };
        var speedCLeft = new GravityRegion
        {
            Name = "SpeedCLeft", DisplayName = "Speed C down", GravityLayer = 0,
            Size = new Vector2(200, 2000), Direction = Vector2.Down, GravityStrength = 550,
            Position = new Vector2(200, -3000)
        };
        var speedCRight = new GravityRegion
        {
            Name = "SpeedCRight", DisplayName = "Speed C up", GravityLayer = 0,
            Size = new Vector2(200, 2000), Direction = Vector2.Up, GravityStrength = 550,
            Position = new Vector2(300, -3000)
        };
        GetParent().AddChild(speedA);
        GetParent().AddChild(speedLow);
        GetParent().AddChild(speedCLeft);
        GetParent().AddChild(speedCRight);
        _player.RefreshGravitySources();
        _player.ResetAt(new Vector2(-400, -3000), new Vector2(1200, 0));
        bool sawL0 = false, sawL2 = false, sawL0Pair = false;
        for (int i = 0; i < 120; i++)
        {
            yield return 1;
            if (_player.GravitySourceLabel == "Speed A" && _player.CurrentGravityLayer == 0) sawL0 = true;
            if (_player.GravitySourceLabel == "Speed Low" && _player.CurrentGravityLayer == 2) sawL2 = true;
            if (_player.CurrentGravityLayer == 0 && _player.ContributingGravityRegions.Count == 2 &&
                _player.Gravity.IsZeroApprox()) sawL0Pair = true;
        }
        Check(sawL0, "physical high-speed crossing sees L0 A");
        Check(sawL2, "physical high-speed crossing falls back to L2");
        Check(sawL0Pair, "physical high-speed crossing sees the L0 zero pair");
        Check(float.IsFinite(_player.Gravity.X) && float.IsFinite(_player.BodyUp.Y),
            "physical high-speed crossing keeps finite state");
        speedA.QueueFree(); speedLow.QueueFree(); speedCLeft.QueueFree(); speedCRight.QueueFree();
        _player.RefreshGravitySources();
        yield return 1;

        yield return 30;
        Check(float.IsFinite(_player.Gravity.X) && float.IsFinite(_player.Gravity.Y),
            "gravity remains finite after boundary sweep");
        Check(float.IsFinite(_player.BodyUp.X) && float.IsFinite(_player.BodyUp.Y),
            "body remains finite after boundary sweep");
    }
}


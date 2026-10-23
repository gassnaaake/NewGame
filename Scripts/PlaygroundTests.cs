using Godot;
using System;
using System.Collections.Generic;

namespace PlanetLab;

// P-002.3 checks. Runs the real GravityPlayground scene and physics loop:
// gravity-zone selection, a per-frame landing probe, and a scripted input run
// from spawn to the capsule goal (no relay/teleport during the route).
public partial class PlaygroundTests : Node
{
    private PlaygroundLevel _level = null!;
    private Player _player = null!;
    private GoalZone _goal = null!;
    private DashPlatform _startDeck = null!;
    private DashPlatform _launchDeck = null!;
    private DashPlatform _ramp = null!;
    private DashPlatform _featherPad = null!;
    private IEnumerator<int> _steps = null!;
    private int _wait;
    private int _checks;

    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        _level = (PlaygroundLevel)GetParent();
        _player = _level.GetNode<Player>("Player");
        _goal = _level.GetNode<GoalZone>("Gameplay/GoalBeacon");
        _startDeck = _level.GetNode<DashPlatform>("World/Terrain/StartDeck");
        _launchDeck = _level.GetNode<DashPlatform>("World/Terrain/LaunchDeck");
        _ramp = _level.GetNode<DashPlatform>("World/Terrain/LaunchRamp");
        _featherPad = _level.GetNode<DashPlatform>("World/Terrain/FeatherPad");
        _player.TestMode = true;
        _steps = Run().GetEnumerator();
    }

    public override void _PhysicsProcess(double delta)
    {
        try
        {
            if (_wait-- > 0) return;
            if (_steps.MoveNext()) _wait = Math.Max(0, _steps.Current - 1);
            else { GD.Print($"PLAYGROUND PASS: {_checks} checks"); GetTree().Quit(); SetPhysicsProcess(false); }
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

    // 中文：验证 Inspector 元数据本身：说明只读、不序列化，且中文玩法分组存在。
    // 真正的中文排版/可读性仍需人在 Inspector 中检查。
    private void CheckInspectorHelp(Node node, string expectedGroup, string label)
    {
        bool helpFound = false, helpReadOnly = false, helpStored = false, groupFound = false;
        foreach (Godot.Collections.Dictionary property in node.GetPropertyList())
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            string propertyName = property["name"].AsStringName().ToString();
            if (propertyName == "UsageNote")
            {
                helpFound = true;
                helpReadOnly = usage.HasFlag(PropertyUsageFlags.ReadOnly);
                helpStored = usage.HasFlag(PropertyUsageFlags.Storage);
            }
            if (propertyName == expectedGroup && usage.HasFlag(PropertyUsageFlags.Group))
                groupFound = true;
        }
        Check(helpFound && helpReadOnly && !helpStored,
            $"{label}: Chinese usage note is read-only and not serialized");
        Check(groupFound, $"{label}: Chinese gameplay group is present");
    }

    private IEnumerable<int> Run()
    {
        yield return 120;
        GD.Print($"ANGLE up->left={Mathf.RadToDeg(Vector2.Up.AngleTo(Vector2.Left)):F1} " +
                 $"up->right={Mathf.RadToDeg(Vector2.Up.AngleTo(Vector2.Right)):F1}");

        Check(_player.Grounded && _player.SupportBody == _startDeck, "spawn settles on the start deck");
        Check(_player.CurrentGravityLayer == 0, "spawn uses the L0 start zone");
        Check(_player.GravityTotal.DistanceTo(Vector2.Down * 550) < 0.1f,
            "start zone applies flat 550 downward gravity");
        Check(_startDeck.Size.X >= 300, "start deck is wide enough for a normal jump test");

        // 中文：手摆平台必须由真实子碰撞组成；不得依赖运行时隐藏生成，也不得共用 Shape。
        Node2D terrain = _level.GetNode<Node2D>("World/Terrain");
        int platformCount = 0, missingShape = 0, duplicateShapeOrChild = 0;
        var shapes = new List<Shape2D>();
        foreach (Node node in terrain.GetChildren())
        {
            if (node is not DashPlatform platform) continue;
            platformCount++;
            if (platform.GetChildCount() != 1) duplicateShapeOrChild++;
            if (platform.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape is not RectangleShape2D shape)
            {
                missingShape++;
                continue;
            }
            if (shapes.Contains(shape)) duplicateShapeOrChild++;
            shapes.Add(shape);
        }
        Check(platformCount == 11, "all hand-placed platforms are present");
        Check(missingShape == 0, "every hand-placed platform has a real RectangleShape2D child");
        Check(duplicateShapeOrChild == 0,
            "platform shapes are independent and no runtime-injected collision child exists");

        CheckInspectorHelp(_level.GetNode<GravityRegion>("World/GravityZones/StartFlat_L0"),
            "① 常用玩法（优先调整）", "GravityRegion");
        CheckInspectorHelp(_level.GetNode<Planet>("World/Planets/Sphere"),
            "① 常用玩法（优先调整）", "Planet");
        CheckInspectorHelp(_goal, "① 终点触发（常用）", "GoalZone");

        // Zone policy by position, independent of trigger history.
        GravitySelection atStart = GravitySelector.Select(_player.GravitySources, new Vector2(-1560, -410));
        Check(atStart.HighestPriorityLayer == 0 && atStart.Contributing.Count == 1,
            "start point has exactly one L0 contributor");
        Check(atStart.Total.DistanceTo(Vector2.Down * 550) < 0.1f, "start point total is down 550");

        GravitySelection inFloat = GravitySelector.Select(_player.GravitySources, new Vector2(-750, -550));
        Check(inFloat.HighestPriorityLayer == 1 && inFloat.Contributing.Count == 1,
            "outside the island only the L1 float zone contributes");
        Check(inFloat.Total.DistanceTo(Vector2.Down * 260) < 0.1f, "L1 float zone is weak downward gravity");

        GravitySelection onIsland = GravitySelector.Select(_player.GravitySources, new Vector2(-400, -560));
        Check(onIsland.HighestPriorityLayer == 0 && onIsland.Contributing.Count == 1,
            "L0 island overrides the L1 float zone");
        Check(onIsland.Total.DistanceTo(Vector2.Down * 550) < 0.1f, "island restores normal gravity");

        GravitySelection inSum = GravitySelector.Select(_player.GravitySources, new Vector2(100, -650));
        Check(inSum.HighestPriorityLayer == 0 && inSum.Contributing.Count == 2,
            "sum zone has two same-layer contributors");
        Check(inSum.Total.DistanceTo(new Vector2(300, 550)) < 0.1f,
            "same-layer down/right vectors add to (300, 550)");

        GravitySelection onBridge = GravitySelector.Select(_player.GravitySources, new Vector2(410, -650));
        Check(onBridge.HighestPriorityLayer == 0 && onBridge.Contributing.Count == 1,
            "bridge returns to a single L0 zone");
        Check(onBridge.Total.DistanceTo(Vector2.Down * 550) < 0.1f, "bridge gravity is flat down");

        GravitySelection onLaunch = GravitySelector.Select(_player.GravitySources, new Vector2(700, -620));
        Check(onLaunch.HighestPriorityLayer == 0 && onLaunch.Contributing.Count == 1,
            "launch deck uses its own L0 zone");
        Check(onLaunch.Total.DistanceTo(new Vector2(142.35f, 531.30f)) < 0.2f,
            "launch gravity is tilted 15 degrees toward the capsule");

        GravitySelection feather = GravitySelector.Select(_player.GravitySources, new Vector2(1680, 1000));
        Check(feather.HighestPriorityLayer == 0 && feather.Contributing.Count == 1,
            "feather pad L0 overrides the capsule L1");
        Check(feather.Total.DistanceTo(Vector2.Down * 40) < 0.1f, "feather pad is near-weightless g=40");

        GravitySelection onCapsule = GravitySelector.Select(_player.GravitySources, new Vector2(1460, 1050));
        Check(onCapsule.HighestPriorityLayer == 1 && onCapsule.Total.Length() > 500f,
            "capsule influence is an L1 gravity source at the landing area");

        GravitySelection inSpace = GravitySelector.Select(_player.GravitySources, new Vector2(0, -3000));
        Check(!inSpace.HasActive && inSpace.Total == Vector2.Zero, "deep space outside all zones is zero gravity");

        // Frame data: landing on a flat local-gravity deck and on the sphere.
        CollisionShape2D? deckShape = _startDeck.GetChildOrNull<CollisionShape2D>(0);
        CollisionShape2D? playerShape = _player.GetChildOrNull<CollisionShape2D>(0);
        GD.Print($"DECK pos={_startDeck.GlobalPosition} size={_startDeck.Size} children={_startDeck.GetChildCount()} " +
                 $"shape={(deckShape?.Shape as RectangleShape2D)?.Size} disabled={deckShape?.Disabled} " +
                 $"layer={_startDeck.CollisionLayer} mask={_startDeck.CollisionMask}");
        GD.Print($"DECKSHAPE global={deckShape?.GlobalPosition} local={deckShape?.Position} " +
                 $"rot={deckShape?.GlobalRotation} scale={deckShape?.GlobalScale}");
        GD.Print($"PLAYER pos={_player.GlobalPosition} layer={_player.CollisionLayer} mask={_player.CollisionMask} " +
                 $"shape={(playerShape?.Shape as CircleShape2D)?.Radius}");
        foreach (int step in ProbeLanding("flat deck", new Vector2(-1500, -430), new Vector2(0, 300), _startDeck))
            yield return step;
        foreach (int step in ProbeLanding("sphere", new Vector2(-1500, -180), new Vector2(0, 300), null))
            yield return step;

        // A normal jump under the local flat zone must return to the same deck.
        _player.ResetAt(new Vector2(-1560, -410));
        yield return 60;
        Check(_player.Grounded && _player.SupportBody == _startDeck, "jump setup is grounded on the start deck");
        float jumpX = _player.GlobalPosition.X;
        _player.TestJump = true;
        yield return 240;
        Check(_player.Grounded && _player.SupportBody == _startDeck,
            "normal jump lands back on the same start deck");
        Check(Mathf.Abs(_player.GlobalPosition.X - jumpX) < 4,
            "normal jump is not pulled sideways by planet gravity");

        // Camera orientation must use the signed body angle on a left-leaning ramp.
        _player.ResetAt(new Vector2(893.0f, -648.4f));
        yield return 120;
        Check(_player.Grounded && _player.SupportBody == _ramp, "ramp diagnostic setup is grounded");
        float signedBody = Mathf.Atan2(_player.BodyUp.X, -_player.BodyUp.Y);
        float cameraAngle = GetViewport().GetCamera2D()?.GlobalRotation ?? 0;
        GD.Print($"RAMP CAMERA bodyUp={_player.BodyUp} signed={Mathf.RadToDeg(signedBody):F2} " +
                 $"camera={Mathf.RadToDeg(cameraAngle):F2}");
        Check(signedBody < -0.2f, "ramp body up leans left");
        Check(Mathf.Abs(Mathf.AngleDifference(cameraAngle, signedBody)) < 0.08f,
            "camera follows the signed body angle, not an unsigned 180-degree flip");

        // The feather pad must allow a long, slow jump under g=40.
        _player.ResetAt(new Vector2(1680, 1010));
        yield return 200;
        Check(_player.Grounded && _player.SupportBody == _featherPad, "feather pad setup is grounded");
        Check(_player.Gravity.Length() < 41, "feather pad applies near-zero gravity");
        float featherY = _player.GlobalPosition.Y;
        _player.TestJump = true;
        yield return 45;
        Check(!_player.Grounded && _player.GlobalPosition.Y < featherY - 40,
            "feather pad jump floats slowly above the pad");
        float highestFeather = float.MaxValue;
        for (int i = 0; i < 500; i++)
        {
            yield return 1;
            highestFeather = Mathf.Min(highestFeather, _player.GlobalPosition.Y);
        }
        Check(highestFeather > 800, "feather ceiling keeps the low-gravity jump inside the pocket");
        Check(_player.Grounded && _player.SupportBody == _featherPad,
            "feather jump returns to the pad instead of floating away");

        // Full route: only TestAxis/TestJump plus one dashed departure from the ramp.
        _player.ResetAt(new Vector2(-1560, -410));
        yield return 60;
        Check(_player.Grounded && _player.SupportBody == _startDeck, "route is reset to the real spawn");
        bool rampJumped = false;
        int rampAirFrames = 0;
        bool wasDashing = false;
        string lastSupport = "";
        float previousCamera = GetViewport().GetCamera2D()?.GlobalRotation ?? 0;
        float maxCameraStep = 0;
        for (int frame = 0; frame < 6000; frame++)
        {
            if (_level.GoalReached) break;
            if (_player.State == Player.MovementState.DownDash)
            {
                wasDashing = true;
                yield return 1;
                continue;
            }
            if (wasDashing)
            {
                wasDashing = false;
                GD.Print($"ROUTE DASH END frame={frame} pos={_player.Position} reason={_player.LastDashEndReason} " +
                         $"vel={_player.Velocity} support={_player.SupportBody?.Name} grounded={_player.Grounded} " +
                         $"bodyDown={_player.BodyDown}");
            }

            _player.TestAxis = 1;
            if (_player.Grounded && _player.SupportBody is DashPlatform platform)
            {
                string supportName = platform.Name.ToString();
                if (supportName != lastSupport)
                {
                    GD.Print($"ROUTE frame={frame} support={supportName} pos={_player.GlobalPosition} speed={_player.Velocity}");
                    lastSupport = supportName;
                }
                float localX = platform.ToLocal(_player.GlobalPosition).X;
                float edge = platform.Size.X * 0.5f;
                if (platform == _ramp && localX >= edge - 5 && !rampJumped)
                {
                    _player.TestJump = true;
                    rampJumped = true;
                    rampAirFrames = 0;
                }
                else if (platform != _ramp && platform != _launchDeck && localX >= edge - 8)
                {
                    _player.TestJump = true;
                }
            }
            else if (!_player.Grounded && rampJumped)
            {
                _player.TestAxis = 0;
                rampAirFrames++;
                if (rampAirFrames < 2) { yield return 1; continue; }
                rampJumped = false;
                GD.Print($"ROUTE frame={frame} ramp departure bodyDown={_player.BodyDown} pos={_player.GlobalPosition}");
                Check(_player.BodyDown.Dot(new Vector2(0.258819f, 0.965926f)) > 0.98f,
                    "ramp takeoff keeps the body-down aimed at the capsule");
                Check(_player.TryBeginDash(), "route starts the down dash after leaving the ramp");
                yield return 1;
                continue;
            }

            yield return 1;
            float cameraNow = GetViewport().GetCamera2D()?.GlobalRotation ?? 0;
            maxCameraStep = Mathf.Max(maxCameraStep, Mathf.Abs(Mathf.AngleDifference(cameraNow, previousCamera)));
            previousCamera = cameraNow;
            if (frame % 120 == 0)
                GD.Print($"ROUTE frame={frame} pos={_player.Position} support={_player.SupportBody?.Name} " +
                         $"grounded={_player.Grounded} layer={_player.CurrentGravityLayer} vel={_player.Velocity}");
        }

        GD.Print($"ROUTE CAMERA max physics-frame step={Mathf.RadToDeg(maxCameraStep):F2} deg");
        Check(maxCameraStep < 0.35f, "route camera never snaps in a single physics frame");
        Check(_level.GoalReached, "scripted input reaches the capsule goal without relocation");
        Check(_goal.Reached, "goal area records the arrival");
        _player.TestAxis = 0;
        yield return 30;
        Check(_player.SupportBody == _level.GetNode<Planet>("World/Planets/Capsule"),
            "the player finishes grounded on the capsule planet");
        Check(_player.Position.DistanceTo(new Vector2(1500, 1070)) < 120,
            "the finish is at the capsule beacon, not a distant trigger");
        Check(_player.PendingCollisionRestores == 0,
            "dash-through ramp exception is restored after the goal");
    }

    private IEnumerable<int> ProbeLanding(string label, Vector2 start, Vector2 velocity, DashPlatform? expectedDeck)
    {
        _player.ResetAt(start, velocity);
        bool landed = false;
        int contactFrame = -1;
        float maxOutward = 0;
        for (int i = 0; i < 240; i++)
        {
            yield return 1;
            if (!_player.Grounded)
            {
                if (i % 12 == 0)
                    GD.Print($"APPROACH {label} f={i} x={_player.GlobalPosition.X:F2} y={_player.GlobalPosition.Y:F2} " +
                             $"v={_player.Velocity} upward={_player.UpDirection.ToString("F2")} gravity={_player.Gravity.ToString("F1")}");
                continue;
            }
            if (!landed)
            {
                landed = true;
                contactFrame = i;
                GD.Print($"CONTACT {label} support={_player.SupportBody?.Name} " +
                         $"pos={_player.GlobalPosition} layer={_player.CurrentGravityLayer}");
                Check(expectedDeck == null || _player.SupportBody == expectedDeck,
                    $"{label}: landing support is expected (actual {_player.SupportBody?.Name})");
            }
            if (i - contactFrame <= 12)
            {
                float outward = _player.Velocity.Dot(_player.SupportNormal);
                maxOutward = Mathf.Max(maxOutward, outward);
                GD.Print($"LANDING {label} f={i} x={_player.GlobalPosition.X:F2} y={_player.GlobalPosition.Y:F3} " +
                         $"v={_player.Velocity} n={_player.SupportNormal.ToString("F2")} outward={outward:F3} " +
                         $"state={_player.State} support={_player.SupportBody?.Name} " +
                         $"up={_player.UpDirection.ToString("F2")} bodyUp={_player.BodyUp.ToString("F2")} " +
                         $"cam={Mathf.RadToDeg(GetViewport().GetCamera2D()?.GlobalRotation ?? 0):F2}");
            }
            if (i - contactFrame >= 30) break;
        }
        Check(landed, $"{label}: player makes contact");
        Check(maxOutward < 0.5f, $"{label}: no outward normal rebound in the 12 frames after contact");
    }
}

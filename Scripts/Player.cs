using Godot;
using System.Collections.Generic;

namespace PlanetLab;

public partial class Player : CharacterBody2D
{
    public enum MovementState { Normal, DownDash }

    [Export] public float DownDashSpeed { get; set; } = 1200;
    [Export] public Key DownDashKey { get; set; } = Key.J;
    public MovementState State { get; private set; }
    public Vector2 DashDirection { get; private set; }
    public string LastDashEndReason { get; private set; } = "None";
    private bool _dashRequested;

    [Export] public float GroundAcceleration { get; set; } = 650;
    [Export] public float GroundDeceleration { get; set; } = 800;
    [Export] public float MaxGroundSpeed { get; set; } = 200;
    [Export] public float JumpSpeed { get; set; } = 290;
    [Export] public float AirAcceleration { get; set; } = 65;
    [Export] public float AirControlSpeed { get; set; } = 200;
    [Export] public float MaxLaunchSpeed { get; set; } = 1100;
    [Export] public float LaunchSpeedPerPixel { get; set; } = 4;
    [Export] public float MinimumDragPixels { get; set; } = 6;
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float SupportProbeDistance { get; set; } = 0.2f;

    // Body rotation is visual only. The collision circle never rotates.
    // Grounded and airborne responses are separate so landing can be crisp
    // without making every airborne gravity change snap the character.
    [Export(PropertyHint.Range, "0.1,60,0.1")] public float GroundedBodyRotationResponse { get; set; } = 24;
    [Export(PropertyHint.Range, "0.1,60,0.1")] public float AirborneBodyRotationResponse { get; set; } = 12;
    [Export(PropertyHint.Range, "0,20,0.1")] public float GravityDeadzone { get; set; } = 1;

    public const float Radius = 12;
    public IGravitySource? GravitySource =>
        _gravitySelection.Contributing.Count == 0 ? null : _gravitySelection.Contributing[0].Source;
    public string GravitySourceLabel =>
        _gravitySelection.Contributing.Count == 0
            ? "None (inertia)"
            : _gravitySelection.Contributing[0].Name;
    public int CurrentGravityLayer => _gravitySelection.HighestPriorityLayer;
    public Vector2 GravityTotal => _gravitySelection.Total;
    public IReadOnlyList<GravityContribution> ActiveGravityRegions => _gravitySelection.Active;
    public IReadOnlyList<GravityContribution> ContributingGravityRegions => _gravitySelection.Contributing;
    public bool HasActiveGravity => _gravitySelection.HasActive;
    public IReadOnlyList<IGravitySource> GravitySources => _gravitySources;

    public Planet? SupportPlanet { get; private set; }
    public PhysicsBody2D? SupportBody { get; private set; }
    private readonly HashSet<DashPlatform> _ignoredPlatforms = new();
    public int PendingCollisionRestores => _ignoredPlatforms.Count;
    public Vector2 Gravity { get; private set; }
    public Vector2 SupportNormal { get; private set; } = Vector2.Up;
    public bool Grounded { get; private set; }
    public bool Aiming { get; private set; }
    public Vector2 LaunchVelocity { get; private set; }
    public bool TestMode { get; set; }
    public float TestAxis { get; set; }
    public bool TestJump { get; set; }
    public Vector2 BodyUp => _visualUp;
    public Vector2 BodyDown => -_visualUp;

    private readonly List<IGravitySource> _gravitySources = new();
    private readonly GravitySelection _gravitySelection = new();
    private Vector2 _aimStart;
    private Vector2 _visualUp = Vector2.Up;
    private Vector2 _physicalUp = Vector2.Up;
    private PhysicsBody2D? _lastSupportBody;
    private bool _skipGroundControl;
    private readonly KinematicCollision2D _supportProbe = new();

    public override void _Ready()
    {
        FloorSnapLength = 0;
        FloorStopOnSlope = false;
        FloorConstantSpeed = false;
        SafeMargin = 0.08f;
        FloorMaxAngle = Mathf.DegToRad(75);
        RefreshGravitySources();
    }

    // Regions and planets add themselves to "gravity_sources" from _EnterTree.
    // Tests that create a region after _Ready must call this once.
    public void RefreshGravitySources()
    {
        _gravitySources.Clear();
        foreach (Node node in GetTree().GetNodesInGroup("gravity_sources"))
            if (node is IGravitySource source)
                _gravitySources.Add(source);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (TestMode) return;
        if (input is InputEventKey dashKey && dashKey.Pressed && !dashKey.Echo && dashKey.PhysicalKeycode == DownDashKey)
            _dashRequested = true;
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
        {
            if (mouse.Pressed) BeginAim(mouse.Position);
            else if (Aiming) ReleaseAim(mouse.Position);
        }
        if (input is InputEventMouseMotion motion && Aiming) UpdateAim(motion.Position);
        if (input is InputEventKey key && key.Pressed && key.Keycode == Key.Escape) CancelAim();
    }

    public bool BeginAim(Vector2 screenPosition)
    {
        if (!Grounded || Aiming || State != MovementState.Normal) return false;
        Aiming = true;
        _aimStart = screenPosition;
        LaunchVelocity = Vector2.Zero;
        return true;
    }

    public void UpdateAim(Vector2 screenPosition)
    {
        if (!Aiming) return;
        Vector2 drag = _aimStart - screenPosition;
        float cameraAngle = GetViewport().GetCamera2D()?.GlobalRotation ?? 0;
        LaunchVelocity = drag.Length() < MinimumDragPixels ? Vector2.Zero
            : drag.Rotated(cameraAngle).LimitLength(MaxLaunchSpeed / Mathf.Max(0.001f, LaunchSpeedPerPixel)) * LaunchSpeedPerPixel;
        QueueRedraw();
    }

    public void ReleaseAim(Vector2 screenPosition)
    {
        if (!Aiming) return;
        UpdateAim(screenPosition);
        Aiming = false;
        if (!LaunchVelocity.IsZeroApprox())
        {
            Velocity = LaunchVelocity;
            Grounded = false;
            SupportPlanet = null;
            SupportBody = null;
            _skipGroundControl = true;
        }
        QueueRedraw();
    }

    public void CancelAim() { Aiming = false; LaunchVelocity = Vector2.Zero; QueueRedraw(); }

    public void ResetAt(Vector2 position, Vector2 velocity = default)
    {
        State = MovementState.Normal;
        foreach (DashPlatform platform in _ignoredPlatforms)
            if (IsInstanceValid(platform)) RemoveCollisionExceptionWith(platform);
        _ignoredPlatforms.Clear();
        SupportBody = null;
        _lastSupportBody = null;
        DashDirection = Vector2.Zero;
        LastDashEndReason = "Reset";
        _dashRequested = false;
        GlobalPosition = position;
        Velocity = velocity;
        Grounded = false;
        SupportPlanet = null;
        Gravity = Vector2.Zero;
        _gravitySelection.Clear();
        SupportNormal = Vector2.Up;
        _visualUp = Vector2.Up;
        _physicalUp = Vector2.Up;
        _skipGroundControl = true;
        TestAxis = 0;
        TestJump = false;
        CancelAim();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        bool hadSupport = Grounded;
        RestoreClearedPlatforms();

        // 中文：每物理帧按当前位置重新选择重力源；不缓存进入历史或上一个来源。
        GravitySelector.Select(_gravitySources, GlobalPosition, _gravitySelection);
        float deadzone = Mathf.Max(0, GravityDeadzone);
        Gravity = _gravitySelection.Total.LengthSquared() > deadzone * deadzone
            ? _gravitySelection.Total
            : Vector2.Zero;
        bool hasUsableGravity = !Gravity.IsZeroApprox();

        if (_dashRequested) { TryBeginDash(); _dashRequested = false; }
        if (State == MovementState.DownDash) { StepDash(dt); QueueRedraw(); return; }
        if (Aiming) { QueueRedraw(); return; }

        Vector2 physicalUp;
        if (Grounded && !SupportNormal.IsZeroApprox())
            physicalUp = SupportNormal;
        else if (hasUsableGravity)
            physicalUp = -Gravity.Normalized();
        else
            physicalUp = _physicalUp;
        if (physicalUp.LengthSquared() < 0.000001f) physicalUp = Vector2.Up;
        UpDirection = physicalUp.Normalized();
        if (Grounded || hasUsableGravity) _physicalUp = UpDirection;

        Vector2 controlUp = Grounded ? UpDirection : BodyUp;
        if (controlUp.LengthSquared() < 0.000001f) controlUp = UpDirection;
        Vector2 tangent = new(-controlUp.Y, controlUp.X);
        float axis = TestMode ? TestAxis : (Input.IsPhysicalKeyPressed(Key.D) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) ? 1 : 0);
        bool jump = TestMode ? TestJump : Input.IsActionJustPressed("jump");
        TestJump = false;

        if (Grounded && !_skipGroundControl)
        {
            float speed = Velocity.Dot(tangent);
            float target = axis * MaxGroundSpeed;
            float next = Mathf.MoveToward(speed, target, (axis == 0 ? GroundDeceleration : GroundAcceleration) * dt);
            Velocity += tangent * (next - speed);
            if (jump)
            {
                Velocity = tangent * Velocity.Dot(tangent) + SupportNormal * JumpSpeed;
                Grounded = false;
                SupportPlanet = null;
                SupportBody = null;
            }
        }
        else if (axis != 0)
        {
            float speed = Velocity.Dot(tangent);
            // Air input cannot erase launch momentum or impose an airborne speed cap.
            if (speed * axis < AirControlSpeed)
                Velocity += tangent * axis * Mathf.Min(AirAcceleration * dt, AirControlSpeed - speed * axis);
        }
        _skipGroundControl = false;

        Velocity += Gravity * dt;
        MoveAndSlide();
        Grounded = IsOnFloor();
        SupportPlanet = null;
        SupportBody = null;

        if (Grounded)
        {
            SupportNormal = GetFloorNormal().Normalized();
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                KinematicCollision2D collision = GetSlideCollision(i);
                if (collision.GetNormal().Dot(UpDirection) >= Mathf.Cos(FloorMaxAngle) && collision.GetCollider() is PhysicsBody2D body)
                {
                    SupportBody = body;
                    SupportPlanet = body as Planet;
                }
            }
        }
        else if (hasUsableGravity && SupportProbeDistance > 0 &&
                 TestMove(GlobalTransform, -UpDirection * SupportProbeDistance, _supportProbe, 0.01f))
        {
            // 中文：支撑探针只观察亚像素接触，不移动/吸附角色；不接受正在分离的接触，
            // 避免起跳或高速离地时被错误判定为接地。
            // Observe subpixel support without moving/snapping the body. Never accept
            // a separating contact (jump / tangent launch / high-speed detachment).
            Vector2 normal = _supportProbe.GetNormal().Normalized();
            if (normal.Dot(UpDirection) >= Mathf.Cos(FloorMaxAngle) &&
                Velocity.Dot(normal) <= 0.5f && _supportProbe.GetCollider() is PhysicsBody2D body)
            {
                Grounded = true;
                SupportNormal = normal;
                SupportBody = body;
                SupportPlanet = body as Planet;
            }
        }

        // 中文：首次接地或支撑体变化时，只去掉法线速度，保留切向速度；
        // 不在弯曲胶囊上每帧投影，避免与碰撞响应互相打架。
        // Touchdown consumes only the normal component. A support probe can report
        // contact just before MoveAndSlide has removed incoming normal velocity.
        // Project again when the support body changes, but not every frame on a
        // curved capsule where a probe normal can fight the collision response.
        bool supportChanged = Grounded && SupportBody != null && SupportBody != _lastSupportBody;
        if (Grounded && (!hadSupport || supportChanged))
            Velocity = Velocity.Slide(SupportNormal);
        _lastSupportBody = Grounded ? SupportBody : null;

        UpdateBodyOrientation(dt, hasUsableGravity);
        QueueRedraw();
    }

    private void UpdateBodyOrientation(float dt, bool hasUsableGravity)
    {
        if (State == MovementState.DownDash) return;

        // 中文：视觉朝向目标：接地用支撑法线，空中用有效重力反方向；
        // 无/近零重力时保持上一次可靠朝向，不用微小合力乱转。
        Vector2 target;
        if (Grounded && !SupportNormal.IsZeroApprox())
            target = SupportNormal;
        else if (hasUsableGravity)
            target = -Gravity.Normalized();
        else
            return; // Stable near-zero gravity: keep the last reliable orientation.

        if (target.LengthSquared() < 0.000001f) return;
        target = target.Normalized();
        float response = Mathf.Max(0, Grounded ? GroundedBodyRotationResponse : AirborneBodyRotationResponse);
        if (response <= 0)
        {
            _visualUp = target;
            return;
        }

        float nextAngle = Mathf.LerpAngle(
            _visualUp.Angle(),
            target.Angle(),
            1 - Mathf.Exp(-response * dt));
        _visualUp = Vector2.FromAngle(nextAngle).Normalized();
    }

    // 中文：下砸只允许在空中发动；锁定发动瞬间的视觉脚下方向，之后不再跟随重力。
    public bool TryBeginDash()
    {
        if (Grounded || Aiming || State != MovementState.Normal) return false;
        DashDirection = BodyDown.LengthSquared() > 0.000001f ? BodyDown.Normalized() : Vector2.Down;
        State = MovementState.DownDash;
        Velocity = DashDirection * Mathf.Max(1, DownDashSpeed);
        return true;
    }

    private void StepDash(float dt)
    {
        // 中文：下砸是直线定速扫掠；遇到普通实体停止，遇到易碎/穿越平台则按既有规则
        // 破坏或添加临时碰撞例外，并把剩余位移继续用完。
        Velocity = DashDirection * Mathf.Max(1, DownDashSpeed);
        Vector2 remaining = Velocity * dt;
        while (!remaining.IsZeroApprox())
        {
            KinematicCollision2D? hit = MoveAndCollide(remaining);
            if (hit == null) return;
            if (hit.GetCollider() is DashPlatform platform && platform.Kind != DashPlatform.PlatformKind.Solid)
            {
                // Per-player exception: other normal bodies still collide with pass platforms.
                if (!_ignoredPlatforms.Add(platform))
                {
                    EndDash(hit);
                    LastDashEndReason = "Repeated collision (safety stop)";
                    return;
                }
                AddCollisionExceptionWith(platform);
                if (platform.Kind == DashPlatform.PlatformKind.Breakable) platform.Break();
                remaining = hit.GetRemainder();
                continue;
            }
            EndDash(hit);
            return;
        }
    }

    private void RestoreClearedPlatforms()
    {
        foreach (DashPlatform platform in new List<DashPlatform>(_ignoredPlatforms))
        {
            if (!IsInstanceValid(platform)) { _ignoredPlatforms.Remove(platform); continue; }
            // This also runs after a dash hits a solid inside an overlapping pass
            // platform: don't re-enable contact until the whole character has exited.
            if (!platform.Destroyed && platform.OverlapsPlayer(GlobalPosition, Radius)) continue;
            RemoveCollisionExceptionWith(platform);
            _ignoredPlatforms.Remove(platform);
        }
    }

    private void EndDash(KinematicCollision2D hit)
    {
        State = MovementState.Normal;
        LastDashEndReason = $"Blocked by {(hit.GetCollider() as Node)?.Name}";
        // Dash impact stops the dash, rather than reflecting or turning its velocity.
        Velocity = Vector2.Zero;
        SupportNormal = hit.GetNormal().Normalized();
        Grounded = !Gravity.IsZeroApprox() &&
                   SupportNormal.Dot(-Gravity.Normalized()) > Mathf.Cos(FloorMaxAngle);
        SupportPlanet = Grounded ? hit.GetCollider() as Planet : null;
        SupportBody = Grounded ? hit.GetCollider() as PhysicsBody2D : null;
        _skipGroundControl = true;
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, new Color("f9d47a"));
        Vector2 side = new(-_visualUp.Y, _visualUp.X);
        DrawColoredPolygon(new[] { _visualUp * 23, _visualUp * 9 + side * 6, _visualUp * 9 - side * 6 }, Colors.White);
        if (!Gravity.IsZeroApprox()) DrawLine(Vector2.Zero, Gravity.Normalized() * 48, new Color("ff8190"), 2, true);
        if (Grounded) DrawLine(Vector2.Zero, SupportNormal * 38, new Color("7ef0bb"), 2, true);
        if (Aiming && !LaunchVelocity.IsZeroApprox())
        {
            Vector2 end = LaunchVelocity / 4;
            Vector2 direction = end.Normalized();
            DrawLine(Vector2.Zero, end, Colors.White, 3, true);
            DrawLine(end, end - direction.Rotated(0.5f) * 18, Colors.White, 3, true);
            DrawLine(end, end - direction.Rotated(-0.5f) * 18, Colors.White, 3, true);
        }
    }
}

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
    public const float Radius = 12;
    public Planet? GravitySource { get; private set; }
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
    private readonly List<Planet> _planets = new();
    private Vector2 _aimStart;
    private Vector2 _visualUp = Vector2.Up;
    private bool _skipGroundControl;
    private readonly KinematicCollision2D _supportProbe = new();

    public override void _Ready()
    {
        foreach (Node node in GetTree().GetNodesInGroup("planets"))
            _planets.Add((Planet)node);
        FloorSnapLength = 0;
        FloorStopOnSlope = false;
        FloorConstantSpeed = false;
        SafeMargin = 0.08f;
        FloorMaxAngle = Mathf.DegToRad(75);
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
        DashDirection = Vector2.Zero;
        LastDashEndReason = "Reset";
        _dashRequested = false;
        GlobalPosition = position;
        Velocity = velocity;
        Grounded = false;
        SupportPlanet = null;
        GravitySource = null;
        Gravity = Vector2.Zero;
        SupportNormal = Vector2.Up;
        _visualUp = Vector2.Up;
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
        GravitySource = GravitySelector.Select(_planets, GlobalPosition);
        Gravity = GravitySource?.GravityAt(GlobalPosition) ?? Vector2.Zero;
        if (_dashRequested) { TryBeginDash(); _dashRequested = false; }
        if (State == MovementState.DownDash) { StepDash(dt); QueueRedraw(); return; }
        if (Aiming) { QueueRedraw(); return; }

        Vector2 up = Grounded ? SupportNormal : Gravity.IsZeroApprox() ? _visualUp : -Gravity.Normalized();
        UpDirection = up;
        Vector2 controlUp = Grounded ? up : BodyUp;
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
        else if (!Gravity.IsZeroApprox() && SupportProbeDistance > 0 &&
                 TestMove(GlobalTransform, -UpDirection * SupportProbeDistance, _supportProbe, 0.01f))
        {
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
        // Touchdown consumes only the normal component. A support probe can report
        // contact just before MoveAndSlide has removed incoming normal velocity.
        // Conversely curved slide contacts can leave a small outward component.
        if (Grounded && !hadSupport) Velocity = Velocity.Slide(SupportNormal);
        if (Grounded) _visualUp = SupportNormal;
        QueueRedraw();
    }

    public bool TryBeginDash()
    {
        if (Grounded || Aiming || State != MovementState.Normal) return false;
        DashDirection = BodyDown.Normalized();
        State = MovementState.DownDash;
        Velocity = DashDirection * Mathf.Max(1, DownDashSpeed);
        return true;
    }

    private void StepDash(float dt)
    {
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
        Grounded = !Gravity.IsZeroApprox() && SupportNormal.Dot(-Gravity.Normalized()) > Mathf.Cos(FloorMaxAngle);
        SupportPlanet = Grounded ? hit.GetCollider() as Planet : null;
        SupportBody = Grounded ? hit.GetCollider() as PhysicsBody2D : null;
        if (Grounded) _visualUp = SupportNormal;
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

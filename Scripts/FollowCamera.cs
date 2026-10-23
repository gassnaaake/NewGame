using Godot;

namespace PlanetLab;

public partial class FollowCamera : Camera2D
{
    // 中文：摄像机独立于角色，按自身响应缓慢追随 BodyUp；不是旋转角色节点。
    [Export] public NodePath TargetPath { get; set; } = new("../Player");
    [Export(PropertyHint.Range, "0.1,30,0.1")] public float RotationResponse { get; set; } = 5;
    [Export(PropertyHint.Range, "0.2,1.5,0.05")] public float ViewZoom { get; set; } = 0.65f;
    private Player _target = null!;
    public override void _Ready()
    {
        _target = GetNode<Player>(TargetPath);
        IgnoreRotation = false;
        Zoom = Vector2.One * ViewZoom;
        GlobalPosition = _target.GlobalPosition;
    }
    public override void _Process(double delta)
    {
        GlobalPosition = _target.GlobalPosition;
        Vector2 bodyUp = _target.BodyUp;
        if (bodyUp.LengthSquared() < 0.000001f ||
            !float.IsFinite(bodyUp.X) || !float.IsFinite(bodyUp.Y))
            return;

        float angle = Vector2.Up.AngleTo(bodyUp.Normalized());
        GlobalRotation = Mathf.LerpAngle(GlobalRotation, angle, 1 - Mathf.Exp(-RotationResponse * (float)delta));
        Zoom = Vector2.One * ViewZoom;
    }
}

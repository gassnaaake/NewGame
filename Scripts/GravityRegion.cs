using Godot;

namespace PlanetLab;

// A rectangular, independently configurable gravity region. It has no collision
// shape; terrain collision stays on Planet / DashPlatform / other static bodies.
public partial class GravityRegion : Node2D, IGravitySource
{
    private int _gravityLayer;
    private Vector2 _size = new(240, 240);
    private float _gravityStrength = 550;

    [Export] public string DisplayName { get; set; } = "Local Gravity";

    [Export(PropertyHint.Range, "0,32,1")]
    public int GravityLayer
    {
        get => _gravityLayer;
        set => _gravityLayer = Mathf.Max(0, value);
    }

    [Export]
    public Vector2 Size
    {
        get => _size;
        set => _size = new Vector2(Mathf.Max(1, value.X), Mathf.Max(1, value.Y));
    }

    // Direction is configured in the region's local space, so rotating the
    // region rotates its gravity with it.
    [Export] public Vector2 Direction { get; set; } = Vector2.Down;
    [Export(PropertyHint.Range, "0,2000,10")]
    public float GravityStrength
    {
        get => _gravityStrength;
        set => _gravityStrength = Mathf.Max(0, value);
    }
    [Export] public Color RegionColor { get; set; } = new("6fe0a0");
    [Export] public bool ShowLabel { get; set; } = true;
    [Export] public Vector2 LabelOffset { get; set; } = Vector2.Zero;

    public string GravityLabel =>
        string.IsNullOrWhiteSpace(DisplayName) ? Name.ToString() : DisplayName;

    public override void _EnterTree()
    {
        AddToGroup("gravity_sources");
    }

    public override void _Ready()
    {
        ZIndex = -1;
        QueueRedraw();
    }

    public bool TryGetGravity(Vector2 worldPosition, out Vector2 gravity)
    {
        Vector2 local = ToLocal(worldPosition);
        Vector2 half = Size * 0.5f;
        if (Mathf.Abs(local.X) > half.X || Mathf.Abs(local.Y) > half.Y)
        {
            gravity = Vector2.Zero;
            return false;
        }

        Vector2 localDirection = Direction.LengthSquared() > 0.000001f
            ? Direction.Normalized()
            : Vector2.Down;
        gravity = localDirection.Rotated(GlobalRotation) * GravityStrength;
        return true;
    }

    public override void _Draw()
    {
        Vector2 half = Size * 0.5f;
        Color fill = new(RegionColor.R, RegionColor.G, RegionColor.B, 0.08f);
        Color outline = new(RegionColor.R, RegionColor.G, RegionColor.B, 0.7f);
        DrawRect(new Rect2(-half, Size), fill);
        DrawRect(new Rect2(-half, Size), outline, false, 2);

        Vector2 direction = Direction.LengthSquared() > 0.000001f
            ? Direction.Normalized()
            : Vector2.Down;
        Vector2 tip = direction * 42;
        Vector2 side = new(-direction.Y, direction.X);
        DrawLine(Vector2.Zero, tip, RegionColor, 3, true);
        DrawLine(tip, tip - direction * 10 + side * 6, RegionColor, 3, true);
        DrawLine(tip, tip - direction * 10 - side * 6, RegionColor, 3, true);

        if (!ShowLabel) return;
        Vector2 labelPosition = new Vector2(-half.X + 8, -half.Y + 20) + LabelOffset;
        DrawString(
            ThemeDB.FallbackFont,
            labelPosition,
            $"L{GravityLayer}  {GravityLabel}",
            fontSize: 15,
            modulate: RegionColor);
        DrawString(
            ThemeDB.FallbackFont,
            labelPosition + new Vector2(0, 18),
            $"g {GravityStrength:F0}",
            fontSize: 13,
            modulate: new Color(RegionColor.R, RegionColor.G, RegionColor.B, 0.8f));
    }
}

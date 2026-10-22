using Godot;

namespace PlanetLab;

// A rectangular, independently configurable gravity region. It has no collision
// shape; terrain collision stays on Planet / DashPlatform / other static bodies.
//
// [Tool] only adds a drawing preview: the rectangle uses the same Size clamp and
// the same half-extent test as TryGetGravity, so the shown area is exactly the
// area the runtime queries. No nodes, colliders or scene properties are created
// or changed by the preview.
[Tool]
public partial class GravityRegion : Node2D, IGravitySource
{
    private int _gravityLayer;
    private Vector2 _size = new(240, 240);
    private float _gravityStrength = 550;
    private string _displayName = "Local Gravity";
    private Vector2 _direction = Vector2.Down;
    private Color _regionColor = new("6fe0a0");
    private bool _showLabel = true;
    private Vector2 _labelOffset = Vector2.Zero;

    [ExportGroup("Identity / Priority")]
    [Export]
    public string DisplayName
    {
        get => _displayName;
        set
        {
            _displayName = value ?? string.Empty;
            QueueRedraw();
        }
    }

    [Export(PropertyHint.Range, "0,32,1")]
    public int GravityLayer
    {
        get => _gravityLayer;
        set
        {
            _gravityLayer = Mathf.Max(0, value);
            QueueRedraw();
        }
    }

    [ExportSubgroup("Range")]
    [Export]
    public Vector2 Size
    {
        get => _size;
        set
        {
            _size = new Vector2(Mathf.Max(1, value.X), Mathf.Max(1, value.Y));
            QueueRedraw();
        }
    }

    // Direction is configured in the region's local space, so rotating the
    // region rotates its gravity with it.
    [ExportSubgroup("Direction & Strength")]
    [Export]
    public Vector2 Direction
    {
        get => _direction;
        set
        {
            _direction = value;
            QueueRedraw();
        }
    }

    [Export(PropertyHint.Range, "0,2000,10")]
    public float GravityStrength
    {
        get => _gravityStrength;
        set
        {
            _gravityStrength = Mathf.Max(0, value);
            QueueRedraw();
        }
    }

    [ExportGroup("Appearance")]
    [Export]
    public Color RegionColor
    {
        get => _regionColor;
        set
        {
            _regionColor = value;
            QueueRedraw();
        }
    }

    [Export]
    public bool ShowLabel
    {
        get => _showLabel;
        set
        {
            _showLabel = value;
            QueueRedraw();
        }
    }

    [Export]
    public Vector2 LabelOffset
    {
        get => _labelOffset;
        set
        {
            _labelOffset = value;
            QueueRedraw();
        }
    }

    public string GravityLabel =>
        string.IsNullOrWhiteSpace(DisplayName) ? Name.ToString() : DisplayName;

    private Vector2 HalfSize => Size * 0.5f;

    private Vector2 LocalDirection => Direction.LengthSquared() > 0.000001f
        ? Direction.Normalized()
        : Vector2.Down;

    public override void _EnterTree()
    {
        // The editor preview must not add groups to the saved scene.
        if (!Engine.IsEditorHint())
            AddToGroup("gravity_sources");
    }

    public override void _Ready()
    {
        // Node2D does not request transform notifications by default; this is
        // what refreshes the preview after gizmo moves/rotations without any
        // per-frame redraw.
        SetNotifyTransform(true);
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == CanvasItem.NotificationTransformChanged)
            QueueRedraw();
    }

    public bool TryGetGravity(Vector2 worldPosition, out Vector2 gravity)
    {
        Vector2 local = ToLocal(worldPosition);
        Vector2 half = HalfSize;
        if (Mathf.Abs(local.X) > half.X || Mathf.Abs(local.Y) > half.Y)
        {
            gravity = Vector2.Zero;
            return false;
        }

        gravity = LocalDirection.Rotated(GlobalRotation) * GravityStrength;
        return true;
    }

    public override void _Draw()
    {
        Vector2 half = HalfSize;
        var rect = new Rect2(-half, Size);
        Color color = RegionColor;
        var fill = new Color(color.R, color.G, color.B, 0.16f);
        var outline = new Color(color.R, color.G, color.B, 0.85f);

        DrawRect(rect, fill);
        DrawRect(rect, outline, false, 2);

        Vector2 direction = LocalDirection;
        Vector2 tip = direction * 42;
        Vector2 side = new(-direction.Y, direction.X);
        DrawLine(Vector2.Zero, tip, color, 3, true);
        DrawLine(tip, tip - direction * 10 + side * 6, color, 3, true);
        DrawLine(tip, tip - direction * 10 - side * 6, color, 3, true);

        if (!ShowLabel) return;
        Vector2 labelPosition = new Vector2(-half.X + 8, -half.Y + 20) + LabelOffset;
        DrawString(
            ThemeDB.FallbackFont,
            labelPosition,
            $"L{GravityLayer}  {GravityLabel}",
            fontSize: 15,
            modulate: color);
        DrawString(
            ThemeDB.FallbackFont,
            labelPosition + new Vector2(0, 18),
            $"g {GravityStrength:F0}",
            fontSize: 13,
            modulate: outline);
    }
}

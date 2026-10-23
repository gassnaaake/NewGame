using Godot;

namespace PlanetLab;

// 中文：矩形、固定方向的局部重力区域。没有碰撞体：地形碰撞属于 Planet / DashPlatform。
// [Tool] 预览使用与 TryGetGravity 完全相同的 Size 钳制和半矩形判定，因此画出来的范围
// 就是运行时实际生效的范围；预览不会创建节点、碰撞或场景属性。
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

    [ExportGroup("使用说明（只读）")]
    [Export(PropertyHint.MultilineText)]
    public string UsageNote { get; set; } =
        "常用：GravityLayer 0 最高，数字越大优先级越低；GravityStrength 单位 px/s²，越大拉力越强。\n" +
        "Size 是矩形范围（px），应覆盖平台和完整跳跃空间；Direction 是局部方向，如 (0,1) 向下、(1,0) 向右，旋转区域会一起转。\n" +
        "同 Layer 区域相加；较低 Layer 完全不参与；外观/标签只影响显示。";

    [ExportGroup("① 常用玩法（优先调整）")]
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

    [ExportSubgroup("范围")]
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

    // 中文：Direction 是区域局部方向；区域旋转时重力方向一起旋转。
    [ExportSubgroup("方向与强度")]
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

    [ExportGroup("② 标识与显示")]
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

    [ExportGroup("③ 外观 / 标签（次要）")]
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
        // 中文：编辑器预览不把区域加入运行时重力源分组。
        if (!Engine.IsEditorHint())
            AddToGroup("gravity_sources");
    }

    public override void _Ready()
    {
        SetNotifyTransform(true);
        QueueRedraw();
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        EditorHelp.MakeReadOnly(property, nameof(UsageNote));
    }

    public override void _Notification(int what)
    {
        if (what == CanvasItem.NotificationTransformChanged)
            QueueRedraw();
    }

    // 中文：唯一的重力查询入口；边界完全由局部 Size 决定，没有额外碰撞。
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

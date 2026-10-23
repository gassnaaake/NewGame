using Godot;

namespace PlanetLab;

// 中文：手工摆放的平台组件。平台的碰撞是场景中真实存在的 CollisionShape2D 子节点，
// 其 RectangleShape2D.Size 是唯一尺寸来源；脚本不再在 _Ready 里隐藏 AddChild。
// [Tool] 只负责按真实子碰撞绘制平台外观，不创建/保存任何碰撞节点。
[Tool]
public partial class DashPlatform : StaticBody2D
{
    public enum PlatformKind { Solid, Breakable, DashPass }

    [ExportGroup("使用说明（只读）")]
    [Export(PropertyHint.MultilineText)]
    public string UsageNote { get; set; } =
        "碰撞尺寸来自子节点 CollisionShape2D 的 RectangleShape2D.Size：选中子节点后修改 Shape 或拖动 2D 控制点，不要再找平台节点的 Size。\n" +
        "Kind：Solid 普通；Breakable 下砸破坏；DashPass 下砸可穿过。Caption / ShowTakeoffMarker 只影响显示。\n" +
        "每个平台使用独立 Shape；复制平台后若共用资源，请在 Shape 菜单选择 Make Unique。";

    private PlatformKind _kind;
    private string _caption = "";
    private bool _showTakeoffMarker;

    [ExportGroup("① 平台玩法（常用）")]
    [Export]
    public PlatformKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            QueueRedraw();
        }
    }

    [ExportGroup("② 外观 / 标签（次要）")]
    [Export]
    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value ?? string.Empty;
            QueueRedraw();
        }
    }

    [Export]
    public bool ShowTakeoffMarker
    {
        get => _showTakeoffMarker;
        set
        {
            _showTakeoffMarker = value;
            QueueRedraw();
        }
    }

    public bool Destroyed { get; private set; }

    // 中文：供测试/编辑器检查使用；实际碰撞来源是场景里的子节点。
    public CollisionShape2D? CollisionShapeNode => GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
    public RectangleShape2D? RectangleShape => CollisionShapeNode?.Shape as RectangleShape2D;

    // 中文：只读派生尺寸，不是第二套可编辑配置；场景里改子节点 Shape。
    public Vector2 Size => RectangleShape?.Size ?? Vector2.Zero;

    private Transform2D _polledTransform = Transform2D.Identity;
    private Vector2 _polledSize;
    private bool _polledHasShape;
    private float _pollTimer;

    public override void _Ready()
    {
        SetNotifyTransform(true);
        SetProcess(Engine.IsEditorHint());
        if (Engine.IsEditorHint())
        {
            PollCollisionShape();
            WarnIfShapeShared();
            QueueRedraw();
            return;
        }

        // 中文：只设置层/掩码，不创建碰撞体。缺少子碰撞时明确警告，不静默生成。
        CollisionLayer = 1;
        CollisionMask = 2;
        if (RectangleShape is null)
            GD.PushWarning($"{Name}: missing RectangleShape2D child; no hidden runtime collision is created.");
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) return;
        _pollTimer -= (float)delta;
        if (_pollTimer > 0) return;
        _pollTimer = 0.2f;
        PollCollisionShape();
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        // 中文：UsageNote 只用于阅读，不能在 Inspector 编辑，也不写入 .tscn。
        EditorHelp.MakeReadOnly(property, nameof(UsageNote));
    }

    // 中文：编辑器低频轮询子碰撞的尺寸/变换，更新平台外观。
    // 不订阅 Shape.Changed，避免替换资源时对已断开连接做 disconnect。
    private void PollCollisionShape()
    {
        Shape2D? shape = CollisionShapeNode?.Shape;
        bool hasShape = shape is RectangleShape2D;
        Vector2 size = shape is RectangleShape2D rectangle ? rectangle.Size : Vector2.Zero;
        Transform2D transform = CollisionShapeNode?.Transform ?? Transform2D.Identity;
        if (hasShape != _polledHasShape ||
            !size.IsEqualApprox(_polledSize) ||
            !transform.IsEqualApprox(_polledTransform))
        {
            _polledHasShape = hasShape;
            _polledSize = size;
            _polledTransform = transform;
            QueueRedraw();
        }
    }

    private void WarnIfShapeShared()
    {
        Shape2D? shape = CollisionShapeNode?.Shape;
        Node? parent = GetParent();
        if (shape is null || parent is null) return;

        foreach (Node sibling in parent.GetChildren())
        {
            if (sibling == this || sibling is not DashPlatform other) continue;
            if (ReferenceEquals(other.CollisionShapeNode?.Shape, shape))
            {
                GD.PushWarning($"{Name}: CollisionShape2D.Shape is shared with {other.Name}; choose Make Unique.");
                return;
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == CanvasItem.NotificationTransformChanged)
            QueueRedraw();
    }

    public void Break()
    {
        Destroyed = true;
        CollisionLayer = 0;
        Hide();
    }

    // 中文：用子碰撞的真实局部变换与矩形判断重叠，支持移动/旋转/缩放 Shape。
    public bool OverlapsPlayer(Vector2 center, float radius)
    {
        CollisionShape2D? node = CollisionShapeNode;
        if (node?.Shape is not RectangleShape2D rectangle) return false;

        Vector2 local = node.Transform.AffineInverse() * ToLocal(center);
        Vector2 half = rectangle.Size * 0.5f;
        Vector2 nearest = local.Clamp(-half, half);
        return local.DistanceTo(nearest) <= radius + 0.25f;
    }

    public override void _Draw()
    {
        Color color = Kind switch
        {
            PlatformKind.Breakable => new Color("efae62"),
            PlatformKind.DashPass => new Color("79c7ec"),
            _ => new Color("b4c2cc")
        };

        CollisionShape2D? node = CollisionShapeNode;
        if (node?.Shape is not RectangleShape2D rectangle)
        {
            // 中文：编辑器里明确提示缺少真实碰撞子节点，不画一个虚假的尺寸。
            DrawLine(Vector2.Zero, new Vector2(40, 0), new Color("ff6b6b"), 3, true);
            DrawString(ThemeDB.FallbackFont, new Vector2(4, -10),
                "Missing CollisionShape2D (RectangleShape2D)", fontSize: 14,
                modulate: new Color("ff6b6b"));
            return;
        }

        Transform2D transform = node.Transform;
        DrawSetTransform(transform.Origin, transform.Rotation, transform.Scale);
        var rect = new Rect2(-rectangle.Size / 2, rectangle.Size);
        DrawRect(rect, color);
        if (Engine.IsEditorHint())
            DrawRect(rect, new Color(1f, 1f, 1f, 0.55f), false, 1);
        DrawLine(new Vector2(-rectangle.Size.X / 2, -rectangle.Size.Y / 2),
            new Vector2(rectangle.Size.X / 2, -rectangle.Size.Y / 2), Colors.White, 2);
        if (ShowTakeoffMarker)
            DrawCircle(new Vector2(0, -rectangle.Size.Y / 2 - 3), 5, Colors.White);
        DrawString(ThemeDB.FallbackFont,
            new Vector2(-rectangle.Size.X / 2, -rectangle.Size.Y / 2 - 9),
            Caption, fontSize: 16, modulate: color);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

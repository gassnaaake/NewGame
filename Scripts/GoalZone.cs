using Godot;

namespace PlanetLab;

// 中文：P-002.3 的终点标记。只检测现有 Player 的 Area2D，不改变移动能力，
// 也不识别“目标星球”；到达后由 PlaygroundLevel 显示完成状态。
[Tool]
public partial class GoalZone : Area2D
{
    private float _radius = 90;
    private string _caption = "GOAL";
    private Color _beaconColor = new("ffd166");

    [ExportGroup("使用说明（只读）")]
    [Export(PropertyHint.MultilineText)]
    public string UsageNote { get; set; } =
        "Radius 是完成触发半径（px）：Player 进入圆范围即完成，其他物体不会触发。\n" +
        "Caption / BeaconColor 只影响终点旗帜和圆环显示；终点只检测 Player。";

    [ExportGroup("① 终点触发（常用）")]
    [Export(PropertyHint.Range, "10,400,5")]
    public float Radius
    {
        get => _radius;
        set { _radius = Mathf.Max(10, value); RefreshPreview(); }
    }

    [ExportGroup("② 外观 / 标签（次要）")]
    [Export]
    public string Caption
    {
        get => _caption;
        set { _caption = value; RefreshPreview(); }
    }

    [Export]
    public Color BeaconColor
    {
        get => _beaconColor;
        set { _beaconColor = value; RefreshPreview(); }
    }

    [Signal] public delegate void PlayerReachedEventHandler();

    public bool Reached { get; private set; }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            RefreshPreview();
            return;
        }

        CollisionLayer = 0;
        CollisionMask = 2; // 中文：Player 是 collision layer 2 上的唯一目标。
        Monitoring = true;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        BodyEntered += OnBodyEntered;
        RefreshPreview();
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        EditorHelp.MakeReadOnly(property, nameof(UsageNote));
    }

    private void OnBodyEntered(Node2D body)
    {
        if (Reached || body is not Player) return;
        Reached = true;
        EmitSignal(SignalName.PlayerReached);
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (IsInsideTree()) QueueRedraw();
    }

    public override void _Draw()
    {
        Color color = Reached ? new Color("7ef0bb") : _beaconColor;
        DrawCircle(Vector2.Zero, _radius, new Color(color.R, color.G, color.B, 0.08f));
        DrawArc(Vector2.Zero, _radius, 0, Mathf.Tau, 48,
            new Color(color.R, color.G, color.B, 0.75f), 2, true);
        // 中文：旗帜/旗杆在世界中旋转，编辑器与运行时一致。
        DrawLine(Vector2.Zero, new Vector2(0, -58), color, 3, true);
        DrawColoredPolygon(new[] { new Vector2(0, -58), new Vector2(34, -47), new Vector2(0, -36) }, color);
        DrawString(ThemeDB.FallbackFont, new Vector2(-24, 30), _caption, fontSize: 16, modulate: color);
    }
}

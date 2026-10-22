using Godot;

namespace PlanetLab;

// A configurable special platform. Collision is generated once at runtime in
// _Ready; [Tool] only draws the footprint so the 2D editor shows the same
// RectangleShape2D(Size) the game will create, without adding a collider node
// to the edited scene.
[Tool]
public partial class DashPlatform : StaticBody2D
{
    public enum PlatformKind { Solid, Breakable, DashPass }

    private PlatformKind _kind;
    private Vector2 _size = new(120, 12);
    private string _caption = "";
    private bool _showTakeoffMarker;

    [ExportGroup("Platform")]
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

    [Export]
    public Vector2 Size
    {
        get => _size;
        set
        {
            _size = value;
            QueueRedraw();
        }
    }

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

    [ExportGroup("Takeoff Marker")]
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

    public override void _Ready()
    {
        SetNotifyTransform(true);
        if (Engine.IsEditorHint())
        {
            // The runtime collider must never be added to the edited scene.
            QueueRedraw();
            return;
        }

        CollisionLayer = 1;
        CollisionMask = 2;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } });
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

    public bool OverlapsPlayer(Vector2 center, float radius)
    {
        Vector2 local = ToLocal(center);
        Vector2 nearest = local.Clamp(-Size / 2, Size / 2);
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
        var rect = new Rect2(-Size / 2, Size);
        DrawRect(rect, color);

        // Same rectangle as the runtime RectangleShape2D(Size); only the editor
        // adds the full outline so the exact collision footprint is readable.
        if (Engine.IsEditorHint())
            DrawRect(rect, new Color(1f, 1f, 1f, 0.55f), false, 1);
        DrawLine(new Vector2(-Size.X / 2, -Size.Y / 2), new Vector2(Size.X / 2, -Size.Y / 2), Colors.White, 2);
        if (ShowTakeoffMarker)
            DrawCircle(new Vector2(0, -Size.Y / 2 - 3), 5, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(-Size.X / 2, -Size.Y / 2 - 9), Caption, fontSize: 16, modulate: color);
    }
}

using Godot;

namespace PlanetLab;

// P-002.3 goal beacon. The trigger is a plain Area2D that only detects the
// existing Player body; it adds no movement ability and no target detection.
[Tool]
public partial class GoalZone : Area2D
{
    private float _radius = 90;
    private string _caption = "GOAL";
    private Color _beaconColor = new("ffd166");

    [Export(PropertyHint.Range, "10,400,5")]
    public float Radius
    {
        get => _radius;
        set { _radius = Mathf.Max(10, value); RefreshPreview(); }
    }

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
        CollisionMask = 2; // Player is the only body on collision layer 2.
        Monitoring = true;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        BodyEntered += OnBodyEntered;
        RefreshPreview();
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
        // Flag pole and pennant: readable in the editor and at runtime.
        DrawLine(Vector2.Zero, new Vector2(0, -58), color, 3, true);
        DrawColoredPolygon(new[] { new Vector2(0, -58), new Vector2(34, -47), new Vector2(0, -36) }, color);
        DrawString(ThemeDB.FallbackFont, new Vector2(-24, 30), _caption, fontSize: 16, modulate: color);
    }
}

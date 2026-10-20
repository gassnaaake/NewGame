using Godot;

namespace PlanetLab;

public partial class DashPlatform : StaticBody2D
{
    public enum PlatformKind { Solid, Breakable, DashPass }
    [Export] public PlatformKind Kind { get; set; }
    [Export] public Vector2 Size { get; set; } = new(120, 12);
    [Export] public string Caption { get; set; } = "";
    [Export] public bool ShowTakeoffMarker { get; set; }
    public bool Destroyed { get; private set; }
    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 2;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } });
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
        DrawRect(new Rect2(-Size / 2, Size), color);
        DrawLine(new Vector2(-Size.X / 2, -Size.Y / 2), new Vector2(Size.X / 2, -Size.Y / 2), Colors.White, 2);
        if (ShowTakeoffMarker)
            DrawCircle(new Vector2(0, -Size.Y / 2 - 3), 5, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(-Size.X / 2, -Size.Y / 2 - 9), Caption, fontSize: 16, modulate: color);
    }
}

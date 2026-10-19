using Godot;

namespace PlanetLab;

public partial class Planet : StaticBody2D
{
    [Export] public string DisplayName { get; set; } = "Planet";
    [Export] public Shape2D AttractionGeometry { get; set; } = new CircleShape2D { Radius = 180 };
    [Export(PropertyHint.Range, "0,2000,10")] public float GravityStrength { get; set; } = 550;
    // Influence region is the attraction shape expanded outwards by this distance.
    [Export(PropertyHint.Range, "0,2000,10")] public float InfluenceRange { get; set; } = 500;
    [Export] public Color BodyColor { get; set; } = new("31516c");
    [Export] public bool ShowInfluence { get; set; } = true;
    public Shape2D SolidGeometry => GetNode<CollisionShape2D>("CollisionShape2D").Shape;

    public override void _Ready()
    {
        AddToGroup("planets");
        if (!GlobalScale.IsEqualApprox(Vector2.One))
            GD.PushError($"{Name}: use shape dimensions, not node scale, for P-001 planets.");
    }

    public SurfaceSample Sample(Vector2 worldPosition)
    {
        SurfaceSample local = SurfaceGeometry.Sample(AttractionGeometry, ToLocal(worldPosition));
        return new SurfaceSample(ToGlobal(local.Point), local.Normal.Rotated(GlobalRotation), local.SignedDistance);
    }

    public Vector2 GravityAt(Vector2 worldPosition)
    {
        SurfaceSample sample = Sample(worldPosition);
        Vector2 toward = sample.Point - worldPosition;
        // On the boundary, pull into the body. Inside a separate attraction shape,
        // pull toward its closest boundary; solids still block motion independently.
        return (toward.LengthSquared() > 0.000001f ? toward.Normalized() : -sample.Normal) * GravityStrength;
    }

    public override void _Draw()
    {
        if (!HasNode("CollisionShape2D")) return;
        Vector2[] outline = Outline(SolidGeometry);
        DrawColoredPolygon(outline, BodyColor);
        DrawPolyline(Close(outline), BodyColor.Lightened(0.35f), 2, true);
        DrawPolyline(Close(Outline(AttractionGeometry)), new Color(0.5f, 0.9f, 0.8f, 0.6f), 1, true);
        if (ShowInfluence)
            DrawPolyline(Close(Outline(AttractionGeometry, InfluenceRange)), new Color(0.4f, 0.7f, 0.9f, 0.14f), 1, true);
    }

    private static Vector2[] Close(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        return closed;
    }

    private static Vector2[] Outline(Shape2D shape, float expansion = 0)
    {
        float r = shape is CapsuleShape2D c ? c.Radius : ((CircleShape2D)shape).Radius;
        float h = shape is CapsuleShape2D cap ? Mathf.Max(0, cap.Height / 2 - cap.Radius) : 0;
        var points = new Vector2[128];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Tau * i / points.Length;
            Vector2 n = Vector2.FromAngle(angle);
            points[i] = n * (r + expansion) + new Vector2(0, n.Y >= 0 ? h : -h);
        }
        return points;
    }
}

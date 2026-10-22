using Godot;

namespace PlanetLab;

// P-001 planet: a static body whose attraction boundary is an independent shape.
// [Tool] draws the same boundaries and real gravity samples in the 2D editor so
// designers can see where the planet pulls and in which direction. The preview
// never creates nodes or colliders and never changes scene properties.
[Tool]
public partial class Planet : StaticBody2D, IGravitySource
{
    private int _gravityLayer = 1;
    private float _gravityStrength = 550;
    private string _displayName = "Planet";
    private Shape2D _attractionGeometry = new CircleShape2D { Radius = 180 };
    private float _influenceRange = 500;
    private Color _bodyColor = new("31516c");
    private bool _showInfluence = true;
    private bool _showDirectionArrows = true;

    // Editor-only bookkeeping: refresh the preview when the attraction resource or
    // the collision shape child changes, without redrawing every editor frame.
    private Shape2D? _watchedAttraction;
    private Shape2D? _polledSolidShape;
    private Transform2D _polledSolidTransform = Transform2D.Identity;
    private float _polledSolidRadius;
    private float _polledSolidHeight;
    private float _pollTimer;

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

    [ExportGroup("Attraction")]
    [Export]
    public Shape2D AttractionGeometry
    {
        get => _attractionGeometry;
        set
        {
            _attractionGeometry = value;
            WatchAttractionShape(value);
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

    // Influence region is the attraction shape expanded outwards by this distance.
    [Export(PropertyHint.Range, "0,2000,10")]
    public float InfluenceRange
    {
        get => _influenceRange;
        set
        {
            _influenceRange = value;
            QueueRedraw();
        }
    }

    [ExportGroup("Appearance")]
    [Export]
    public Color BodyColor
    {
        get => _bodyColor;
        set
        {
            _bodyColor = value;
            QueueRedraw();
        }
    }

    [Export]
    public bool ShowInfluence
    {
        get => _showInfluence;
        set
        {
            _showInfluence = value;
            QueueRedraw();
        }
    }

    // Editor and runtime direction samples along the attraction outline.
    [Export]
    public bool ShowDirectionArrows
    {
        get => _showDirectionArrows;
        set
        {
            _showDirectionArrows = value;
            QueueRedraw();
        }
    }

    public string GravityLabel => DisplayName;

    public Shape2D? SolidGeometry =>
        GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
        {
            WatchAttractionShape(AttractionGeometry);
            return;
        }
        AddToGroup("planets");
        AddToGroup("gravity_sources");
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
            WatchAttractionShape(null);
    }

    public override void _Notification(int what)
    {
        // Refresh sampled directions after a move/rotation (editor gizmo or code).
        if (what == CanvasItem.NotificationTransformChanged)
            QueueRedraw();
    }

    public override void _Ready()
    {
        SetNotifyTransform(true);
        if (Engine.IsEditorHint())
        {
            // _Process is used only for a cheap editor-side shape poll.
            SetProcess(true);
            QueueRedraw();
            return;
        }

        SetProcess(false);
        if (!GlobalScale.IsEqualApprox(Vector2.One))
            GD.PushError($"{Name}: use shape dimensions, not node scale, for P-001 planets.");
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) return;
        _pollTimer -= (float)delta;
        if (_pollTimer > 0) return;
        _pollTimer = 0.2f;
        PollEditorSolidShape();
    }

    public bool TryGetGravity(Vector2 worldPosition, out Vector2 gravity)
    {
        if (AttractionGeometry is null)
        {
            gravity = Vector2.Zero;
            return false;
        }

        SurfaceSample sample = Sample(worldPosition);
        if (sample.SignedDistance > InfluenceRange)
        {
            gravity = Vector2.Zero;
            return false;
        }
        gravity = GravityAt(worldPosition);
        return true;
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
        bool editor = Engine.IsEditorHint();

        // The solid body is drawn from the collision child so the editor shows the
        // actual terrain silhouette. A missing child or empty shape is a safe skip.
        CollisionShape2D? solidNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        Shape2D? solid = solidNode?.Shape;
        if (solid is not null && TryOutline(solid, 0, out Vector2[] solidPoints))
        {
            Transform2D solidTransform = solidNode!.Transform;
            for (int i = 0; i < solidPoints.Length; i++)
                solidPoints[i] = solidTransform * solidPoints[i];
            DrawColoredPolygon(solidPoints, BodyColor);
            DrawPolyline(Close(solidPoints), BodyColor.Lightened(0.35f), 2, true);
        }

        // Everything below is the attraction preview; unsupported/empty geometry
        // simply draws no gravity gizmo instead of throwing in the editor.
        if (AttractionGeometry is null || !TryOutline(AttractionGeometry, 0, out Vector2[] attractionPoints))
            return;

        DrawPolyline(
            Close(attractionPoints),
            new Color(0.5f, 0.9f, 0.8f, editor ? 0.85f : 0.6f),
            1,
            true);

        if (ShowInfluence && TryOutline(AttractionGeometry, InfluenceRange, out Vector2[] influencePoints))
            DrawPolyline(
                Close(influencePoints),
                new Color(0.4f, 0.7f, 0.9f, editor ? 0.3f : 0.14f),
                1,
                true);

        if (ShowDirectionArrows)
            DrawGravitySamples(AttractionGeometry, editor);

        if (editor)
            DrawEditorIdentity(AttractionGeometry);
    }

    // Convert a world-space gravity vector into this node's local drawing space.
    private void DrawGravityArrow(
        Vector2 boundaryPoint,
        Vector2 outwardNormal,
        float gap,
        float length,
        float headLength,
        float headWidth,
        Color color)
    {
        Vector2 samplePoint = boundaryPoint + outwardNormal * gap;
        Vector2 worldGravity = GravityAt(ToGlobal(samplePoint));
        if (worldGravity.LengthSquared() < 0.000001f) return;

        Vector2 localGravity = worldGravity.Rotated(-GlobalRotation);
        if (localGravity.LengthSquared() < 0.000001f) return;
        Vector2 direction = localGravity.Normalized();

        Vector2 tip = samplePoint + direction * length;
        Vector2 side = new(-direction.Y, direction.X);
        DrawLine(samplePoint, tip, color, 2, true);
        DrawLine(tip, tip - direction * headLength + side * headWidth, color, 2, true);
        DrawLine(tip, tip - direction * headLength - side * headWidth, color, 2, true);
    }

    // Samples the real GravityAt() direction at a moderate number of points on
    // the attraction outline: 10 for a circle; 4 along the straight sides plus
    // 3 per cap for a capsule (both caps and both sides are covered).
    private void DrawGravitySamples(Shape2D shape, bool editor)
    {
        const float Gap = 24;
        const float Length = 20;
        const float HeadLength = 7;
        const float HeadWidth = 4;
        var color = new Color(0.95f, 0.85f, 0.55f, editor ? 0.9f : 0.6f);

        if (shape is CircleShape2D circle)
        {
            const int Count = 10;
            float radius = Mathf.Max(0, circle.Radius);
            for (int i = 0; i < Count; i++)
            {
                Vector2 normal = Vector2.FromAngle(Mathf.Tau * i / Count);
                DrawGravityArrow(normal * radius, normal, Gap, Length, HeadLength, HeadWidth, color);
            }
            return;
        }

        if (shape is not CapsuleShape2D capsule) return;

        float capsuleRadius = Mathf.Max(0, capsule.Radius);
        float halfSpine = Mathf.Max(0, capsule.Height * 0.5f - capsule.Radius);
        if (halfSpine <= 0.001f)
        {
            // Degenerate capsule (effectively a circle).
            const int Count = 10;
            for (int i = 0; i < Count; i++)
            {
                Vector2 normal = Vector2.FromAngle(Mathf.Tau * i / Count);
                DrawGravityArrow(normal * capsuleRadius, normal, Gap, Length, HeadLength, HeadWidth, color);
            }
            return;
        }

        // Straight sides, away from the cap shoulder points.
        for (int i = 0; i < 2; i++)
        {
            float y = (i == 0 ? -1 : 1) * halfSpine * 0.55f;
            DrawGravityArrow(new Vector2(capsuleRadius, y), Vector2.Right, Gap, Length, HeadLength, HeadWidth, color);
            DrawGravityArrow(new Vector2(-capsuleRadius, y), Vector2.Left, Gap, Length, HeadLength, HeadWidth, color);
        }

        // Both cap semicircles: shoulder, tip, shoulder.
        for (int cap = 0; cap < 2; cap++)
        {
            float sign = cap == 0 ? -1 : 1;
            Vector2 capCenter = new(0, sign * halfSpine);
            for (int i = 0; i < 3; i++)
            {
                float angle = sign < 0
                    ? Mathf.Pi + i * Mathf.Pi * 0.5f
                    : i * Mathf.Pi * 0.5f;
                Vector2 normal = Vector2.FromAngle(angle);
                DrawGravityArrow(capCenter + normal * capsuleRadius, normal, Gap, Length, HeadLength, HeadWidth, color);
            }
        }
    }

    private void DrawEditorIdentity(Shape2D shape)
    {
        Vector2 anchor;
        switch (shape)
        {
            case CircleShape2D circle:
                anchor = new Vector2(-circle.Radius * 0.55f, -circle.Radius - 10);
                break;
            case CapsuleShape2D capsule:
                float top = Mathf.Max(0, capsule.Height * 0.5f - capsule.Radius) + capsule.Radius;
                anchor = new Vector2(-capsule.Radius * 0.55f, -top - 10);
                break;
            default:
                return;
        }

        Color color = BodyColor.Lightened(0.55f);
        color.A = 0.9f;
        string label = string.IsNullOrWhiteSpace(DisplayName) ? Name.ToString() : DisplayName;
        DrawString(
            ThemeDB.FallbackFont,
            anchor,
            $"L{GravityLayer}  {label}",
            fontSize: 13,
            modulate: color);
    }

    private void WatchAttractionShape(Shape2D? shape)
    {
        if (!Engine.IsEditorHint()) return;
        if (ReferenceEquals(shape, _watchedAttraction)) return;

        Shape2D? previous = _watchedAttraction;
        if (previous is not null && GodotObject.IsInstanceValid(previous))
            previous.Changed -= OnWatchedShapeChanged;

        _watchedAttraction = shape;
        if (shape is not null)
            shape.Changed += OnWatchedShapeChanged;
    }

    private void OnWatchedShapeChanged()
    {
        QueueRedraw();
    }

    // The planet's own AttractionGeometry signal covers the common case where the
    // CollisionShape2D shares the same sub-resource. This low-frequency poll is a
    // fallback for a separate solid shape, a swapped child shape or an offset
    // collision child; it only redraws when something actually changed.
    private void PollEditorSolidShape()
    {
        CollisionShape2D? solidNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        Shape2D? solid = solidNode?.Shape;
        Transform2D solidTransform = solidNode?.Transform ?? Transform2D.Identity;
        float radius = solid switch
        {
            CircleShape2D circle => circle.Radius,
            CapsuleShape2D capsule => capsule.Radius,
            _ => 0
        };
        float height = solid is CapsuleShape2D capsuleShape ? capsuleShape.Height : 0;

        bool changed = !ReferenceEquals(solid, _polledSolidShape)
            || !solidTransform.IsEqualApprox(_polledSolidTransform)
            || !Mathf.IsEqualApprox(radius, _polledSolidRadius)
            || !Mathf.IsEqualApprox(height, _polledSolidHeight);
        if (!changed) return;

        _polledSolidShape = solid;
        _polledSolidTransform = solidTransform;
        _polledSolidRadius = radius;
        _polledSolidHeight = height;
        QueueRedraw();
    }

    private static bool TryOutline(Shape2D? shape, float expansion, out Vector2[] points)
    {
        const int segments = 128;
        switch (shape)
        {
            case CircleShape2D circle:
            {
                float radius = Mathf.Max(0, circle.Radius + expansion);
                points = new Vector2[segments];
                for (int i = 0; i < segments; i++)
                    points[i] = Vector2.FromAngle(Mathf.Tau * i / segments) * radius;
                return true;
            }
            case CapsuleShape2D capsule:
            {
                float radius = Mathf.Max(0, capsule.Radius + expansion);
                float halfSpine = Mathf.Max(0, capsule.Height * 0.5f - capsule.Radius);
                points = new Vector2[segments];
                for (int i = 0; i < segments; i++)
                {
                    Vector2 normal = Vector2.FromAngle(Mathf.Tau * i / segments);
                    points[i] = normal * radius + new Vector2(0, normal.Y >= 0 ? halfSpine : -halfSpine);
                }
                return true;
            }
            default:
                points = System.Array.Empty<Vector2>();
                return false;
        }
    }

    private static Vector2[] Close(Vector2[] points)
    {
        if (points.Length == 0) return points;
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        return closed;
    }
}

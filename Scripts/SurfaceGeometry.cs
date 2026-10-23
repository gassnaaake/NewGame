using Godot;

namespace PlanetLab;

public readonly record struct SurfaceSample(Vector2 Point, Vector2 Normal, float SignedDistance);

// 中文：只查询吸引形状的外边界，不读取实体碰撞接触点或分解缝隙。
public static class SurfaceGeometry
{
    public static SurfaceSample Sample(Shape2D shape, Vector2 position)
    {
        float radius;
        Vector2 spine;
        switch (shape)
        {
            case CircleShape2D circle:
                radius = circle.Radius;
                spine = Vector2.Zero;
                break;
            case CapsuleShape2D capsule:
                radius = capsule.Radius;
                float halfSpine = Mathf.Max(0, capsule.Height * 0.5f - radius);
                spine = new Vector2(0, Mathf.Clamp(position.Y, -halfSpine, halfSpine));
                break;
            default:
                throw new System.NotSupportedException("P-001 supports CircleShape2D and CapsuleShape2D attraction geometry.");
        }
        Vector2 offset = position - spine;
        float distance = offset.Length();
        // Deterministic choice at equidistant interior points; valid even at the center.
        Vector2 normal = distance > 0.0001f ? offset / distance : Vector2.Right;
        return new SurfaceSample(spine + normal * radius, normal, distance - radius);
    }
}

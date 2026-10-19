using Godot;
using System.Collections.Generic;

namespace PlanetLab;

public static class GravitySelector
{
    // Explicit policy seam: P-001 selects the nearest eligible attraction boundary.
    // Stable scene order resolves exact ties; no hidden hysteresis or velocity edits.
    public static Planet? Select(IReadOnlyList<Planet> planets, Vector2 position)
    {
        Planet? selected = null;
        float best = float.PositiveInfinity;
        foreach (Planet planet in planets)
        {
            SurfaceSample sample = planet.Sample(position);
            if (sample.SignedDistance > planet.InfluenceRange) continue;
            float distance = Mathf.Abs(sample.SignedDistance);
            if (distance < best)
            {
                best = distance;
                selected = planet;
            }
        }
        return selected;
    }
}

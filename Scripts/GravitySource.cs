using Godot;
using System.Collections.Generic;

namespace PlanetLab;

// A spatial gravity provider. GravityLayer is a gameplay priority, not a Godot
// collision layer. Smaller values are stronger priorities; 0 is the highest.
public interface IGravitySource
{
    int GravityLayer { get; }
    string GravityLabel { get; }
    bool TryGetGravity(Vector2 worldPosition, out Vector2 gravity);
}

public readonly record struct GravityContribution(
    IGravitySource Source,
    string Name,
    int Layer,
    Vector2 Vector);

// Per-frame result of the GravityLayer policy. Lists are reused by Player to
// avoid allocating every physics tick; callers should not retain them.
public sealed class GravitySelection
{
    public List<GravityContribution> Active { get; } = new();
    public List<GravityContribution> Contributing { get; } = new();
    public int HighestPriorityLayer { get; internal set; } = -1;
    public Vector2 Total { get; internal set; }
    public bool HasActive => Active.Count > 0;

    public void Clear()
    {
        Active.Clear();
        Contributing.Clear();
        HighestPriorityLayer = -1;
        Total = Vector2.Zero;
    }
}

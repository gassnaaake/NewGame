using Godot;
using System.Collections.Generic;

namespace PlanetLab;

// The one place that applies the GravityLayer policy. This reads only the
// character's current position and the configured regions; no trigger order,
// entering history or last-source state is consulted.
public static class GravitySelector
{
    public static GravitySelection Select(IReadOnlyList<IGravitySource> sources, Vector2 position)
    {
        var result = new GravitySelection();
        Select(sources, position, result);
        return result;
    }

    public static void Select(
        IReadOnlyList<IGravitySource> sources,
        Vector2 position,
        GravitySelection result)
    {
        result.Clear();
        int highestPriority = int.MaxValue;

        for (int i = 0; i < sources.Count; i++)
        {
            IGravitySource source = sources[i];
            if (source == null) continue;
            if (source is Node node &&
                (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion()))
                continue;
            if (!source.TryGetGravity(position, out Vector2 gravity)) continue;
            if (!float.IsFinite(gravity.X) || !float.IsFinite(gravity.Y)) continue;

            int layer = Mathf.Max(0, source.GravityLayer);
            var contribution = new GravityContribution(
                source,
                source.GravityLabel ?? source.GetType().Name,
                layer,
                gravity);
            result.Active.Add(contribution);
            if (layer < highestPriority) highestPriority = layer;
        }

        if (highestPriority == int.MaxValue) return;

        result.HighestPriorityLayer = highestPriority;
        for (int i = 0; i < result.Active.Count; i++)
        {
            GravityContribution contribution = result.Active[i];
            if (contribution.Layer != highestPriority) continue;
            result.Contributing.Add(contribution);
            result.Total += contribution.Vector;
        }
    }
}

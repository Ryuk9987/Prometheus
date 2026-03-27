#nullable disable
using Godot;
using System.Collections.Generic;

public enum WorldObjectKind
{
    Resource, Animal, Campfire, BuildOrder, Structure
}

/// <summary>
/// Lightweight descriptor for any inspectable world object.
/// Objects register themselves on _Ready and unregister on _ExitTree.
/// </summary>
public class WorldObjectEntry
{
    public Node3D          Node     { get; }
    public WorldObjectKind Kind     { get; }
    public string          Label    { get; }   // display name
    public string          Icon     { get; }

    public WorldObjectEntry(Node3D node, WorldObjectKind kind, string label, string icon)
    { Node = node; Kind = kind; Label = label; Icon = icon; }
}

public partial class WorldObjectRegistry : Node
{
    public static WorldObjectRegistry Instance { get; private set; }
    private readonly List<WorldObjectEntry> _objects = new();

    public IReadOnlyList<WorldObjectEntry> Objects => _objects;

    public override void _Ready() => Instance = this;

    public void Register(WorldObjectEntry e)   => _objects.Add(e);
    public void Unregister(WorldObjectEntry e) => _objects.Remove(e);

    /// <summary>Find nearest world object to a screen position.</summary>
    public WorldObjectEntry FindNearest(Vector2 screenPos, Camera3D camera, float pixelThreshold = 55f)
    {
        WorldObjectEntry best = null;
        float bestDist = pixelThreshold;

        foreach (var entry in _objects)
        {
            if (!IsInstanceValid(entry.Node)) continue;

            // Use visual center rather than node origin (which is at ground level)
            var worldPos = entry.Node.GlobalPosition;
            worldPos.Y += VisualCenterOffset(entry);

            if (!camera.IsPositionInFrustum(worldPos)) continue;

            var screenObj = camera.UnprojectPosition(worldPos);
            float dist = screenPos.DistanceTo(screenObj);
            if (dist < bestDist) { bestDist = dist; best = entry; }
        }
        return best;
    }

    /// <summary>Y offset to the visual center of an object (so clicks land on the body, not the ground).</summary>
    private static float VisualCenterOffset(WorldObjectEntry entry) => entry.Kind switch {
        WorldObjectKind.Animal    => entry.Node is Animal a ? a.Type switch {
            AnimalType.Deer   => 0.75f,
            AnimalType.Boar   => 0.52f,
            AnimalType.Rabbit => 0.32f,
            _ => 0.5f
        } : 0.5f,
        WorldObjectKind.Campfire  => 0.4f,
        WorldObjectKind.Structure => 1.0f,
        WorldObjectKind.NPC       => 1.0f,
        _ => 0.3f
    };
}

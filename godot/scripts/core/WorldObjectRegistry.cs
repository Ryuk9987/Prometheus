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
    public WorldObjectEntry FindNearest(Vector2 screenPos, Camera3D camera, float pixelThreshold = 45f)
    {
        WorldObjectEntry best = null;
        float bestDist = pixelThreshold;

        foreach (var entry in _objects)
        {
            if (!IsInstanceValid(entry.Node)) continue;
            if (!camera.IsPositionInFrustum(entry.Node.GlobalPosition)) continue;

            var screenObj = camera.UnprojectPosition(entry.Node.GlobalPosition);
            float dist = screenPos.DistanceTo(screenObj);
            if (dist < bestDist) { bestDist = dist; best = entry; }
        }
        return best;
    }
}

#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Tracks all resource nodes in the world.
/// NPCs query this to find the nearest food/water.
/// </summary>
public partial class ResourceManager : Node
{
    public static ResourceManager Instance { get; private set; }

    private readonly List<ResourceNode> _nodes = new();

    public override void _Ready()
    {
        Instance = this;

        // Subscribe to world tick for resource respawn
        if (GameManager.Instance != null)
            GameManager.Instance.Connect(
                GameManager.SignalName.WorldTick,
                Callable.From<double>(OnWorldTick));
    }

    public void Register(ResourceNode node)   => _nodes.Add(node);
    public void Unregister(ResourceNode node) => _nodes.Remove(node);

    /// <summary>Find nearest non-empty resource of the given type.</summary>
    public ResourceNode FindNearest(Vector3 from, ResourceType type, float maxRange = 60f)
    {
        ResourceNode best = null;
        float bestDist = maxRange;

        foreach (var node in _nodes)
        {
            if (node.Type != type || node.IsEmpty) continue;
            float d = from.DistanceTo(node.GlobalPosition);
            if (d < bestDist) { bestDist = d; best = node; }
        }

        return best;
    }

    private void OnWorldTick(double delta)
    {
        foreach (var node in _nodes)
            node.OnWorldTick(delta);
    }
}

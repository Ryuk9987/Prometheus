#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Tracks all campfires. Allows NPCs to find the nearest one.
/// </summary>
public partial class CampfireManager : Node
{
    public static CampfireManager Instance { get; private set; }

    private readonly List<Campfire> _campfires = new();
    public IReadOnlyList<Campfire>  Campfires => _campfires;

    public override void _Ready() => Instance = this;

    public void Register(Campfire c)   => _campfires.Add(c);
    public void Unregister(Campfire c) => _campfires.Remove(c);

    public Campfire FindNearest(Vector3 from, float maxRange = 40f)
    {
        Campfire best = null; float bestD = maxRange;
        foreach (var c in _campfires)
        {
            float d = from.DistanceTo(c.GlobalPosition);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    public Campfire FindNearestNeedingFuel(Vector3 from, float maxRange = 40f)
    {
        Campfire best = null; float bestD = maxRange;
        foreach (var c in _campfires)
        {
            if (c.Fuel >= c.MaxFuel) continue;
            float d = from.DistanceTo(c.GlobalPosition);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }
}

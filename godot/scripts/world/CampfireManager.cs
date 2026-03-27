#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Tracks all campfires. Allows NPCs to find the nearest one.
/// Also tracks pending build sites so multiple NPCs don't start building simultaneously.
/// </summary>
public partial class CampfireManager : Node
{
    public static CampfireManager Instance { get; private set; }

    private readonly List<Campfire> _campfires = new();
    public IReadOnlyList<Campfire>  Campfires => _campfires;

    // Positions where a campfire is being built (claimed by an NPC, not yet spawned)
    private readonly List<Vector3> _pendingSites = new();

    public override void _Ready() => Instance = this;

    public void Register(Campfire c)   => _campfires.Add(c);
    public void Unregister(Campfire c) => _campfires.Remove(c);

    /// <summary>
    /// Claim a build site. Returns false if a fire or pending site is already within minDist.
    /// Call this before starting to build — if false, don't build.
    /// </summary>
    public bool TryClaimBuildSite(Vector3 pos, float minDist = 20f)
    {
        foreach (var c in _campfires)
            if (c.GlobalPosition.DistanceTo(pos) < minDist) return false;
        foreach (var p in _pendingSites)
            if (p.DistanceTo(pos) < minDist) return false;
        _pendingSites.Add(pos);
        return true;
    }

    /// <summary>Release a pending site (call when campfire is built or NPC gives up).</summary>
    public void ReleaseSite(Vector3 pos)
    {
        for (int i = _pendingSites.Count - 1; i >= 0; i--)
            if (_pendingSites[i].DistanceTo(pos) < 1f)
                { _pendingSites.RemoveAt(i); return; }
    }

    /// <summary>True if a real or pending fire exists within range of pos.</summary>
    public bool HasFireNear(Vector3 pos, float range = 20f)
    {
        foreach (var c in _campfires)
            if (c.GlobalPosition.DistanceTo(pos) < range) return true;
        foreach (var p in _pendingSites)
            if (p.DistanceTo(pos) < range) return true;
        return false;
    }

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

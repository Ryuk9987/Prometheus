#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BuildOrderManager : Node
{
    public static BuildOrderManager Instance { get; private set; }
    private readonly List<BuildOrder> _orders = new();
    public IReadOnlyList<BuildOrder> Orders => _orders;

    public override void _Ready() => Instance = this;

    public void Register(BuildOrder o)   => _orders.Add(o);
    public void Unregister(BuildOrder o) => _orders.Remove(o);

    public BuildOrder FindNearest(Vector3 from, string knowledgeId = null, float maxRange = 60f)
    {
        BuildOrder best = null; float bestD = maxRange;
        foreach (var o in _orders)
        {
            if (o.Status == BuildOrderStatus.Done) continue;
            if (knowledgeId != null && o.KnowledgeId != knowledgeId) continue;
            float d = from.DistanceTo(o.GlobalPosition);
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }

    public BuildOrder FindNearestRelevant(Vector3 from, NpcEntity npc, float maxRange = 60f)
    {
        BuildOrder best = null; float bestD = maxRange;
        foreach (var o in _orders)
        {
            if (o.Status == BuildOrderStatus.Done) continue;
            // NPC must know the knowledge (any depth)
            if (!npc.Knowledge.Knows(o.KnowledgeId)) continue;
            // Prefer orders from own tribe (or tribal-less orders)
            if (!string.IsNullOrEmpty(o.TribeId) && o.TribeId != npc.TribeId) continue;
            float d = from.DistanceTo(o.GlobalPosition);
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }
}

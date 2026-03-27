#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// LeaderBehavior — the tribe's Leader NPC plans and manages the settlement.
///
/// Only the Leader (SocialRole.Leader) issues BuildOrders.
/// No other NPC initiates construction autonomously.
///
/// Settlement layout (concentric zones around SettlementCenter):
///   Zone 0 — Center  (r=0):   Campfire, Storehouse
///   Zone 1 — Living  (r=8m):  Shelters / Huts
///   Zone 2 — Work    (r=16m): Workshop (near stone), Farm (near water/berries)
///   Zone 3 — Defense (r=24m): Walls
///
/// The Leader also:
///   - Sets the SettlementCenter on first tick (near current tribe position)
///   - Chooses Workshop/Farm placement based on resource proximity
///   - Evaluates tribe needs every 20 seconds
/// </summary>
public partial class LeaderBehavior : Node
{
    private NpcEntity _owner;
    private double    _evalTimer = 0;
    private const double EvalInterval = 20.0;

    // Whether this leader has already set the settlement center
    private bool _centerSet = false;

    public override void _Ready() => _owner = GetParent<NpcEntity>();

    public bool Tick(double delta)
    {
        if (_owner.SocialRole != SocialRole.Leader) return false;

        _evalTimer += delta;
        if (_evalTimer < EvalInterval) return false;
        _evalTimer = 0;

        EvaluateSettlement();
        return false; // Leader doesn't monopolize movement
    }

    // ── Main evaluation ───────────────────────────────────────────────────
    private void EvaluateSettlement()
    {
        var tribe = GetTribe();
        if (tribe == null || tribe.Members.Count == 0) return;

        // Set settlement center once — use tribe's geographic center, not leader's lone position
        if (!tribe.HasSettlementCenter)
        {
            tribe.SettlementCenter    = tribe.Center; // avg position of all members
            tribe.HasSettlementCenter = true;
            GD.Print($"[Leader] {_owner.NpcName} established settlement center at {tribe.SettlementCenter}");
        }

        var knowledge = CollectTribeKnowledge(tribe);
        var center    = tribe.SettlementCenter;
        int members   = tribe.Members.Count;

        // Helpers
        int existingOf(BuildingType bt) => SettlementManager.Instance?.Buildings
            .Count(b => b.TribeId == tribe.Name && b.Type == bt) ?? 0;
        int pendingOf(string kid) => BuildOrderManager.Instance?.Orders
            .Count(o => o.KnowledgeId == kid && o.TribeId == tribe.Name) ?? 0;

        // ── Rule 1: Campfire — Zone 0 ─────────────────────────────────────
        bool hasFireNear = CampfireManager.Instance?.HasFireNear(center, 18f) ?? false;
        if (!hasFireNear && Has(knowledge, "fire_making", 0.1f))
        {
            string kid = Has(knowledge, "campfire_stone", 0.1f) ? "campfire_stone" : "campfire";
            if (CampfireManager.Instance?.TryClaimBuildSite(center, 18f) == true)
            {
                PlaceOrder(tribe, kid, center + SmallJitter(), 3f);
                GD.Print($"[Leader] {_owner.NpcName} orders campfire.");
                return;
            }
        }

        // ── Rule 2: Shelters — Zone 1 ─────────────────────────────────────
        int sheltersNeeded = Mathf.Min(members / 3, 5);
        int sheltersHave   = existingOf(BuildingType.Shelter) + existingOf(BuildingType.ShelterImproved)
                           + existingOf(BuildingType.ShelterMud) + existingOf(BuildingType.Hut)
                           + existingOf(BuildingType.WoodenHut)
                           + pendingOf("shelter") + pendingOf("shelter_improved")
                           + pendingOf("shelter_mud") + pendingOf("hut") + pendingOf("wooden_shelter");
        if (Has(knowledge, "shelter", 0.05f) && sheltersHave < sheltersNeeded)
        {
            // Pick best shelter tier available
            string kid = Has(knowledge, "hut", 0.3f)              ? "hut"
                       : Has(knowledge, "shelter_mud", 0.2f)       ? "shelter_mud"
                       : Has(knowledge, "shelter_improved", 0.15f) ? "shelter_improved"
                       : "shelter";
            var bt = kid == "hut" ? BuildingType.Hut : BuildingType.Shelter;
            var pos = center + Ring(1, sheltersHave, 8f);
            PlaceOrder(tribe, kid, pos, 5f);
            GD.Print($"[Leader] {_owner.NpcName} orders {kid} ({sheltersHave+1}/{sheltersNeeded}).");
            return;
        }

        // ── Rule 3: Storehouse — Zone 0 ──────────────────────────────────
        if (members >= 5 && Has(knowledge, "storehouse", 0.15f)
            && existingOf(BuildingType.Storehouse) == 0 && pendingOf("storehouse") == 0)
        {
            PlaceOrder(tribe, "storehouse", center + Ring(0, 1, 5f), 8f);
            GD.Print($"[Leader] {_owner.NpcName} orders storehouse.");
            return;
        }

        // ── Rule 4: Workshop — Zone 2, near stone ────────────────────────
        if (Has(knowledge, "tools", 0.25f) && Has(knowledge, "axe", 0.2f)
            && existingOf(BuildingType.Workshop) == 0 && pendingOf("workshop") == 0)
        {
            var pos = BestProductionSpot(center, ResourceType.Stone, 16f);
            PlaceOrder(tribe, "workshop", pos, 8f);
            GD.Print($"[Leader] {_owner.NpcName} orders workshop near stone.");
            return;
        }

        // ── Rule 5: Farm — Zone 2, near water/berries ────────────────────
        if (Has(knowledge, "farming", 0.2f) && existingOf(BuildingType.Farm) < 2
            && pendingOf("farm") == 0)
        {
            var pos = BestProductionSpot(center, ResourceType.Food, 16f);
            PlaceOrder(tribe, "farm", pos, 12f);
            GD.Print($"[Leader] {_owner.NpcName} orders farm near food.");
            return;
        }

        // ── Rule 6: Well — Zone 0/1 ───────────────────────────────────────
        if (Has(knowledge, "pottery", 0.2f)
            && existingOf(BuildingType.Well) == 0 && pendingOf("well") == 0)
        {
            PlaceOrder(tribe, "well", center + Ring(0, 2, 6f), 5f);
            GD.Print($"[Leader] {_owner.NpcName} orders well.");
            return;
        }

        // ── Rule 7: Walls — Zone 3 ───────────────────────────────────────
        if (Has(knowledge, "wall", 0.1f) && members >= 8)
        {
            int wallsHave = existingOf(BuildingType.Wall) + pendingOf("wall");
            int wallsNeed = 6; // ring of walls
            if (wallsHave < wallsNeed)
            {
                var pos = center + Ring(3, wallsHave, 24f);
                PlaceOrder(tribe, "wall", pos, 4f);
                GD.Print($"[Leader] {_owner.NpcName} orders wall segment {wallsHave+1}/{wallsNeed}.");
                return;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void PlaceOrder(Tribe tribe, string knowledgeId, Vector3 pos, float minSpacing)
    {
        pos.Y = 0.5f;

        // Don't overlap existing buildings or pending orders
        bool tooClose = BuildOrderManager.Instance?.Orders
            .Any(o => o.GlobalPosition.DistanceTo(pos) < minSpacing) ?? false;
        tooClose = tooClose || (SettlementManager.Instance?.Buildings
            .Any(b => b.GlobalPosition.DistanceTo(pos) < minSpacing) ?? false);
        if (tooClose) return;

        var order = new BuildOrder();
        order.KnowledgeId  = knowledgeId;
        order.Position     = pos;
        order.TribeId      = tribe.Name;
        order.IsAutonomous = true;
        _owner.GetParent().CallDeferred(Node.MethodName.AddChild, order);
    }

    /// <summary>Find best production spot: prefer positions near given resource type.</summary>
    private Vector3 BestProductionSpot(Vector3 center, ResourceType resType, float zoneRadius)
    {
        // Try to find a resource node nearby
        var node = ResourceManager.Instance?.FindNearest(center, resType, zoneRadius * 2f);
        if (node != null)
        {
            // Place production building halfway between center and resource
            var dir = (node.GlobalPosition - center).Normalized();
            return center + dir * zoneRadius;
        }
        // Fallback: just place at zone radius
        return center + Ring(2, 0, zoneRadius);
    }

    private Tribe GetTribe()
        => TribeManager.Instance?.Tribes.FirstOrDefault(t => t.Name == _owner.TribeId);

    private static Dictionary<string, float> CollectTribeKnowledge(Tribe tribe)
    {
        var pool = new Dictionary<string, float>();
        foreach (var npc in tribe.Members)
            foreach (var kv in npc.Knowledge.Knowledge)
                if (!pool.ContainsKey(kv.Key) || pool[kv.Key] < kv.Value.Depth)
                    pool[kv.Key] = kv.Value.Depth;
        return pool;
    }

    private static bool Has(Dictionary<string, float> k, string id, float minDepth)
        => k.TryGetValue(id, out float d) && d >= minDepth;

    /// <summary>Evenly spaced ring position (6 slots per ring by default).</summary>
    private static Vector3 Ring(int ring, int slot, float radius)
    {
        int slots = ring == 0 ? 4 : 6 + ring * 2;
        float angle = slot * Mathf.Tau / slots;
        return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
    }

    private static Vector3 SmallJitter()
        => new Vector3((float)GD.RandRange(-1.5, 1.5), 0, (float)GD.RandRange(-1.5, 1.5));
}

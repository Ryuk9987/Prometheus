#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SettlementManager — autonomous settlement building logic.
///
/// Each tribe evaluates its needs and issues BuildOrders automatically
/// based on collective knowledge and current situation.
///
/// Priority chain (autonomous):
///   1. Campfire (fire knowledge) — warmth + social hub
///   2. Shelter/Hut — basic housing for members
///   3. Storehouse — if food surplus exists
///   4. Workshop — if tools knowledge deep enough
///   5. Farm — if agriculture knowledge deep enough
///   6. Well — if near water source
///   7. Wall — if tribe is threatened (future)
///   8. Road — connects settlement to resources (planning knowledge)
///   9. Town planning — organized layout (specialization knowledge)
/// </summary>
public partial class SettlementManager : Node
{
    public static SettlementManager Instance { get; private set; }

    private double _tickTimer = 0;
    private const double TickInterval = 15.0; // evaluate every 15 seconds

    // All completed buildings in the world
    private readonly List<CompletedBuilding> _buildings = new();

    public IReadOnlyList<CompletedBuilding> Buildings => _buildings;

    public override void _Ready() => Instance = this;

    public void RegisterBuilding(CompletedBuilding b)   => _buildings.Add(b);
    public void UnregisterBuilding(CompletedBuilding b) => _buildings.Remove(b);

    public override void _Process(double delta)
    {
        _tickTimer += delta;
        if (_tickTimer < TickInterval) return;
        _tickTimer = 0;
        EvaluateAll();
    }

    // ── Evaluation ────────────────────────────────────────────────────────
    private void EvaluateAll()
    {
        if (TribeManager.Instance == null) return;
        foreach (var tribe in TribeManager.Instance.Tribes)
            EvaluateTribe(tribe);
    }

    private void EvaluateTribe(Tribe tribe)
    {
        var knowledge = CollectTribeKnowledge(tribe);
        var center    = TribeCenter(tribe);
        int members   = tribe.Members.Count;
        if (members == 0) return;

        // Count existing buildings per type for this tribe
        var existing = _buildings
            .Where(b => b.TribeId == tribe.Name)
            .GroupBy(b => b.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        int Count(BuildingType t) => existing.TryGetValue(t, out int n) ? n : 0;

        // ── Rule 1: Campfire — handled exclusively by CampfireBehavior on NPCs.
        // SettlementManager does NOT place campfires to avoid duplicate campfire objects.
        // (CampfireBehavior creates proper Campfire nodes with fuel/burn mechanics.)

        // Also count pending BuildOrders as "already being built"
        int pendingOf(string kid) => BuildOrderManager.Instance?.Orders
            .Count(o => o.KnowledgeId == kid && o.TribeId == tribe.Name) ?? 0;

        // ── Rule 2: Shelter — 1 per 3 members (max 4), threshold 0.05
        int sheltersNeeded = Mathf.Min(members / 3, 4);
        int sheltersHave   = Count(BuildingType.Shelter) + Count(BuildingType.Hut)
                           + Count(BuildingType.WoodenHut)
                           + pendingOf("shelter") + pendingOf("hut");
        if (Has(knowledge, "shelter", 0.05f) && sheltersHave < sheltersNeeded)
        {
            string kId = Has(knowledge, "wooden_shelter", 0.2f) ? "wooden_shelter"
                       : Has(knowledge, "hut", 0.2f)            ? "hut"
                       : "shelter";
            var bt = kId == "wooden_shelter" ? BuildingType.WoodenHut
                   : kId == "hut"            ? BuildingType.Hut
                   : BuildingType.Shelter;
            PlaceAutonomous(tribe, kId, bt, center, SpiralOffset(sheltersHave, 6f));
            return;
        }

        // ── Rule 3: Storehouse — if tribe >= 5 members
        if (members >= 5 && Has(knowledge, "storehouse", 0.15f)
            && Count(BuildingType.Storehouse) == 0 && pendingOf("storehouse") == 0)
        {
            PlaceAutonomous(tribe, "storehouse", BuildingType.Storehouse,
                center, SpiralOffset(1, 8f));
            return;
        }

        // ── Rule 4: Workshop — if tools known
        if (Has(knowledge, "axe", 0.25f) && Has(knowledge, "tools", 0.25f)
            && Count(BuildingType.Workshop) == 0 && pendingOf("workshop") == 0)
        {
            PlaceAutonomous(tribe, "workshop", BuildingType.Workshop,
                center, SpiralOffset(2, 8f));
            return;
        }

        // ── Rule 5: Farm — if agriculture known
        if (Has(knowledge, "farming", 0.2f) && Count(BuildingType.Farm) < 2)
        {
            PlaceAutonomous(tribe, "farm", BuildingType.Farm,
                center, SpiralOffset(Count(BuildingType.Farm) + 3, 12f));
            return;
        }

        // ── Rule 6: Well — if pottery known
        if (Has(knowledge, "pottery", 0.2f)
            && Count(BuildingType.Well) == 0 && pendingOf("well") == 0)
        {
            PlaceAutonomous(tribe, "well", BuildingType.Well,
                center, SpiralOffset(4, 7f));
            return;
        }

        // ── Rule 7: Roads — connect center to resources (needs specialization)
        if (Has(knowledge, "specialization", 0.3f))
            EvaluateRoads(tribe, knowledge, center);
    }

    // ── Road logic ────────────────────────────────────────────────────────
    private void EvaluateRoads(Tribe tribe, Dictionary<string,float> knowledge, Vector3 center)
    {
        // Find nearest water, food, forest — build road toward it
        var targets = new List<Vector3>();

        // Nearest water
        var water = NatureManager.Instance?.Objects
            .FirstOrDefault(o => o.ObjType == NatureObjectType.WaterSource);
        if (water != null) targets.Add(water.GlobalPosition);

        // Nearest food cluster
        var food = NatureManager.Instance?.Objects
            .Where(o => o.ObjType == NatureObjectType.BushBerry)
            .OrderBy(o => o.GlobalPosition.DistanceTo(center))
            .FirstOrDefault();
        if (food != null) targets.Add(food.GlobalPosition);

        foreach (var target in targets)
        {
            // Place road segments every 3 units
            float dist = center.DistanceTo(target);
            int segments = (int)(dist / 3f);
            int existing = _buildings.Count(b => b.TribeId == tribe.Name && b.Type == BuildingType.Road);
            if (existing >= segments) continue;

            var dir = (target - center).Normalized();
            var pos = center + dir * (existing * 3f + 2f);
            PlaceAutonomous(tribe, "road", BuildingType.Road, pos, Vector3.Zero);
            return;
        }
    }

    // ── Build order placement ─────────────────────────────────────────────
    private const float MinBuildingSpacing    = 4f;   // between buildings of same tribe
    private const float MinSettlementSpacing  = 30f;  // between different tribe centers

    private void PlaceAutonomous(Tribe tribe, string knowledgeId, BuildingType bt,
        Vector3 center, Vector3 offset)
    {
        var pos = center + offset;
        pos.Y = 0.5f;

        // Don't overlap existing buildings or orders
        bool tooClose = BuildOrderManager.Instance?.Orders
            .Any(o => o.GlobalPosition.DistanceTo(pos) < MinBuildingSpacing) ?? false;
        tooClose = tooClose || _buildings.Any(b => b.GlobalPosition.DistanceTo(pos) < MinBuildingSpacing);
        if (tooClose) return;

        // Splinter settlements must be far enough from other tribe centers
        if (TribeManager.Instance != null)
        {
            foreach (var other in TribeManager.Instance.Tribes)
            {
                if (other.Name == tribe.Name) continue;
                if (other.Center.DistanceTo(center) < MinSettlementSpacing)
                {
                    // Push building away from existing settlement
                    var awayDir = (center - other.Center).Normalized();
                    pos = other.Center + awayDir * MinSettlementSpacing + offset;
                    pos.Y = 0.5f;
                    break;
                }
            }
        }

        var order = new BuildOrder();
        order.KnowledgeId = knowledgeId;
        order.Position    = pos;
        order.TribeId     = tribe.Name;
        order.IsAutonomous = true;
        GetParent().CallDeferred(Node.MethodName.AddChild, order);

        GD.Print($"[Settlement] {tribe.Name} autonomously placing {knowledgeId} at {pos}");
    }

    // ── Spawn completed building when BuildOrder finishes ─────────────────
    public void OnBuildOrderCompleted(BuildOrder order)
    {
        // Campfires spawn a real Campfire node (with fuel/burn mechanics), not a CompletedBuilding.
        // Also release the pending site claim so CampfireManager knows the spot is taken.
        if (order.KnowledgeId == "campfire" || order.KnowledgeId == "campfire_stone")
        {
            CampfireManager.Instance?.ReleaseSite(order.GlobalPosition);
            var fire = new Campfire();
            fire.WithStoneRing = order.KnowledgeId == "campfire_stone";
            fire.Position = order.GlobalPosition;
            GetParent().CallDeferred(Node.MethodName.AddChild, fire);
            return;
        }

        var bt = KnowledgeIdToBuildingType(order.KnowledgeId);
        if (bt == null) return;

        var building = new CompletedBuilding();
        building.Type    = bt.Value;
        building.TribeId = order.TribeId;
        building.Position = order.GlobalPosition;
        GetParent().CallDeferred(Node.MethodName.AddChild, building);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    public static BuildingType? KnowledgeIdToBuildingType(string id) => id switch {
        "campfire" or "campfire_stone" => BuildingType.Campfire,
        "shelter"                      => BuildingType.Shelter,
        "hut"                          => BuildingType.Hut,
        "wooden_shelter"               => BuildingType.WoodenHut,
        "storehouse"                   => BuildingType.Storehouse,
        "workshop"                     => BuildingType.Workshop,
        "farming"                      => BuildingType.Farm,
        "well"                         => BuildingType.Well,
        "wall"                         => BuildingType.Wall,
        "road"                         => BuildingType.Road,
        _ => null
    };

    private static Dictionary<string,float> CollectTribeKnowledge(Tribe tribe)
    {
        var pool = new Dictionary<string, float>();
        foreach (var npc in tribe.Members)
            foreach (var kv in npc.Knowledge.Knowledge)
                if (!pool.ContainsKey(kv.Key) || pool[kv.Key] < kv.Value.Depth)
                    pool[kv.Key] = kv.Value.Depth;
        return pool;
    }

    private static bool Has(Dictionary<string,float> k, string id, float minDepth)
        => k.TryGetValue(id, out float d) && d >= minDepth;

    private static Vector3 TribeCenter(Tribe tribe)
    {
        if (tribe.Members.Count == 0) return Vector3.Zero;
        var sum = Vector3.Zero;
        foreach (var m in tribe.Members) sum += m.GlobalPosition;
        return sum / tribe.Members.Count;
    }

    private static Vector3 SpiralOffset(int index, float radius)
    {
        float angle = index * Mathf.Tau / 6f; // 60° steps
        return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
    }
}

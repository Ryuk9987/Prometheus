#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SettlementManager — pure data store for completed buildings.
///
/// All autonomous building decisions are now made exclusively by LeaderBehavior.
/// This class no longer evaluates tribes or places BuildOrders itself.
/// </summary>
public partial class SettlementManager : Node
{
    public static SettlementManager Instance { get; private set; }

    private readonly List<CompletedBuilding> _buildings = new();
    public IReadOnlyList<CompletedBuilding> Buildings => _buildings;

    public override void _Ready() => Instance = this;

    public void RegisterBuilding(CompletedBuilding b)   => _buildings.Add(b);
    public void UnregisterBuilding(CompletedBuilding b) => _buildings.Remove(b);

    /// <summary>Called by BuildOrder when construction completes.</summary>
    public void OnBuildOrderCompleted(BuildOrder order)
    {
        // Campfires get a real Campfire node (with fuel/burn mechanics)
        if (order.KnowledgeId == "campfire" || order.KnowledgeId == "campfire_stone")
        {
            CampfireManager.Instance?.ReleaseSite(order.GlobalPosition);
            var fire = new Campfire();
            fire.WithStoneRing = order.KnowledgeId == "campfire_stone";
            fire.Position = order.GlobalPosition;
            GetParent().CallDeferred(Node.MethodName.AddChild, fire);
            return;
        }

        // All other buildings become CompletedBuilding nodes
        var bt = KnowledgeIdToBuildingType(order.KnowledgeId);
        if (bt == null) return;

        var building = new CompletedBuilding();
        building.Type     = bt.Value;
        building.TribeId  = order.TribeId;
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
}

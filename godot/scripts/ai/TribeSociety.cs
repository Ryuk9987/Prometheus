#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

public enum SocialRole
{
    Unassigned,
    Leader,     // Decides, organizes — 1 per tribe
    Hunter,     // Provides meat (2-3 per tribe)
    Gatherer,   // Collects plants/berries (2-3)
    Builder,    // Constructs buildings (1-2)
    Healer,     // Treats sick NPCs (0-1)
    Farmer,     // Tends fields (0-2, unlocked later)
    Guard,      // Patrols territory (0-2, unlocked later)
    Child,      // Too young to work
}

/// <summary>
/// TribeSociety — manages role assignment per tribe.
/// The leader evaluates every 30 seconds and assigns roles based on:
///  - Tribe size → determines how many of each role are needed
///  - NPC personality/knowledge → determines who fits each role best
///  - Survival needs → starving tribe needs more hunters/gatherers
///
/// Minimum viable tribe (survival):
///   1 Leader, 2 Hunters, 2 Gatherers, 1 Builder = 6 NPCs
/// </summary>
public partial class TribeSociety : Node
{
    public static TribeSociety Instance { get; private set; }

    private double _reviewTimer = 0;
    private const double ReviewInterval = 30.0;

    public override void _Ready()
    {
        Instance = this;
        // Initial role assignment after NPCs are registered
        CallDeferred(MethodName.ReviewAll);
    }

    public override void _Process(double delta)
    {
        _reviewTimer += delta;
        if (_reviewTimer < ReviewInterval) return;
        _reviewTimer = 0;
        ReviewAll();
    }

    public void ReviewAll()
    {
        if (TribeManager.Instance == null) return;
        foreach (var tribe in TribeManager.Instance.Tribes)
            AssignRoles(tribe);
    }

    // ── Role assignment ───────────────────────────────────────────────────
    public void AssignRoles(Tribe tribe)
    {
        var members = tribe.Members.Where(n => n.Age >= 14).ToList();
        int count = members.Count;
        if (count == 0) return;

        // Calculate needed counts based on tribe size
        int needLeader   = 1;
        int needHunters  = Mathf.Max(2, count / 5);
        int needGatherers= Mathf.Max(2, count / 5);
        // Small tribes (< 5): no dedicated builder — leader builds personally (via BuildWorker fallback)
        // Larger tribes: assign a dedicated builder
        int needBuilders = count >= 5 ? 1 : 0;
        int needHealers  = count >= 8 ? 1 : 0;
        int needFarmers  = count >= 10 && HasTribeKnowledge(tribe, "farming", 0.3f) ? Mathf.Max(1, count / 10) : 0;

        // Check survival pressure: if hungry, more hunters/gatherers
        float avgHunger = members.Average(n => n.Needs.Hunger);
        if (avgHunger > 0.6f)
        {
            needHunters   += 1;
            needGatherers += 1;
            needBuilders   = Mathf.Max(0, needBuilders - 1);
        }

        // Sort by fitness for each role
        // Leader: highest belief + cooperation experience
        var ranked = members.OrderByDescending(n =>
            n.Belief.Belief * 0.4f +
            n.Personality.Empathy * 0.3f +
            n.Knowledge.Knowledge.Count * 0.02f).ToList();

        // Clear all roles before re-assigning
        foreach (var n in tribe.Members) n.SocialRole = SocialRole.Unassigned;
        // Re-filter after clear (age check)
        members = tribe.Members.Where(n => n.Age >= 14).ToList();
        count = members.Count;
        if (count == 0) return;

        // Assign leader — always use TribeManager's elected Leader (single source of truth)
        // TribeManager elects by knowledge + belief; we just sync the SocialRole here.
        int idx = 0;
        if (tribe.Leader != null && tribe.Members.Contains(tribe.Leader))
        {
            tribe.Leader.SocialRole = SocialRole.Leader;
        }
        else
        {
            AssignBest(ranked, ref idx, SocialRole.Leader, needLeader,
                n => n.Belief.Belief * 0.4f + n.Personality.Empathy * 0.4f + n.Knowledge.Knowledge.Count * 0.02f);
        }

        // Hunters: high courage, hunting knowledge
        AssignBest(ranked, ref idx, SocialRole.Hunter, needHunters,
            n => n.Personality.Courage * 0.5f +
                 (n.Knowledge.Knows("hunting") ? n.Knowledge.Knowledge["hunting"].Depth * 0.5f : 0f));

        // Gatherers: high curiosity + foraging knowledge
        AssignBest(ranked, ref idx, SocialRole.Gatherer, needGatherers,
            n => n.Personality.Curiosity * 0.4f +
                 (n.Knowledge.Knows("food_gathering") ? 0.3f : 0f) +
                 (n.Knowledge.Knows("herbal_medicine") ? 0.2f : 0f));

        // Builders: tools + construction knowledge
        AssignBest(ranked, ref idx, SocialRole.Builder, needBuilders,
            n => (n.Knowledge.Knows("tools")   ? n.Knowledge.Knowledge["tools"].Depth * 0.4f : 0f) +
                 (n.Knowledge.Knows("shelter") ? n.Knowledge.Knowledge["shelter"].Depth * 0.3f : 0f) +
                 (n.Knowledge.Knows("axe")     ? 0.3f : 0f));

        // Healers: medicine + empathy
        AssignBest(ranked, ref idx, SocialRole.Healer, needHealers,
            n => n.Personality.Empathy * 0.3f +
                 (n.Knowledge.Knows("medicine") ? n.Knowledge.Knowledge["medicine"].Depth * 0.7f : 0f));

        // Farmers
        AssignBest(ranked, ref idx, SocialRole.Farmer, needFarmers,
            n => (n.Knowledge.Knows("farming") ? n.Knowledge.Knowledge["farming"].Depth * 0.8f : 0f));

        // Remaining = unassigned (will default to gathering/wandering)
        GD.Print($"[Society] {tribe.Name}: {SummaryFor(members)}");
    }

    private static void AssignBest(List<NpcEntity> pool, ref int startIdx,
        SocialRole role, int count, System.Func<NpcEntity, float> score)
    {
        if (count <= 0) return;
        // Only pick from truly unassigned NPCs
        var unassigned = pool
            .Where(n => n.SocialRole == SocialRole.Unassigned)
            .OrderByDescending(score)
            .Take(count)
            .ToList();
        foreach (var n in unassigned) n.SocialRole = role;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static bool HasTribeKnowledge(Tribe tribe, string id, float minDepth)
        => tribe.Members.Any(n => n.Knowledge.Knows(id) &&
                                  n.Knowledge.Knowledge[id].Depth >= minDepth);

    private static string SummaryFor(List<NpcEntity> members)
    {
        var counts = members.GroupBy(n => n.SocialRole)
            .Select(g => $"{g.Key}:{g.Count()}");
        return string.Join(" ", counts);
    }
}

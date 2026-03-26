#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Autonomous knowledge discovery based on real NPC experience.
///
/// Runs every 8 seconds. Checks concrete conditions in the world
/// and grants knowledge proportional to how often the NPC encounters
/// a relevant situation.
///
/// Also: deepens existing knowledge through repeated use (skill progression).
/// </summary>
public partial class AutonomousDiscovery : Node
{
    private NpcEntity _owner;
    private double    _timer = 0;
    private const double Interval = 8.0;

    // Experience counters (reset after discovery)
    private int _hungerEvents   = 0;
    private int _fireExposure   = 0;
    private int _buildEvents    = 0;
    private int _socialEvents   = 0;
    private int _forageEvents   = 0;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
        _timer = GD.RandRange(0.0, Interval);
    }

    public override void _Process(double delta)
    {
        // Accumulate experience every frame
        if (_owner.Needs.IsHungry)  _hungerEvents++;
        if (_owner.CampfireBuilder.IsActive || NearFire()) _fireExposure++;
        if (_owner.BuildWorker.IsActive)  _buildEvents++;
        if (_owner.Foraging.IsActive)     _forageEvents++;

        int near = GameManager.Instance?.AllNpcs
            .Count(n => n != _owner && n.GlobalPosition.DistanceTo(_owner.GlobalPosition) < 5f) ?? 0;
        if (near >= 2) _socialEvents++;

        _timer += delta;
        if (_timer < Interval) return;
        _timer = 0;

        TryDiscover();
        DeepensExisting();
    }

    // ── Experience-based discoveries ─────────────────────────────────────
    private void TryDiscover()
    {
        var known    = _owner.Knowledge.Knowledge;
        float curiosity = _owner.Personality.Curiosity;
        var rng = new RandomNumberGenerator(); rng.Randomize();

        // ── Fire: hungry + near wood at night
        if (!Has("fire") && _hungerEvents > 5)
        {
            bool isNight = DayCycle.Instance?.IsNight ?? false;
            bool hasWood = _owner.Inventory?.Get(ResourceType.Branch) > 0f
                        || NearNature(NatureObjectType.TreeOak, NatureObjectType.TreePine, NatureObjectType.TreeBirch);
            if (hasWood && (isNight || _hungerEvents > 15))
                Discover("fire", 0.2f, 0.5f, "discovered fire from necessity");
        }

        // ── Sharp stone: has stone in inventory + used tools
        if (!Has("sharp_stone") && Has("stone") && _forageEvents > 10)
            Discover("sharp_stone", 0.2f, 0.4f, "shaped a stone tool");

        // ── Tools: has sharp stone + branches
        if (!Has("tools") && Has("sharp_stone", 0.15f)
            && (_owner.Inventory?.Get(ResourceType.Branch) > 0f || Has("wood")))
            Discover("tools", 0.15f, 0.4f, "combined stone and branch");

        // ── Axe: uses tools regularly near trees
        if (!Has("axe") && Has("tools", 0.3f) && _buildEvents + _forageEvents > 20)
            Discover("axe", 0.15f, 0.35f, "made a heavier tool for cutting");

        // ── Shelter: repeatedly cold/wet, knows fire
        if (!Has("shelter") && Has("fire") && _hungerEvents > 20)
            Discover("shelter", 0.15f, 0.35f, "thought of building shelter");

        // ── Cooking: near fire with food
        if (!Has("cooking") && Has("fire", 0.2f) && _fireExposure > 30
            && (_owner.Needs.IsHungry || Has("food_gathering")))
            Discover("cooking", 0.2f, 0.4f, "cooked food over fire");

        // ── Fire starter: uses fire a lot
        if (!Has("fire_starter") && Has("fire", 0.3f) && Has("stone", 0.2f) && _fireExposure > 50)
            Discover("fire_starter", 0.2f, 0.4f, "made fire with flint stones");

        // ── Medicine: near fire + has food knowledge
        if (!Has("medicine") && Has("fire", 0.4f) && _fireExposure > 60 && curiosity > 0.5f)
        {
            bool nearFood = NearNature(NatureObjectType.BushBerry, NatureObjectType.MushroomEdible);
            if (nearFood) Discover("medicine", 0.15f, 0.3f, "found healing plants near fire");
        }

        // ── Language: lots of social contact
        if (!Has("language") && _socialEvents > 100)
            Discover("language", 0.15f, 0.4f, "developed language from social contact");

        // ── Agriculture: hungry + near berry bushes
        if (!Has("agriculture") && _hungerEvents > 30
            && NearNature(NatureObjectType.BushBerry))
            Discover("agriculture", 0.1f, 0.25f, "noticed plants growing from seeds");

        // ── Pottery: knows fire + stone, builder
        if (!Has("pottery") && Has("fire", 0.3f) && Has("stone", 0.2f)
            && _buildEvents > 20 && curiosity > 0.4f)
            Discover("pottery", 0.1f, 0.3f, "shaped clay with fire");

        // ── Lumber: uses axe + builds
        if (!Has("lumber") && Has("axe", 0.2f) && _buildEvents > 30)
            Discover("lumber", 0.15f, 0.35f, "processed wood into planks");

        // Wellbeing-driven discovery hints
        var hint = _owner.Wellbeing?.GetDiscoveryHint();
        if (hint != null && !_owner.Knowledge.Knows(hint))
            Discover(hint, 0.1f, 0.3f, $"driven by wellbeing need ({hint})");

        // Reset counters
        _hungerEvents = Mathf.Max(0, _hungerEvents - 5);
        _fireExposure = Mathf.Max(0, _fireExposure - 10);
        _buildEvents  = Mathf.Max(0, _buildEvents  - 5);
        _socialEvents = Mathf.Max(0, _socialEvents - 20);
        _forageEvents = Mathf.Max(0, _forageEvents - 5);
    }

    // ── Skill deepening through use ────────────────────────────────────────
    private void DeepensExisting()
    {
        const float GainPerUse = 0.005f;

        // Fire knowledge deepens through campfire use
        if (_fireExposure > 5 && Has("fire"))
            _owner.Knowledge.Verify("fire", GainPerUse);

        // Tool knowledge deepens through building
        if (_buildEvents > 3 && Has("tools"))
            _owner.Knowledge.Verify("tools", GainPerUse);

        // Agriculture deepens through foraging
        if (_forageEvents > 5 && Has("agriculture"))
            _owner.Knowledge.Verify("agriculture", GainPerUse);

        // Hunting deepens through cooperation tasks
        if (_owner.Cooperation.HasTask && Has("hunting"))
            _owner.Knowledge.Verify("hunting", GainPerUse * 2f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private bool Has(string id, float minDepth = 0.01f)
        => _owner.Knowledge.Knows(id) && _owner.Knowledge.Knowledge[id].Depth >= minDepth;

    private bool NearFire()
        => CampfireManager.Instance?.FindNearest(_owner.GlobalPosition, 8f) != null;

    private bool NearNature(params NatureObjectType[] types)
    {
        if (NatureManager.Instance == null) return false;
        foreach (var o in NatureManager.Instance.Objects)
        {
            if (!types.Contains(o.ObjType)) continue;
            if (o.GlobalPosition.DistanceTo(_owner.GlobalPosition) < 8f) return true;
        }
        return false;
    }

    private void Discover(string id, float depth, float confidence, string reason)
    {
        _owner.Knowledge.Learn(id, depth, confidence, "experience");
        GD.Print($"[Discovery] {_owner.NpcName} → {id} ({reason})");
    }
}

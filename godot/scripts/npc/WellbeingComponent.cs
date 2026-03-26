#nullable disable
using Godot;
using System.Linq;

/// <summary>
/// WellbeingComponent — tracks Safety, Comfort, Temperature and derives
/// an overall Wellbeing score (0=miserable, 1=thriving).
///
/// Effects on NPC behavior:
///   Low Safety   → flees toward group/fire, refuses to wander far
///   Low Comfort  → seeks shelter, builds/requests better housing
///   Cold (temp)  → seeks fire, refuses to work outdoors, learns clothing
///   High Wellbeing → higher belief gain, faster knowledge learning, more social
///
/// All values: 0=worst, 1=best.
/// </summary>
public partial class WellbeingComponent : Node
{
    // ── Current values ────────────────────────────────────────────────────
    public float Safety      { get; private set; } = 0.5f;   // 0=terrified, 1=fully safe
    public float Comfort     { get; private set; } = 0.3f;   // 0=miserable, 1=cozy
    public float Temperature { get; private set; } = 0.6f;   // 0=freezing, 1=warm
    public float Wellbeing   { get; private set; } = 0.5f;   // composite

    // ── Thresholds ────────────────────────────────────────────────────────
    public bool IsCold        => Temperature < 0.3f;
    public bool IsChilly      => Temperature < 0.5f;
    public bool IsComfortable => Comfort     > 0.6f;
    public bool FeelsSafe     => Safety      > 0.5f;
    public bool IsContent     => Wellbeing   > 0.65f;
    public bool IsSuffering   => Wellbeing   < 0.3f;

    private NpcEntity _owner;

    // State tracking
    private double _updateTimer = 0;
    private const double UpdateInterval = 3.0; // evaluate every 3 seconds

    // World temp baseline (changes with day/night)
    private float _worldTemp = 0.6f;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
        if (GameManager.Instance != null)
            GameManager.Instance.Connect(GameManager.SignalName.WorldTick,
                Callable.From<double>(OnWorldTick));
    }

    public override void _Process(double delta)
    {
        _updateTimer += delta;
        if (_updateTimer < UpdateInterval) return;
        _updateTimer = 0;
        Evaluate();
    }

    private void Evaluate()
    {
        EvaluateTemperature();
        EvaluateSafety();
        EvaluateComfort();
        RecalcWellbeing();
        ApplyEffects();
    }

    // ── Temperature ───────────────────────────────────────────────────────
    private void EvaluateTemperature()
    {
        // Base: world temperature (day = warm, night = cold)
        bool isNight = DayCycle.Instance?.IsNight ?? false;
        float hour   = DayCycle.Instance?.Hour ?? 12f;
        _worldTemp   = isNight ? 0.2f : (hour >= 10f && hour <= 16f ? 0.75f : 0.5f);

        float temp = _worldTemp;

        // +Heat from nearby campfire
        var fire = CampfireManager.Instance?.FindNearest(_owner.GlobalPosition, 10f);
        if (fire != null && fire.IsBurning)
        {
            float dist = _owner.GlobalPosition.DistanceTo(fire.GlobalPosition);
            float heat = Mathf.Lerp(0.5f, 0.1f, dist / 10f);
            temp = Mathf.Min(1f, temp + heat);
        }

        // +Heat from completed building (shelter/hut)
        var nearBuilding = SettlementManager.Instance?.Buildings
            .Where(b => b.ShelterCapacity > 0 && b.GlobalPosition.DistanceTo(_owner.GlobalPosition) < b.InfluenceRadius)
            .OrderBy(b => b.GlobalPosition.DistanceTo(_owner.GlobalPosition))
            .FirstOrDefault();
        if (nearBuilding != null)
        {
            float shelterBonus = nearBuilding.Type switch {
                BuildingType.Hut       => 0.35f,
                BuildingType.WoodenHut => 0.4f,
                BuildingType.Shelter   => 0.2f,
                _ => 0.1f
            };
            temp = Mathf.Min(1f, temp + shelterBonus);
        }

        // +Clothing knowledge reduces cold sensitivity
        if (_owner.Knowledge.Knows("preservation") || _owner.Knowledge.Knows("herbal_medicine"))
            temp = Mathf.Min(1f, temp + 0.1f);

        Temperature = Mathf.Lerp(Temperature, temp, 0.15f); // smooth
    }

    // ── Safety ────────────────────────────────────────────────────────────
    private void EvaluateSafety()
    {
        float safety = 0.3f; // baseline: world is dangerous

        // +Safety from group size
        int nearAllies = GameManager.Instance?.AllNpcs
            .Count(n => n != _owner && n.TribeId == _owner.TribeId
                        && n.GlobalPosition.DistanceTo(_owner.GlobalPosition) < 10f) ?? 0;
        safety += Mathf.Min(0.4f, nearAllies * 0.08f);

        // +Safety from fire nearby
        if (CampfireManager.Instance?.FindNearest(_owner.GlobalPosition, 12f) != null)
            safety += 0.15f;

        // +Safety from buildings
        int nearBuildings = SettlementManager.Instance?.Buildings
            .Count(b => b.GlobalPosition.DistanceTo(_owner.GlobalPosition) < 15f) ?? 0;
        safety += Mathf.Min(0.2f, nearBuildings * 0.05f);

        // +Safety from walls
        int nearWalls = SettlementManager.Instance?.Buildings
            .Count(b => b.Type == BuildingType.Wall
                        && b.GlobalPosition.DistanceTo(_owner.GlobalPosition) < 20f) ?? 0;
        safety += Mathf.Min(0.1f, nearWalls * 0.05f);

        // -Safety: alone at night
        if ((DayCycle.Instance?.IsNight ?? false) && nearAllies == 0)
            safety -= 0.2f;

        Safety = Mathf.Lerp(Safety, Mathf.Clamp(safety, 0f, 1f), 0.2f);
    }

    // ── Comfort ───────────────────────────────────────────────────────────
    private void EvaluateComfort()
    {
        float comfort = 0.1f; // baseline: sleeping on ground is bad

        // +Comfort from shelter/hut nearby
        var bestBuilding = SettlementManager.Instance?.Buildings
            .Where(b => b.ShelterCapacity > 0 && b.GlobalPosition.DistanceTo(_owner.GlobalPosition) < b.InfluenceRadius)
            .OrderByDescending(b => b.ShelterCapacity)
            .FirstOrDefault();
        if (bestBuilding != null)
        {
            comfort += bestBuilding.Type switch {
                BuildingType.Hut       => 0.55f,
                BuildingType.WoodenHut => 0.65f,
                BuildingType.Shelter   => 0.3f,
                BuildingType.Storehouse => 0.1f,
                _ => 0.05f
            };
        }

        // +Comfort from fire
        var fire = CampfireManager.Instance?.FindNearest(_owner.GlobalPosition, 8f);
        if (fire != null && fire.IsBurning) comfort += 0.2f;

        // -Comfort: hunger/thirst
        comfort -= _owner.Needs.Hunger * 0.3f;
        comfort -= _owner.Needs.Thirst * 0.2f;

        // -Comfort: being cold
        if (IsCold) comfort -= 0.2f;

        Comfort = Mathf.Lerp(Comfort, Mathf.Clamp(comfort, 0f, 1f), 0.15f);
    }

    // ── Wellbeing composite ───────────────────────────────────────────────
    private void RecalcWellbeing()
    {
        // Weighted average of all factors
        float hunger   = 1f - _owner.Needs.Hunger;
        float thirst   = 1f - _owner.Needs.Thirst;
        float wb = hunger    * 0.30f
                 + thirst    * 0.20f
                 + Safety    * 0.20f
                 + Comfort   * 0.15f
                 + Temperature*0.15f;
        Wellbeing = Mathf.Lerp(Wellbeing, Mathf.Clamp(wb, 0f, 1f), 0.1f);
    }

    // ── Behavioral effects ────────────────────────────────────────────────
    private void ApplyEffects()
    {
        // High wellbeing → faster belief/knowledge growth
        if (IsContent)
        {
            _owner.Belief.Belief = Mathf.Min(1f, _owner.Belief.Belief + 0.001f);
        }

        // Suffering → lose belief in oracle
        if (IsSuffering)
        {
            _owner.Belief.Belief = Mathf.Max(0f, _owner.Belief.Belief - 0.003f);
        }

        // Cold → seek fire (overrides wander target)
        if (IsCold && _owner.SocialRole != SocialRole.Builder)
        {
            var fire = CampfireManager.Instance?.FindNearest(_owner.GlobalPosition, 40f);
            if (fire != null)
                _owner.TribeCenterHint = fire.GlobalPosition;
        }

        // Unsafe at night → seek group
        if (!FeelsSafe && (DayCycle.Instance?.IsNight ?? false))
        {
            var tribe = TribeManager.Instance?.GetTribe(_owner);
            if (tribe != null) _owner.TribeCenterHint = tribe.Center;
        }
    }

    // ── External triggers ─────────────────────────────────────────────────
    private void OnWorldTick(double delta)
    {
        // Cold causes hunger faster (body burns more energy)
        if (IsCold)
            _owner.Needs.Hunger = Mathf.Min(1f, _owner.Needs.Hunger + 0.01f);

        // Comfort triggers desire to improve shelter → discovery hint
        if (!IsComfortable && _owner.SocialRole == SocialRole.Builder)
        {
            // Builder with low comfort gets extra motivation to seek shelter knowledge
            if (!_owner.Knowledge.Knows("shelter"))
                _owner.Knowledge.Learn("shelter", 0.05f, 0.2f, "comfort_need");
        }
    }

    // ── Discovery hints ───────────────────────────────────────────────────
    /// <summary>Called by AutonomousDiscovery to check wellbeing-driven needs.</summary>
    public string GetDiscoveryHint()
    {
        if (IsCold && !_owner.Knowledge.Knows("fire_control")) return "fire_control";
        if (IsCold && _owner.Knowledge.Knows("fire") && !_owner.Knowledge.Knows("fire_starter")) return "fire_starter";
        if (!IsComfortable && _owner.Knowledge.Knows("lumber") && !_owner.Knowledge.Knows("wooden_shelter")) return "wooden_shelter";
        if (!FeelsSafe && !_owner.Knowledge.Knows("wall") && _owner.Knowledge.Knows("tools")) return "wall";
        return null;
    }
}

#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// NPCs discover new knowledge autonomously when they face repeated problems.
/// Examples:
///   - Hungry often + near wood → discovers fire (cooking)
///   - Getting wet (rain) + knows shelter → discovers improved shelter
///   - Near stone + knows tools → discovers better tools
///   - High curiosity near campfire → discovers medicine (from herbs near fire)
/// </summary>
public partial class AutonomousDiscovery : Node
{
    private NpcEntity _owner;
    private double    _timer    = 0;
    private const double Interval = 20.0; // check every 20 real seconds

    // Track problem frequency
    private int _hungerEvents = 0;
    private int _thirstEvents = 0;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
        // Stagger timers
        _timer = GD.RandRange(0.0, Interval);
    }

    public override void _Process(double delta)
    {
        // Track needs
        if (_owner.Needs.IsHungry)  _hungerEvents++;
        if (_owner.Needs.IsThirsty) _thirstEvents++;

        _timer += delta;
        if (_timer < Interval) return;
        _timer = 0;

        TryDiscover();
    }

    private void TryDiscover()
    {
        var known    = _owner.Knowledge.Knowledge;
        var rng      = new RandomNumberGenerator(); rng.Randomize();
        float curiosity = _owner.Personality.Curiosity;

        // ── Fire from friction ─────────────────────────────────────────────
        if (!known.ContainsKey("fire") && _hungerEvents > 3)
        {
            // Has wood nearby + cold night → might discover fire
            var wood = ResourceManager.Instance?.FindNearest(_owner.GlobalPosition, ResourceType.Wood, 5f);
            bool isNight = DayCycle.Instance?.IsNight ?? false;
            if (wood != null && isNight && rng.RandfRange(0f,1f) < curiosity * 0.3f)
            {
                _owner.Knowledge.Learn("fire", 0.15f, 0.4f, "autonomous");
                GD.Print($"[Discovery] {_owner.NpcName} discovered FIRE from necessity!");
                _hungerEvents = 0;
            }
        }

        // ── Better tools from repeated stone use ─────────────────────────
        if (known.ContainsKey("tools") && known["tools"].Depth > 0.5f
            && !known.ContainsKey("axe") && curiosity > 0.5f
            && rng.RandfRange(0f,1f) < 0.15f)
        {
            _owner.Knowledge.Learn("axe", 0.1f, 0.3f, "autonomous");
            GD.Print($"[Discovery] {_owner.NpcName} invented the AXE!");
        }

        // ── Shelter from rain ──────────────────────────────────────────────
        if (!known.ContainsKey("shelter") && known.ContainsKey("fire")
            && _hungerEvents > 5 && rng.RandfRange(0f,1f) < curiosity * 0.2f)
        {
            _owner.Knowledge.Learn("shelter", 0.12f, 0.35f, "autonomous");
            GD.Print($"[Discovery] {_owner.NpcName} thought of SHELTER!");
        }

        // ── Medicine near campfire ─────────────────────────────────────────
        if (!known.ContainsKey("medicine") && known.ContainsKey("fire")
            && known["fire"].Depth > 0.4f && curiosity > 0.65f)
        {
            var fire = CampfireManager.Instance?.FindNearest(_owner.GlobalPosition, 6f);
            var food = ResourceManager.Instance?.FindNearest(_owner.GlobalPosition, ResourceType.Food, 4f);
            if (fire != null && food != null && rng.RandfRange(0f,1f) < 0.1f)
            {
                _owner.Knowledge.Learn("medicine", 0.1f, 0.3f, "autonomous");
                GD.Print($"[Discovery] {_owner.NpcName} discovered MEDICINE near fire!");
            }
        }

        // ── Language from social contact ───────────────────────────────────
        if (!known.ContainsKey("language") && curiosity > 0.5f)
        {
            int nearbyNpcs = GameManager.Instance?.AllNpcs
                .Count(n => n != _owner && n.GlobalPosition.DistanceTo(_owner.GlobalPosition) < 5f) ?? 0;
            if (nearbyNpcs >= 2 && rng.RandfRange(0f,1f) < 0.05f)
            {
                _owner.Knowledge.Learn("language", 0.1f, 0.4f, "autonomous");
                GD.Print($"[Discovery] {_owner.NpcName} developed LANGUAGE from social contact!");
            }
        }

        // ── Agriculture from planting observation ─────────────────────────
        if (!known.ContainsKey("agriculture") && known.ContainsKey("fire")
            && _hungerEvents > 8 && rng.RandfRange(0f,1f) < curiosity * 0.1f)
        {
            _owner.Knowledge.Learn("agriculture", 0.08f, 0.25f, "autonomous");
            GD.Print($"[Discovery] {_owner.NpcName} thought about AGRICULTURE!");
            _hungerEvents = 0;
        }

        // Reset counters
        _hungerEvents = Mathf.Max(0, _hungerEvents - 1);
        _thirstEvents = Mathf.Max(0, _thirstEvents - 1);
    }
}

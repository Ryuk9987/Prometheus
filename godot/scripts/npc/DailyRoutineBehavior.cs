#nullable disable
using Godot;
using System.Linq;

/// <summary>
/// DailyRoutineBehavior — governs NPC behavior based on time of day.
///
/// Schedule:
///   06:00–08:00  Morning     — wake up, tend fire, eat (near campfire)
///   08:00–12:00  Work AM     — gather, build, hunt, craft
///   12:00–13:00  Midday Rest — eat, socialize near campfire
///   13:00–17:00  Work PM     — gather, build, hunt, craft
///   17:00–20:00  Evening     — socialize, teach, learn around fire
///   20:00–22:00  Dusk        — eat, wind down near campfire/shelter
///   22:00–06:00  Sleep       — move to shelter/fire, no work
///
/// This component BLOCKS other behaviors when sleeping or resting.
/// Other behaviors (Foraging, BuildWorker etc.) check IsWorkTime() before acting.
/// </summary>
public partial class DailyRoutineBehavior : Node
{
    public enum DailyPhase
    {
        Morning,    // 06–08: aufwachen, Feuer, essen
        WorkAM,     // 08–12: Arbeit
        MiddayRest, // 12–13: Mittagspause
        WorkPM,     // 13–17: Arbeit
        Evening,    // 17–20: Freizeit, sozial
        Dusk,       // 20–22: Abend, Essen, Runterkommen
        Sleep,      // 22–06: Schlafen
    }

    public static DailyPhase GetPhase(float hour) => hour switch {
        >= 6f  and < 8f  => DailyPhase.Morning,
        >= 8f  and < 12f => DailyPhase.WorkAM,
        >= 12f and < 13f => DailyPhase.MiddayRest,
        >= 13f and < 17f => DailyPhase.WorkPM,
        >= 17f and < 20f => DailyPhase.Evening,
        >= 20f and < 22f => DailyPhase.Dusk,
        _                => DailyPhase.Sleep,
    };

    public DailyPhase CurrentPhase => GetPhase(DayCycle.Instance?.Hour ?? 8f);

    /// <summary>True when NPC should be working (gathering, building, hunting, crafting).</summary>
    public bool IsWorkTime => CurrentPhase is DailyPhase.WorkAM or DailyPhase.WorkPM;

    /// <summary>True when NPC should be sleeping — blocks all active behaviors.</summary>
    public bool IsSleeping => CurrentPhase == DailyPhase.Sleep;

    /// <summary>True when NPC should be near fire/shelter (morning, rest, evening, dusk, sleep).</summary>
    public bool ShouldStayNearCamp => CurrentPhase is not DailyPhase.WorkAM and not DailyPhase.WorkPM;

    private NpcEntity _owner;
    private DailyPhase _lastPhase = DailyPhase.WorkAM;
    private Vector3 _sleepPosition;
    private bool    _sleepPositionSet = false;
    private const float MoveSpeed = 0.8f;
    private const float CampRadius = 12f;

    public override void _Ready() => _owner = GetParent<NpcEntity>();

    /// <summary>
    /// Called from NpcEntity._Process. Returns true if this behavior takes control.
    /// Sleep and rest phases block all work behaviors.
    /// </summary>
    public bool Tick(double delta)
    {
        if (DayCycle.Instance == null) return false;

        var phase = CurrentPhase;

        // Phase transition log
        if (phase != _lastPhase)
        {
            OnPhaseChanged(_lastPhase, phase);
            _lastPhase = phase;
        }

        // ── SLEEP: move to shelter/fire and stay put ──────────────────
        if (phase == DailyPhase.Sleep)
        {
            if (!_sleepPositionSet)
            {
                _sleepPosition    = FindSleepSpot();
                _sleepPositionSet = true;
            }
            var toSleep = _sleepPosition - _owner.GlobalPosition; toSleep.Y = 0;
            if (toSleep.Length() > 1.5f)
                _owner.GlobalPosition += toSleep.Normalized() * MoveSpeed * (float)delta;
            return true; // blocks everything
        }
        _sleepPositionSet = false;

        // ── MORNING: wake up, tend fire, eat — stay near camp ────────
        if (phase == DailyPhase.Morning)
        {
            StayNearCamp(delta);
            return true;
        }

        // ── MIDDAY REST: eat + socialize near fire ────────────────────
        if (phase == DailyPhase.MiddayRest)
        {
            StayNearCamp(delta);
            return true;
        }

        // ── EVENING: socialize, teach, sit around fire ────────────────
        if (phase == DailyPhase.Evening)
        {
            StayNearCamp(delta);
            return true;
        }

        // ── DUSK: wind down near fire/shelter ─────────────────────────
        if (phase == DailyPhase.Dusk)
        {
            StayNearCamp(delta);
            return true;
        }

        // Work phases: don't block
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void StayNearCamp(double delta)
    {
        var camp = FindNearestCampfire();
        if (camp == null) return;

        var toFire = camp.GlobalPosition - _owner.GlobalPosition; toFire.Y = 0;
        float dist = toFire.Length();

        if (dist > CampRadius)
        {
            // Walk toward fire
            _owner.GlobalPosition += toFire.Normalized() * MoveSpeed * 0.5f * (float)delta;
        }
        else if (dist < 2f)
        {
            // Too close — drift slightly
            var away = (_owner.GlobalPosition - camp.GlobalPosition).Normalized();
            _owner.GlobalPosition += away * MoveSpeed * 0.2f * (float)delta;
        }
        // else: comfortable distance, stay put (small idle wander handled by NpcEntity)
    }

    private Vector3 FindSleepSpot()
    {
        // Prefer a nearby shelter
        var shelter = SettlementManager.Instance?.Buildings
            .Where(b => b.TribeId == _owner.TribeId
                     && b.Type is BuildingType.Shelter or BuildingType.ShelterImproved
                                or BuildingType.ShelterMud or BuildingType.Hut
                                or BuildingType.WoodenHut)
            .OrderBy(b => b.GlobalPosition.DistanceTo(_owner.GlobalPosition))
            .FirstOrDefault();

        if (shelter != null)
            return shelter.GlobalPosition + new Vector3(
                GD.RandRange(-1.0f, 1.0f), 0, GD.RandRange(-1.0f, 1.0f));

        // Fall back to nearest campfire
        var fire = FindNearestCampfire();
        if (fire != null)
            return fire.GlobalPosition + new Vector3(
                GD.RandRange(-2.0f, 2.0f), 0, GD.RandRange(-2.0f, 2.0f));

        // Last resort: stay put
        return _owner.GlobalPosition;
    }

    private Campfire FindNearestCampfire()
        => CampfireManager.Instance?.Campfires
            .Where(c => c.IsBurning)
            .OrderBy(c => c.GlobalPosition.DistanceTo(_owner.GlobalPosition))
            .FirstOrDefault();

    private void OnPhaseChanged(DailyPhase from, DailyPhase to)
    {
        string hour = DayCycle.Instance?.TimeString ?? "??:??";
        GD.Print($"[Routine] {_owner.NpcName}: {from} → {to} ({hour})");

        // Consume food at meal times (morning + midday + dusk)
        if (to is DailyPhase.Morning or DailyPhase.MiddayRest or DailyPhase.Dusk)
        {
            if (_owner.Needs.IsHungry)
                _owner.Needs.Eat(0.5f); // eat a meal — reduces hunger
        }
    }
}

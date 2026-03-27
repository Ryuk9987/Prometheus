#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Autonomous knowledge discovery based on real NPC experience.
///
/// Runs every 8 seconds. Checks concrete conditions and grants knowledge
/// proportional to how often the NPC encounters a relevant situation.
///
/// Discovery chain (Stone Age realism):
///   fire_making → campfire → campfire_stone → campfire_pot_hook
///   shelter → shelter_improved → shelter_mud → hut
///   foraging → medicine → herbal_medicine
///   hunting → hide_working → clothing_basic
///   rope_making → axe / spear / fire_drill
///   stone → sharp_stone → tools → axe / shovel
///   language (innate, deepens with social contact)
/// </summary>
public partial class AutonomousDiscovery : Node
{
    private NpcEntity _owner;
    private double    _timer = 0;
    private const double Interval = 8.0;

    // Experience counters
    private int _hungerEvents   = 0;
    private int _fireExposure   = 0;
    private int _buildEvents    = 0;
    private int _socialEvents   = 0;
    private int _forageEvents   = 0;
    private int _huntEvents     = 0;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
        _timer = GD.RandRange(0.0, Interval);
    }

    public override void _Process(double delta)
    {
        if (_owner.Needs.IsHungry)              _hungerEvents++;
        if (NearFire() || _owner.CampfireBuilder.IsActive) _fireExposure++;
        if (_owner.BuildWorker.IsActive)        _buildEvents++;
        if (_owner.Foraging.IsActive)           _forageEvents++;

        int near = GameManager.Instance?.AllNpcs
            .Count(n => n != _owner && n.GlobalPosition.DistanceTo(_owner.GlobalPosition) < 5f) ?? 0;
        if (near >= 2) _socialEvents++;

        _timer += delta;
        if (_timer < Interval) return;
        _timer = 0;

        TryDiscover();
        DeepenExisting();
    }

    // ── Discovery logic ───────────────────────────────────────────────────
    private void TryDiscover()
    {
        float curiosity = _owner.Personality.Curiosity;
        float courage   = _owner.Personality.Courage;
        var rng = new RandomNumberGenerator(); rng.Randomize();

        // ── FEUER MACHEN (fire_making) ─────────────────────────────────
        // Alle NPCs haben Grund-Feuer-Wissen. fire_making entsteht durch Not + Holz + Kälte
        if (!Has("fire_making") && _hungerEvents > 10)
        {
            bool isNight = DayCycle.Instance?.IsNight ?? false;
            bool hasWood = _owner.Inventory?.Get(ResourceType.Branch) > 0f
                        || NearNature(NatureObjectType.TreeOak, NatureObjectType.TreePine, NatureObjectType.TreeBirch);
            if (hasWood && (isNight || _hungerEvents > 20))
                Discover("fire_making", 0.20f, 0.50f, "lernte Feuer durch Reiben von Stöcken zu machen");
        }

        // ── FEUER (fire) — kennen → wird tiefer durch Erfahrung ───────
        if (!Has("fire", 0.2f) && Has("fire_making") && _fireExposure > 5)
            Discover("fire", 0.20f, 0.50f, "verstand Feuer durch ständige Nutzung");

        // ── LAGERFEUER BAUEN (campfire) ────────────────────────────────
        if (!Has("campfire") && Has("fire_making", 0.2f) && _fireExposure > 15)
            Discover("campfire", 0.15f, 0.40f, "lernte ein Lagerfeuer gezielt anzulegen");

        // ── FEUERSTEIN (fire_striker) — Zufallsentdeckung beim Steinhacken
        if (!Has("fire_striker") && Has("fire_making", 0.3f) && Has("stone", 0.2f) && _buildEvents > 20)
            Discover("fire_striker", 0.20f, 0.40f, "schlug Funken aus Feuerstein");

        // ── FEUERBOHRER mit Bogen (fire_drill) — braucht Seil + fire_making
        if (!Has("fire_drill") && Has("fire_making", 0.4f) && Has("rope_making", 0.2f) && _fireExposure > 40)
            Discover("fire_drill", 0.20f, 0.40f, "baute einen Bogenbohrer für schnelleres Feuer");

        // ── SCHARFER STEIN (sharp_stone) ──────────────────────────────
        if (!Has("sharp_stone") && Has("stone") && _forageEvents > 8)
            Discover("sharp_stone", 0.20f, 0.40f, "splitterte einen Stein zu einer Schneide");

        // ── STEINWERKZEUG (tools) ─────────────────────────────────────
        if (!Has("tools") && Has("sharp_stone", 0.15f) && _buildEvents > 5)
            Discover("tools", 0.15f, 0.40f, "kombinierte Stein und Ast zu Werkzeug");

        // ── SEILE (rope_making) ────────────────────────────────────────
        // NPCs kennen Basis-Seile aus Starterwissen, vertiefen durch Nutzung
        if (!Has("rope_making", 0.3f) && Has("rope_making") && _buildEvents > 10)
            Discover("rope_making", 0.10f, 0.30f, "verfeinerte Seiltechnik durch Übung");

        // ── AXTHERSTELLUNG (axe) ──────────────────────────────────────
        if (!Has("axe") && Has("tools", 0.3f) && Has("rope_making", 0.2f) && _buildEvents > 20)
            Discover("axe", 0.15f, 0.35f, "band schweren Stein an Holzgriff");

        // ── SPEER (spear) ─────────────────────────────────────────────
        if (!Has("spear") && Has("tools", 0.2f) && Has("rope_making", 0.15f) && _huntEvents > 10)
            Discover("spear", 0.20f, 0.40f, "band Steinspitze an Holzschaft");

        // ── UNTERKUNFT (shelter) — alle kennen Basis, vertiefen ───────
        if (!Has("shelter", 0.2f) && Has("shelter") && _hungerEvents > 10)
            Discover("shelter", 0.10f, 0.30f, "verbesserte Unterkunft durch Erfahrung");

        // ── VERBESSERTE HÜTTE (shelter_improved) ──────────────────────
        if (!Has("shelter_improved") && Has("shelter", 0.2f) && Has("wood") && _buildEvents > 15)
            Discover("shelter_improved", 0.15f, 0.30f, "baute eine rundere stabilere Hütte");

        // ── LEHMHÜTTE (shelter_mud) ────────────────────────────────────
        if (!Has("shelter_mud") && Has("shelter_improved", 0.2f) && Has("clay") && _buildEvents > 25)
            Discover("shelter_mud", 0.10f, 0.25f, "verputzte Wände mit Lehm für bessere Dämmung");

        // ── FELLBEARBEITUNG (hide_working) — alle haben Basis ─────────
        if (!Has("hide_working", 0.3f) && Has("hide_working") && _huntEvents > 8)
            Discover("hide_working", 0.10f, 0.25f, "verbesserte Fellbearbeitung durch Übung");

        // ── EINFACHE KLEIDUNG (clothing_basic) ───────────────────────
        if (!Has("clothing_basic") && Has("hide_working", 0.2f) && _hungerEvents > 15)
            Discover("clothing_basic", 0.15f, 0.30f, "fertigte groben Fellumhang an");

        // ── KNOCHENNADEL (bone_needle) ────────────────────────────────
        if (!Has("bone_needle") && Has("hide_working", 0.3f) && Has("sharp_stone", 0.2f) && _huntEvents > 20)
            Discover("bone_needle", 0.15f, 0.30f, "schliff Knochen zu einer Nadel");

        // ── SAMMELN (foraging) — alle kennen Basis ────────────────────
        if (!Has("foraging", 0.3f) && Has("foraging") && _forageEvents > 15)
            Discover("foraging", 0.05f, 0.20f, "lernte mehr Pflanzen zu unterscheiden");

        // ── HEILKUNDE (medicine) ──────────────────────────────────────
        if (!Has("medicine") && Has("foraging", 0.3f) && _forageEvents > 30 && curiosity > 0.4f)
        {
            bool nearFood = NearNature(NatureObjectType.BushBerry, NatureObjectType.MushroomEdible);
            if (nearFood) Discover("medicine", 0.15f, 0.30f, "erkannte heilende Pflanzen");
        }

        // ── KOCHEN (cooking) ──────────────────────────────────────────
        if (!Has("cooking") && Has("fire", 0.2f) && Has("campfire") && _fireExposure > 25
            && _owner.Needs.IsHungry)
            Discover("cooking", 0.20f, 0.40f, "garte Nahrung über dem Feuer");

        // ── TÖPFEREI (pottery) ────────────────────────────────────────
        if (!Has("pottery") && Has("fire", 0.3f) && Has("clay") && _buildEvents > 20 && curiosity > 0.4f)
            Discover("pottery", 0.10f, 0.30f, "formte Ton und härtete ihn im Feuer");

        // ── HOLZVERARBEITUNG (lumber) ─────────────────────────────────
        if (!Has("lumber") && Has("axe", 0.25f) && _buildEvents > 30)
            Discover("lumber", 0.15f, 0.35f, "bearbeitete Stämme zu Balken");

        // ── SPRACHE (language) — vertieft sich durch sozialen Kontakt ─
        if (!Has("language", 0.4f) && Has("language") && _socialEvents > 50)
            Discover("language", 0.05f, 0.20f, "verfeinerte Stammessprache durch Gemeinschaft");

        // ── ACKERBAU (agriculture) ────────────────────────────────────
        if (!Has("agriculture") && Has("foraging", 0.3f) && Has("shovel") && _forageEvents > 40)
            Discover("agriculture", 0.10f, 0.25f, "bemerkte dass Samen wachsen");

        // ── Wellbeing-getriebene Entdeckungen ─────────────────────────
        var hint = _owner.Wellbeing?.GetDiscoveryHint();
        if (hint != null && !_owner.Knowledge.Knows(hint))
            Discover(hint, 0.10f, 0.25f, $"Bedürfnis trieb Entdeckung ({hint})");

        // Reset counters (partial — experience accumulates slowly)
        _hungerEvents = Mathf.Max(0, _hungerEvents - 5);
        _fireExposure = Mathf.Max(0, _fireExposure - 10);
        _buildEvents  = Mathf.Max(0, _buildEvents  - 5);
        _socialEvents = Mathf.Max(0, _socialEvents - 20);
        _forageEvents = Mathf.Max(0, _forageEvents - 5);
        _huntEvents   = Mathf.Max(0, _huntEvents   - 5);
    }

    // ── Skill deepening through use ────────────────────────────────────────
    private void DeepenExisting()
    {
        const float GainPerUse = 0.005f;

        if (_fireExposure > 5  && Has("fire"))         _owner.Knowledge.Verify("fire", GainPerUse);
        if (_fireExposure > 5  && Has("fire_making"))  _owner.Knowledge.Verify("fire_making", GainPerUse);
        if (_buildEvents  > 3  && Has("tools"))        _owner.Knowledge.Verify("tools", GainPerUse);
        if (_forageEvents > 5  && Has("foraging"))     _owner.Knowledge.Verify("foraging", GainPerUse);
        if (_huntEvents   > 3  && Has("hunting"))      _owner.Knowledge.Verify("hunting", GainPerUse * 2f);
        if (_buildEvents  > 5  && Has("shelter"))      _owner.Knowledge.Verify("shelter", GainPerUse);
        if (_socialEvents > 10 && Has("language"))     _owner.Knowledge.Verify("language", GainPerUse);
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

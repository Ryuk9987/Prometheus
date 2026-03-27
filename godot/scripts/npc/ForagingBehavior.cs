#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// NPC gathers from NatureObjects.
/// Trees must be FELLED (requires tools knowledge) → yields logs, bark, branches.
/// Bushes/mushrooms are harvested directly.
/// 
/// Poisonous plants: NPC learns through EXPERIENCE.
///   - First encounter: NPC tries it (curiosity-based)
///   - Gets sick: negative health effect, marks as "dangerous"
///   - Teaches others: "don't eat the purple berries"
/// </summary>
public partial class ForagingBehavior : Node
{
    private NpcEntity    _owner;
    private NatureObject _target;
    private double       _checkTimer = 0;
    private const double CheckInterval = 5.0;
    private const float  WorkRange    = 1.8f;
    private const float  MoveSpeed    = 0.9f;

    // Poison experience tracking
    private readonly HashSet<NatureObjectType> _knownPoisonous = new();
    private readonly HashSet<NatureObjectType> _knownEdible    = new();
    private float _sickTimer = 0f;
    public  bool  IsSick    => _sickTimer > 0f;

    public bool IsActive => _target != null;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
    }

    public override void _Process(double delta)
    {
        // Sickness timer
        if (_sickTimer > 0f)
        {
            _sickTimer -= (float)delta;
            _owner.Needs.Hunger = Mathf.Clamp(_owner.Needs.Hunger + 0.01f * (float)delta, 0f, 1f);
        }
    }

    public bool Tick(double delta)
    {
        // Clear invalid target
        if (_target != null && (!IsInstanceValid(_target) ||
            _target.State == NatureObject.GrowthState.Stump ||
            _target.State == NatureObject.GrowthState.Harvested))
            _target = null;

        if (_target == null)
        {
            _checkTimer += delta;
            if (_checkTimer < CheckInterval) return false;
            _checkTimer = 0;
            _target = FindTarget();
            if (_target == null) return false;
        }

        // Move to target
        var dir = _target.GlobalPosition - _owner.GlobalPosition; dir.Y = 0;
        if (dir.Length() > WorkRange)
        {
            _owner.GlobalPosition += dir.Normalized() * MoveSpeed * (float)delta;
            return true;
        }

        // Interact
        Interact();
        _target = null;
        return true;
    }

    private NatureObject FindTarget()
    {
        if (NatureManager.Instance == null) return null;

        // Priority based on needs
        if (_owner.Needs.IsHungry)
        {
            // Look for edible food — avoid known poisonous
            var food = NatureManager.Instance.FindNearest(_owner.GlobalPosition,
                new[] { NatureObjectType.BushBerry, NatureObjectType.MushroomEdible }, 25f);
            // Check if known poisonous
            if (food != null && _knownPoisonous.Contains(food.ObjType))
                food = null; // skip

            if (food != null) return food;

            // If very hungry and curious — might try unknown plant
            if (_owner.Needs.IsStarving && _owner.Personality.Curiosity > 0.5f)
            {
                var unknown = NatureManager.Instance.FindNearest(_owner.GlobalPosition,
                    new[] { NatureObjectType.BushPoison, NatureObjectType.MushroomPoison }, 15f);
                if (unknown != null && !_knownPoisonous.Contains(unknown.ObjType))
                    return unknown; // might try it...
            }
        }

        // Gather wood if not enough
        var inv = _owner.GetNodeOrNull<NpcInventory>("NpcInventory");
        if (inv != null && inv.Get(ResourceType.Branch) < 2f)
        {
            var tree = NatureManager.Instance.FindNearest(_owner.GlobalPosition,
                new[] { NatureObjectType.TreeOak, NatureObjectType.TreePine,
                        NatureObjectType.TreeBirch }, 30f);
            if (tree != null) return tree;
        }

        return null;
    }

    private void Interact()
    {
        var inv = _owner.GetNodeOrNull<NpcInventory>("NpcInventory");
        if (_target == null) return;

        List<(ResourceType, float, string)> yields;

        if (_target.IsTree)
        {
            // Has axe in inventory → best yields, deepens axe knowledge
            bool hasAxe   = inv != null && inv.Has(ResourceType.ToolAxe, 1f);
            // Has sharp stone → can fell but less efficient
            bool hasSharp = inv != null && inv.Has(ResourceType.ToolSharpStone, 1f);
            // Has knowledge of tools (even without physical tool) → can attempt
            bool canFell  = hasAxe || hasSharp
                         || _owner.Knowledge.Knows("tools")
                         || _owner.Knowledge.Knows("axe");

            if (!canFell)
            {
                if (inv != null) inv.Add(ResourceType.Branch, 0.5f);
                GD.Print($"[Forage] {_owner.NpcName} picks up branches (no tools).");
                return;
            }

            yields = _target.Fell();

            if (hasAxe)
            {
                _owner.Knowledge.Verify("axe", 0.05f);
                GD.Print($"[Forage] {_owner.NpcName} fells {_target.ObjType} (Axt)!");
            }
            else
            {
                _owner.Knowledge.Verify("tools", 0.05f);
                GD.Print($"[Forage] {_owner.NpcName} fells {_target.ObjType}!");
            }
        }
        else
        {
            yields = _target.Harvest();
        }

        // Process yields
        foreach (var (res, amt, label) in yields)
        {
            // Poison check
            if (res == ResourceType.BerryPoison || res == ResourceType.MushroomPoison)
            {
                HandlePoisonEncounter(_target.ObjType, res);
                continue;
            }

            // Eat if hungry, else store
            if ((res == ResourceType.BerryEdible || res == ResourceType.MushroomEdible)
                && _owner.Needs.IsHungry)
            {
                _owner.Needs.Eat(amt * 0.3f);
                _knownEdible.Add(_target.ObjType);
                _owner.Knowledge.Learn("food_gathering", amt * 0.05f, 0.4f, "experience");
                GD.Print($"[Forage] {_owner.NpcName} eats {label}.");
            }
            else if (inv != null)
            {
                inv.Add(res, amt);
                GD.Print($"[Forage] {_owner.NpcName} picks up {amt:F1}x {label}.");
            }
        }
    }

    private void HandlePoisonEncounter(NatureObjectType plantType, ResourceType res)
    {
        if (_knownPoisonous.Contains(plantType)) return; // already knows

        // First time — curiosity determines if they try it
        float tryChance = _owner.Personality.Curiosity * 0.4f;
        var rng = new RandomNumberGenerator(); rng.Randomize();

        if (rng.RandfRange(0f, 1f) < tryChance)
        {
            // NPC tries it and gets sick
            _sickTimer = 30f;
            _knownPoisonous.Add(plantType);
            _owner.Needs.Hunger = Mathf.Min(_owner.Needs.Hunger + 0.3f, 1f); // feels worse

            GD.Print($"[Forage] ⚠ {_owner.NpcName} ate poison from {plantType} — got sick! Will remember.");

            // Learn: this plant = dangerous
            _owner.Knowledge.Learn($"poison_{plantType}", 0.8f, 0.95f, "experience_pain");

            // Warn nearby NPCs (social learning)
            WarnNearbyNpcs(plantType);
        }
        else
        {
            GD.Print($"[Forage] {_owner.NpcName} is cautious about {plantType} — avoids it.");
            _knownPoisonous.Add(plantType); // instinct says avoid
        }
    }

    private void WarnNearbyNpcs(NatureObjectType plantType)
    {
        if (GameManager.Instance == null) return;
        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            if (npc == _owner) continue;
            if (npc.GlobalPosition.DistanceTo(_owner.GlobalPosition) > 6f) continue;

            var forage = npc.GetNodeOrNull<ForagingBehavior>("ForagingBehavior");
            if (forage != null)
            {
                forage._knownPoisonous.Add(plantType);
                npc.Knowledge.Learn($"poison_{plantType}", 0.5f, 0.7f, _owner.NpcName);
                GD.Print($"[Forage] {_owner.NpcName} warns {npc.NpcName} about {plantType}.");
            }
        }
    }

    public bool KnowsEdible(NatureObjectType t)   => _knownEdible.Contains(t);
    public bool KnowsPoisonous(NatureObjectType t) => _knownPoisonous.Contains(t);
}

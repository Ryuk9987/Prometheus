#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// CraftingBehavior — NPCs craft tools when they have the knowledge and materials.
///
/// Runs on idle check every 15 seconds.
/// Priority: only crafts if NPC doesn't already own the tool.
///
/// Craft chain (Stone Age order):
///   sharp_stone  → ToolSharpStone  (needs: Stone)
///   rope_making  → ToolRope        (needs: Branch x2)
///   tools        → (nothing extra — uses sharp_stone)
///   spear        → ToolSpear       (needs: ToolSharpStone + Branch x2 + ToolRope)
///   axe          → ToolAxe         (needs: ToolSharpStone + Branch + ToolRope)
///   fire_drill   → ToolFireDrill   (needs: Branch x2 + ToolRope)
///   bone_needle  → ToolBoneNeedle  (needs: Bone + ToolSharpStone)
///   bow          → ToolBow         (needs: Branch x2 + ToolRope)
/// </summary>
public partial class CraftingBehavior : Node
{
    private NpcEntity _owner;
    private double    _checkTimer = 0;
    private const double CheckInterval = 15.0;

    // Crafting in progress
    private string   _craftingId    = null;
    private double   _craftTimer    = 0;
    private double   _craftDuration = 3.0; // seconds to craft

    public bool IsActive => _craftingId != null;

    public override void _Ready() => _owner = GetParent<NpcEntity>();

    public bool Tick(double delta)
    {
        // If currently crafting, count down
        if (_craftingId != null)
        {
            _craftTimer += delta;
            if (_craftTimer >= _craftDuration)
            {
                FinishCraft(_craftingId);
                _craftingId = null;
                _craftTimer = 0;
            }
            return true; // busy crafting
        }

        _checkTimer += delta;
        if (_checkTimer < CheckInterval) return false;
        _checkTimer = 0;

        TryStartCraft();
        return false;
    }

    // ── Decide what to craft ─────────────────────────────────────────────
    private void TryStartCraft()
    {
        var inv = _owner.Inventory;
        var k   = _owner.Knowledge;

        // ── Sharp stone (foundation of everything) ───────────────────────
        if (!inv.Has(ResourceType.ToolSharpStone, 1f)
            && k.Knows("sharp_stone") && inv.Has(ResourceType.Stone, 1f))
        {
            StartCraft("sharp_stone", ResourceType.ToolSharpStone, 1.5f,
                new(){ (ResourceType.Stone, 1f) });
            return;
        }

        // ── Rope ─────────────────────────────────────────────────────────
        if (!inv.Has(ResourceType.ToolRope, 1f)
            && k.Knows("rope_making") && inv.Has(ResourceType.Branch, 2f))
        {
            StartCraft("rope_making", ResourceType.ToolRope, 3.0f,
                new(){ (ResourceType.Branch, 2f) });
            return;
        }

        // ── Fire drill ───────────────────────────────────────────────────
        if (!inv.Has(ResourceType.ToolFireDrill, 1f)
            && k.Knows("fire_drill")
            && inv.Has(ResourceType.Branch, 2f) && inv.Has(ResourceType.ToolRope, 1f))
        {
            StartCraft("fire_drill", ResourceType.ToolFireDrill, 4.0f,
                new(){ (ResourceType.Branch, 2f) }); // rope not consumed, just needed
            return;
        }

        // ── Spear ────────────────────────────────────────────────────────
        if (!inv.Has(ResourceType.ToolSpear, 1f)
            && k.Knows("spear")
            && inv.Has(ResourceType.ToolSharpStone, 1f)
            && inv.Has(ResourceType.Branch, 2f)
            && inv.Has(ResourceType.ToolRope, 1f))
        {
            StartCraft("spear", ResourceType.ToolSpear, 5.0f,
                new(){ (ResourceType.ToolSharpStone, 1f), (ResourceType.Branch, 2f) });
            return;
        }

        // ── Axe ──────────────────────────────────────────────────────────
        if (!inv.Has(ResourceType.ToolAxe, 1f)
            && k.Knows("axe")
            && inv.Has(ResourceType.ToolSharpStone, 1f)
            && inv.Has(ResourceType.Branch, 1f)
            && inv.Has(ResourceType.ToolRope, 1f))
        {
            StartCraft("axe", ResourceType.ToolAxe, 5.0f,
                new(){ (ResourceType.ToolSharpStone, 1f), (ResourceType.Branch, 1f) });
            return;
        }

        // ── Bone needle ──────────────────────────────────────────────────
        if (!inv.Has(ResourceType.ToolBoneNeedle, 1f)
            && k.Knows("bone_needle")
            && inv.Has(ResourceType.Bone, 1f)
            && inv.Has(ResourceType.ToolSharpStone, 1f))
        {
            StartCraft("bone_needle", ResourceType.ToolBoneNeedle, 4.0f,
                new(){ (ResourceType.Bone, 1f) }); // sharp stone not consumed
            return;
        }

        // ── Bow ──────────────────────────────────────────────────────────
        if (!inv.Has(ResourceType.ToolBow, 1f)
            && k.Knows("bow")
            && inv.Has(ResourceType.Branch, 2f)
            && inv.Has(ResourceType.ToolRope, 1f))
        {
            StartCraft("bow", ResourceType.ToolBow, 6.0f,
                new(){ (ResourceType.Branch, 2f) });
            return;
        }
    }

    // ── Craft helpers ────────────────────────────────────────────────────
    private void StartCraft(string knowledgeId, ResourceType result, double duration,
        List<(ResourceType res, float amount)> costs)
    {
        // Consume materials
        foreach (var (res, amt) in costs)
            _owner.Inventory.Remove(res, amt);

        _craftingId    = $"{knowledgeId}:{result}:{result}";
        _craftTimer    = 0;
        _craftDuration = duration;
        _pendingResult = result;
        _pendingKnowledge = knowledgeId;

        GD.Print($"[Craft] {_owner.NpcName} crafting {knowledgeId}...");
    }

    private ResourceType _pendingResult;
    private string       _pendingKnowledge;

    private void FinishCraft(string craftId)
    {
        _owner.Inventory.Add(_pendingResult, 1f);
        _owner.Knowledge.Verify(_pendingKnowledge, 0.05f); // deepen knowledge through use
        GD.Print($"[Craft] {_owner.NpcName} finished: {_pendingKnowledge} → {_pendingResult}");
    }
}

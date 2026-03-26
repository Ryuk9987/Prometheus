#nullable disable
using Godot;

/// <summary>
/// Tracks an NPC's belief in the Oracle (the player).
/// Without sufficient belief, the Oracle's ideas cannot reach this NPC.
/// Belief spreads socially — believers influence nearby NPCs over time.
/// </summary>
public partial class BeliefComponent : Node
{
    [Export] public float Belief       { get; set; } = 0.1f;  // 0=none, 1=devout
    [Export] public float BeliefDecay  { get; set; } = 0.0001f; // very slow decay (0.006/min)

    public bool CanHearOracle => Belief >= 0.3f;

    public void OnWorldTick(double delta)
    {
        // Belief slowly fades without reinforcement
        Belief = Mathf.Clamp(Belief - BeliefDecay * (float)delta, 0f, 1f);
    }

    /// <summary>Strengthen belief — called when Oracle delivers a useful idea.</summary>
    public void Reinforce(float amount = 0.15f)
    {
        Belief = Mathf.Clamp(Belief + amount, 0f, 1f);
        GD.Print($"[Belief] {GetParent().Name} belief reinforced → {Belief:F2}");
    }

    /// <summary>Spread belief socially to a nearby NPC.</summary>
    public void SpreadTo(BeliefComponent other, float empathy)
    {
        if (Belief < 0.3f) return;
        float spread = Belief * empathy * 0.05f;
        other.Belief = Mathf.Clamp(other.Belief + spread, 0f, 1f);
    }
}

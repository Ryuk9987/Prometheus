using Godot;

/// <summary>
/// Tracks an NPC's basic survival needs.
/// Needs drive behavior — hungry NPCs seek food over socialising.
/// </summary>
public partial class NeedsComponent : Node
{
    [Export] public float Hunger          { get; set; } = 0.0f;
    [Export] public float HungerDecayRate { get; set; } = 0.05f; // per world tick

    public bool IsHungry   => Hunger >= 0.7f;
    public bool IsStarving => Hunger >= 0.95f;

    /// <summary>Called once per World Tick (every ~1 second).</summary>
    public void OnWorldTick(double delta)
    {
        Hunger = Mathf.Clamp(Hunger + HungerDecayRate * (float)delta, 0f, 1f);

        if (IsStarving)
            GD.Print($"[NeedsComponent] {GetParent().Name} is STARVING!");
    }

    public void Eat(float amount = 0.5f)
    {
        Hunger = Mathf.Clamp(Hunger - amount, 0f, 1f);
    }
}

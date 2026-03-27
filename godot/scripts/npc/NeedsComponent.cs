#nullable disable
using Godot;

public partial class NeedsComponent : Node
{
    [Export] public float Hunger           { get; set; } = 0.0f;
    [Export] public float Thirst           { get; set; } = 0.0f;
    // Decay per second. Day is now 10 min/hour → 4h real per day.
    // Target: NPC gets hungry ~every 2 real hours (= half a game day)
    // 0.04/s was tuned for 1min/hour — now 10x slower day needs ~10x slower decay
    [Export] public float HungerDecayRate  { get; set; } = 0.004f; // per second
    [Export] public float ThirstDecayRate  { get; set; } = 0.006f; // thirst rises faster

    public bool IsHungry   => Hunger >= 0.75f; // raised threshold — NPCs work longer before panicking
    public bool IsStarving => Hunger >= 0.95f;
    public bool IsThirsty  => Thirst >= 0.75f;

    /// <summary>Most urgent need right now.</summary>
    public ResourceType? UrgentNeed
    {
        get
        {
            if (Thirst >= Hunger && IsThirsty)  return ResourceType.Water;
            if (Hunger >= Thirst && IsHungry)   return ResourceType.Food;
            if (IsThirsty)                       return ResourceType.Water;
            if (IsHungry)                        return ResourceType.Food;
            return null;
        }
    }

    public void OnWorldTick(double delta)
    {
        Hunger = Mathf.Clamp(Hunger + HungerDecayRate * (float)delta, 0f, 1f);
        Thirst = Mathf.Clamp(Thirst + ThirstDecayRate * (float)delta, 0f, 1f);
    }

    public void Eat(float amount = 1f)   => Hunger = Mathf.Clamp(Hunger - amount * 0.4f, 0f, 1f);
    public void Drink(float amount = 1f) => Thirst = Mathf.Clamp(Thirst - amount * 0.5f, 0f, 1f);
}

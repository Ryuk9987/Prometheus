using Godot;

/// <summary>
/// Each NPC has a unique personality that influences how they learn,
/// interact, and respond to the Oracle's messages.
/// </summary>
public partial class PersonalityComponent : Node
{
    [Export] public float Curiosity  { get; set; } = 0.5f;  // Drives learning & exploration
    [Export] public float Courage    { get; set; } = 0.5f;  // Risk-taking, trying new things
    [Export] public float Empathy    { get; set; } = 0.5f;  // Social bonding, teaching others
    [Export] public float Distrust   { get; set; } = 0.3f;  // Resistance to Oracle / strangers

    public void Randomize(RandomNumberGenerator rng)
    {
        Curiosity = rng.RandfRange(0.1f, 1.0f);
        Courage   = rng.RandfRange(0.1f, 1.0f);
        Empathy   = rng.RandfRange(0.1f, 1.0f);
        Distrust  = rng.RandfRange(0.0f, 0.6f);
    }

    public string Summary() =>
        $"Curiosity:{Curiosity:F1} Courage:{Courage:F1} Empathy:{Empathy:F1} Distrust:{Distrust:F1}";
}

#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// The Oracle is the player's only connection to the world.
/// Ideas are whispered to believers — what they do with them is up to them.
/// </summary>
public partial class OracleManager : Node
{
    public static OracleManager Instance { get; private set; }

    // Ideas the player can give — expandable
    public static readonly Dictionary<string, OracleIdea> Ideas = new()
    {
        { "fire",        new OracleIdea("fire",        "🔥 Feuer",       "Die Wärme des Lichts kann gezähmt werden.", 0.0f) },
        { "tools",       new OracleIdea("tools",       "🪨 Werkzeuge",   "Stein auf Stein ergibt eine scharfe Kante.", 0.1f) },
        { "shelter",     new OracleIdea("shelter",     "🏚️ Unterkunft",  "Der Regen kann nicht schaden, wenn man ein Dach hat.", 0.2f) },
        { "hunting",     new OracleIdea("hunting",     "🏹 Jagd",        "Geduld und Schnelligkeit besiegen jedes Tier.", 0.2f) },
        { "agriculture", new OracleIdea("agriculture", "🌾 Ackerbau",    "Samen in die Erde, Geduld, und die Erde gibt zurück.", 0.5f) },
        { "language",    new OracleIdea("language",    "💬 Sprache",     "Klänge können Bedeutung tragen — wenn alle einverstanden sind.", 0.3f) },
        { "writing",     new OracleIdea("writing",     "📜 Schrift",     "Wissen kann außerhalb des Geistes leben.", 0.7f) },
        { "medicine",    new OracleIdea("medicine",    "🌿 Heilkunde",   "Manche Pflanzen lindern den Schmerz.", 0.4f) },
        { "astronomy",   new OracleIdea("astronomy",   "⭐ Astronomie",  "Die Sterne bewegen sich mit Regelmäßigkeit — sie erzählen Geschichten.", 0.6f) },
        { "metalwork",   new OracleIdea("metalwork",   "⚒️ Metallarbeit","Das Feuer kann Stein verwandeln — in etwas Härteres.", 0.8f) },
    };

    [Signal] public delegate void IdeaDeliveredEventHandler(string ideaId, int believersReached);

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// Deliver an idea to all NPCs who believe in the Oracle.
    /// Returns how many NPCs received it.
    /// </summary>
    public int DeliverIdea(string ideaId)
    {
        if (!Ideas.TryGetValue(ideaId, out var idea))
        {
            GD.PrintErr($"[Oracle] Unknown idea: {ideaId}");
            return 0;
        }

        int reached = 0;
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            var belief = npc.GetNodeOrNull<BeliefComponent>("BeliefComponent");
            if (belief == null || !belief.CanHearOracle) continue;

            // Interpretation quality depends on belief strength + curiosity
            float understanding = belief.Belief * npc.Personality.Curiosity;
            float depth = understanding * rng.RandfRange(0.2f, 0.5f);
            float confidence = belief.Belief * 0.6f;

            npc.Knowledge.Learn(ideaId, depth, confidence, "oracle");
            belief.Reinforce(0.1f); // idea reinforces belief

            reached++;
        }

        GD.Print($"[Oracle] Idea '{ideaId}' delivered to {reached} believers.");
        EmitSignal(SignalName.IdeaDelivered, ideaId, reached);
        return reached;
    }
}

public class OracleIdea
{
    public string Id          { get; }
    public string DisplayName { get; }
    public string Flavor      { get; }
    public float  MinBeliefEra { get; } // future: era-gating

    public OracleIdea(string id, string displayName, string flavor, float minBeliefEra)
    {
        Id = id; DisplayName = displayName; Flavor = flavor; MinBeliefEra = minBeliefEra;
    }
}

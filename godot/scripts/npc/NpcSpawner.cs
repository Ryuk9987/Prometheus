#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Spawns NPCs with realistic Stone Age starter knowledge.
///
/// Every NPC gets a shared base of survival knowledge (foraging, language basics,
/// hide working, fire awareness). On top of that, each NPC has one deeper specialty.
///
/// This mirrors a real small tribe: everyone knows the basics,
/// but individuals carry deeper knowledge in specific areas.
/// </summary>
public partial class NpcSpawner : Node3D
{
    [Export] public PackedScene NpcScene    { get; set; }
    [Export] public int         SpawnCount  { get; set; } = 20;
    [Export] public float       SpawnRadius { get; set; } = 15f;

    private static readonly string[] Names =
    {
        "Arak", "Boru", "Cana", "Deth", "Eska",
        "Firo", "Gara", "Hora", "Imas", "Jura",
        "Kael", "Lora", "Manu", "Nira", "Orak",
        "Pira", "Quet", "Roka", "Sura", "Tane"
    };

    // Base knowledge every NPC starts with (id, depth, confidence)
    private static readonly (string id, float depth, float conf)[] BaseKnowledge =
    {
        // Stammessprache — Grundvoraussetzung für Koordination
        ("language",    0.20f, 0.8f),
        // Sammeln — Nahrungsgrundlage
        ("foraging",    0.25f, 0.9f),
        // Wasser finden
        ("water",       0.30f, 0.9f),
        // Stein kennen (Grundmaterial)
        ("stone",       0.20f, 0.8f),
        // Holz kennen (Grundmaterial)
        ("wood",        0.20f, 0.8f),
        // Grundlegende Jagdkenntnisse — alle können Fallen stellen
        ("hunting",     0.15f, 0.6f),
        // Fellbearbeitung — Grundkleidung mit Stein
        ("hide_working",0.15f, 0.6f),
        // Seile aus Pflanzenfasern — alle kennen Grundtechnik
        ("rope_making", 0.10f, 0.5f),
        // Feuer — kennen es, aber noch nicht alle können es machen
        ("fire",        0.10f, 0.5f),
        // Einfache Unterkunft — alle wissen wie man sich schützt
        ("shelter",     0.10f, 0.5f),
    };

    // Specializations — jeder NPC hat ein tieferes Wissen
    private static readonly (string id, float depth, float conf)[] Specializations =
    {
        ("fire_making",  0.80f, 0.90f),  // 0: Arak — Feuerhüter
        ("stone",        0.50f, 0.70f),  // 1: Boru — Steinbearbeiter
        ("hunting",      0.50f, 0.65f),  // 2: Cana — Jägerin
        ("water",        0.50f, 0.70f),  // 3: Deth — Wasserkundiger
        ("wood",         0.45f, 0.65f),  // 4: Eska — Holzkundige
        ("foraging",     0.50f, 0.75f),  // 5: Firo — Sammler
        ("hide_working", 0.45f, 0.65f),  // 6: Gara — Fellbearbeiter
        ("rope_making",  0.45f, 0.65f),  // 7: Hora — Seiler
        ("medicine",     0.40f, 0.60f),  // 8: Imas — Heilerin
        ("language",     0.50f, 0.80f),  // 9: Jura — Geschichtenerzähler
        ("hunting",      0.40f, 0.60f),  // 10: Kael
        ("foraging",     0.40f, 0.65f),  // 11: Lora
        ("fire_making",  0.35f, 0.55f),  // 12: Manu
        ("stone",        0.40f, 0.60f),  // 13: Nira
        ("wood",         0.40f, 0.60f),  // 14: Orak
        ("hide_working", 0.35f, 0.55f),  // 15: Pira
        ("rope_making",  0.35f, 0.55f),  // 16: Quet
        ("hunting",      0.35f, 0.55f),  // 17: Roka
        ("foraging",     0.35f, 0.55f),  // 18: Sura
        ("medicine",     0.30f, 0.50f),  // 19: Tane
    };

    public override void _Ready()
    {
        if (NpcScene == null)
        {
            GD.PrintErr("[NpcSpawner] NpcScene is not assigned!");
            return;
        }

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < SpawnCount; i++)
        {
            var npc = NpcScene.Instantiate<NpcEntity>();
            npc.NpcName     = Names[i % Names.Length];
            npc.Age         = rng.RandiRange(15, 45);
            npc.TribeId     = "tribe_alpha";
            npc.BeliefScore = i < 6
                ? rng.RandfRange(0.35f, 0.75f)
                : rng.RandfRange(0.0f, 0.2f);

            float x = rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = rng.RandfRange(-SpawnRadius, SpawnRadius);
            npc.Position = new Vector3(x, 0f, z);

            // Store base + specialization for _Ready() in NpcEntity
            var knowledgeList = new List<(string, float, float)>();
            foreach (var k in BaseKnowledge)
                knowledgeList.Add(k);
            if (i < Specializations.Length)
                knowledgeList.Add(Specializations[i]);

            npc.StarterKnowledge = knowledgeList;

            if (i == 0)
                GD.Print($"[NpcSpawner] {npc.NpcName} ist der Feuerhüter des Stammes.");

            GetParent().CallDeferred(Node.MethodName.AddChild, npc);
        }

        GD.Print($"[NpcSpawner] {SpawnCount} NPCs spawned into tribe_alpha.");
    }
}

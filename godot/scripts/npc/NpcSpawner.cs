#nullable disable
using Godot;

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
            npc.BeliefScore = rng.RandfRange(0.0f, 0.3f);

            float x = rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = rng.RandfRange(-SpawnRadius, SpawnRadius);
            npc.Position = new Vector3(x, 0.5f, z);

            // First NPC is the "fire keeper" — knows fire at high depth
            if (i == 0)
            {
                npc.Knowledge.Learn("fire", depth: 0.8f, confidence: 0.9f, sourceId: "oracle");
                npc.NpcName = "Arak (Fire Keeper)";
                GD.Print("[NpcSpawner] Arak knows fire — knowledge will spread.");
            }

            GetParent().CallDeferred(Node.MethodName.AddChild, npc);
        }

        GD.Print($"[NpcSpawner] Spawning {SpawnCount} NPCs into tribe_alpha.");
    }
}

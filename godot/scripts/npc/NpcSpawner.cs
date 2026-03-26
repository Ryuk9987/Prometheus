using Godot;

/// <summary>
/// Spawns initial NPC population at game start.
/// Place in the World scene; assign NpcScene in the Inspector.
/// </summary>
public partial class NpcSpawner : Node3D
{
    [Export] public PackedScene NpcScene   { get; set; }
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
            GD.PrintErr("[NpcSpawner] NpcScene is not assigned! Set it in the Inspector.");
            return;
        }

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < SpawnCount; i++)
        {
            var npc = NpcScene.Instantiate<NpcEntity>();
            npc.NpcName = Names[i % Names.Length];
            npc.Age     = rng.RandiRange(15, 45);
            npc.TribeId = "tribe_alpha";
            npc.BeliefScore = rng.RandfRange(0.0f, 0.3f); // start with low belief

            float x = rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = rng.RandfRange(-SpawnRadius, SpawnRadius);
            npc.Position = new Vector3(x, 0.5f, z);

            GetParent().CallDeferred(Node.MethodName.AddChild, npc);
        }

        GD.Print($"[NpcSpawner] Spawned {SpawnCount} NPCs into tribe_alpha.");
    }
}

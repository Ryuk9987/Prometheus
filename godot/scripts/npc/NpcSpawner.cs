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
			// First 6 NPCs start as believers — enough for a usable knowledge pool
			npc.BeliefScore = i < 6
				? rng.RandfRange(0.35f, 0.75f)
				: rng.RandfRange(0.0f, 0.2f);

			float x = rng.RandfRange(-SpawnRadius, SpawnRadius);
			float z = rng.RandfRange(-SpawnRadius, SpawnRadius);
			npc.Position = new Vector3(x, 0f, z);

			// Starter knowledge per NPC slot
			if (i == 0) {
				npc.NpcName = "Arak";
				npc.StarterKnowledgeId = "fire";
				npc.StarterKnowledgeDepth = 0.8f;
				npc.StarterKnowledgeConfidence = 0.9f;
				GD.Print("[NpcSpawner] Arak will be the Fire Keeper.");
			}
			else if (i == 1) {
				npc.StarterKnowledgeId = "stone";
				npc.StarterKnowledgeDepth = 0.5f;
				npc.StarterKnowledgeConfidence = 0.7f;
			}
			else if (i == 2) {
				npc.StarterKnowledgeId = "hunting";
				npc.StarterKnowledgeDepth = 0.4f;
				npc.StarterKnowledgeConfidence = 0.6f;
			}
			else if (i == 3) {
				npc.StarterKnowledgeId = "water";
				npc.StarterKnowledgeDepth = 0.5f;
				npc.StarterKnowledgeConfidence = 0.7f;
			}
			else if (i == 4) {
				npc.StarterKnowledgeId = "wood";
				npc.StarterKnowledgeDepth = 0.4f;
				npc.StarterKnowledgeConfidence = 0.6f;
			}

			GetParent().CallDeferred(Node.MethodName.AddChild, npc);
		}

		GD.Print($"[NpcSpawner] Spawning {SpawnCount} NPCs into tribe_alpha.");
	}
}

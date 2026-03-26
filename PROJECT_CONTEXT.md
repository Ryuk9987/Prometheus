# PROJECT_CONTEXT.md — PROMETHEUS
> Für Agenten: Lies dieses Dokument vor jeder Arbeit am Projekt.

## Was ist Prometheus?

Ein God-Game / Zivilisations-Simulation in Godot 4 (C#).
Der Spieler ist ein unsichtbarer Gott, der via Orakel Ideen an gläubige NPCs weitergibt.
Die NPCs sind autonome Individuen mit eigenem Wissen, Persönlichkeit und Bedürfnissen.

## Technischer Stack

- **Engine:** Godot 4.x (neueste stabile Version)
- **Sprache:** C# (.NET 8)
- **Plattform:** Windows PC (Entwicklung auf 192.168.0.129)
- **Server:** Ubuntu 192.168.0.234 (Build + CI)
- **Versionskontrolle:** Git / GitHub

## Kern-Architekturen

### NPC-System
```
NpcEntity (Node3D)
├── PersonalityComponent   // Neugier, Mut, Empathie, Misstrauen
├── NeedsComponent         // Hunger, Sicherheit, Schlaf
├── KnowledgeComponent     // Dictionary<string, KnowledgeItem>
├── BeliefComponent        // Glaube an Götter (inkl. Spieler-Orakel)
├── SocialComponent        // Beziehungen zu anderen NPCs
└── BehaviorTree           // Entscheidungslogik
```

### Wissens-System
```csharp
public class KnowledgeItem {
    public string Id;           // "fire", "bow", "agriculture"
    public float Depth;         // 0.0 = gehört, 1.0 = gemeistert
    public float Confidence;    // Wie sicher ist der NPC sich?
    public string SourceNpcId;  // Von wem gelernt?
    public bool IsVerified;     // Durch eigene Erfahrung bestätigt?
}
```

### Orakel-System
```
OracleManager (Singleton/Autoload)
├── IdeaQueue: List<IdeaPayload>
├── BeliefFilter: Nur Gläubige empfangen
└── IdeaInterpreter: Idee → NPC-verständliches Konzept
```

### Terrain
- Godot 4 `MeshInstance3D` mit dynamischer Mesh-Deformation
- Heightmap-basiert, Runtime-editable
- Shader: PBR-Terrain mit Biom-Blending

## Wichtige Regeln

1. **NPC-Logik läuft in separaten Threads** (WorkerThreadPool) — keine UI-Calls aus Threads!
2. **Godot-Thread-Safety:** Node-Zugriff nur über CallDeferred() aus Threads
3. **Performance:** Max 500–1000 aktive NPCs; entfernte NPCs = vereinfachte Simulation
4. **Kein Hard-coded Technologiebaum** — alles emergent durch NPC-Experimente
5. **Jede Änderung:** README.md, PROJECT_CONTEXT.md und ROADMAP.md aktualisieren
6. **Commits:** Immer PR erstellen, kein direkter Push auf main

## Projektstruktur (Godot)

```
godot/
├── project.godot
├── addons/
├── assets/
│   ├── models/
│   ├── textures/
│   └── sounds/
├── scenes/
│   ├── world/
│   ├── npc/
│   └── ui/
└── scripts/
    ├── core/
    │   ├── GameManager.cs
    │   ├── OracleManager.cs
    │   └── WorldManager.cs
    ├── npc/
    │   ├── NpcEntity.cs
    │   ├── KnowledgeComponent.cs
    │   ├── BeliefComponent.cs
    │   ├── PersonalityComponent.cs
    │   ├── NeedsComponent.cs
    │   └── BehaviorTree/
    ├── terrain/
    │   └── TerrainManager.cs
    └── ui/
        └── OracleUI.cs
```

## Agenten-Zuständigkeiten

| Agent | Aufgabe |
|-------|---------|
| `csharp-godot` | Hauptentwicklung: NPC-System, Orakel, Terrain, Simulation |
| `nano-banana-artist` | 3D Assets: NPC-Modelle, Terrain-Texturen, UI-Icons |
| Apollo | GDD, Projektkoordination, PR-Review |

## Skills im Projekt

| Skill | Pfad | Zweck |
|-------|------|-------|
| `game-ai` | `skills/game-ai/` | NPC Behavior Trees, Utility AI |
| `godot-bridge` | `skills/godot-bridge/` | Godot CLI Generator |

## GitHub Repo

- Noch nicht erstellt → nächster Schritt: `gh repo create Prometheus`

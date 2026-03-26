# PROMETHEUS — Roadmap

## Phase 1: Prototyp (Kern-Systeme) 🚧

### Meilenstein 1.1 — Welt-Grundgerüst ✅ (2026-03-26)
- [x] Godot 4 Projekt (3D, C#) anlegen
- [x] Terrain: StaticBody3D + PlaneMesh (100x100), vorbereitet für Deformation
- [x] Kamera: Strategische Vogelperspektive (Zoom, Orbit, WASD Pan)
- [x] Beleuchtung: DirectionalLight3D (Sonne) + ProceduralSky

### Meilenstein 1.2 — NPC Basis ✅ (2026-03-26)
- [x] NpcEntity.cs: Position, Alter, Stamm-ID, BeliefScore
- [x] PersonalityComponent.cs: Neugier, Mut, Empathie, Misstrauen
- [x] NeedsComponent.cs: Hunger (sinkt per WorldTick)
- [x] KnowledgeComponent.cs: Dictionary<string, KnowledgeItem>, TeachTo()
- [x] RandomWalk + NavigationAgent3D Bewegung
- [x] CapsuleMesh + Label3D (Name über Kopf)
- [x] NpcSpawner.cs: 20 NPCs, zufällig im Stammeslager

### Meilenstein 1.3 — Wissens-System
- [ ] Wissen stirbt mit NPC (ohne Weitergabe) — Basis ✅ in KnowledgeComponent
- [ ] Mündliche Übertragung aktiv triggern (NPCs in Nähe = Gespräch möglich)
- [ ] Proximity-System: NPCs in Radius interagieren automatisch
- [ ] Wissens-Verfall bei langer Nicht-Nutzung

### Meilenstein 1.4 — Glaube + Orakel
- [ ] Glaubens-Score pro NPC (0.0–1.0)
- [ ] Orakel-Interface: Einfaches UI
- [ ] Idee einspeisen → NPC empfängt + interpretiert
- [ ] Glaube nötig für Orakel-Zugang

### Meilenstein 1.5 — Demo v0.1
- [ ] 20 NPCs, 1 Stamm, Steinzeit
- [ ] Feuer als erstes Wissen (vom Spieler über Orakel)
- [ ] NPCs entwickeln Feuer-Nutzung emergent
- [ ] Gameplay-Schleife spielbar

---

## Phase 2: Vertiefung

- [ ] Mehrere Stämme + Interaktionen
- [ ] Handel zwischen Gruppen
- [ ] Rivalen-Götter (andere Glaubensrichtungen)
- [ ] Technologiebaum emergent (nicht fest vorgegeben)
- [ ] Schrift als Gamechanger
- [ ] Terrain-Deformation durch NPCs (Hütten bauen, roden)

---

## Phase 3: Alpha

- [ ] Vollständige Eras (Urzeit → Antike)
- [ ] Ökosystem (Jagd, Ressourcenerschöpfung)
- [ ] Performance-Optimierung (LOD für NPCs)
- [ ] Sound + Musik
- [ ] Orakel-UI verfeinert

---

## Phase 4: Beta / Release

- [ ] Polish + Balancing
- [ ] Savegame-System
- [ ] Tutorial / Onboarding
- [ ] Windows Build
- [ ] Steam-Seite (optional)

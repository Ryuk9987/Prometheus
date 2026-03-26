# PROMETHEUS — Game Design Document (GDD)
**Version:** 0.1 (Konzeptphase)
**Stand:** 2026-03-26
**Genre:** God Game / Zivilisations-Simulation
**Engine:** Godot 4 (C#)

---

## 🌟 Vision

> *Du bist ein Gott — aber kein allmächtiger. Du kannst nur flüstern. Was die Menschen daraus machen, liegt bei ihnen.*

PROMETHEUS ist ein Gott-Spiel, in dem der Spieler eine frühe Menschheit aus der Steinzeit heraus begleitet. Statt direkter Kontrolle kommuniziert der Spieler über ein **Orakel** — ein mystisches Interface, durch das Ideen, Konzepte und Wissen in die Welt einfließen.

Die Welt lebt. NPCs denken, lernen, vergessen, sterben. Kulturen entstehen und vergehen. Wissen breitet sich aus — oder geht verloren.

---

## 🎮 Kernkonzepte

### 1. Das Orakel
- Einzige Kommunikationsform zwischen Spieler und Welt
- Nur Gläubige können das Orakel wahrnehmen
- Spieler kann **Ideen einspeisen** (z.B. "Bogen", "Ackerbau", "Mathematik")
- NPCs interpretieren Ideen nach ihrem eigenen Verständnis und Kontext
- Keine direkte Kontrolle — nur Impuls

### 2. Glaube als Ressource
- Jede Glaubensgemeinschaft hat einen eigenen **Glaubens-Score** gegenüber dem Spieler
- Glaube wächst durch: Wunder, bestätigte Ideen, Propheten
- Glaube sinkt durch: Fehler, Rivalen-Götter, rationalistisches Denken (später)
- Ohne Glaube kein Orakel-Zugang

### 3. Individuelles Wissen
- Jeder NPC ist ein **Individuum** mit eigenem Wissens-Inventar
- Wissen hat Stufen: Gehört → Verstanden → Gemeistert
- Wissen wird **mündlich** weitergegeben (mit Verlust/Verfremdung)
- Schrift (wenn entwickelt) macht Wissen persistent + skalierbar
- NPCs sterben → ihr ungesichertes Wissen stirbt mit

### 4. Emergente Technologieentwicklung
```
Spieler gibt Impuls: "Bogen"
→ NPC versteht Konzept "Ast + Sehne"
→ NPC experimentiert (Zeit + Ressourcen)
  → Variante A funktioniert → wird behalten, weitergegeben
  → Variante B versagt → wird verworfen
→ Erfolgreiche Technik verbreitet sich durch Gruppe
→ Variationen entstehen organisch (Langbogen, Kurzbogen...)
```

### 5. Ökosoziales System
- Ressourcen, Klima, Fauna → beeinflussen Gesellschaft
- Gesellschaft → beeinflusst Ökosystem (Jagd, Rodung, Ackerbau)
- Positives Feedback und Kollaps möglich
- Verformbares Gelände: NPCs können Welt physisch verändern (Hütten, Felder, Minen)

---

## ⏳ Historische Eras

| Era | Trigger | Neue Mechaniken |
|-----|---------|-----------------|
| **Urzeit** | Start | Feuer, Stein, Sprache, Klan |
| **Frühzeit** | Ackerbau entdeckt | Sesshafte Gruppen, Vorräte, Handel |
| **Altertum** | Schrift entwickelt | Wissen persistiert, Institutionen |
| **Antike** | Stadtstaaten | Politik, Kriege, Religion institutionalisiert |
| **Mittelalter** | (je nach Entwicklung) | Feudalsystem oder andere Strukturen |
| **Neuzeit+** | Wissenschaft | Spieler-Einfluss schwindet (NPCs denken kritisch) |

---

## 🧠 NPC-Simulation

### Individuelle Eigenschaften
```
- Alter, Geschlecht, Stamm
- Persönlichkeit: Neugier, Mut, Empathie, Misstrauen (0.0–1.0)
- Wissen: Dictionary<string, KnowledgeItem> (mit Tiefe + Sicherheit)
- Glaube: Welche Götter/Kräfte sie anerkennen
- Beziehungen: Vertrauen zu anderen NPCs (soziales Netzwerk)
- Motivation: Hunger, Sicherheit, Neugier, Status, Spiritualität
```

### Wissensübertragung
```
NPC A (Meister: 0.9) spricht mit NPC B (Unbekannt: 0.0)
→ Übertragungsformel:
  Gewonnenes_Wissen = Basis_Transfer * A.Lehrfähigkeit * B.Neugier * Vertrauensfaktor
  Verfremdung = Random(-0.1, 0.1) * (1 - A.Kommunikation)
→ NPC B: Gehört: 0.3–0.6 (je nach Formel)
```

### Verhaltensebenen
1. **Survival** (Hunger, Kälte, Gefahr) — höchste Priorität
2. **Social** (Kommunizieren, Handeln, Lieben)
3. **Spiritual** (Beten, Rituale, Prophezeiungen empfangen)
4. **Intellectual** (Experimentieren, Lehren, Entdecken)

---

## 🗺️ Welt

### Terrain
- 3D deformierbar (Marching Cubes oder Heightmap + Runtime Mesh Deformation)
- Biome: Steppe, Wald, Küste, Bergland
- Ressourcen: Stein, Holz, Nahrung, Wasser, Erz (später)
- NPCs hinterlassen physische Spuren (Pfade, Hütten, Felder)

### Kamera
- Strategische Vogelperspektive (Zoom in/out)
- Smooth orbit camera
- Optional: "Propheten-Blick" (Kamera folgt einzelnem NPC)

---

## 🎯 Spielziel

**Kein klassisches Win/Lose.**

Der Spieler beobachtet und formt — was entsteht, ist die Geschichte.

Mögliche Enden:
- Zivilisation erreicht Raumfahrt (Spieler-Einfluss endet)
- Zivilisation kollabiert (Spieler versagt als Gott)
- Equilibrium (stabile Welt ohne weiteres Wachstum)
- Religionskrieg (Spieler verliert Kontrolle an Rivalen-Götter)

---

## 🔧 Engine-Entscheidung: Godot 4 (C#)

### Warum Godot 4?
- ✅ Bereits im Einsatz (ClickAdventure)
- ✅ Kostenlos, keine Runtime-Fees
- ✅ C# für komplexe Simulationslogik
- ✅ Gute 3D-Unterstützung (Vulkan-Renderer)
- ✅ Deformierbares Terrain via CustomMesh + ShaderMaterial möglich
- ✅ Große Community, aktive Entwicklung

### Warum nicht Unity?
- ❌ Licensing-Kontroverse (Runtime Fee)
- ❌ Kein neues Tool lernen nötig

### Warum nicht Unreal?
- ❌ C++ Komplexität
- ❌ Overkill für Indie-Simulation
- ❌ Hohe Hardware-Anforderungen

### Simulation-Performance-Strategie
- NPC-Logik in separatem **Thread-Pool** (Godot WorkerThreadPool)
- LOD-System für NPCs: Weit entfernte NPCs = vereinfachte Simulation
- Max aktive NPCs: 500–1000 (je nach Zoom/Sichtbereich)
- Welt-State wird serialisiert → Schlafende NPCs bleiben erhalten

---

## 💻 Software-Anforderungen

### Server (192.168.0.234 — Ubuntu)
| Software | Zweck | Installation |
|----------|-------|-------------|
| Godot 4.x (headless) | CI-Builds, Exports | `apt` oder Binary |
| .NET SDK 8 | C# Compilation | `apt` |
| Git | Source Control | ✅ vorhanden |
| Python 3 | Build-Scripts | ✅ vorhanden |
| Node.js | clawbridge CLI | ✅ vorhanden |

### PC Erickos (192.168.0.129 — Windows)
| Software | Zweck |
|----------|-------|
| **Godot 4 Editor** | Hauptentwicklung, Scene-Design |
| **.NET SDK 8** | C# IntelliSense + Build |
| **Blender** | 3D Assets (Terrain, Gebäude, NPCs) |
| **Git + GitHub Desktop** | Version Control |
| **VS Code** (optional) | Scripting |

---

## 🛠️ Skills & Agenten

### Installierte Skills
| Skill | Zweck |
|-------|-------|
| `game-ai` | NPC Behavior Trees, State Machines, Utility AI |
| `godot-bridge` | Godot 4 CLI — Scenes, Scripts, Komponenten generieren |

### Agenten-Zuständigkeiten
| Agent | Rolle |
|-------|-------|
| **csharp-godot** | Hauptentwickler — Simulation, NPC-System, Terrain |
| **nano-banana-artist** | 3D Assets — NPCs, Terrain-Texturen, UI |
| **Apollo (ich)** | Projektleitung, GDD, Koordination |

---

## 📁 Projektstruktur
```
Prometheus/
├── GDD.md                    # Dieses Dokument
├── ROADMAP.md                # Meilensteine + Tasks
├── README.md                 # Projektübersicht
├── PROJECT_CONTEXT.md        # Technischer Kontext für Agenten
└── godot/                    # (wird von csharp-godot erstellt)
    ├── project.godot
    ├── scenes/
    ├── scripts/
    ├── assets/
    └── ...
```

---

## 🗓️ Nächste Schritte (Phase 1 — Prototyp)

1. [ ] Godot 4 Projekt anlegen (3D, C#)
2. [ ] Terrain-System: Heightmap + einfache Deformation
3. [ ] NPC-Basisklasse: Position, Hunger, Bewegung
4. [ ] Wissens-System: Dictionary-basiert, mündliche Übertragung
5. [ ] Orakel-Interface: Einfaches UI zum Ideen einspeisen
6. [ ] Glaubens-System: Score pro NPC-Gruppe
7. [ ] Demo: 20 NPCs, 1 Stammesgruppe, Feuer als erstes Wissen

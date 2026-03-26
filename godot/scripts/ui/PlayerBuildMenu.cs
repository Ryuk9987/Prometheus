#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Player Build Menu — press B to open.
/// Shows knowledge unlocked by NPCs, grouped by category.
/// Player selects a building → clicks on terrain to place build order.
/// NPCs then come and build it automatically.
/// </summary>
public partial class PlayerBuildMenu : CanvasLayer
{
    private Panel          _panel;
    private VBoxContainer  _content;
    private Label          _statusLabel;
    private bool           _open         = false;
    private string         _selectedId   = null;
    private bool           _placingMode  = false;

    public override void _Ready()
    {
        BuildUI();
        _panel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.B)          { Toggle(); return; }
            if (k.Keycode == Key.Escape && (_placingMode || _open)) { Cancel(); return; }
        }

        // Terrain click to place build order
        if (_placingMode && @event is InputEventMouseButton mb
            && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            PlaceBuildOrder(mb);
        }
    }

    private void Toggle() { if (_open) Cancel(); else Open(); }

    private void Open()
    {
        _open = true; _panel.Visible = true;
        RefreshContent();
    }

    private void Cancel()
    {
        _open = false; _panel.Visible = false;
        _placingMode = false; _selectedId = null;
    }

    private void RefreshContent()
    {
        foreach (Node c in _content.GetChildren()) c.QueueFree();

        // Only knowledge known by FOLLOWERS (believers) — losing them costs you options
        var knownIds = new HashSet<string>();
        if (GameManager.Instance != null)
            foreach (var npc in GameManager.Instance.AllNpcs)
            {
                if (!npc.Belief.CanHearOracle) continue; // only followers
                foreach (var k in npc.Knowledge.Knowledge)
                {
                    var def = KnowledgeCatalog.Get(k.Key);
                    if (def == null) continue;
                    if (k.Value.Depth >= def.MinDepth)
                        knownIds.Add(k.Key);
                }
            }

        // Group by category
        foreach (KnowledgeCategory cat in System.Enum.GetValues(typeof(KnowledgeCategory)))
        {
            var defs = KnowledgeCatalog.GetByCategory(cat)
                .Where(d => knownIds.Contains(d.Id))
                .ToList();
            if (defs.Count == 0) continue;

            // Category header
            var header = new Label();
            header.Text = CategoryLabel(cat);
            header.AddThemeFontSizeOverride("font_size", 14);
            header.AddThemeColorOverride("font_color", CategoryColor(cat));
            _content.AddChild(header);

            foreach (var def in defs)
            {
                var row = MakeEntryRow(def, knownIds);
                _content.AddChild(row);
            }
            _content.AddChild(new HSeparator());
        }

        if (_content.GetChildCount() == 0)
        {
            var lbl = new Label();
            lbl.Text = "Noch kein Wissen freigeschaltet.\nGib NPCs Ideen über das Orakel-Tablet.";
            lbl.AutowrapMode = TextServer.AutowrapMode.Word;
            lbl.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _content.AddChild(lbl);
        }
    }

    private Control MakeEntryRow(KnowledgeDefinition def, HashSet<string> known)
    {
        var hbox = new HBoxContainer();

        // Check prerequisites
        bool prereqsMet = def.Requires.All(r => known.Contains(r));

        // Icon + Name button
        var btn = new Button();
        btn.Text    = $"{def.Icon} {def.DisplayName}";
        btn.Flat    = false;
        btn.Disabled = !prereqsMet;
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.TooltipText = BuildTooltip(def, known);

        if (def.Category == KnowledgeCategory.Building ||
            def.Category == KnowledgeCategory.Tool)
        {
            string capturedId = def.Id;
            string capturedCat = def.Category.ToString();
            btn.Pressed += () => SelectEntry(capturedId, def.Category);
        }

        hbox.AddChild(btn);

        // Materials label
        if (def.Materials.Count > 0)
        {
            string mats = string.Join(" ", def.Materials.Select(m => $"{m.Amount:F0}x{ResourceIcon(m.Resource)}"));
            var matLbl = new Label();
            matLbl.Text = mats;
            matLbl.AddThemeFontSizeOverride("font_size", 11);
            matLbl.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.5f));
            hbox.AddChild(matLbl);
        }

        return hbox;
    }

    private void SelectEntry(string id, KnowledgeCategory cat)
    {
        _selectedId = id;
        var def = KnowledgeCatalog.Get(id);

        if (cat == KnowledgeCategory.Building)
        {
            _placingMode = true;
            _panel.Visible = false;
            _statusLabel.Text = $"📍 Klicke auf Terrain um {def.Icon} {def.DisplayName} zu platzieren  |  ESC = abbrechen";
            _statusLabel.Visible = true;
        }
        else if (cat == KnowledgeCategory.Tool)
        {
            // Craft order — assign to nearest available NPC with this knowledge
            IssueCraftOrder(id, def);
        }
    }

    private void IssueCraftOrder(string id, KnowledgeDefinition def)
    {
        // Find capable NPC
        NpcEntity best = null; float bestDepth = 0;
        if (GameManager.Instance != null)
            foreach (var npc in GameManager.Instance.AllNpcs)
                if (npc.Knowledge.Knows(id))
                {
                    float d = npc.Knowledge.Knowledge[id].Depth;
                    if (d > bestDepth) { bestDepth = d; best = npc; }
                }

        if (best == null)
        {
            _statusLabel.Text = $"Kein NPC kennt {def.DisplayName} gut genug.";
            _statusLabel.Visible = true;
            return;
        }

        // Issue craft task (simplified: NPC gets knowledge reinforced)
        best.Knowledge.Verify(id, 0.1f);
        _statusLabel.Text = $"✓ {best.NpcName} wurde beauftragt: {def.Icon} {def.DisplayName} herstellen.";
        _statusLabel.Visible = true;
        GD.Print($"[BuildMenu] Craft order: {best.NpcName} → {id}");
        Cancel();
    }

    private void PlaceBuildOrder(InputEventMouseButton mb)
    {
        if (_selectedId == null) return;

        // Raycast to find terrain position
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        var from = camera.ProjectRayOrigin(mb.Position);
        var dir  = camera.ProjectRayNormal(mb.Position);

        // Simple plane intersection at Y=0
        if (Mathf.Abs(dir.Y) < 0.001f) return;
        float t = -from.Y / dir.Y;
        if (t < 0) return;
        var worldPos = from + dir * t;

        // Create build order
        var order = new BuildOrder();
        order.KnowledgeId = _selectedId;
        order.Required    = 5f + KnowledgeCatalog.Get(_selectedId)?.Materials.Sum(m => m.Amount) ?? 5f;
        order.Position    = new Vector3(worldPos.X, 0, worldPos.Z);

        GetTree().Root.FindChild("World", true, false)?.AddChild(order);

        var def = KnowledgeCatalog.Get(_selectedId);
        GD.Print($"[BuildMenu] Build order placed: {_selectedId} at {worldPos}");
        _statusLabel.Text = $"✓ Bauauftrag für {def?.Icon} {def?.DisplayName} erteilt! NPCs kommen.";

        _placingMode = false;
        _selectedId  = null;
    }

    private void BuildUI()
    {
        // Status bar (bottom center, always visible when active)
        _statusLabel = new Label();
        _statusLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _statusLabel.Position = new Vector2(0, -40);
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _statusLabel.AddThemeFontSizeOverride("font_size", 16);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.6f));
        _statusLabel.Visible = false;
        AddChild(_statusLabel);

        // Main panel (left side)
        _panel = new Panel();
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _panel.Position = new Vector2(8, -350);
        _panel.Size     = new Vector2(300, 700);

        var style = new StyleBoxFlat();
        style.BgColor     = new Color(0.06f, 0.08f, 0.14f, 0.95f);
        style.BorderColor  = new Color(0.3f, 0.6f, 0.4f);
        style.BorderWidthBottom = style.BorderWidthTop =
        style.BorderWidthLeft   = style.BorderWidthRight = 2;
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   10);
        margin.AddThemeConstantOverride("margin_right",  10);
        margin.AddThemeConstantOverride("margin_top",    10);
        margin.AddThemeConstantOverride("margin_bottom", 10);

        var vbox = new VBoxContainer();

        var title = new Label();
        title.Text = "🏗  Baumenü  [B]";
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.6f));
        vbox.AddChild(title);

        var hint = new Label();
        hint.Text = "Freigeschaltetes Wissen deiner NPCs.\nGebäude: auf Terrain platzieren.\nWerkzeug: NPC bekommt Auftrag.";
        hint.AutowrapMode = TextServer.AutowrapMode.Word;
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        vbox.AddChild(hint);
        vbox.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_content);
        vbox.AddChild(scroll);

        vbox.AddChild(MakeButton("✕ Schließen", Cancel));

        margin.AddChild(vbox);
        _panel.AddChild(margin);
        AddChild(_panel);
    }

    private static Button MakeButton(string text, System.Action onPress)
    {
        var b = new Button(); b.Text = text;
        if (onPress != null) b.Pressed += onPress;
        return b;
    }

    private static string BuildTooltip(KnowledgeDefinition def, HashSet<string> known)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(def.Description);
        if (def.Materials.Count > 0)
            sb.AppendLine("Materialien: " + string.Join(", ", def.Materials.Select(m => $"{m.Amount:F0}x {m.Resource}")));
        if (def.Requires.Count > 0)
            sb.AppendLine("Benötigt: " + string.Join(", ", def.Requires));
        if (def.Unlocks.Count > 0)
            sb.AppendLine("Schaltet frei: " + string.Join(", ", def.Unlocks));
        return sb.ToString().Trim();
    }

    private static string CategoryLabel(KnowledgeCategory cat) => cat switch {
        KnowledgeCategory.Building => "🏗  Gebäude",
        KnowledgeCategory.Tool     => "🔧  Werkzeuge",
        KnowledgeCategory.Skill    => "📚  Fähigkeiten",
        KnowledgeCategory.Nature   => "🌿  Natur",
        KnowledgeCategory.Concept  => "💡  Konzepte",
        _                          => cat.ToString()
    };

    private static Color CategoryColor(KnowledgeCategory cat) => cat switch {
        KnowledgeCategory.Building => new Color(0.6f, 0.9f, 0.5f),
        KnowledgeCategory.Tool     => new Color(0.9f, 0.7f, 0.3f),
        KnowledgeCategory.Skill    => new Color(0.5f, 0.8f, 1f),
        KnowledgeCategory.Nature   => new Color(0.4f, 0.9f, 0.4f),
        KnowledgeCategory.Concept  => new Color(0.8f, 0.5f, 1f),
        _                          => new Color(0.8f, 0.8f, 0.8f)
    };

    private static string ResourceIcon(ResourceType r) => r switch {
        ResourceType.Wood  => "🪵",
        ResourceType.Stone => "⬟",
        ResourceType.Food  => "🍎",
        ResourceType.Water => "💧",
        _                  => r.ToString()
    };
}

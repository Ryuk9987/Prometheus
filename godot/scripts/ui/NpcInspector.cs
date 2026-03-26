#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RimWorld-style NPC inspector panel.
/// Click NPC in world → panel opens on right side.
/// Tabs: Info | Wissen | Gedanken | Auftrag
/// </summary>
public partial class NpcInspector : CanvasLayer
{
    public static NpcInspector Instance { get; private set; }

    private Panel           _panel;
    private Label           _nameLabel;
    private HBoxContainer   _tabBar;
    private ScrollContainer _tabScroll;
    private VBoxContainer   _tabContent;
    private NpcEntity       _npc;
    private int             _activeTab = 0;

    private static readonly string[] TabNames = { "📋 Info", "📚 Wissen", "💭 Gedanken", "⚒ Auftrag" };

    public override void _Ready()
    {
        Instance = this;
        BuildUI();
        _panel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            if (_panel.Visible) { Close(); GetViewport().SetInputAsHandled(); return; }
        }

        // LMB in world → try to select NPC
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            if (_panel.Visible && IsClickOnPanel(mb.Position)) return;
            TrySelectNpc(mb.Position);
        }
    }

    private bool IsClickOnPanel(Vector2 pos)
    {
        var rect = new Rect2(_panel.GlobalPosition, _panel.Size);
        return rect.HasPoint(pos);
    }

    public override void _Process(double delta)
    {
        if (_panel.Visible && _npc != null && IsInstanceValid(_npc))
            RefreshActiveTab();
    }

    // ── Selection ─────────────────────────────────────────────────────────
    private void TrySelectNpc(Vector2 screenPos)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null || GameManager.Instance == null) return;

        NpcEntity closest = null;
        float closestDist = 40f; // pixels

        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            if (!camera.IsPositionInFrustum(npc.GlobalPosition)) continue;
            var screenNpc = camera.UnprojectPosition(npc.GlobalPosition + Vector3.Up * 0.5f);
            float dist = screenPos.DistanceTo(screenNpc);
            if (dist < closestDist) { closestDist = dist; closest = npc; }
        }

        if (closest != null)
        {
            WorldInspector.Instance?.Close(); // close world inspector when NPC selected
            Open(closest);
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(NpcEntity npc)
    {
        _npc = npc;
        _panel.Visible = true;
        _nameLabel.Text = npc.NpcName;
        ShowTab(0);
    }

    public void Close()
    {
        _panel.Visible = false;
        _npc = null;
    }

    // ── Tabs ──────────────────────────────────────────────────────────────
    private void ShowTab(int idx)
    {
        _activeTab = idx;
        // Update tab button styles
        int i = 0;
        foreach (Button btn in _tabBar.GetChildren())
        {
            btn.Flat = (i != idx);
            i++;
        }
        RefreshActiveTab();
    }

    private void RefreshActiveTab()
    {
        foreach (Node c in _tabContent.GetChildren()) c.QueueFree();
        switch (_activeTab)
        {
            case 0: FillInfoTab(_tabContent);      break;
            case 1: FillKnowledgeTab(_tabContent); break;
            case 2: FillThoughtsTab(_tabContent);  break;
            case 3: FillOrderTab(_tabContent);     break;
        }
    }

    // ── Tab: Info ─────────────────────────────────────────────────────────
    private void FillInfoTab(VBoxContainer vbox)
    {
        vbox.AddThemeConstantOverride("separation", 6);

        var tribe = TribeManager.Instance?.GetTribe(_npc);
        bool isLeader = tribe?.Leader == _npc;
        bool isFollower = _npc.Belief.CanHearOracle;

        AddRow(vbox, "Name",         _npc.NpcName);
        AddRow(vbox, "Alter",        $"{_npc.Age} Jahre");
        AddRow(vbox, "Stamm",        tribe != null ? tribe.Name : "Keiner",
            tribe != null ? tribe.Color : new Color(0.5f,0.5f,0.5f));
        AddRow(vbox, "Rolle",        _npc.Cooperation.Role + (isLeader ? " 👑" : ""));
        AddRow(vbox, "Anhänger",     isFollower ? "✓ Ja" : "✗ Nein",
            isFollower ? new Color(0.3f,1f,0.5f) : new Color(0.8f,0.4f,0.4f));

        vbox.AddChild(new HSeparator());
        AddSectionHeader(vbox, "Bedürfnisse");

        AddBar(vbox, "Hunger",  _npc.Needs.Hunger,
            _npc.Needs.IsStarving ? new Color(1f,0.2f,0.2f) :
            _npc.Needs.IsHungry   ? new Color(1f,0.7f,0.2f) : new Color(0.3f,0.8f,0.3f));
        AddBar(vbox, "Durst",   _npc.Needs.Thirst,
            _npc.Needs.Thirst >= 0.95f ? new Color(1f,0.2f,0.2f) :
            _npc.Needs.IsThirsty       ? new Color(1f,0.7f,0.2f) : new Color(0.3f,0.6f,1f));

        vbox.AddChild(new HSeparator());
        AddSectionHeader(vbox, "Persönlichkeit");
        AddBar(vbox, "Neugier",    _npc.Personality.Curiosity,  new Color(0.5f,0.8f,1f));
        AddBar(vbox, "Mut",        _npc.Personality.Courage,    new Color(1f,0.5f,0.3f));
        AddBar(vbox, "Empathie",   _npc.Personality.Empathy,    new Color(0.9f,0.5f,1f));
        AddBar(vbox, "Misstrauen", _npc.Personality.Distrust,   new Color(0.8f,0.3f,0.3f));

        vbox.AddChild(new HSeparator());
        AddSectionHeader(vbox, "Wohlbefinden");
        var wb = _npc.Wellbeing;
        if (wb != null)
        {
            AddBar(vbox, "Wohlbefinden", wb.Wellbeing,
                wb.IsContent ? new Color(0.3f,1f,0.5f) :
                wb.IsSuffering ? new Color(1f,0.3f,0.3f) : new Color(0.8f,0.7f,0.3f));
            AddBar(vbox, "Sicherheit",  wb.Safety,
                wb.FeelsSafe ? new Color(0.4f,0.8f,0.4f) : new Color(1f,0.5f,0.3f));
            AddBar(vbox, "Komfort",     wb.Comfort,
                wb.IsComfortable ? new Color(0.6f,0.8f,1f) : new Color(0.7f,0.5f,0.3f));
            AddBar(vbox, "Temperatur",  wb.Temperature,
                wb.IsCold ? new Color(0.3f,0.5f,1f) : new Color(1f,0.6f,0.2f));

            // Status badges
            var badges = new System.Text.StringBuilder();
            if (wb.IsCold)    badges.Append("🥶 Friert  ");
            if (!wb.FeelsSafe) badges.Append("😨 Angst  ");
            if (!wb.IsComfortable) badges.Append("😣 Unkomfortabel  ");
            if (wb.IsContent)  badges.Append("😊 Zufrieden");
            if (badges.Length > 0)
            {
                var statusLbl = new Label();
                statusLbl.Text = badges.ToString();
                statusLbl.AddThemeFontSizeOverride("font_size", 11);
                statusLbl.AddThemeColorOverride("font_color", new Color(0.8f,0.8f,0.6f));
                vbox.AddChild(statusLbl);
            }
        }

        vbox.AddChild(new HSeparator());
        AddSectionHeader(vbox, "Glaube");
        AddBar(vbox, "Orakel-Glaube", _npc.Belief.Belief,
            _npc.Belief.CanHearOracle ? new Color(0.4f,0.8f,1f) : new Color(0.5f,0.5f,0.5f));

        // Camera follow button
        vbox.AddChild(new HSeparator());
        var btn = MakeBtn("📷 Kamera folgen", () => CameraFollow.Instance?.Follow(_npc));
        vbox.AddChild(btn);
    }

    // ── Tab: Wissen ───────────────────────────────────────────────────────
    private void FillKnowledgeTab(VBoxContainer vbox)
    {
        vbox.AddThemeConstantOverride("separation", 3);

        // Group by category
        foreach (KnowledgeCategory cat in System.Enum.GetValues(typeof(KnowledgeCategory)))
        {
            var items = _npc.Knowledge.Knowledge
                .Where(k => KnowledgeCatalog.Get(k.Key)?.Category == cat)
                .OrderByDescending(k => k.Value.Depth)
                .ToList();
            if (items.Count == 0) continue;

            AddSectionHeader(vbox, CategoryName(cat));
            foreach (var kv in items)
            {
                var def = KnowledgeCatalog.Get(kv.Key);
                string icon = def?.Icon ?? "•";
                string name = def?.DisplayName ?? kv.Key;
                var item = kv.Value;

                var hbox = new HBoxContainer();
                var lbl = new Label();
                lbl.Text = $"{icon} {name}";
                lbl.CustomMinimumSize = new Vector2(140, 0);
                lbl.AddThemeFontSizeOverride("font_size", 12);
                hbox.AddChild(lbl);

                // Depth bar
                int filled = (int)(item.Depth * 10);
                var bar = new Label();
                bar.Text = new string('█', filled) + new string('░', 10 - filled);
                bar.AddThemeFontSizeOverride("font_size", 11);
                bar.AddThemeColorOverride("font_color",
                    item.Depth > 0.6f ? new Color(0.3f,1f,0.4f) :
                    item.Depth > 0.3f ? new Color(1f,0.8f,0.2f) : new Color(0.7f,0.4f,0.4f));
                hbox.AddChild(bar);

                // Depth number
                var num = new Label();
                num.Text = $" {item.Depth:F2}";
                num.AddThemeFontSizeOverride("font_size", 11);
                num.AddThemeColorOverride("font_color", new Color(0.6f,0.6f,0.6f));
                hbox.AddChild(num);

                // Source tag
                if (!string.IsNullOrEmpty(item.SourceNpcId))
                {
                    var src = new Label();
                    src.Text = $" [{item.SourceNpcId}]";
                    src.AddThemeFontSizeOverride("font_size", 10);
                    src.AddThemeColorOverride("font_color", new Color(0.4f,0.4f,0.6f));
                    hbox.AddChild(src);
                }

                vbox.AddChild(hbox);
            }
        }

        if (_npc.Knowledge.Knowledge.Count == 0)
        {
            var empty = new Label();
            empty.Text = "Kein Wissen vorhanden.";
            empty.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
            vbox.AddChild(empty);
        }
    }

    // ── Tab: Gedanken ─────────────────────────────────────────────────────
    private void FillThoughtsTab(VBoxContainer vbox)
    {
        vbox.AddThemeConstantOverride("separation", 4);

        // Generate thoughts based on state
        var thoughts = GenerateThoughts();
        if (thoughts.Count == 0)
        {
            var lbl = new Label(); lbl.Text = "Keine besonderen Gedanken.";
            lbl.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
            vbox.AddChild(lbl);
        }
        else
        {
            foreach (var (thought, mood) in thoughts)
            {
                var lbl = new Label();
                lbl.Text = thought;
                lbl.AutowrapMode = TextServer.AutowrapMode.Word;
                lbl.AddThemeFontSizeOverride("font_size", 12);
                lbl.AddThemeColorOverride("font_color", mood switch {
                    "good"    => new Color(0.4f,1f,0.5f),
                    "bad"     => new Color(1f,0.4f,0.4f),
                    "neutral" => new Color(0.8f,0.8f,0.6f),
                    _         => new Color(0.7f,0.7f,0.7f)
                });
                vbox.AddChild(lbl);
            }
        }
    }

    private List<(string text, string mood)> GenerateThoughts()
    {
        var thoughts = new List<(string,string)>();
        if (_npc.Needs.IsStarving)   thoughts.Add(("⚠ Ich sterbe vor Hunger!", "bad"));
        else if (_npc.Needs.IsHungry) thoughts.Add(("Ich bin hungrig.", "bad"));
        if (_npc.Needs.IsThirsty)    thoughts.Add(("Ich brauche Wasser.", "bad"));
        if (_npc.Belief.Belief > 0.5f)
            thoughts.Add(("Das Orakel hat mir wichtiges Wissen gegeben.", "good"));
        if (_npc.Belief.Belief < 0.1f)
            thoughts.Add(("Das Orakel ist mir fremd.", "neutral"));
        if (_npc.Knowledge.Knows("fire"))
        {
            float d = _npc.Knowledge.Knowledge["fire"].Depth;
            if (d > 0.5f) thoughts.Add(("Feuer hält uns warm und sicher.", "good"));
            else           thoughts.Add(("Feuer ist faszinierend, ich lerne noch.", "neutral"));
        }
        if (_npc.CampfireBuilder.IsActive)
            thoughts.Add(("Ich baue gerade ein Lagerfeuer.", "neutral"));
        if (_npc.BuildWorker.IsActive)
            thoughts.Add(("Ich arbeite an einem Bauprojekt.", "neutral"));
        if (TribeManager.Instance?.GetTribe(_npc) is Tribe t && t.Leader == _npc)
            thoughts.Add(($"Ich bin die Anführerin von {t.Name}.", "good"));
        if (_npc.Personality.Curiosity > 0.7f)
            thoughts.Add(("Es gibt so viel zu entdecken!", "good"));
        if (_npc.Knowledge.Knowledge.Count == 0)
            thoughts.Add(("Die Welt ist ein Rätsel.", "neutral"));
        return thoughts;
    }

    // ── Tab: Auftrag ──────────────────────────────────────────────────────
    private void FillOrderTab(VBoxContainer vbox)
    {
        vbox.AddThemeConstantOverride("separation", 5);

        // Inventory
        AddSectionHeader(vbox, "Inventar");
        var invLabel = new Label();
        invLabel.Text = _npc.Inventory?.Summary() ?? "—";
        invLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        invLabel.AddThemeFontSizeOverride("font_size", 12);
        invLabel.AddThemeColorOverride("font_color", new Color(0.8f,0.8f,0.6f));
        vbox.AddChild(invLabel);
        vbox.AddChild(new HSeparator());

        // Current activity
        AddSectionHeader(vbox, "Aktuelle Tätigkeit");
        string activity =
            _npc.Foraging.IsActive           ? "🌿 Sammelt in der Natur" :
            _npc.Survival.IsSeekingResource  ? "🍗 Sucht Nahrung/Wasser" :
            _npc.CampfireBuilder.IsActive    ? "🔥 Baut Lagerfeuer" :
            _npc.BuildWorker.IsActive        ? "🏗 Baut Gebäude" :
            _npc.Cooperation.HasTask         ? "⚔ Kooperationsaufgabe" :
                                               "🚶 Wandert";
        var actLbl = new Label(); actLbl.Text = activity;
        actLbl.AddThemeColorOverride("font_color", new Color(0.9f,0.9f,0.6f));
        vbox.AddChild(actLbl);

        // Craftable items
        vbox.AddChild(new HSeparator());
        AddSectionHeader(vbox, "Handwerk-Aufträge");

        var craftable = KnowledgeCatalog.GetByCategory(KnowledgeCategory.Tool)
            .Where(d => _npc.Knowledge.Knows(d.Id) &&
                        _npc.Knowledge.Knowledge[d.Id].Depth >= d.MinDepth)
            .ToList();

        if (craftable.Count == 0)
        {
            var lbl = new Label();
            lbl.Text = "Kennt noch keine Handwerk-Techniken.";
            lbl.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
            vbox.AddChild(lbl);
        }
        else
        {
            foreach (var def in craftable)
            {
                var hbox = new HBoxContainer();
                var btn = MakeBtn($"{def.Icon} {def.DisplayName} herstellen", () => {
                    IssueCraftOrder(_npc, def);
                });
                btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                hbox.AddChild(btn);

                string mats = string.Join(" ", def.Materials.Select(m =>
                    $"{m.Amount:F0}x {m.Resource}"));
                var matLbl = new Label();
                matLbl.Text = mats;
                matLbl.AddThemeFontSizeOverride("font_size", 11);
                matLbl.AddThemeColorOverride("font_color", new Color(0.6f,0.6f,0.4f));
                hbox.AddChild(matLbl);
                vbox.AddChild(hbox);
            }
        }

        // Building orders this NPC can work on
        vbox.AddChild(new HSeparator());
        AddSectionHeader(vbox, "Bau-Aufträge");
        var orders = BuildOrderManager.Instance?.Orders
            .Where(o => _npc.Knowledge.Knows(o.KnowledgeId))
            .ToList() ?? new();

        if (orders.Count == 0)
        {
            var lbl = new Label(); lbl.Text = "Keine offenen Bauaufträge.";
            lbl.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
            vbox.AddChild(lbl);
        }
        else
        {
            foreach (var order in orders)
            {
                var def = KnowledgeCatalog.Get(order.KnowledgeId);
                var lbl = new Label();
                lbl.Text = $"{def?.Icon} {def?.DisplayName} — {(int)(order.Progress/order.Required*100)}%";
                lbl.AddThemeColorOverride("font_color", new Color(0.7f,0.9f,0.6f));
                vbox.AddChild(lbl);
            }
        }
    }

    private void IssueCraftOrder(NpcEntity npc, KnowledgeDefinition def)
    {
        npc.Knowledge.Verify(def.Id, 0.1f);
        GD.Print($"[NpcInspector] {npc.NpcName} ordered: {def.DisplayName}");
        ShowTab(3); // Refresh order tab
    }

    // ── UI Build ──────────────────────────────────────────────────────────
    private void BuildUI()
    {
        // Panel: fixed size, anchored top-right corner
        _panel = new Panel();
        _panel.AnchorLeft  = 1f; _panel.AnchorRight  = 1f;
        _panel.AnchorTop   = 0f; _panel.AnchorBottom = 0f;
        _panel.OffsetLeft  = -326; _panel.OffsetTop    = 10;
        _panel.OffsetRight = 0;   _panel.OffsetBottom  = 590;
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.07f, 0.08f, 0.13f, 0.97f);
        style.BorderColor = new Color(0.4f, 0.6f, 0.9f);
        style.BorderWidthBottom = style.BorderWidthTop =
        style.BorderWidthLeft   = style.BorderWidthRight = 2;
        _panel.AddThemeStyleboxOverride("panel", style);

        // Root VBox fills the panel via anchors (set after AddChild)
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(root);
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.OffsetLeft = 8; root.OffsetRight = -8;
        root.OffsetTop  = 8; root.OffsetBottom = -8;

        // Header row
        var header = new HBoxContainer();
        _nameLabel = new Label();
        _nameLabel.AddThemeFontSizeOverride("font_size", 18);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
        _nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(_nameLabel);
        var closeBtn = MakeBtn("✕", Close);
        closeBtn.CustomMinimumSize = new Vector2(30, 0);
        header.AddChild(closeBtn);
        root.AddChild(header);
        root.AddChild(new HSeparator());

        // Tab bar
        _tabBar = new HBoxContainer();
        _tabBar.AddThemeConstantOverride("separation", 2);
        for (int i = 0; i < TabNames.Length; i++)
        {
            int captured = i;
            var btn = new Button();
            btn.Text = TabNames[i];
            btn.Flat = (i != 0);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += () => ShowTab(captured);
            _tabBar.AddChild(btn);
        }
        root.AddChild(_tabBar);
        root.AddChild(new HSeparator());

        // Scroll area for tab content (fills rest of panel)
        _tabScroll = new ScrollContainer();
        _tabScroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        _tabScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _tabScroll.MouseFilter         = Control.MouseFilterEnum.Stop;
        root.AddChild(_tabScroll);

        _tabContent = new VBoxContainer();
        _tabContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _tabContent.AddThemeConstantOverride("separation", 4);
        _tabScroll.AddChild(_tabContent);

        AddChild(_panel);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void AddRow(VBoxContainer vbox, string key, string value, Color? color = null)
    {
        var hbox = new HBoxContainer();
        var k = new Label(); k.Text = key + ":";
        k.CustomMinimumSize = new Vector2(110, 0);
        k.AddThemeFontSizeOverride("font_size", 12);
        k.AddThemeColorOverride("font_color", new Color(0.6f,0.6f,0.6f));
        hbox.AddChild(k);
        var v = new Label(); v.Text = value;
        v.AddThemeFontSizeOverride("font_size", 12);
        v.AddThemeColorOverride("font_color", color ?? new Color(0.9f,0.9f,0.9f));
        hbox.AddChild(v);
        vbox.AddChild(hbox);
    }

    private void AddBar(VBoxContainer vbox, string label, float value, Color barColor)
    {
        var hbox = new HBoxContainer();
        var lbl = new Label(); lbl.Text = label + ":";
        lbl.CustomMinimumSize = new Vector2(100, 0);
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", new Color(0.6f,0.6f,0.6f));
        hbox.AddChild(lbl);

        var bar = new ProgressBar();
        bar.Value   = value * 100;
        bar.CustomMinimumSize = new Vector2(130, 16);
        bar.ShowPercentage = false;
        hbox.AddChild(bar);

        var num = new Label(); num.Text = $" {value:F2}";
        num.AddThemeFontSizeOverride("font_size", 11);
        num.AddThemeColorOverride("font_color", barColor);
        hbox.AddChild(num);
        vbox.AddChild(hbox);
    }

    private void AddSectionHeader(VBoxContainer vbox, string text)
    {
        var lbl = new Label(); lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", new Color(0.5f,0.8f,1f));
        vbox.AddChild(lbl);
    }

    private static Button MakeBtn(string text, System.Action onPress)
    {
        var b = new Button(); b.Text = text;
        b.AddThemeFontSizeOverride("font_size", 12);
        if (onPress != null) b.Pressed += onPress;
        return b;
    }

    private static string CategoryName(KnowledgeCategory cat) => cat switch {
        KnowledgeCategory.Nature  => "🌿 Natur",
        KnowledgeCategory.Tool    => "🔧 Werkzeuge",
        KnowledgeCategory.Building=> "🏗 Gebäude",
        KnowledgeCategory.Skill   => "📚 Fähigkeiten",
        KnowledgeCategory.Concept => "💡 Konzepte",
        _ => cat.ToString()
    };
}

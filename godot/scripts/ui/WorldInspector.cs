#nullable disable
using Godot;
using System.Linq;

/// <summary>
/// Click on any world object → info panel appears.
/// Works alongside NpcInspector: NPC click → NpcInspector, everything else → WorldInspector.
/// </summary>
public partial class WorldInspector : CanvasLayer
{
    public static WorldInspector Instance { get; private set; }

    private Panel         _panel;
    private Label         _iconLabel;
    private Label         _titleLabel;
    private VBoxContainer _content;
    private WorldObjectEntry _current;

    public override void _Ready()
    {
        Instance = this;
        BuildUI();
        _panel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo
            && k.Keycode == Key.Escape && _panel.Visible)
        {
            Close(); GetViewport().SetInputAsHandled(); return;
        }

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            if (_panel.Visible && IsOnPanel(mb.Position)) return;

            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var entry = WorldObjectRegistry.Instance?.FindNearest(mb.Position, camera);
            if (entry != null) Show(entry);
        }
    }

    private bool IsOnPanel(Vector2 pos)
        => new Rect2(_panel.GlobalPosition, _panel.Size).HasPoint(pos);

    public void Show(WorldObjectEntry entry)
    {
        _current = entry;
        _panel.Visible = true;
        _iconLabel.Text  = entry.Icon;
        _titleLabel.Text = entry.Label;
        RefreshContent();
    }

    public override void _Process(double delta)
    {
        if (!_panel.Visible || _current == null) return;
        if (!IsInstanceValid(_current.Node)) { Close(); return; }
        RefreshContent();
    }

    private void RefreshContent()
    {
        foreach (Node c in _content.GetChildren()) c.QueueFree();

        switch (_current.Kind)
        {
            case WorldObjectKind.Resource:
                if (_current.Node is NatureObject nat) BuildNatureContent(nat);
                else BuildResourceContent(_current.Node as ResourceNode);
                break;
            case WorldObjectKind.Animal:
                BuildAnimalContent(_current.Node as Animal);
                break;
            case WorldObjectKind.Campfire:
                BuildCampfireContent(_current.Node as Campfire);
                break;
            case WorldObjectKind.BuildOrder:
                BuildBuildOrderContent(_current.Node as BuildOrder);
                break;
            case WorldObjectKind.Structure:
                BuildGenericContent();
                break;
        }
    }

    private void BuildResourceContent(ResourceNode r)
    {
        if (r == null) return;
        AddRow("Typ",     r.Type.ToString());
        AddRow("Vorrat",  $"{r.Amount:F1} / {r.MaxAmount:F1}");
        AddBar("Menge",   r.Amount / r.MaxAmount,
            r.IsEmpty ? new Color(0.5f,0.5f,0.5f) : new Color(0.3f,0.8f,0.3f));
        AddRow("Respawn", r.RespawnRate > 0 ? $"+{r.RespawnRate:F2}/Tick" : "Nein");
        AddRow("Status",  r.IsEmpty ? "⚠ Erschöpft" : "✓ Verfügbar",
            r.IsEmpty ? new Color(1f,0.4f,0.4f) : new Color(0.4f,1f,0.5f));

        // Nearby NPCs harvesting
        int harvesters = GameManager.Instance?.AllNpcs
            .Count(n => n.Survival.IsSeekingResource &&
                        n.GlobalPosition.DistanceTo(r.GlobalPosition) < 5f) ?? 0;
        if (harvesters > 0)
            AddRow("Nutzer", $"{harvesters} NPC(s) sammeln hier");
    }

    private void BuildAnimalContent(Animal a)
    {
        if (a == null) return;
        AddRow("Tierart",    a.Type.ToString());
        AddBar("Gesundheit", a.Health / 5f,
            a.Health > 2f ? new Color(0.3f,0.9f,0.3f) : new Color(1f,0.4f,0.4f));
        AddRow("Nahrungswert", $"{a.FoodValue:F0} Einheiten");
        AddRow("Fluchtradius", $"{a.FleeRadius:F0} m");
        AddRow("Status",
            a.IsDead ? "💀 Tot" : "🏃 Flieht/Wandert",
            a.IsDead ? new Color(0.5f,0.5f,0.5f) : new Color(0.8f,0.8f,0.4f));

        // Nearest hunting NPC
        var hunter = GameManager.Instance?.AllNpcs
            .Where(n => n.Cooperation.HasTask)
            .OrderBy(n => n.GlobalPosition.DistanceTo(a.GlobalPosition))
            .FirstOrDefault();
        if (hunter != null && hunter.GlobalPosition.DistanceTo(a.GlobalPosition) < 20f)
            AddRow("Gejagt von", hunter.NpcName);
    }

    private void BuildCampfireContent(Campfire c)
    {
        if (c == null) return;
        AddRow("Typ",    c.WithStoneRing ? "Mit Steinkranz" : "Einfach");
        AddRow("Status", c.CurrentStage.ToString(),
            c.IsBurning ? new Color(1f,0.6f,0.2f) : new Color(0.5f,0.5f,0.5f));
        AddBar("Brennstoff", c.Fuel / c.MaxFuel,
            c.Fuel > c.MaxFuel * 0.5f ? new Color(1f,0.5f,0.1f) : new Color(0.8f,0.3f,0.1f));
        AddRow("Brennstoff", $"{c.Fuel:F1} / {c.MaxFuel:F1}");

        // How many NPCs warming
        int warming = GameManager.Instance?.AllNpcs
            .Count(n => n.GlobalPosition.DistanceTo(c.GlobalPosition) < 6f) ?? 0;
        if (warming > 0)
            AddRow("NPC's in der Nähe", warming.ToString());

        if (!c.IsBurning)
        {
            var hint = new Label();
            hint.Text = "Benötigt mehr Holz zum Entzünden.";
            hint.AutowrapMode = TextServer.AutowrapMode.Word;
            hint.AddThemeColorOverride("font_color", new Color(0.7f,0.5f,0.3f));
            _content.AddChild(hint);
        }
    }

    private void BuildBuildOrderContent(BuildOrder b)
    {
        if (b == null) return;
        var def = KnowledgeCatalog.Get(b.KnowledgeId);
        AddRow("Bauwerk",   def?.DisplayName ?? b.KnowledgeId);
        AddRow("Status",    b.Status.ToString());
        AddBar("Fortschritt", b.Progress / b.Required,
            new Color(0.4f,0.8f,1f));
        AddRow("Fortschritt", $"{(int)(b.Progress/b.Required*100)}%");

        // Materialien
        if (def?.Materials.Count > 0)
        {
            _content.AddChild(new HSeparator());
            var matH = new Label(); matH.Text = "Materialien:";
            matH.AddThemeFontSizeOverride("font_size", 12);
            matH.AddThemeColorOverride("font_color", new Color(0.6f,0.6f,0.6f));
            _content.AddChild(matH);
            foreach (var m in def.Materials)
                AddRow($"  {m.Resource}", $"{m.Amount:F0} benötigt");
        }

        // Workers
        int workers = GameManager.Instance?.AllNpcs
            .Count(n => n.BuildWorker.IsActive &&
                        n.GlobalPosition.DistanceTo(b.GlobalPosition) < 5f) ?? 0;
        AddRow("Bauarbeiter", workers > 0 ? $"{workers} aktiv" : "Keiner");

        if (b.Status == BuildOrderStatus.Pending)
        {
            var hint = new Label();
            hint.Text = "Wartet auf NPC mit passendem Wissen.";
            hint.AutowrapMode = TextServer.AutowrapMode.Word;
            hint.AddThemeColorOverride("font_color", new Color(0.6f,0.6f,0.4f));
            _content.AddChild(hint);
        }
    }

    private void BuildNatureContent(NatureObject n)
    {
        if (n == null) return;
        AddRow("Typ",      n.GetDisplayName());
        AddRow("Status",   n.State.ToString(),
            n.State == NatureObject.GrowthState.Mature ? new Color(0.3f,0.9f,0.3f) :
            n.State == NatureObject.GrowthState.Stump  ? new Color(0.6f,0.4f,0.2f) :
            new Color(0.8f,0.8f,0.4f));
        AddBar("Wuchs",    n.GrowthAge, new Color(0.3f,0.8f,0.3f));

        // Yields
        if (NatureObject.Yields.TryGetValue(n.ObjType, out var yields))
        {
            _content.AddChild(new HSeparator());
            var h = new Label(); h.Text = n.IsFellable ? "Beim Fällen:" : "Beim Sammeln:";
            h.AddThemeFontSizeOverride("font_size", 12);
            h.AddThemeColorOverride("font_color", new Color(0.6f,0.8f,0.6f));
            _content.AddChild(h);
            foreach (var (res, amt, lbl) in yields)
            {
                if (lbl == null) continue;
                bool isPoison = res is ResourceType.BerryPoison or ResourceType.MushroomPoison;
                AddRow($"  {lbl}", $"{amt * n.GrowthAge:F1}",
                    isPoison ? new Color(0.9f,0.3f,0.9f) : new Color(0.8f,0.8f,0.5f));
            }
        }

        if (n.IsFellable && !n.Knowledge_Required_Check())
        {
            var hint = new Label();
            hint.Text = "⚠ Zum Fällen werden Werkzeuge benötigt.";
            hint.AutowrapMode = TextServer.AutowrapMode.Word;
            hint.AddThemeColorOverride("font_color", new Color(1f,0.7f,0.3f));
            hint.AddThemeFontSizeOverride("font_size", 11);
            _content.AddChild(hint);
        }
    }

    private void BuildGenericContent()
    {
        var lbl = new Label();
        lbl.Text = "Kein weiteres Informationen verfügbar.";
        lbl.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
        _content.AddChild(lbl);
    }

    public void Close()
    {
        _panel.Visible = false;
        _current = null;
    }

    // ── UI Build ─────────────────────────────────────────────────────────
    private void BuildUI()
    {
        _panel = new Panel();
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _panel.Position = new Vector2(8, -280);
        _panel.Size     = new Vector2(260, 270);

        var style = new StyleBoxFlat();
        style.BgColor    = new Color(0.06f, 0.08f, 0.13f, 0.95f);
        style.BorderColor = new Color(0.5f, 0.7f, 0.4f);
        style.BorderWidthBottom = style.BorderWidthTop =
        style.BorderWidthLeft   = style.BorderWidthRight = 2;
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[]{"left","right","top","bottom"})
            margin.AddThemeConstantOverride($"margin_{s}", 8);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);

        // Header
        var header = new HBoxContainer();
        _iconLabel = new Label();
        _iconLabel.AddThemeFontSizeOverride("font_size", 22);
        header.AddChild(_iconLabel);

        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 15);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f,0.9f,0.6f));
        _titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(_titleLabel);

        var closeBtn = new Button(); closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(28,0);
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);
        vbox.AddChild(header);
        vbox.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_content);
        vbox.AddChild(scroll);

        margin.AddChild(vbox);
        _panel.AddChild(margin);
        AddChild(_panel);
    }

    private void AddRow(string key, string value, Color? col = null)
    {
        var hbox = new HBoxContainer();
        var k = new Label(); k.Text = key + ":";
        k.CustomMinimumSize = new Vector2(100,0);
        k.AddThemeFontSizeOverride("font_size", 12);
        k.AddThemeColorOverride("font_color", new Color(0.55f,0.55f,0.55f));
        hbox.AddChild(k);
        var v = new Label(); v.Text = value;
        v.AddThemeFontSizeOverride("font_size", 12);
        v.AddThemeColorOverride("font_color", col ?? new Color(0.9f,0.9f,0.9f));
        hbox.AddChild(v);
        _content.AddChild(hbox);
    }

    private void AddBar(string label, float value, Color barColor)
    {
        var hbox = new HBoxContainer();
        var lbl = new Label(); lbl.Text = label + ":";
        lbl.CustomMinimumSize = new Vector2(100,0);
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", new Color(0.55f,0.55f,0.55f));
        hbox.AddChild(lbl);
        var bar = new ProgressBar();
        bar.Value = Mathf.Clamp(value,0f,1f) * 100f;
        bar.ShowPercentage = false;
        bar.CustomMinimumSize = new Vector2(110,15);
        hbox.AddChild(bar);
        _content.AddChild(hbox);
    }
}

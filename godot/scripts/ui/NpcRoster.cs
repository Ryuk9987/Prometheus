#nullable disable
using Godot;
using System.Linq;

/// <summary>
/// NPC Roster (TAB to toggle) — replaces the debug overlay.
/// Clean table view: portrait column, name/role, bars for HP/Hunger/Thirst,
/// tribe badge, top knowledge pills.
/// Click a row → opens NpcInspector for that NPC.
/// </summary>
public partial class NpcRoster : CanvasLayer
{
    private Panel          _panel;
    private Label          _header;
    private ScrollContainer _scroll;
    private VBoxContainer  _list;
    private bool           _open = false;

    public override void _Ready()
    {
        Layer = 5;
        BuildUI();
        _panel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Tab)
        {
            _open = !_open;
            _panel.Visible = _open;
            if (_open) Refresh();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_open) Refresh();
    }

    // ── Data ─────────────────────────────────────────────────────────────
    private void Refresh()
    {
        var npcs = GameManager.Instance?.AllNpcs;
        if (npcs == null) return;

        int total    = npcs.Count;
        int tasks    = npcs.Count(n => n.Cooperation.HasTask);
        int believers = npcs.Count(n => n.Belief.CanHearOracle);
        int tribes   = TribeManager.Instance?.AllTribes.Count ?? 0;

        _header.Text = $"PROMETHEUS — Bevölkerung   👤 {total}   🔮 {believers} Anhänger   🏕 {tribes} Stämme    [TAB schließen]";

        foreach (Node c in _list.GetChildren()) c.QueueFree();

        foreach (var npc in npcs.OrderBy(n => n.NpcName))
            _list.AddChild(BuildRow(npc));
    }

    // ── Row builder ───────────────────────────────────────────────────────
    private Control BuildRow(NpcEntity npc)
    {
        var tribe    = TribeManager.Instance?.GetTribe(npc);
        bool isFollower = npc.Belief.CanHearOracle;
        bool isLeader   = tribe?.Leader == npc;

        var row = new PanelContainer();
        var s   = new StyleBoxFlat();
        s.BgColor = isFollower
            ? new Color(0.08f, 0.12f, 0.18f)
            : new Color(0.06f, 0.07f, 0.1f);
        s.BorderColor = isLeader
            ? new Color(1f, 0.8f, 0.2f)
            : (isFollower ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.15f, 0.17f, 0.22f));
        s.BorderWidthBottom = s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = 1;
        s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight =
        s.CornerRadiusTopLeft    = s.CornerRadiusTopRight    = 4;
        row.AddThemeStyleboxOverride("panel", s);

        var m = new MarginContainer();
        foreach (var side in new[]{"left","right","top","bottom"})
            m.AddThemeConstantOverride($"margin_{side}", 5);
        row.AddChild(m);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        m.AddChild(hbox);

        // ── Avatar circle (color = tribe color)
        var avatar = new ColorRect();
        avatar.CustomMinimumSize = new Vector2(32, 32);
        avatar.Color = tribe?.Color ?? new Color(0.4f, 0.4f, 0.4f);
        var avatarLbl = new Label();
        avatarLbl.Text = npc.NpcName.Substring(0,1);
        avatarLbl.AddThemeFontSizeOverride("font_size", 16);
        avatarLbl.AddThemeColorOverride("font_color", Colors.White);
        avatarLbl.HorizontalAlignment = HorizontalAlignment.Center;
        avatarLbl.VerticalAlignment   = VerticalAlignment.Center;
        avatarLbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        avatar.AddChild(avatarLbl);
        hbox.AddChild(avatar);

        // ── Name + role + tribe
        var infoCol = new VBoxContainer();
        infoCol.CustomMinimumSize = new Vector2(140, 0);
        infoCol.AddThemeConstantOverride("separation", 1);
        hbox.AddChild(infoCol);

        var nameRow = new HBoxContainer();
        var nameLbl = new Label();
        nameLbl.Text = npc.NpcName + (isLeader ? " 👑" : "");
        nameLbl.AddThemeFontSizeOverride("font_size", 13);
        nameLbl.AddThemeColorOverride("font_color",
            isLeader ? new Color(1f,0.85f,0.3f) :
            isFollower ? new Color(0.7f,0.85f,1f) : new Color(0.75f,0.75f,0.75f));
        nameRow.AddChild(nameLbl);
        infoCol.AddChild(nameRow);

        var roleLbl = new Label();
        roleLbl.Text = $"{npc.Cooperation.Role}  •  {(tribe != null ? tribe.Name : "kein Stamm")}";
        roleLbl.AddThemeFontSizeOverride("font_size", 10);
        roleLbl.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.55f));
        infoCol.AddChild(roleLbl);

        // Activity
        string activity =
            npc.Foraging.IsActive          ? "🌿 Sammelt" :
            npc.Survival.IsSeekingResource ? "🍗 Sucht Nahrung" :
            npc.CampfireBuilder.IsActive   ? "🔥 Lagerfeuer" :
            npc.BuildWorker.IsActive       ? "🏗 Baut" :
            npc.Cooperation.HasTask        ? $"⚔ {npc.Cooperation.Role}" :
                                              "🚶 Wandert";
        var actLbl = new Label();
        actLbl.Text = activity;
        actLbl.AddThemeFontSizeOverride("font_size", 10);
        actLbl.AddThemeColorOverride("font_color", new Color(0.5f,0.7f,0.5f));
        infoCol.AddChild(actLbl);

        // ── Status bars (Hunger / Durst / Glaube)
        var barsCol = new VBoxContainer();
        barsCol.CustomMinimumSize = new Vector2(120, 0);
        barsCol.AddThemeConstantOverride("separation", 2);
        hbox.AddChild(barsCol);

        barsCol.AddChild(MiniBar("🍖", npc.Needs.Hunger,
            npc.Needs.IsStarving ? new Color(1f,0.15f,0.15f) :
            npc.Needs.IsHungry   ? new Color(1f,0.65f,0.1f)  : new Color(0.3f,0.75f,0.3f)));

        barsCol.AddChild(MiniBar("💧", npc.Needs.Thirst,
            npc.Needs.Thirst >= 0.9f ? new Color(1f,0.15f,0.15f) :
            npc.Needs.IsThirsty      ? new Color(0.5f,0.7f,1f)   : new Color(0.25f,0.55f,1f)));

        barsCol.AddChild(MiniBar("🔮", npc.Belief.Belief,
            isFollower ? new Color(0.4f,0.7f,1f) : new Color(0.35f,0.35f,0.4f)));

        // ── Knowledge pills
        var pillsFlow = new HFlowContainer();
        pillsFlow.AddThemeConstantOverride("h_separation", 3);
        pillsFlow.AddThemeConstantOverride("v_separation", 2);
        pillsFlow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(pillsFlow);

        var topKnowledge = npc.Knowledge.Knowledge
            .OrderByDescending(kv => kv.Value.Depth)
            .Take(6);
        foreach (var kv in topKnowledge)
        {
            var def = KnowledgeCatalog.Get(kv.Key);
            var pill = new Label();
            pill.Text = $"{def?.Icon ?? "•"}{(kv.Value.Depth >= 0.5f ? "+" : "")}";
            pill.AddThemeFontSizeOverride("font_size", 11);
            pill.TooltipText = $"{def?.DisplayName ?? kv.Key} ({kv.Value.Depth:F2})";
            pill.AddThemeColorOverride("font_color",
                kv.Value.Depth >= 0.5f ? new Color(0.4f,1f,0.5f) :
                kv.Value.Depth >= 0.2f ? new Color(0.9f,0.8f,0.3f) : new Color(0.5f,0.5f,0.5f));
            pillsFlow.AddChild(pill);
        }

        // ── Click → NpcInspector
        var btn = new Button();
        btn.Text = "🔍";
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.TooltipText = "Inspizieren";
        btn.CustomMinimumSize = new Vector2(32, 0);
        var cap = npc;
        btn.Pressed += () => {
            NpcInspector.Instance?.Open(cap);
            _open = false;
            _panel.Visible = false;
        };
        hbox.AddChild(btn);

        return row;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static Control MiniBar(string icon, float value, Color col)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 3);

        var lbl = new Label();
        lbl.Text = icon;
        lbl.AddThemeFontSizeOverride("font_size", 10);
        hbox.AddChild(lbl);

        var bar = new ProgressBar();
        bar.Value            = Mathf.Clamp(value, 0f, 1f) * 100f;
        bar.ShowPercentage   = false;
        bar.CustomMinimumSize = new Vector2(80, 10);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(bar);

        var num = new Label();
        num.Text = $"{(int)(value*100)}";
        num.AddThemeFontSizeOverride("font_size", 9);
        num.AddThemeColorOverride("font_color", col);
        num.CustomMinimumSize = new Vector2(24, 0);
        hbox.AddChild(num);

        return hbox;
    }

    // ── UI Build ──────────────────────────────────────────────────────────
    private void BuildUI()
    {
        _panel = new Panel();
        _panel.AnchorLeft   = 0.05f; _panel.AnchorRight  = 0.95f;
        _panel.AnchorTop    = 0.04f; _panel.AnchorBottom = 0.96f;

        var ps = new StyleBoxFlat();
        ps.BgColor    = new Color(0.04f, 0.05f, 0.09f, 0.97f);
        ps.BorderColor = new Color(0.35f, 0.45f, 0.7f);
        ps.BorderWidthBottom = ps.BorderWidthTop =
        ps.BorderWidthLeft   = ps.BorderWidthRight = 2;
        ps.CornerRadiusBottomLeft = ps.CornerRadiusBottomRight =
        ps.CornerRadiusTopLeft    = ps.CornerRadiusTopRight    = 8;
        _panel.AddThemeStyleboxOverride("panel", ps);

        var rootM = new MarginContainer();
        rootM.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[]{"left","right","top","bottom"})
            rootM.AddThemeConstantOverride($"margin_{s}", 12);
        _panel.AddChild(rootM);

        var rootV = new VBoxContainer();
        rootV.AddThemeConstantOverride("separation", 6);
        rootM.AddChild(rootV);

        // Header
        _header = new Label();
        _header.AddThemeFontSizeOverride("font_size", 14);
        _header.AddThemeColorOverride("font_color", new Color(0.9f,0.85f,0.5f));
        rootV.AddChild(_header);

        // Column headers
        var colHdr = new HBoxContainer();
        colHdr.AddThemeConstantOverride("separation", 8);

        void ColHead(string t, float minW, Color c) {
            var l = new Label(); l.Text = t;
            l.AddThemeFontSizeOverride("font_size", 11);
            l.AddThemeColorOverride("font_color", c);
            if (minW > 0) l.CustomMinimumSize = new Vector2(minW, 0);
            else l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            colHdr.AddChild(l);
        }

        ColHead("", 32, Colors.White);                       // avatar
        ColHead("Name / Rolle / Aktivität",   140, new Color(0.6f,0.65f,0.7f));
        ColHead("Hunger / Durst / Glaube",    120, new Color(0.6f,0.65f,0.7f));
        ColHead("Wissen",                       0, new Color(0.6f,0.65f,0.7f));
        ColHead("",                            32, Colors.White); // inspect btn
        rootV.AddChild(colHdr);
        rootV.AddChild(new HSeparator());

        _scroll = new ScrollContainer();
        _scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rootV.AddChild(_scroll);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 3);
        _list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _scroll.AddChild(_list);

        AddChild(_panel);
    }
}

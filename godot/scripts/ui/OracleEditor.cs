#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Oracle Editor (O-Taste) — simple two-column overlay.
///
/// LEFT:  Follower knowledge pool — click to toggle selection (no slot limit)
/// RIGHT: Matching recipes for current selection — click Reveal to teach NPCs
///
/// Design: Full-screen semi-transparent overlay. No drag. No slots.
/// </summary>
public partial class OracleEditor : CanvasLayer
{
    public static OracleEditor Instance { get; private set; }

    private Control         _root;
    private ScrollContainer _poolScroll;
    private VBoxContainer   _poolBox;
    private ScrollContainer _resultScroll;
    private VBoxContainer   _resultBox;
    private Label           _statusLabel;
    private Label           _selectionLabel;

    private readonly HashSet<string> _selected = new();

    public override void _Ready()
    {
        Instance = this;
        BuildUI();
        _root.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.O)
            {
                if (_root.Visible) Close(); else Open();
                GetViewport().SetInputAsHandled();
            }
            else if (k.Keycode == Key.Escape && _root.Visible)
            {
                Close(); GetViewport().SetInputAsHandled();
            }
        }
    }

    public void Open()
    {
        _selected.Clear();
        _root.Visible = true;
        Refresh();
    }

    public void Close() => _root.Visible = false;

    // ── Refresh ───────────────────────────────────────────────────────────
    private void Refresh()
    {
        RefreshPool();
        RefreshResults();
    }

    private void RefreshPool()
    {
        foreach (Node c in _poolBox.GetChildren()) c.QueueFree();

        var pool = CollectFollowerKnowledge();

        var hdr = MakeLabel($"📚 Wissen deiner Anhänger ({pool.Count})", 14, new Color(0.6f,0.85f,1f));
        _poolBox.AddChild(hdr);

        if (pool.Count == 0)
        {
            _poolBox.AddChild(MakeLabel(
                "Noch keine Anhänger.\nBringe NPCs zum Glauben damit der Pool gefüllt wird.",
                12, new Color(0.6f,0.5f,0.3f), wrap: true));
            _selectionLabel.Text = "";
            return;
        }

        _poolBox.AddChild(new HSeparator());
        _poolBox.AddChild(MakeLabel("Klicke Konzepte an um sie zu kombinieren:", 11, new Color(0.5f,0.5f,0.5f)));

        // Group by category
        var byCategory = pool
            .GroupBy(kv => KnowledgeCatalog.Get(kv.Key)?.Category ?? KnowledgeCategory.Concept)
            .OrderBy(g => (int)g.Key);

        foreach (var group in byCategory)
        {
            var catHdr = MakeLabel(CategoryName(group.Key), 11, new Color(0.5f,0.65f,0.5f));
            _poolBox.AddChild(catHdr);

            foreach (var kv in group.OrderByDescending(x => x.Value))
            {
                string id    = kv.Key;
                float  depth = kv.Value;
                var def = KnowledgeCatalog.Get(id);
                bool sel = _selected.Contains(id);

                var btn = new Button();
                btn.Text = $"{def?.Icon ?? "•"} {def?.DisplayName ?? id}   {DepthBar(depth)} {depth:F2}";
                btn.AddThemeFontSizeOverride("font_size", 12);
                btn.TooltipText = def?.Description ?? id;
                btn.Flat = !sel;

                if (sel) btn.Modulate = new Color(0.4f, 0.9f, 1f);
                else     btn.Modulate = Colors.White;

                string cap = id;
                btn.Pressed += () => { Toggle(cap); };
                _poolBox.AddChild(btn);
            }
        }

        UpdateSelectionLabel();
    }

    private void RefreshResults()
    {
        foreach (Node c in _resultBox.GetChildren()) c.QueueFree();

        var hdr = MakeLabel("💡 Mögliche Offenbarungen", 14, new Color(1f,0.9f,0.4f));
        _resultBox.AddChild(hdr);
        _resultBox.AddChild(new HSeparator());

        if (_selected.Count == 0)
        {
            _resultBox.AddChild(MakeLabel(
                "Wähle ein oder mehrere Konzepte aus dem Pool links.\n\nDas Orakel zeigt dir was daraus entstehen kann.",
                12, new Color(0.45f,0.45f,0.45f), wrap: true));
            return;
        }

        var pool = CollectFollowerKnowledge();
        var selArr = _selected.ToArray();
        var allMatches = KnowledgeRecipes.FindMatches(selArr);

        // Partition: ready vs. depth-too-low
        var ready  = allMatches.Where(r => HasDepth(r, pool)).ToList();
        var nearly = allMatches.Where(r => !HasDepth(r, pool)).ToList();

        if (allMatches.Count == 0)
        {
            _resultBox.AddChild(MakeLabel(
                "Diese Kombination ergibt (noch) keine Offenbarung.\nVersuche eine andere Auswahl.",
                12, new Color(0.7f,0.4f,0.4f), wrap: true));

            // Hint: what other known knowledge would help
            var hints = SuggestAdditions(selArr, pool);
            if (hints.Count > 0)
            {
                _resultBox.AddChild(MakeLabel("Tipp: füge hinzu um mehr zu entdecken:", 11, new Color(0.5f,0.6f,0.4f)));
                foreach (var h in hints.Take(4))
                {
                    var def = KnowledgeCatalog.Get(h);
                    _resultBox.AddChild(MakeLabel($"  + {def?.Icon} {def?.DisplayName ?? h}", 12, new Color(0.7f,0.8f,0.5f)));
                }
            }
            return;
        }

        if (ready.Count > 0)
            _resultBox.AddChild(MakeLabel($"✅ {ready.Count} Offenbarung(en) bereit:", 12, new Color(0.4f,1f,0.5f)));

        foreach (var r in ready)   AddRecipeCard(r, pool, ready: true);

        if (nearly.Count > 0)
        {
            _resultBox.AddChild(new HSeparator());
            _resultBox.AddChild(MakeLabel($"⚠ {nearly.Count} fast bereit (Tiefe fehlt):", 11, new Color(0.7f,0.6f,0.3f)));
            foreach (var r in nearly) AddRecipeCard(r, pool, ready: false);
        }
    }

    // ── Recipe card ───────────────────────────────────────────────────────
    private void AddRecipeCard(KnowledgeRecipes.Recipe recipe, Dictionary<string,float> pool, bool ready)
    {
        var card = new PanelContainer();
        var s = new StyleBoxFlat();
        s.BgColor = ready ? new Color(0.07f,0.14f,0.09f) : new Color(0.1f,0.08f,0.05f);
        s.BorderColor = ready ? new Color(0.3f,0.75f,0.4f) : new Color(0.5f,0.4f,0.15f);
        s.BorderWidthBottom = s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = 1;
        card.AddThemeStyleboxOverride("panel", s);

        var m = new MarginContainer();
        foreach (var side in new[]{"left","right","top","bottom"})
            m.AddThemeConstantOverride($"margin_{side}", 6);
        card.AddChild(m);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        m.AddChild(vbox);

        var def = KnowledgeCatalog.Get(recipe.Result);
        vbox.AddChild(MakeLabel($"{def?.Icon ?? "💡"} {def?.DisplayName ?? recipe.Result}", 14,
            ready ? new Color(0.5f,1f,0.6f) : new Color(0.85f,0.7f,0.35f)));

        vbox.AddChild(MakeLabel(recipe.Hint, 11, new Color(0.7f,0.7f,0.6f), wrap: true));
        vbox.AddChild(MakeLabel($"\" {recipe.NpcThought} \"", 10, new Color(0.5f,0.55f,0.8f), wrap: true));

        if (ready)
        {
            var btn = new Button();
            btn.Text = "✨ Dem Orakel offenbaren";
            btn.AddThemeFontSizeOverride("font_size", 13);
            var cap = recipe;
            btn.Pressed += () => Reveal(cap);
            vbox.AddChild(btn);
        }
        else
        {
            // Show missing depth inline
            var sb = new System.Text.StringBuilder("Benötigte Tiefe:\n");
            foreach (var ing in recipe.Ingredients)
            {
                pool.TryGetValue(ing, out float d);
                if (d < recipe.MinDepth)
                {
                    var iDef = KnowledgeCatalog.Get(ing);
                    sb.Append($"  {iDef?.DisplayName ?? ing}: {d:F2} von {recipe.MinDepth:F2}\n");
                }
            }
            vbox.AddChild(MakeLabel(sb.ToString(), 10, new Color(0.8f,0.5f,0.3f), wrap: true));
        }

        _resultBox.AddChild(card);
    }

    // ── Actions ───────────────────────────────────────────────────────────
    private void Toggle(string id)
    {
        if (_selected.Contains(id)) _selected.Remove(id);
        else _selected.Add(id);
        Refresh();
    }

    private void Reveal(KnowledgeRecipes.Recipe recipe)
    {
        int taught = 0;
        foreach (var npc in GameManager.Instance?.AllNpcs ?? new())
        {
            if (!npc.Belief.CanHearOracle) continue;
            bool hasBase = recipe.Ingredients.All(ing => npc.Knowledge.Knows(ing));
            if (!hasBase) continue;

            float depth = recipe.Ingredients
                .Select(ing => npc.Knowledge.Knowledge.TryGetValue(ing, out var e) ? e.Depth : 0f)
                .Average() * 0.75f;

            npc.Knowledge.Learn(recipe.Result, depth, recipe.MinDepth + 0.2f, "oracle_revelation");
            taught++;
        }

        OracleTablet.Instance?.TriggerFlash($"oracle:{recipe.Result}");

        var def = KnowledgeCatalog.Get(recipe.Result);
        _statusLabel.Text = taught > 0
            ? $"✨ {taught} NPC(s) haben '{def?.DisplayName ?? recipe.Result}' empfangen!"
            : "⚠ Kein NPC hatte alle Voraussetzungen.";

        _selected.Clear();
        Refresh();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void UpdateSelectionLabel()
    {
        if (_selected.Count == 0)
        {
            _selectionLabel.Text = "Nichts ausgewählt";
            _selectionLabel.AddThemeColorOverride("font_color", new Color(0.4f,0.4f,0.4f));
        }
        else
        {
            var names = _selected.Select(id => KnowledgeCatalog.Get(id)?.Icon + " " + (KnowledgeCatalog.Get(id)?.DisplayName ?? id));
            _selectionLabel.Text = "Auswahl: " + string.Join(" + ", names);
            _selectionLabel.AddThemeColorOverride("font_color", new Color(0.9f,0.85f,0.5f));
        }
    }

    private static bool HasDepth(KnowledgeRecipes.Recipe r, Dictionary<string,float> pool)
    {
        foreach (var ing in r.Ingredients)
            if (!pool.TryGetValue(ing, out float d) || d < r.MinDepth) return false;
        return true;
    }

    private List<string> SuggestAdditions(string[] current, Dictionary<string,float> pool)
    {
        var suggestions = new HashSet<string>();
        var currentSet  = new HashSet<string>(current);
        foreach (var recipe in KnowledgeRecipes.All)
        {
            bool allCoveredExceptOne = recipe.Ingredients.Count(i => !currentSet.Contains(i)) <= 1;
            if (!allCoveredExceptOne) continue;
            foreach (var ing in recipe.Ingredients)
                if (!currentSet.Contains(ing) && pool.ContainsKey(ing))
                    suggestions.Add(ing);
        }
        return suggestions.ToList();
    }

    private Dictionary<string, float> CollectFollowerKnowledge()
    {
        var pool = new Dictionary<string, float>();
        foreach (var npc in GameManager.Instance?.AllNpcs ?? new())
        {
            if (!npc.Belief.CanHearOracle) continue;
            foreach (var kv in npc.Knowledge.Knowledge)
                if (!pool.ContainsKey(kv.Key) || pool[kv.Key] < kv.Value.Depth)
                    pool[kv.Key] = kv.Value.Depth;
        }
        return pool;
    }

    private static string DepthBar(float d)
    {
        int f = Mathf.Clamp((int)(d * 10), 0, 10);
        return new string('█', f) + new string('░', 10 - f);
    }

    private static string CategoryName(KnowledgeCategory cat) => cat switch {
        KnowledgeCategory.Nature   => "🌿 Natur",
        KnowledgeCategory.Tool     => "🔧 Werkzeug",
        KnowledgeCategory.Building => "🏗 Gebäude",
        KnowledgeCategory.Skill    => "📚 Fähigkeit",
        KnowledgeCategory.Concept  => "💡 Konzept",
        _ => cat.ToString()
    };

    private static Label MakeLabel(string text, int size, Color col, bool wrap = false)
    {
        var l = new Label();
        l.Text = text;
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", col);
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.Word;
        return l;
    }

    // ── UI Build ──────────────────────────────────────────────────────────
    private void BuildUI()
    {
        // Full-screen semi-transparent overlay
        _root = new Control();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // Background
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0f, 0f, 0f, 0.85f);
        _root.AddChild(bg);

        // Main panel — centered, 92% of screen
        var panel = new Panel();
        panel.AnchorLeft   = 0.04f; panel.AnchorRight  = 0.96f;
        panel.AnchorTop    = 0.04f; panel.AnchorBottom = 0.96f;
        var ps = new StyleBoxFlat();
        ps.BgColor    = new Color(0.06f, 0.07f, 0.12f, 0.98f);
        ps.BorderColor = new Color(0.5f, 0.4f, 0.8f);
        ps.BorderWidthBottom = ps.BorderWidthTop =
        ps.BorderWidthLeft   = ps.BorderWidthRight = 2;
        ps.CornerRadiusBottomLeft = ps.CornerRadiusBottomRight =
        ps.CornerRadiusTopLeft    = ps.CornerRadiusTopRight    = 8;
        panel.AddThemeStyleboxOverride("panel", ps);
        _root.AddChild(panel);

        var rootMargin = new MarginContainer();
        rootMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[]{"left","right","top","bottom"})
            rootMargin.AddThemeConstantOverride($"margin_{s}", 14);
        panel.AddChild(rootMargin);

        var rootVbox = new VBoxContainer();
        rootVbox.AddThemeConstantOverride("separation", 8);
        rootMargin.AddChild(rootVbox);

        // ── Title bar
        var titleRow = new HBoxContainer();
        var title = MakeLabel("☀  Orakel — Wissen offenbaren", 22, new Color(1f,0.9f,0.4f));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        var closeBtn = new Button(); closeBtn.Text = "✕  Schließen  (O)";
        closeBtn.Pressed += Close;
        titleRow.AddChild(closeBtn);
        rootVbox.AddChild(titleRow);

        // ── Selection display
        _selectionLabel = MakeLabel("Nichts ausgewählt", 13, new Color(0.4f,0.4f,0.4f));
        rootVbox.AddChild(_selectionLabel);

        rootVbox.AddChild(new HSeparator());

        // ── Two-column content
        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 16);
        cols.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rootVbox.AddChild(cols);

        // Left column header
        var leftVbox = new VBoxContainer();
        leftVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftVbox.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        leftVbox.AddThemeConstantOverride("separation", 4);
        cols.AddChild(leftVbox);

        _poolScroll = new ScrollContainer();
        _poolScroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        _poolScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _poolBox = new VBoxContainer();
        _poolBox.AddThemeConstantOverride("separation", 3);
        _poolBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _poolScroll.AddChild(_poolBox);
        leftVbox.AddChild(_poolScroll);

        // Vertical divider
        var div = new VSeparator();
        cols.AddChild(div);

        // Right column
        var rightVbox = new VBoxContainer();
        rightVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightVbox.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        rightVbox.AddThemeConstantOverride("separation", 4);
        cols.AddChild(rightVbox);

        _resultScroll = new ScrollContainer();
        _resultScroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        _resultScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _resultBox = new VBoxContainer();
        _resultBox.AddThemeConstantOverride("separation", 6);
        _resultBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _resultScroll.AddChild(_resultBox);
        rightVbox.AddChild(_resultScroll);

        // Status bar
        _statusLabel = MakeLabel("", 13, new Color(0.9f,0.8f,0.4f));
        rootVbox.AddChild(_statusLabel);
    }
}

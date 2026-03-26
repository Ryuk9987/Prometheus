#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Oracle Editor (O-Taste) — completely redesigned.
/// 
/// Left panel:  Follower knowledge pool (collective knowledge of believers)
/// Center:      Workbench — drag up to 3 knowledge tiles here to combine
/// Right panel: Result preview — what the Oracle could teach
/// 
/// Flow:
///   1. Player picks 1–3 tiles from follower pool
///   2. Matching recipes light up in preview
///   3. Player confirms → Oracle flashes → NPCs learn
/// </summary>
public partial class OracleEditor : CanvasLayer
{
    public static OracleEditor Instance { get; private set; }

    private Panel               _panel;
    private VBoxContainer       _poolBox;       // left: follower knowledge
    private HBoxContainer       _workbench;     // center: selected slots
    private VBoxContainer       _resultBox;     // right: matching recipes
    private Label               _statusLabel;

    private readonly List<string>        _selected   = new(); // selected knowledge IDs
    private readonly List<KnowledgeRecipes.Recipe> _matches = new();

    public override void _Ready()
    {
        Instance = this;
        BuildUI();
        _panel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.O || k.Keycode == Key.B)
            {
                if (_panel.Visible) Close();
                else Open();
                GetViewport().SetInputAsHandled();
            }
            if (k.Keycode == Key.Escape && _panel.Visible)
            {
                Close(); GetViewport().SetInputAsHandled();
            }
        }
    }

    public void Open()
    {
        _panel.Visible = true;
        _selected.Clear();
        RefreshAll();
    }

    public void Close() => _panel.Visible = false;

    // ── Refresh ───────────────────────────────────────────────────────────
    private void RefreshAll()
    {
        RefreshPool();
        RefreshWorkbench();
        RefreshResults();
    }

    private void RefreshPool()
    {
        foreach (Node c in _poolBox.GetChildren()) c.QueueFree();

        // Collect all knowledge from followers
        var pool = CollectFollowerKnowledge();
        if (pool.Count == 0)
        {
            var lbl = new Label();
            lbl.Text = "Keine Anhänger mit Wissen vorhanden.\nBringe NPCs dazu dir zu glauben.";
            lbl.AddThemeColorOverride("font_color", new Color(0.6f,0.5f,0.3f));
            lbl.AutowrapMode = TextServer.AutowrapMode.Word;
            _poolBox.AddChild(lbl);
            return;
        }

        var header = new Label();
        header.Text = $"📚 Wissen deiner Anhänger ({pool.Count} Konzepte)";
        header.AddThemeFontSizeOverride("font_size", 13);
        header.AddThemeColorOverride("font_color", new Color(0.6f,0.8f,1f));
        _poolBox.AddChild(header);
        _poolBox.AddChild(new HSeparator());

        // Group by category
        foreach (var group in pool.GroupBy(p => KnowledgeCatalog.Get(p.Key)?.Category ?? KnowledgeCategory.Concept)
            .OrderBy(g => g.Key))
        {
            var catLabel = new Label();
            catLabel.Text = CategoryName(group.Key);
            catLabel.AddThemeFontSizeOverride("font_size", 11);
            catLabel.AddThemeColorOverride("font_color", new Color(0.5f,0.6f,0.5f));
            _poolBox.AddChild(catLabel);

            foreach (var kv in group.OrderByDescending(x => x.Value))
            {
                string id = kv.Key;
                float  depth = kv.Value;
                var def = KnowledgeCatalog.Get(id);
                bool selected = _selected.Contains(id);

                var btn = new Button();
                btn.Text = $"{def?.Icon ?? "•"} {def?.DisplayName ?? id}  {DepthBar(depth)}";
                btn.AddThemeFontSizeOverride("font_size", 12);
                btn.TooltipText = $"Tiefe: {depth:F2}";

                if (selected)
                {
                    btn.AddThemeColorOverride("font_color", new Color(0.2f,0.2f,0.2f));
                    btn.Modulate = new Color(0.6f,0.9f,1f);
                }

                string capturedId = id;
                btn.Pressed += () => ToggleSelect(capturedId);
                _poolBox.AddChild(btn);
            }
        }
    }

    private void RefreshWorkbench()
    {
        foreach (Node c in _workbench.GetChildren()) c.QueueFree();

        for (int i = 0; i < 3; i++)
        {
            var slot = new Panel();
            slot.CustomMinimumSize = new Vector2(160, 80);

            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = i < _selected.Count
                ? new Color(0.15f, 0.25f, 0.4f)
                : new Color(0.08f, 0.1f, 0.15f);
            slotStyle.BorderColor = i < _selected.Count
                ? new Color(0.4f, 0.7f, 1f)
                : new Color(0.25f, 0.3f, 0.4f);
            slotStyle.BorderWidthBottom = slotStyle.BorderWidthTop =
            slotStyle.BorderWidthLeft   = slotStyle.BorderWidthRight = 2;
            slot.AddThemeStyleboxOverride("panel", slotStyle);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 2);
            slot.AddChild(vbox);

            if (i < _selected.Count)
            {
                string id = _selected[i];
                var def = KnowledgeCatalog.Get(id);
                var lbl = new Label();
                lbl.Text = $"{def?.Icon ?? "•"}\n{def?.DisplayName ?? id}";
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.AddThemeFontSizeOverride("font_size", 13);
                lbl.AddThemeColorOverride("font_color", new Color(0.9f,0.95f,1f));
                vbox.AddChild(lbl);

                string capturedId = id;
                var removeBtn = new Button();
                removeBtn.Text = "✕ Entfernen";
                removeBtn.AddThemeFontSizeOverride("font_size", 10);
                removeBtn.Pressed += () => { _selected.Remove(capturedId); RefreshAll(); };
                vbox.AddChild(removeBtn);
            }
            else
            {
                var lbl = new Label();
                lbl.Text = $"Slot {i+1}\n(leer)";
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.AddThemeColorOverride("font_color", new Color(0.3f,0.35f,0.4f));
                lbl.AddThemeFontSizeOverride("font_size", 11);
                vbox.AddChild(lbl);
            }

            _workbench.AddChild(slot);

            if (i < 2)
            {
                var plus = new Label(); plus.Text = "+";
                plus.AddThemeFontSizeOverride("font_size", 24);
                plus.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
                plus.VerticalAlignment = VerticalAlignment.Center;
                _workbench.AddChild(plus);
            }
        }
    }

    private void RefreshResults()
    {
        foreach (Node c in _resultBox.GetChildren()) c.QueueFree();

        var header = new Label();
        header.Text = "💡 Mögliche Offenbarungen";
        header.AddThemeFontSizeOverride("font_size", 13);
        header.AddThemeColorOverride("font_color", new Color(1f,0.9f,0.5f));
        _resultBox.AddChild(header);
        _resultBox.AddChild(new HSeparator());

        if (_selected.Count == 0)
        {
            var hint = new Label();
            hint.Text = "Wähle 1–3 Wissens-Bausteine aus dem Pool links.";
            hint.AutowrapMode = TextServer.AutowrapMode.Word;
            hint.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
            _resultBox.AddChild(hint);
            return;
        }

        _matches.Clear();
        _matches.AddRange(KnowledgeRecipes.FindMatches(_selected.ToArray()));

        // Filter: only recipes where followers have sufficient depth
        var pool = CollectFollowerKnowledge();
        var validMatches = _matches.Where(r => {
            foreach (var ing in r.Ingredients)
                if (!pool.TryGetValue(ing, out float d) || d < r.MinDepth) return false;
            return true;
        }).ToList();

        // Also show near-matches (missing depth only)
        var nearMatches = _matches.Except(validMatches).ToList();

        if (validMatches.Count == 0 && nearMatches.Count == 0)
        {
            var noMatch = new Label();
            noMatch.Text = "Diese Kombination ergibt keine bekannte Offenbarung.\nProbiere andere Kombinationen.";
            noMatch.AutowrapMode = TextServer.AutowrapMode.Word;
            noMatch.AddThemeColorOverride("font_color", new Color(0.7f,0.4f,0.4f));
            _resultBox.AddChild(noMatch);
            return;
        }

        foreach (var recipe in validMatches)
        {
            var recipePanel = BuildRecipePanel(recipe, ready: true);
            _resultBox.AddChild(recipePanel);
        }

        if (nearMatches.Count > 0)
        {
            var nearHeader = new Label();
            nearHeader.Text = "⚠ Fast bereit (zu geringe Tiefe):";
            nearHeader.AddThemeFontSizeOverride("font_size", 11);
            nearHeader.AddThemeColorOverride("font_color", new Color(0.7f,0.6f,0.3f));
            _resultBox.AddChild(nearHeader);
            foreach (var recipe in nearMatches)
                _resultBox.AddChild(BuildRecipePanel(recipe, ready: false));
        }
    }

    private Panel BuildRecipePanel(KnowledgeRecipes.Recipe recipe, bool ready)
    {
        var panel = new Panel();
        panel.CustomMinimumSize = new Vector2(0, 0);
        var pStyle = new StyleBoxFlat();
        pStyle.BgColor = ready
            ? new Color(0.08f, 0.15f, 0.1f)
            : new Color(0.1f, 0.09f, 0.06f);
        pStyle.BorderColor = ready ? new Color(0.3f,0.8f,0.4f) : new Color(0.5f,0.45f,0.2f);
        pStyle.BorderWidthBottom = pStyle.BorderWidthTop =
        pStyle.BorderWidthLeft   = pStyle.BorderWidthRight = 1;
        panel.AddThemeStyleboxOverride("panel", pStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        var margin = new MarginContainer();
        foreach (var s in new[]{"left","right","top","bottom"})
            margin.AddThemeConstantOverride($"margin_{s}", 6);
        panel.AddChild(margin);
        margin.AddChild(vbox);

        var def = KnowledgeCatalog.Get(recipe.Result);
        var titleLbl = new Label();
        titleLbl.Text = $"{def?.Icon ?? "💡"} {def?.DisplayName ?? recipe.Result}";
        titleLbl.AddThemeFontSizeOverride("font_size", 14);
        titleLbl.AddThemeColorOverride("font_color", ready ? new Color(0.5f,1f,0.6f) : new Color(0.8f,0.7f,0.4f));
        vbox.AddChild(titleLbl);

        var hintLbl = new Label();
        hintLbl.Text = recipe.Hint;
        hintLbl.AddThemeFontSizeOverride("font_size", 11);
        hintLbl.AddThemeColorOverride("font_color", new Color(0.7f,0.7f,0.6f));
        hintLbl.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(hintLbl);

        var thoughtLbl = new Label();
        thoughtLbl.Text = $"💭 „{recipe.NpcThought}"";
        thoughtLbl.AddThemeFontSizeOverride("font_size", 10);
        thoughtLbl.AddThemeColorOverride("font_color", new Color(0.5f,0.6f,0.8f));
        thoughtLbl.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(thoughtLbl);

        if (ready)
        {
            var revealBtn = new Button();
            revealBtn.Text = "✨ Dem Orakel offenbaren";
            revealBtn.AddThemeFontSizeOverride("font_size", 13);
            var capturedRecipe = recipe;
            revealBtn.Pressed += () => RevealKnowledge(capturedRecipe);
            vbox.AddChild(revealBtn);
        }
        else
        {
            // Show what's missing
            var pool = CollectFollowerKnowledge();
            var missingParts = new System.Text.StringBuilder();
            foreach (var ing in recipe.Ingredients)
            {
                pool.TryGetValue(ing, out float d);
                if (d < recipe.MinDepth)
                {
                    var ingDef = KnowledgeCatalog.Get(ing);
                    missingParts.Append($"  {ingDef?.DisplayName ?? ing}: {d:F2} / {recipe.MinDepth:F2}\n");
                }
            }
            var missingLbl = new Label();
            missingLbl.Text = "Benötigte Tiefe fehlt:\n" + missingParts;
            missingLbl.AddThemeFontSizeOverride("font_size", 10);
            missingLbl.AddThemeColorOverride("font_color", new Color(0.8f,0.5f,0.3f));
            vbox.AddChild(missingLbl);
        }

        return panel;
    }

    // ── Logic ─────────────────────────────────────────────────────────────
    private void ToggleSelect(string id)
    {
        if (_selected.Contains(id))
        {
            _selected.Remove(id);
        }
        else if (_selected.Count < 3)
        {
            _selected.Add(id);
        }
        else
        {
            _statusLabel.Text = "Maximal 3 Bausteine gleichzeitig wählen.";
            return;
        }
        _statusLabel.Text = "";
        RefreshAll();
    }

    private void RevealKnowledge(KnowledgeRecipes.Recipe recipe)
    {
        if (OracleTablet.Instance == null)
        {
            _statusLabel.Text = "Kein Orakel-Tablet in der Welt gefunden!";
            return;
        }

        // Teach result to all followers
        int taught = 0;
        foreach (var npc in GameManager.Instance?.AllNpcs ?? new())
        {
            if (!npc.Belief.CanHearOracle) continue;

            // Check NPC has the ingredients
            bool canLearn = true;
            foreach (var ing in recipe.Ingredients)
                if (!npc.Knowledge.Knows(ing)) { canLearn = false; break; }

            if (!canLearn) continue;

            float resultDepth = recipe.Ingredients
                .Select(ing => npc.Knowledge.Knows(ing) ? npc.Knowledge.Knowledge[ing].Depth : 0f)
                .Average() * 0.8f;

            npc.Knowledge.Learn(recipe.Result, resultDepth, recipe.MinDepth + 0.2f, "oracle_revelation");
            GD.Print($"[OracleEditor] {npc.NpcName} learned {recipe.Result} (depth:{resultDepth:F2}) via revelation");
            taught++;
        }

        // Trigger Oracle flash
        OracleTablet.Instance.TriggerFlash($"oracle:{recipe.Result}");

        _statusLabel.Text = taught > 0
            ? $"✨ {taught} Anhänger haben '{KnowledgeCatalog.Get(recipe.Result)?.DisplayName ?? recipe.Result}' empfangen!"
            : "⚠ Kein Anhänger konnte das Wissen empfangen (fehlende Grundlagen).";

        _selected.Clear();
        RefreshAll();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private Dictionary<string, float> CollectFollowerKnowledge()
    {
        var pool = new Dictionary<string, float>();
        if (GameManager.Instance == null) return pool;
        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            if (!npc.Belief.CanHearOracle) continue;
            foreach (var kv in npc.Knowledge.Knowledge)
            {
                if (!pool.ContainsKey(kv.Key) || pool[kv.Key] < kv.Value.Depth)
                    pool[kv.Key] = kv.Value.Depth;
            }
        }
        return pool;
    }

    private static string DepthBar(float depth)
    {
        int f = (int)(depth * 10);
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

    // ── UI Build ─────────────────────────────────────────────────────────
    private void BuildUI()
    {
        _panel = new Panel();
        _panel.AnchorLeft = 0f; _panel.AnchorRight  = 0f;
        _panel.AnchorTop  = 0f; _panel.AnchorBottom = 0f;
        _panel.OffsetLeft = 0; _panel.OffsetTop    = 0;
        _panel.OffsetRight = 0; _panel.OffsetBottom = 0;

        var style = new StyleBoxFlat();
        style.BgColor    = new Color(0.05f, 0.05f, 0.1f, 0.97f);
        style.BorderColor = new Color(0.5f, 0.4f, 0.8f);
        style.BorderWidthBottom = style.BorderWidthTop =
        style.BorderWidthLeft   = style.BorderWidthRight = 2;
        _panel.AddThemeStyleboxOverride("panel", style);

        AddChild(_panel);
        _panel.Ready += () => {
            _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        };

        var rootMargin = new MarginContainer();
        rootMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[]{"left","right","top","bottom"})
            rootMargin.AddThemeConstantOverride($"margin_{s}", 12);
        _panel.AddChild(rootMargin);

        var rootVbox = new VBoxContainer();
        rootVbox.AddThemeConstantOverride("separation", 8);
        rootMargin.AddChild(rootVbox);

        // Title bar
        var titleBar = new HBoxContainer();
        var titleLbl = new Label();
        titleLbl.Text = "☀ Orakel — Wissen kombinieren";
        titleLbl.AddThemeFontSizeOverride("font_size", 20);
        titleLbl.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
        titleLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleBar.AddChild(titleLbl);
        var closeBtn = new Button(); closeBtn.Text = "✕ Schließen (O)";
        closeBtn.Pressed += Close;
        titleBar.AddChild(closeBtn);
        rootVbox.AddChild(titleBar);

        var subTitle = new Label();
        subTitle.Text = "Wähle Wissens-Bausteine aus dem Pool deiner Anhänger und kombiniere sie zu neuen Offenbarungen.";
        subTitle.AutowrapMode = TextServer.AutowrapMode.Word;
        subTitle.AddThemeColorOverride("font_color", new Color(0.6f,0.6f,0.6f));
        rootVbox.AddChild(subTitle);

        rootVbox.AddChild(new HSeparator());

        // Workbench
        var wbLabel = new Label(); wbLabel.Text = "🔨 Werkbank";
        wbLabel.AddThemeFontSizeOverride("font_size", 14);
        wbLabel.AddThemeColorOverride("font_color", new Color(0.9f,0.8f,0.5f));
        rootVbox.AddChild(wbLabel);
        _workbench = new HBoxContainer();
        _workbench.AddThemeConstantOverride("separation", 12);
        rootVbox.AddChild(_workbench);

        rootVbox.AddChild(new HSeparator());

        // Three columns
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rootVbox.AddChild(columns);

        // Left: pool
        var poolScroll = new ScrollContainer();
        poolScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        poolScroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        _poolBox = new VBoxContainer();
        _poolBox.AddThemeConstantOverride("separation", 3);
        _poolBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        poolScroll.AddChild(_poolBox);
        columns.AddChild(poolScroll);

        // Right: results
        var resultScroll = new ScrollContainer();
        resultScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        resultScroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        _resultBox = new VBoxContainer();
        _resultBox.AddThemeConstantOverride("separation", 6);
        _resultBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        resultScroll.AddChild(_resultBox);
        columns.AddChild(resultScroll);

        // Status bar
        _statusLabel = new Label();
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.9f,0.8f,0.4f));
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        rootVbox.AddChild(_statusLabel);
    }
}

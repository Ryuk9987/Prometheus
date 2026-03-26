#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// The Oracle UI — press O to open/close.
/// The player selects an idea to whisper to believers.
/// </summary>
public partial class OracleUI : CanvasLayer
{
    private Panel          _panel;
    private VBoxContainer  _ideaList;
    private Label          _statusLabel;
    private Label          _beliefCountLabel;
    private bool           _open = false;

    public override void _Ready()
    {
        BuildUI();
        _panel.Visible = false;

        if (OracleManager.Instance != null)
            OracleManager.Instance.Connect(
                OracleManager.SignalName.IdeaDelivered,
                Callable.From<string, int>(OnIdeaDelivered));
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.O)
        {
            _open = !_open;
            _panel.Visible = _open;
            if (_open) RefreshBelieverCount();
        }
    }

    private void BuildUI()
    {
        _panel = new Panel();
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _panel.Position = new Vector2(20, 0);
        _panel.Size = new Vector2(340, 560);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_left",   12);
        margin.AddThemeConstantOverride("margin_right",  12);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);
        _panel.AddChild(margin);

        // Title
        var title = new Label();
        title.Text = "⚡ Das Orakel";
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        title.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(title);

        _beliefCountLabel = new Label();
        _beliefCountLabel.Text = "Gläubige: ...";
        _beliefCountLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 1f));
        vbox.AddChild(_beliefCountLabel);

        var separator = new HSeparator();
        vbox.AddChild(separator);

        var hint = new Label();
        hint.Text = "Wähle eine Idee zum Einflüstern:";
        hint.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(hint);

        // Scrollable idea list
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0, 340);
        _ideaList = new VBoxContainer();
        scroll.AddChild(_ideaList);
        vbox.AddChild(scroll);

        // Status
        _statusLabel = new Label();
        _statusLabel.Text = "";
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(_statusLabel);

        var closeHint = new Label();
        closeHint.Text = "[O] Schließen";
        closeHint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        vbox.AddChild(closeHint);

        AddChild(_panel);
        PopulateIdeas();
    }

    private void PopulateIdeas()
    {
        foreach (var idea in OracleManager.Ideas.Values)
        {
            var btn = new Button();
            btn.Text = $"{idea.DisplayName}";
            btn.TooltipText = idea.Flavor;
            btn.Pressed += () => OnIdeaSelected(idea.Id, idea.Flavor);
            _ideaList.AddChild(btn);
        }
    }

    private void OnIdeaSelected(string ideaId, string flavor)
    {
        if (OracleManager.Instance == null) return;
        int reached = OracleManager.Instance.DeliverIdea(ideaId);
        _statusLabel.Text = reached > 0
            ? $"✓ {reached} Gläubige empfingen die Idee.\n\"{flavor}\""
            : "✗ Niemand hört die Stimme des Orakels.\nErhöhe den Glauben zuerst.";
        RefreshBelieverCount();
    }

    private void OnIdeaDelivered(string ideaId, int count)
    {
        // Could animate / flash here later
    }

    private void RefreshBelieverCount()
    {
        if (GameManager.Instance == null) return;
        int believers = 0;
        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            var b = npc.GetNodeOrNull<BeliefComponent>("BeliefComponent");
            if (b != null && b.CanHearOracle) believers++;
        }
        _beliefCountLabel.Text = $"Gläubige: {believers} / {GameManager.Instance.AllNpcs.Count}";
    }
}

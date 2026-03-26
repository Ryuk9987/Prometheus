#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Debug overlay (TAB to toggle).
/// Click an NPC name to follow with camera. Click again to stop.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private Panel          _panel;
    private VBoxContainer  _npcList;
    private Label          _headerLabel;
    private bool           _visible   = true;
    private NpcEntity      _followed  = null;

    public override void _Ready()
    {
        _panel = new Panel();
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _panel.Position = new Vector2(8, 8);
        _panel.Size     = new Vector2(320, 600);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.03f, 0.04f, 0.08f, 0.82f);
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   8);
        margin.AddThemeConstantOverride("margin_right",  8);
        margin.AddThemeConstantOverride("margin_top",    6);
        margin.AddThemeConstantOverride("margin_bottom", 6);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 1);

        _headerLabel = new Label();
        _headerLabel.AddThemeFontSizeOverride("font_size", 13);
        _headerLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.85f, 1f));
        vbox.AddChild(_headerLabel);
        vbox.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _npcList = new VBoxContainer();
        _npcList.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(_npcList);
        vbox.AddChild(scroll);

        margin.AddChild(vbox);
        _panel.AddChild(margin);
        AddChild(_panel);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Tab)
        {
            _visible = !_visible;
            _panel.Visible = _visible;
        }
    }

    public override void _Process(double delta)
    {
        if (!_visible || GameManager.Instance == null) return;

        int tasks   = TaskManager.Instance?.Tasks.Count ?? 0;
        int tribes  = TribeManager.Instance?.Tribes.Count ?? 0;
        string time = DayCycle.Instance != null ? $" | 🕐 {DayCycle.Instance.TimeString}" : "";
        string followHint = _followed != null ? $"  📷 {_followed.NpcName}" : "";
        _headerLabel.Text = $"PROMETHEUS — Debug{time}\nNPCs: {GameManager.Instance.AllNpcs.Count} | Tasks: {tasks} | Stämme: {tribes} | TAB=toggle{followHint}";

        // Rebuild NPC list every frame (simple approach for debug)
        foreach (Node child in _npcList.GetChildren())
            child.QueueFree();

        foreach (var npc in GameManager.Instance.AllNpcs)
            _npcList.AddChild(MakeNpcRow(npc));
    }

    private Control MakeNpcRow(NpcEntity npc)
    {
        var btn = new Button();
        btn.Flat = true;
        btn.Alignment = HorizontalAlignment.Left;
        btn.AddThemeFontSizeOverride("font_size", 12);

        bool isFollowed = _followed == npc;

        // Build rich label text manually (Button doesn't do BBCode — use RichTextLabel overlay trick)
        string hungerCol = npc.Needs.IsStarving ? "#ff4444" : npc.Needs.IsHungry ? "#ffaa22" : "#44ff88";
        string thirstCol = npc.Needs.Thirst >= 0.95f ? "#ff4444" : npc.Needs.IsThirsty ? "#ffaa22" : "#44ff88";
        string beliefCol = npc.Belief.Belief >= 0.3f ? "#44ccff" : "#888888";
        string taskMark  = npc.Cooperation.HasTask ? "⚔" : "";
        string followMark = isFollowed ? " 📷" : "";

        var rtl = new RichTextLabel();
        rtl.BbcodeEnabled = true;
        rtl.FitContent   = true;
        rtl.CustomMinimumSize = new Vector2(300, 16);
        rtl.MouseFilter  = Control.MouseFilterEnum.Ignore;
        rtl.AddThemeFontSizeOverride("normal_font_size", 12);

        string kb = "";
        foreach (var k in npc.Knowledge.Knowledge.Values)
        {
            string kc = k.Depth > 0.6f ? "green" : k.Depth > 0.3f ? "orange" : "red";
            kb += $" [color={kc}]{k.Id}({k.Depth:F1})[/color]";
        }

        rtl.Text =
            $"[color=yellow]{npc.NpcName}[/color][color=gray]({npc.Cooperation.Role}){taskMark}[/color]{followMark}" +
            $"  [color={hungerCol}]H:{npc.Needs.Hunger:F1}[/color]" +
            $" [color={thirstCol}]T:{npc.Needs.Thirst:F1}[/color]" +
            $" [color={beliefCol}]B:{npc.Belief.Belief:F1}[/color]" +
            kb;

        // Transparent button as click area
        var clickArea = new Button();
        clickArea.Flat = true;
        clickArea.CustomMinimumSize = new Vector2(300, 18);
        var captured = npc;
        clickArea.Pressed += () => ToggleFollow(captured);

        // Overlay: rtl on top of clickArea
        var container = new Control();
        container.CustomMinimumSize = new Vector2(300, 18);
        container.AddChild(clickArea);
        rtl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rtl.MouseFilter = Control.MouseFilterEnum.Ignore;
        container.AddChild(rtl);

        // Highlight followed
        if (isFollowed)
        {
            var highlight = new ColorRect();
            highlight.Color = new Color(0.3f, 0.6f, 1f, 0.12f);
            highlight.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
            container.AddChild(highlight);
        }

        return container;
    }

    private void ToggleFollow(NpcEntity npc)
    {
        if (_followed == npc)
        {
            _followed = null;
            CameraFollow.Instance?.StopFollow();
        }
        else
        {
            _followed = npc;
            CameraFollow.Instance?.Follow(npc);
        }
    }
}

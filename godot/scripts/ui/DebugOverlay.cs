#nullable disable
using Godot;

/// <summary>
/// Simple debug overlay — press TAB to toggle.
/// Shows knowledge spread across all NPCs in real time.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private RichTextLabel _label;
    private bool _visible = true;

    public override void _Ready()
    {
        _label = new RichTextLabel();
        _label.BbcodeEnabled = true;
        _label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _label.Position = new Vector2(10, 10);
        _label.Size = new Vector2(420, 500);
        _label.AddThemeColorOverride("default_color", new Color(1, 1, 1, 0.9f));
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("ui_focus_next")) // TAB
        {
            _visible = !_visible;
            _label.Visible = _visible;
        }

        if (!_visible || GameManager.Instance == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[b]PROMETHEUS — Knowledge Spread[/b]");
        sb.AppendLine($"[color=gray]NPCs: {GameManager.Instance.AllNpcs.Count} | TAB = toggle[/color]");
        sb.AppendLine("─────────────────────────────");

        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            var knows = npc.Knowledge.Knowledge;
            if (knows.Count == 0) continue;

            sb.Append($"[color=yellow]{npc.NpcName}[/color] ");
            foreach (var k in knows.Values)
            {
                string color = k.Depth > 0.7f ? "green" : k.Depth > 0.3f ? "orange" : "red";
                sb.Append($"[color={color}]{k.Id}({k.Depth:F1})[/color] ");
            }
            sb.AppendLine();
        }

        _label.Text = sb.ToString();
    }
}

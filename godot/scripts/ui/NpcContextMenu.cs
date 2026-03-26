#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Click an NPC in the 3D world → context menu appears near them.
/// Options depend on what the NPC knows.
/// E.g. "Fertige Axt" if they know tools+rope.
/// </summary>
public partial class NpcContextMenu : CanvasLayer
{
    public static NpcContextMenu Instance { get; private set; }

    private Panel     _panel;
    private VBoxContainer _list;
    private Label     _npcNameLabel;
    private NpcEntity _targetNpc;
    private bool      _visible = false;

    public override void _Ready()
    {
        Instance = this;
        BuildUI();
        _panel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && k.Keycode == Key.Escape && _visible)
        {
            Hide();
            return;
        }

        // Right-click in world → try to select NPC
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            if (_visible) { Hide(); return; }
            TrySelectNpc(mb.Position);
        }
    }

    private void TrySelectNpc(Vector2 screenPos)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null || GameManager.Instance == null) return;

        var origin = camera.ProjectRayOrigin(screenPos);
        var dir    = camera.ProjectRayNormal(screenPos);

        NpcEntity closest = null;
        float closestDist = 3f; // max screen-space distance in world units

        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            // Project NPC position to screen
            var screenNpc = camera.UnprojectPosition(npc.GlobalPosition);
            float dist = screenPos.DistanceTo(screenNpc);
            if (dist < closestDist * 30f) // pixel threshold
            {
                // Also check world distance for depth
                float worldDist = (npc.GlobalPosition - origin).Length();
                if (worldDist < 80f) { closest = npc; closestDist = dist; }
            }
        }

        if (closest != null) ShowFor(closest, screenPos);
    }

    public void ShowFor(NpcEntity npc, Vector2 screenPos)
    {
        _targetNpc = npc;
        _visible   = true;

        _npcNameLabel.Text = $"{npc.NpcName}  [{npc.Cooperation.Role}]";

        // Clear old entries
        foreach (Node c in _list.GetChildren()) c.QueueFree();

        // Camera follow
        var followBtn = MakeBtn($"📷 Kamera folgen", () => {
            CameraFollow.Instance?.Follow(npc);
            Hide();
        });
        _list.AddChild(followBtn);

        // Show knowledge
        var kLabel = new Label();
        kLabel.Text = "── Wissen ──";
        kLabel.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
        kLabel.AddThemeFontSizeOverride("font_size", 11);
        _list.AddChild(kLabel);

        foreach (var k in npc.Knowledge.Knowledge.Values.OrderByDescending(k => k.Depth))
        {
            var def = KnowledgeCatalog.Get(k.Id);
            string icon = def?.Icon ?? "•";
            string depthBar = new string('█', (int)(k.Depth * 5)) + new string('░', 5 - (int)(k.Depth * 5));
            var lbl = new Label();
            lbl.Text = $"  {icon} {k.Id}  [{depthBar}] {k.Depth:F2}";
            lbl.AddThemeFontSizeOverride("font_size", 11);
            lbl.AddThemeColorOverride("font_color",
                k.Depth > 0.6f ? new Color(0.3f,1f,0.4f) :
                k.Depth > 0.3f ? new Color(1f,0.8f,0.3f) :
                                  new Color(0.6f,0.6f,0.6f));
            _list.AddChild(lbl);
        }

        // Craftable items this NPC can make
        var craftable = GetCraftableByNpc(npc);
        if (craftable.Count > 0)
        {
            var sep = new HSeparator(); _list.AddChild(sep);
            var cHeader = new Label();
            cHeader.Text = "── Auftrag erteilen ──";
            cHeader.AddThemeColorOverride("font_color", new Color(0.9f,0.7f,0.3f));
            cHeader.AddThemeFontSizeOverride("font_size", 12);
            _list.AddChild(cHeader);

            foreach (var def in craftable)
            {
                string mats = string.Join(" ", def.Materials.Select(m => $"{m.Amount:F0}x{ResourceLabel(m.Resource)}"));
                var btn = MakeBtn($"{def.Icon} {def.DisplayName}  {mats}", () => {
                    IssueCraft(npc, def);
                    Hide();
                });
                _list.AddChild(btn);
            }
        }

        // Position panel near NPC screen position
        _panel.Position = new Vector2(
            Mathf.Clamp(screenPos.X + 10, 0, GetViewport().GetVisibleRect().Size.X - 260),
            Mathf.Clamp(screenPos.Y - 20, 0, GetViewport().GetVisibleRect().Size.Y - 400));
        _panel.Visible = true;
    }

    private List<KnowledgeDefinition> GetCraftableByNpc(NpcEntity npc)
    {
        var result = new List<KnowledgeDefinition>();
        foreach (var def in KnowledgeCatalog.GetByCategory(KnowledgeCategory.Tool))
        {
            if (!npc.Knowledge.Knows(def.Id)) continue;
            if (npc.Knowledge.Knowledge[def.Id].Depth < def.MinDepth) continue;
            result.Add(def);
        }
        return result;
    }

    private void IssueCraft(NpcEntity npc, KnowledgeDefinition def)
    {
        npc.Knowledge.Verify(def.Id, 0.1f);
        GD.Print($"[NpcContext] {npc.NpcName} ordered to craft: {def.DisplayName}");
        // TODO: create actual item in future iteration
    }

    public new void Hide()
    {
        _visible = false;
        _panel.Visible = false;
        _targetNpc = null;
    }

    private void BuildUI()
    {
        _panel = new Panel();
        _panel.Size = new Vector2(250, 400);
        var style = new StyleBoxFlat();
        style.BgColor     = new Color(0.06f, 0.08f, 0.14f, 0.96f);
        style.BorderColor  = new Color(0.5f, 0.8f, 0.4f);
        style.BorderWidthBottom = style.BorderWidthTop =
        style.BorderWidthLeft   = style.BorderWidthRight = 2;
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var s in new[]{"left","right","top","bottom"})
            margin.AddThemeConstantOverride($"margin_{s}", 8);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);

        _npcNameLabel = new Label();
        _npcNameLabel.AddThemeFontSizeOverride("font_size", 15);
        _npcNameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
        vbox.AddChild(_npcNameLabel);
        vbox.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_list);
        vbox.AddChild(scroll);

        vbox.AddChild(MakeBtn("✕ Schließen", Hide));

        margin.AddChild(vbox);
        _panel.AddChild(margin);
        AddChild(_panel);
    }

    private static Button MakeBtn(string text, System.Action onPress)
    {
        var b = new Button(); b.Text = text; b.Flat = false;
        b.AddThemeFontSizeOverride("font_size", 12);
        if (onPress != null) b.Pressed += onPress;
        return b;
    }

    private static string ResourceLabel(ResourceType r) => r switch {
        ResourceType.Wood  => "Holz", ResourceType.Stone => "Stein",
        ResourceType.Food  => "Nahrung", ResourceType.Water => "Wasser",
        _ => r.ToString()
    };
}

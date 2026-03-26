using Godot;

/// <summary>
/// Core NPC entity. Each individual is unique — their own knowledge,
/// personality, needs. They live, learn, teach, and die.
/// </summary>
public partial class NpcEntity : Node3D
{
    [Export] public string NpcName    { get; set; } = "Unknown";
    [Export] public int    Age        { get; set; } = 20;
    [Export] public string TribeId   { get; set; } = "tribe_a";
    [Export] public float  BeliefScore { get; set; } = 0.0f; // 0=no faith, 1=devout

    public PersonalityComponent Personality { get; private set; }
    public NeedsComponent       Needs       { get; private set; }
    public KnowledgeComponent   Knowledge   { get; private set; }

    private NavigationAgent3D  _navAgent;
    private Label3D            _nameLabel;
    private RandomNumberGenerator _rng = new();

    private double _wanderTimer   = 0;
    private const double WanderInterval = 3.5;
    private const float  WanderRadius   = 10f;
    private const float  MoveSpeed      = 2.5f;

    public override void _Ready()
    {
        Personality = GetNode<PersonalityComponent>("PersonalityComponent");
        Needs       = GetNode<NeedsComponent>("NeedsComponent");
        Knowledge   = GetNode<KnowledgeComponent>("KnowledgeComponent");
        _navAgent   = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _nameLabel  = GetNode<Label3D>("Label3D");

        _nameLabel.Text = NpcName;

        _rng.Randomize();
        Personality.Randomize(_rng);

        // Register with GameManager and subscribe to world tick
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterNpc(this);
            GameManager.Instance.Connect(
                GameManager.SignalName.WorldTick,
                Callable.From<double>(OnWorldTick));
        }

        SetRandomWanderTarget();
    }

    public override void _Process(double delta)
    {
        // Move toward nav target
        if (!_navAgent.IsNavigationFinished())
        {
            var next = _navAgent.GetNextPathPosition();
            var dir  = (next - GlobalPosition).Normalized();
            GlobalPosition += dir * MoveSpeed * (float)delta;
        }

        // Pick new wander target periodically
        _wanderTimer += delta;
        if (_wanderTimer >= WanderInterval * (1.5f - Personality.Curiosity))
        {
            _wanderTimer = 0;
            SetRandomWanderTarget();
        }
    }

    private void OnWorldTick(double delta)
    {
        Needs.OnWorldTick(delta);
        Age++; // crude aging (tick = ~1 year in prototype)
    }

    private void SetRandomWanderTarget()
    {
        var offset = new Vector3(
            _rng.RandfRange(-WanderRadius, WanderRadius), 0,
            _rng.RandfRange(-WanderRadius, WanderRadius));
        _navAgent.TargetPosition = GlobalPosition + offset;
    }

    public override void _ExitTree()
    {
        GameManager.Instance?.UnregisterNpc(this);
    }
}

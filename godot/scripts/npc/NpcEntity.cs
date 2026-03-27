#nullable disable
using Godot;
using System.Collections.Generic;

public partial class NpcEntity : Node3D
{
    [Export] public string NpcName     { get; set; } = "Unknown";
    [Export] public int    Age         { get; set; } = 20;
    [Export] public string TribeId     { get; set; } = "tribe_a";
    [Export] public float  BeliefScore { get; set; } = 0.0f;

    // Set before adding to tree — list of (id, depth, confidence) applied in _Ready()
    public List<(string id, float depth, float conf)> StarterKnowledge { get; set; } = new();

    // Legacy single-entry support (kept for compatibility)
    public string StarterKnowledgeId         { get; set; } = "";
    public float  StarterKnowledgeDepth      { get; set; } = 0.0f;
    public float  StarterKnowledgeConfidence { get; set; } = 0.0f;

    public PersonalityComponent Personality { get; private set; }
    public NeedsComponent       Needs       { get; private set; }
    public KnowledgeComponent   Knowledge   { get; private set; }
    public SocialComponent      Social      { get; private set; }
    public BeliefComponent      Belief      { get; private set; }
    public SurvivalBehavior      Survival        { get; private set; }
    public TabletSeekBehavior    TabletSeek      { get; private set; }
    public CooperationComponent  Cooperation     { get; private set; }
    public CampfireBehavior      CampfireBuilder   { get; private set; }
    public BuildWorkerBehavior   BuildWorker        { get; private set; }
    public LeaderBehavior        LeaderPlanning     { get; private set; }
    public ForagingBehavior      Foraging           { get; private set; }
    public NpcInventory          Inventory          { get; private set; }
    public WellbeingComponent    Wellbeing          { get; private set; }
    public SocialRole            SocialRole         { get; set; } = SocialRole.Unassigned;

    private Label3D               _nameLabel;
    private RandomNumberGenerator _rng = new();

    private Vector3 _wanderTarget;
    private double  _wanderTimer   = 0;
    private const double WanderInterval = 3.5;
    private const float  WanderRadius   = 15f;
    private const float  MoveSpeed      = 2.5f;

    // Tribe coordination
    public Vector3 TribeCenterHint    { get; set; }
    public bool    ShouldRallyToTribe { get; set; } = false;

    public override void _Ready()
    {
        Personality = GetNode<PersonalityComponent>("PersonalityComponent");
        Needs       = GetNode<NeedsComponent>("NeedsComponent");
        Knowledge   = GetNode<KnowledgeComponent>("KnowledgeComponent");
        Social      = GetNode<SocialComponent>("SocialComponent");
        Belief      = GetNode<BeliefComponent>("BeliefComponent");
        Survival        = GetNode<SurvivalBehavior>("SurvivalBehavior");
        TabletSeek      = GetNode<TabletSeekBehavior>("TabletSeekBehavior");
        Cooperation     = GetNode<CooperationComponent>("CooperationComponent");
        CampfireBuilder = GetNode<CampfireBehavior>("CampfireBehavior");
        BuildWorker     = GetNode<BuildWorkerBehavior>("BuildWorkerBehavior");
        LeaderPlanning  = GetNode<LeaderBehavior>("LeaderBehavior");
        Foraging        = GetNode<ForagingBehavior>("ForagingBehavior");
        Inventory       = GetNode<NpcInventory>("NpcInventory");
        Wellbeing       = GetNode<WellbeingComponent>("WellbeingComponent");
        _nameLabel   = GetNode<Label3D>("Label3D");

        _nameLabel.Text = NpcName;
        _wanderTarget   = GlobalPosition;

        _rng.Randomize();
        Personality.Randomize(_rng);
        Belief.Belief = BeliefScore;

        // Apply starter knowledge list (new system)
        foreach (var (id, depth, conf) in StarterKnowledge)
            Knowledge.Learn(id, depth, conf, "innate");

        // Legacy single-entry support
        if (StarterKnowledgeId != "")
            Knowledge.Learn(StarterKnowledgeId, StarterKnowledgeDepth, StarterKnowledgeConfidence, "oracle");

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
        // Priority 1: Survival (hunger/thirst critical — always)
        if (Survival.Tick(delta)) return;

        // Priority 2: Oracle Tablet (believers seek knowledge)
        if (TabletSeek.Tick(delta)) return;

        // Priority 3: Tend campfires (any NPC that knows fire)
        if (CampfireBuilder.Tick(delta)) return;

        // Priority 4–7: Role-based behavior
        switch (SocialRole)
        {
            case SocialRole.Builder:
                // Builders focus on construction first, then gather materials
                if (BuildWorker.Tick(delta)) return;
                if (Foraging.Tick(delta)) return;
                break;

            case SocialRole.Hunter:
                // Hunters focus on cooperative hunting
                if (Cooperation.Tick(delta)) return;
                break;

            case SocialRole.Gatherer:
                // Gatherers focus on foraging
                if (Foraging.Tick(delta)) return;
                if (Cooperation.Tick(delta)) return;
                break;

            case SocialRole.Farmer:
                // Farmers tend fields (cooperation task type = farm)
                if (Cooperation.Tick(delta)) return;
                if (Foraging.Tick(delta)) return;
                break;

            case SocialRole.Healer:
                // Healers stay near camp, gather herbs
                if (Foraging.Tick(delta)) return;
                break;

            case SocialRole.Leader:
                // Leader plans settlement, then cooperates/forages
                LeaderPlanning.Tick(delta);
                if (Cooperation.Tick(delta)) return;
                if (Foraging.Tick(delta)) return;
                break;

            default:
                // Unassigned: forage → build → coop
                if (Foraging.Tick(delta)) return;
                if (BuildWorker.Tick(delta)) return;
                if (Cooperation.Tick(delta)) return;
                break;
        }

        // Priority 5: Rally to tribe at night
        if (ShouldRallyToTribe)
        {
            var rallyDir = TribeCenterHint - GlobalPosition; rallyDir.Y = 0;
            if (rallyDir.Length() > 5f)
            {
                GlobalPosition += rallyDir.Normalized() * MoveSpeed * 0.6f * (float)delta;
                return;
            }
        }

        // Priority 6: Wander
        var dir = _wanderTarget - GlobalPosition;
        dir.Y = 0;
        if (dir.Length() > 0.6f)
            GlobalPosition += dir.Normalized() * MoveSpeed * (float)delta;

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
        Belief.OnWorldTick(delta);
        Social.OnWorldTick(delta);
    }

    private void SetRandomWanderTarget()
    {
        _wanderTarget = GlobalPosition + new Vector3(
            _rng.RandfRange(-WanderRadius, WanderRadius), 0,
            _rng.RandfRange(-WanderRadius, WanderRadius));
    }

    public override void _ExitTree()
    {
        GameManager.Instance?.UnregisterNpc(this);
    }
}

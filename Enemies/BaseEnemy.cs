using Godot;
using System;

public partial class BaseEnemy : CharacterBody2D
{
    // ==========================================
    // CÁC THÔNG SỐ TÙY CHỈNH (INSPECTOR)
    // ==========================================
    [ExportGroup("Máu & Sát Thương Nhận Vào")]
    [Export] public int MaxHits { get; set; } = 3;                 // Số lần chạm / bị đánh trúng để die (tùy chỉnh được)
    [Export] public float InvincibleDuration { get; set; } = 0.5f; // Thời gian bất tử chớp nhoáng sau mỗi hit

    [ExportGroup("Tốc Độ & Di Chuyển")]
    [Export] public float PatrolSpeed { get; set; } = 100.0f;      // Tốc độ đi tuần tra tự nhiên
    [Export] public float ChaseSpeed { get; set; } = 220.0f;       // Tốc độ khi đuổi theo người chơi
    [Export] public float Gravity { get; set; } = 980.0f;
    [Export] public float ChaseMaxDuration { get; set; } = 10.0f;  // Dí tối đa khoảng 10 giây thì dừng

    [ExportGroup("Nhảy Vượt Chướng Ngại Vật")]
    [Export] public bool CanJumpObstacle { get; set; } = true;     // Cho phép nhảy khi gặp vật cản lúc dí
    [Export] public float JumpVelocity { get; set; } = -420.0f;    // Lực nhảy qua vật cản
    [Export] public float JumpCooldown { get; set; } = 0.4f;       // Khoảng cách giữa 2 lần nhảy tránh spam

    [ExportGroup("Tấn Công & Hồi Chiêu")]
    [Export] public float AttackCooldown { get; set; } = 1.5f;     // Hồi chiêu đòn đánh (mặc định 1.5s, có thể chỉnh trên Inspector)
    [Export] public float AttackDuration { get; set; } = 0.6f;     // Thời gian thực hiện hoạt ảnh đòn đánh

    [ExportGroup("Thời Gian Đi Tuần Tra Tự Nhiên")]
    [Export] public float MinIdleTime { get; set; } = 1.0f;        // Thời gian đứng yên nghỉ ngơi tối thiểu
    [Export] public float MaxIdleTime { get; set; } = 3.0f;        // Tối đa
    [Export] public float MinWalkTime { get; set; } = 2.0f;        // Thời gian đi dạo tối thiểu
    [Export] public float MaxWalkTime { get; set; } = 5.0f;        // Tối đa

    // ==========================================
    // TRẠNG THÁI ENEMY
    // ==========================================
    public enum EnemyState
    {
        PatrolWalk,
        PatrolIdle,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    public EnemyState CurrentState { get; protected set; } = EnemyState.PatrolIdle;

    protected int _currentHits = 0;
    protected float _invincibleTimer = 0f;
    protected float _chaseTimer = 0f;
    protected float _stateTimer = 0f;
    protected float _attackCooldownTimer = 0f;
    protected float _jumpCooldownTimer = 0f;
    protected int _patrolDirection = 1; // 1: phải, -1: trái
    protected Node2D _targetPlayer = null;

    protected AnimatedSprite2D _animSprite;
    protected Area2D _sightArea;
    protected Area2D _attackArea;
    protected RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        _rng.Randomize();

        // Quái ở Layer 3 (giá trị 4)
        CollisionLayer = 4;

        // Quái chỉ va chạm vật lý với Layer 1 (Sàn/Tường), KHÔNG va chạm với Player (Layer 2)
        // Nên quái và người chơi đi xuyên qua nhau hoàn toàn!
        CollisionMask = 1;

        // Quái nổi trên Portal (ZIndex = 0) và dưới Player (ZIndex = 10)
        ZIndex = 5;

        // Từ tính bám sàn (Floor Snapping) giúp quái di chuyển đầm, không bị nảy trên dốc
        FloorSnapLength = 10.0f;
        FloorStopOnSlope = true;
        FloorConstantSpeed = true;

        // Đảm bảo quái bỏ qua va chạm vật lý với toàn bộ nhân vật người chơi
        foreach (Node node in GetTree().GetNodesInGroup("Player"))
        {
            if (node is CollisionObject2D col)
            {
                AddCollisionExceptionWith(col);
            }
        }

        // Tìm AnimatedSprite2D (trong Container hoặc trực tiếp con của root)
        _animSprite = GetNodeOrNull<AnimatedSprite2D>("Container/AnimatedSprite2D")
                   ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        // Tìm Sight và AttackArea
        _sightArea = GetNodeOrNull<Area2D>("Sight")
                  ?? GetNodeOrNull<Area2D>("SightArea");
        _attackArea = GetNodeOrNull<Area2D>("AttackArea")
                   ?? GetNodeOrNull<Area2D>("Attack");

        // ĐẶC BIỆT: Cài đặt CollisionMask cho Sight và AttackArea để quét cả Layer 1 và 2 (Mask = 3)
        // Đảm bảo không bao giờ bị lệch layer khiến quái không nhìn thấy người chơi!
        if (_sightArea != null)
        {
            _sightArea.CollisionMask = 3; // Quét cả Layer 1 và Layer 2
            _sightArea.BodyEntered += OnSightBodyEntered;
            _sightArea.BodyExited += OnSightBodyExited;
        }

        if (_attackArea != null)
        {
            _attackArea.CollisionMask = 3; // Quét cả Layer 1 và Layer 2
            _attackArea.BodyEntered += OnAttackAreaBodyEntered;
            _attackArea.BodyExited += OnAttackAreaBodyExited;
        }

        EnterPatrolIdle();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 velocity = Velocity;

        // Cập nhật các bộ đếm thời gian
        if (_invincibleTimer > 0f) _invincibleTimer -= dt;
        if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;
        if (_jumpCooldownTimer > 0f) _jumpCooldownTimer -= dt;

        // Trọng lực và ma sát tường nếu đang trên không
        if (!IsOnFloor())
        {
            velocity.Y += Gravity * dt;

            // Ma sát trượt vật cản / tường khi quái rơi
            if (IsOnWall() && velocity.Y > 0)
            {
                velocity.Y = Mathf.MoveToward(velocity.Y, 150.0f, 600.0f * dt);
            }
        }

        if (CurrentState == EnemyState.Dead)
        {
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        // Xử lý từng trạng thái
        switch (CurrentState)
        {
            case EnemyState.PatrolIdle:
                HandlePatrolIdleState(dt, ref velocity);
                break;

            case EnemyState.PatrolWalk:
                HandlePatrolWalkState(dt, ref velocity);
                break;

            case EnemyState.Chase:
                HandleChaseState(dt, ref velocity);
                break;

            case EnemyState.Attack:
                HandleAttackState(dt, ref velocity);
                break;

            case EnemyState.Hurt:
                HandleHurtState(dt, ref velocity);
                break;
        }

        Velocity = velocity;
        MoveAndSlide();

        UpdateVisuals();
    }

    // ==========================================
    // LOGIC CÁC TRẠNG THÁI
    // ==========================================

    private void HandlePatrolIdleState(float dt, ref Vector2 velocity)
    {
        velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * 4f * dt);
        _stateTimer -= dt;

        if (_stateTimer <= 0f)
        {
            _patrolDirection = _rng.RandiRange(0, 1) == 0 ? -1 : 1;
            EnterPatrolWalk();
        }
    }

    private void HandlePatrolWalkState(float dt, ref Vector2 velocity)
    {
        velocity.X = _patrolDirection * PatrolSpeed;
        _stateTimer -= dt;

        if (IsOnWall() || _stateTimer <= 0f)
        {
            if (IsOnWall())
            {
                _patrolDirection *= -1;
            }
            EnterPatrolIdle();
        }
    }

    private void HandleChaseState(float dt, ref Vector2 velocity)
    {
        if (_targetPlayer == null || !IsInstanceValid(_targetPlayer))
        {
            EnterPatrolIdle();
            return;
        }

        _chaseTimer += dt;

        if (_chaseTimer >= ChaseMaxDuration)
        {
            _targetPlayer = null;
            EnterPatrolIdle();
            return;
        }

        float diffX = _targetPlayer.GlobalPosition.X - GlobalPosition.X;
        float distToPlayer = Mathf.Abs(diffX);
        int chaseDir = diffX > 0 ? 1 : -1;

        if (distToPlayer < 40f && _attackCooldownTimer <= 0f)
        {
            velocity.X = 0;
            EnterAttack();
            return;
        }

        velocity.X = chaseDir * ChaseSpeed;
        _patrolDirection = chaseDir;

        if (CanJumpObstacle && IsOnFloor() && _jumpCooldownTimer <= 0f)
        {
            bool hitWall = IsOnWall();
            bool playerIsAbove = (_targetPlayer.GlobalPosition.Y < GlobalPosition.Y - 25.0f) && (distToPlayer < 120.0f);

            if (hitWall || playerIsAbove)
            {
                velocity.Y = JumpVelocity;
                _jumpCooldownTimer = JumpCooldown;
            }
        }
    }

    private void HandleAttackState(float dt, ref Vector2 velocity)
    {
        velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * 4f * dt);
        _stateTimer -= dt;

        if (_stateTimer <= 0f)
        {
            _attackCooldownTimer = AttackCooldown;

            if (_targetPlayer != null && IsInstanceValid(_targetPlayer) && _chaseTimer < ChaseMaxDuration)
            {
                CurrentState = EnemyState.Chase;
            }
            else
            {
                EnterPatrolIdle();
            }
        }
    }

    private void HandleHurtState(float dt, ref Vector2 velocity)
    {
        velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * 4f * dt);
        _stateTimer -= dt;

        if (_stateTimer <= 0f)
        {
            if (_targetPlayer != null && IsInstanceValid(_targetPlayer))
            {
                CurrentState = EnemyState.Chase;
            }
            else
            {
                EnterPatrolIdle();
            }
        }
    }

    protected void EnterPatrolIdle()
    {
        CurrentState = EnemyState.PatrolIdle;
        _stateTimer = _rng.RandfRange(MinIdleTime, MaxIdleTime);
    }

    protected void EnterPatrolWalk()
    {
        CurrentState = EnemyState.PatrolWalk;
        _stateTimer = _rng.RandfRange(MinWalkTime, MaxWalkTime);
    }

    protected void EnterAttack()
    {
        CurrentState = EnemyState.Attack;
        _stateTimer = AttackDuration;
    }

    public virtual void TakeDamage(int amount = 1)
    {
        if (_invincibleTimer > 0f || CurrentState == EnemyState.Dead) return;

        _currentHits += amount;
        _invincibleTimer = InvincibleDuration;

        if (_currentHits >= MaxHits)
        {
            Die();
        }
        else
        {
            CurrentState = EnemyState.Hurt;
            _stateTimer = 0.4f;
            PlayAnimationSafe("hurt_critical");
        }
    }

    protected virtual void Die()
    {
        CurrentState = EnemyState.Dead;
        PlayAnimationSafe("hurt_critical");

        var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (col != null) col.SetDeferred("disabled", true);

        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public virtual void OnSightBodyEntered(Node2D body)
    {
        // Nhận diện Player qua Group hoặc kiểu BaseCharacter
        if (body.IsInGroup("Player") || body is BaseCharacter)
        {
            _targetPlayer = body;
            _chaseTimer = 0f;
            CurrentState = EnemyState.Chase;

            if (body is CollisionObject2D col)
            {
                AddCollisionExceptionWith(col);
            }
        }
    }

    public virtual void OnSightBodyExited(Node2D body)
    {
    }

    public virtual void OnAttackAreaBodyEntered(Node2D body)
    {
        if (body.IsInGroup("Player") || body is BaseCharacter)
        {
            if (_attackCooldownTimer <= 0f)
            {
                EnterAttack();
            }
        }
    }

    public virtual void OnAttackAreaBodyExited(Node2D body)
    {
    }

    protected virtual void UpdateVisuals()
    {
        if (_animSprite == null) return;

        if (Mathf.Abs(Velocity.X) > 1.0f)
        {
            _animSprite.FlipH = (Velocity.X < 0);
        }
        else if (_targetPlayer != null && CurrentState == EnemyState.Attack)
        {
            _animSprite.FlipH = (_targetPlayer.GlobalPosition.X < GlobalPosition.X);
        }

        switch (CurrentState)
        {
            case EnemyState.PatrolIdle:
                PlayAnimationSafe("idle");
                break;

            case EnemyState.PatrolWalk:
                PlayAnimationSafe("move");
                break;

            case EnemyState.Chase:
                PlayAnimationSafe("move");
                break;

            case EnemyState.Attack:
                PlayAnimationSafe("attack");
                break;

            case EnemyState.Hurt:
            case EnemyState.Dead:
                PlayAnimationSafe("hurt_critical");
                break;
        }
    }

    protected void PlayAnimationSafe(string animName)
    {
        if (_animSprite != null && _animSprite.SpriteFrames != null && _animSprite.SpriteFrames.HasAnimation(animName))
        {
            if (_animSprite.Animation != animName || !_animSprite.IsPlaying())
            {
                _animSprite.Play(animName);
            }
        }
    }
}

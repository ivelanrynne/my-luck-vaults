using Godot;
using System;

/// <summary>
/// Class quái cơ bản (BaseEnemy).
/// Đóng vai trò là nền tảng AI cho tất cả quái vật trong game:
/// - Tự động tuần tra (Patrol Walk / Idle).
/// - Tự động phát hiện người chơi khi bước vào tầm nhìn (Sight Area), hỗ trợ cả quét liên tục để chống sót mục tiêu khi vừa spawn hoặc đổi nhân vật (Tab).
/// - Đuổi theo (Chase) và tấn công (Attack) khi tiếp cận người chơi.
/// - Hỗ trợ nhảy vượt chướng ngại vật cho quái đi bộ.
/// - TÍNH NĂNG BAY (Flying Enemy): Dễ dàng biến quái thành quái bay chỉ với 1 dòng code trong file .cs cá nhân hoặc tích chọn trên Inspector.
/// </summary>
public partial class BaseEnemy : CharacterBody2D
{
    // ==========================================
    // CÁC THÔNG SỐ TÙY CHỈNH (INSPECTOR)
    // ==========================================
    [ExportGroup("Máu & Sát Thương Nhận Vào")]
    [Export] public int MaxHits { get; set; } = 3;                 // Số lần chạm / bị đánh trúng để die (tùy chỉnh được)
    [Export] public float InvincibleDuration { get; set; } = 0.5f; // Thời gian bất tử chớp nhoáng sau mỗi hit

    [ExportGroup("Tốc Độ & Di Chuyển Mặt Đất")]
    [Export] public float PatrolSpeed { get; set; } = 100.0f;      // Tốc độ đi tuần tra tự nhiên
    [Export] public float ChaseSpeed { get; set; } = 220.0f;       // Tốc độ khi đuổi theo người chơi
    [Export] public float Gravity { get; set; } = 980.0f;
    [Export] public float ChaseMaxDuration { get; set; } = 10.0f;  // Dí tối đa khoảng 10 giây trước khi nghỉ lấy hơi

    [ExportGroup("Chế Độ Quái Bay (Flying Enemy Mode)")]
    // Đánh dấu quái có khả năng bay lượn tự do hay không
    // (Bật true trên Inspector hoặc gán IsFlyingEnemy = true trong file .cs của quái)
    [Export] public bool IsFlyingEnemy { get; set; } = false;
    // Tốc độ bay tuần tra
    [Export] public float FlyingSpeed { get; set; } = 160.0f;
    // Tốc độ bay khi dí theo người chơi
    [Export] public float FlyingChaseSpeed { get; set; } = 230.0f;
    // Tần số lượn sóng tự nhiên trục Y (nhấp nhô khi bay)
    [Export] public float FlyWobbleFrequency { get; set; } = 2.5f;
    // Biên độ lượn sóng trục Y (pixels)
    [Export] public float FlyWobbleAmplitude { get; set; } = 22.0f;

    [ExportGroup("Nhảy Vượt Chướng Ngại Vật (Quái Đi Bộ)")]
    [Export] public bool CanJumpObstacle { get; set; } = true;     // Cho phép nhảy khi gặp vật cản lúc dí
    [Export] public float JumpVelocity { get; set; } = -520.0f;    // Lực nhảy qua vật cản (âm = lên)
    [Export] public float JumpCooldown { get; set; } = 0.8f;       // Thời gian hồi giữa 2 lần nhảy
    [Export] public float JumpTriggerHeight { get; set; } = 40.0f; // Player cao hơn quái bao nhiêu px thì nhảy
    [Export] public float JumpTriggerRange { get; set; } = 200.0f; // Khoảng cách ngang tối đa để kích hoạt nhảy vượt cao

    [ExportGroup("Tấn Công & Tầm Đánh")]
    [Export] public float AttackRange { get; set; } = 55.0f;       // Khoảng cách 2D để tung đòn đánh
    [Export] public float AttackCooldown { get; set; } = 1.5f;     // Hồi chiêu đòn đánh
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
    protected float _flightTimer = 0f;
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
        CollisionMask = 1;

        // Quái nổi trên Portal (ZIndex = 0) và dưới Player (ZIndex = 10)
        ZIndex = 5;

        // Từ tính bám sàn (Floor Snapping) cho quái đi bộ
        if (!IsFlyingEnemy)
        {
            FloorSnapLength = 10.0f;
            FloorStopOnSlope = true;
            FloorConstantSpeed = true;
        }

        // Bỏ qua va chạm vật lý với nhân vật người chơi
        IgnorePlayerPhysicsCollisions();

        // Tìm AnimatedSprite2D
        _animSprite = GetNodeOrNull<AnimatedSprite2D>("Container/AnimatedSprite2D")
                   ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        // Tìm Sight và AttackArea
        _sightArea = GetNodeOrNull<Area2D>("Sight")
                  ?? GetNodeOrNull<Area2D>("SightArea");
        _attackArea = GetNodeOrNull<Area2D>("AttackArea")
                   ?? GetNodeOrNull<Area2D>("Attack");

        // Cài đặt CollisionMask quét cả Layer 1 và 2 (Mask = 3)
        if (_sightArea != null)
        {
            _sightArea.CollisionMask = 3;
            _sightArea.BodyEntered += OnSightBodyEntered;
            _sightArea.BodyExited += OnSightBodyExited;
        }

        if (_attackArea != null)
        {
            _attackArea.CollisionMask = 3;
            _attackArea.BodyEntered += OnAttackAreaBodyEntered;
            _attackArea.BodyExited += OnAttackAreaBodyExited;
        }

        EnterPatrolIdle();
    }

    private void IgnorePlayerPhysicsCollisions()
    {
        foreach (Node node in GetTree().GetNodesInGroup("Player"))
        {
            if (node is CollisionObject2D col)
            {
                AddCollisionExceptionWith(col);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _flightTimer += dt;
        Vector2 velocity = Velocity;

        // Cập nhật các bộ đếm thời gian
        if (_invincibleTimer > 0f) _invincibleTimer -= dt;
        if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;
        if (_jumpCooldownTimer > 0f) _jumpCooldownTimer -= dt;

        // QUÉT MỤC TIÊU NGƯỜI CHƠI CHỦ ĐỘNG:
        // Giải quyết triệt để lỗi người chơi đứng trong vùng nhìn từ trước, hoặc vừa đổi nhân vật Tab
        if (_targetPlayer == null || !IsInstanceValid(_targetPlayer))
        {
            _targetPlayer = null;
            AcquirePlayerTarget();
        }

        // Xử lý trọng lực: Quái bay hoàn toàn không chịu trọng lực!
        if (!IsFlyingEnemy)
        {
            if (!IsOnFloor())
            {
                velocity.Y += Gravity * dt;

                // Ma sát trượt tường khi quái rơi
                if (IsOnWall() && velocity.Y > 0)
                {
                    velocity.Y = Mathf.MoveToward(velocity.Y, 150.0f, 600.0f * dt);
                }
            }
        }

        if (CurrentState == EnemyState.Dead)
        {
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        // Xử lý từng trạng thái hành vi
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

    /// <summary>
    /// Quét tìm mục tiêu người chơi trong tầm nhìn
    /// </summary>
    protected void AcquirePlayerTarget()
    {
        // 1. Quét qua các body đang overlap trong SightArea
        if (_sightArea != null)
        {
            foreach (var body in _sightArea.GetOverlappingBodies())
            {
                if (body is Node2D n && (n.IsInGroup("Player") || n is BaseCharacter))
                {
                    SetTargetPlayer(n);
                    return;
                }
            }
        }

        // 2. Dự phòng: Kiểm tra nhóm "Player" trong bán kính tầm nhìn (~250px)
        foreach (Node node in GetTree().GetNodesInGroup("Player"))
        {
            if (node is Node2D playerNode && IsInstanceValid(playerNode))
            {
                if (GlobalPosition.DistanceTo(playerNode.GlobalPosition) <= 260.0f)
                {
                    SetTargetPlayer(playerNode);
                    return;
                }
            }
        }
    }

    protected void SetTargetPlayer(Node2D player)
    {
        _targetPlayer = player;
        _chaseTimer = 0f;
        CurrentState = EnemyState.Chase;

        if (player is CollisionObject2D col)
        {
            AddCollisionExceptionWith(col);
        }
    }

    // ==========================================
    // LOGIC CÁC TRẠNG THÁI
    // ==========================================

    private void HandlePatrolIdleState(float dt, ref Vector2 velocity)
    {
        velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * 4f * dt);

        if (IsFlyingEnemy)
        {
            // Nhấp nhô nhẹ khi lơ lửng tại chỗ
            velocity.Y = Mathf.Sin(_flightTimer * FlyWobbleFrequency) * (FlyWobbleAmplitude * 0.5f);
        }

        _stateTimer -= dt;

        if (_stateTimer <= 0f)
        {
            _patrolDirection = _rng.RandiRange(0, 1) == 0 ? -1 : 1;
            EnterPatrolWalk();
        }
    }

    private void HandlePatrolWalkState(float dt, ref Vector2 velocity)
    {
        float speed = IsFlyingEnemy ? FlyingSpeed : PatrolSpeed;
        velocity.X = _patrolDirection * speed;

        if (IsFlyingEnemy)
        {
            // Lượn sóng Y nhịp nhàng khi bay dạo
            velocity.Y = Mathf.Sin(_flightTimer * FlyWobbleFrequency) * FlyWobbleAmplitude;
        }

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
            _targetPlayer = null;
            EnterPatrolIdle();
            return;
        }

        _chaseTimer += dt;

        // Nếu hết thời gian dí tối đa: kiểm tra xem người chơi có còn gần không
        if (_chaseTimer >= ChaseMaxDuration)
        {
            _chaseTimer = 0f;
            // Tạm nghỉ 1s rồi tiếp tục dí nếu người chơi vẫn ở gần
            _stateTimer = 1.0f;
            CurrentState = EnemyState.PatrolIdle;
            return;
        }

        float distToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);

        // Kiểm tra điều kiện tấn công (cự ly 2D gần HOẶC AttackArea đang chạm Player)
        bool inAttackRange = distToPlayer <= AttackRange;
        if (!inAttackRange && _attackArea != null)
        {
            inAttackRange = _attackArea.OverlapsBody(_targetPlayer);
        }

        if (inAttackRange && _attackCooldownTimer <= 0f)
        {
            velocity = Vector2.Zero;
            EnterAttack();
            return;
        }

        // DI CHUYỂN ĐUỔI THEO:
        if (IsFlyingEnemy)
        {
            // QUÁI BAY: Phóng thẳng 2D tự do hướng về người chơi
            Vector2 toPlayer = _targetPlayer.GlobalPosition - GlobalPosition;
            if (toPlayer.LengthSquared() > 0.01f)
            {
                Vector2 dir = toPlayer.Normalized();
                velocity = dir * FlyingChaseSpeed;
                _patrolDirection = dir.X >= 0 ? 1 : -1;
            }
        }
        else
        {
            // QUÁI ĐI BỘ: Di chuyển ngang và nhảy khi gặp vật cản
            float diffX = _targetPlayer.GlobalPosition.X - GlobalPosition.X;
            int chaseDir = diffX > 0 ? 1 : -1;

            velocity.X = chaseDir * ChaseSpeed;
            _patrolDirection = chaseDir;

            if (CanJumpObstacle && IsOnFloor() && _jumpCooldownTimer <= 0f)
            {
                bool hitWall = IsOnWall();
                bool playerIsAbove = (_targetPlayer.GlobalPosition.Y < GlobalPosition.Y - JumpTriggerHeight)
                                     && (Mathf.Abs(diffX) < JumpTriggerRange);

                if (hitWall || playerIsAbove)
                {
                    velocity.Y = JumpVelocity;
                    _jumpCooldownTimer = JumpCooldown;
                }
            }
        }
    }

    private void HandleAttackState(float dt, ref Vector2 velocity)
    {
        velocity = velocity.MoveToward(Vector2.Zero, PatrolSpeed * 5f * dt);
        _stateTimer -= dt;

        if (_stateTimer <= 0f)
        {
            _attackCooldownTimer = AttackCooldown;

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

    private void HandleHurtState(float dt, ref Vector2 velocity)
    {
        velocity = velocity.MoveToward(Vector2.Zero, PatrolSpeed * 4f * dt);
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

        // Quay mặt chuẩn xác về hướng người chơi khi bắt đầu đòn tấn công
        if (_targetPlayer != null && IsInstanceValid(_targetPlayer) && _animSprite != null)
        {
            _animSprite.FlipH = (_targetPlayer.GlobalPosition.X < GlobalPosition.X);
        }
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
            SetTargetPlayer(body);
        }
    }

    public virtual void OnSightBodyExited(Node2D body)
    {
    }

    public virtual void OnAttackAreaBodyEntered(Node2D body)
    {
        if (body.IsInGroup("Player") || body is BaseCharacter)
        {
            if (_attackCooldownTimer <= 0f && CurrentState != EnemyState.Attack)
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
        else if (_targetPlayer != null && IsInstanceValid(_targetPlayer) && CurrentState == EnemyState.Attack)
        {
            _animSprite.FlipH = (_targetPlayer.GlobalPosition.X < GlobalPosition.X);
        }

        switch (CurrentState)
        {
            case EnemyState.PatrolIdle:
                PlayAnimationSafe("idle");
                break;

            case EnemyState.PatrolWalk:
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

using Godot;
using System;
using System.Collections.Generic;
using MyLuckVaults.Enemies.AI;

/// <summary>
/// Class quái cơ bản (BaseEnemy) với hệ thống AI tự hành thông minh thế hệ mới:
/// - Quét nhận diện mục tiêu chủ động liên tục, không bao giờ bỏ đánh khi người chơi đứng im.
/// - Tích hợp thuật toán tìm đường động A* 2D theo thời gian thực (EnemyNavigationGrid):
///   + QUÁI BAY (IsFlyingEnemy = true): Tự động bay vòng tránh chướng ngại vật, tự căn lề chui lọt
///     qua ống khói / khe tường / cửa hẹp, tránh bị kẹt trên sàn 1 chiều, bay mượt theo đường cong
///     kèm né va chạm tức thời (Whisker Avoidance) và đón đầu hướng di chuyển của người chơi.
///   + QUÁI ĐI BỘ (IsFlyingEnemy = false): Tự động nhảy qua chướng ngại vật/hố sâu, leo lên các bậc thềm cao,
///     và tự tìm đường đi vòng tới miệng hố / ống khói để rơi xuống hầm săn đuổi người chơi!
/// - Hệ thống phát hiện kẹt (Anti-Stuck Recovery) tự động gỡ kẹt nếu vướng góc địa hình.
/// </summary>
public partial class BaseEnemy : CharacterBody2D
{
    // ==========================================
    // CÁC THÔNG SỐ TÙY CHỈNH (INSPECTOR)
    // ==========================================
    [ExportGroup("Máu & Sát Thương Nhận Vào")]
    [Export] public int MaxHits { get; set; } = 3;                 // Số lần bị đánh trúng để tiêu diệt
    [Export] public float InvincibleDuration { get; set; } = 0.5f; // Thời gian bất tử chớp nhoáng sau mỗi hit

    [ExportGroup("Tốc Độ & Di Chuyển Mặt Đất")]
    [Export] public float PatrolSpeed { get; set; } = 100.0f;      // Tốc độ đi tuần tra tự nhiên
    [Export] public float ChaseSpeed { get; set; } = 220.0f;       // Tốc độ khi đuổi theo người chơi
    [Export] public float GroundAcceleration { get; set; } = 1500.0f;
    [Export] public float Gravity { get; set; } = 980.0f;
    [Export] public float ChaseMaxDuration { get; set; } = 10.0f;  // Thời gian tối đa đuổi khi mục tiêu quá xa
    [Export] public float LoseAggroDistance { get; set; } = 950.0f;// Khoảng cách quá xa mới từ bỏ truy đuổi

    [ExportGroup("Chế Độ Quái Bay (Flying Enemy Mode)")]
    // Đánh dấu quái có khả năng bay lượn tự do hay không (chỉ cần IsFlyingEnemy = true trong file .cs riêng)
    [Export] public bool IsFlyingEnemy { get; set; } = false;
    [Export] public float FlyingSpeed { get; set; } = 160.0f;      // Tốc độ bay tuần tra
    [Export] public float FlyingChaseSpeed { get; set; } = 230.0f; // Tốc độ bay khi săn đuổi
    [Export] public float FlyAcceleration { get; set; } = 900.0f;  // Gia tốc lượn chuyển hướng mượt mà
    [Export] public float FlyWobbleFrequency { get; set; } = 2.5f; // Tần số nhấp nhô tự nhiên
    [Export] public float FlyWobbleAmplitude { get; set; } = 22.0f;// Biên độ nhấp nhô (px)

    [ExportGroup("Nhảy Vượt Chướng Ngại Vật (Quái Đi Bộ)")]
    [Export] public bool CanJumpObstacle { get; set; } = true;     // Cho phép nhảy vượt vật cản/leo thềm
    [Export] public int MaxAirJumps { get; set; } = 1;             // Số lần nhảy thêm trên không khi cần leo dốc cao
    [Export] public float JumpVelocity { get; set; } = -520.0f;    // Lực nhảy (âm = hướng lên)
    [Export] public float JumpCooldown { get; set; } = 0.55f;      // Thời gian hồi giữa 2 lần nhảy
    [Export] public float JumpTriggerHeight { get; set; } = 36.0f; // Độ cao chênh lệch tối thiểu để nhảy
    [Export] public float JumpTriggerRange { get; set; } = 220.0f; // Phạm vi ngang kích hoạt nhảy

    [ExportGroup("Tấn Công & Tầm Đánh")]
    [Export] public float AttackRange { get; set; } = 55.0f;       // Khoảng cách 2D để tung đòn đánh
    [Export] public float AttackCooldown { get; set; } = 1.5f;     // Hồi chiêu đòn đánh
    [Export] public float AttackDuration { get; set; } = 0.6f;     // Thời gian thực hiện hoạt ảnh đòn đánh

    [ExportGroup("Thời Gian Đi Tuần Tra Tự Nhiên")]
    [Export] public float MinIdleTime { get; set; } = 1.0f;
    [Export] public float MaxIdleTime { get; set; } = 3.0f;
    [Export] public float MinWalkTime { get; set; } = 2.0f;
    [Export] public float MaxWalkTime { get; set; } = 5.0f;

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
    protected int _airJumpsRemaining = 0;
    protected Node2D _targetPlayer = null;

    protected AnimatedSprite2D _animSprite;
    protected Area2D _sightArea;
    protected Area2D _attackArea;
    protected RandomNumberGenerator _rng = new RandomNumberGenerator();

    // ==========================================
    // BIẾN AI TÌM ĐƯỜNG & CHỐNG KẸT (PATHFINDING & ANTI-STUCK)
    // ==========================================
    private List<Vector2> _currentPath = new List<Vector2>();
    private int _pathWaypointIndex = 0;
    private float _repathTimer = 0f;
    private Vector2 _lastPathGoal = Vector2.Zero;

    private float _stuckCheckTimer = 0f;
    private Vector2 _lastStuckCheckPos = Vector2.Zero;
    private float _escapeTimer = 0f;
    private Vector2 _escapeVector = Vector2.Zero;

    private float _bodyHalfWidth = 24.0f;
    private float _bodyHalfHeight = 24.0f;
    private int _maxPlatformerJumpSteps = 16;

    public override void _Ready()
    {
        _rng.Randomize();

        // Quái ở Layer 3 (giá trị 4), chỉ va chạm vật lý với địa hình Layer 1
        CollisionLayer = 4;
        CollisionMask = 1;
        ZIndex = 5;

        if (!IsFlyingEnemy)
        {
            FloorSnapLength = 10.0f;
            FloorStopOnSlope = true;
            FloorConstantSpeed = true;
        }
        else
        {
            // Quái bay chuyển sang MotionMode Floating để không dính ma sát sàn/tường bất thường
            MotionMode = MotionModeEnum.Floating;
        }

        // Đo kích thước hitbox thực tế của con quái này để AI tìm đường chui lọt lỗ/ống khói chuẩn xác
        ComputeBodyExtents();
        ComputePlatformerJumpBudget();

        // Stagger timer tìm đường ban đầu để nhiều quái không tính A* cùng 1 frame
        _repathTimer = _rng.RandfRange(0.02f, 0.15f);
        _lastStuckCheckPos = GlobalPosition;

        IgnorePlayerPhysicsCollisions();

        _animSprite = GetNodeOrNull<AnimatedSprite2D>("Container/AnimatedSprite2D")
                   ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        _sightArea = GetNodeOrNull<Area2D>("Sight")
                  ?? GetNodeOrNull<Area2D>("SightArea");
        _attackArea = GetNodeOrNull<Area2D>("AttackArea")
                   ?? GetNodeOrNull<Area2D>("Attack");

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

    private void ComputeBodyExtents()
    {
        var colShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (colShape != null && colShape.Shape != null)
        {
            float localHalfW = 24f;
            float localHalfH = 24f;

            if (colShape.Shape is CircleShape2D circle)
            {
                localHalfW = circle.Radius;
                localHalfH = circle.Radius;
            }
            else if (colShape.Shape is CapsuleShape2D capsule)
            {
                localHalfW = capsule.Radius;
                localHalfH = capsule.Height * 0.5f;
            }
            else if (colShape.Shape is RectangleShape2D rect)
            {
                localHalfW = rect.Size.X * 0.5f;
                localHalfH = rect.Size.Y * 0.5f;
            }

            Vector2 totalScale = new Vector2(
                Mathf.Abs(Scale.X * colShape.Scale.X),
                Mathf.Abs(Scale.Y * colShape.Scale.Y)
            );
            localHalfW *= totalScale.X;
            localHalfH *= totalScale.Y;

            // Xử lý xoay (ví dụ StormSkyHarpy xoay CollisionShape2D 90 độ)
            float cos = Mathf.Abs(Mathf.Cos(colShape.Rotation));
            float sin = Mathf.Abs(Mathf.Sin(colShape.Rotation));

            _bodyHalfWidth = Mathf.Clamp(localHalfW * cos + localHalfH * sin, 16f, 64f);
            _bodyHalfHeight = Mathf.Clamp(localHalfW * sin + localHalfH * cos, 16f, 64f);
        }
    }

    private void ComputePlatformerJumpBudget()
    {
        if (Gravity <= 1f)
        {
            _maxPlatformerJumpSteps = 16;
            return;
        }
        float singleJumpPx = (JumpVelocity * JumpVelocity) / (2.0f * Gravity);
        int singleCells = Mathf.Max(5, Mathf.RoundToInt(singleJumpPx / EnemyNavigationGrid.CellSize));
        int totalCells = singleCells * (1 + Mathf.Max(0, MaxAirJumps));
        _maxPlatformerJumpSteps = Mathf.Clamp(totalCells * 2, 12, 32);
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
        if (_repathTimer > 0f) _repathTimer -= dt;
        if (_escapeTimer > 0f) _escapeTimer -= dt;

        // QUÉT MỤC TIÊU NGƯỜI CHƠI CHỦ ĐỘNG
        if (_targetPlayer == null || !IsInstanceValid(_targetPlayer))
        {
            _targetPlayer = null;
            AcquirePlayerTarget();
        }
        else
        {
            if (CurrentState == EnemyState.PatrolIdle || CurrentState == EnemyState.PatrolWalk)
            {
                CurrentState = EnemyState.Chase;
                _chaseTimer = 0f;
            }
        }

        // Trọng lực cho quái đi bộ
        if (!IsFlyingEnemy)
        {
            if (IsOnFloor())
            {
                _airJumpsRemaining = MaxAirJumps;
            }
            else
            {
                velocity.Y += Gravity * dt;
                if (IsOnWall() && velocity.Y > 0)
                {
                    velocity.Y = Mathf.MoveToward(velocity.Y, 160.0f, 600.0f * dt);
                }
            }
        }

        if (CurrentState == EnemyState.Dead)
        {
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

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
    // LOGIC TUẦN TRA THÔNG MINH (PATROL)
    // ==========================================

    private void HandlePatrolIdleState(float dt, ref Vector2 velocity)
    {
        if (IsFlyingEnemy)
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, FlyAcceleration * dt);
            float hoverY = Mathf.Sin(_flightTimer * FlyWobbleFrequency) * (FlyWobbleAmplitude * 0.5f);
            velocity.Y = Mathf.MoveToward(velocity.Y, hoverY, FlyAcceleration * dt);
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * 4f * dt);
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
        _stateTimer -= dt;

        if (IsFlyingEnemy)
        {
            Vector2 desired = new Vector2(
                _patrolDirection * FlyingSpeed,
                Mathf.Sin(_flightTimer * FlyWobbleFrequency) * FlyWobbleAmplitude
            );
            velocity = velocity.MoveToward(desired, FlyAcceleration * dt);

            // Kiểm tra vật cản phía trước khi bay tuần tra để quay đầu lịch sự thay vì húc tường
            if (IsOnWall() || IsWallAhead(_patrolDirection, _bodyHalfWidth + 24f))
            {
                _patrolDirection *= -1;
                EnterPatrolIdle();
                return;
            }
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, _patrolDirection * PatrolSpeed, GroundAcceleration * dt);

            // Quái đi bộ tự động quay đầu ở mép vực sâu khi đang đi tuần tra (không tự lao xuống hố vô cớ)
            bool hitWall = IsOnWall();
            bool cliffAhead = IsOnFloor() && !HasFloorAhead(_patrolDirection, _bodyHalfWidth + 16f);

            if (hitWall || cliffAhead)
            {
                _patrolDirection *= -1;
                EnterPatrolIdle();
                return;
            }
        }

        if (_stateTimer <= 0f)
        {
            EnterPatrolIdle();
        }
    }

    // ==========================================
    // LOGIC SĂN ĐUỔI THÔNG MINH (AUTONOMOUS CHASE AI)
    // ==========================================

    private void HandleChaseState(float dt, ref Vector2 velocity)
    {
        if (_targetPlayer == null || !IsInstanceValid(_targetPlayer))
        {
            _targetPlayer = null;
            _currentPath.Clear();
            EnterPatrolIdle();
            return;
        }

        _chaseTimer += dt;
        float distToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);

        // Nếu người chơi chạy thật xa ngoài tầm đuổi (LoseAggroDistance) VÀ hết giờ đuổi -> mới ngừng truy đuổi
        if (distToPlayer > LoseAggroDistance && _chaseTimer >= ChaseMaxDuration)
        {
            _targetPlayer = null;
            _chaseTimer = 0f;
            _currentPath.Clear();
            EnterPatrolIdle();
            return;
        }

        // Nếu người chơi vẫn trong tầm săn đuổi hợp lý: giữ mục tiêu liên tục không ngừng nghỉ
        if (distToPlayer <= 350.0f)
        {
            _chaseTimer = 0f;
        }

        // Kiểm tra điều kiện tấn công (cự ly 2D gần HOẶC AttackArea đang chạm Player)
        bool inAttackRange = distToPlayer <= AttackRange;
        if (!inAttackRange && _attackArea != null)
        {
            inAttackRange = _attackArea.OverlapsBody(_targetPlayer);
        }

        if (inAttackRange && _attackCooldownTimer <= 0f)
        {
            velocity = IsFlyingEnemy ? velocity * 0.25f : new Vector2(0, velocity.Y);
            EnterAttack();
            return;
        }

        // Kiểm tra kẹt vị trí (chỉ kiểm tra khi chưa vào tầm đánh để tránh nhảy loạn lúc chờ hồi chiêu)
        UpdateStuckDetection(dt, ref velocity, inAttackRange);

        // Chạy thuật toán điều hướng chuyên biệt
        if (IsFlyingEnemy)
        {
            HandleFlyingChaseAI(dt, ref velocity, distToPlayer, inAttackRange);
        }
        else
        {
            HandleGroundChaseAI(dt, ref velocity, distToPlayer, inAttackRange);
        }
    }

    // -------------------------------------------------------------------------
    // AI QUÁI BAY: Tìm đường A* 2D + Làm mượt đường bay + Né râu cảm biến + Đón đầu
    // -------------------------------------------------------------------------
    private void HandleFlyingChaseAI(float dt, ref Vector2 velocity, float distToPlayer, bool inAttackRange)
    {
        // Đã áp sát tầm đánh và đang chờ hồi chiêu -> giữ nhịp lượn nhẹ ổn định để đánh liên tục
        if (inAttackRange && _attackCooldownTimer > 0f)
        {
            Vector2 toTarget = (_targetPlayer.GlobalPosition - GlobalPosition);
            Vector2 hoverVel = toTarget.LengthSquared() > 16f ? toTarget.Normalized() * (FlyingSpeed * 0.3f) : Vector2.Zero;
            hoverVel.Y += Mathf.Sin(_flightTimer * FlyWobbleFrequency) * (FlyWobbleAmplitude * 0.4f);
            velocity = velocity.MoveToward(hoverVel, FlyAcceleration * dt);
            if (Mathf.Abs(toTarget.X) > 4f) _patrolDirection = toTarget.X >= 0 ? 1 : -1;
            return;
        }

        // Nếu đang trong trạng thái gỡ kẹt tức thời
        if (_escapeTimer > 0f)
        {
            velocity = velocity.MoveToward(_escapeVector, FlyAcceleration * 1.6f * dt);
            if (Mathf.Abs(velocity.X) > 5f) _patrolDirection = velocity.X >= 0 ? 1 : -1;
            return;
        }

        World2D world = GetWorld2D();
        PhysicsDirectSpaceState2D spaceState = world?.DirectSpaceState;

        Vector2 targetPos = _targetPlayer.GlobalPosition;
        // Đón đầu nhẹ hướng chạy của người chơi nếu đang ở cự ly trung bình
        Vector2 predictedPos = targetPos;
        if (_targetPlayer is CharacterBody2D playerBody && distToPlayer > 70f && distToPlayer < 450f)
        {
            predictedPos += playerBody.Velocity * 0.14f;
        }

        // 1. KIỂM TRA TẦM NHÌN BAY THẲNG THÔNG THOÁNG (LOS)
        bool hasDirectFlight = EnemyNavigationGrid.HasClearFlightLine(
            spaceState, world, GlobalPosition, targetPos, _bodyHalfWidth, _bodyHalfHeight
        );

        Vector2 steerTarget = targetPos;

        if (hasDirectFlight)
        {
            _currentPath.Clear();

            // Kiểm tra điểm đón đầu có thoáng không, nếu thoáng thì bay đón đầu
            if (predictedPos != targetPos &&
                EnemyNavigationGrid.HasClearFlightLine(spaceState, world, GlobalPosition, predictedPos, _bodyHalfWidth, _bodyHalfHeight))
            {
                steerTarget = predictedPos;
            }
        }
        else
        {
            // 2. BỊ TƯỜNG / SÀN / VẬT CẢN / ỐNG KHÓI CHẶN -> TÌM ĐƯỜNG A* ĐỘNG!
            bool needRepath = _repathTimer <= 0f
                           || _currentPath.Count == 0
                           || _pathWaypointIndex >= _currentPath.Count
                           || targetPos.DistanceTo(_lastPathGoal) > 48.0f;

            if (needRepath)
            {
                var result = EnemyNavigationGrid.FindFlyingPath(
                    spaceState, world, GlobalPosition, targetPos, _bodyHalfWidth, _bodyHalfHeight
                );

                _repathTimer = result.ReachedExactGoal
                    ? (0.22f + _rng.RandfRange(0f, 0.08f))
                    : (0.45f + _rng.RandfRange(0f, 0.15f));
                _lastPathGoal = targetPos;

                if (result.Success && result.Waypoints.Count > 0)
                {
                    _currentPath = result.Waypoints;
                    _pathWaypointIndex = 0;
                }
            }

            if (_currentPath.Count > 0 && _pathWaypointIndex < _currentPath.Count)
            {
                // Tự động bỏ qua waypoint hiện tại nếu đã tới gần HOẶC nhìn thấy thẳng waypoint tiếp theo
                while (_pathWaypointIndex < _currentPath.Count - 1)
                {
                    float wpDist = GlobalPosition.DistanceTo(_currentPath[_pathWaypointIndex]);
                    bool canShortcut = EnemyNavigationGrid.HasClearFlightLine(
                        spaceState, world, GlobalPosition, _currentPath[_pathWaypointIndex + 1],
                        _bodyHalfWidth, _bodyHalfHeight
                    );

                    if (wpDist < 22f || canShortcut)
                    {
                        _pathWaypointIndex++;
                    }
                    else
                    {
                        break;
                    }
                }

                steerTarget = _currentPath[_pathWaypointIndex];

                // Nếu chạm tới waypoint cuối mà vẫn còn xa target, cập nhật lại path
                if (_pathWaypointIndex == _currentPath.Count - 1 && GlobalPosition.DistanceTo(steerTarget) < 20f)
                {
                    _repathTimer = 0f;
                }
            }
        }

        // 3. TÍNH VECTOR HƯỚNG BAY + CẢM BIẾN NÉ GÓC TRƯỢT MƯỢT (WHISKER TANGENT SLIDE)
        Vector2 toSteer = steerTarget - GlobalPosition;
        Vector2 desiredDir = toSteer.LengthSquared() > 0.01f ? toSteer.Normalized() : Vector2.Zero;

        if (desiredDir != Vector2.Zero && spaceState != null)
        {
            float avoidanceWeight = hasDirectFlight ? 1.0f : 0.55f;
            desiredDir = ApplyFlightWhiskerAvoidance(spaceState, desiredDir, targetPos, avoidanceWeight);
        }

        // Thêm nhịp đập cánh nhẹ nhàng khi còn ở xa, tắt bớt khi chui khe hẹp
        Vector2 desiredVel = desiredDir * FlyingChaseSpeed;
        if (hasDirectFlight && distToPlayer > 90f)
        {
            desiredVel.Y += Mathf.Sin(_flightTimer * FlyWobbleFrequency) * (FlyWobbleAmplitude * 0.45f);
        }

        velocity = velocity.MoveToward(desiredVel, FlyAcceleration * dt);

        if (Mathf.Abs(velocity.X) > 4f)
        {
            _patrolDirection = velocity.X >= 0 ? 1 : -1;
        }
    }

    /// <summary>
    /// Râu cảm biến phía trước (Whiskers) giúp quái bay trượt lượn theo mép tường thay vì húc tường hay bật ngược ra sau.
    /// </summary>
    private Vector2 ApplyFlightWhiskerAvoidance(PhysicsDirectSpaceState2D spaceState, Vector2 desiredDir, Vector2 targetPos, float weight)
    {
        float feelerLen = Mathf.Max(_bodyHalfWidth, _bodyHalfHeight) + 22.0f;
        Vector2 avoidance = Vector2.Zero;
        Vector2 strongestNormal = Vector2.Zero;
        float strongestHit = 0f;

        float[] angles = { 0f, 0.52f, -0.52f, 1.0f, -1.0f };
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 rayDir = desiredDir.Rotated(angles[i]);
            var query = PhysicsRayQueryParameters2D.Create(
                GlobalPosition,
                GlobalPosition + rayDir * feelerLen,
                1 // Layer 1
            );
            query.CollideWithBodies = true;
            query.CollideWithAreas = false;

            var hit = spaceState.IntersectRay(query);
            if (hit != null && hit.Count > 0)
            {
                if (IsHitOneWayFromBelowOrSide(hit, rayDir)) continue;

                Vector2 hitPos = hit["position"].AsVector2();
                Vector2 normal = hit["normal"].AsVector2();
                float dist = GlobalPosition.DistanceTo(hitPos);
                float strength = 1.0f - Mathf.Clamp(dist / feelerLen, 0f, 1f);

                avoidance += normal * (strength * weight);
                if (strength > strongestHit)
                {
                    strongestHit = strength;
                    strongestNormal = normal;
                }
            }
        }

        if (strongestHit <= 0.001f) return desiredDir;

        Vector2 combined = desiredDir + avoidance;
        // Nếu lao trực diện vuông góc tường khiến tổng vector bị triệt tiêu hoặc quay ngược lại:
        // Chuyển sang vector trượt tiếp tuyến (tangent) hướng về phía mục tiêu để né qua cạnh tường!
        if (combined.Dot(desiredDir) <= 0.15f && strongestNormal != Vector2.Zero)
        {
            Vector2 tangent = new Vector2(-strongestNormal.Y, strongestNormal.X);
            if (tangent.Dot(targetPos - GlobalPosition) < 0f)
            {
                tangent = -tangent;
            }
            return (tangent * 0.85f + strongestNormal * 0.25f).Normalized();
        }

        return combined.LengthSquared() > 0.001f ? combined.Normalized() : desiredDir;
    }

    private bool IsHitOneWayFromBelowOrSide(Godot.Collections.Dictionary hit, Vector2 rayDir)
    {
        if (!hit.ContainsKey("collider")) return false;
        GodotObject obj = hit["collider"].AsGodotObject();
        if (obj is CollisionObject2D colObj)
        {
            int shapeIdx = hit.ContainsKey("shape") ? hit["shape"].AsInt32() : 0;
            uint ownerId = colObj.ShapeFindOwner(shapeIdx);
            if (colObj.IsShapeOwnerOneWayCollisionEnabled(ownerId))
            {
                return rayDir.Y <= 0.1f; // Cho phép bay lên hoặc bay ngang qua OneWay
            }
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // AI QUÁI ĐI BỘ: Platformer A* + Nhảy qua hố/vật cản + Tự căn phanh rớt đúng miệng ống khói/hầm
    // -------------------------------------------------------------------------
    private void HandleGroundChaseAI(float dt, ref Vector2 velocity, float distToPlayer, bool inAttackRange)
    {
        Vector2 targetPos = _targetPlayer.GlobalPosition;
        float diffY = targetPos.Y - GlobalPosition.Y;
        float diffX = targetPos.X - GlobalPosition.X;

        // Đã đứng áp sát tầm đánh trên cùng mặt sàn và đang chờ hồi chiêu: đứng vững mặt đối mặt sẵn sàng vung đòn
        if (inAttackRange && _attackCooldownTimer > 0f && Mathf.Abs(diffY) < 32f)
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, GroundAcceleration * dt);
            if (Mathf.Abs(diffX) > 4f) _patrolDirection = diffX >= 0 ? 1 : -1;
            return;
        }

        World2D world = GetWorld2D();
        PhysicsDirectSpaceState2D spaceState = world?.DirectSpaceState;

        // 1. Kiểm tra xem có thể chạy bộ trực tiếp trên cùng một mặt sàn phẳng không
        bool sameFloorLevel = Mathf.Abs(diffY) < 42f;
        bool canDirectWalk = sameFloorLevel && IsOnFloor() && IsDirectGroundPathClear(targetPos);

        Vector2 moveTarget = targetPos;
        Vector2 lookAheadTarget = targetPos;
        bool usingPathfinder = false;
        bool upcomingDropShaft = false;
        Vector2 dropShaftTarget = targetPos;

        if (canDirectWalk)
        {
            _currentPath.Clear();
        }
        else
        {
            usingPathfinder = true;
            bool needRepath = _repathTimer <= 0f
                           || _currentPath.Count == 0
                           || _pathWaypointIndex >= _currentPath.Count
                           || targetPos.DistanceTo(_lastPathGoal) > 48.0f;

            if (needRepath)
            {
                var result = EnemyNavigationGrid.FindGroundPath(
                    spaceState, world, GlobalPosition, targetPos,
                    _bodyHalfWidth, _bodyHalfHeight, _maxPlatformerJumpSteps
                );

                _repathTimer = result.ReachedExactGoal
                    ? (0.22f + _rng.RandfRange(0f, 0.08f))
                    : (0.45f + _rng.RandfRange(0f, 0.15f));
                _lastPathGoal = targetPos;

                if (result.Success && result.Waypoints.Count > 0)
                {
                    _currentPath = result.Waypoints;
                    _pathWaypointIndex = 0;
                }
            }

            if (_currentPath.Count > 0 && _pathWaypointIndex < _currentPath.Count)
            {
                // Cập nhật tiến trình đi qua từng waypoint trên lưới 16px
                while (_pathWaypointIndex < _currentPath.Count - 1)
                {
                    Vector2 wp = _currentPath[_pathWaypointIndex];
                    if (Mathf.Abs(GlobalPosition.X - wp.X) < 16f && Mathf.Abs(GlobalPosition.Y - wp.Y) < 26f)
                    {
                        _pathWaypointIndex++;
                    }
                    else
                    {
                        break;
                    }
                }

                moveTarget = _currentPath[_pathWaypointIndex];
                int lookIdx = Mathf.Min(_pathWaypointIndex + 1, _currentPath.Count - 1);
                lookAheadTarget = _currentPath[lookIdx];

                // Quét trước tối đa 5 waypoint sắp tới xem có đường rơi xuống ống khói / hố không!
                // Giúp quái KHÔNG nhảy vọt qua miệng ống khói ngay trước mép hố!
                int maxProbe = Mathf.Min(_currentPath.Count - 1, _pathWaypointIndex + 5);
                for (int p = _pathWaypointIndex; p <= maxProbe; p++)
                {
                    Vector2 futureWp = _currentPath[p];
                    if (futureWp.Y > GlobalPosition.Y + 18f && Mathf.Abs(futureWp.X - GlobalPosition.X) < 76f)
                    {
                        upcomingDropShaft = true;
                        dropShaftTarget = futureWp;
                        break;
                    }
                }
            }
        }

        // 2. ĐIỀU KHIỂN DI CHUYỂN NGANG HƯỚNG TỚI WAYPOINT (CÓ PHANH CĂN TÂM KHI XUỐNG ỐNG KHÓI HẸP)
        float steerDiffX = moveTarget.X - GlobalPosition.X;
        int moveDir = Mathf.Abs(steerDiffX) > 6f ? (steerDiffX > 0 ? 1 : -1) : _patrolDirection;

        if (upcomingDropShaft && Mathf.Abs(dropShaftTarget.X - GlobalPosition.X) < 42f)
        {
            // Phanh nhẹ và căn tâm ngay phía trên miệng ống khói để rơi thẳng xuống hầm thay vì lao quá đà sang bờ bên kia
            float shaftDiffX = dropShaftTarget.X - GlobalPosition.X;
            float alignVelX = Mathf.Clamp(shaftDiffX * 5.0f, -110f, 110f);
            velocity.X = Mathf.MoveToward(velocity.X, alignVelX, GroundAcceleration * 1.3f * dt);
            if (Mathf.Abs(shaftDiffX) > 4f) _patrolDirection = shaftDiffX >= 0 ? 1 : -1;
        }
        else if (Mathf.Abs(steerDiffX) <= 6f && moveTarget.Y > GlobalPosition.Y + 20f)
        {
            velocity.X = Mathf.MoveToward(velocity.X, steerDiffX * 4f, GroundAcceleration * dt);
        }
        else
        {
            float accel = IsOnFloor() ? GroundAcceleration : GroundAcceleration * 0.7f;
            velocity.X = Mathf.MoveToward(velocity.X, moveDir * ChaseSpeed, accel * dt);
            _patrolDirection = moveDir;
        }

        // 3. QUYẾT ĐỊNH NHẢY THÔNG MINH (Vượt vật cản / Nhảy qua hố / Leo bậc thềm)
        if (CanJumpObstacle && _jumpCooldownTimer <= 0f)
        {
            bool wantsGoDownShaft = upcomingDropShaft || moveTarget.Y > GlobalPosition.Y + 18f;
            bool ceilingBlocked = IsCeilingDirectlyAbove();
            bool wantsGoUp = !ceilingBlocked && (
                (moveTarget.Y < GlobalPosition.Y - 16f && Mathf.Abs(moveTarget.X - GlobalPosition.X) < 56f) ||
                (lookAheadTarget.Y < GlobalPosition.Y - 22f && Mathf.Abs(lookAheadTarget.X - GlobalPosition.X) < 64f)
            );

            bool hitWall = IsOnWall();
            bool pitAhead = IsOnFloor() && !HasFloorAhead(moveDir, _bodyHalfWidth + 14f);
            bool jumpAcrossPit = pitAhead && !wantsGoDownShaft;

            bool shouldJump = false;

            if (IsOnFloor())
            {
                if (usingPathfinder)
                {
                    if (wantsGoUp || hitWall || jumpAcrossPit)
                    {
                        shouldJump = true;
                    }
                }
                else
                {
                    bool playerAbove = !ceilingBlocked && (targetPos.Y < GlobalPosition.Y - JumpTriggerHeight) && (Mathf.Abs(diffX) < JumpTriggerRange);
                    if (hitWall || playerAbove || jumpAcrossPit)
                    {
                        shouldJump = true;
                    }
                }
            }
            else if (_airJumpsRemaining > 0 && velocity.Y >= -30f && wantsGoUp)
            {
                // Nhảy bồi thêm trên không khi cần leo lên bậc thềm cao
                shouldJump = true;
                _airJumpsRemaining--;
            }

            if (shouldJump)
            {
                velocity.Y = JumpVelocity;
                _jumpCooldownTimer = JumpCooldown;
            }
        }
    }

    // -------------------------------------------------------------------------
    // HỆ THỐNG PHÁT HIỆN & TỰ ĐỘNG GỠ KẸT (ANTI-STUCK RECOVERY)
    // -------------------------------------------------------------------------
    private void UpdateStuckDetection(float dt, ref Vector2 velocity, bool inAttackRange)
    {
        if (inAttackRange)
        {
            _stuckCheckTimer = 0f;
            _lastStuckCheckPos = GlobalPosition;
            return;
        }

        _stuckCheckTimer += dt;
        if (_stuckCheckTimer < 0.42f) return;

        float movedDist = GlobalPosition.DistanceTo(_lastStuckCheckPos);
        _stuckCheckTimer = 0f;
        _lastStuckCheckPos = GlobalPosition;

        if (movedDist < 10.0f)
        {
            // Làm mới dữ liệu bản đồ cục bộ và bắt buộc tính lại đường đi A* ngay lập tức
            EnemyNavigationGrid.InvalidateRegion(GlobalPosition, 6);
            _repathTimer = 0f;

            if (IsFlyingEnemy)
            {
                // Nếu quái bay đang tỳ vào góc vật cản: bật né theo vector pháp tuyến va chạm
                if (GetSlideCollisionCount() > 0)
                {
                    Vector2 normal = GetLastSlideCollision().GetNormal();
                    Vector2 tangent = new Vector2(-normal.Y, normal.X);
                    if (_targetPlayer != null && tangent.Dot(_targetPlayer.GlobalPosition - GlobalPosition) < 0)
                    {
                        tangent = -tangent;
                    }
                    _escapeVector = (normal * 0.65f + tangent * 0.85f).Normalized() * FlyingChaseSpeed;
                    _escapeTimer = 0.28f;
                }
                else
                {
                    // Nhích nhẹ lên trên để thoát mép tường
                    _escapeVector = new Vector2(_patrolDirection * FlyingSpeed * 0.6f, -FlyingChaseSpeed * 0.8f);
                    _escapeTimer = 0.22f;
                }
            }
            else
            {
                // Nếu quái đi bộ bị kẹt: thực hiện cú nhảy thoát kẹt
                if (CanJumpObstacle && IsOnFloor() && _jumpCooldownTimer <= 0f)
                {
                    velocity.Y = JumpVelocity;
                    _jumpCooldownTimer = JumpCooldown;
                }
            }
        }
    }

    // ==========================================
    // CÁC HÀM CẢM BIẾN VẬT LÝ HỖ TRỢ
    // ==========================================

    private bool HasFloorAhead(int dir, float forwardDist)
    {
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return true;

        Vector2 start = GlobalPosition + new Vector2(dir * forwardDist, 0);
        Vector2 end = start + new Vector2(0, _bodyHalfHeight + 34.0f);

        var query = PhysicsRayQueryParameters2D.Create(start, end, 1);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        var result = spaceState.IntersectRay(query);
        return result != null && result.Count > 0;
    }

    private bool IsWallAhead(int dir, float forwardDist)
    {
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return false;

        Vector2 end = GlobalPosition + new Vector2(dir * forwardDist, 0);
        var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, end, 1);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        var result = spaceState.IntersectRay(query);
        return result != null && result.Count > 0;
    }

    private bool IsCeilingDirectlyAbove()
    {
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return false;

        Vector2 end = GlobalPosition - new Vector2(0, _bodyHalfHeight + 22.0f);
        var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, end, 1);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        var result = spaceState.IntersectRay(query);
        return result != null && result.Count > 0;
    }

    private bool IsDirectGroundPathClear(Vector2 targetPos)
    {
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return false;

        float dist = Mathf.Abs(targetPos.X - GlobalPosition.X);
        if (dist > 320f) return false;

        // 1. Kiểm tra không có tường hoặc vật cản thấp ở giữa (bắn cả tia giữa thân và tia sát chân)
        var centerQuery = PhysicsRayQueryParameters2D.Create(GlobalPosition, targetPos, 1);
        centerQuery.CollideWithBodies = true;
        centerQuery.CollideWithAreas = false;
        var centerHit = spaceState.IntersectRay(centerQuery);
        if (centerHit != null && centerHit.Count > 0) return false;

        Vector2 footOffset = new Vector2(0, _bodyHalfHeight * 0.65f);
        var lowQuery = PhysicsRayQueryParameters2D.Create(GlobalPosition + footOffset, targetPos + footOffset, 1);
        lowQuery.CollideWithBodies = true;
        lowQuery.CollideWithAreas = false;
        var lowHit = spaceState.IntersectRay(lowQuery);
        if (lowHit != null && lowHit.Count > 0) return false;

        // 2. Kiểm tra mặt sàn liên tục ở khoảng giữa (không có hố sâu)
        int steps = Mathf.Max(1, Mathf.FloorToInt(dist / 32f));
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / (steps + 1);
            Vector2 sample = GlobalPosition.Lerp(targetPos, t);
            var floorQuery = PhysicsRayQueryParameters2D.Create(
                sample,
                sample + new Vector2(0, _bodyHalfHeight + 40f),
                1
            );
            floorQuery.CollideWithBodies = true;
            floorQuery.CollideWithAreas = false;
            var floorHit = spaceState.IntersectRay(floorQuery);
            if (floorHit == null || floorHit.Count == 0) return false;
        }

        return true;
    }

    // ==========================================
    // TẤN CÔNG, NHẬN SÁT THƯƠNG & HOẠT ẢNH
    // ==========================================

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
                _chaseTimer = 0f;
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

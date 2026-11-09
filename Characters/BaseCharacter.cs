using Godot;
using System;

public partial class BaseCharacter : CharacterBody2D
{
    // ==========================================
    // CÁC THÔNG SỐ CƠ BẢN (CHỈNH TRỰC TIẾP TRÊN INSPECTOR)
    // ==========================================
    [ExportGroup("Di Chuyển Trên Mặt Đất")]
    [Export] public bool CanMove { get; set; } = true;
    [Export] public float WalkSpeed { get; set; } = 250.0f;
    [Export] public float RunSpeed { get; set; } = 400.0f;
    [Export] public float Acceleration { get; set; } = 1500.0f;
    [Export] public float Friction { get; set; } = 1200.0f;

    [ExportGroup("Nhảy & Trọng Lực & Ma Sát")]
    [Export] public float JumpVelocity { get; set; } = -420.0f;
    [Export] public int MaxJumps { get; set; } = 2; // Nhảy N lần tùy chỉnh
    [Export] public float FallGravityMultiplier { get; set; } = 1.35f;
    [Export] public float CoyoteTimeMax { get; set; } = 0.15f;
    [Export] public float JumpBufferMax { get; set; } = 0.15f;
    [Export] public float AirAcceleration { get; set; } = 950.0f; // Gia tốc trên không (thấp hơn mặt đất để có quán tính chân thực)
    [Export] public float AirFriction { get; set; } = 350.0f;      // Ma sát cản không khí
    [Export] public float WallSlideSpeedMax { get; set; } = 130.0f; // Tốc độ trượt ma sát tường tối đa khi cọ vào vật cản/tường
    [Export] public float WallFriction { get; set; } = 800.0f;     // Lực ma sát hãm rơi khi áp sát tường

    [ExportGroup("Chế Độ Bay (Fly Mode - Phím F)")]
    [Export] public bool CanFly { get; set; } = true;
    [Export] public float FlySpeed { get; set; } = 320.0f;
    [Export] public float FlyBoostSpeed { get; set; } = 480.0f;
    [Export] public float FlyAcceleration { get; set; } = 1200.0f;
    [Export] public float MaxFlyDuration { get; set; } = 50.0f; // Thời gian bay tối đa liên tục (giây)

    [ExportGroup("Lướt (Dash - Phím Q)")]
    [Export] public bool CanDashSkill { get; set; } = true;
    [Export] public float DashSpeed { get; set; } = 650.0f;
    [Export] public float DashDuration { get; set; } = 0.18f;
    [Export] public float DashCooldown { get; set; } = 0.3f;
    [Export] public bool AllowAirDash { get; set; } = true;

    [ExportGroup("Năng Lượng & Giới Hạn (Mana / Stamina / Breath)")]
    [Export] public float MaxMana { get; set; } = 1000.0f;
    [Export] public float CurrentMana { get; set; } = 100.0f;
    [Export] public float ManaRegenRate { get; set; } = 15.0f;   // Hồi phục mỗi giây khi nghỉ ngơi
    [Export] public float FlyManaDrain { get; set; } = 20.0f;    // Tiêu hao mỗi giây khi bay
    [Export] public float RunManaDrain { get; set; } = 14.0f;    // Tiêu hao mỗi giây khi chạy nhanh
    [Export] public float SwimManaDrain { get; set; } = 10.0f;   // Tiêu hao mỗi giây khi ở dưới nước/bơi
    [Export] public float DashManaCost { get; set; } = 20.0f;    // Tiêu hao cho mỗi cú lướt Q
    [Export] public float MaxRunDuration { get; set; } = 8.0f;   // Chạy liên tục tối đa (giây)
    [Export] public float MaxSwimDuration { get; set; } = 10.0f; // Ở dưới nước tối đa (giây)
    [Export] public bool IsSwimming { get; set; } = false;       // Cờ trạng thái dưới nước

    // ==========================================
    // THÀNH PHẦN & BIẾN TRẠNG THÁI NỘI BỘ
    // ==========================================
    public AnimatedSprite2D AnimatedSprite { get; private set; }

    protected int _remainingJumps;
    protected float _coyoteTimer = 0f;
    protected float _jumpBufferTimer = 0f;
    protected float _dashTimer = 0f;
    protected float _dashCooldownTimer = 0f;
    protected bool _hasAirDashed = false; // Ngăn spam dash trên không nhiều lần

    protected float _flyDurationTimer = 0f;
    protected float _runDurationTimer = 0f;
    protected float _swimDurationTimer = 0f;

    // Hướng nhìn nhân vật: 1 là Phải, -1 là Trái
    public int FacingDirection { get; set; } = 1;

    protected bool _isDashing = false;
    protected bool _isFlying = false;

    // Các thuộc tính Public để BaseLevel kế thừa trạng thái khi bấm Tab đổi nhân vật
    public int RemainingJumps
    {
        get => _remainingJumps;
        set => _remainingJumps = value;
    }

    public bool IsFlyingState
    {
        get => _isFlying;
        set => _isFlying = value;
    }

    public bool HasAirDashed
    {
        get => _hasAirDashed;
        set => _hasAirDashed = value;
    }

    public float FlyDurationTimer
    {
        get => _flyDurationTimer;
        set => _flyDurationTimer = value;
    }

    public float RunDurationTimer
    {
        get => _runDurationTimer;
        set => _runDurationTimer = value;
    }

    public float SwimDurationTimer
    {
        get => _swimDurationTimer;
        set => _swimDurationTimer = value;
    }

    public override void _Ready()
    {
        // Thêm vào group "Player" để kẻ địch nhận diện
        AddToGroup("Player");

        // Player: Đứng ở Layer 2 (giá trị 2)
        CollisionLayer = 2;

        // Mask = 1: Chỉ va chạm vật lý với Sàn/Tường Layer 1
        CollisionMask = 1;

        // ZIndex = 10 để nhân vật luôn hiển thị trên Portal và Quái
        ZIndex = 10;

        // Từ tính bám sàn (Floor Snapping) giúp tiếp đất mượt mà, không bị nảy trên dốc
        FloorSnapLength = 12.0f;
        FloorStopOnSlope = true;
        FloorConstantSpeed = true;

        AnimatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        // Nếu _remainingJumps chưa được truyền từ nhân vật trước thì gán MaxJumps
        if (_remainingJumps <= 0 && IsOnFloor())
        {
            _remainingJumps = MaxJumps;
        }

        if (AnimatedSprite != null)
        {
            AnimatedSprite.FlipH = (FacingDirection == -1);
            AnimatedSprite.Play("idle");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!CanMove) return;

        float dt = (float)delta;
        Vector2 velocity = Velocity;

        UpdateTimers(dt);
        UpdateManaAndDurations(dt);

        if (_isDashing)
        {
            HandleDashing(dt, ref velocity);
            Velocity = velocity;
            MoveAndSlide();
            UpdateVisuals();
            return;
        }

        CheckDashInput();

        if (_isFlying)
        {
            HandleFlyMovement(dt, ref velocity);
        }
        else
        {
            ApplyGravityAndFriction(dt, ref velocity);
            HandleJump(ref velocity);
            HandleHorizontalMovement(dt, ref velocity);
        }

        Velocity = velocity;
        MoveAndSlide();

        UpdateVisuals();
    }

    /// <summary>
    /// Cập nhật Mana và giới hạn thời gian bay / chạy / bơi
    /// </summary>
    protected virtual void UpdateManaAndDurations(float dt)
    {
        bool isMoving = Velocity.LengthSquared() > 100.0f;
        bool isSprinting = IsSprintPressed() && isMoving && IsOnFloor();

        // 1. Quản lý thời gian và năng lượng khi Bay
        if (_isFlying)
        {
            _flyDurationTimer += dt;
            CurrentMana = Mathf.Max(0f, CurrentMana - FlyManaDrain * dt);

            if (CurrentMana <= 0f || _flyDurationTimer >= MaxFlyDuration)
            {
                // Hết năng lượng hoặc hết thời gian bay -> Buộc hạ cánh rơi tự do
                _isFlying = false;
            }
        }
        else
        {
            // Hồi phục dần thời gian bay khi không bay
            if (_flyDurationTimer > 0f)
            {
                _flyDurationTimer = Mathf.Max(0f, _flyDurationTimer - dt * 1.5f);
            }
        }

        // 2. Quản lý thời gian và năng lượng khi Chạy nhanh
        if (isSprinting)
        {
            _runDurationTimer += dt;
            CurrentMana = Mathf.Max(0f, CurrentMana - RunManaDrain * dt);

            if (CurrentMana <= 0f || _runDurationTimer >= MaxRunDuration)
            {
                // Hết mana hoặc chạy quá lâu -> Tự động giảm về đi bộ
                // Được xử lý trong CanSprintNow()
            }
        }
        else
        {
            if (_runDurationTimer > 0f)
            {
                _runDurationTimer = Mathf.Max(0f, _runDurationTimer - dt * 2.0f);
            }
        }

        // 3. Quản lý thời gian và năng lượng khi Bơi dưới nước
        if (IsSwimming)
        {
            _swimDurationTimer += dt;
            CurrentMana = Mathf.Max(0f, CurrentMana - SwimManaDrain * dt);
        }
        else
        {
            if (_swimDurationTimer > 0f)
            {
                _swimDurationTimer = Mathf.Max(0f, _swimDurationTimer - dt * 2.5f);
            }
        }

        // 4. Hồi phục Mana tự nhiên khi không tiêu tốn năng lượng
        if (!_isFlying && !isSprinting && !_isDashing && !IsSwimming)
        {
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + ManaRegenRate * dt);
        }
    }

    /// <summary>
    /// Kiểm tra người chơi có đủ điều kiện chạy nước rút không
    /// </summary>
    protected virtual bool CanSprintNow()
    {
        return IsSprintPressed() && CurrentMana > 2.0f && _runDurationTimer < MaxRunDuration;
    }

    protected virtual Vector2 GetMovementInputVector()
    {
        float x = 0f;
        float y = 0f;

        if (Input.IsKeyPressed(Key.A) || Input.IsActionPressed("ui_left")) x -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsActionPressed("ui_right")) x += 1f;

        bool isUp = Input.IsKeyPressed(Key.W) || Input.IsActionPressed("ui_up")
                 || (_isFlying && Input.IsKeyPressed(Key.Space));
        if (isUp) y -= 1f;

        if (Input.IsKeyPressed(Key.S) || Input.IsActionPressed("ui_down")) y += 1f;

        Vector2 input = new Vector2(x, y);
        return input.LengthSquared() > 0 ? input.Normalized() : Vector2.Zero;
    }

    protected virtual bool IsJumpPressed()
    {
        return Input.IsKeyPressed(Key.Space)
            || Input.IsKeyPressed(Key.W)
            || Input.IsActionPressed("ui_up");
    }

    protected virtual bool IsSprintPressed()
    {
        return Input.IsKeyPressed(Key.Shift);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!CanMove) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F && CanFly)
            {
                if (!_isFlying)
                {
                    // Chỉ cho phép kích hoạt bay khi còn đủ Mana và thời gian bay
                    if (CurrentMana > 5.0f && _flyDurationTimer < MaxFlyDuration)
                    {
                        _isFlying = true;
                        _remainingJumps = MaxJumps;
                    }
                }
                else
                {
                    _isFlying = false;
                }
            }
        }
    }

    protected virtual void HandleFlyMovement(float dt, ref Vector2 velocity)
    {
        Vector2 input = GetMovementInputVector();
        float currentSpeed = CanSprintNow() ? FlyBoostSpeed : FlySpeed;

        if (input != Vector2.Zero)
        {
            velocity = velocity.MoveToward(input * currentSpeed, FlyAcceleration * dt);
            if (input.X != 0)
            {
                FacingDirection = input.X > 0 ? 1 : -1;
            }
        }
        else
        {
            velocity = velocity.MoveToward(Vector2.Zero, Friction * dt);
        }
    }

    /// <summary>
    /// Xử lý trọng lực, ma sát không khí và ma sát trượt tường chân thực
    /// </summary>
    protected virtual void ApplyGravityAndFriction(float dt, ref Vector2 velocity)
    {
        if (!IsOnFloor())
        {
            float gravity = GetGravity().Y;

            if (IsSwimming)
            {
                // Dưới nước trọng lực giảm mạnh, có lực đẩy nổi
                velocity.Y += gravity * 0.3f * dt;
                velocity.Y = Mathf.Min(velocity.Y, 150.0f);
            }
            else
            {
                if (velocity.Y > 0)
                {
                    velocity.Y += gravity * FallGravityMultiplier * dt;
                }
                else
                {
                    velocity.Y += gravity * dt;
                }

                // MA SÁT TRƯỢT TƯỜNG (Wall Friction):
                // Khi người chơi áp sát tường/vật cản lúc đang rơi, lực ma sát sẽ hãm tốc độ rơi
                if (IsOnWall() && velocity.Y > 0)
                {
                    velocity.Y = Mathf.MoveToward(velocity.Y, WallSlideSpeedMax, WallFriction * dt);
                }
            }
        }
        else
        {
            // Khi chân chạm đất: reset lại số lần nhảy và cờ air dash
            _remainingJumps = MaxJumps;
            _coyoteTimer = CoyoteTimeMax;
            _hasAirDashed = false;
        }
    }

    protected virtual void HandleJump(ref Vector2 velocity)
    {
        if (Input.IsKeyPressed(Key.Space) || Input.IsKeyPressed(Key.W) || Input.IsActionJustPressed("ui_up"))
        {
            _jumpBufferTimer = JumpBufferMax;
        }

        bool canJumpFromGround = (_coyoteTimer > 0f) && (_jumpBufferTimer > 0f);
        bool isHoldingJumpOnFloor = IsJumpPressed() && IsOnFloor();

        if (canJumpFromGround || isHoldingJumpOnFloor)
        {
            velocity.Y = JumpVelocity;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _remainingJumps = MaxJumps - 1;
        }
        else if (Input.IsKeyPressed(Key.Space) || Input.IsKeyPressed(Key.W) || Input.IsActionJustPressed("ui_up"))
        {
            // Nhảy trên không: chỉ nhảy được nếu còn lượt nhảy
            if (_remainingJumps > 0 && !IsOnFloor())
            {
                velocity.Y = JumpVelocity;
                _remainingJumps--;
            }
        }
    }

    protected virtual void HandleHorizontalMovement(float dt, ref Vector2 velocity)
    {
        Vector2 input = GetMovementInputVector();
        float targetSpeed = CanSprintNow() ? RunSpeed : WalkSpeed;

        if (IsSwimming)
        {
            targetSpeed *= 0.65f; // Ma sát nước cản tốc độ di chuyển
        }

        if (input.X != 0)
        {
            float currentAccel = IsOnFloor() ? Acceleration : AirAcceleration;
            velocity.X = Mathf.MoveToward(velocity.X, input.X * targetSpeed, currentAccel * dt);
            FacingDirection = input.X > 0 ? 1 : -1;
        }
        else
        {
            float currentFriction = IsOnFloor() ? Friction : AirFriction;
            velocity.X = Mathf.MoveToward(velocity.X, 0, currentFriction * dt);
        }
    }

    protected virtual void CheckDashInput()
    {
        if (!CanDashSkill || _dashCooldownTimer > 0f) return;

        // Nếu ở trên không và đã dash rồi thì không cho phép dash tiếp (ngăn bay map)
        if (!IsOnFloor() && (!AllowAirDash || _hasAirDashed)) return;

        // Yêu cầu đủ Mana để Dash
        if (CurrentMana < DashManaCost) return;

        if (Input.IsKeyPressed(Key.Q))
        {
            _isDashing = true;
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown;
            CurrentMana -= DashManaCost;

            if (!IsOnFloor())
            {
                _hasAirDashed = true; // Đánh dấu đã dash trên không
            }
        }
    }

    protected virtual void HandleDashing(float dt, ref Vector2 velocity)
    {
        velocity.X = FacingDirection * DashSpeed;

        // Hãm rơi nhẹ trong chớp mắt lúc dash nhưng không biến mất trọng lực hoàn toàn
        velocity.Y = Mathf.MoveToward(velocity.Y, 0f, 600f * dt);

        _dashTimer -= dt;

        if (_dashTimer <= 0f)
        {
            _isDashing = false;
            // Kết thúc dash: hãm nhẹ vận tốc để tránh văng xuyên map
            velocity.X *= 0.5f;
        }
    }

    protected virtual void UpdateVisuals()
    {
        if (AnimatedSprite == null) return;

        AnimatedSprite.FlipH = (FacingDirection == -1);

        if (_isDashing)
        {
            PlayAnimationSafe("run");
            return;
        }

        if (_isFlying)
        {
            PlayAnimationSafe("fly");
            return;
        }

        if (!IsOnFloor())
        {
            PlayAnimationSafe("jump");
            return;
        }

        if (Mathf.Abs(Velocity.X) > 10.0f)
        {
            bool isRunning = Mathf.Abs(Velocity.X) > WalkSpeed + 10.0f;
            PlayAnimationSafe(isRunning ? "run" : "walk");
        }
        else
        {
            PlayAnimationSafe("idle");
        }
    }

    protected void PlayAnimationSafe(string animName)
    {
        if (AnimatedSprite != null && AnimatedSprite.SpriteFrames != null && AnimatedSprite.SpriteFrames.HasAnimation(animName))
        {
            if (AnimatedSprite.Animation != animName || !AnimatedSprite.IsPlaying())
            {
                AnimatedSprite.Play(animName);
            }
        }
    }

    private void UpdateTimers(float dt)
    {
        if (_coyoteTimer > 0f) _coyoteTimer -= dt;
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= dt;
    }
}

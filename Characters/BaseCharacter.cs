using Godot;
using System;

/// <summary>
/// Class đại diện cho nhân vật gốc (Player Base).
/// Chứa toàn bộ logic vật lý nền tảng: di chuyển, nhảy nhiều lần (Multi-Jump),
/// lướt (Dash), bay (Fly), bơi (Swim), thể lực/mana, ma sát tường và hoạt ảnh.
/// Tất cả các lớp nhân vật cụ thể (Char01, Char02, ...) kế thừa từ class này.
/// </summary>
public partial class BaseCharacter : CharacterBody2D
{
    // ==========================================
    // CÁC THÔNG SỐ CƠ BẢN (CHỈNH TRỰC TIẾP TRÊN INSPECTOR)
    // ==========================================
    [ExportGroup("Di Chuyển Trên Mặt Đất")]
    // Cho phép hoặc vô hiệu hóa toàn bộ việc điều khiển nhân vật (hữu ích khi cutscene/chết)
    [Export] public bool CanMove { get; set; } = true;
    // Tốc độ di chuyển bình thường (pixels/giây)
    [Export] public float WalkSpeed { get; set; } = 250.0f;
    // Tốc độ khi giữ Shift chạy nhanh
    [Export] public float RunSpeed { get; set; } = 400.0f;
    // Lực gia tốc khi bắt đầu bước đi (tăng tốc nhanh/chậm tạo cảm giác đầm)
    [Export] public float Acceleration { get; set; } = 1500.0f;
    // Lực ma sát hãm lại khi nhả phím di chuyển trên mặt đất
    [Export] public float Friction { get; set; } = 1200.0f;

    [ExportGroup("Nhảy & Trọng Lực & Ma Sát")]
    // Lực phóng lên khi nhảy (giá trị âm hướng lên trên theo trục Y của Godot)
    [Export] public float JumpVelocity { get; set; } = -420.0f;
    // Tổng số lần nhảy cho phép: 1 lần bật từ mặt đất + (MaxJumps - 1) lần nhảy trên không
    [Export] public int MaxJumps { get; set; } = 10;
    // Hệ số nhân trọng lực khi đang rơi xuống, giúp cú rơi dứt khoát không bị cảm giác "lơ lửng trên mặt trăng"
    [Export] public float FallGravityMultiplier { get; set; } = 1.35f;
    // Coyote Time: Khoảng thời gian ân huệ (giây) sau khi vừa bước hụt khỏi mép bục vẫn bấm nhảy được
    [Export] public float CoyoteTimeMax { get; set; } = 0.15f;
    // Jump Buffer: Cho phép nhận lệnh nhảy trước khi chạm đất 0.15s, ngay khi chạm đất sẽ tự nhảy ngay lập tức
    [Export] public float JumpBufferMax { get; set; } = 0.15f;
    // Gia tốc điều hướng khi đang ở trên không (thấp hơn mặt đất để giữ quán tính)
    [Export] public float AirAcceleration { get; set; } = 950.0f;
    // Ma sát cản không khí khi không ấn phím điều hướng trên không
    [Export] public float AirFriction { get; set; } = 350.0f;
    // Tốc độ tối đa khi trượt áp sát tường (Wall Slide)
    [Export] public float WallSlideSpeedMax { get; set; } = 130.0f;
    // Lực hãm ma sát khi bám vào tường lúc đang rơi
    [Export] public float WallFriction { get; set; } = 800.0f;

    [ExportGroup("Chế Độ Bay (Fly Mode - Phím F)")]
    // Bật/tắt khả năng bay lượn tự do 8 hướng của nhân vật
    [Export] public bool CanFly { get; set; } = true;
    // Tốc độ bay thông thường
    [Export] public float FlySpeed { get; set; } = 320.0f;
    // Tốc độ bay tăng tốc khi giữ Shift
    [Export] public float FlyBoostSpeed { get; set; } = 480.0f;
    // Gia tốc đạt vận tốc bay tối đa
    [Export] public float FlyAcceleration { get; set; } = 1200.0f;
    // Thời gian bay liên tục tối đa (giây) trước khi bị ép hạ cánh
    [Export] public float MaxFlyDuration { get; set; } = 50.0f;

    [ExportGroup("Lướt (Dash - Phím Q)")]
    // Bật/tắt kỹ năng lướt nhanh
    [Export] public bool CanDashSkill { get; set; } = true;
    // Vận tốc phóng đi khi thực hiện Dash
    [Export] public float DashSpeed { get; set; } = 650.0f;
    // Thời gian duy trì cú lướt (giây)
    [Export] public float DashDuration { get; set; } = 0.18f;
    // Thời gian hồi chiêu giữa 2 lần Dash liên tiếp
    [Export] public float DashCooldown { get; set; } = 0.3f;
    // Cho phép lướt khi đang ở trên không
    [Export] public bool AllowAirDash { get; set; } = true;

    [ExportGroup("Năng Lượng & Giới Hạn (Mana / Stamina / Breath)")]
    // Thanh mana/thể lực tối đa
    [Export] public float MaxMana { get; set; } = 1000.0f;
    // Mana hiện tại
    [Export] public float CurrentMana { get; set; } = 1000.0f;
    // Tốc độ tự động hồi phục Mana mỗi giây khi nhân vật ở trạng thái nghỉ
    [Export] public float ManaRegenRate { get; set; } = 15.0f;
    // Mana tiêu hao mỗi giây khi kích hoạt chế độ bay F
    [Export] public float FlyManaDrain { get; set; } = 20.0f;
    // Mana tiêu hao mỗi giây khi chạy nước rút (Shift)
    [Export] public float RunManaDrain { get; set; } = 14.0f;
    // Tiêu hao Mana mỗi giây khi bơi dưới nước (giả lập thanh dưỡng khí/thể lực bơi)
    [Export] public float SwimManaDrain { get; set; } = 10.0f;
    // Lượng Mana tiêu tốn ngay lập tức cho mỗi lần bấm Dash (Q)
    [Export] public float DashManaCost { get; set; } = 20.0f;
    // Giới hạn thời gian chạy nước rút liên tục (giây)
    [Export] public float MaxRunDuration { get; set; } = 8.0f;
    // Giới hạn thời gian ở dưới nước tối đa (giây)
    [Export] public float MaxSwimDuration { get; set; } = 10.0f;
    // Cờ trạng thái nhân vật có đang chìm trong môi trường nước hay không
    [Export] public bool IsSwimming { get; set; } = false;

    // ==========================================
    // THÀNH PHẦN & BIẾN TRẠNG THÁI NỘI BỘ
    // ==========================================
    // Node hiển thị hoạt ảnh Sprite của nhân vật
    public AnimatedSprite2D AnimatedSprite { get; private set; }

    // Số lần nhảy còn lại trước khi chạm đất trở lại
    protected int _remainingJumps;
    // Đồng hồ đếm lùi thời gian Coyote Time
    protected float _coyoteTimer = 0f;
    // Đồng hồ đếm lùi thời gian lưu đệm phím nhảy Jump Buffer
    protected float _jumpBufferTimer = 0f;
    // Thời gian duy trì cú Dash hiện tại
    protected float _dashTimer = 0f;
    // Đồng hồ đếm lùi hồi chiêu Dash
    protected float _dashCooldownTimer = 0f;
    // Cờ kiểm soát chỉ cho phép lướt trên không đúng 1 lần duy nhất để chống bay xuyên bản đồ
    protected bool _hasAirDashed = false;

    // Bộ đếm thời gian liên tục cho các trạng thái tiêu hao thể lực
    protected float _flyDurationTimer = 0f;
    protected float _runDurationTimer = 0f;
    protected float _swimDurationTimer = 0f;

    // Lưu trạng thái nhấn ở frame trước để xác định chính xác sự kiện vừa mới ấn phím (Single-Tap Trigger)
    private bool _spaceWasPressed = false;
    private bool _wWasPressed = false;
    private bool _qWasPressed = false;
    private bool _fWasPressed = false;

    // Cờ nhận biết phím vừa nhấn ở frame hiện tại (được tính toán ở đầu mỗi _PhysicsProcess)
    private bool _isJumpJustPressed = false;
    private bool _isDashJustPressed = false;
    private bool _isFlyJustPressed = false;

    // Hướng mặt nhân vật đang quay: 1 là Phải, -1 là Trái
    public int FacingDirection { get; set; } = 1;

    // Trạng thái đang thực hiện lướt nhanh
    protected bool _isDashing = false;
    // Trạng thái đang kích hoạt chế độ bay tự do
    protected bool _isFlying = false;

    // Các thuộc tính Public để BaseLevel đồng bộ chính xác khi người chơi bấm Tab đổi nhân vật
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

    /// <summary>
    /// Hàm khởi tạo khi Node sẵn sàng trong SceneTree.
    /// Thiết lập nhóm nhận diện, cấu hình Layer va chạm vật lý và tải tham chiếu Sprite.
    /// </summary>
    public override void _Ready()
    {
        // Thêm vào group "Player" để quái, cổng và trigger nhận diện nhân vật người chơi
        AddToGroup("Player");

        // Cấu hình vật lý:
        // CollisionLayer = 2: Bản thân Player đứng ở Layer 2
        // CollisionMask = 1: Player chỉ va chạm vật lý với Sàn/Tường (Layer 1), không bị quái cản bước
        CollisionLayer = 2;
        CollisionMask = 1;

        // Đặt ZIndex = 10 để nhân vật luôn hiển thị nổi trên Cổng dịch chuyển (Z=0) và Quái (Z=5)
        ZIndex = 10;

        // Bật tính năng bám sàn (Floor Snapping) để tiếp đất mượt mà, không bị rung nảy khi chạy trên sườn dốc
        FloorSnapLength = 12.0f;
        FloorStopOnSlope = true;
        FloorConstantSpeed = true;

        // Lấy node AnimatedSprite2D con của nhân vật
        AnimatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        // Khởi tạo số lượt nhảy đầy đủ ban đầu
        _remainingJumps = MaxJumps;

        // Áp dụng hướng nhìn và phát animation đứng chờ
        if (AnimatedSprite != null)
        {
            AnimatedSprite.FlipH = (FacingDirection == -1);
            AnimatedSprite.Play("idle");
        }
    }

    /// <summary>
    /// Vòng lặp vật lý chính của Godot (mặc định 60 lần/giây cố định).
    /// Xử lý tuần tự: Đọc phím -> Đếm thời gian -> Quản lý Mana -> Di chuyển/Nhảy -> Va chạm -> Cập nhật Sprite.
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        // Nếu bị khóa di chuyển (CanMove = false), dừng toàn bộ xử lý vật lý
        if (!CanMove) return;

        float dt = (float)delta;
        Vector2 velocity = Velocity;

        // 1. Phân tích xung phím (phát hiện bấm 1 lần để chống lỗi ăn lặp phím khi giữ phím)
        UpdateKeyEdgeTriggers();

        // 2. Giảm các bộ đếm thời gian (Coyote, JumpBuffer, Cooldown)
        UpdateTimers(dt);

        // 3. Tính toán năng lượng tiêu hao/hồi phục (Bay, Chạy nhanh, Bơi)
        UpdateManaAndDurations(dt);

        // 4. Kiểm tra bật/tắt chế độ bay phím F
        CheckFlyToggle();

        // 5. Nếu đang trong trạng thái Dash, ưu tiên xử lý lướt và bỏ qua các logic di chuyển thông thường
        if (_isDashing)
        {
            HandleDashing(dt, ref velocity);
            Velocity = velocity;
            MoveAndSlide();
            UpdateVisuals();
            return;
        }

        // 6. Kiểm tra xem người chơi có kích hoạt Dash (phím Q) không
        CheckDashInput();

        // 7. Xử lý di chuyển theo chế độ: Đang Bay hoặc Đi bộ/Nhảy thông thường
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

        // 8. Áp dụng vận tốc vào hàm vật lý chuẩn của CharacterBody2D
        Velocity = velocity;
        MoveAndSlide();

        // 9. Cập nhật hướng quay mặt và animation tương ứng
        UpdateVisuals();
    }

    /// <summary>
    /// Phát hiện các phím vừa mới được ấn xuống ở frame này (Just Pressed / Single-Tap).
    /// Giúp phân biệt rạch ròi giữa việc "ấn 1 lần" và "ấn giữ phím liên tục".
    /// </summary>
    private void UpdateKeyEdgeTriggers()
    {
        bool spaceNow = Input.IsKeyPressed(Key.Space);
        bool wNow = Input.IsKeyPressed(Key.W);
        bool qNow = Input.IsKeyPressed(Key.Q);
        bool fNow = Input.IsKeyPressed(Key.F);

        // Bấm nhảy: kiểm tra Space hoặc W vừa được ấn xuống ở frame này, hoặc bắt qua Input Map mặc định của Godot
        bool spaceJustPressed = spaceNow && !_spaceWasPressed;
        bool wJustPressed = wNow && !_wWasPressed;
        _isJumpJustPressed = spaceJustPressed || wJustPressed || Input.IsActionJustPressed("ui_accept") || Input.IsActionJustPressed("ui_up");

        // Lướt Q và Bay F vừa mới ấn xuống
        _isDashJustPressed = (qNow && !_qWasPressed);
        _isFlyJustPressed = (fNow && !_fWasPressed);

        // Lưu trạng thái hiện tại làm mốc cho frame kế tiếp
        _spaceWasPressed = spaceNow;
        _wWasPressed = wNow;
        _qWasPressed = qNow;
        _fWasPressed = fNow;
    }

    /// <summary>
    /// Xử lý chuyển đổi qua lại giữa chế độ Đi bộ và chế độ Bay khi ấn phím F.
    /// Có kiểm tra điều kiện đủ Mana và chưa vượt quá thời gian bay liên tục tối đa.
    /// </summary>
    private void CheckFlyToggle()
    {
        if (_isFlyJustPressed && CanFly)
        {
            if (!_isFlying)
            {
                // Chỉ cho bay khi còn năng lượng và chưa hết thể lực bay
                if (CurrentMana > 5.0f && _flyDurationTimer < MaxFlyDuration)
                {
                    _isFlying = true;
                    _remainingJumps = MaxJumps; // Khi bật bay thì reset lại lượt nhảy
                }
            }
            else
            {
                // Tắt chế độ bay, nhân vật sẽ rơi tự do
                _isFlying = false;
            }
        }
    }

    /// <summary>
    /// Cập nhật Mana và quản lý thời gian sử dụng liên tục của các trạng thái Bay / Chạy / Bơi.
    /// Tự động hồi phục khi nhân vật đứng yên nghỉ ngơi.
    /// </summary>
    protected virtual void UpdateManaAndDurations(float dt)
    {
        bool isMoving = Velocity.LengthSquared() > 100.0f;
        bool isSprinting = IsSprintPressed() && isMoving && IsOnFloor();

        // 1. Quản lý trạng thái Bay
        if (_isFlying)
        {
            _flyDurationTimer += dt;
            CurrentMana = Mathf.Max(0f, CurrentMana - FlyManaDrain * dt);

            // Buộc rơi khi cạn Mana hoặc bay quá thời gian cho phép
            if (CurrentMana <= 0f || _flyDurationTimer >= MaxFlyDuration)
            {
                _isFlying = false;
            }
        }
        else
        {
            // Hồi phục dần thời gian bay tích lũy khi không bay
            if (_flyDurationTimer > 0f)
            {
                _flyDurationTimer = Mathf.Max(0f, _flyDurationTimer - dt * 1.5f);
            }
        }

        // 2. Quản lý trạng thái Chạy nhanh (Sprint)
        if (isSprinting)
        {
            _runDurationTimer += dt;
            CurrentMana = Mathf.Max(0f, CurrentMana - RunManaDrain * dt);
        }
        else
        {
            // Hồi phục thể lực chạy khi đi bộ hoặc đứng yên
            if (_runDurationTimer > 0f)
            {
                _runDurationTimer = Mathf.Max(0f, _runDurationTimer - dt * 2.0f);
            }
        }

        // 3. Quản lý trạng thái Bơi (Dưới nước)
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

        // 4. Hồi phục Mana tự nhiên khi nhân vật không tiêu tốn năng lượng cho bất kỳ hành động nào
        if (!_isFlying && !isSprinting && !_isDashing && !IsSwimming)
        {
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + ManaRegenRate * dt);
        }
    }

    /// <summary>
    /// Kiểm tra xem nhân vật có đủ điều kiện để kích hoạt chạy nhanh hay không.
    /// </summary>
    protected virtual bool CanSprintNow()
    {
        return IsSprintPressed() && CurrentMana > 2.0f && _runDurationTimer < MaxRunDuration;
    }

    /// <summary>
    /// Đọc vector di chuyển từ phím bấm A, D (ngang) và W, S, Space (dọc khi bay).
    /// Trả về vector đã được chuẩn hóa (Normalized) để tránh di chuyển chéo bị nhanh hơn bình thường.
    /// </summary>
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

    /// <summary>
    /// Kiểm tra người chơi có đang giữ phím Shift để tăng tốc chạy/bay hay không.
    /// </summary>
    protected virtual bool IsSprintPressed()
    {
        return Input.IsKeyPressed(Key.Shift);
    }

    /// <summary>
    /// Xử lý di chuyển đa hướng trong không gian khi ở chế độ Bay (Fly).
    /// </summary>
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
            // Dừng êm khi nhả phím điều khiển
            velocity = velocity.MoveToward(Vector2.Zero, Friction * dt);
        }
    }

    /// <summary>
    /// Tính toán trọng lực khi rơi, độ nổi dưới nước, và lực ma sát hãm trượt tường.
    /// </summary>
    protected virtual void ApplyGravityAndFriction(float dt, ref Vector2 velocity)
    {
        if (!IsOnFloor())
        {
            float gravity = GetGravity().Y;

            if (IsSwimming)
            {
                // Khi ở dưới nước, trọng lực giảm còn 30% và bị giới hạn tốc độ chìm
                velocity.Y += gravity * 0.3f * dt;
                velocity.Y = Mathf.Min(velocity.Y, 150.0f);
            }
            else
            {
                // Rơi xuống có trọng lực nhân thêm FallGravityMultiplier để cú nhảy dứt khoát
                if (velocity.Y > 0)
                {
                    velocity.Y += gravity * FallGravityMultiplier * dt;
                }
                else
                {
                    velocity.Y += gravity * dt;
                }

                // Ma sát trượt tường: Giảm vận tốc rơi khi người chơi cọ vào tường thẳng đứng
                if (IsOnWall() && velocity.Y > 0)
                {
                    velocity.Y = Mathf.MoveToward(velocity.Y, WallSlideSpeedMax, WallFriction * dt);
                }
            }
        }
        else
        {
            // Khi tiếp đất: Khôi phục toàn bộ số lần nhảy và cờ lướt trên không
            _remainingJumps = MaxJumps;
            _coyoteTimer = CoyoteTimeMax;
            _hasAirDashed = false;
        }
    }

    /// <summary>
    /// Xử lý cơ chế nhảy thông minh:
    /// - Nhảy từ mặt đất (hoặc trong khoảng thời gian Coyote Time).
    /// - Bộ đệm Jump Buffer (bấm nhảy trước khi chạm đất vẫn nhận).
    /// - Nhảy nhiều lần trên không (Multi-Jump / Air Jump) chuẩn xác từng lần bấm.
    /// </summary>
    protected virtual void HandleJump(ref Vector2 velocity)
    {
        // Khi người chơi vừa ấn nút nhảy, kích hoạt bộ đệm Jump Buffer
        if (_isJumpJustPressed)
        {
            _jumpBufferTimer = JumpBufferMax;
        }

        bool hasBuffer = _jumpBufferTimer > 0f;
        bool canGroundJump = (_coyoteTimer > 0f) && hasBuffer;

        if (canGroundJump)
        {
            // Cú nhảy ban đầu từ mặt đất
            velocity.Y = JumpVelocity;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _remainingJumps = MaxJumps - 1; // Tiêu hao 1 lần nhảy đất, còn lại N-1 lần trên không
        }
        else if (hasBuffer && !IsOnFloor())
        {
            // Nhảy trên không: Mỗi lần bấm nút chỉ tiêu hao đúng 1 lượt nhảy
            if (_remainingJumps > 0)
            {
                velocity.Y = JumpVelocity;
                _jumpBufferTimer = 0f; // Reset buffer ngay lập tức để không bị nuốt nhiều lượt cùng lúc
                _remainingJumps--;
            }
        }
    }

    /// <summary>
    /// Xử lý di chuyển ngang trên mặt đất hoặc điều hướng trên không.
    /// </summary>
    protected virtual void HandleHorizontalMovement(float dt, ref Vector2 velocity)
    {
        Vector2 input = GetMovementInputVector();
        float targetSpeed = CanSprintNow() ? RunSpeed : WalkSpeed;

        if (IsSwimming)
        {
            targetSpeed *= 0.65f; // Ma sát nước làm giảm tốc độ bơi
        }

        if (input.X != 0)
        {
            // Gia tốc trên đất khác gia tốc trên không để tạo độ trớn quán tính chân thực
            float currentAccel = IsOnFloor() ? Acceleration : AirAcceleration;
            velocity.X = Mathf.MoveToward(velocity.X, input.X * targetSpeed, currentAccel * dt);
            FacingDirection = input.X > 0 ? 1 : -1;
        }
        else
        {
            // Ma sát hãm lại khi nhả phím
            float currentFriction = IsOnFloor() ? Friction : AirFriction;
            velocity.X = Mathf.MoveToward(velocity.X, 0, currentFriction * dt);
        }
    }

    /// <summary>
    /// Kiểm tra điều kiện và kích hoạt kỹ năng Dash (phím Q).
    /// </summary>
    protected virtual void CheckDashInput()
    {
        if (!CanDashSkill || _dashCooldownTimer > 0f) return;
        if (!IsOnFloor() && (!AllowAirDash || _hasAirDashed)) return;
        if (CurrentMana < DashManaCost) return;

        if (_isDashJustPressed)
        {
            _isDashing = true;
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown;
            CurrentMana -= DashManaCost;

            if (!IsOnFloor())
            {
                _hasAirDashed = true; // Đánh dấu đã dùng cú lướt trên không
            }
        }
    }

    /// <summary>
    /// Xử lý vận tốc và thời gian khi đang thực hiện động tác Dash.
    /// </summary>
    protected virtual void HandleDashing(float dt, ref Vector2 velocity)
    {
        velocity.X = FacingDirection * DashSpeed;
        // Hãm nhẹ trọng lực khi lướt để tạo cảm giác lướt thẳng xé gió
        velocity.Y = Mathf.MoveToward(velocity.Y, 0f, 600f * dt);

        _dashTimer -= dt;

        if (_dashTimer <= 0f)
        {
            _isDashing = false;
            // Giảm 50% vận tốc ngang khi hết dash để nhân vật không bị trôi quá đà
            velocity.X *= 0.5f;
        }
    }

    /// <summary>
    /// Cập nhật hình ảnh hoạt ảnh (Animation) và lật mặt Sprite theo hướng di chuyển.
    /// </summary>
    protected virtual void UpdateVisuals()
    {
        if (AnimatedSprite == null) return;

        // Lật Sprite theo hướng mặt nhân vật (FacingDirection = -1 là quay trái)
        AnimatedSprite.FlipH = (FacingDirection == -1);

        // Thứ tự ưu tiên hoạt ảnh: Dash -> Bay -> Nhảy -> Chạy -> Đi bộ -> Đứng chờ
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

    /// <summary>
    /// Phát animation an toàn: kiểm tra có tồn tại animation đó trong SpriteFrames không
    /// và chỉ phát khi animation hiện tại khác hoặc đang bị ngắt để tránh giật hình.
    /// </summary>
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

    /// <summary>
    /// Đếm lùi các bộ đếm thời gian mỗi frame vật lý.
    /// </summary>
    private void UpdateTimers(float dt)
    {
        if (_coyoteTimer > 0f) _coyoteTimer -= dt;
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= dt;
    }
}

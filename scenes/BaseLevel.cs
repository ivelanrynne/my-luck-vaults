using Godot;
using System;

/// <summary>
/// Level nền tảng cho toàn bộ các màn chơi / bản đồ trong game (EmeraldCloverMeadow, CozyHouse, IceCave, Royal, ...).
/// Đảm nhiệm:
/// 1. Tự động khởi tạo và hiển thị hệ thống UI cố định (Pause, Setting, Audio) qua CanvasLayer độc lập.
/// 2. Quản lý việc sinh (Spawn) nhân vật người chơi được chọn từ GameManager.
/// 3. Hỗ trợ bấm phím [Tab] để hoán đổi nhanh giữa các nhân vật (Char01 <-> Char02) mà vẫn bảo toàn nguyên vẹn
///    vận tốc đà rơi (momentum), số lượt nhảy còn lại, lượng mana và trạng thái bay để chống các lỗi bay xuyên map.
/// </summary>
public partial class BaseLevel : Node2D
{
    // Tham chiếu đến thực thể nhân vật đang hoạt động trong level hiện tại
    protected CharacterBody2D _currentCharacter;

    // Vị trí mặc định để sinh nhân vật khi người chơi bước vào màn chơi lần đầu
    [Export]
    public Vector2 SpawnPosition = new Vector2(300, 250);

    // Đường dẫn PackedScene của ControlUI (chứa nút Pause, Play, Setting, BackToMenu, ...)
    [Export]
    public PackedScene ControlUiScene = GD.Load<PackedScene>("res://UserUI/ControlUI.tscn");

    [ExportGroup("Giới Hạn Đổi Nhân Vật")]
    // Thời gian hồi chiêu (giây) giữa các lần bấm phím Tab đổi nhân vật để chống spam lơ lửng trên không
    [Export]
    public float SwitchCooldown { get; set; } = 1.0f;

    // Bộ đếm lùi thời gian hồi chiêu đổi nhân vật
    protected float _switchCooldownTimer = 0f;

    public override void _Ready()
    {
        // 1. Tự động khởi tạo hệ thống giao diện UI trên màn hình (được đặt trong CanvasLayer riêng)
        SetupControlUI();

        // 2. Tự động sinh nhân vật đang được chọn trong GameManager tại vị trí xuất phát
        SpawnCharacter(GameManager.SelectedCharacterIndex, SpawnPosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Đếm lùi thời gian hồi chiêu của phím Tab
        if (_switchCooldownTimer > 0f)
        {
            _switchCooldownTimer -= (float)delta;
        }
    }

    /// <summary>
    /// Tạo ra CanvasLayer độc lập để hiển thị ControlUI.
    /// Giúp UI luôn cố định trên màn hình camera và nằm đè lên trên toàn bộ quái và bản đồ.
    /// </summary>
    protected virtual void SetupControlUI()
    {
        if (ControlUiScene != null)
        {
            var canvasLayer = new CanvasLayer();
            canvasLayer.Name = "UICanvasLayer";
            // Đặt Layer = 100 để đảm bảo UI luôn hiển thị ở tầng trên cùng của viewport
            canvasLayer.Layer = 100;
            
            var uiInstance = ControlUiScene.Instantiate<Control>();
            canvasLayer.AddChild(uiInstance);
            AddChild(canvasLayer);
        }
    }

    /// <summary>
    /// Bắt sự kiện phím toàn cục chưa được xử lý bởi GUI.
    /// Nhận phím Tab để hoán đổi nhân vật.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Tab)
            {
                // Chỉ cho đổi nhân vật khi đã hết thời gian hồi chiêu
                if (_switchCooldownTimer <= 0f)
                {
                    SwitchCharacter();
                }
            }
        }
    }

    /// <summary>
    /// Thực hiện hoán đổi sang nhân vật tiếp theo trong danh sách:
    /// - Sao chép toàn bộ trạng thái hiện tại (vị trí, vận tốc, hướng mặt, mana, số lần nhảy, bay/lướt).
    /// - Hủy an toàn nhân vật cũ.
    /// - Tạo nhân vật mới và nạp lại toàn bộ trạng thái để quá trình chơi liền mạch, không bị khựng đà.
    /// </summary>
    protected virtual void SwitchCharacter()
    {
        // Tính toán index của nhân vật kế tiếp theo vòng tròn (0 -> 1 -> 0)
        int nextIndex = (GameManager.SelectedCharacterIndex + 1) % GameManager.CharacterScenes.Length;
        GameManager.SelectedCharacterIndex = nextIndex;

        Vector2 currentPos = _currentCharacter != null ? _currentCharacter.GlobalPosition : SpawnPosition;
        
        // 1. Lưu lại toàn bộ trạng thái của nhân vật cũ
        Vector2 currentVelocity = _currentCharacter != null ? _currentCharacter.Velocity : Vector2.Zero;
        int currentFacingDirection = 1;
        int remainingJumps = 0;
        float currentMana = 100f;
        bool isFlying = false;
        bool hasAirDashed = false;
        float flyTimer = 0f;
        float runTimer = 0f;
        float swimTimer = 0f;

        if (_currentCharacter is BaseCharacter baseChar)
        {
            currentFacingDirection = baseChar.FacingDirection;
            remainingJumps = baseChar.RemainingJumps;
            currentMana = baseChar.CurrentMana;
            isFlying = baseChar.IsFlyingState;
            hasAirDashed = baseChar.HasAirDashed;
            flyTimer = baseChar.FlyDurationTimer;
            runTimer = baseChar.RunDurationTimer;
            swimTimer = baseChar.SwimDurationTimer;
        }
        else if (_currentCharacter != null)
        {
            var sprite = _currentCharacter.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            if (sprite != null && sprite.FlipH)
            {
                currentFacingDirection = -1;
            }
        }

        // 2. Vô hiệu hóa va chạm và giải phóng nhân vật cũ khỏi bộ nhớ
        if (_currentCharacter != null && IsInstanceValid(_currentCharacter))
        {
            var col = _currentCharacter.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (col != null) col.SetDeferred("disabled", true);

            RemoveChild(_currentCharacter);
            _currentCharacter.QueueFree();
            _currentCharacter = null;
        }

        // 3. Khởi tạo nhân vật mới tại đúng vị trí, duy trì nguyên vẹn vận tốc đà quán tính (momentum)
        SpawnCharacter(nextIndex, currentPos, currentVelocity, currentFacingDirection);

        // 4. Kế thừa các biến trạng thái quan trọng vào nhân vật mới để chống hack/bug nhảy vô hạn
        if (_currentCharacter is BaseCharacter newChar)
        {
            newChar.RemainingJumps = remainingJumps;
            newChar.CurrentMana = currentMana;
            newChar.IsFlyingState = isFlying;
            newChar.HasAirDashed = hasAirDashed;
            newChar.FlyDurationTimer = flyTimer;
            newChar.RunDurationTimer = runTimer;
            newChar.SwimDurationTimer = swimTimer;
        }

        // 5. Kích hoạt hồi chiêu đổi nhân vật
        _switchCooldownTimer = SwitchCooldown;
    }

    /// <summary>
    /// Nạp Scene nhân vật từ đường dẫn tài nguyên, đặt tọa độ, vận tốc và đưa vào cây node.
    /// </summary>
    protected virtual void SpawnCharacter(int charIndex, Vector2 position, Vector2 velocity = default, int facingDirection = 1)
    {
        string scenePath = GameManager.CharacterScenes[charIndex];
        var charScene = GD.Load<PackedScene>(scenePath);
        _currentCharacter = charScene.Instantiate<CharacterBody2D>();

        _currentCharacter.GlobalPosition = position;
        _currentCharacter.Velocity = velocity;

        // Giữ nguyên hướng nhìn đã lưu từ nhân vật trước
        if (_currentCharacter is BaseCharacter baseChar)
        {
            baseChar.FacingDirection = facingDirection;
        }
        else
        {
            var sprite = _currentCharacter.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            if (sprite != null)
            {
                sprite.FlipH = (facingDirection == -1);
            }
        }

        _currentCharacter.SetPhysicsProcess(true);
        AddChild(_currentCharacter);

        // Đảm bảo Sprite luôn phát animation ban đầu mượt mà
        var animSprite = _currentCharacter.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animSprite != null)
        {
            animSprite.Play("idle");
        }
    }

    /// <summary>
    /// Phương thức công khai được gọi bởi các Portal khi người chơi vừa bước qua cổng dịch chuyển.
    /// Giúp dịch chuyển nhân vật tới tọa độ chỉ định của level mới.
    /// </summary>
    public void SpawnCharacterAt(int charIndex, Vector2 position, int facingDirection = 1)
    {
        if (_currentCharacter != null && IsInstanceValid(_currentCharacter))
        {
            RemoveChild(_currentCharacter);
            _currentCharacter.QueueFree();
            _currentCharacter = null;
        }

        SpawnCharacter(charIndex, position, Vector2.Zero, facingDirection);
    }
}

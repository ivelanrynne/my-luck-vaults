using Godot;

/// <summary>
/// Cổng dịch chuyển nền tảng (Base Portal).
/// Nhiệm vụ:
/// 1. Tự động thiết lập thứ tự hiển thị (ZIndex = 0) để quái (Z=5) và player (Z=10) luôn đi đè lên trên hoạt ảnh cổng.
/// 2. Bắt va chạm Area2D khi Player bước vào vùng kích hoạt.
/// 3. Chuyển Scene sang màn chơi đích (TargetLevelPath) và đặt người chơi tại tọa độ chỉ định (TargetPosition).
/// </summary>
public partial class BasePortal : Node2D
{
    // Đường dẫn Scene đích cần chuyển tới (ví dụ: "res://scenes/EmeraldCloverMeadow.tscn")
    [Export]
    public string TargetLevelPath { get; set; } = string.Empty;

    // Tọa độ xuất hiện của Player trong màn chơi mới sau khi bước qua cổng
    [Export]
    public Vector2 TargetPosition = new Vector2(300, 300);

    // Cờ chống kích hoạt chuyển scene nhiều lần liên tiếp
    private bool _isTransitioning;

    public override void _Ready()
    {
        // Đặt ZIndex = 0: Cổng luôn nằm ở lớp nền sâu nhất so với các thực thể sống (Quái Z=5, Player Z=10)
        ZIndex = 0;
        var animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animatedSprite != null)
        {
            animatedSprite.ZIndex = 0;
        }

        // Lấy vùng va chạm Area2D để lắng nghe người chơi bước vào
        var area = GetNodeOrNull<Area2D>("Area2D");
        if (area == null)
        {
            GD.PushError($"{Name}: Không tìm thấy Area2D để kích hoạt Portal.");
            return;
        }

        // CollisionMask = 3 (Nhị phân 0011 = Layer 1 | Layer 2): Nhận diện cả vật thể sàn và Player (Layer 2)
        area.CollisionMask = 3;
        area.BodyEntered += OnBodyEntered;

        // Bắt đầu phát animation xoay cổng
        PlayAnimation();
    }

    /// <summary>
    /// Phát hoạt ảnh chuyển động của cổng nếu có animation "activated"
    /// </summary>
    protected virtual void PlayAnimation()
    {
        var animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animatedSprite != null && animatedSprite.SpriteFrames != null)
        {
            if (animatedSprite.SpriteFrames.HasAnimation("activated"))
            {
                animatedSprite.Play("activated");
            }
        }
    }

    /// <summary>
    /// Xử lý sự kiện khi có vật thể bước vào vùng cổng:
    /// Kiểm tra xem có phải là người chơi hay không -> Đổi cảnh -> Đặt người chơi tại vị trí mới.
    /// </summary>
    private async void OnBodyEntered(Node2D body)
    {
        // Bỏ qua nếu đang trong quá trình chuyển cảnh hoặc vật thể không phải là nhân vật di chuyển
        if (_isTransitioning || body is not CharacterBody2D character)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetLevelPath))
        {
            GD.PushError($"{Name}: Chưa cấu hình TargetLevelPath.");
            return;
        }

        var targetScene = GD.Load<PackedScene>(TargetLevelPath);
        if (targetScene == null)
        {
            GD.PushError($"{Name}: Không thể tải level '{TargetLevelPath}'.");
            return;
        }

        // Lưu lại hướng mặt hiện tại của nhân vật để khi sang map mới vẫn nhìn đúng hướng
        int facingDirection = GetFacingDirection(character);
        _isTransitioning = true;
        PlayAnimation();

        // Tiến hành chuyển sang Scene màn chơi mới
        var error = GetTree().ChangeSceneToPacked(targetScene);
        if (error != Error.Ok)
        {
            _isTransitioning = false;
            GD.PushError($"{Name}: Không thể chuyển sang level '{TargetLevelPath}': {error}.");
            return;
        }

        // Đợi tín hiệu SceneChanged hoàn tất để đảm bảo node map mới đã được nạp vào cây
        await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);

        // Sinh nhân vật tại tọa độ TargetPosition của level mới
        if (GetTree().CurrentScene is BaseLevel targetLevel)
        {
            targetLevel.SpawnCharacterAt(GameManager.SelectedCharacterIndex, TargetPosition, facingDirection);
        }
        else
        {
            GD.PushError($"{Name}: Scene đích không kế thừa BaseLevel.");
        }
    }

    /// <summary>
    /// Xác định hướng nhìn của nhân vật dựa vào thuộc tính FacingDirection hoặc thuộc tính lật FlipH của Sprite.
    /// </summary>
    private static int GetFacingDirection(CharacterBody2D character)
    {
        if (character is BaseCharacter baseCharacter)
        {
            return baseCharacter.FacingDirection < 0 ? -1 : 1;
        }

        var sprite = character.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        return sprite != null && sprite.FlipH ? -1 : 1;
    }
}

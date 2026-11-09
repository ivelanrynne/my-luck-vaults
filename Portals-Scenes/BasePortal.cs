using Godot;

public partial class BasePortal : Node2D
{
    [Export]
    public string TargetLevelPath { get; set; } = string.Empty;

    [Export]
    public Vector2 TargetPosition = new Vector2(300, 300);

    private bool _isTransitioning;

    public override void _Ready()
    {
        // Đặt Portal ở ZIndex = 0 (thấp hơn Quái ZIndex = 5 và Player ZIndex = 10)
        // để khi Quái hoặc Người chơi đi vào cổng, hoạt ảnh cổng luôn nằm phía sau
        ZIndex = 0;
        var animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animatedSprite != null)
        {
            animatedSprite.ZIndex = 0;
        }

        var area = GetNodeOrNull<Area2D>("Area2D");
        if (area == null)
        {
            GD.PushError($"{Name}: Không tìm thấy Area2D để kích hoạt Portal.");
            return;
        }

        // Đảm bảo Area2D có thể phát hiện được Player (Layer 2) và các body khác (Mask = 1 | 2 = 3)
        area.CollisionMask = 3;
        area.BodyEntered += OnBodyEntered;
        PlayAnimation();
    }

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

    private async void OnBodyEntered(Node2D body)
    {
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

        int facingDirection = GetFacingDirection(character);
        _isTransitioning = true;
        PlayAnimation();

        var error = GetTree().ChangeSceneToPacked(targetScene);
        if (error != Error.Ok)
        {
            _isTransitioning = false;
            GD.PushError($"{Name}: Không thể chuyển sang level '{TargetLevelPath}': {error}.");
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
        if (GetTree().CurrentScene is BaseLevel targetLevel)
        {
            targetLevel.SpawnCharacterAt(GameManager.SelectedCharacterIndex, TargetPosition, facingDirection);
        }
        else
        {
            GD.PushError($"{Name}: Scene đích không kế thừa BaseLevel.");
        }
    }

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
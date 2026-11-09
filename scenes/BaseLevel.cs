using Godot;
using System;

public partial class BaseLevel : Node2D
{
    protected CharacterBody2D _currentCharacter;

    [Export]
    public Vector2 SpawnPosition = new Vector2(300, 250);

    [Export]
    public PackedScene ControlUiScene = GD.Load<PackedScene>("res://UserUI/ControlUI.tscn");

    [ExportGroup("Giới Hạn Đổi Nhân Vật")]
    [Export]
    public float SwitchCooldown { get; set; } = 1.0f; // Thời gian hồi chiêu khi ấn Tab đổi nhân vật (giây)

    protected float _switchCooldownTimer = 0f;

    public override void _Ready()
    {
        // Tự động spawn UI cố định trên màn hình (trừ MainMenu vì MainMenu không kế thừa BaseLevel)
        SetupControlUI();

        // Tự động spawn nhân vật đang chọn
        SpawnCharacter(GameManager.SelectedCharacterIndex, SpawnPosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_switchCooldownTimer > 0f)
        {
            _switchCooldownTimer -= (float)delta;
        }
    }

    protected virtual void SetupControlUI()
    {
        if (ControlUiScene != null)
        {
            var canvasLayer = new CanvasLayer();
            canvasLayer.Name = "UICanvasLayer";
            canvasLayer.Layer = 100; // Đặt layer cao để UI luôn nổi trên quái và map
            
            var uiInstance = ControlUiScene.Instantiate<Control>();
            canvasLayer.AddChild(uiInstance);
            AddChild(canvasLayer);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Bấm Tab để đổi nhân vật (có giới hạn cooldown chống spam lơ lửng)
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Tab)
            {
                if (_switchCooldownTimer <= 0f)
                {
                    SwitchCharacter();
                }
            }
        }
    }

    protected virtual void SwitchCharacter()
    {
        int nextIndex = (GameManager.SelectedCharacterIndex + 1) % GameManager.CharacterScenes.Length;
        GameManager.SelectedCharacterIndex = nextIndex;

        Vector2 currentPos = _currentCharacter != null ? _currentCharacter.GlobalPosition : SpawnPosition;
        
        // Lưu lại toàn bộ trạng thái của nhân vật cũ để truyền sang nhân vật mới
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

        // Tắt collision và giải phóng nhân vật cũ
        if (_currentCharacter != null && IsInstanceValid(_currentCharacter))
        {
            var col = _currentCharacter.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (col != null) col.SetDeferred("disabled", true);

            RemoveChild(_currentCharacter);
            _currentCharacter.QueueFree();
            _currentCharacter = null;
        }

        // Spawn nhân vật mới và truyền nguyên vẹn vận tốc đà rơi (momentum) cùng hướng nhìn
        SpawnCharacter(nextIndex, currentPos, currentVelocity, currentFacingDirection);

        // Kế thừa chính xác số lần nhảy còn lại, mana và trạng thái bay để ngăn bug nhảy vô hạn / lơ lửng
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

        // Kích hoạt thời gian hồi chiêu đổi nhân vật
        _switchCooldownTimer = SwitchCooldown;
    }

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

        // Đảm bảo animation chạy mượt mà ngay khi vừa switch
        var animSprite = _currentCharacter.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animSprite != null)
        {
            animSprite.Play("idle");
        }
    }

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

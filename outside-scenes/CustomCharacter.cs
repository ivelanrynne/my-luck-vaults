using Godot;
using System;

/// <summary>
/// Màn hình phòng chọn và xem trước nhân vật (Custom Character Scene).
/// Cho phép người chơi hoán đổi lựa chọn giữa Char01 và Char02, lưu vào GameManager,
/// và di chuyển sang màn chơi thử nghiệm hoặc quay lại Main Menu.
/// </summary>
public partial class CustomCharacter : Node2D
{
	[ExportGroup("Cấu Hình Đường Dẫn")]
	// Đường dẫn Scene màn chơi khi bấm nút Play từ phòng Custom
	[Export(PropertyHint.File, "*.tscn")]
	public string GameScenePath { get; set; } = "res://scenes/Royal/Royal.tscn";

	// Đường dẫn quay về Menu chính
	[Export(PropertyHint.File, "*.tscn")]
	public string MainMenuScenePath { get; set; } = "res://outside-scenes/main.tscn";

	private CharacterBody2D _char01;
	private CharacterBody2D _char02;

	public override void _Ready()
	{
		_char01 = GetNode<CharacterBody2D>("Char01");
		_char02 = GetNode<CharacterBody2D>("Char02");

		// Tắt xử lý vật lý để nhân vật đứng yên làm mẫu trưng bày, không bị rơi tự do
		_char01.SetPhysicsProcess(false);
		_char02.SetPhysicsProcess(false);

		// Vô hiệu hóa Camera2D riêng của từng nhân vật để tránh tranh chấp camera với scene
		var cam1 = _char01.GetNodeOrNull<Camera2D>("Camera2D");
		if (cam1 != null) cam1.Enabled = false;

		var cam2 = _char02.GetNodeOrNull<Camera2D>("Camera2D");
		if (cam2 != null) cam2.Enabled = false;

		// Cấu hình UIRoot trong phòng Custom Character: giữ Setting, ẩn nút Pause/Play để tránh nhầm lẫn
		var uiRoot = GetNodeOrNull<ControlUI>("UICanvasLayer/UIRoot")
				  ?? GetNodeOrNull<ControlUI>("UIRoot")
				  ?? FindChild("UIRoot", true, false) as ControlUI;
		if (uiRoot != null)
		{
			uiRoot.EnableGameState = false;
			if (uiRoot.GameStateManager != null)
			{
				uiRoot.GameStateManager.Visible = false;
			}
		}

		// Cập nhật hiệu ứng hiển thị ban đầu
		HighlightSelectedCharacter();
	}

	/// <summary>
	/// Chọn nhân vật theo chỉ số (0: Char01, 1: Char02) và lưu vào GameManager toàn cục
	/// </summary>
	public void SelectCharacter(int index)
	{
		GameManager.SelectedCharacterIndex = index;
		HighlightSelectedCharacter();
	}

	/// <summary>
	/// Hiệu ứng làm sáng nhân vật đang được chọn và làm mờ nhẹ nhân vật chưa chọn
	/// </summary>
	private void HighlightSelectedCharacter()
	{
		if (GameManager.SelectedCharacterIndex == 0)
		{
			_char01.Modulate = new Color(1, 1, 1, 1f);            // Đầy đủ độ sáng
			_char02.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.7f); // Mờ hơn
		}
		else
		{
			_char01.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.7f); // Mờ hơn
			_char02.Modulate = new Color(1, 1, 1, 1f);            // Đầy đủ độ sáng
		}
	}

	// Tín hiệu kết nối từ nút bấm chọn nhân vật 1 và 2
	public void _on_btn_select_char01_pressed() => SelectCharacter(0);
	public void _on_btn_select_char02_pressed() => SelectCharacter(1);

	/// <summary>
	/// Bắt đầu chơi: chuyển đến màn chơi đã cấu hình
	/// </summary>
	public void _on_btn_play_pressed()
	{
		if (!string.IsNullOrWhiteSpace(GameScenePath))
		{
			GetTree().ChangeSceneToFile(GameScenePath);
		}
	}

	/// <summary>
	/// Quay lại sảnh Menu chính
	/// </summary>
	public void _on_btn_goBack_pressed()
	{
		if (!string.IsNullOrWhiteSpace(MainMenuScenePath))
		{
			GetTree().ChangeSceneToFile(MainMenuScenePath);
		}
	}
}

using Godot;
using System;

public partial class CustomCharacter : Node2D
{
	private CharacterBody2D _char01;
	private CharacterBody2D _char02;
	public override void _Ready()
	{
		_char01 = GetNode<CharacterBody2D>("Char01");
		_char02 = GetNode<CharacterBody2D>("Char02");

		_char01.SetPhysicsProcess(false);
		_char02.SetPhysicsProcess(false);

		// Tắt Camera2D của 2 nhân vật khi ở trang Custom
		var cam1 = _char01.GetNodeOrNull<Camera2D>("Camera2D");
		if (cam1 != null) cam1.Enabled = false;
		var cam2 = _char02.GetNodeOrNull<Camera2D>("Camera2D");
		if (cam2 != null) cam2.Enabled = false;

		HighlightSelectedCharacter();
	}

	// Hàm chọn nhân vật theo index (0 là Char01, 1 là Char02)
	public void SelectCharacter(int index)
	{
		GameManager.SelectedCharacterIndex = index;
		HighlightSelectedCharacter();
		GD.Print($"Đã chọn nhân vật: index {index}");
	}
	// Hiệu ứng trực quan để biết đang chọn ai (ví dụ: làm sáng/mờ)
	private void HighlightSelectedCharacter()
	{
		if (GameManager.SelectedCharacterIndex == 0)
		{
			_char01.Modulate = new Color(1, 1, 1, 1f);       // Rõ nét
			_char02.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.7f); // Mờ hơn
		}
		else
		{
			_char01.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.7f); // Mờ hơn
			_char02.Modulate = new Color(1, 1, 1, 1f);       // Rõ nét
		}
	}
	// Nếu bạn tạo 2 nút Button để bấm chọn:
	public void _on_btn_select_char01_pressed() => SelectCharacter(0);
	public void _on_btn_select_char02_pressed() => SelectCharacter(1);

	public void _on_btn_play_pressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/Royal.tscn");
	}

	public void _on_btn_goBack_pressed()
	{
		GetTree().ChangeSceneToFile("res://outside-scenes/main.tscn");
	}
}

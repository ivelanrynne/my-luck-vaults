using Godot;
using System;

/// <summary>
/// Màn hình menu chính (Main Menu).
/// Điều hướng người chơi vào thế giới game, vào phòng tùy biến nhân vật hoặc thoát game.
/// Toàn bộ đường dẫn Scene đích được đặt thành [Export] để dễ dàng tùy chỉnh ngay trên Godot Inspector.
/// </summary>
public partial class MainMenu : Control
{
	[ExportGroup("Cấu Hình Đường Dẫn Chuyển Cảnh")]
	// Đường dẫn màn chơi khởi đầu khi bấm nút Play
	[Export(PropertyHint.File, "*.tscn")]
	public string StartGameScenePath { get; set; } = "res://scenes/Royal/Royal.tscn";

	// Đường dẫn Scene phòng tùy chỉnh nhân vật
	[Export(PropertyHint.File, "*.tscn")]
	public string CustomCharacterScenePath { get; set; } = "res://outside-scenes/custom_character.tscn";

	/// <summary>
	/// Bắt đầu game: chuyển sang bản đồ đầu tiên
	/// </summary>
	public void _on_btn_play_pressed()
	{
		if (!string.IsNullOrWhiteSpace(StartGameScenePath))
		{
			GetTree().ChangeSceneToFile(StartGameScenePath);
		}
	}

	/// <summary>
	/// Vào phòng chọn / tùy biến nhân vật
	/// </summary>
	public void _on_btn_custom_character_pressed()
	{
		if (!string.IsNullOrWhiteSpace(CustomCharacterScenePath))
		{
			GetTree().ChangeSceneToFile(CustomCharacterScenePath);
		}
	}

	/// <summary>
	/// Thoát khỏi trò chơi
	/// </summary>
	public void _on_btn_quit_pressed()
	{
		GetTree().Quit();
	}
}

using Godot;
using System;

/// <summary>
/// Node gốc chứa toàn bộ các hệ thống giao diện trong màn chơi (HUD Root).
/// Đóng vai trò là container quản lý tập trung các module UI:
/// - SettingButtonPopup: Quản lý cài đặt âm thanh, popup menu, giftcode, go back.
/// - GameState: Quản lý trạng thái vòng lặp trò chơi (Pause/Play) và làm mờ 5-10% màn chơi.
/// </summary>
public partial class ControlUI : Control
{
	[ExportGroup("Các Module Giao Diện Con")]
	[Export] public SettingButtonPopup SettingPopup;
	[Export] public GameState GameStateManager;

	[ExportGroup("Tùy Chọn Chế Độ")]
	// Bật/tắt nút Pause/Play (ví dụ: màn hình CustomCharacter không cần Pause thì tắt đi)
	[Export] public bool EnableGameState { get; set; } = true;

	public override void _Ready()
	{
		// Luôn chạy UI ngay cả khi game bị Pause
		ProcessMode = ProcessModeEnum.Always;

		// Để chuột đi xuyên qua các khoảng trống của container UI xuống game
		MouseFilter = MouseFilterEnum.Ignore;

		// 1. Tự động liên kết SettingPopup nếu chưa gán
		if (SettingPopup == null)
		{
			SettingPopup = GetNodeOrNull<SettingButtonPopup>("SettingButtonPopup");
		}

		// 2. Tự động liên kết GameStateManager nếu chưa gán
		if (GameStateManager == null)
		{
			GameStateManager = GetNodeOrNull<GameState>("GameState");
		}

		// 3. Cấu hình trạng thái hiển thị của GameStateManager
		if (GameStateManager != null)
		{
			GameStateManager.Visible = EnableGameState;
			GameStateManager.SetProcess(EnableGameState);
			GameStateManager.SetProcessUnhandledInput(EnableGameState);
		}
	}
}

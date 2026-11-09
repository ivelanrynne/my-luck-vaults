using Godot;
using System;

public partial class ControlUI : Control
{
	[Export] public BaseButton PauseButton;
	[Export] public BaseButton PlayButton;

	public override void _Ready()
	{
		// Đảm bảo ControlUI và toàn bộ node con của nó vẫn nhận input khi game bị Pause
		ProcessMode = ProcessModeEnum.Always;

		// Đảm bảo UIRoot không cản click chuột ở các vùng trống xuống game
		MouseFilter = MouseFilterEnum.Ignore;

		// 1. Tự động tìm PauseButton
		if (PauseButton == null)
		{
			PauseButton = GetNodeOrNull<BaseButton>("pauseBtn")
					   ?? GetNodeOrNull<BaseButton>("PauseButton");
		}

		// 2. Tự động tìm PlayButton
		if (PlayButton == null)
		{
			PlayButton = GetNodeOrNull<BaseButton>("playBtn")
					  ?? GetNodeOrNull<BaseButton>("PlayButton");
		}

		// 3. Tắt FocusMode để phím Space không bị chiếm
		if (PauseButton != null)
		{
			PauseButton.FocusMode = FocusModeEnum.None;
			PauseButton.Pressed += OnPauseButtonPressed;
		}

		if (PlayButton != null)
		{
			PlayButton.FocusMode = FocusModeEnum.None;
			PlayButton.Pressed += OnPlayButtonPressed;
		}

		// 4. Trạng thái khởi tạo: game đang chạy, hiển thị nút Pause, ẩn nút Play
		UpdatePauseUI();
	}

	/// <summary>
	/// Khi ấn nút Pause: dừng trò chơi và chuyển sang hiện nút Play
	/// </summary>
	public void OnPauseButtonPressed()
	{
		GetTree().Paused = true;
		UpdatePauseUI();
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Khi ấn nút Play: tiếp tục trò chơi và chuyển sang hiện nút Pause
	/// </summary>
	public void OnPlayButtonPressed()
	{
		GetTree().Paused = false;
		UpdatePauseUI();
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Cập nhật hiển thị nút Pause và Play theo trạng thái Paused hiện tại của Tree
	/// </summary>
	private void UpdatePauseUI()
	{
		bool isPaused = GetTree().Paused;

		if (PauseButton != null)
		{
			PauseButton.Visible = !isPaused;
		}

		if (PlayButton != null)
		{
			PlayButton.Visible = isPaused;
		}
	}
}

using Godot;
using System;

/// <summary>
/// Giao diện điều khiển chính trên màn hình (HUD / Control UI).
/// Quản lý chức năng Tạm dừng (Pause) và Tiếp tục chơi (Play).
/// 
/// ĐẶC ĐIỂM QUAN TRỌNG:
/// - ProcessMode = Always: Giúp UI vẫn hoạt động và bấm được nút kể cả khi toàn bộ game đang bị Pause (GetTree().Paused = true).
/// - MouseFilter = Ignore: Đảm bảo vùng trống của ControlUI không nuốt click chuột của người chơi xuống màn hình game.
/// - FocusMode = None: Ngăn nút cướp phím Space/Enter làm mất khả năng nhảy của nhân vật.
/// </summary>
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

		// 1. Tự động tìm PauseButton nếu chưa gán trên Inspector
		if (PauseButton == null)
		{
			PauseButton = GetNodeOrNull<BaseButton>("pauseBtn")
					   ?? GetNodeOrNull<BaseButton>("PauseButton");
		}

		// 2. Tự động tìm PlayButton nếu chưa gán trên Inspector
		if (PlayButton == null)
		{
			PlayButton = GetNodeOrNull<BaseButton>("playBtn")
					  ?? GetNodeOrNull<BaseButton>("PlayButton");
		}

		// 3. Tắt FocusMode để phím Space (nhảy) không bị nút bấm UI cướp mất
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
	/// Khi ấn nút Pause: dừng toàn bộ logic vật lý của trò chơi và chuyển sang hiện nút Play.
	/// Đồng thời giải phóng Focus để tránh bàn phím bị kẹt vào nút UI.
	/// </summary>
	public void OnPauseButtonPressed()
	{
		GetTree().Paused = true;
		UpdatePauseUI();
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Khi ấn nút Play: tiếp tục trò chơi và chuyển sang hiện nút Pause.
	/// </summary>
	public void OnPlayButtonPressed()
	{
		GetTree().Paused = false;
		UpdatePauseUI();
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Cập nhật hiển thị nút Pause và Play theo trạng thái Paused hiện tại của SceneTree.
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

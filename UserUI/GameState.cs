using Godot;
using System;

/// <summary>
/// Quản lý trạng thái vòng lặp trò chơi: Tạm dừng (Pause) và Tiếp tục (Play).
/// 
/// ĐẶC TÍNH QUAN TRỌNG:
/// - ProcessMode = ProcessModeEnum.Always: Cho phép xử lý và nhận phím/click chuột ngay cả khi trò chơi bị dừng (Paused).
/// - MouseFilter = MouseFilterEnum.Ignore: Ngăn vùng trống của Control nuốt click chuột của người chơi xuống màn hình game.
/// - Hỗ trợ cả Click chuột trực tiếp vào nút Pause/Play lẫn phím tắt [P] trên bàn phím.
/// - HIỆU ỨNG DIM MÀN HÌNH: Khi game Pause, tự động làm mờ/tối màn hình trò chơi khoảng 5-10% (trừ các User UI)
///   giúp người chơi tập trung và cảm nhận rõ nét trạng thái tạm dừng.
/// </summary>
public partial class GameState : Control
{
	[ExportGroup("Các Nút Điều Khiển")]
	[Export] public BaseButton PauseButton;
	[Export] public BaseButton PlayButton;

	[ExportGroup("Hiệu Ứng Mờ Nền Khi Pause")]
	// Màn mờ 5-10% phía sau UI khi trò chơi Pause
	[Export] public ColorRect PauseDimOverlay;
	// Tỷ lệ mờ (Mặc định 0.08f = 8%, nằm trong khoảng 5% - 10%)
	[Export(PropertyHint.Range, "0.05, 0.10, 0.01")]
	public float PauseDimOpacity { get; set; } = 0.08f;

	[ExportGroup("Phím Tắt (Hotkeys)")]
	// Phím tắt để bật/tắt tạm dừng game (Mặc định: Phím P)
	[Export] public Key PauseToggleKey { get; set; } = Key.P;

	public override void _Ready()
	{
		// Đảm bảo node luôn nhận input ngay cả khi game đang ở trạng thái Pause
		ProcessMode = ProcessModeEnum.Always;

		// Để chuột đi xuyên qua các vùng trống không chứa nút bấm
		MouseFilter = MouseFilterEnum.Ignore;

		// 1. Tự động liên kết nút Pause nếu chưa gán trên Inspector
		if (PauseButton == null)
		{
			PauseButton = GetNodeOrNull<BaseButton>("pauseBtn")
					   ?? GetNodeOrNull<BaseButton>("PauseButton");
		}

		// 2. Tự động liên kết nút Play nếu chưa gán trên Inspector
		if (PlayButton == null)
		{
			PlayButton = GetNodeOrNull<BaseButton>("playBtn")
					  ?? GetNodeOrNull<BaseButton>("PlayButton");
		}

		// 3. Khởi tạo màn mờ 5-10% (PauseDimOverlay) nếu chưa có
		SetupPauseDimOverlay();

		// 4. Tắt FocusMode trên các nút để phím Space (nhảy) không bị cướp
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

		// 5. Đồng bộ hóa trạng thái hiển thị của nút ngay khi khởi tạo
		UpdatePauseUI();
	}

	/// <summary>
	/// Tạo hoặc cấu hình màn đen mờ 5-10% phủ gameplay khi Pause
	/// </summary>
	private void SetupPauseDimOverlay()
	{
		if (PauseDimOverlay == null)
		{
			// Kiểm tra ở parent (ControlUI) hoặc ở chính node này
			PauseDimOverlay = GetParent()?.GetNodeOrNull<ColorRect>("PauseDimOverlay")
						   ?? GetNodeOrNull<ColorRect>("PauseDimOverlay");
		}

		if (PauseDimOverlay == null)
		{
			PauseDimOverlay = new ColorRect();
			PauseDimOverlay.Name = "PauseDimOverlay";
			PauseDimOverlay.Color = new Color(0, 0, 0, PauseDimOpacity);
			PauseDimOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
			PauseDimOverlay.MouseFilter = MouseFilterEnum.Ignore; // Không chặn tương tác chuột

			// Thêm vào parent (ControlUI) ở vị trí đầu tiên (dưới cùng) để nằm sau toàn bộ UI
			if (GetParent() is Control parentCtrl)
			{
				parentCtrl.AddChild(PauseDimOverlay);
				parentCtrl.MoveChild(PauseDimOverlay, 0);
			}
			else
			{
				AddChild(PauseDimOverlay);
				MoveChild(PauseDimOverlay, 0);
			}
		}

		PauseDimOverlay.Visible = GetTree().Paused;
	}

	/// <summary>
	/// Lắng nghe phím tắt toàn cục (Phím P) để chuyển đổi qua lại giữa Pause và Play.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == PauseToggleKey)
			{
				TogglePauseState();
				GetViewport()?.SetInputAsHandled(); // Đánh dấu sự kiện đã được xử lý xong
			}
		}
	}

	/// <summary>
	/// Đảo ngược trạng thái Pause hiện tại của trò chơi.
	/// </summary>
	public void TogglePauseState()
	{
		bool willBePaused = !GetTree().Paused;
		GetTree().Paused = willBePaused;
		UpdatePauseUI();
		GetViewport()?.GuiReleaseFocus(); // Giải phóng tiêu điểm tránh kẹt phím
	}

	/// <summary>
	/// Sự kiện khi người chơi click vào nút Pause.
	/// </summary>
	public void OnPauseButtonPressed()
	{
		GetTree().Paused = true;
		UpdatePauseUI();
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Sự kiện khi người chơi click vào nút Play.
	/// </summary>
	public void OnPlayButtonPressed()
	{
		GetTree().Paused = false;
		UpdatePauseUI();
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Cập nhật trạng thái hiển thị ẩn/hiện của 2 nút PauseButton và PlayButton
	/// cùng hiệu ứng làm mờ gameplay 5-10%.
	/// </summary>
	public void UpdatePauseUI()
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

		// Bật / tắt màn làm mờ 5-10% gameplay
		if (PauseDimOverlay != null)
		{
			PauseDimOverlay.Visible = isPaused;
		}
	}
}

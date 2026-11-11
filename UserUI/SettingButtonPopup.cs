using Godot;
using System;

/// <summary>
/// Quản lý Popup cài đặt (Setting Popup) bao gồm:
/// - Mở/Đóng popup bảng cài đặt (hỗ trợ cả click nút Setting lẫn phím [ESC]).
/// - Tự động tạo và điều khiển màn đen làm mờ nền (DimBackground) chặn click chuột xuống gameplay.
/// - Nhúng module con SoundState để quản lý âm lượng và tắt/bật tiếng.
/// - Nhúng module con CodeRedeem để người chơi nạp giftcode.
/// - Nhúng module con BackToMenu để xác nhận quay về sảnh chính (hỗ trợ phím [L]).
/// </summary>
public partial class SettingButtonPopup : Control
{
	[ExportGroup("Các Nút & Panel Cơ Bản")]
	[Export] public BaseButton SettingButton;
	[Export] public Control PopupPanel;
	[Export] public BaseButton CloseButton;  // Nút X để đóng bảng
	[Export] public ColorRect DimBackground; // Màn đen mờ phía sau popup
	[Export] public Control BackToMenuNode;  // Tham chiếu BackToMenu (nút + popup con)
	[Export] public SoundState SoundStateNode; // Tham chiếu module âm thanh
	[Export] public CodeRedeem CodeRedeemNode; // Tham chiếu module nhập code

	[ExportGroup("Phím Tắt (Hotkeys)")]
	// Phím tắt để bật / tắt bảng Setting (Mặc định: Phím ESC)
	[Export] public Key SettingToggleKey { get; set; } = Key.Escape;

	public override void _Ready()
	{
		// Luôn xử lý UI ngay cả khi game bị Pause
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Ignore;

		// 1. Tự động tìm PopupPanel
		if (PopupPanel == null)
		{
			PopupPanel = GetNodeOrNull<Control>("popupPanel")
					  ?? GetNodeOrNull<Control>("PopupPanel");
		}

		// 1.1 Tự động tìm BackToMenu nếu có
		if (BackToMenuNode == null)
		{
			BackToMenuNode = GetNodeOrNull<Control>("popupPanel/ConfirmBackPopup")
						  ?? GetNodeOrNull<Control>("ConfirmBackPopup")
						  ?? GetNodeOrNull<Control>("BackToMenu")
						  ?? GetNodeOrNull<Control>("popupPanel/BackToMenu");
		}

		// 1.2 Tự động tìm SoundState
		if (SoundStateNode == null)
		{
			SoundStateNode = GetNodeOrNull<SoundState>("popupPanel/soundState")
						  ?? GetNodeOrNull<SoundState>("soundState")
						  ?? GetNodeOrNull<SoundState>("SoundState");
		}

		// 1.3 Tự động tìm CodeRedeem
		if (CodeRedeemNode == null)
		{
			CodeRedeemNode = GetNodeOrNull<CodeRedeem>("popupPanel/CodeRedeem")
						  ?? GetNodeOrNull<CodeRedeem>("CodeRedeem");
		}

		// 2. Tự động tìm DimBackground (hoặc tự tạo nếu chưa có)
		if (DimBackground == null)
		{
			DimBackground = GetNodeOrNull<ColorRect>("DimBackground")
						 ?? GetNodeOrNull<ColorRect>("dimBackground");
		}

		if (DimBackground == null)
		{
			DimBackground = new ColorRect();
			DimBackground.Name = "DimBackground";
			DimBackground.Color = new Color(0, 0, 0, 0.6f); // Đen mờ 60%
			DimBackground.SetAnchorsPreset(LayoutPreset.FullRect);
			DimBackground.MouseFilter = MouseFilterEnum.Stop; // Chặn click xuyên qua xuống game

			AddChild(DimBackground);
			if (PopupPanel != null)
			{
				MoveChild(DimBackground, PopupPanel.GetIndex());
			}
		}

		// 3. Tự động tìm SettingButton
		if (SettingButton == null)
		{
			SettingButton = GetNodeOrNull<BaseButton>("settingBtn")
						 ?? GetNodeOrNull<BaseButton>("SettingButton")
						 ?? GetNodeOrNull<BaseButton>("triggerBtn");
		}

		// Đảm bảo nút SettingButton luôn nằm trên cùng để luôn click được
		if (SettingButton != null)
		{
			MoveChild(SettingButton, -1);
			SettingButton.Pressed += OnSettingButtonPressed;
		}

		// 4. Tự động tìm Nút X (CloseButton / exitPopup)
		if (CloseButton == null && PopupPanel != null)
		{
			CloseButton = PopupPanel.GetNodeOrNull<BaseButton>("exitPopup")
					   ?? PopupPanel.GetNodeOrNull<BaseButton>("closeBtn")
					   ?? PopupPanel.GetNodeOrNull<BaseButton>("CloseButton");
		}
		if (CloseButton == null)
		{
			CloseButton = GetNodeOrNull<BaseButton>("exitPopup")
					   ?? GetNodeOrNull<BaseButton>("closeBtn")
					   ?? GetNodeOrNull<BaseButton>("CloseButton");
		}

		if (CloseButton != null)
		{
			CloseButton.Pressed += OnCloseButtonPressed;
		}

		// 5. Mặc định ẩn popup và màn đen
		SetPopupVisible(false);

		// 6. Tắt FocusMode trên toàn bộ các nút UI để phím Space không bị chiếm dụng
		RemoveAllControlFocus(this);
	}

	/// <summary>
	/// Đệ quy tắt FocusMode của tất cả các Button và Control (trừ ô nhập text LineEdit)
	/// để tránh việc phím Space hay Enter vô tình kích hoạt lại nút vừa bấm.
	/// </summary>
	private void RemoveAllControlFocus(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is BaseButton btn)
			{
				btn.FocusMode = FocusModeEnum.None;
			}
			else if (child is Slider slider)
			{
				slider.FocusMode = FocusModeEnum.None;
			}
			else if (child is Control ctrl && !(child is LineEdit))
			{
				ctrl.FocusMode = FocusModeEnum.None;
			}

			if (child.GetChildCount() > 0)
			{
				RemoveAllControlFocus(child);
			}
		}
	}

	/// <summary>
	/// Bật / Tắt trạng thái hiển thị của cả Popup và Màn đen làm mờ
	/// </summary>
	public void SetPopupVisible(bool visible)
	{
		MouseFilter = MouseFilterEnum.Ignore;

		if (PopupPanel != null)
		{
			PopupPanel.Visible = visible;
			PopupPanel.MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		}
		if (DimBackground != null)
		{
			DimBackground.Visible = visible;
			DimBackground.MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		}
		if (BackToMenuNode != null)
		{
			BackToMenuNode.Visible = visible;
			BackToMenuNode.MouseFilter = MouseFilterEnum.Ignore;

			// Khi đóng Setting, nếu popup xác nhận bên trong BackToMenu đang mở thì cũng reset đóng nó lại
			if (!visible && BackToMenuNode is BackToMenu btm)
			{
				btm.SetPopupVisible(false);
			}
		}

		// Giải phóng focus của chuột/phím khỏi UI ngay lập tức
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Bật / Tắt bảng Setting khi bấm nút Setting
	/// </summary>
	public void OnSettingButtonPressed()
	{
		bool isCurrentlyVisible = PopupPanel != null && PopupPanel.Visible;
		SetPopupVisible(!isCurrentlyVisible);
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Đóng bảng Setting khi bấm nút X
	/// </summary>
	public void OnCloseButtonPressed()
	{
		SetPopupVisible(false);
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Lắng nghe phím ESC và phím L:
	/// - Phím [ESC]: Bật/Tắt bảng Setting tiện lợi mà không cần dùng chuột.
	/// - Phím [L]: Khi đang mở bảng Setting, nhấn [L] sẽ mở bảng xác nhận Go Back Menu.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			bool isSettingOpen = PopupPanel != null && PopupPanel.Visible;

			// 1. Phím [L]: Bật popup Go Back khi bảng Setting đang hiển thị
			if (keyEvent.Keycode == Key.L && isSettingOpen)
			{
				if (BackToMenuNode is BackToMenu btm)
				{
					if (btm.PopupPanel == null || !btm.PopupPanel.Visible)
					{
						btm.OnGoBackButtonPressed();
						GetViewport()?.SetInputAsHandled();
						return;
					}
				}
			}

			// 2. Phím [ESC]: Đóng/Bật Setting
			if (keyEvent.Keycode == SettingToggleKey)
			{
				// Nếu popup xác nhận Back to Menu con đang mở thì để BackToMenu đóng trước
				if (BackToMenuNode is BackToMenu btm && btm.PopupPanel != null && btm.PopupPanel.Visible)
				{
					return; // Nhường quyền xử lý ESC cho BackToMenu
				}

				// Đảo ngược trạng thái hiển thị của SettingPopup
				OnSettingButtonPressed();
				GetViewport()?.SetInputAsHandled();
			}
		}
	}
}

using Godot;
using System;

public partial class BackToMenu : Control
{
	[Export] public BaseButton GoBackButton; // Nút kích hoạt mở popup ("Back to Menu")
	[Export] public Control PopupPanel;      // Khung Panel bảng xác nhận
	[Export] public BaseButton ConfirmButton; // Nút xác nhận về Menu
	[Export] public BaseButton CancelButton;  // Nút hủy bỏ
	[Export] public BaseButton CloseButton;   // Nút X ở góc bảng
	[Export] public ColorRect DimBackground;  // Màn đen mờ phía sau popup
	[Export] public CanvasItem BackHomeIcon;  // Icon ngôi nhà backHomeIcon

	[Export] public string MainMenuScenePath = "res://outside-scenes/main.tscn";

	public override void _Ready()
	{
		// Đảm bảo chính node container này KHÔNG BAO GIỜ chặn chuột ở vùng trống
		MouseFilter = MouseFilterEnum.Ignore;

		// 1. Tự động tìm PopupPanel
		if (PopupPanel == null)
		{
			PopupPanel = GetNodeOrNull<Control>("Panel")
					  ?? GetNodeOrNull<Control>("popupPanel")
					  ?? GetNodeOrNull<Control>("PopupPanel");
		}

		// 2. Tự động tìm DimBackground (hoặc tự tạo nếu chưa có)
		if (DimBackground == null)
		{
			DimBackground = GetNodeOrNull<ColorRect>("DimBackground")
						 ?? GetNodeOrNull<ColorRect>("dimBackground");
		}

		// Tạo DimBackground phủ toàn màn hình với hiệu ứng đen mờ 60%
		if (DimBackground == null)
		{
			DimBackground = new ColorRect();
			DimBackground.Name = "DimBackground";
			DimBackground.Color = new Color(0, 0, 0, 0.6f);
			DimBackground.SetAnchorsPreset(LayoutPreset.FullRect);
			DimBackground.MouseFilter = MouseFilterEnum.Stop; // Chặn click chuột xuyên qua xuống game

			AddChild(DimBackground);
			if (PopupPanel != null)
			{
				MoveChild(DimBackground, PopupPanel.GetIndex());
			}
		}

		// 3. Tự động tìm nút kích hoạt goBackBtn
		if (GoBackButton == null)
		{
			GoBackButton = GetNodeOrNull<BaseButton>("goBackBtn")
						?? GetNodeOrNull<BaseButton>("GoBackButton")
						?? GetNodeOrNull<BaseButton>("btn_goBack");
		}

		// 3.1 Tự động tìm backHomeIcon (Sprite2D hoặc Control/TextureRect)
		if (BackHomeIcon == null)
		{
			BackHomeIcon = GetNodeOrNull<CanvasItem>("backHomeIcon")
						?? GetNodeOrNull<CanvasItem>("BackHomeIcon")
						?? GetNodeOrNull<CanvasItem>("homeIcon");
		}

		// Mặc định ẩn icon ngôi nhà, chỉ hiện khi hover/pressed vào nút Back to Menu
		if (BackHomeIcon != null)
		{
			BackHomeIcon.Visible = false;
		}

		// Kết nối sự kiện Hover & Pressed của goBackBtn để hiện/ẩn backHomeIcon
		if (GoBackButton != null)
		{
			GoBackButton.MouseEntered += OnGoBackButtonHoverStarted;
			GoBackButton.MouseExited += OnGoBackButtonHoverEnded;
			GoBackButton.ButtonDown += OnGoBackButtonHoverStarted;
			GoBackButton.ButtonUp += OnGoBackButtonHoverEnded;
		}

		// 4. Tự động tìm Nút Confirm ("confirm")
		if (ConfirmButton == null && PopupPanel != null)
		{
			ConfirmButton = PopupPanel.GetNodeOrNull<BaseButton>("confirm")
						 ?? PopupPanel.GetNodeOrNull<BaseButton>("ConfirmButton")
						 ?? PopupPanel.GetNodeOrNull<BaseButton>("btn_confirm");
		}
		if (ConfirmButton == null)
		{
			ConfirmButton = GetNodeOrNull<BaseButton>("confirm");
		}

		// 5. Tự động tìm Nút Cancel ("confirm2" hoặc "cancel")
		if (CancelButton == null && PopupPanel != null)
		{
			CancelButton = PopupPanel.GetNodeOrNull<BaseButton>("confirm2")
						?? PopupPanel.GetNodeOrNull<BaseButton>("cancel")
						?? PopupPanel.GetNodeOrNull<BaseButton>("CancelButton")
						?? PopupPanel.GetNodeOrNull<BaseButton>("btn_cancel");
		}
		if (CancelButton == null)
		{
			CancelButton = GetNodeOrNull<BaseButton>("confirm2")
						?? GetNodeOrNull<BaseButton>("cancel");
		}

		// 6. Tự động tìm Nút X ("exitPopup" hoặc con của PopupPanel)
		if (CloseButton == null)
		{
			CloseButton = GetNodeOrNull<BaseButton>("exitPopup")
					   ?? PopupPanel?.GetNodeOrNull<BaseButton>("exitPopup")
					   ?? PopupPanel?.GetNodeOrNull<BaseButton>("closeBtn")
					   ?? PopupPanel?.GetNodeOrNull<BaseButton>("CloseButton");
		}

		// Đảm bảo nút X và PopupPanel nằm trên DimBackground
		if (CloseButton != null && DimBackground != null)
		{
			MoveChild(CloseButton, -1);
		}

		// 7. Mặc định ẩn popup xác nhận và màn đen
		SetPopupVisible(false);

		// 8. Tắt FocusMode trên toàn bộ các nút UI để phím Space không bị chiếm dụng
		RemoveAllControlFocus(this);

		// 9. Kết nối sự kiện Click
		if (GoBackButton != null)
		{
			GoBackButton.Pressed += OnGoBackButtonPressed;
		}

		if (ConfirmButton != null)
		{
			ConfirmButton.Pressed += OnConfirmButtonPressed;
		}

		if (CancelButton != null)
		{
			CancelButton.Pressed += OnCancelButtonPressed;
		}

		if (CloseButton != null)
		{
			CloseButton.Pressed += OnCloseButtonPressed;
		}
	}

	/// <summary>
	/// Khi hover chuột hoặc ấn vào nút Back to Menu: Hiện icon ngôi nhà backHomeIcon
	/// </summary>
	private void OnGoBackButtonHoverStarted()
	{
		if (BackHomeIcon != null)
		{
			BackHomeIcon.Visible = true;
		}
	}

	/// <summary>
	/// Khi chuột rời khỏi nút Back to Menu: Ẩn icon ngôi nhà backHomeIcon
	/// </summary>
	private void OnGoBackButtonHoverEnded()
	{
		// Nếu nút vẫn đang ở trạng thái hover hoặc pressed thì chưa ẩn
		if (GoBackButton != null && (GoBackButton.IsHovered() || GoBackButton.ButtonPressed))
		{
			return;
		}

		if (BackHomeIcon != null)
		{
			BackHomeIcon.Visible = false;
		}
	}

	/// <summary>
	/// Đệ quy tắt FocusMode của tất cả các Button và Control để tránh việc phím Space/Enter vô tình kích hoạt lại nút.
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
	/// Bật / Tắt trạng thái hiển thị của cả Popup xác nhận và Màn đen làm mờ
	/// </summary>
	public void SetPopupVisible(bool visible)
	{
		// Node root luôn để Ignore để không che khuất hay nuốt chuột của Setting phía dưới
		MouseFilter = MouseFilterEnum.Ignore;

		if (PopupPanel != null)
		{
			PopupPanel.Visible = visible;
			PopupPanel.MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		}
		if (CloseButton != null)
		{
			CloseButton.Visible = visible;
		}
		if (DimBackground != null)
		{
			DimBackground.Visible = visible;
			DimBackground.MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		}

		// Khi popup mở hoặc đóng, đảm bảo icon backHomeIcon ẩn đi nếu không hover
		if (!visible && BackHomeIcon != null)
		{
			BackHomeIcon.Visible = false;
		}

		// Giải phóng focus của chuột/phím khỏi UI ngay lập tức
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Bấm nút Back to Menu thì mở popup xác nhận
	/// </summary>
	public void OnGoBackButtonPressed()
	{
		SetPopupVisible(true);
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Bấm Confirm: chuyển về scene MainMenu chính xác
	/// </summary>
	public void OnConfirmButtonPressed()
	{
		GetViewport()?.GuiReleaseFocus();

		// Nếu game đang Pause thì bỏ Pause trước khi chuyển scene
		if (GetTree().Paused)
		{
			GetTree().Paused = false;
		}

		if (!string.IsNullOrWhiteSpace(MainMenuScenePath))
		{
			GetTree().ChangeSceneToFile(MainMenuScenePath);
		}
		else
		{
			GetTree().ChangeSceneToFile("res://outside-scenes/main.tscn");
		}
	}

	/// <summary>
	/// Tương thích ngược nếu có signal kết nối ConfirmGoBack
	/// </summary>
	public virtual void ConfirmGoBack()
	{
		OnConfirmButtonPressed();
	}

	/// <summary>
	/// Bấm Cancel: đóng popup xác nhận
	/// </summary>
	public void OnCancelButtonPressed()
	{
		SetPopupVisible(false);
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Bấm nút X: đóng popup xác nhận
	/// </summary>
	public void OnCloseButtonPressed()
	{
		SetPopupVisible(false);
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Bấm ESC trên bàn phím cũng tự động đóng popup nếu đang mở
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Escape && PopupPanel != null && PopupPanel.Visible)
			{
				OnCloseButtonPressed();
				GetViewport().SetInputAsHandled();
			}
		}
	}
}

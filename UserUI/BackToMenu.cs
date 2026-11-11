using Godot;
using System;

/// <summary>
/// Module xác nhận quay trở về sảnh chính (Back To Menu Popup).
/// Quản lý:
/// - Nút bấm kích hoạt mở popup xác nhận "Back to Menu" (hỗ trợ cả click lẫn phím tắt [L]).
/// - Màn đen mờ (DimBackground) phủ toàn màn hình khi mở popup để làm tối nền phía sau giống setting popup.
/// - ĐẶC BIỆT: Hỗ trợ đóng popup khi người chơi CHỦ ĐỘNG CLICK RA NGOÀI bảng (vào màn đen), hoặc ấn phím [ESC], hoặc ấn nút [X]/Cancel.
/// - Hiệu ứng hiển thị Icon ngôi nhà (BackHomeIcon):
///   + Hiện khi hover/rê chuột hoặc ấn vào nút.
///   + Khi bảng popup xác nhận đang hiển thị, icon ngôi nhà luôn được giữ nguyên và chỉ biến mất khi ấn Esc hoặc nút X/Cancel hoặc click ra ngoài!
/// - Phím [Enter] được dùng để xác nhận Confirm quay về Menu ngay lập tức.
/// - Bảng xác nhận kèm 2 lựa chọn: Confirm (trở về MainMenu) và Cancel (hủy bỏ).
/// </summary>
public partial class BackToMenu : Control
{
	[ExportGroup("Các Nút & Panel Cơ Bản")]
	[Export] public BaseButton GoBackButton; // Nút kích hoạt mở popup ("Back to Menu")
	[Export] public Control PopupPanel;      // Khung Panel bảng xác nhận
	[Export] public BaseButton ConfirmButton; // Nút xác nhận về Menu
	[Export] public BaseButton CancelButton;  // Nút hủy bỏ
	[Export] public BaseButton CloseButton;   // Nút X ở góc bảng
	[Export] public ColorRect DimBackground;  // Màn đen mờ phía sau popup
	[Export] public CanvasItem BackHomeIcon;  // Icon ngôi nhà backHomeIcon

	[ExportGroup("Cấu Hình Đường Dẫn Chuyển Cảnh")]
	// Đường dẫn Scene sảnh chính khi người chơi xác nhận trở về
	[Export(PropertyHint.File, "*.tscn")]
	public string MainMenuScenePath = "res://outside-scenes/main.tscn";

	// Cờ theo dõi xem chuột có đang hover trực tiếp trên GoBackButton hay không
	private bool _isHoveringGoBack = false;

	public override void _Ready()
	{
		// Luôn xử lý UI ngay cả khi game bị Pause
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Ignore;

		// 1. Tự động tìm PopupPanel (hỗ trợ cả Panel, Panel2, popupPanel, PopupPanel)
		if (PopupPanel == null)
		{
			PopupPanel = GetNodeOrNull<Control>("Panel2")
					  ?? GetNodeOrNull<Control>("Panel")
					  ?? GetNodeOrNull<Control>("popupPanel")
					  ?? GetNodeOrNull<Control>("PopupPanel");
		}

		// 2. Tự động tìm DimBackground (hoặc tự tạo nếu chưa có)
		if (DimBackground == null)
		{
			DimBackground = GetNodeOrNull<ColorRect>("DimBackground")
						 ?? GetNodeOrNull<ColorRect>("dimBackground");
		}

		// Tạo DimBackground phủ toàn màn hình với hiệu ứng đen mờ nếu chưa có
		if (DimBackground == null)
		{
			DimBackground = new ColorRect();
			DimBackground.Name = "DimBackground";
			DimBackground.Color = new Color(0, 0, 0, 0.6f);
			DimBackground.MouseFilter = MouseFilterEnum.Stop; // Nhận click chuột để đóng khi bấm ra ngoài

			// Thêm DimBackground vào cây node
			AddChild(DimBackground);
			if (PopupPanel != null)
			{
				MoveChild(DimBackground, PopupPanel.GetIndex());
			}
		}

		// Cấu hình DimBackground: phủ toàn màn hình viewport (1152x648 hoặc theo kích thước cửa sổ)
		ConfigureDimBackground();

		// Kết nối sự kiện click chuột lên DimBackground (bấm ra ngoài bảng thì tắt popup)
		if (DimBackground != null)
		{
			DimBackground.GuiInput += OnDimBackgroundGuiInput;
		}

		// 3. Tự động tìm nút kích hoạt goBackBtn (hỗ trợ goBackBtn2, goBackBtn, GoBackButton, btn_goBack)
		if (GoBackButton == null)
		{
			GoBackButton = GetNodeOrNull<BaseButton>("goBackBtn2")
						?? GetNodeOrNull<BaseButton>("goBackBtn")
						?? GetNodeOrNull<BaseButton>("GoBackButton")
						?? GetNodeOrNull<BaseButton>("btn_goBack");
		}

		// 3.1 Tự động tìm backHomeIcon (tìm cả bên trong GoBackButton lẫn ở root)
		if (BackHomeIcon == null && GoBackButton != null)
		{
			BackHomeIcon = GoBackButton.GetNodeOrNull<CanvasItem>("backHomeIcon")
						?? GoBackButton.GetNodeOrNull<CanvasItem>("BackHomeIcon")
						?? GoBackButton.GetNodeOrNull<CanvasItem>("homeIcon");
		}
		if (BackHomeIcon == null)
		{
			BackHomeIcon = GetNodeOrNull<CanvasItem>("backHomeIcon")
						?? GetNodeOrNull<CanvasItem>("BackHomeIcon")
						?? GetNodeOrNull<CanvasItem>("homeIcon");
		}

		// Mặc định ẩn icon ngôi nhà khi bắt đầu
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

		// 6. Tự động tìm Nút X ("exitPopup" hoặc "closeBtn")
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
	/// Cấu hình tọa độ và kích thước của DimBackground để luôn bao phủ toàn bộ màn hình
	/// kể cả khi node này nằm bên trong popupPanel của Setting.
	/// </summary>
	private void ConfigureDimBackground()
	{
		if (DimBackground == null) return;

		// Phủ rộng ra toàn bộ màn hình (toàn bộ viewport chuẩn 1152x648)
		DimBackground.SetAnchorsPreset(LayoutPreset.FullRect);
		DimBackground.OffsetLeft = -2000;
		DimBackground.OffsetTop = -2000;
		DimBackground.OffsetRight = 2000;
		DimBackground.OffsetBottom = 2000;
		DimBackground.MouseFilter = MouseFilterEnum.Stop;
	}

	/// <summary>
	/// Khi người chơi click chuột vào DimBackground (vùng bên ngoài bảng Popup)
	/// </summary>
	private void OnDimBackgroundGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			// Tự động đóng popup xác nhận khi click ra ngoài
			OnCloseButtonPressed();
			GetViewport()?.SetInputAsHandled();
		}
	}

	/// <summary>
	/// Khi hover chuột hoặc ấn vào nút Back to Menu: Hiện icon ngôi nhà backHomeIcon
	/// </summary>
	private void OnGoBackButtonHoverStarted()
	{
		_isHoveringGoBack = true;
		if (BackHomeIcon != null)
		{
			BackHomeIcon.Visible = true;
		}
	}

	/// <summary>
	/// Khi rời chuột: Nếu popup xác nhận ĐANG MỞ, giữ nguyên icon ngôi nhà.
	/// Chỉ ẩn khi popup không mở.
	/// </summary>
	private void OnGoBackButtonHoverEnded()
	{
		_isHoveringGoBack = false;

		// Nếu bảng popup đang hiện, giữ nguyên icon không cho mất!
		if (PopupPanel != null && PopupPanel.Visible)
		{
			return;
		}

		if (BackHomeIcon != null)
		{
			BackHomeIcon.Visible = false;
		}
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
	/// Bật / Tắt trạng thái hiển thị của cả Popup xác nhận và Màn đen làm mờ
	/// </summary>
	public void SetPopupVisible(bool visible)
	{
		MouseFilter = MouseFilterEnum.Ignore;

		if (DimBackground != null)
		{
			DimBackground.Visible = visible;
			DimBackground.MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
			if (visible)
			{
				ConfigureDimBackground();
			}
		}
		if (PopupPanel != null)
		{
			PopupPanel.Visible = visible;
			PopupPanel.MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		}
		if (CloseButton != null)
		{
			CloseButton.Visible = visible;
		}

		// QUY TẮC HIỂN THỊ ICON NGÔI NHÀ:
		// Khi popup đang mở -> luôn giữ icon hiển thị.
		// Khi popup đóng (do Esc, X, Cancel hoặc bấm ra ngoài) -> ẩn icon đi trừ phi chuột đang hover nút.
		if (BackHomeIcon != null)
		{
			if (visible)
			{
				BackHomeIcon.Visible = true;
			}
			else
			{
				BackHomeIcon.Visible = _isHoveringGoBack;
			}
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
	/// Lắng nghe các phím tắt bàn phím:
	/// - Phím [L]: Bật popup Back to Menu khi đang mở Setting popup mà không gặp lỗi.
	/// - Phím [Enter]: Dùng để xác nhận Confirm quay về Menu khi popup đang hiển thị.
	/// - Phím [ESC]: Đóng popup xác nhận nếu đang mở.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			bool isPopupOpen = PopupPanel != null && PopupPanel.Visible;

			// 1. Phím [L]: Kích hoạt popup Back to Menu khi đang ở trong Setting Popup
			if (keyEvent.Keycode == Key.L)
			{
				// Kiểm tra xem nút GoBackButton có đang hiển thị và hoạt động không (khi bảng Setting đang mở)
				if (IsVisibleInTree() && (GoBackButton == null || GoBackButton.IsVisibleInTree()))
				{
					if (!isPopupOpen)
					{
						OnGoBackButtonPressed();
						GetViewport()?.SetInputAsHandled();
						return;
					}
				}
			}

			// 2. Phím [Enter] hoặc [Numpad Enter]: Xác nhận nút Confirm khi popup xác nhận đang mở
			if (isPopupOpen && (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter))
			{
				OnConfirmButtonPressed();
				GetViewport()?.SetInputAsHandled();
				return;
			}

			// 3. Phím [ESC]: Đóng popup xác nhận và làm mất icon ngôi nhà
			if (isPopupOpen && keyEvent.Keycode == Key.Escape)
			{
				OnCloseButtonPressed();
				GetViewport()?.SetInputAsHandled();
				return;
			}
		}
	}
}

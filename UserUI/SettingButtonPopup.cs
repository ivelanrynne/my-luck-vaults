using Godot;
using System;

public partial class SettingButtonPopup : Control
{
	[Export] public BaseButton SettingButton;
	[Export] public Control PopupPanel;
	[Export] public BaseButton CloseButton; // Nút X để đóng bảng
	[Export] public ColorRect DimBackground; // Màn đen mờ phía sau popup
	[Export] public Control BackToMenuNode; // Tham chiếu BackToMenu (nút + popup con)

	[ExportGroup("Âm Thanh (Sound Control)")]
	[Export] public Slider VolumeSlider;
	[Export] public BaseButton ActiveSoundButton;
	[Export] public BaseButton MutedSoundButton;

	// Lưu giá trị âm lượng trước khi bấm mute để khi mở lại có thể khôi phục đúng mức cũ (nếu về 0 thì mở lại là 100)
	private double _lastNonZeroVolume = 100.0;

	public override void _Ready()
	{
		// Đảm bảo node root của Setting không nuốt chuột của gameplay hay UI khác
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
			BackToMenuNode = GetNodeOrNull<Control>("BackToMenu")
						  ?? GetNodeOrNull<Control>("popupPanel/BackToMenu")
						  ?? GetNodeOrNull<Control>("ConfirmBackPopup");
		}

		// 2. Tự động tìm DimBackground (hoặc tự tạo nếu chưa có)
		if (DimBackground == null)
		{
			DimBackground = GetNodeOrNull<ColorRect>("DimBackground")
						 ?? GetNodeOrNull<ColorRect>("dimBackground");
		}

		// Nếu trong scene chưa có node DimBackground, code sẽ tự động tạo ngay một cái
		if (DimBackground == null)
		{
			DimBackground = new ColorRect();
			DimBackground.Name = "DimBackground";
			DimBackground.Color = new Color(0, 0, 0, 0.6f); // Đen mờ 60%
			DimBackground.SetAnchorsPreset(LayoutPreset.FullRect);
			DimBackground.MouseFilter = MouseFilterEnum.Stop; // Chặn click xuyên qua xuống game

			// Đưa DimBackground vào dưới PopupPanel
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
		}

		// 4. Tự động tìm Nút X (CloseButton / exitPopup)
		if (CloseButton == null && PopupPanel != null)
		{
			CloseButton = PopupPanel.GetNodeOrNull<BaseButton>("exitPopup")
					   ?? PopupPanel.GetNodeOrNull<BaseButton>("closeBtn")
					   ?? PopupPanel.GetNodeOrNull<BaseButton>("CloseButton")
					   ?? PopupPanel.GetNodeOrNull<BaseButton>("btn_close")
					   ?? PopupPanel.GetNodeOrNull<BaseButton>("ButtonClose");
		}
		if (CloseButton == null)
		{
			CloseButton = GetNodeOrNull<BaseButton>("exitPopup")
					   ?? GetNodeOrNull<BaseButton>("closeBtn")
					   ?? GetNodeOrNull<BaseButton>("CloseButton");
		}

		// 5. Tìm các thành phần điều khiển âm thanh
		SetupSoundControls();

		// 6. Mặc định ẩn popup và màn đen
		SetPopupVisible(false);

		// 7. Tắt FocusMode trên toàn bộ các nút UI để phím Space không bị chiếm dụng
		RemoveAllControlFocus(this);

		// 8. Kết nối sự kiện Click cơ bản
		if (SettingButton != null)
		{
			SettingButton.Pressed += OnSettingButtonPressed;
		}

		if (CloseButton != null)
		{
			CloseButton.Pressed += OnCloseButtonPressed;
		}
	}

	/// <summary>
	/// Khởi tạo và liên kết các nút Âm thanh (Active/Muted) và thanh trượt Volume
	/// </summary>
	private void SetupSoundControls()
	{
		if (VolumeSlider == null && PopupPanel != null)
		{
			VolumeSlider = PopupPanel.GetNodeOrNull<Slider>("volumeChange/HSlider")
						?? PopupPanel.GetNodeOrNull<Slider>("HSlider")
						?? PopupPanel.GetNodeOrNull<Slider>("VolumeSlider");
		}

		if (ActiveSoundButton == null && PopupPanel != null)
		{
			ActiveSoundButton = PopupPanel.GetNodeOrNull<BaseButton>("activeSound")
							 ?? PopupPanel.GetNodeOrNull<BaseButton>("soundBtn")
							 ?? PopupPanel.GetNodeOrNull<BaseButton>("btn_activeSound");
		}

		if (MutedSoundButton == null && PopupPanel != null)
		{
			MutedSoundButton = PopupPanel.GetNodeOrNull<BaseButton>("mutedSound")
							?? PopupPanel.GetNodeOrNull<BaseButton>("muteBtn")
							?? PopupPanel.GetNodeOrNull<BaseButton>("btn_mutedSound");
		}

		// Lắng nghe sự kiện kéo thanh âm lượng
		if (VolumeSlider != null)
		{
			VolumeSlider.ValueChanged += OnVolumeSliderValueChanged;
			if (VolumeSlider.Value > 0)
			{
				_lastNonZeroVolume = VolumeSlider.Value;
			}
			UpdateSoundUI(VolumeSlider.Value);
		}

		// Lắng nghe nút Active Sound (đang có tiếng, bấm vào thì Mute về 0)
		if (ActiveSoundButton != null)
		{
			ActiveSoundButton.Pressed += OnActiveSoundPressed;
		}

		// Lắng nghe nút Muted Sound (đang tắt tiếng, bấm vào thì bật full 100 hoặc khôi phục)
		if (MutedSoundButton != null)
		{
			MutedSoundButton.Pressed += OnMutedSoundPressed;
		}
	}

	/// <summary>
	/// Khi người dùng kéo thanh âm lượng (dùng chuột kéo về 0 hoặc tăng lên)
	/// </summary>
	private void OnVolumeSliderValueChanged(double value)
	{
		if (value > 0)
		{
			_lastNonZeroVolume = value;
		}
		ApplyAudioVolume(value);
		UpdateSoundUI(value);
	}

	/// <summary>
	/// Bấm vào icon có tiếng (Active Sound) -> Giảm thanh slider về 0 và chuyển sang icon Mute
	/// </summary>
	private void OnActiveSoundPressed()
	{
		if (VolumeSlider != null && VolumeSlider.Value > 0)
		{
			_lastNonZeroVolume = VolumeSlider.Value;
			VolumeSlider.Value = 0; // Tự động kích hoạt OnVolumeSliderValueChanged
		}
		else
		{
			UpdateSoundUI(0);
		}
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Bấm vào icon tắt tiếng (Muted Sound) -> Đẩy thanh slider lên full 100 (hoặc mức cũ) và chuyển sang icon Active
	/// </summary>
	private void OnMutedSoundPressed()
	{
		double targetVolume = _lastNonZeroVolume > 0 ? _lastNonZeroVolume : 100.0;
		if (VolumeSlider != null)
		{
			VolumeSlider.Value = targetVolume; // Tự động kích hoạt OnVolumeSliderValueChanged
		}
		else
		{
			UpdateSoundUI(targetVolume);
		}
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Cập nhật hiển thị luân phiên giữa 2 icon ActiveSound và MutedSound theo giá trị âm lượng
	/// </summary>
	private void UpdateSoundUI(double volumeValue)
	{
		bool isMuted = volumeValue <= 0.001;

		if (ActiveSoundButton != null)
		{
			ActiveSoundButton.Visible = !isMuted;
		}

		if (MutedSoundButton != null)
		{
			MutedSoundButton.Visible = isMuted;
		}
	}

	/// <summary>
	/// Áp dụng âm lượng thực tế vào AudioServer của Godot
	/// </summary>
	private void ApplyAudioVolume(double value)
	{
		int masterBusIndex = AudioServer.GetBusIndex("Master");
		if (masterBusIndex >= 0)
		{
			if (value <= 0.001)
			{
				AudioServer.SetBusMute(masterBusIndex, true);
			}
			else
			{
				AudioServer.SetBusMute(masterBusIndex, false);
				// Chuyển đổi tuyến tính 0 - 100 sang thang đo decibel (dB)
				float volumeDb = Mathf.LinearToDb((float)(value / 100.0));
				AudioServer.SetBusVolumeDb(masterBusIndex, volumeDb);
			}
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
	/// Bật / Tắt trạng thái hiển thị của cả Popup và Màn đen làm mờ
	/// </summary>
	private void SetPopupVisible(bool visible)
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
	/// Bấm ESC trên bàn phím cũng tự động đóng bảng nếu đang mở
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

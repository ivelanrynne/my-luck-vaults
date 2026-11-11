using Godot;
using System;

/// <summary>
/// Module quản lý hệ thống âm thanh (Sound & Volume System) trong UserUI:
/// - Điều khiển âm lượng tổng (Master Audio Bus) theo thang đo dB chuẩn xác.
/// - Đồng bộ hoá thanh trượt Slider và hai nút Mute/Active Sound.
/// - Ghi nhớ mức âm lượng trước khi tắt tiếng để khôi phục nhanh chóng.
/// - Tự động loại bỏ FocusMode trên các nút để không chiếm phím Space/Enter của gameplay và UI.
/// </summary>
public partial class SoundState : Control
{
	[ExportGroup("Các Thành Phần UI Âm Thanh")]
	[Export] public Slider VolumeSlider;
	[Export] public BaseButton ActiveSoundButton;
	[Export] public BaseButton MutedSoundButton;

	// Lưu giá trị âm lượng trước khi tắt tiếng (nếu về 0 thì mở lại là 100)
	private double _lastNonZeroVolume = 100.0;

	public override void _Ready()
	{
		// Luôn xử lý âm thanh ngay cả khi game bị Pause
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Ignore;

		// 1. Tự động tìm thanh trượt VolumeSlider nếu chưa gán
		if (VolumeSlider == null)
		{
			VolumeSlider = GetNodeOrNull<Slider>("volumeChange/HSlider")
						?? GetNodeOrNull<Slider>("HSlider")
						?? GetNodeOrNull<Slider>("VolumeSlider");
		}

		// 2. Tự động tìm nút bật tiếng ActiveSound
		if (ActiveSoundButton == null)
		{
			ActiveSoundButton = GetNodeOrNull<BaseButton>("activeSound")
							 ?? GetNodeOrNull<BaseButton>("soundBtn")
							 ?? GetNodeOrNull<BaseButton>("btn_activeSound");
		}

		// 3. Tự động tìm nút tắt tiếng MutedSound
		if (MutedSoundButton == null)
		{
			MutedSoundButton = GetNodeOrNull<BaseButton>("mutedSound")
							?? GetNodeOrNull<BaseButton>("muteBtn")
							?? GetNodeOrNull<BaseButton>("btn_mutedSound");
		}

		// 4. Bỏ FocusMode để tránh tranh chấp phím Space/Enter
		RemoveFocus(VolumeSlider);
		RemoveFocus(ActiveSoundButton);
		RemoveFocus(MutedSoundButton);

		// 5. Kết nối sự kiện thanh trượt Volume
		if (VolumeSlider != null)
		{
			VolumeSlider.ValueChanged += OnVolumeSliderValueChanged;
			if (VolumeSlider.Value > 0)
			{
				_lastNonZeroVolume = VolumeSlider.Value;
			}
			ApplyAudioVolume(VolumeSlider.Value);
			UpdateSoundUI(VolumeSlider.Value);
		}
		else
		{
			// Đọc âm lượng hiện tại từ AudioServer nếu có
			int masterBusIndex = AudioServer.GetBusIndex("Master");
			if (masterBusIndex >= 0)
			{
				bool isMuted = AudioServer.IsBusMute(masterBusIndex);
				UpdateSoundUI(isMuted ? 0 : 100);
			}
		}

		// 6. Kết nối sự kiện Click nút âm thanh
		if (ActiveSoundButton != null)
		{
			ActiveSoundButton.Pressed += OnActiveSoundPressed;
		}

		if (MutedSoundButton != null)
		{
			MutedSoundButton.Pressed += OnMutedSoundPressed;
		}
	}

	/// <summary>
	/// Khi người dùng kéo thanh âm lượng
	/// </summary>
	public void OnVolumeSliderValueChanged(double value)
	{
		if (value > 0)
		{
			_lastNonZeroVolume = value;
		}
		ApplyAudioVolume(value);
		UpdateSoundUI(value);
	}

	/// <summary>
	/// Bấm vào icon đang có tiếng -> Mute về 0 và chuyển sang icon Mute
	/// </summary>
	public void OnActiveSoundPressed()
	{
		if (VolumeSlider != null && VolumeSlider.Value > 0)
		{
			_lastNonZeroVolume = VolumeSlider.Value;
			VolumeSlider.Value = 0; // Kích hoạt OnVolumeSliderValueChanged
		}
		else
		{
			ApplyAudioVolume(0);
			UpdateSoundUI(0);
		}
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Bấm vào icon tắt tiếng -> Khôi phục về mức âm lượng trước đó
	/// </summary>
	public void OnMutedSoundPressed()
	{
		double targetVolume = _lastNonZeroVolume > 0 ? _lastNonZeroVolume : 100.0;
		if (VolumeSlider != null)
		{
			VolumeSlider.Value = targetVolume; // Kích hoạt OnVolumeSliderValueChanged
		}
		else
		{
			ApplyAudioVolume(targetVolume);
			UpdateSoundUI(targetVolume);
		}
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Cập nhật hiển thị luân phiên giữa 2 icon ActiveSound và MutedSound
	/// </summary>
	public void UpdateSoundUI(double volumeValue)
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
	/// Áp dụng âm lượng thực tế vào AudioServer Master Bus của Godot
	/// </summary>
	public void ApplyAudioVolume(double value)
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
				// Chuyển đổi tuyến tính 0 - 100 sang dB
				float volumeDb = Mathf.LinearToDb((float)(value / 100.0));
				AudioServer.SetBusVolumeDb(masterBusIndex, volumeDb);
			}
		}
	}

	/// <summary>
	/// Đặt giá trị âm lượng trực tiếp từ code bên ngoài
	/// </summary>
	public void SetVolume(double value)
	{
		if (VolumeSlider != null)
		{
			VolumeSlider.Value = value;
		}
		else
		{
			ApplyAudioVolume(value);
			UpdateSoundUI(value);
		}
	}

	/// <summary>
	/// Lấy giá trị âm lượng hiện tại
	/// </summary>
	public double GetVolume()
	{
		return VolumeSlider != null ? VolumeSlider.Value : _lastNonZeroVolume;
	}

	private void RemoveFocus(Control ctrl)
	{
		if (ctrl != null)
		{
			ctrl.FocusMode = FocusModeEnum.None;
		}
	}
}

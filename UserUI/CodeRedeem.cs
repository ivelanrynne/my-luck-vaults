using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Trạng thái kết quả sau khi người chơi nhập mã code redeem.
/// </summary>
public enum RedeemStatus
{
	Success,  // Nhập thành công (REDEEMED_SUCCESSFULLY)
	Already,  // Code đã được sử dụng trước đó (ALREADY_REDEEMED)
	Invalid,  // Code không tồn tại hoặc sai chính tả (INVALID_CODE)
	Expired   // Code đã hết hạn sử dụng (EXPIRED)
}

/// <summary>
/// Module quản lý hệ thống nhập Giftcode / Redeem Code (CodeRedeem):
/// - Tự động loại bỏ khoảng trắng ở đầu và cuối chuỗi (Trim).
/// - Phím [Enter] được dùng làm phím xác nhận nạp code (thay vì làm nhân vật nhảy).
/// - Tự động chuyển đổi hiển thị luân phiên giữa 4 trạng thái phản hồi.
/// - Cung cấp khu vực biến tùy chỉnh (Custom Codes) có chú thích rõ ràng.
/// </summary>
public partial class CodeRedeem : Control
{
	// =========================================================================
	// [KHU VỰC TÙY CHỈNH CODE] - THÊM / BỚT MÃ CODE DÀNH CHO BẠN TẠI ĐÂY:
	// - Thêm RedeemStatus.Success : Mã hợp lệ, nhập sẽ thành công.
	// - Thêm RedeemStatus.Expired : Mã đã hết hạn, nhập sẽ báo EXPIRED.
	// - Các mã không nằm trong danh sách sẽ tự động báo INVALID_CODE.
	// =========================================================================
	public Dictionary<string, RedeemStatus> CustomCodes = new(StringComparer.OrdinalIgnoreCase)
	{
		// >>> DÒNG CODE TỰ CUSTOM MÃ CODE CỦA BẠN Ở ĐÂY (Thêm/bớt thoải mái) <<<
		{ "LUCKY2026", RedeemStatus.Success },
		{ "VAULT_OPEN", RedeemStatus.Success },
		{ "FREE_GEMS", RedeemStatus.Success },
		{ "WELCOME_HERO", RedeemStatus.Success },
		{ "OLD_CODE_2025", RedeemStatus.Expired },
		{ "EVENT_CLOSED", RedeemStatus.Expired }
	};

	[ExportGroup("Các Node Giao Diện")]
	[Export] public LineEdit CodeInputField;
	[Export] public BaseButton RedeemButton;

	[ExportGroup("4 Nhãn Trạng Thái Kết Quả")]
	[Export] public Label SuccessLabel;
	[Export] public Label AlreadyLabel;
	[Export] public Label InvalidLabel;
	[Export] public Label ExpiredLabel;

	// Lưu trữ danh sách các mã code đã được nhận thành công trong phiên chơi
	private readonly HashSet<string> _redeemedCodes = new(StringComparer.OrdinalIgnoreCase);

	// Timer tự động ẩn thông báo trạng thái sau vài giây
	private SceneTreeTimer _hideStatusTimer;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		// 1. Tự động tìm ô nhập mã LineEdit
		if (CodeInputField == null)
		{
			CodeInputField = GetNodeOrNull<LineEdit>("enterCodes")
						  ?? GetNodeOrNull<LineEdit>("LineEdit");
		}

		// 2. Tự động tìm nút Confirm/Redeem
		if (RedeemButton == null)
		{
			RedeemButton = CodeInputField?.GetNodeOrNull<BaseButton>("redeemBtn")
						?? GetNodeOrNull<BaseButton>("redeemBtn")
						?? GetNodeOrNull<BaseButton>("enterCodes/redeemBtn");
		}

		// 3. Tự động tìm 4 nhãn trạng thái thông báo
		if (SuccessLabel == null) SuccessLabel = GetNodeOrNull<Label>("success");
		if (AlreadyLabel == null) AlreadyLabel = GetNodeOrNull<Label>("already");
		if (InvalidLabel == null) InvalidLabel = GetNodeOrNull<Label>("invalid");
		if (ExpiredLabel == null) ExpiredLabel = GetNodeOrNull<Label>("expired");

		// Mặc định ẩn toàn bộ 4 nhãn trạng thái khi vừa mở giao diện
		HideAllStatusLabels();

		// 4. Kết nối sự kiện phím Enter trên LineEdit và nút Redeem
		if (CodeInputField != null)
		{
			// Phím Enter trên bàn phím khi đang đứng ở ô nhập code sẽ kích hoạt Redeem ngay lập tức
			CodeInputField.TextSubmitted += OnCodeTextSubmitted;
		}

		if (RedeemButton != null)
		{
			RedeemButton.FocusMode = FocusModeEnum.None; // Tránh cướp phím Space
			RedeemButton.Pressed += OnRedeemButtonPressed;
		}
	}

	/// <summary>
	/// Khi người chơi bấm phím Enter trong ô LineEdit
	/// </summary>
	private void OnCodeTextSubmitted(string submittedText)
	{
		ProcessRedeem(submittedText);
	}

	/// <summary>
	/// Khi người chơi click vào nút Comfirm/Redeem
	/// </summary>
	private void OnRedeemButtonPressed()
	{
		if (CodeInputField != null)
		{
			ProcessRedeem(CodeInputField.Text);
		}
	}

	/// <summary>
	/// Xử lý logic kiểm tra và kích hoạt mã code
	/// </summary>
	public void ProcessRedeem(string rawInput)
	{
		// 1. Tự động loại bỏ khoảng trắng ở đầu và cuối chuỗi
		string cleanCode = rawInput?.Trim() ?? string.Empty;

		if (string.IsNullOrWhiteSpace(cleanCode))
		{
			// Nếu bỏ trống mà bấm enter thì hiện INVALID_CODE
			ShowStatus(RedeemStatus.Invalid);
			return;
		}

		// 2. Kiểm tra xem code này người chơi đã nhập thành công trong phiên chưa
		if (_redeemedCodes.Contains(cleanCode))
		{
			ShowStatus(RedeemStatus.Already);
			return;
		}

		// 3. Kiểm tra trong từ điển CustomCodes
		if (CustomCodes.TryGetValue(cleanCode, out RedeemStatus codeStatus))
		{
			if (codeStatus == RedeemStatus.Success)
			{
				_redeemedCodes.Add(cleanCode); // Đánh dấu đã nhận thành công
				ShowStatus(RedeemStatus.Success);

				// TODO: Có thể thêm hàm cộng thưởng/quà ở đây nếu cần!
				GD.Print($"[CodeRedeem] Nhận mã thành công: {cleanCode}");
			}
			else if (codeStatus == RedeemStatus.Expired)
			{
				ShowStatus(RedeemStatus.Expired);
			}
			else
			{
				ShowStatus(codeStatus);
			}
		}
		else
		{
			// Không tìm thấy mã trong danh sách -> Mã không hợp lệ
			ShowStatus(RedeemStatus.Invalid);
		}

		// Xóa trắng ô nhập sau khi nhập xong để tiện nhập mã tiếp theo
		if (CodeInputField != null)
		{
			CodeInputField.Text = string.Empty;
		}

		// Giải phóng tiêu điểm bàn phím
		GetViewport()?.GuiReleaseFocus();
	}

	/// <summary>
	/// Tự động chuyển đổi hiển thị giữa 4 trạng thái (Success, Already, Invalid, Expired)
	/// </summary>
	public void ShowStatus(RedeemStatus status)
	{
		HideAllStatusLabels();

		switch (status)
		{
			case RedeemStatus.Success:
				if (SuccessLabel != null) SuccessLabel.Visible = true;
				break;
			case RedeemStatus.Already:
				if (AlreadyLabel != null) AlreadyLabel.Visible = true;
				break;
			case RedeemStatus.Invalid:
				if (InvalidLabel != null) InvalidLabel.Visible = true;
				break;
			case RedeemStatus.Expired:
				if (ExpiredLabel != null) ExpiredLabel.Visible = true;
				break;
		}

		// Đặt lịch tự ẩn thông báo sau 4 giây để UI luôn gọn gàng
		_hideStatusTimer = GetTree().CreateTimer(4.0);
		_hideStatusTimer.Timeout += OnStatusTimerTimeout;
	}

	private void OnStatusTimerTimeout()
	{
		HideAllStatusLabels();
	}

	/// <summary>
	/// Ẩn toàn bộ cả 4 nhãn thông báo
	/// </summary>
	public void HideAllStatusLabels()
	{
		if (SuccessLabel != null) SuccessLabel.Visible = false;
		if (AlreadyLabel != null) AlreadyLabel.Visible = false;
		if (InvalidLabel != null) InvalidLabel.Visible = false;
		if (ExpiredLabel != null) ExpiredLabel.Visible = false;
	}

	/// <summary>
	/// Bắt phím Enter khi người chơi đang chọn ô code hoặc muốn xác nhận nạp code
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter)
			{
				// Nếu ô nhập code đang có tiêu điểm (focus) thì xác nhận code
				if (CodeInputField != null && CodeInputField.HasFocus())
				{
					ProcessRedeem(CodeInputField.Text);
					GetViewport()?.SetInputAsHandled();
				}
			}
		}
	}
}

using Godot;
using System;

/// <summary>
/// Quái StormSkyHarpy (Chim Ưng / Yêu Tinh Bão Tố).
/// Mẫu quái bay (Flying Enemy) điển hình của dự án.
/// </summary>
public partial class StormSkyHarpy : BaseEnemy
{
    public override void _Ready()
    {
        // =========================================================================
        // [HƯỚNG DẪN TẠO QUÁI BAY TỐI GIẢN NHẤT]:
        // Trong bất kỳ file .cs của con quái nào bạn muốn cho bay, chỉ cần gán:
        //      IsFlyingEnemy = true;
        // ngay trước hàm base._Ready();
        // Quái sẽ lập tức hủy bỏ trọng lực, lượn sóng trên không và phóng thẳng 2D dí người chơi!
        // =========================================================================
        IsFlyingEnemy = true;

        // (Tùy chọn) Chỉnh thêm tốc độ bay nếu muốn quái nhanh/chậm hơn bình thường:
        FlyingSpeed = 170.0f;
        FlyingChaseSpeed = 250.0f;

        base._Ready();
    }
}

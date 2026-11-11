using Godot;

/// <summary>
/// Trình quản lý trạng thái trò chơi toàn cục (Singleton tĩnh).
/// Lưu trữ các thông tin xuyên suốt giữa các màn chơi:
/// - Danh sách các đường dẫn Scene nhân vật có trong game.
/// - Index nhân vật mà người chơi đang lựa chọn.
/// </summary>
public static class GameManager
{
    /// <summary>
    /// Danh sách đường dẫn tài nguyên Scene (.tscn) của các nhân vật có thể chơi.
    /// Có thể mở rộng thêm Char03, Char04,... tại đây.
    /// </summary>
    public static readonly string[] CharacterScenes = new string[]
    {
        "res://Characters/Leo/Char01.tscn", // Index 0: Nhân vật Leo (Nam)
        "res://Characters/Luna/Char02.tscn"  // Index 1: Nhân vật Nữ
    };

    /// <summary>
    /// Chỉ số (index) nhân vật đang được người chơi lựa chọn.
    /// Mặc định ban đầu chọn 1 (Char02) hoặc 0 (Char01).
    /// </summary>
    public static int SelectedCharacterIndex = 1;

    /// <summary>
    /// Lấy đường dẫn Scene tài nguyên của nhân vật hiện tại đang được chọn.
    /// </summary>
    public static string GetSelectedCharacterPath()
    {
        return CharacterScenes[SelectedCharacterIndex];
    }
}

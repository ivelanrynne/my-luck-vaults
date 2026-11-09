using Godot;

public static class GameManager
{
    // Đường dẫn scene của các nhân vật
    public static readonly string[] CharacterScenes = new string[]
    {
        "res://Characters/Char01.tscn",
        "res://Characters/Char02.tscn"
    };

    // Index nhân vật đang chọn (0: Char01, 1: Char02)
    public static int SelectedCharacterIndex = 1;

    public static string GetSelectedCharacterPath()
    {
        return CharacterScenes[SelectedCharacterIndex];
    }
}
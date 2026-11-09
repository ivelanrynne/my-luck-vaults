using Godot;
using System;

public partial class MainMenu : Control
{
	public void _on_btn_play_pressed()
	{
		// GetTree().ChangeSceneToFile("res://scenes/IceCave.tscn");
		// GetTree().ChangeSceneToFile("res://scenes/CozyHouse.tscn");
		GetTree().ChangeSceneToFile("res://scenes/EmeraldCloverMeadow.tscn");
	}

	public void _on_btn_custom_character_pressed()
	{
		GetTree().ChangeSceneToFile("res://outside-scenes/custom_character.tscn");
	}

	public void _on_btn_quit_pressed()
	{
		GetTree().Quit();
	}
}

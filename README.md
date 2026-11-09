# My Luck Vaults

*The Godot 4+ C# powered game we are crafting together.*

## 🎮 Overview
**My Luck Vaults** is a small indie game built with **Godot 4** and **C# (Mono)**.  The player explores procedurally‑generated vaults, gathers luck‑boosting items, and unlocks new characters.  This repository contains the full project source – scenes, scripts, assets and the tooling needed to develop and ship the game.

## 📂 Repository Structure
```
my‑luck‑vaults/
├─ .gitignore            # Updated to ignore Godot, Mono, VS/Rider artefacts
├─ project.godot         # Godot project file
├─ ExportPresets.cfg    # Export settings for Windows/macOS/Linux
├─ UserUI/              # UI components (SettingButtonPopup.cs, ControlUI.cs)
├─ Characters/          # BaseCharacter.cs and derived character scripts
├─ scenes/              # Core gameplay scenes (EmeraldCloverMeadow.cs, ...)
├─ outside‑scenes/      # Main menu and non‑gameplay scenes
└─ README.md            # You are reading it right now!
```

## 📋 Prerequisites
- **Godot Engine 4.x** (download from https://godotengine.org)
- **Mono/C# support** – install the .NET SDK (>= .NET 6) and enable the Mono version of Godot.
- (Optional) **Visual Studio / Rider / VS Code** for C# editing – the `.gitignore` already excludes IDE‑specific files.

## 🚀 Getting Started
1. Clone the repository:
   ```bash
   git clone https://github.com/ivelanrynne/my-luck-vaults.git
   cd my‑luck‑vaults
   ```
2. Open the project in Godot (use the *Mono* version).
3. The editor will import the C# scripts automatically.  Press **F5** (or click *Play*) to run the game inside the editor.

## 📦 Building / Exporting
1. Open **Project → Export** in the Godot editor.
2. Add a preset for your target platform (Windows, macOS, Linux).
3. Click **Export Project** – the built executable will be placed in the `exports/` folder.

## 🤝 Contributing
Contributions are welcome!  Please:
1. Fork the repo.
2. Create a feature branch (`git checkout -b feature‑awesome‑thing`).
3. Commit your changes with clear messages.
4. Open a Pull Request describing the change.

Make sure your code follows the existing C# style and that the project still runs inside Godot.

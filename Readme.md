# War Thunder VR Settings Assistant

<p align="center">
  <img src="Assets/MainScreenLogo.png" alt="War Thunder VR Settings Assistant Logo" width="500">
</p>

<p align="center">
  A lightweight utility for managing War Thunder graphics settings for both Desktop and VR gameplay.
</p>

---

## Video Tutorial

Watch the full setup and usage guide here:

▶️ https://www.youtube.com/watch?v=piDV7o6Ja-I

---

## Features

### Graphics Profile Management

* Switch between Desktop and VR graphics configurations
* Support for custom `.blk` files
* Built-in VR presets:

  * Low
  * Medium
  * High

### Neck Rotation Assistance (v1.3)

* Embedded OpenXR neck-assistance controls with live headset/view graphs
* Separate left/right and up/down settings, with optional linked editing
* Draggable activation, release, natural-resume, and maximum-view markers
* Rear-view boost that restores natural 1:1 movement after the configured boost zone
* HOTAS single-button or multi-button bindings for recenter and assistance toggle
* Motion stabilization and clear backend/telemetry connection status

### Settings Capture

* Capture your current War Thunder graphics settings directly from `config.blk`
* Save Desktop and VR configurations for quick access
* Easily remove or replace saved profiles

### Easy Configuration

* Browse or drag-and-drop `.blk` files
* One-click profile switching
* Simple and intuitive interface

### Recommended Settings Guide

* Interactive setup guide for:

  * Virtual Desktop
  * Meta Quest
  * SteamVR

### Extras

* Built-in help and recommendation tools
* Fun hidden easter eggs

#### Easter Egg Commands

On the main screen, click the large War Thunder logo first, then enter one of these arrow-key sequences:

* `↑ ↑ ↓ ↓ ← → ← →`
* `← ← → → ↑ ↓`

Have fun. ;)

---

## Requirements

* Windows 10 / Windows 11
* War Thunder
* .NET Desktop Runtime (if required by your release build)

---

## How To Use

1. Select your War Thunder installation folder
2. Configure your Desktop and/or VR profile
3. Choose a preset or custom `.blk`
4. Apply the desired settings
5. Launch War Thunder

For Neck Assist, enable it before starting the VR session so the OpenXR layer can load. Once connected, graph and slider changes apply live without restarting the game. Yellow starts the boost, orange releases it when returning to center, cyan restores natural 1:1 movement, and green controls the maximum resulting view.

---

## Disclaimer

This is an unofficial community-made tool and is not affiliated with, endorsed by, or sponsored by Gaijin Entertainment.

War Thunder is a trademark of Gaijin Entertainment.

Always keep a backup of your original `config.blk` before modifying graphics settings.

---

## License

MIT License

# FpvDrone

[![Join the Discord](https://jatify.com/badges/discord.png)](https://discord.gg/Ru9t2KdfVK) [![Buy me a coffee](https://jatify.com/badges/coffee.png)](https://buymeacoffee.com/imdizmo)

Launch an FPV racing drone from your character and fly it through your world with a gamepad (a keyboard works too).

## Controls (Mode 2)

| | PS4 (DualShock) | Xbox |
|---|---|---|
| Launch / land | **Home** (keyboard) | **Home** |
| Throttle | **R2** | RT |
| Yaw | left stick ← → | left stick |
| Pitch / roll | right stick | right stick |
| Acro / Angle mode | **Triangle** | Y |
| Reset next to you | **Circle** | B |

**Acro** (default) is how real FPV quads fly: sticks set rotation rates and the drone holds its attitude, so you fly it through flips and dives. **Angle** levels itself when you let go, for learning.

No gamepad: Space (full) / Shift (low) throttle, W/S pitch, A/D roll, Q/E yaw.

## Settings

**End** opens a settings panel (while flying or not): rotation speed, expo, Angle-mode tilt, thrust, drag, deadzone, camera uptilt and FOV, with Beginner / Normal / Racer presets and a reset. Changes apply at once and are saved; the drone holds still while it is open.

## Flight

- Goggles-style OSD: speed, altitude, distance, throttle bar, battery and mode; warnings for low battery and signal.
- Crash into anything hard and fast, or into water, and it resets next to you. Gentle touches slide off surfaces.
- Range is limited (default 230 m), because the world only loads around your character. Your character stands still while you fly.
- Everything (rates, expo, thrust, drag, camera uptilt and FOV, range, battery, HUD) is in `BepInEx/config/jatify.fpvdrone.cfg`.

Client-side: nothing is added to the world and other players see nothing.

## Credits

- Mod loader: [BepInEx](https://github.com/BepInEx/BepInEx).
- Code: written together with Claude (Anthropic's AI), which shows up as a co-author in the commit history.

FpvDrone is an unofficial, fan-made mod. It is not made, endorsed or supported by Iron Gate AB. Please report FpvDrone bugs [on GitHub](https://github.com/imdizmo/FpvDrone/issues). Valheim is a trademark of Iron Gate AB.

Source: https://github.com/imdizmo/FpvDrone (MIT; the MIT licence covers FpvDrone's own code only)

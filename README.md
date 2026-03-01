# Helvety Screenshots

WinUI 3 desktop screenshot tool for fast, keyboard-driven capture workflows on Windows.

## Development Status

This project is under active development and is not a release/stable build yet.
Expect rapid changes, incomplete features, and occasional breaking behavior while core capture UX is being refined.

## Current Focus

- Global hotkey-triggered screenshot mode
- Frozen-screen capture overlay
- Window snapping with visual highlight
- Free-rectangle selection
- Clipboard and save-folder capture actions
- Screenshots page with live folder refresh after new captures
- Thumbnail previews for common image formats (PNG, JPG/JPEG, BMP, GIF, TIFF; WebP when codec is installed)
- File metadata shown as European date/time format (`dd.MM.yyyy HH:mm`) plus relative age (`... ago`)
- Iterative UX polish (overlay guidance, animation, interaction tuning)
- Border FX personalization (intensity profile, rotating palettes, adaptive chase speed)
- Settings-controlled overlay guidance visibility

## Tech Stack

- .NET 8
- WinUI 3 (Windows App SDK)
- Native Win32 interop for hooks, hit-testing, and capture support

## Run Locally

1. Open the solution in Visual Studio 2022 (with WinUI/.NET desktop workloads).
2. Build and run the `helvety.screenshots` project.
3. Configure save folder and hotkey in the app settings.
4. (Optional) Tune screenshot border intensity in `Settings > Screenshot Border Effects`.
5. (Optional) Toggle screenshot overlay guidance visibility in settings.

## Notes

- This repository is public but still in heavy iteration.

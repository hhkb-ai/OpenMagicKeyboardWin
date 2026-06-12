# OpenMagicKeyboardWin

OpenMagicKeyboardWin is an independent open-source Windows utility and driver research project for Apple Magic Keyboard.

The first target device is **Apple Magic Keyboard A2450**. The current core goal is **Fn ↔ Left Ctrl behavior** on Windows 10/11.

> **Current stage:** MVP-A Pre-VM hardening completed. The driver targets **USB mode only** (`VID_05AC&PID_029C`). The `.sys` is unsigned, not installed, not loaded, not production ready. Bluetooth support is a future goal (MVP-D).

## Goals

- Detect Apple Magic Keyboard A2450 on Windows (USB mode).
- Collect A2450 HID / Raw Input reports safely.
- Implement a clean-room Windows solution for Fn and Left Ctrl behavior.
- Support Fn-layer shortcuts such as Delete, Home, End, Page Up, and Page Down.
- Add a tray app for connection status, battery status, and configuration.
- Keep the project independent from proprietary commercial software.

**Note:** The current driver implementation targets USB mode only. Bluetooth support (MVP-D) is a separate future phase that requires additional HID report analysis.

## Non-goals

- This project does not crack, patch, bypass, or repackage any paid software.
- This project does not include proprietary drivers, icons, UI assets, license logic, or binaries from commercial software.
- Touch ID support is not planned for the first version.
- The first version focuses on A2450 only. Other models can be added later.

## Roadmap

### Phase 0 — Repository scaffold

- Project structure
- Documentation
- Clean-room policy
- A2450 HID logger scaffold
- Driver design notes

### Phase 1 — A2450 HID Logger

Build a Windows tool to collect non-textual key and HID data:

- Device path
- VID / PID
- Product string
- Manufacturer string
- Bluetooth / USB transport hint
- Raw Input key events
- Fn / Ctrl / F-row / arrow / Backspace test matrix

### Phase 2 — A2450 driver MVP

Build a minimal driver prototype:

- Target A2450 only
- Fn → Left Ctrl
- Left Ctrl → internal FnLayer
- FnLayer + Backspace / arrows / F-row mappings

### Phase 3 — Tray app

- Connection status
- Battery status
- Fn/Ctrl swap toggle
- F-row mode toggle
- Export diagnostics

## Safety and privacy

The logger must not record text content. It only records device metadata, virtual keys, scan codes, make/break state, and raw report bytes needed for driver development.

## Repository creation

If this folder is local, create the GitHub repository with:

```powershell
gh repo create hhkb-ai/OpenMagicKeyboardWin --public --source . --remote origin --push
```

Or manually create an empty repository named `OpenMagicKeyboardWin` under `hhkb-ai`, then run:

```powershell
git init
git add .
git commit -m "Initial clean-room project scaffold"
git branch -M main
git remote add origin https://github.com/hhkb-ai/OpenMagicKeyboardWin.git
git push -u origin main
```

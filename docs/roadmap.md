# OpenMagicKeyboardWin Roadmap

## MVP-A — Keyboard Transform (Current)

**Status: Pre-VM hardening completed, WDK verified (unsigned .sys, not installed)**

| Feature | Status |
|---------|--------|
| Fn / Globe → Left Ctrl | ✅ C# + C driver |
| Physical Left Ctrl → internal FnLayer | ✅ C# + C driver |
| FnLayer + Backspace → Delete | ✅ C# + C driver |
| FnLayer + ↑ → PageUp | ✅ C# + C driver |
| FnLayer + ↓ → PageDown | ✅ C# + C driver |
| FnLayer + ← → Home | ✅ C# + C driver |
| FnLayer + → → End | ✅ C# + C driver |
| C# unit tests (37/37) | ✅ |
| WDK/KMDF build scaffold | ✅ |
| WDK build (Debug + Release) | ✅ 0 warnings, 0 errors (post-hardening) |
| .sys generation | ✅ Debug 11 KB, Release 11 KB |
| Filter.c minimal interception | ✅ EvtIoInternalDeviceControl + completion routine |
| Report length hardening | ✅ merged to main, WDK verified |
| INF template | ✅ (design only) |
| C#/C parity check | ✅ |

**MVP-A current state:**
- C# transform model complete
- C# unit tests passing (37/37)
- C driver transform logic implemented
- Minimal HID READ_REPORT interception implemented
- Pre-VM report-length hardening merged into main and WDK verified
- Driver unsigned, not installed, not loaded, not bound to real hardware
- VM load test not started
- Real A2450 functional test not started
- Not production ready

**Remaining for MVP-A completion:**
- [ ] Final orchestrator review
- [ ] Documentation sync
- [ ] MVP-A VM test plan
- [ ] VM load test
- [ ] Real A2450 functional test in controlled environment

## MVP-B — Media Keys

**Status: Design complete, implementation pending**

| Feature | Status |
|---------|--------|
| A2450MediaKeyMapper model | ✅ |
| TransformWithConsumerUsage | ✅ |
| COL02 Consumer Control verified | ✅ |
| FnLayer + F7 → Previous Track | ⏳ |
| FnLayer + F8 → Play/Pause | ⏳ |
| FnLayer + F9 → Next Track | ⏳ |
| FnLayer + F10 → Mute | ⏳ |
| FnLayer + F11 → Volume Down | ⏳ |
| FnLayer + F12 → Volume Up | ⏳ |

**Implementation approach:**
- Driver intercepts COL01 keyboard report
- When FnLayer + F7~F12 detected, suppress the F key from COL01
- Synthesize Consumer Control Input Report for COL02
- Alternative: virtual HID device that exposes Consumer Control

**Decision needed:** Filter synthesis vs virtual HID device.

## MVP-C — Distribution

| Feature | Status |
|---------|--------|
| Installer (MSI/MSIX) | ❌ |
| EV code signing | ❌ |
| Microsoft Hardware Dashboard | ❌ |
| System tray configuration app | ❌ |
| GUI for Fn/Ctrl swap toggle | ❌ |
| Auto-start on boot | ❌ |

## MVP-D — Bluetooth

| Feature | Status |
|---------|--------|
| Bluetooth HID Report analysis | ❌ |
| Bluetooth HCI snoop capture | ❌ |
| Bluetooth Fn byte verification | ❌ |
| Bluetooth filter driver binding | ❌ |
| Dual-mode (USB + BT) support | ❌ |

**Note:** The current driver only targets USB mode (`VID_05AC&PID_029C`). Bluetooth mode uses different hardware IDs and may have a different HID report structure. The Fn byte (Byte 9) position and value need to be verified separately for Bluetooth.

## Timeline

| Phase | Target | Notes |
|-------|--------|-------|
| MVP-A | Current | Minimal interception implemented, not installed |
| MVP-B | TBD | After MVP-A verified in VM |
| MVP-C | TBD | After MVP-B stable |
| MVP-D | TBD | After Bluetooth capture done |

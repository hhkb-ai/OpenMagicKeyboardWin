# A2450 Transform Parity Check

## Purpose

The C# user-mode transformer (`A2450ReportTransformer`) and the C kernel-mode transformer (`ReportTransform.c`) must implement identical logic. This document verifies parity between the two implementations.

The C# version serves as the reference implementation with 37 unit tests. The C driver version will be used in the KMDF filter driver.

## Shared HID Report Constants

| Constant | C# Value | C Value | Match |
|----------|----------|---------|-------|
| ReportLength | 10 | A2450_REPORT_LENGTH = 10 | ✅ |
| ReportId | 0x01 | A2450_REPORT_ID = 0x01 | ✅ |
| AppleFnMask | 0x02 | A2450_APPLE_FN_MASK = 0x02 | ✅ |
| LeftCtrlMask | 0x01 | A2450_MOD_LEFT_CTRL = 0x01 | ✅ |
| Key slots | Byte 3-8 | Report[3]..Report[8] | ✅ |

## Keyboard Report Transform Rules

| Rule | C# | C | Match |
|------|-----|---|-------|
| Fn detection | `(report[9] & 0x02) != 0` | `(Report[9] & A2450_APPLE_FN_MASK) != 0` | ✅ |
| Left Ctrl detection | `(report[1] & 0x01) != 0` | `(Report[1] & A2450_MOD_LEFT_CTRL) != 0` | ✅ |
| Fn → Ctrl | `report[1] \|= 0x01` | `Report[1] \|= A2450_MOD_LEFT_CTRL` | ✅ |
| Clear Fn byte | `report[9] &= ~0x02` | `Report[9] &= ~A2450_APPLE_FN_MASK` | ✅ |
| Ctrl → FnLayer (remove) | `report[1] &= ~0x01` | `Report[1] &= ~A2450_MOD_LEFT_CTRL` | ✅ |
| Step 2b re-apply Fn Ctrl | `if (physicalFnDown) report[1] \|= 0x01` | `if (physicalFnDown) Report[1] \|= A2450_MOD_LEFT_CTRL` | ✅ |
| FnLayer active = physicalLeftCtrlDown | `physicalLeftCtrlDown` | `State->FnLayerActive = TRUE` | ✅ |

## FnLayer Key Remapping

| Key | From | To | C# | C | Match |
|-----|------|----|----|---|-------|
| Backspace | 0x2A | 0x4C | `{ 0x2A, 0x4C }` | `case A2450_USAGE_BACKSPACE: return A2450_USAGE_DELETE` | ✅ |
| Up | 0x52 | 0x4B | `{ 0x52, 0x4B }` | `case A2450_USAGE_UP: return A2450_USAGE_PAGEUP` | ✅ |
| Down | 0x51 | 0x4E | `{ 0x51, 0x4E }` | `case A2450_USAGE_DOWN: return A2450_USAGE_PAGEDOWN` | ✅ |
| Left | 0x50 | 0x4A | `{ 0x50, 0x4A }` | `case A2450_USAGE_LEFT: return A2450_USAGE_HOME` | ✅ |
| Right | 0x4F | 0x4D | `{ 0x4F, 0x4D }` | `case A2450_USAGE_RIGHT: return A2450_USAGE_END` | ✅ |

## C# Implementation

File: `src/OpenMagicKeyboard.Shared/A2450ReportTransformer.cs`

- Returns a new `byte[]` (does not modify input)
- Uses `A2450TransformOptions` for configuration
- Has `TransformWithConsumerUsage` extension for media key detection
- 37 unit tests in `tests/OpenMagicKeyboard.TransformTests/`

## C Driver Implementation

File: `driver/OpenMagicKeyboardA2450Filter/ReportTransform.c`

- Modifies report in-place (kernel efficiency)
- Uses `A2450_TRANSFORM_STATE` for configuration
- MVP-A only: no media key output
- State stored in device context

## Parity Matrix

| Feature | C# | C Driver (MVP-A) | Notes |
|---------|-----|-------------------|-------|
| ReportLength = 10 | ✅ | ✅ | |
| ReportId = 0x01 | ✅ | ✅ | |
| Fn → Left Ctrl | ✅ | ✅ | |
| Clear Apple Fn byte | ✅ | ✅ | |
| Left Ctrl → FnLayer | ✅ | ✅ | |
| Step 2b re-apply | ✅ | ✅ | |
| Backspace → Delete | ✅ | ✅ | |
| Up → PageUp | ✅ | ✅ | |
| Down → PageDown | ✅ | ✅ | |
| Left → Home | ✅ | ✅ | |
| Right → End | ✅ | ✅ | |
| F7-F12 in COL01 | ❌ | ❌ | Correctly excluded |
| ConsumerUsage result | ✅ | N/A | C# only, C driver MVP-A skips |
| In-place mutation | N/A | ✅ | C driver modifies in-place |
| Copy-then-transform | ✅ | N/A | C# returns new array |

## Known Differences

1. **In-place vs copy**: C driver modifies report in-place for kernel efficiency. C# returns a new array for safety.

2. **ConsumerUsage**: C# `TransformWithConsumerUsage` can return `A2450TransformResult.ConsumerUsage` for FnLayer + F7~F12. C driver MVP-A does not output media keys. Media keys belong to MVP-B.

3. **State management**: C driver uses `A2450_TRANSFORM_STATE` struct stored in device context. C# uses `A2450TransformOptions` per-call.

4. **F7-F12 handling**: Neither implementation puts F7-F12 into the COL01 keyboard report. This is correct — media keys require Consumer Control (COL02).

## Test Cases (MVP-A Relevant)

From the 37 C# unit tests, these are directly relevant to MVP-A driver logic:

| Test | Input | Expected Output |
|------|-------|----------------|
| Idle | `01 00 00 00...00` | No change |
| Fn alone | `01 00 00 00...02` | `01 01 00 00...00` |
| Left Ctrl alone | `01 01 00 00...00` | `01 00 00 00...00` |
| Fn + Ctrl | `01 01 00 00...02` | `01 01 00 00...00` |
| Ctrl + Backspace | `01 01 00 2A...00` | `01 00 00 4C...00` |
| Ctrl + Up | `01 01 00 52...00` | `01 00 00 4B...00` |
| Ctrl + Down | `01 01 00 51...00` | `01 00 00 4E...00` |
| Ctrl + Left | `01 01 00 50...00` | `01 00 00 4A...00` |
| Ctrl + Right | `01 01 00 4F...00` | `01 00 00 4D...00` |
| Ctrl + A | `01 01 00 04...00` | `01 00 00 04...00` |
| Fn + Backspace | `01 00 00 2A...02` | `01 01 00 2A...00` |

All 37 tests pass. C# implementation is the reference; C driver must produce identical byte-level output for the same input.

## Review Fixes Applied

### A2450Report.h modifier comment

The modifier byte (Byte 1) bit mapping comment was corrected. The standard HID Keyboard modifier bitmap defines:

- bit 0 = Left Ctrl (0x01)
- bit 1 = Left Shift (0x02)

USBPcap confirms A2450 follows this standard. The previous comment incorrectly stated A2450 deviated from the standard.

### C driver modified return value

`ReportTransform.c` was updated so that `ClearAppleFnByte` only sets `modified = TRUE` when the byte actually changes. Idle reports (Byte 9 already 0x00) now correctly return `FALSE`.

### Media key channel

The C driver comment previously referenced COL03 for Consumer Control. This was corrected to COL02, matching the real device descriptor dump:

- COL02 = Consumer Control (UsagePage 0x000C, 2-byte input)
- COL03 = Vendor Defined (UsagePage 0xFF00, 65-byte input)

Do not send Output Reports to the physical COL02 device.

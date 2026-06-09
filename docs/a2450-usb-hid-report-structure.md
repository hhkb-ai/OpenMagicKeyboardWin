# Apple Magic Keyboard A2450 USB HID Report Structure

This document records the current USBPcap findings for Apple Magic Keyboard A2450 on Windows.

## Current status

Earlier Windows user-mode tests showed that Fn / Globe is not visible through the standard Raw Input keyboard path and was not readable from the vendor-defined HID collections through normal user-mode HID APIs.

USBPcap captures now show that the keyboard does send Fn state on the USB transport layer.

## Device identity

Observed USB identity:

```text
USB VID:PID = 05AC:029C
```

Observed Bluetooth identity from earlier logs:

```text
BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&029C
```

## Captured endpoint

The useful USBPcap endpoint was:

```text
1.2.2 -> host
URB_INTERRUPT in
```

The useful HID report length is currently observed as 10 bytes.

## Report layout hypothesis

Current observed 10-byte report layout:

```text
Byte 0 = Report ID, currently 0x01
Byte 1 = Modifier byte
Byte 2 = Reserved
Byte 3 = First key usage slot
Byte 4 = Second key usage slot
Byte 5 = Third key usage slot
Byte 6 = Fourth key usage slot
Byte 7 = Fifth key usage slot
Byte 8 = Sixth key usage slot
Byte 9 = Apple Fn / Globe state byte
```

## Confirmed fields

### Idle

```text
01 00 00 00 00 00 00 00 00 00
```

### Fn / Globe down

```text
01 00 00 00 00 00 00 00 00 02
```

Interpretation:

```text
Byte 9 = 0x02 means Fn / Globe is held.
```

### Left Ctrl down

```text
01 01 00 00 00 00 00 00 00 00
```

Interpretation:

```text
Byte 1 bit 0 = Left Ctrl.
```

### Fn + Left Ctrl

```text
01 01 00 00 00 00 00 00 00 02
```

Interpretation:

```text
Left Ctrl and Fn can be present in the same report.
```

### Backspace

```text
01 00 00 2A 00 00 00 00 00 00
```

Interpretation:

```text
Backspace = HID keyboard usage 0x2A.
```

### F-row with Fn held

Observed pattern:

```text
Fn + F1  = 01 00 00 3A 00 00 00 00 00 02
Fn + F2  = 01 00 00 3B 00 00 00 00 00 02
Fn + F3  = 01 00 00 3C 00 00 00 00 00 02
Fn + F4  = 01 00 00 3D 00 00 00 00 00 02
Fn + F5  = 01 00 00 3E 00 00 00 00 00 02
Fn + F6  = 01 00 00 3F 00 00 00 00 00 02
Fn + F7  = 01 00 00 40 00 00 00 00 00 02
Fn + F8  = 01 00 00 41 00 00 00 00 00 02
Fn + F9  = 01 00 00 42 00 00 00 00 00 02
Fn + F10 = 01 00 00 43 00 00 00 00 00 02
Fn + F11 = 01 00 00 44 00 00 00 00 00 02
Fn + F12 = 01 00 00 45 00 00 00 00 00 02
```

The F-key usage code remains unchanged. Fn state is carried in byte 9.

### Direction keys with Fn held

Observed pattern:

```text
Fn + Right = 01 00 00 4F 00 00 00 00 00 02
Fn + Left  = 01 00 00 50 00 00 00 00 00 02
Fn + Down  = 01 00 00 51 00 00 00 00 00 02
Fn + Up    = 01 00 00 52 00 00 00 00 00 02
```

The arrow-key usage code remains unchanged. Fn state is carried in byte 9.

## Key usage references

```text
F1        = 0x3A
F2        = 0x3B
F3        = 0x3C
F4        = 0x3D
F5        = 0x3E
F6        = 0x3F
F7        = 0x40
F8        = 0x41
F9        = 0x42
F10       = 0x43
F11       = 0x44
F12       = 0x45
Backspace = 0x2A
Right     = 0x4F
Left      = 0x50
Down      = 0x51
Up        = 0x52
```

## Current conclusion

Fn / Globe is visible on the USB transport layer.

Current best hypothesis:

```text
Fn / Globe state = report byte 9 value 0x02
```

This does not mean ordinary Windows user-mode APIs can read it directly. Earlier user-mode tests indicate Raw Input / HidSharp / normal HID API access may not expose the needed keyboard report because the Windows HID keyboard stack consumes it first.

## Implementation implications

The next implementation step should focus on a keyboard/HID filter design rather than continuing ordinary user-mode HID polling.

Potential mapping target:

```text
Physical Fn         -> emit Left Ctrl
Physical Left Ctrl  -> internal FnLayer state
FnLayer + Backspace -> Delete
FnLayer + Up        -> PageUp
FnLayer + Down      -> PageDown
FnLayer + Left      -> Home
FnLayer + Right     -> End
FnLayer + F7        -> Previous Track
FnLayer + F8        -> Play/Pause
FnLayer + F9        -> Next Track
FnLayer + F10       -> Mute
FnLayer + F11       -> Volume Down
FnLayer + F12       -> Volume Up
```

## Remaining validation

A final small capture can still be useful for a clean Fn + Backspace sample without Ctrl mixed in:

```text
Fn + Backspace expected: 01 00 00 2A 00 00 00 00 00 02
```

However, Fn itself and the main report layout are already sufficiently clear for the next design phase.

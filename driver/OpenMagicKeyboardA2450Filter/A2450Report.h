/*
 * A2450Report.h — Apple Magic Keyboard A2450 HID Report definitions.
 *
 * CONFIRMED via USBPcap + Wireshark:
 *   A2450 USB mode sends 10-byte HID reports on Interrupt IN endpoint 0x82.
 *
 * Report layout:
 *   Byte 0 = Report ID (0x01)
 *   Byte 1 = Modifier (standard HID Keyboard modifier bitmap)
 *   Byte 2 = Reserved (0x00)
 *   Byte 3 = Key slot 1 (HID Usage Code, Usage Page 0x07)
 *   Byte 4 = Key slot 2
 *   Byte 5 = Key slot 3
 *   Byte 6 = Key slot 4
 *   Byte 7 = Key slot 5
 *   Byte 8 = Key slot 6
 *   Byte 9 = Apple Fn/Globe state (0x00=released, 0x02=pressed)
 *
 * Modifier byte (Byte 1) bit mapping:
 *   This matches the standard HID Keyboard modifier bitmap:
 *     bit 0 = Left Ctrl   (0x01)
 *     bit 1 = Left Shift  (0x02)
 *     bit 2 = Left Alt    (0x04)
 *     bit 3 = Left GUI    (0x08)
 *     bit 4 = Right Ctrl  (0x10)
 *     bit 5 = Right Shift (0x20)
 *     bit 6 = Right Alt   (0x40)
 *     bit 7 = Right GUI   (0x80)
 *
 *   USBPcap confirms A2450 Left Ctrl produces byte 1 = 0x01,
 *   which is consistent with the standard HID modifier bitmap.
 *
 * DESIGN / BUILD SKELETON ONLY.
 * Do not install.
 * Do not bind to real hardware.
 * Do not run on production machines.
 */

#pragma once

#include <ntddk.h>

#define A2450_REPORT_LENGTH     10
#define A2450_REPORT_ID         0x01

/* Apple Fn/Globe state byte (Byte 9) */
#define A2450_APPLE_FN_MASK     0x02

/* Modifier byte (Byte 1) — as observed via USBPcap on A2450 */
#define A2450_MOD_LEFT_CTRL     0x01
#define A2450_MOD_LEFT_SHIFT    0x02
#define A2450_MOD_LEFT_ALT      0x04
#define A2450_MOD_LEFT_GUI      0x08
#define A2450_MOD_RIGHT_CTRL    0x10
#define A2450_MOD_RIGHT_SHIFT   0x20
#define A2450_MOD_RIGHT_ALT     0x40
#define A2450_MOD_RIGHT_GUI     0x80

/* HID Usage Codes (Usage Page 0x07) for FnLayer mapping */
#define A2450_USAGE_BACKSPACE   0x2A
#define A2450_USAGE_DELETE      0x4C
#define A2450_USAGE_UP          0x52
#define A2450_USAGE_PAGEUP      0x4B
#define A2450_USAGE_DOWN        0x51
#define A2450_USAGE_PAGEDOWN    0x4E
#define A2450_USAGE_LEFT        0x50
#define A2450_USAGE_HOME        0x4A
#define A2450_USAGE_RIGHT       0x4F
#define A2450_USAGE_END         0x4D

/* A2450 USB Hardware IDs */
#define A2450_USB_VID           0x05AC
#define A2450_USB_PID           0x029C

/*
 * Inline helper: check if a report is an A2450 keyboard report (10 bytes, ID 0x01).
 */
__forceinline BOOLEAN
A2450IsKeyboardReport(_In_reads_(Length) const UCHAR* Report, _In_ size_t Length)
{
    return (Length >= A2450_REPORT_LENGTH) && (Report[0] == A2450_REPORT_ID);
}

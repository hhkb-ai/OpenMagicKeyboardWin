/*
 * ReportTransform.c — A2450 HID Report transformation logic.
 *
 * This file implements the core Fn/Ctrl swap and FnLayer remapping
 * for Apple Magic Keyboard A2450 on Windows.
 *
 * Based on USBPcap-confirmed HID report structure:
 *   Byte 0 = Report ID (0x01)
 *   Byte 1 = Modifier (bit 0 = Left Ctrl on A2450)
 *   Byte 2 = Reserved
 *   Byte 3-8 = Key usage slots
 *   Byte 9 = Apple Fn state (0x02 = pressed)
 *
 * This is a design skeleton only.
 * Do not install.
 * Do not bind to real hardware yet.
 * Do not run on production machines.
 */

#include "ReportTransform.h"

VOID
A2450TransformStateInit(_Out_ PA2450_TRANSFORM_STATE State)
{
    RtlZeroMemory(State, sizeof(*State));
    State->SwapEnabled = TRUE;
    State->ClearAppleFnByte = TRUE;
    State->EnableFnLayer = TRUE;
    State->FnLayerActive = FALSE;
}

BOOLEAN
A2450TransformKeyboardReport(
    _Inout_updates_bytes_(ReportLength) UCHAR* Report,
    _In_ size_t ReportLength,
    _In_ PA2450_TRANSFORM_STATE State
)
{
    BOOLEAN modified = FALSE;

    /* Validate input */
    if (ReportLength < A2450_REPORT_LENGTH)
    {
        return FALSE;
    }

    if (Report[0] != A2450_REPORT_ID)
    {
        return FALSE;
    }

    if (!State->SwapEnabled)
    {
        return FALSE;
    }

    /*
     * Step 0: Read original physical state BEFORE any mutation.
     *
     * physicalFnDown: Byte 9 bit 1 (0x02)
     * physicalLeftCtrlDown: Byte 1 bit 0 (0x01) — A2450-specific!
     */
    BOOLEAN physicalFnDown = (Report[9] & A2450_APPLE_FN_MASK) != 0;
    BOOLEAN physicalLeftCtrlDown = (Report[1] & A2450_MOD_LEFT_CTRL) != 0;

    /*
     * Step 1: Physical Fn → Left Ctrl.
     *
     * If the user pressed the physical Fn/Globe key, we want Windows
     * to see a Left Ctrl press. Set the Left Ctrl bit in the modifier.
     */
    if (physicalFnDown)
    {
        Report[1] |= A2450_MOD_LEFT_CTRL;
        modified = TRUE;
    }

    /*
     * Clear Apple Fn byte if configured.
     *
     * kbdhid.sys ignores Byte 9, but clearing it keeps the output clean.
     * We already extracted physicalFnDown above, so this is safe.
     * Only set modified=TRUE if the byte actually changed.
     */
    if (State->ClearAppleFnByte && (Report[9] & A2450_APPLE_FN_MASK) != 0)
    {
        Report[9] &= ~A2450_APPLE_FN_MASK;
        modified = TRUE;
    }

    /*
     * Step 2: Physical Left Ctrl → FnLayer.
     *
     * The physical Left Ctrl key is repurposed as a FnLayer modifier.
     * Remove it from the modifier byte so Windows doesn't see a Ctrl press.
     *
     * IMPORTANT: This runs AFTER Step 1, so if Fn also set the Ctrl bit,
     * we need to re-apply it below (Step 2b).
     */
    if (physicalLeftCtrlDown)
    {
        Report[1] &= ~A2450_MOD_LEFT_CTRL;
        State->FnLayerActive = TRUE;
        modified = TRUE;
    }
    else
    {
        State->FnLayerActive = FALSE;
    }

    /*
     * Step 2b: Re-apply Fn-mapped Left Ctrl.
     *
     * If both Fn and physical Ctrl were pressed, Step 1 set Ctrl,
     * then Step 2 cleared it. Re-apply so the output still has Ctrl
     * (which came from Fn, not from the physical Ctrl key).
     */
    if (physicalFnDown)
    {
        Report[1] |= A2450_MOD_LEFT_CTRL;
    }

    /*
     * Step 3: FnLayer key remapping.
     *
     * If the internal FnLayer is active (physical Ctrl held),
     * scan key slots and remap as needed.
     */
    if (State->EnableFnLayer && State->FnLayerActive)
    {
        for (int i = 3; i <= 8; i++)
        {
            if (Report[i] != 0x00)
            {
                UCHAR original = Report[i];
                Report[i] = A2450RemapFnLayerKey(Report[i]);
                if (Report[i] != original)
                {
                    modified = TRUE;
                }
            }
        }
    }

    return modified;
}

UCHAR
A2450RemapFnLayerKey(_In_ UCHAR Usage)
{
    switch (Usage)
    {
    case A2450_USAGE_BACKSPACE: return A2450_USAGE_DELETE;
    case A2450_USAGE_UP:        return A2450_USAGE_PAGEUP;
    case A2450_USAGE_DOWN:      return A2450_USAGE_PAGEDOWN;
    case A2450_USAGE_LEFT:      return A2450_USAGE_HOME;
    case A2450_USAGE_RIGHT:     return A2450_USAGE_END;
    default:                    return Usage;
    }

    /*
     * F7-F12 media keys need Consumer Control Usage Page 0x0C.
     * Standard keyboard reports (Usage Page 0x07) cannot output media keys.
     * This belongs to MVP-B and requires either:
     *   - Synthesizing a Consumer Control Input Report for COL02
     *   - A virtual HID device that exposes Consumer Control
     *
     * Do not send Output Reports to the physical COL02 device.
     *
     * F7  (0x40) → Previous Track
     * F8  (0x41) → Play/Pause
     * F9  (0x42) → Next Track
     * F10 (0x43) → Mute
     * F11 (0x44) → Volume Down
     * F12 (0x45) → Volume Up
     */
}

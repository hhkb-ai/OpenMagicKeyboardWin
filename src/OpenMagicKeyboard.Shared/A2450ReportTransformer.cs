namespace OpenMagicKeyboard.Shared;

/// <summary>
/// Options controlling how A2450 HID reports are transformed.
/// </summary>
public sealed class A2450TransformOptions
{
    /// <summary>
    /// When true, physical Fn/Globe emits Left Ctrl and physical Left Ctrl becomes internal FnLayer.
    /// </summary>
    public bool SwapFnAndLeftCtrl { get; set; } = true;

    /// <summary>
    /// When true, clear the Apple Fn state byte (byte 9) from the output report.
    /// </summary>
    public bool ClearAppleFnByte { get; set; } = true;

    /// <summary>
    /// When true, FnLayer key remappings (Backspace→Delete, arrows→navigation) are applied.
    /// </summary>
    public bool EnableFnLayer { get; set; } = true;
}

/// <summary>
/// Result of transforming an A2450 HID report, including optional consumer usage for media keys.
/// </summary>
/// <param name="KeyboardReport">The transformed 10-byte keyboard HID report.</param>
/// <param name="ConsumerUsage">
/// Consumer Control usage code if FnLayer + F7~F12 was detected, null otherwise.
/// Should be sent via COL02 (UsagePage 0x000C).
/// </param>
public sealed record A2450TransformResult(
    byte[] KeyboardReport,
    ushort? ConsumerUsage
);

/// <summary>
/// Transforms Apple Magic Keyboard A2450 HID reports to swap Fn/Ctrl and apply FnLayer mappings.
///
/// A2450 USB HID Report (10 bytes):
///   Byte 0 = Report ID (0x01)
///   Byte 1 = Modifier (standard HID)
///   Byte 2 = Reserved (0x00)
///   Byte 3-8 = Key usage slots (HID Usage Codes)
///   Byte 9 = Apple Fn/Globe state (0x00=released, 0x02=pressed)
///
/// Modifier bits (as observed via USBPcap on A2450):
///   Bit 0 (0x01) = Left Ctrl
///   Bit 1 (0x02) = Left Shift
///   Bit 2 (0x04) = Left Alt (Option)
///   Bit 3 (0x08) = Left GUI (Command)
///   Bit 4 (0x10) = Right Ctrl
///   Bit 5 (0x20) = Right Shift
///   Bit 6 (0x40) = Right Alt
///   Bit 7 (0x80) = Right GUI
/// </summary>
public static class A2450ReportTransformer
{
    private const int ReportLength = 10;
    private const byte ReportId = 0x01;
    private const byte AppleFnMask = 0x02;
    private const byte LeftCtrlMask = 0x01;

    // FnLayer key remapping table
    // F7-F12 media keys need Consumer Control Usage Page 0x0C and should be handled later.
    private static readonly Dictionary<byte, byte> FnLayerMap = new()
    {
        { 0x2A, 0x4C }, // Backspace → Delete
        { 0x52, 0x4B }, // Up        → Page Up
        { 0x51, 0x4E }, // Down      → Page Down
        { 0x50, 0x4A }, // Left      → Home
        { 0x4F, 0x4D }, // Right     → End
    };

    /// <summary>
    /// Transforms a 10-byte A2450 HID report according to Fn/Ctrl swap rules.
    /// Returns a new 10-byte array; the input is not modified.
    /// </summary>
    public static byte[] Transform(byte[] inputReport, A2450TransformOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inputReport);

        if (inputReport.Length != ReportLength)
            throw new ArgumentException($"Report must be {ReportLength} bytes, got {inputReport.Length}.", nameof(inputReport));

        options ??= new A2450TransformOptions();

        // Work on a copy so the caller's array is untouched.
        var output = (byte[])inputReport.Clone();

        // Only process keyboard report (Report ID 0x01).
        if (output[0] != ReportId)
            return output;

        // --- Read original physical state BEFORE any mutation ---
        bool physicalFnDown = (output[9] & AppleFnMask) != 0;
        bool physicalLeftCtrlDown = (output[1] & LeftCtrlMask) != 0;

        if (!options.SwapFnAndLeftCtrl)
            return output;

        // --- Step 1: Physical Fn → Left Ctrl ---
        // If Fn is pressed, set the Left Ctrl modifier bit.
        if (physicalFnDown)
        {
            output[1] |= LeftCtrlMask;
        }

        // Clear Apple Fn byte if requested.
        if (options.ClearAppleFnByte)
        {
            output[9] &= unchecked((byte)~AppleFnMask);
        }

        // --- Step 2: Physical Left Ctrl → internal FnLayer ---
        // Remove the physical Left Ctrl from the modifier so it doesn't reach the system.
        // The Left Ctrl bit in the output now comes ONLY from Fn (if pressed).
        if (physicalLeftCtrlDown)
        {
            output[1] &= unchecked((byte)~LeftCtrlMask);
        }

        // --- Step 3: FnLayer key remapping ---
        // FnLayer is active when the PHYSICAL Left Ctrl was held (before we cleared it).
        if (options.EnableFnLayer && physicalLeftCtrlDown)
        {
            for (int i = 3; i <= 8; i++)
            {
                if (output[i] != 0x00 && FnLayerMap.TryGetValue(output[i], out byte mapped))
                {
                    output[i] = mapped;
                }
            }
        }

        // Restore the Fn-mapped Left Ctrl bit if Fn was pressed.
        // This must happen AFTER Step 2 so that physical Ctrl's removal doesn't also remove Fn's Ctrl.
        if (physicalFnDown)
        {
            output[1] |= LeftCtrlMask;
        }

        return output;
    }

    /// <summary>
    /// Transforms a 10-byte A2450 HID report and also detects FnLayer + F7~F12 media key combos.
    /// Returns both the transformed keyboard report and an optional Consumer Control usage.
    /// The ConsumerUsage should be sent via COL02 (UsagePage 0x000C), not COL01.
    /// </summary>
    public static A2450TransformResult TransformWithConsumerUsage(byte[] inputReport, A2450TransformOptions? options = null)
    {
        var keyboardReport = Transform(inputReport, options);

        // Detect FnLayer + F7~F12 from the ORIGINAL input (before transformation).
        // FnLayer is active when physical Left Ctrl is held.
        ushort? consumerUsage = null;

        if (inputReport.Length >= ReportLength && inputReport[0] == ReportId)
        {
            bool physicalLeftCtrlDown = (inputReport[1] & LeftCtrlMask) != 0;

            if (physicalLeftCtrlDown)
            {
                for (int i = 3; i <= 8; i++)
                {
                    if (inputReport[i] != 0x00)
                    {
                        consumerUsage = A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(inputReport[i]);
                        if (consumerUsage.HasValue)
                            break; // Take the first media key found
                    }
                }
            }
        }

        return new A2450TransformResult(keyboardReport, consumerUsage);
    }
}

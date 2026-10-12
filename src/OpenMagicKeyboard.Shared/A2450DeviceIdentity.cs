namespace OpenMagicKeyboard.Shared;

/// <summary>
/// Recognizes the USB and classic-Bluetooth hardware ID spellings observed for
/// Apple Magic Keyboard A2450 (Apple vendor 0x05AC, product 0x029C).
/// This is device discovery only: it does not claim that Bluetooth Fn reports
/// use the same layout as the independently verified USB reports.
/// </summary>
public static class A2450DeviceIdentity
{
    // Windows USB HID symbolic links: HID#VID_05AC&PID_029C...
    private const string UsbId = "VID_05AC&PID_029C";

    // Windows Bluetooth HID IDs: BTHENUM\{...}_VID&0001004C_PID&029C
    // or corresponding HID symbolic links. Bluetooth VID uses Bluetooth SIG
    // company ID formatting rather than the USB VID_05AC spelling.
    private const string BluetoothId = "VID&0001004C_PID&029C";

    public static bool IsA2450(string? devicePathOrId) =>
        GetTransport(devicePathOrId) != null;

    /// <returns>"USB", "Bluetooth", or null if not recognized.</returns>
    public static string? GetTransport(string? devicePathOrId)
    {
        if (string.IsNullOrWhiteSpace(devicePathOrId))
            return null;

        if (devicePathOrId.Contains(BluetoothId, StringComparison.OrdinalIgnoreCase))
            return "Bluetooth";

        if (devicePathOrId.Contains(UsbId, StringComparison.OrdinalIgnoreCase))
            return "USB";

        return null;
    }
}

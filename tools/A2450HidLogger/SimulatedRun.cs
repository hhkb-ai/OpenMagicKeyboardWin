using System.Text.Json;
using OpenMagicKeyboard.Shared;

namespace A2450HidLogger;

internal static class SimulatedRun
{
    public static void WriteLogs()
    {
        Directory.CreateDirectory("logs");

        var devices = new[]
        {
            new KeyboardDeviceInfo(
                DeviceId: @"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0001004C_PID&SIMU",
                Name: "Apple Magic Keyboard A2450 (simulated)",
                Manufacturer: "Apple Inc. (simulated)",
                PnpClass: "Keyboard",
                Service: "kbdhid",
                Vid: "004C",
                Pid: "SIMU",
                LooksLikeAppleKeyboard: true,
                LooksLikeBluetooth: true,
                LooksLikeUsb: false
            )
        };

        File.WriteAllText(
            Path.Combine("logs", "device-snapshot.simulated.json"),
            JsonSerializer.Serialize(devices, JsonOptions.Default));

        var baseTime = DateTimeOffset.Now;
        var events = new[]
        {
            Event(baseTime.AddMilliseconds(0), 0xA2, 0x1D, true, "VK_LCONTROL down"),
            Event(baseTime.AddMilliseconds(120), 0xA2, 0x1D, false, "VK_LCONTROL up"),
            Event(baseTime.AddMilliseconds(240), 0x70, 0x3B, true, "F1 down"),
            Event(baseTime.AddMilliseconds(360), 0x70, 0x3B, false, "F1 up"),
            Event(baseTime.AddMilliseconds(480), 0x08, 0x0E, true, "Backspace down"),
            Event(baseTime.AddMilliseconds(600), 0x08, 0x0E, false, "Backspace up"),
            Event(baseTime.AddMilliseconds(720), 0x25, 0x4B, true, "Left Arrow down"),
            Event(baseTime.AddMilliseconds(840), 0x25, 0x4B, false, "Left Arrow up")
        };

        var jsonlPath = Path.Combine("logs", "a2450-key-events.simulated.jsonl");
        File.WriteAllLines(jsonlPath, events.Select(e => JsonSerializer.Serialize(e, JsonOptions.Compact)));

        Console.WriteLine("Simulation complete.");
        Console.WriteLine("Generated:");
        Console.WriteLine("  logs/device-snapshot.simulated.json");
        Console.WriteLine("  logs/a2450-key-events.simulated.jsonl");
        Console.WriteLine();
        Console.WriteLine("These files are only format samples. They are not real A2450 HID data.");
    }

    private static KeyEventRecord Event(DateTimeOffset time, int virtualKey, int scanCode, bool down, string note)
    {
        return new KeyEventRecord(
            Time: time,
            DeviceName: @"\\?\BTHENUM#Dev_SIMULATED_A2450",
            DevicePath: @"\\?\BTHENUM#Dev_SIMULATED_A2450",
            VirtualKey: virtualKey,
            ScanCode: scanCode,
            Flags: 0,
            Message: down ? 0x0100 : 0x0101,
            IsKeyDown: down,
            IsKeyUp: !down,
            Note: note,
            RawReportHex: null
        );
    }
}

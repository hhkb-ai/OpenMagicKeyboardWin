using System.Runtime.InteropServices;
using System.Text.Json;
using OpenMagicKeyboard.Shared;

namespace A2450HidLogger;

internal sealed class RawInputSink : Form
{
    private readonly string _eventLogPath = Path.Combine("logs", "a2450-key-events.jsonl");

    public RawInputSink()
    {
        Text = "A2450 HID Logger - close to stop";
        Width = 520;
        Height = 140;
        StartPosition = FormStartPosition.CenterScreen;

        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "A2450 HID Logger is running.\nPress the test keys, then close this window.",
        };
        Controls.Add(label);

        RegisterKeyboardRawInput();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == RawInputNative.WM_INPUT)
        {
            TryHandleRawInput(m.LParam);
        }

        base.WndProc(ref m);
    }

    private void RegisterKeyboardRawInput()
    {
        var devices = new[]
        {
            new RawInputNative.RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x06,
                dwFlags = RawInputNative.RIDEV_INPUTSINK,
                hwndTarget = Handle
            }
        };

        var ok = RawInputNative.RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RawInputNative.RAWINPUTDEVICE>());

        if (!ok)
        {
            throw new InvalidOperationException($"RegisterRawInputDevices failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private void TryHandleRawInput(IntPtr lParam)
    {
        uint size = 0;
        RawInputNative.GetRawInputData(
            lParam,
            RawInputNative.RID_INPUT,
            IntPtr.Zero,
            ref size,
            (uint)Marshal.SizeOf<RawInputNative.RAWINPUTHEADER>());

        if (size == 0)
            return;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var read = RawInputNative.GetRawInputData(
                lParam,
                RawInputNative.RID_INPUT,
                buffer,
                ref size,
                (uint)Marshal.SizeOf<RawInputNative.RAWINPUTHEADER>());

            if (read == uint.MaxValue)
                return;

            var raw = Marshal.PtrToStructure<RawInputNative.RAWINPUT>(buffer);
            if (raw.header.dwType != RawInputNative.RIM_TYPEKEYBOARD)
                return;

            var message = unchecked((int)raw.keyboard.Message);
            var isDown = message is 0x0100 or 0x0104; // WM_KEYDOWN / WM_SYSKEYDOWN
            var isUp = message is 0x0101 or 0x0105;   // WM_KEYUP / WM_SYSKEYUP
            var deviceName = RawInputNative.GetDeviceName(raw.header.hDevice);

            var record = new KeyEventRecord(
                Time: DateTimeOffset.Now,
                DeviceName: deviceName,
                DevicePath: deviceName,
                VirtualKey: raw.keyboard.VKey,
                ScanCode: raw.keyboard.MakeCode,
                Flags: raw.keyboard.Flags,
                Message: message,
                IsKeyDown: isDown,
                IsKeyUp: isUp,
                Note: KeyNotes.Describe(raw.keyboard.VKey, raw.keyboard.MakeCode, raw.keyboard.Flags),
                RawReportHex: null
            );

            var line = JsonSerializer.Serialize(record, JsonOptions.Compact);
            File.AppendAllText(_eventLogPath, line + Environment.NewLine);
            Console.WriteLine(line);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}

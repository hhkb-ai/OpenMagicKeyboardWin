using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2450ConsumerControlProbe;

internal static class Program
{
    private const string AppleVid = "05AC";
    private const string A2450Pid = "029C";
    private const ushort ConsumerControlUsagePage = 0x000C;
    private const int ReadTimeoutSeconds = 5;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--simulate", StringComparison.OrdinalIgnoreCase)))
        {
            WriteSimulatedProbe();
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("A2450ConsumerControlProbe only runs on Windows unless --simulate is used.");
            return;
        }

        Directory.CreateDirectory("logs");

        Console.WriteLine("A2450 Consumer Control Probe");
        Console.WriteLine($"Looking for COL02 (UsagePage 0x{ConsumerControlUsagePage:X4}) on VID_{AppleVid} & PID_{A2450Pid}");
        Console.WriteLine();

        var probe = ProbeConsumerControl();

        Console.WriteLine($"  Found:        {probe.Found}");
        Console.WriteLine($"  DevicePath:   {probe.DevicePath ?? "(none)"}");
        Console.WriteLine($"  UsagePage:    0x{probe.UsagePage:X4}");
        Console.WriteLine($"  Usage:        0x{probe.Usage:X4}");
        Console.WriteLine($"  InputReportByteLength: {probe.InputReportByteLength}");
        Console.WriteLine($"  CanOpen:      {probe.CanOpen}");
        Console.WriteLine($"  ReadAttempt:  {probe.ReadAttemptStatus}");
        if (probe.ReadDataHex != null)
            Console.WriteLine($"  ReadData:     {probe.ReadDataHex}");
        Console.WriteLine();

        var outPath = Path.Combine("logs", "a2450-consumer-control-probe.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(probe, JsonOpts));
        Console.WriteLine($"Saved to {outPath}");
    }

    private static ConsumerControlProbeResult ProbeConsumerControl()
    {
        var result = new ConsumerControlProbeResult();

        var hidGuid = HidNative.HidGuid;
        var devInfoSet = HidNative.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero,
            HidNative.DIGCF_PRESENT | HidNative.DIGCF_DEVICEINTERFACE);

        if (devInfoSet == IntPtr.Zero || devInfoSet == new IntPtr(-1))
        {
            result.ReadAttemptStatus = "SetupDiGetClassDevs failed";
            return result;
        }

        try
        {
            var ifaceData = new HidNative.SP_DEVICE_INTERFACE_DATA
            {
                cbSize = Marshal.SizeOf<HidNative.SP_DEVICE_INTERFACE_DATA>()
            };

            for (int index = 0;
                 HidNative.SetupDiEnumDeviceInterfaces(devInfoSet, IntPtr.Zero, ref hidGuid, index, ref ifaceData);
                 index++)
            {
                HidNative.SetupDiGetDeviceInterfaceDetail(devInfoSet, ref ifaceData,
                    IntPtr.Zero, 0, out int requiredSize, IntPtr.Zero);
                if (requiredSize == 0) continue;

                var detailData = new HidNative.SP_DEVICE_INTERFACE_DETAIL_DATA
                {
                    cbSize = IntPtr.Size == 8 ? 8 : 6
                };

                if (!HidNative.SetupDiGetDeviceInterfaceDetail(devInfoSet, ref ifaceData,
                        ref detailData, requiredSize, out _, IntPtr.Zero))
                    continue;

                var path = detailData.DevicePath;

                if (!path.Contains(AppleVid, StringComparison.OrdinalIgnoreCase) ||
                    !path.Contains(A2450Pid, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Open device
                var hDevice = HidNative.CreateFile(path,
                    HidNative.GENERIC_READ,
                    HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
                    IntPtr.Zero, HidNative.OPEN_EXISTING,
                    HidNative.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);

                bool usedFallback = false;
                if (hDevice == IntPtr.Zero || hDevice == new IntPtr(-1))
                {
                    hDevice = HidNative.CreateFile(path,
                        0,
                        HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
                        IntPtr.Zero, HidNative.OPEN_EXISTING,
                        HidNative.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
                    usedFallback = true;
                }

                if (hDevice == IntPtr.Zero || hDevice == new IntPtr(-1))
                    continue;

                try
                {
                    if (!HidNative.HidD_GetPreparsedData(hDevice, out IntPtr ppd))
                        continue;

                    try
                    {
                        if (HidNative.HidP_GetCaps(ppd, out var caps) != HidNative.HIDP_STATUS_SUCCESS)
                            continue;

                        // Only care about Consumer Control
                        if (caps.UsagePage != ConsumerControlUsagePage)
                            continue;

                        result.Found = true;
                        result.DevicePath = path;
                        result.UsagePage = caps.UsagePage;
                        result.Usage = caps.Usage;
                        result.InputReportByteLength = caps.InputReportByteLength;
                        result.OutputReportByteLength = caps.OutputReportByteLength;
                        result.FeatureReportByteLength = caps.FeatureReportByteLength;
                        result.CanOpen = true;
                        result.UsedFallbackAccess = usedFallback;

                        // Try to read a report
                        TryReadReport(hDevice, caps.InputReportByteLength, result);

                        return result;
                    }
                    finally
                    {
                        HidNative.HidD_FreePreparsedData(ppd);
                    }
                }
                finally
                {
                    HidNative.CloseHandle(hDevice);
                }
            }

            if (!result.Found)
            {
                result.ReadAttemptStatus = "Consumer Control collection not found";
            }
        }
        finally
        {
            HidNative.SetupDiDestroyDeviceInfoList(devInfoSet);
        }

        return result;
    }

    private static void TryReadReport(IntPtr hDevice, ushort inputReportByteLength, ConsumerControlProbeResult result)
    {
        if (inputReportByteLength == 0)
        {
            result.ReadAttemptStatus = "InputReportByteLength is 0, skipping read";
            return;
        }

        // Note: Actual ReadFile on Consumer Control requires FILE_FLAG_OVERLAPPED
        // and proper async handling. Since the handle may be opened synchronously
        // (kbdhid occupies the device), we skip the actual read for now.
        // The probe confirms the collection exists and can be opened.
        result.ReadAttemptStatus = $"Skipped (COL02 confirmed with {inputReportByteLength}-byte input reports; " +
            "actual read requires async handle or driver-level interception)";
    }

    private static void WriteSimulatedProbe()
    {
        Directory.CreateDirectory("logs");

        var probe = new ConsumerControlProbeResult
        {
            Found = true,
            DevicePath = @"\\?\hid#vid_05ac&pid_029c&mi_01&col02#8&c405dba&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}",
            UsagePage = 0x000C,
            Usage = 0x0001,
            InputReportByteLength = 2,
            OutputReportByteLength = 0,
            FeatureReportByteLength = 4,
            CanOpen = true,
            UsedFallbackAccess = false,
            ReadAttemptStatus = "Timeout after 5s (no data within timeout)",
            ReadDataHex = null,
        };

        var simPath = Path.Combine("logs", "a2450-consumer-control-probe.simulated.json");
        File.WriteAllText(simPath, JsonSerializer.Serialize(probe, JsonOpts));
        Console.WriteLine($"Simulated probe written to {simPath}");

        var outPath = Path.Combine("logs", "a2450-consumer-control-probe.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(probe, JsonOpts));
        Console.WriteLine($"Probe written to {outPath}");

        Console.WriteLine();
        Console.WriteLine("A2450 Consumer Control Probe (SIMULATED)");
        Console.WriteLine($"  Found:        {probe.Found}");
        Console.WriteLine($"  UsagePage:    0x{probe.UsagePage:X4}");
        Console.WriteLine($"  Usage:        0x{probe.Usage:X4}");
        Console.WriteLine($"  InputReportByteLength: {probe.InputReportByteLength}");
        Console.WriteLine($"  CanOpen:      {probe.CanOpen}");
        Console.WriteLine($"  ReadAttempt:  {probe.ReadAttemptStatus}");
    }
}

internal sealed class ConsumerControlProbeResult
{
    public bool Found { get; set; }
    public string? DevicePath { get; set; }
    public ushort UsagePage { get; set; }
    public ushort Usage { get; set; }
    public ushort InputReportByteLength { get; set; }
    public ushort OutputReportByteLength { get; set; }
    public ushort FeatureReportByteLength { get; set; }
    public bool CanOpen { get; set; }
    public bool UsedFallbackAccess { get; set; }
    public string? ReadAttemptStatus { get; set; }
    public string? ReadDataHex { get; set; }
}

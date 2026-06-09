using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2450DescriptorDump;

internal static class Program
{
    private const string AppleVid = "05AC";
    private const string A2450Pid = "029C";

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
            WriteSimulatedDump();
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("A2450DescriptorDump only runs on Windows unless --simulate is used.");
            Console.Error.WriteLine("Try: dotnet run --project tools/A2450DescriptorDump -- --simulate");
            return;
        }

        Directory.CreateDirectory("logs");

        var entries = EnumerateA2450Collections().ToList();

        Console.WriteLine("A2450 HID Descriptor Dump");
        Console.WriteLine($"Found {entries.Count} HID collection(s) for VID_{AppleVid} & PID_{A2450Pid}");
        Console.WriteLine();

        foreach (var e in entries)
        {
            var cc = e.UsagePage == 0x0C ? " *** Consumer Control! ***" : "";
            Console.WriteLine($"  {e.CollectionLabel,-8} UsagePage=0x{e.UsagePage:X4} Usage=0x{e.Usage:X4}{cc}");
            Console.WriteLine($"           Path: {e.DevicePath}");
            Console.WriteLine($"           InputReportByteLength:  {e.InputReportByteLength}");
            Console.WriteLine($"           OutputReportByteLength: {e.OutputReportByteLength}");
            Console.WriteLine($"           FeatureReportByteLength:{e.FeatureReportByteLength}");
            Console.WriteLine();
        }

        var hasConsumerControl = entries.Any(e => e.UsagePage == 0x0C);
        Console.WriteLine(hasConsumerControl
            ? "RESULT: Consumer Control Usage Page 0x0C FOUND — media keys may be possible."
            : "RESULT: No Consumer Control Usage Page 0x0C detected on this device.");

        var dump = new DescriptorDump
        {
            DeviceVid = AppleVid,
            DevicePid = A2450Pid,
            Collections = entries,
            HasConsumerControl = hasConsumerControl,
        };

        var outPath = Path.Combine("logs", "a2450-descriptor-dump.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(dump, JsonOpts));
        Console.WriteLine($"\nSaved to {outPath}");
    }

    private static IEnumerable<HidCollectionEntry> EnumerateA2450Collections()
    {
        var hidGuid = HidNative.HidGuid;
        var devInfoSet = HidNative.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero,
            HidNative.DIGCF_PRESENT | HidNative.DIGCF_DEVICEINTERFACE);

        if (devInfoSet == IntPtr.Zero || devInfoSet == new IntPtr(-1))
            yield break;

        try
        {
            var ifaceData = new HidNative.SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<HidNative.SP_DEVICE_INTERFACE_DATA>() };

            for (int index = 0; HidNative.SetupDiEnumDeviceInterfaces(devInfoSet, IntPtr.Zero, ref hidGuid, index, ref ifaceData); index++)
            {
                // Get required size
                HidNative.SetupDiGetDeviceInterfaceDetail(devInfoSet, ref ifaceData, IntPtr.Zero, 0, out int requiredSize, IntPtr.Zero);
                if (requiredSize == 0) continue;

                var detailData = new HidNative.SP_DEVICE_INTERFACE_DETAIL_DATA { cbSize = IntPtr.Size == 8 ? 8 : 6 };
                if (!HidNative.SetupDiGetDeviceInterfaceDetail(devInfoSet, ref ifaceData, ref detailData, requiredSize, out _, IntPtr.Zero))
                    continue;

                var path = detailData.DevicePath;

                // Filter by VID & PID
                if (!path.Contains(AppleVid, StringComparison.OrdinalIgnoreCase) ||
                    !path.Contains(A2450Pid, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Determine collection label
                string label = "UNKNOWN";
                if (path.Contains("MI_01&COL01", StringComparison.OrdinalIgnoreCase)) label = "COL01";
                else if (path.Contains("MI_01&COL02", StringComparison.OrdinalIgnoreCase)) label = "COL02";
                else if (path.Contains("MI_01&COL03", StringComparison.OrdinalIgnoreCase)) label = "COL03";
                else if (path.Contains("MI_00", StringComparison.OrdinalIgnoreCase)) label = "MI_00";
                else if (path.Contains("MI_01", StringComparison.OrdinalIgnoreCase)) label = "MI_01";

                // Open device and get caps
                // Try GENERIC_READ first; if the device is occupied by kbdhid,
                // fall back to desiredAccess=0 which still allows HidD_GetPreparsedData.
                var hDevice = HidNative.CreateFile(path,
                    HidNative.GENERIC_READ,
                    HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
                    IntPtr.Zero, HidNative.OPEN_EXISTING,
                    HidNative.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);

                if (hDevice == IntPtr.Zero || hDevice == new IntPtr(-1))
                {
                    hDevice = HidNative.CreateFile(path,
                        0,
                        HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
                        IntPtr.Zero, HidNative.OPEN_EXISTING,
                        HidNative.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
                }

                if (hDevice == IntPtr.Zero || hDevice == new IntPtr(-1))
                    continue;

                try
                {
                    var attrs = new HidNative.HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HidNative.HIDD_ATTRIBUTES>() };
                    HidNative.HidD_GetAttributes(hDevice, ref attrs);

                    if (HidNative.HidD_GetPreparsedData(hDevice, out IntPtr ppd))
                    {
                        try
                        {
                            if (HidNative.HidP_GetCaps(ppd, out var caps) == HidNative.HIDP_STATUS_SUCCESS)
                            {
                                yield return new HidCollectionEntry
                                {
                                    DevicePath = path,
                                    CollectionLabel = label,
                                    UsagePage = caps.UsagePage,
                                    Usage = caps.Usage,
                                    InputReportByteLength = caps.InputReportByteLength,
                                    OutputReportByteLength = caps.OutputReportByteLength,
                                    FeatureReportByteLength = caps.FeatureReportByteLength,
                                    VendorId = attrs.VendorID,
                                    ProductId = attrs.ProductID,
                                };
                            }
                        }
                        finally
                        {
                            HidNative.HidD_FreePreparsedData(ppd);
                        }
                    }
                }
                finally
                {
                    HidNative.CloseHandle(hDevice);
                }
            }
        }
        finally
        {
            HidNative.SetupDiDestroyDeviceInfoList(devInfoSet);
        }
    }

    private static void WriteSimulatedDump()
    {
        Directory.CreateDirectory("logs");

        var entries = new List<HidCollectionEntry>
        {
            new()
            {
                DevicePath = @"\\?\hid#vid_05ac&pid_029c&mi_01&col01#8&c405dba&0&0000",
                CollectionLabel = "COL01",
                UsagePage = 0x01,
                Usage = 0x06,
                InputReportByteLength = 10,
                OutputReportByteLength = 0,
                FeatureReportByteLength = 0,
                VendorId = 0x05AC,
                ProductId = 0x029C,
            },
            new()
            {
                DevicePath = @"\\?\hid#vid_05ac&pid_029c&mi_01&col02#8&c405dba&0&0001",
                CollectionLabel = "COL02",
                UsagePage = 0x0C,
                Usage = 0x01,
                InputReportByteLength = 2,
                OutputReportByteLength = 0,
                FeatureReportByteLength = 4,
                VendorId = 0x05AC,
                ProductId = 0x029C,
            },
            new()
            {
                DevicePath = @"\\?\hid#vid_05ac&pid_029c&mi_01&col03#8&c405dba&0&0002",
                CollectionLabel = "COL03",
                UsagePage = 0xFF00,
                Usage = 0x0006,
                InputReportByteLength = 65,
                OutputReportByteLength = 0,
                FeatureReportByteLength = 0,
                VendorId = 0x05AC,
                ProductId = 0x029C,
            },
            new()
            {
                DevicePath = @"\\?\hid#vid_05ac&pid_029c&mi_00&col01#8&30179b7c&0&0000",
                CollectionLabel = "MI_00",
                UsagePage = 0xFF00,
                Usage = 0x000B,
                InputReportByteLength = 5,
                OutputReportByteLength = 0,
                FeatureReportByteLength = 0,
                VendorId = 0x05AC,
                ProductId = 0x029C,
            },
            new()
            {
                DevicePath = @"\\?\hid#vid_05ac&pid_029c&mi_00&col02#8&30179b7c&0&0001",
                CollectionLabel = "MI_00",
                UsagePage = 0xFF00,
                Usage = 0x0014,
                InputReportByteLength = 3,
                OutputReportByteLength = 0,
                FeatureReportByteLength = 0,
                VendorId = 0x05AC,
                ProductId = 0x029C,
            },
        };

        var hasConsumerControl = entries.Any(e => e.UsagePage == 0x0C);

        var dump = new DescriptorDump
        {
            DeviceVid = AppleVid,
            DevicePid = A2450Pid,
            Collections = entries,
            HasConsumerControl = hasConsumerControl,
        };

        // Write simulated log (for CI)
        var simPath = Path.Combine("logs", "a2450-descriptor-dump.simulated.json");
        File.WriteAllText(simPath, JsonSerializer.Serialize(dump, JsonOpts));
        Console.WriteLine($"Simulated descriptor dump written to {simPath}");

        // Also write the "real" log so the tool can be tested end-to-end
        var outPath = Path.Combine("logs", "a2450-descriptor-dump.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(dump, JsonOpts));
        Console.WriteLine($"Descriptor dump written to {outPath}");

        Console.WriteLine();
        Console.WriteLine("A2450 HID Descriptor Dump (SIMULATED)");
        Console.WriteLine($"Found {entries.Count} HID collection(s)");
        Console.WriteLine();
        foreach (var e in entries)
        {
            var cc = e.UsagePage == 0x0C ? " *** Consumer Control! ***" : "";
            Console.WriteLine($"  {e.CollectionLabel,-8} UsagePage=0x{e.UsagePage:X4} Usage=0x{e.Usage:X4}{cc}");
            Console.WriteLine($"           InputReportByteLength:  {e.InputReportByteLength}");
            Console.WriteLine();
        }

        Console.WriteLine(hasConsumerControl
            ? "RESULT: Consumer Control Usage Page 0x0C FOUND — media keys may be possible."
            : "RESULT: No Consumer Control Usage Page 0x0C detected.");
    }
}

internal sealed class DescriptorDump
{
    public string? DeviceVid { get; set; }
    public string? DevicePid { get; set; }
    public List<HidCollectionEntry>? Collections { get; set; }
    public bool HasConsumerControl { get; set; }
}

internal sealed class HidCollectionEntry
{
    public string? DevicePath { get; set; }
    public string? CollectionLabel { get; set; }
    public ushort UsagePage { get; set; }
    public ushort Usage { get; set; }
    public ushort InputReportByteLength { get; set; }
    public ushort OutputReportByteLength { get; set; }
    public ushort FeatureReportByteLength { get; set; }
    public ushort VendorId { get; set; }
    public ushort ProductId { get; set; }
}

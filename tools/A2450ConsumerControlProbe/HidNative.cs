using System.Runtime.InteropServices;

namespace A2450ConsumerControlProbe;

internal static class HidNative
{
    // --- GUID_DEVINTERFACE_HID ---
    public static Guid HidGuid = new("4d1e55b2-f16f-11cf-88cb-001111000030");

    // --- SetupAPI constants ---
    public const int DIGCF_PRESENT = 0x02;
    public const int DIGCF_DEVICEINTERFACE = 0x10;
    public const int ERROR_NO_MORE_ITEMS = 259;
    public const int ERROR_INSUFFICIENT_BUFFER = 122;

    // --- Kernel32 constants ---
    public const uint GENERIC_READ = 0x80000000;
    public const uint FILE_SHARE_READ = 0x01;
    public const uint FILE_SHARE_WRITE = 0x02;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    public const uint INVALID_HANDLE_VALUE = 0xFFFFFFFF;
    public const uint WAIT_OBJECT_0 = 0;
    public const uint WAIT_TIMEOUT = 258;
    public const uint ERROR_IO_PENDING = 997;
    public const uint ERROR_OPERATION_ABORTED = 995;

    // --- HidP constants ---
    public const int HidP_Input = 0;
    public const int HIDP_STATUS_SUCCESS = 0x00110000;

    // --- SetupAPI structs ---

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string DevicePath;
    }

    // --- HID structs ---

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    // --- Overlapped struct ---

    [StructLayout(LayoutKind.Sequential)]
    public struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public int OffsetLow;
        public int OffsetHigh;
        public IntPtr hEvent;
    }

    // --- SetupAPI P/Invoke ---

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        string? Enumerator,
        IntPtr hwndParent,
        int Flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr DeviceInfoSet,
        IntPtr DeviceInfoData,
        ref Guid InterfaceClassGuid,
        int MemberIndex,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        int DeviceInterfaceDetailDataSize,
        out int RequiredSize,
        IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        int DeviceInterfaceDetailDataSize,
        out int RequiredSize,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData,
        int DeviceInterfaceDetailDataSize,
        out int RequiredSize,
        IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    // --- HID P/Invoke ---

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetAttributes(
        IntPtr HidDeviceObject,
        ref HIDD_ATTRIBUTES Attributes);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetInputReport(
        IntPtr HidDeviceObject,
        byte[] ReportBuffer,
        int ReportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetPreparsedData(
        IntPtr HidDeviceObject,
        out IntPtr PreparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern int HidP_GetCaps(
        IntPtr PreparsedData,
        out HIDP_CAPS Capabilities);

    // --- Kernel32 P/Invoke ---

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(
        IntPtr hFile,
        byte[] lpBuffer,
        int nNumberOfBytesToRead,
        out int lpNumberOfBytesRead,
        ref OVERLAPPED lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetOverlappedResult(
        IntPtr hFile,
        ref OVERLAPPED lpOverlapped,
        out int lpNumberOfBytesTransferred,
        bool bWait);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateEvent(
        IntPtr lpEventAttributes,
        bool bManualReset,
        bool bInitialState,
        string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CancelIo(IntPtr hFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ResetEvent(IntPtr hEvent);

    // --- DeviceIoControl for HID ---

    // IOCTL_HID_READ_REPORT = CTL_CODE(FILE_DEVICE_KEYBOARD=0x0B, 0x0A, METHOD_NEITHER=3, FILE_ANY_ACCESS=0)
    public const uint IOCTL_HID_READ_REPORT = (0x0B << 16) | (0x0A << 2) | 3;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        byte[] lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);
}

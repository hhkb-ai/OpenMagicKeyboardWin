namespace OpenMagicKeyboard.Shared;

public sealed record KeyEventRecord(
    DateTimeOffset Time,
    string? DeviceName,
    string? DevicePath,
    int VirtualKey,
    int ScanCode,
    int Flags,
    int Message,
    bool IsKeyDown,
    bool IsKeyUp,
    string? Note = null,
    string? RawReportHex = null
);

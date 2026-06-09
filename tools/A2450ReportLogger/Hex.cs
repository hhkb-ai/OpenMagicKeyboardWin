namespace A2450ReportLogger;

internal static class Hex
{
    public static string ToHex(ReadOnlySpan<byte> data)
    {
        return Convert.ToHexString(data).ToLowerInvariant();
    }
}

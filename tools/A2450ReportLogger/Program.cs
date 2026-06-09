namespace A2450ReportLogger;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--simulate", StringComparison.OrdinalIgnoreCase)))
        {
            SimulatedReportRun.WriteLogs();
            return;
        }

        Console.WriteLine("A2450ReportLogger");
        Console.WriteLine();
        Console.WriteLine("This scaffold is ready for the next local implementation step.");
        Console.WriteLine("Run the simulation first:");
        Console.WriteLine("  dotnet run --project .\\tools\\A2450ReportLogger\\A2450ReportLogger.csproj -- --simulate");
        Console.WriteLine();
        Console.WriteLine("Next local task:");
        Console.WriteLine("  Implement Windows HID device enumeration and read reports from the Apple PID 029C COL02 collection.");
        Console.WriteLine();
        Console.WriteLine("Expected future output:");
        Console.WriteLine("  logs/a2450-raw-hid-reports.jsonl");
    }
}

using StreamScanner.Maui.Services;

namespace SecurityScanner;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         SECURITY SCANNER AGENT v1.0                            ║");
        Console.WriteLine("║         Automated Security Vulnerability Detection             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Determine the directory to scan
        var targetDirectory = args.Length > 0 ? args[0] : GetDefaultScanDirectory();

        if (!Directory.Exists(targetDirectory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Directory not found: {targetDirectory}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Scanning directory: {targetDirectory}");
        Console.WriteLine();

        // File extensions to scan
        var extensions = new[] { ".cs", ".js", ".ts", ".json", ".xml", ".config", ".html" };

        Console.WriteLine($"File types: {string.Join(", ", extensions)}");
        Console.WriteLine();
        Console.WriteLine("Scanning...");
        Console.WriteLine();

        var scanner = new SecurityScannerService();
        var report = await scanner.ScanDirectoryAsync(targetDirectory, extensions);

        // Display console report
        Console.WriteLine(report.GenerateConsoleReport());

        // Generate markdown report
        var reportPath = Path.Combine(targetDirectory, "security-report.md");
        await File.WriteAllTextAsync(reportPath, report.GenerateMarkdownReport());

        Console.WriteLine($"Detailed report saved to: {reportPath}");
        Console.WriteLine();

        // Return exit code based on findings
        if (report.CriticalCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("RESULT: FAILED - Critical security issues found!");
            Console.ResetColor();
            return 2;
        }
        else if (report.HighCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("RESULT: WARNING - High severity issues found. Review recommended.");
            Console.ResetColor();
            return 1;
        }
        else if (report.Findings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("RESULT: PASSED with notes - Minor issues found.");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("RESULT: PASSED - No security issues detected!");
            Console.ResetColor();
            return 0;
        }
    }

    static string GetDefaultScanDirectory()
    {
        // Try to find the solution root
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (Directory.GetFiles(current, "*.sln").Length > 0 ||
                Directory.GetFiles(current, "*.csproj").Length > 0)
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}

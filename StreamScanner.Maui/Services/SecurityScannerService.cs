using System.Text.RegularExpressions;

namespace StreamScanner.Maui.Services;

/// <summary>
/// Security Scanner Agent - Automatically checks code for security vulnerabilities
/// Based on OWASP Top 10 and common security best practices
/// </summary>
public interface ISecurityScannerService
{
    /// <summary>
    /// Scan a file for security issues
    /// </summary>
    Task<List<SecurityFinding>> ScanFileAsync(string filePath, string content);

    /// <summary>
    /// Scan a directory recursively for security issues
    /// </summary>
    Task<SecurityReport> ScanDirectoryAsync(string directoryPath, string[] fileExtensions);

    /// <summary>
    /// Get all security rules
    /// </summary>
    List<SecurityRule> GetRules();
}

public class SecurityScannerService : ISecurityScannerService
{
    private readonly List<SecurityRule> _rules;

    public SecurityScannerService()
    {
        _rules = InitializeRules();
    }

    public List<SecurityRule> GetRules() => _rules;

    public Task<List<SecurityFinding>> ScanFileAsync(string filePath, string content)
    {
        var findings = new List<SecurityFinding>();
        var lines = content.Split('\n');
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        foreach (var rule in _rules)
        {
            if (!rule.AppliesTo(extension))
                continue;

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var line = lines[lineNum];

                if (rule.Pattern.IsMatch(line))
                {
                    // Check if this is a false positive (in comments, etc.)
                    if (IsLikelyFalsePositive(line, rule))
                        continue;

                    findings.Add(new SecurityFinding
                    {
                        RuleId = rule.Id,
                        Severity = rule.Severity,
                        Category = rule.Category,
                        Title = rule.Title,
                        Description = rule.Description,
                        FilePath = filePath,
                        LineNumber = lineNum + 1,
                        LineContent = line.Trim(),
                        Recommendation = rule.Recommendation
                    });
                }
            }
        }

        return Task.FromResult(findings);
    }

    public async Task<SecurityReport> ScanDirectoryAsync(string directoryPath, string[] fileExtensions)
    {
        var report = new SecurityReport
        {
            ScanStartTime = DateTime.UtcNow,
            DirectoryScanned = directoryPath
        };

        var allFindings = new List<SecurityFinding>();
        var filesScanned = 0;

        foreach (var extension in fileExtensions)
        {
            var pattern = $"*{extension}";
            var files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                // Skip generated files, bin, obj, etc.
                if (ShouldSkipFile(file))
                    continue;

                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var findings = await ScanFileAsync(file, content);
                    allFindings.AddRange(findings);
                    filesScanned++;
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Error scanning {file}: {ex.Message}");
                }
            }
        }

        report.ScanEndTime = DateTime.UtcNow;
        report.FilesScanned = filesScanned;
        report.Findings = allFindings;

        return report;
    }

    private static bool ShouldSkipFile(string filePath)
    {
        var skipPatterns = new[]
        {
            "/bin/", "\\bin\\",
            "/obj/", "\\obj\\",
            "/node_modules/", "\\node_modules\\",
            "/.git/", "\\.git\\",
            "/packages/", "\\packages\\",
            ".Designer.cs",
            ".g.cs",
            ".generated.cs"
        };

        return skipPatterns.Any(p => filePath.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLikelyFalsePositive(string line, SecurityRule rule)
    {
        var trimmed = line.TrimStart();

        // Skip comments
        if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
            trimmed.StartsWith("*") || trimmed.StartsWith("#"))
            return true;

        // Skip if it's a placeholder/example value
        if (rule.Category == SecurityCategory.HardcodedSecrets)
        {
            var placeholders = new[] { "YOUR_", "EXAMPLE_", "PLACEHOLDER", "xxx", "***", "<your", "[your" };
            if (placeholders.Any(p => line.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static List<SecurityRule> InitializeRules()
    {
        return new List<SecurityRule>
        {
            // === HARDCODED SECRETS (A02:2021 - Cryptographic Failures) ===
            new SecurityRule
            {
                Id = "SEC001",
                Title = "Hardcoded API Key",
                Category = SecurityCategory.HardcodedSecrets,
                Severity = SecuritySeverity.High,
                Description = "Detected potential hardcoded API key. API keys should be stored in secure configuration.",
                Pattern = new Regex(@"[""'][\w-]{20,}[""'].*(?:api[_-]?key|apikey)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Store API keys in environment variables, secure vaults, or encrypted configuration files.",
                FileExtensions = new[] { ".cs", ".js", ".ts", ".json", ".xml", ".config" }
            },
            new SecurityRule
            {
                Id = "SEC002",
                Title = "Hardcoded Password",
                Category = SecurityCategory.HardcodedSecrets,
                Severity = SecuritySeverity.Critical,
                Description = "Detected potential hardcoded password or secret.",
                Pattern = new Regex(@"(?:password|passwd|pwd|secret|token)\s*[=:]\s*[""'][^""']{4,}[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Never hardcode passwords. Use secure credential storage or environment variables.",
                FileExtensions = new[] { ".cs", ".js", ".ts", ".json", ".xml", ".config" }
            },
            new SecurityRule
            {
                Id = "SEC003",
                Title = "Hardcoded Connection String",
                Category = SecurityCategory.HardcodedSecrets,
                Severity = SecuritySeverity.High,
                Description = "Detected potential hardcoded database connection string.",
                Pattern = new Regex(@"(?:connection\s*string|connstr|connectionstring)\s*[=:]\s*[""'][^""']+[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Store connection strings in secure configuration with encryption.",
                FileExtensions = new[] { ".cs", ".js", ".ts", ".json", ".xml", ".config" }
            },

            // === INSECURE COMMUNICATION (A02:2021) ===
            new SecurityRule
            {
                Id = "SEC010",
                Title = "Insecure HTTP URL",
                Category = SecurityCategory.InsecureCommunication,
                Severity = SecuritySeverity.Medium,
                Description = "Using HTTP instead of HTTPS can expose data to interception.",
                Pattern = new Regex(@"[""']http://(?!localhost|127\.0\.0\.1|0\.0\.0\.0)[^""'\s]+[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Use HTTPS for all external communications to ensure data encryption in transit.",
                FileExtensions = new[] { ".cs", ".js", ".ts", ".json", ".xml", ".config", ".html" }
            },
            new SecurityRule
            {
                Id = "SEC011",
                Title = "SSL/TLS Validation Disabled",
                Category = SecurityCategory.InsecureCommunication,
                Severity = SecuritySeverity.Critical,
                Description = "Disabling SSL/TLS certificate validation exposes the application to man-in-the-middle attacks.",
                Pattern = new Regex(@"(?:ServerCertificateValidationCallback|CheckCertificateRevocationList)\s*=\s*(?:false|\(\s*\)\s*=>\s*true)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Never disable certificate validation in production. Use proper certificate management.",
                FileExtensions = new[] { ".cs" }
            },

            // === INJECTION VULNERABILITIES (A03:2021) ===
            new SecurityRule
            {
                Id = "SEC020",
                Title = "Potential SQL Injection",
                Category = SecurityCategory.Injection,
                Severity = SecuritySeverity.Critical,
                Description = "String concatenation in SQL queries can lead to SQL injection attacks.",
                Pattern = new Regex(@"(?:ExecuteSql|ExecuteNonQuery|ExecuteReader|ExecuteScalar|SqlCommand)\s*\([^)]*\+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Use parameterized queries or stored procedures instead of string concatenation.",
                FileExtensions = new[] { ".cs" }
            },
            new SecurityRule
            {
                Id = "SEC021",
                Title = "Potential Command Injection",
                Category = SecurityCategory.Injection,
                Severity = SecuritySeverity.Critical,
                Description = "Passing user input to system commands can lead to command injection.",
                Pattern = new Regex(@"Process\.Start\s*\([^)]*\+|ProcessStartInfo\s*\{[^}]*Arguments\s*=.*\+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Validate and sanitize all input before passing to system commands. Use allowlists when possible.",
                FileExtensions = new[] { ".cs" }
            },
            new SecurityRule
            {
                Id = "SEC022",
                Title = "Potential Path Traversal",
                Category = SecurityCategory.Injection,
                Severity = SecuritySeverity.High,
                Description = "User input in file paths can lead to path traversal attacks.",
                Pattern = new Regex(@"(?:File\.|Directory\.|Path\.Combine)\s*\([^)]*Request\[|(?:File\.|Directory\.|Path\.Combine).*user[Ii]nput", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Validate file paths and use Path.GetFullPath() to resolve paths. Check against allowed directories.",
                FileExtensions = new[] { ".cs" }
            },

            // === SENSITIVE DATA EXPOSURE (A02:2021) ===
            new SecurityRule
            {
                Id = "SEC030",
                Title = "Sensitive Data in Logs",
                Category = SecurityCategory.SensitiveDataExposure,
                Severity = SecuritySeverity.Medium,
                Description = "Logging sensitive data like passwords or tokens can lead to data exposure.",
                Pattern = new Regex(@"(?:Log|Console\.Write|Debug\.Write|Trace\.Write).*(?:password|token|secret|apikey|api_key|credit.?card)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Never log sensitive data. Mask or redact sensitive information before logging.",
                FileExtensions = new[] { ".cs", ".js", ".ts" }
            },
            new SecurityRule
            {
                Id = "SEC031",
                Title = "Exception Details Exposed",
                Category = SecurityCategory.SensitiveDataExposure,
                Severity = SecuritySeverity.Low,
                Description = "Exposing full exception details to users can reveal sensitive system information.",
                Pattern = new Regex(@"(?:ToString\(\)|\.Message|\.StackTrace).*(?:return|Response|result)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Return generic error messages to users. Log detailed exceptions server-side only.",
                FileExtensions = new[] { ".cs" }
            },

            // === INSECURE DESERIALIZATION (A08:2021) ===
            new SecurityRule
            {
                Id = "SEC040",
                Title = "Insecure Deserialization",
                Category = SecurityCategory.InsecureDeserialization,
                Severity = SecuritySeverity.High,
                Description = "BinaryFormatter and similar serializers are vulnerable to deserialization attacks.",
                Pattern = new Regex(@"(?:BinaryFormatter|SoapFormatter|NetDataContractSerializer|ObjectStateFormatter)\.Deserialize", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Use secure serializers like System.Text.Json or Newtonsoft.Json with type handling disabled.",
                FileExtensions = new[] { ".cs" }
            },
            new SecurityRule
            {
                Id = "SEC041",
                Title = "JSON Type Handling Enabled",
                Category = SecurityCategory.InsecureDeserialization,
                Severity = SecuritySeverity.High,
                Description = "TypeNameHandling in JSON.NET can lead to deserialization attacks.",
                Pattern = new Regex(@"TypeNameHandling\s*=\s*TypeNameHandling\.(?!None)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Set TypeNameHandling to None or use System.Text.Json instead.",
                FileExtensions = new[] { ".cs" }
            },

            // === CRYPTOGRAPHIC ISSUES (A02:2021) ===
            new SecurityRule
            {
                Id = "SEC050",
                Title = "Weak Cryptographic Algorithm",
                Category = SecurityCategory.WeakCryptography,
                Severity = SecuritySeverity.High,
                Description = "MD5, SHA1, and DES are considered cryptographically weak.",
                Pattern = new Regex(@"(?:MD5|SHA1|DESCryptoServiceProvider|TripleDES)\.Create\(|new\s+(?:MD5|SHA1|DESCryptoServiceProvider)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Use SHA256 or stronger for hashing. Use AES for encryption.",
                FileExtensions = new[] { ".cs" }
            },
            new SecurityRule
            {
                Id = "SEC051",
                Title = "Hardcoded Cryptographic Key",
                Category = SecurityCategory.WeakCryptography,
                Severity = SecuritySeverity.Critical,
                Description = "Hardcoded encryption keys compromise the security of encrypted data.",
                Pattern = new Regex(@"(?:\.Key|\.IV|AesKey|EncryptionKey)\s*=\s*(?:new\s+byte\[\]|Convert\.FromBase64String)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Generate keys securely at runtime and store them in secure key management systems.",
                FileExtensions = new[] { ".cs" }
            },

            // === XSS VULNERABILITIES (A03:2021) ===
            new SecurityRule
            {
                Id = "SEC060",
                Title = "Potential XSS - innerHTML",
                Category = SecurityCategory.CrossSiteScripting,
                Severity = SecuritySeverity.High,
                Description = "Using innerHTML with user input can lead to XSS attacks.",
                Pattern = new Regex(@"\.innerHTML\s*=(?!.*(?:textContent|innerText))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Use textContent or innerText for text. Sanitize HTML input if innerHTML is required.",
                FileExtensions = new[] { ".js", ".ts", ".html" }
            },
            new SecurityRule
            {
                Id = "SEC061",
                Title = "Potential XSS - document.write",
                Category = SecurityCategory.CrossSiteScripting,
                Severity = SecuritySeverity.High,
                Description = "document.write() can introduce XSS vulnerabilities.",
                Pattern = new Regex(@"document\.write\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Avoid document.write(). Use DOM manipulation methods instead.",
                FileExtensions = new[] { ".js", ".ts", ".html" }
            },

            // === MISCONFIGURATION (A05:2021) ===
            new SecurityRule
            {
                Id = "SEC070",
                Title = "Debug Mode Enabled",
                Category = SecurityCategory.Misconfiguration,
                Severity = SecuritySeverity.Medium,
                Description = "Debug mode should be disabled in production.",
                Pattern = new Regex(@"(?:debug|DEBUG)\s*[=:]\s*(?:true|1|""true"")", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Ensure debug mode is disabled in production configurations.",
                FileExtensions = new[] { ".cs", ".json", ".xml", ".config" }
            },
            new SecurityRule
            {
                Id = "SEC071",
                Title = "CORS Allow All Origins",
                Category = SecurityCategory.Misconfiguration,
                Severity = SecuritySeverity.Medium,
                Description = "Allowing all CORS origins can expose the API to cross-origin attacks.",
                Pattern = new Regex(@"(?:AllowAnyOrigin|Access-Control-Allow-Origin.*\*|cors.*origin.*\*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                Recommendation = "Restrict CORS to specific trusted origins.",
                FileExtensions = new[] { ".cs", ".js", ".ts", ".json" }
            }
        };
    }
}

public class SecurityRule
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SecurityCategory Category { get; set; }
    public SecuritySeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public Regex Pattern { get; set; } = null!;
    public string Recommendation { get; set; } = string.Empty;
    public string[] FileExtensions { get; set; } = Array.Empty<string>();

    public bool AppliesTo(string extension)
    {
        return FileExtensions.Length == 0 ||
               FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

public class SecurityFinding
{
    public string RuleId { get; set; } = string.Empty;
    public SecuritySeverity Severity { get; set; }
    public SecurityCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class SecurityReport
{
    public DateTime ScanStartTime { get; set; }
    public DateTime ScanEndTime { get; set; }
    public string DirectoryScanned { get; set; } = string.Empty;
    public int FilesScanned { get; set; }
    public List<SecurityFinding> Findings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public TimeSpan Duration => ScanEndTime - ScanStartTime;

    public int CriticalCount => Findings.Count(f => f.Severity == SecuritySeverity.Critical);
    public int HighCount => Findings.Count(f => f.Severity == SecuritySeverity.High);
    public int MediumCount => Findings.Count(f => f.Severity == SecuritySeverity.Medium);
    public int LowCount => Findings.Count(f => f.Severity == SecuritySeverity.Low);

    public string GenerateMarkdownReport()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# Security Scan Report");
        sb.AppendLine();
        sb.AppendLine($"**Scan Date:** {ScanStartTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Duration:** {Duration.TotalSeconds:F2} seconds");
        sb.AppendLine($"**Directory:** {DirectoryScanned}");
        sb.AppendLine($"**Files Scanned:** {FilesScanned}");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"| Severity | Count |");
        sb.AppendLine($"|----------|-------|");
        sb.AppendLine($"| Critical | {CriticalCount} |");
        sb.AppendLine($"| High | {HighCount} |");
        sb.AppendLine($"| Medium | {MediumCount} |");
        sb.AppendLine($"| Low | {LowCount} |");
        sb.AppendLine($"| **Total** | **{Findings.Count}** |");
        sb.AppendLine();

        if (Findings.Count == 0)
        {
            sb.AppendLine("No security issues found.");
            return sb.ToString();
        }

        sb.AppendLine("## Findings");
        sb.AppendLine();

        var groupedFindings = Findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Category)
            .GroupBy(f => f.Severity);

        foreach (var group in groupedFindings)
        {
            sb.AppendLine($"### {group.Key} Severity");
            sb.AppendLine();

            foreach (var finding in group)
            {
                sb.AppendLine($"#### [{finding.RuleId}] {finding.Title}");
                sb.AppendLine();
                sb.AppendLine($"**File:** `{finding.FilePath}`");
                sb.AppendLine($"**Line:** {finding.LineNumber}");
                sb.AppendLine($"**Category:** {finding.Category}");
                sb.AppendLine();
                sb.AppendLine($"**Description:** {finding.Description}");
                sb.AppendLine();
                sb.AppendLine("**Code:**");
                sb.AppendLine($"```");
                sb.AppendLine(finding.LineContent);
                sb.AppendLine($"```");
                sb.AppendLine();
                sb.AppendLine($"**Recommendation:** {finding.Recommendation}");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        if (Errors.Count > 0)
        {
            sb.AppendLine("## Scan Errors");
            sb.AppendLine();
            foreach (var error in Errors)
            {
                sb.AppendLine($"- {error}");
            }
        }

        return sb.ToString();
    }

    public string GenerateConsoleReport()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║               SECURITY SCAN REPORT                              ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"  Scan Date: {ScanStartTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"  Duration:  {Duration.TotalSeconds:F2} seconds");
        sb.AppendLine($"  Files:     {FilesScanned} scanned");
        sb.AppendLine();
        sb.AppendLine("┌─────────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│  SUMMARY                                                        │");
        sb.AppendLine("├─────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│  Critical: {CriticalCount,-5} High: {HighCount,-5} Medium: {MediumCount,-5} Low: {LowCount,-5}       │");
        sb.AppendLine($"│  Total Findings: {Findings.Count,-47} │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        if (Findings.Count == 0)
        {
            sb.AppendLine("  ✓ No security issues found!");
            return sb.ToString();
        }

        sb.AppendLine("  FINDINGS:");
        sb.AppendLine();

        foreach (var finding in Findings.OrderByDescending(f => f.Severity))
        {
            var severityIcon = finding.Severity switch
            {
                SecuritySeverity.Critical => "🔴",
                SecuritySeverity.High => "🟠",
                SecuritySeverity.Medium => "🟡",
                SecuritySeverity.Low => "🟢",
                _ => "⚪"
            };

            sb.AppendLine($"  {severityIcon} [{finding.RuleId}] {finding.Title}");
            sb.AppendLine($"     File: {finding.FilePath}:{finding.LineNumber}");
            sb.AppendLine($"     {finding.Description}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public enum SecuritySeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum SecurityCategory
{
    HardcodedSecrets,
    InsecureCommunication,
    Injection,
    SensitiveDataExposure,
    InsecureDeserialization,
    WeakCryptography,
    CrossSiteScripting,
    Misconfiguration
}

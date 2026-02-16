using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string Clean = "clean";
const string Restore = "restore";
const string Build = "build";
const string Test = "test";
const string Format = "format";
const string CheckFormat = "check-format";
const string Publish = "publish";
const string DetermineVersion = "determine-version";
const string UpdateVersion = "update-version";
const string PushToNuGet = "push-to-nuget";
const string DisplayBenchmarkResults = "display-benchmark-results";
const string CompareBenchmarkResults = "compare-benchmark-results";
const string GenerateBaseline = "generate-baseline";

Target(
    Clean,
    ["**/bin", "**/obj"],
    dir =>
    {
        IEnumerable<string> GetDirectories(string d)
        {
            return Glob.Directories(".", d);
        }

        void RemoveDirectory(string d)
        {
            if (Directory.Exists(d))
            {
                Console.WriteLine(d);
                Directory.Delete(d, true);
            }
        }

        foreach (var d in GetDirectories(dir))
        {
            RemoveDirectory(d);
        }
    }
);

Target(
    Format,
    () =>
    {
        Run("dotnet", "tool restore");
        Run("dotnet", "csharpier format .");
    }
);
Target(
    CheckFormat,
    () =>
    {
        Run("dotnet", "tool restore");
        Run("dotnet", "csharpier check .");
    }
);
Target(Restore, [CheckFormat], () => Run("dotnet", "restore"));

Target(
    Build,
    [Restore],
    () =>
    {
        Run("dotnet", "build src/SharpCompress/SharpCompress.csproj -c Release --no-restore");
    }
);

Target(
    Test,
    [Build],
    ["net10.0", "net48"],
    framework =>
    {
        IEnumerable<string> GetFiles(string d)
        {
            return Glob.Files(".", d);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && framework == "net48")
        {
            return;
        }

        foreach (var file in GetFiles("**/*.Test.csproj"))
        {
            Run("dotnet", $"test {file} -c Release -f {framework} --no-restore --verbosity=normal");
        }
    }
);

Target(
    Publish,
    [Test],
    () =>
    {
        Run("dotnet", "pack src/SharpCompress/SharpCompress.csproj -c Release -o artifacts/");
    }
);

Target(
    DetermineVersion,
    async () =>
    {
        var (version, isPrerelease) = await GetVersion();
        Console.WriteLine($"VERSION={version}");
        Console.WriteLine(
            $"PRERELEASE={isPrerelease.ToString().ToLower(CultureInfo.InvariantCulture)}"
        );

        // Write to environment file for GitHub Actions
        var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(githubOutput))
        {
            File.AppendAllText(githubOutput, $"version={version}\n");
            File.AppendAllText(
                githubOutput,
                $"prerelease={isPrerelease.ToString().ToLower(CultureInfo.InvariantCulture)}\n"
            );
        }
    }
);

Target(
    UpdateVersion,
    async () =>
    {
        var version = Environment.GetEnvironmentVariable("VERSION");
        if (string.IsNullOrEmpty(version))
        {
            var (detectedVersion, _) = await GetVersion();
            version = detectedVersion;
        }

        Console.WriteLine($"Updating project file with version: {version}");

        var projectPath = "src/SharpCompress/SharpCompress.csproj";
        var content = File.ReadAllText(projectPath);

        // Get base version (without prerelease suffix)
        var baseVersion = version.Split('-')[0];

        // Update VersionPrefix
        content = Regex.Replace(
            content,
            @"<VersionPrefix>[^<]*</VersionPrefix>",
            $"<VersionPrefix>{version}</VersionPrefix>"
        );

        // Update AssemblyVersion
        content = Regex.Replace(
            content,
            @"<AssemblyVersion>[^<]*</AssemblyVersion>",
            $"<AssemblyVersion>{baseVersion}</AssemblyVersion>"
        );

        // Update FileVersion
        content = Regex.Replace(
            content,
            @"<FileVersion>[^<]*</FileVersion>",
            $"<FileVersion>{baseVersion}</FileVersion>"
        );

        File.WriteAllText(projectPath, content);
        Console.WriteLine($"Updated VersionPrefix to: {version}");
        Console.WriteLine($"Updated AssemblyVersion and FileVersion to: {baseVersion}");
    }
);

Target(
    PushToNuGet,
    () =>
    {
        var apiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine(
                "NUGET_API_KEY environment variable is not set. Skipping NuGet push."
            );
            return;
        }

        var packages = Directory.GetFiles("artifacts", "*.nupkg");
        if (packages.Length == 0)
        {
            Console.WriteLine("No packages found in artifacts directory.");
            return;
        }

        foreach (var package in packages)
        {
            Console.WriteLine($"Pushing {package} to NuGet.org");
            try
            {
                // Note: API key is passed via command line argument which is standard practice for dotnet nuget push
                // The key is already in an environment variable and not displayed in normal output
                Run(
                    "dotnet",
                    $"nuget push \"{package}\" --api-key {apiKey} --source https://api.nuget.org/v3/index.json --skip-duplicate"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to push {package}: {ex.Message}");
                throw;
            }
        }
    }
);

Target(
    DisplayBenchmarkResults,
    () =>
    {
        var githubStepSummary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        var resultsDir = "benchmark-results/results";

        if (!Directory.Exists(resultsDir))
        {
            Console.WriteLine("No benchmark results found.");
            return;
        }

        var markdownFiles = Directory
            .GetFiles(resultsDir, "*-report-github.md")
            .OrderBy(f => f)
            .ToList();

        if (markdownFiles.Count == 0)
        {
            Console.WriteLine("No benchmark markdown reports found.");
            return;
        }

        var output = new List<string> { "## Benchmark Results", "" };

        foreach (var file in markdownFiles)
        {
            Console.WriteLine($"Processing {Path.GetFileName(file)}");
            var content = File.ReadAllText(file);
            output.Add(content);
            output.Add("");
        }

        // Write to GitHub Step Summary if available
        if (!string.IsNullOrEmpty(githubStepSummary))
        {
            File.AppendAllLines(githubStepSummary, output);
            Console.WriteLine($"Benchmark results written to GitHub Step Summary");
        }
        else
        {
            // Write to console if not in GitHub Actions
            foreach (var line in output)
            {
                Console.WriteLine(line);
            }
        }
    }
);

Target(
    CompareBenchmarkResults,
    () =>
    {
        var githubStepSummary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        var baselinePath = "tests/SharpCompress.Performance/baseline-results.md";
        var resultsDir = "benchmark-results/results";

        var output = new List<string> { "## Comparison with Baseline", "" };

        if (!File.Exists(baselinePath))
        {
            Console.WriteLine("Baseline file not found");
            output.Add("⚠️ Baseline file not found. Run `generate-baseline` to create it.");
            WriteOutput(output, githubStepSummary);
            return;
        }

        if (!Directory.Exists(resultsDir))
        {
            Console.WriteLine("No current benchmark results found.");
            output.Add("⚠️ No current benchmark results found. Showing baseline only.");
            output.Add("");
            output.Add("### Baseline Results");
            output.AddRange(File.ReadAllLines(baselinePath));
            WriteOutput(output, githubStepSummary);
            return;
        }

        var markdownFiles = Directory
            .GetFiles(resultsDir, "*-report-github.md")
            .OrderBy(f => f)
            .ToList();

        if (markdownFiles.Count == 0)
        {
            Console.WriteLine("No current benchmark markdown reports found.");
            output.Add("⚠️ No current benchmark results found. Showing baseline only.");
            output.Add("");
            output.Add("### Baseline Results");
            output.AddRange(File.ReadAllLines(baselinePath));
            WriteOutput(output, githubStepSummary);
            return;
        }

        Console.WriteLine("Parsing baseline results...");
        var baselineMetrics = ParseBenchmarkResults(File.ReadAllText(baselinePath));

        Console.WriteLine("Parsing current results...");
        var currentText = string.Join("\n", markdownFiles.Select(f => File.ReadAllText(f)));
        var currentMetrics = ParseBenchmarkResults(currentText);

        Console.WriteLine("Comparing results...");
        output.Add("### Performance Comparison");
        output.Add("");
        output.Add(
            "| Benchmark | Baseline Mean | Current Mean | Change | Baseline Memory | Current Memory | Change |"
        );
        output.Add(
            "|-----------|---------------|--------------|--------|-----------------|----------------|--------|"
        );

        var hasRegressions = false;
        var hasImprovements = false;

        foreach (var method in currentMetrics.Keys.Union(baselineMetrics.Keys).OrderBy(k => k))
        {
            var hasCurrent = currentMetrics.TryGetValue(method, out var current);
            var hasBaseline = baselineMetrics.TryGetValue(method, out var baseline);

            if (!hasCurrent)
            {
                output.Add(
                    $"| {method} | {baseline!.Mean} | ❌ Missing | N/A | {baseline.Memory} | N/A | N/A |"
                );
                continue;
            }

            if (!hasBaseline)
            {
                output.Add(
                    $"| {method} | ❌ New | {current!.Mean} | N/A | N/A | {current.Memory} | N/A |"
                );
                continue;
            }

            var timeChange = CalculateChange(baseline!.MeanValue, current!.MeanValue);
            var memChange = CalculateChange(baseline.MemoryValue, current.MemoryValue);

            var timeIcon =
                timeChange > 25 ? "🔴"
                : timeChange < -25 ? "🟢"
                : "⚪";
            var memIcon =
                memChange > 25 ? "🔴"
                : memChange < -25 ? "🟢"
                : "⚪";

            if (timeChange > 25 || memChange > 25)
            {
                hasRegressions = true;
            }
            if (timeChange < -25 || memChange < -25)
            {
                hasImprovements = true;
            }

            output.Add(
                $"| {method} | {baseline.Mean} | {current.Mean} | {timeIcon} {timeChange:+0.0;-0.0;0}% | {baseline.Memory} | {current.Memory} | {memIcon} {memChange:+0.0;-0.0;0}% |"
            );
        }

        output.Add("");
        output.Add("**Legend:**");
        output.Add("- 🔴 Regression (>25% slower/more memory)");
        output.Add("- 🟢 Improvement (>25% faster/less memory)");
        output.Add("- ⚪ No significant change");

        if (hasRegressions)
        {
            output.Add("");
            output.Add(
                "⚠️ **Warning**: Performance regressions detected. Review the changes carefully."
            );
        }
        else if (hasImprovements)
        {
            output.Add("");
            output.Add("✅ Performance improvements detected!");
        }
        else
        {
            output.Add("");
            output.Add("✅ Performance is stable compared to baseline.");
        }

        WriteOutput(output, githubStepSummary);
    }
);

Target(
    GenerateBaseline,
    () =>
    {
        var perfProject = "tests/SharpCompress.Performance/SharpCompress.Performance.csproj";
        var baselinePath = "tests/SharpCompress.Performance/baseline-results.md";
        var artifactsDir = "baseline-artifacts";

        Console.WriteLine("Building performance project...");
        Run("dotnet", $"build {perfProject} --configuration Release");

        Console.WriteLine("Running benchmarks to generate baseline...");
        Run(
            "dotnet",
            $"run --project {perfProject} --configuration Release --no-build -- --filter \"*\" --exporters markdown --artifacts {artifactsDir}"
        );

        var resultsDir = Path.Combine(artifactsDir, "results");
        if (!Directory.Exists(resultsDir))
        {
            Console.WriteLine("ERROR: No benchmark results generated.");
            return;
        }

        var markdownFiles = Directory
            .GetFiles(resultsDir, "*-report-github.md")
            .OrderBy(f => f)
            .ToList();

        if (markdownFiles.Count == 0)
        {
            Console.WriteLine("ERROR: No markdown reports found.");
            return;
        }

        Console.WriteLine($"Combining {markdownFiles.Count} benchmark reports...");
        var baselineContent = new List<string>();

        foreach (var file in markdownFiles)
        {
            var lines = File.ReadAllLines(file);
            baselineContent.AddRange(lines.Select(l => l.Trim()).Where(l => l.StartsWith('|')));
        }

        File.WriteAllText(baselinePath, string.Join(Environment.NewLine, baselineContent));
        Console.WriteLine($"Baseline written to {baselinePath}");

        // Clean up artifacts directory
        if (Directory.Exists(artifactsDir))
        {
            Directory.Delete(artifactsDir, true);
            Console.WriteLine("Cleaned up artifacts directory.");
        }
    }
);

Target("default", [Publish], () => Console.WriteLine("Done!"));

await RunTargetsAndExitAsync(args);

static async Task<(string version, bool isPrerelease)> GetVersion()
{
    // Check if current commit has a version tag
    var currentTag = (await GetGitOutput("tag", "--points-at HEAD"))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault(tag => Regex.IsMatch(tag.Trim(), @"^\d+\.\d+\.\d+$"));

    if (!string.IsNullOrEmpty(currentTag))
    {
        // Tagged release - use the tag as version
        var version = currentTag.Trim();
        Console.WriteLine($"Building tagged release version: {version}");
        return (version, false);
    }
    else
    {
        // Not tagged - create prerelease version
        var allTags = (await GetGitOutput("tag", "--list"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(tag => Regex.IsMatch(tag.Trim(), @"^\d+\.\d+\.\d+$"))
            .Select(tag => tag.Trim())
            .ToList();

        var lastTag = allTags.OrderBy(tag => Version.Parse(tag)).LastOrDefault() ?? "0.0.0";
        var lastVersion = Version.Parse(lastTag);

        // Determine version increment based on branch
        var currentBranch = await GetCurrentBranch();
        Version nextVersion;

        if (currentBranch == "release")
        {
            // Release branch: increment patch version
            nextVersion = new Version(lastVersion.Major, lastVersion.Minor, lastVersion.Build + 1);
            Console.WriteLine($"Building prerelease for release branch (patch increment)");
        }
        else
        {
            // Master or other branches: increment minor version
            nextVersion = new Version(lastVersion.Major, lastVersion.Minor + 1, 0);
            Console.WriteLine($"Building prerelease for {currentBranch} branch (minor increment)");
        }

        // Use commit count since the last version tag if available; otherwise, fall back to total count
        var revListArgs = allTags.Any() ? $"--count {lastTag}..HEAD" : "--count HEAD";
        var commitCount = (await GetGitOutput("rev-list", revListArgs)).Trim();

        var version = $"{nextVersion}-beta.{commitCount}";
        Console.WriteLine($"Building prerelease version: {version}");
        return (version, true);
    }
}

static async Task<string> GetCurrentBranch()
{
    // In GitHub Actions, GITHUB_REF_NAME contains the branch name
    var githubRefName = Environment.GetEnvironmentVariable("GITHUB_REF_NAME");
    if (!string.IsNullOrEmpty(githubRefName))
    {
        return githubRefName;
    }

    // Fallback to git command for local builds
    try
    {
        var (output, _) = await ReadAsync("git", "branch --show-current");
        return output.Trim();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not determine current branch: {ex.Message}");
        return "unknown";
    }
}

static async Task<string> GetGitOutput(string command, string args)
{
    try
    {
        // Use SimpleExec's Read to execute git commands in a cross-platform way
        var (output, _) = await ReadAsync("git", $"{command} {args}");
        return output;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Git command failed: git {command} {args}\n{ex.Message}",
            ex
        );
    }
}

static void WriteOutput(List<string> output, string? githubStepSummary)
{
    if (!string.IsNullOrEmpty(githubStepSummary))
    {
        File.AppendAllLines(githubStepSummary, output);
        Console.WriteLine("Comparison written to GitHub Step Summary");
    }
    else
    {
        foreach (var line in output)
        {
            Console.WriteLine(line);
        }
    }
}

static Dictionary<string, BenchmarkMetric> ParseBenchmarkResults(string markdown)
{
    var metrics = new Dictionary<string, BenchmarkMetric>();
    var lines = markdown.Split('\n');

    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i].Trim();

        // Look for table rows with benchmark data
        if (line.StartsWith('|') && line.Contains("&#39;", StringComparison.Ordinal) && i > 0)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 5)
            {
                var method = parts[1].Replace("&#39;", "'", StringComparison.Ordinal);
                var meanStr = parts[2];

                // Find Allocated column - it's usually the last column or labeled "Allocated"
                string memoryStr = "N/A";
                for (int j = parts.Length - 2; j >= 2; j--)
                {
                    if (
                        parts[j].Contains("KB", StringComparison.Ordinal)
                        || parts[j].Contains("MB", StringComparison.Ordinal)
                        || parts[j].Contains("GB", StringComparison.Ordinal)
                        || parts[j].Contains('B', StringComparison.Ordinal)
                    )
                    {
                        memoryStr = parts[j];
                        break;
                    }
                }

                if (
                    !method.Equals("Method", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(method)
                )
                {
                    var metric = new BenchmarkMetric
                    {
                        Method = method,
                        Mean = meanStr,
                        MeanValue = ParseTimeValue(meanStr),
                        Memory = memoryStr,
                        MemoryValue = ParseMemoryValue(memoryStr),
                    };
                    metrics[method] = metric;
                }
            }
        }
    }

    return metrics;
}

static double ParseTimeValue(string timeStr)
{
    if (string.IsNullOrWhiteSpace(timeStr) || timeStr == "N/A" || timeStr == "NA")
    {
        return 0;
    }

    // Remove thousands separators and parse
    timeStr = timeStr.Replace(",", "", StringComparison.Ordinal).Trim();

    var match = Regex.Match(timeStr, @"([\d.]+)\s*(\w+)");
    if (!match.Success)
    {
        return 0;
    }

    var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    var unit = match.Groups[2].Value.ToLower(CultureInfo.InvariantCulture);

    // Convert to microseconds for comparison
    return unit switch
    {
        "s" => value * 1_000_000,
        "ms" => value * 1_000,
        "μs" or "us" => value,
        "ns" => value / 1_000,
        _ => value,
    };
}

static double ParseMemoryValue(string memStr)
{
    if (string.IsNullOrWhiteSpace(memStr) || memStr == "N/A" || memStr == "NA")
    {
        return 0;
    }

    memStr = memStr.Replace(",", "", StringComparison.Ordinal).Trim();

    var match = Regex.Match(memStr, @"([\d.]+)\s*(\w+)");
    if (!match.Success)
    {
        return 0;
    }

    var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    var unit = match.Groups[2].Value.ToUpper(CultureInfo.InvariantCulture);

    // Convert to KB for comparison
    return unit switch
    {
        "GB" => value * 1_024 * 1_024,
        "MB" => value * 1_024,
        "KB" => value,
        "B" => value / 1_024,
        _ => value,
    };
}

static double CalculateChange(double baseline, double current)
{
    if (baseline == 0)
    {
        return 0;
    }
    return ((current - baseline) / baseline) * 100;
}

record BenchmarkMetric
{
    public string Method { get; init; } = "";
    public string Mean { get; init; } = "";
    public double MeanValue { get; init; }
    public string Memory { get; init; } = "";
    public double MemoryValue { get; init; }
}

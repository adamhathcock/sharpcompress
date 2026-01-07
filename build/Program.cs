using System;
using System.Collections.Generic;
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
        Console.WriteLine($"PRERELEASE={isPrerelease.ToString().ToLower()}");

        // Write to environment file for GitHub Actions
        var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(githubOutput))
        {
            File.AppendAllText(githubOutput, $"version={version}\n");
            File.AppendAllText(githubOutput, $"prerelease={isPrerelease.ToString().ToLower()}\n");
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
        // Not tagged - create prerelease version based on next minor version
        var allTags = (await GetGitOutput("tag", "--list"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(tag => Regex.IsMatch(tag.Trim(), @"^\d+\.\d+\.\d+$"))
            .Select(tag => tag.Trim())
            .ToList();

        var lastTag = allTags.OrderBy(tag => Version.Parse(tag)).LastOrDefault() ?? "0.0.0";
        var lastVersion = Version.Parse(lastTag);

        // Increment minor version for next release
        var nextVersion = new Version(lastVersion.Major, lastVersion.Minor + 1, 0);

        // Use commit count since the last version tag if available; otherwise, fall back to total count
        var revListArgs = allTags.Any() ? $"--count {lastTag}..HEAD" : "--count HEAD";
        var commitCount = (await GetGitOutput("rev-list", revListArgs)).Trim();

        var version = $"{nextVersion}-beta.{commitCount}";
        Console.WriteLine($"Building prerelease version: {version}");
        return (version, true);
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
        throw new Exception($"Git command failed: git {command} {args}\n{ex.Message}", ex);
    }
}

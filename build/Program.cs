using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string Clean = "clean";
const string Restore = "restore";
const string Build = "build";
const string Test = "test";
const string Format = "format";
const string Publish = "publish";

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
        Run("dotnet", "csharpier check .");
    }
);
Target(Restore, [Format], () => Run("dotnet", "restore"));

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
    ["net8.0", "net48"],
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

Target("default", [Publish], () => Console.WriteLine("Done!"));

await RunTargetsAndExitAsync(args).ConfigureAwait(false);

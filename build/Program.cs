using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string Clean = "clean";
const string Build = "build";
const string Test = "test";
const string Publish = "publish";

Target(
    Clean,
    ForEach("**/bin", "**/obj"),
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
    Build,
    () =>
    {
        Run("dotnet", "build src/SharpCompress/SharpCompress.csproj -c Release");
    }
);

Target(
    Test,
    DependsOn(Build),
    ForEach("net7.0", "net462"),
    framework =>
    {
        IEnumerable<string> GetFiles(string d)
        {
            return Glob.Files(".", d);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && framework == "net462")
        {
            return;
        }

        foreach (var file in GetFiles("**/*.Test.csproj"))
        {
            Run("dotnet", $"test {file} -c Release -f {framework}");
        }
    }
);

Target(
    Publish,
    DependsOn(Test),
    () =>
    {
        Run("dotnet", "pack src/SharpCompress/SharpCompress.csproj -c Release -o artifacts/");
    }
);

Target("default", DependsOn(Publish), () => Console.WriteLine("Done!"));

await RunTargetsAndExitAsync(args);

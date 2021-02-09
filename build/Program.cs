using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

class Program
{
    private const string Clean = "clean";
    private const string Format = "format";
    private const string Build = "build";
    private const string Test = "test";
    private const string Publish = "publish";

    static void Main(string[] args)
    {
        Target(Clean,
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
            });

        Target(Format, () =>
        {
            Run("dotnet", "tool restore");
            Run("dotnet", "format --check");
        });

        Target(Build, DependsOn(Format),
               framework =>
               {
                   if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && framework == "net46")
                   {
                       return;
                   }
                   Run("dotnet", "build src/SharpCompress/SharpCompress.csproj -c Release");
               });

        Target(Test, DependsOn(Build), ForEach("net5.0"),
               framework =>
               {
                   IEnumerable<string> GetFiles(string d)
                   {
                       return Glob.Files(".", d);
                   }

                   foreach (var file in GetFiles("**/*.Test.csproj"))
                   {
                       Run("dotnet", $"test {file} -c Release -f {framework}");
                   }
               });

        Target(Publish, DependsOn(Test),
               () =>
               {
                   Run("dotnet", "pack src/SharpCompress/SharpCompress.csproj -c Release -o artifacts/");
               });

        Target("default", DependsOn(Publish), () => Console.WriteLine("Done!"));

        RunTargetsAndExit(args);
    }
}
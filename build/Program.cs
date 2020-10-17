﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

class Program
{
    private const string Clean = "clean";
    private const string Build = "build";
    private const string Test = "test";
    private const string Publish = "publish";
    
    static void Main(string[] args)
    {
        string version = args.Length > 0 ? args[0] : "";
        
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

        Target(Build, ForEach("net46", "netstandard2.0", "netstandard2.1"), 
               framework =>
               {
                   if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && framework == "net46")
                   {
                       return;
                   }

                   Run("dotnet", String.IsNullOrWhiteSpace(version) ? $"build src/SharpCompress/SharpCompress.csproj -c Release" : $"build src/SharpCompress/SharpCompress.csproj -c Release /p:FileVersion={version} /p:AssemblyVersion={version} /p:VersionPrefix={version}");
               });

        Target(Test, DependsOn(Build), ForEach("netcoreapp3.1"), 
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
                   Run("dotnet", String.IsNullOrWhiteSpace(version) ? "pack src/SharpCompress/SharpCompress.csproj -c Release -o artifacts/" : $"pack src/SharpCompress/SharpCompress.csproj -c Release -o artifacts/ /p:FileVersion={version} /p:AssemblyVersion={version} /p:VersionPrefix={version}");
                   ;
               });

        Target("default", DependsOn(Publish), () => Console.WriteLine("Done!"));
        
        RunTargetsAndExit(args.Skip(1).ToArray());
    }
}
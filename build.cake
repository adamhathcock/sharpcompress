var target = Argument("target", "Default");
var tag = Argument("tag", "cake");

Task("Restore")
  .Does(() =>
{
    DotNetCoreRestore(".");
});

Task("Build")
  .IsDependentOn("Restore")
  .Does(() =>
{
    if (IsRunningOnWindows())
    {
        MSBuild("./sharpcompress.sln", c => 
        { 
            c.SetConfiguration("Release")
            .SetVerbosity(Verbosity.Minimal)
            .UseToolVersion(MSBuildToolVersion.VS2017);
        });
    }
    else 
    {
        var settings = new DotNetCoreBuildSettings
        {
            Framework = "netstandard1.0",
            Configuration = "Release"
        };

        DotNetCoreBuild("./src/SharpCompress/SharpCompress.csproj", settings);

        settings.Framework = "netcoreapp1.1";
        DotNetCoreBuild("./tests/SharpCompress.Test/SharpCompress.Test.csproj", settings);
    }
});

Task("Test")
  .IsDependentOn("Build")
  .Does(() =>
{
    if (!bool.Parse(EnvironmentVariable("APPVEYOR") ?? "false")
        && !bool.Parse(EnvironmentVariable("TRAVIS") ?? "false"))
    {
        var files = GetFiles("tests/**/*.csproj");
        foreach(var file in files)
        {
            var settings = new DotNetCoreTestSettings
            {
                Configuration = "Release"
            };

            DotNetCoreTest(file.ToString(), settings);
        }
    } 
    else 
    {
        Information("Skipping tests as this is AppVeyor or Travis CI");
    }
});

Task("Pack")
    .IsDependentOn("Build")
    .Does(() => 
{
    if (IsRunningOnWindows())
    {
        MSBuild("src/SharpCompress/SharpCompress.csproj", c => c
            .SetConfiguration("Release")
            .SetVerbosity(Verbosity.Minimal)
            .UseToolVersion(MSBuildToolVersion.VS2017)
            .WithProperty("NoBuild", "true")
            .WithTarget("Pack"));
    } 
    else 
    {
        Information("Skipping Pack as this is not Windows");
    }
});

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Pack");

 Task("RunTests")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test");


RunTarget(target);
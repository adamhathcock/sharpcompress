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
            Configuration = "Release",
            NoRestore = true
        };

        DotNetCoreBuild("./src/SharpCompress/SharpCompress.csproj", settings);

        settings.Framework = "netstandard1.3";
        DotNetCoreBuild("./src/SharpCompress/SharpCompress.csproj", settings);

        settings.Framework = "netstandard2.0";
        DotNetCoreBuild("./src/SharpCompress/SharpCompress.csproj", settings);
    }
});

Task("Test")
  .IsDependentOn("Build")
  .Does(() =>
{
    var files = GetFiles("tests/**/*.csproj");
    foreach(var file in files)
    {
        var settings = new DotNetCoreTestSettings
        {
            Configuration = "Release",
            Framework = "netcoreapp2.1"
        };
        DotNetCoreTest(file.ToString(), settings);
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
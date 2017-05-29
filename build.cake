var target = Argument("target", "Default");
var tag = Argument("tag", "cake");

Task("Restore")
  .Does(() =>
{
    DotNetCoreRestore(".");
});

Task("Build")
  .Does(() =>
{
    MSBuild("./sharpcompress.sln", c => c
            .SetConfiguration("Release")
            .SetVerbosity(Verbosity.Minimal)
            .UseToolVersion(MSBuildToolVersion.VS2017));
});

Task("Test")
  .IsDependentOn("Build")
  .Does(() =>
{
    if (!bool.Parse(EnvironmentVariable("APPVEYOR") ?? "false"))
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
        Information("Skipping tests as this is AppVeyor");
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
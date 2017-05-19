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
  .Does(() =>
{
    var files = GetFiles("tests/**/*.csproj");
    foreach(var file in files)
    {
        DotNetCoreTest(file.ToString());
    }
});

Task("Pack")
    .IsDependentOn("Build")
    .Does(() => 
{
        MSBuild("src/SharpCompress/SharpCompress.csproj", c => c
            .SetConfiguration("Release")
            .SetVerbosity(Verbosity.Minimal)
            .UseToolVersion(MSBuildToolVersion.VS2017)
            .WithProperty("NoBuild", "true")
            .WithTarget("Pack"));
});

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Pack");

 Task("Rebuild")
    .IsDependentOn("Restore")
    .IsDependentOn("Build");


RunTarget(target);
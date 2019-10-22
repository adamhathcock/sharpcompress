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
    var settings = new DotNetCoreBuildSettings
    {
        Framework = "netstandard1.4",
        Configuration = "Release",
        NoRestore = true
    };

    DotNetCoreBuild("./src/SharpCompress/SharpCompress.csproj", settings);

    if (IsRunningOnWindows())
    {
        settings.Framework = "net46";
        DotNetCoreBuild("./src/SharpCompress/SharpCompress.csproj", settings);
    }

    settings.Framework = "netstandard2.0";
    DotNetCoreBuild("./src/SharpCompress/SharpCompress.csproj", settings);
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
            Framework = "netcoreapp2.2"
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
        var settings = new DotNetCorePackSettings
        {
            Configuration = "Release",
            NoBuild = true
        };

        DotNetCorePack("src/SharpCompress/SharpCompress.csproj", settings);
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
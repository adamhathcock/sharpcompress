#addin "Cake.Json"

#addin "nuget:?package=NuGet.Core"

using NuGet;


//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var apiKey = Argument("apiKey", "");
var repo = Argument("repo", "");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var sources = new [] { "https://api.nuget.org/v3/index.json" };
var publishTarget = "";

Warning("=============");
var globalPath = MakeFullPath("global.json");
var nupkgs = MakeFullPath("nupkgs");
Warning("Operating on global.json: " + globalPath);
Warning("=============");

//////////////////////////////////////////////////////////////////////
// FUNCTIONS
//////////////////////////////////////////////////////////////////////

string MakeFullPath(string relativePath) 
{
    if (string.IsNullOrEmpty(repo))
    {
        return MakeAbsolute(new DirectoryPath(relativePath)).ToString();
    }
    if (!System.IO.Path.IsPathRooted(repo))
    {
        return MakeAbsolute(new DirectoryPath(System.IO.Path.Combine(repo,relativePath))).ToString();
    }
    return System.IO.Path.Combine(repo, relativePath);
}

IEnumerable<string> GetAllProjects() 
{
    var global = DeserializeJsonFromFile<JObject>(globalPath);  
    var projs = global["projects"].Select(x => x.ToString());
    foreach(var y in projs)
    {
        yield return MakeFullPath(y);
    }
}

IEnumerable<string> GetSourceProjects()
{    
    return GetAllProjects().Where(x => x.EndsWith("src"));
}

IEnumerable<string> GetTestProjects()
{    
    return GetAllProjects().Where(x => x.EndsWith("test"));
}

IEnumerable<string> GetFrameworks(string path) 
{
    var projectJObject = DeserializeJsonFromFile<JObject>(path);
    foreach(var prop in ((JObject)projectJObject["frameworks"]).Properties()) 
    {
        yield return prop.Name;   
    }    
}

string GetVersion(string path) 
{
    var projectJObject = DeserializeJsonFromFile<JObject>(path);
    return ((JToken)projectJObject["version"]).ToString();
}

IEnumerable<string> GetProjectJsons(IEnumerable<string> projects) 
{
    foreach(var proj in projects)
    {
        foreach(var projectJson in GetFiles(proj + "/**/project.json"))
        {
            yield return MakeFullPath(projectJson.ToString());
        }
    }
}

bool IsNuGetPublished (FilePath file, string nugetSource)
{
    var pkg = new ZipPackage(file.ToString());

    var repo = PackageRepositoryFactory.Default.CreateRepository(nugetSource);

    var packages = repo.FindPackagesById(pkg.Id);

    var version = SemanticVersion.Parse(pkg.Version.ToString());

    //Filter the list of packages that are not Release (Stable) versions
    var exists = packages.Any (p => p.Version == version);

    return exists;
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Restore")
    .Does(() =>
{
    var settings = new DotNetCoreRestoreSettings
    {
        Sources =  sources,
        NoCache = true
    };
    
    foreach(var project in GetProjectJsons(GetSourceProjects().Concat(GetTestProjects())))
    {
        DotNetCoreRestore(project, settings);
    }
});

Task("Build")
    .Does(() =>
{
    var settings = new DotNetCoreBuildSettings
    {
        Configuration = "Release"
    };
    
    foreach(var project in GetProjectJsons(GetSourceProjects().Concat(GetTestProjects())))
    {
        foreach(var framework in GetFrameworks(project))
        {
            Information("Building: {0} on Framework: {1}", project, framework);
            Information("========");
            settings.Framework = framework;
            DotNetCoreBuild(project, settings);
        }
    }  
});

Task("Test")
    .Does(() =>
{  
    var settings = new DotNetCoreTestSettings
    {
        Configuration = "Release",
        Verbose = true
    };
    
    foreach(var project in GetProjectJsons(GetTestProjects()))
    {       
        settings.Framework = GetFrameworks(project).First();
        DotNetCoreTest(project.ToString(), settings);
    }
       
}).ReportError(exception =>
{  
    Error(exception.ToString());
});

Task("Pack")
    .Does(() =>
{ 
    if (DirectoryExists(nupkgs))
    {
        DeleteDirectory(nupkgs, true);
    }
    CreateDirectory(nupkgs);
    
    var settings = new DotNetCorePackSettings 
    {
        Configuration = "Release",
        OutputDirectory = nupkgs
    };
        
    foreach(var project in GetProjectJsons(GetSourceProjects()))
    { 
        DotNetCorePack(project, settings);
    }   
});

Task("Publish")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Pack")
    .Does(() =>
{
    var packages = GetFiles(nupkgs + "/*.nupkg");
    foreach(var package in packages) 
    {
        if (package.ToString().Contains("symbols"))
        {
            Warning("Skipping Symbols package " + package);
            continue;
        }
        if (IsNuGetPublished(package, sources[1]))
        {
            throw new InvalidOperationException(package + " is already published.");
        }
        NuGetPush(package, new NuGetPushSettings{
            ApiKey = apiKey,
            Verbosity = NuGetVerbosity.Detailed,
            Source = publishTarget
        });     
    }         
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
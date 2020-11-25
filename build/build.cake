#l "scripts/utilities.cake"
#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

var paths = new {
    src = MakeAbsolute(Directory("./../src")).FullPath,
    solution = MakeAbsolute(File("./../src/Abc.Zerio.sln")).FullPath,
    testProject = MakeAbsolute(File("./../src/Abc.Zerio.Tests/Abc.Zerio.Tests.csproj")).FullPath,
    output = MakeAbsolute(Directory("./../output")).FullPath
};

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean").Does(() =>
{
    CleanDirectories(GetDirectories(paths.src + "/**/bin/Release"));
    CleanDirectory(paths.output);
});

Task("Restore-NuGet-Packages").Does(() =>
{
    NuGetRestore(paths.solution);
});

Task("Run-Build").Does(() =>
{
    MSBuild(paths.solution, settings => settings
        .WithTarget("Rebuild")
        .SetConfiguration("Release")
        .SetPlatformTarget(PlatformTarget.MSIL)
        .SetVerbosity(Verbosity.Minimal)
    );
});

Task("Run-Tests").Does(() =>
{
    DotNetCoreTest(paths.testProject, new DotNetCoreTestSettings {
        Configuration = "Release",
        NoBuild = true
    });
});

Task("NuGet-Pack").Does(() =>
{
    MSBuild(paths.solution, settings => settings
        .WithTarget("Pack")
        .SetConfiguration("Release")
        .SetPlatformTarget(PlatformTarget.MSIL)
        .SetVerbosity(Verbosity.Minimal)
        .WithProperty("PackageOutputPath", paths.output)
    );
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Run-Build")
    .IsDependentOn("NuGet-Pack");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

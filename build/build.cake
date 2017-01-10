#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    solution = MakeAbsolute(File("./../src/Abc.Zerio.sln")).FullPath,
    version = MakeAbsolute(File("./../version.txt")).FullPath,
    assemblyInfo = MakeAbsolute(File("./../src/Abc.Zerio/Properties/AssemblyInfo.cs")).FullPath,
    output = new {
        build = MakeAbsolute(Directory("./../output/build")).FullPath,
        nuget = MakeAbsolute(Directory("./../output/nuget")).FullPath,
    },
    nuspec = MakeAbsolute(File("./Abc.Zerio.nuspec")).FullPath,
};

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean").Does(() =>
{
    CleanDirectory(paths.output.build);
    CleanDirectory(paths.output.nuget);
});
Task("Restore-NuGet-Packages").Does(() => NuGetRestore(paths.solution));
Task("Create-AssemblyInfo").Does(()=>{
    var version = System.IO.File.ReadAllText(paths.version);
    CreateAssemblyInfo(paths.assemblyInfo, new AssemblyInfoSettings {
            Title = "Abc.Zerio",
            Product = "Abc.Zerio",
            Description = "Basic performance-oriented TCP client/server messaging C# API based on Windows Registered I/O (RIO) - https://github.com/Abc-Arbitrage/zerio",
            Copyright = "Copyright © ABC arbitrage 2017",
            Company = "ABC arbitrage",
            Version = version,
            FileVersion = version,
            InternalsVisibleTo = new []{ "Abc.Zerio.Tests" }
    });
});
Task("MSBuild").Does(() => MSBuild(paths.solution, settings => settings.SetConfiguration("Release")
                                                                        .SetPlatformTarget(PlatformTarget.MSIL)
                                                                        .WithProperty("OutDir", paths.output.build)));

Task("Run-Unit-Tests").Does(() => NUnit3(paths.output.build + "/*.Tests.dll", new NUnit3Settings { NoResults = true }));
Task("Nuget-Pack").Does(() => 
{
    var version = System.IO.File.ReadAllText(paths.version);
    NuGetPack(paths.nuspec, new NuGetPackSettings {
        Version = version,
        BasePath = paths.output.build,
        OutputDirectory = paths.output.nuget
    });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("MSBuild");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests");

Task("Nuget")
    .IsDependentOn("Test")
    .IsDependentOn("Nuget-Pack")
    .Does(() => {
        var version = System.IO.File.ReadAllText(paths.version);
        Information("   Nuget package is now ready at location: {0}.", paths.output.nuget);
        Warning("   Please remember to create and push a tag based on the currently built version.");
        Information("   You can do so by copying/pasting the following commands:");
        Information("       git tag v{0}", version);
        Information("       git push origin --tags");
    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

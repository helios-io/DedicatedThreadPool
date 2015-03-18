#I @"src/packages/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open Fake
open Fake.FileUtils
open Fake.TaskRunnerHelper

//--------------------------------------------------------------------------------
// Information about the project for Nuget and Assembly info files
//-------------------------------------------------------------------------------

let product = "Helios.DedicatedThreadPool"
let authors = [ "Aaron Stannard";"Roger Alsing"]
let copyright = "Copyright © Roger Alsing, Aaron Stannard 2015"
let company = "Helios Framework"
let description = "An instanced, dedicated ThreadPool for eliminating \"noisy neighbor\" problems on the CLR ThreadPool "
let tags = ["threads";"threadpool";"fiber";"helios";"akka.net";"akka"]
let configuration = "Release"

// Read release notes and version

let release =
    File.ReadLines "RELEASE_NOTES.md"
    |> ReleaseNotesHelper.parseReleaseNotes

//--------------------------------------------------------------------------------
// Directories

let binDir = "bin"
let testOutput = "TestResults"

let nugetDir = binDir @@ "nuget"
let workingDir = binDir @@ "build"
let nugetExe = FullName @"tools\nuget\NuGet.exe"

//--------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDir binDir
)

//--------------------------------------------------------------------------------
// Build the solution

Target "Build" (fun _ ->
    !!"src/Helios.DedicatedThreadPool.sln"
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

//--------------------------------------------------------------------------------
// Copy the build output to bin directory
//--------------------------------------------------------------------------------

Target "CopyOutput" (fun _ ->    
    let copyOutput project =
        let src = "src" @@ project @@ @"bin\release\"
        let dst = binDir @@ project
        CopyDir dst src allFiles
    [ "core/Helios.DedicatedThreadPool"
    ]
    |> List.iter copyOutput
)

Target "BuildRelease" DoNothing

//--------------------------------------------------------------------------------
// Tests targets
//--------------------------------------------------------------------------------
Target "RunTests" <| fun _ ->
    let nunitAssemblies = !! "src/tests/**/bin/Release/*.Tests.dll"

    mkdir testOutput
    nunitAssemblies |> NUnit(fun p -> 
        {p with 
            DisableShadowCopy = true;
            OutputFile = testOutput @@ "TestResults.xml" })

//--------------------------------------------------------------------------------
// Clean test output

Target "CleanTests" <| fun _ ->
    DeleteDir testOutput

//--------------------------------------------------------------------------------
// Nuget targets 
//--------------------------------------------------------------------------------
module Nuget = 

    // selected nuget description
    let description project =
        match project with
        | _ -> description

open Nuget

//--------------------------------------------------------------------------------
// Clean nuget directory
Target "CleanNuget" (fun _ ->
    CleanDir nugetDir
)


//--------------------------------------------------------------------------------
// Pack nuget for all projects
// Publish to nuget.org if nugetkey is specified

let createNugetPackages _ =
    let removeDir dir = 
        let del _ = 
            DeleteDir dir
            not (directoryExists dir)
        runWithRetries del 3 |> ignore

    ensureDirectory nugetDir
    for nuspec in !! "src/**/*.nuspec" do
        printfn "Creating nuget packages for %s" nuspec
        
        CleanDir workingDir

        let project = Path.GetFileNameWithoutExtension nuspec 
        let projectDir = Path.GetDirectoryName nuspec
        let projectFile = (!! (projectDir @@ project + ".*sproj")) |> Seq.head
        
        
       
        let packages = projectDir @@ "packages.config"        
        let packageDependencies = if (fileExists packages) then (getDependencies packages) else []
        let dependencies = packageDependencies
        let releaseVersion = release.NugetVersion

        let pack outputDir  =
            NuGetHelper.NuGet
                (fun p ->
                    { p with
                        Description = description project
                        Authors = authors
                        Copyright = copyright
                        Project =  project
                        Properties = ["Configuration", "Release"]
                        ReleaseNotes = release.Notes |> String.concat "\n"
                        Version = releaseVersion
                        Tags = tags |> String.concat " "
                        OutputPath = outputDir
                        WorkingDir = workingDir
                        Dependencies = dependencies
                        Files = [
                                (@"Helios.Concurrency.DedicatedThreadPool.cs", Some "content", None)
                        ] })
                nuspec

        !! (projectDir @@ "Helios.Concurrency.DedicatedThreadPool.cs")
        |> CopyFiles workingDir
        
        //Remove workingDir/src/obj and workingDir/src/bin

        // Create both normal nuget package and symbols nuget package. 
        // Uses the files we copied to workingDir and outputs to nugetdir
        pack nugetDir

let publishNugetPackages _ = 
    let rec publishPackage url accessKey trialsLeft packageFile =
        let tracing = enableProcessTracing
        enableProcessTracing <- false
        let args p =
            match p with
            | (pack, key, "") -> sprintf "push \"%s\" %s" pack key
            | (pack, key, url) -> sprintf "push \"%s\" %s -source %s" pack key url

        tracefn "Pushing %s Attempts left: %d" (FullName packageFile) trialsLeft
        try 
            let result = ExecProcess (fun info -> 
                    info.FileName <- nugetExe
                    info.WorkingDirectory <- (Path.GetDirectoryName (FullName packageFile))
                    info.Arguments <- args (packageFile, accessKey,url)) (System.TimeSpan.FromMinutes 1.0)
            enableProcessTracing <- tracing
            if result <> 0 then failwithf "Error during NuGet symbol push. %s %s" nugetExe (args (packageFile, accessKey,url))
        with exn -> 
            if (trialsLeft > 0) then (publishPackage url accessKey (trialsLeft-1) packageFile)
            else raise exn
    let shouldPushNugetPackages = hasBuildParam "nugetkey"
    let shouldPushSymbolsPackages = (hasBuildParam "symbolspublishurl") && (hasBuildParam "symbolskey")
    
    if (shouldPushNugetPackages || shouldPushSymbolsPackages) then
        printfn "Pushing nuget packages"
        if shouldPushNugetPackages then
            let normalPackages= 
                !! (nugetDir @@ "*.nupkg") 
                -- (nugetDir @@ "*.symbols.nupkg") |> Seq.sortBy(fun x -> x.ToLower())
            for package in normalPackages do
                publishPackage (getBuildParamOrDefault "nugetpublishurl" "") (getBuildParam "nugetkey") 3 package

        if shouldPushSymbolsPackages then
            let symbolPackages= !! (nugetDir @@ "*.symbols.nupkg") |> Seq.sortBy(fun x -> x.ToLower())
            for package in symbolPackages do
                publishPackage (getBuildParam "symbolspublishurl") (getBuildParam "symbolskey") 3 package

Target "Nuget" <| fun _ -> 
    createNugetPackages()
    publishNugetPackages()

Target "CreateNuget" <| fun _ -> 
    createNugetPackages()

Target "PublishNuget" <| fun _ -> 
    publishNugetPackages()

//--------------------------------------------------------------------------------
// Help 
//--------------------------------------------------------------------------------

Target "Help" <| fun _ ->
    List.iter printfn [
      "usage:"
      "build [target]"
      ""
      " Targets for building:"
      " * Build      Builds"
      " * Nuget      Create and optionally publish nugets packages"
      " * RunTests   Runs tests"
      " * All        Builds, run tests, creates and optionally publish nuget packages"
      ""
      " Other Targets"
      " * Help       Display this help" 
      " * HelpNuget  Display help about creating and pushing nuget packages" 
      ""]

Target "HelpNuget" <| fun _ ->
    List.iter printfn [
      "usage: "
      "build Nuget [nugetkey=<key> [nugetpublishurl=<url>]] "
      "            [symbolskey=<key> symbolspublishurl=<url>] "
      "            [nugetprerelease=<prefix>]"
      ""
      "Arguments for Nuget target:"
      "   nugetprerelease=<prefix>   Creates a pre-release package."
      "                              The version will be version-prefix<date>"
      "                              Example: nugetprerelease=dev =>"
      "                                       0.6.3-dev1408191917"
      ""
      "In order to publish a nuget package, keys must be specified."
      "If a key is not specified the nuget packages will only be created on disk"
      "After a build you can find them in bin/nuget"
      ""
      "For pushing nuget packages to nuget.org and symbols to symbolsource.org"
      "you need to specify nugetkey=<key>"
      "   build Nuget nugetKey=<key for nuget.org>"
      ""
      "For pushing the ordinary nuget packages to another place than nuget.org specify the url"
      "  nugetkey=<key>  nugetpublishurl=<url>  "
      ""
      "For pushing symbols packages specify:"
      "  symbolskey=<key>  symbolspublishurl=<url> "
      ""
      "Examples:"
      "  build Nuget                      Build nuget packages to the bin/nuget folder"
      ""
      "  build Nuget nugetprerelease=dev  Build pre-release nuget packages"
      ""
      "  build Nuget nugetkey=123         Build and publish to nuget.org and symbolsource.org"
      ""
      "  build Nuget nugetprerelease=dev nugetkey=123 nugetpublishurl=http://abc"
      "              symbolskey=456 symbolspublishurl=http://xyz"
      "                                   Build and publish pre-release nuget packages to http://abc"
      "                                   and symbols packages to http://xyz"
      ""]


//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------

Target "All" DoNothing

// build dependencies
"Clean" ==> "Build" ==> "CopyOutput" ==> "BuildRelease"

// tests dependencies
"CleanTests" ==> "RunTests"

// nuget dependencies

"BuildRelease" ==> "All"
"RunTests" ==> "All"
"Nuget" ==> "All"

RunTargetOrDefault "Help"
// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r "./packages/build/FAKE/tools/FakeLib.dll"

open System
open Fake
open Fake.Git
open Fake.ReleaseNotesHelper

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

let appReferences = !! "/**/*.??proj"
let dotnetcliVersion = "2.1.105"
let mutable dotnetExePath = "dotnet"

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------

let run timeout cmd args dir =
    //let timeout = TimeSpan.FromMinutes 1.
    if execProcess (fun info ->
        info.FileName <- cmd
        info.Arguments <- args
        if not (String.IsNullOrWhiteSpace dir) then
            info.WorkingDirectory <- dir
    ) timeout |> not then
        failwithf "Error while running '%s' with args: %s" cmd args

let runDotnet workingDir args =
    let result =
        ExecProcess (fun info ->
            info.FileName <- dotnetExePath
            if not (String.IsNullOrWhiteSpace workingDir) then
                info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if result <> 0 then failwithf "dotnet %s failed" args


let pkill args =
    run TimeSpan.MaxValue "pkill" args ""

let killParentsAndChildren processId =
    sprintf "-P %d" processId |> pkill

// --------------------------------------------------------------------------------------
// Targets
// --------------------------------------------------------------------------------------

Target "Clean" (fun _ ->
    ["bin"; "obj"; "temp" ;"dist"]
    |> DeleteDirs

    appReferences
    |> Seq.collect(fun p ->
        ["bin";"obj"]
        |> Seq.map(fun sp ->
             IO.Path.GetDirectoryName p </> sp)
        )
    |> CleanDirs
    )

Target "InstallDotNetCLI" (fun _ ->
    dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
    )

Target "Restore" (fun _ ->
    runDotnet "." "restore"
    // appReferences
    // |> Seq.iter (fun p ->
    //     let dir = System.IO.Path.GetDirectoryName p
    //     runDotnet dir "restore"
    // )
    )

Target "Build" (fun _ ->
    runDotnet "" "build"
    // appReferences
    // |> Seq.iter (fun p ->
    //     let dir = System.IO.Path.GetDirectoryName p
    //     runDotnet dir "build"
    // )
    )


let invoke f = f ()
let invokeAsync f = async { f () }

Target "Test" (fun _ ->
    !! "**/*.Tests.??proj"
    |> Seq.map System.IO.Path.GetDirectoryName
    |> Seq.iter (fun dir ->
        runDotnet dir "run --framework netcoreapp2.0" // why cli asks for framework?
        )
    // runTests id
    // |> Seq.iter (invoke)
)

// Target "WatchTests" (fun _ ->
//     runTests (sprintf "watch %s")
//     |> Seq.iter (invokeAsync >> Async.Catch >> Async.Ignore >> Async.Start)

//     printfn "Press enter to stop..."
//     Console.ReadLine() |> ignore

//     if isWindows |> not then
//         startedProcesses
//         |> Seq.iter(fst >> killParentsAndChildren >> ignore )
//     else
//         //Hope windows handles this right?
//         ()
//     )

Target "Pack" (fun _ ->
    let releaseNotes = LoadReleaseNotes "RELEASE_NOTES.md"

    appReferences
    |> Seq.iter (fun proj ->
        DotNetCli.Pack (fun c ->
            { c with
                Project = proj
                Configuration = "Release"
                OutputPath = IO.Directory.GetCurrentDirectory() @@ "dist"
                AdditionalArgs =
                    [
                        sprintf "/p:PackageVersion=%s" releaseNotes.NugetVersion
                        sprintf "/p:PackageReleaseNotes=\"%s\"" (String.concat "\n" releaseNotes.Notes)
                    ]
            })
    )
)

Target "Publish" (fun _ ->
    Paket.Push(fun c ->
            { c with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )
)

let release _ =
    let branch = Git.Information.getBranchName ""
    let releaseNotes = LoadReleaseNotes "RELEASE_NOTES.md"
    let version = releaseNotes.NugetVersion //TODO SemVer
    if Git.Information.getBranchName "" <> "master" then failwithf "Not on master, instead on '%s'" branch

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" version)
    Branches.push ""

    Branches.tag "" version
    Branches.pushTag "" "origin" version

// Target "ReleaseQuick" release
Target "Release" release


// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------

"Clean"
  ==> "InstallDotNetCLI"
  ==> "Restore"
  ==> "Build"
  ==> "Pack"
  ==> "Publish"
  ==> "Release"

"Restore"
 ==> "WatchTests"

RunTargetOrDefault "Pack"

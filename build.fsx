// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

// #r "./packages/build/FAKE/tools/FakeLib.dll"
#r "paket: groupref Build //"
// #r "netstandard"
#if !FAKE
  #r "Facades/netstandard"
#endif
#load ".fake/build.fsx/intellisense.fsx"

open System
open System.IO
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

let appReferences = !! "**/*.??proj"

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------

let run timeout cmd args workingDir =
    let exitCode =
        Process.execWithResult
            (fun info ->
                { info with
                    FileName = cmd
                    Arguments = args
                    WorkingDirectory =
                        if String.IsNullOrWhiteSpace workingDir
                        then info.WorkingDirectory
                        else workingDir
                }
                ) timeout
    if not exitCode.OK then
        exitCode.Errors
        |> String.concat Environment.NewLine
        |> failwithf "Error while running '%s %s': %s" cmd args

// --------------------------------------------------------------------------------------
// Targets
// --------------------------------------------------------------------------------------

Target.create "Clean" <| fun _ ->
    appReferences
    |> Seq.collect(fun p ->
        ["bin";"obj"]
        |> Seq.map(fun sp ->
             Path.GetDirectoryName p </> sp)
        )
    |> Shell.cleanDirs

Target.create "Restore" <| fun _ ->
    DotNet.restore id ""

Target.create "Build" <| fun _ ->
    DotNet.build id ""

Target.create "Test" <| fun _ ->
    !! "**/*.Tests.??proj"
    |> Seq.iter (fun p ->
        Trace.tracefn "Test %s" p
        let args = sprintf "--project %s -- --summary --sequenced" p
        DotNet.exec id "run" args |> Trace.tracefn "Test result: %A"
        // dotnetWith p "run" "--framework netcoreapp2.0" // why cli asks for framework?
        // |> ignore
        )
    // runTests id
    // |> Seq.iter (invoke)

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

Target.create "Pack" <| fun _ ->
    let releaseNotes = ReleaseNotes.load "RELEASE_NOTES.md"
    let extraArgs =
        [ sprintf "/p:PackageVersion=%s" releaseNotes.NugetVersion
          sprintf "/p:PackageReleaseNotes=\"%s\"" (String.concat "\n" releaseNotes.Notes)
          "/m:1"
        ] |> String.concat " "
    appReferences
    |> Seq.iter (fun proj ->
        DotNet.pack (fun c ->
            { c with
                Configuration = DotNet.Release
                OutputPath = IO.Directory.GetCurrentDirectory() </> "dist" |> Some
                Common = { c.Common with CustomParams = Some extraArgs }
                VersionSuffix = c.VersionSuffix
            }
        ) proj
        // DotNetCli.Pack (fun c ->
        //     { c with
        //         Project = proj
        //         Configuration = "Release"
        //         OutputPath = IO.Directory.GetCurrentDirectory() @@ "dist"
        //         AdditionalArgs =
        //             [
        //                 sprintf "/p:PackageVersion=%s" releaseNotes.NugetVersion
        //                 sprintf "/p:PackageReleaseNotes=\"%s\"" (String.concat "\n" releaseNotes.Notes)
        //             ]
        //     })
    )

Target.create "Publish" <| fun _ ->
    Paket.push(fun c ->
            { c with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )

// Target "ReleaseQuick" release
Target.create "Release" <| fun _ ->
    let branch = Git.Information.getBranchName ""
    let releaseNotes = ReleaseNotes.load "RELEASE_NOTES.md"
    let version = releaseNotes.SemVer.AsString
    if Git.Information.getBranchName "" <> "master" then failwithf "Not on master, instead on '%s'" branch

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" version)
    Git.Branches.push ""

    Git.Branches.tag "" version
    Git.Branches.pushTag "" "origin" version


// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------

"Clean"
//   ==> "InstallDotNetCLI"
  ==> "Restore"
  ==> "Build"
  ==> "Test"
  ==> "Pack"
  ==> "Publish"
  ==> "Release"

// "Restore"
//  ==> "WatchTests"

Target.runOrDefault "Pack"

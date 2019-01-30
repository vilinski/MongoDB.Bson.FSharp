open Microsoft.Build.Tasks
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Tools


let release = ReleaseNotes.load "RELEASE_NOTES.md"
let srcGlob = "src/**/*.*proj"
let testsGlob = "tests/**/*.*proj"

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    ++ "src/**/temp"
    ++ "src/**/dist"
    |> Shell.cleanDirs
    )

Target.create "Build" (fun _ ->
    !! srcGlob
    |> Seq.iter (DotNet.build id)
//     |> Seq.iter (fun proj ->
//         DotNetCli.Build (fun c ->
//             { c with
//                 Project = proj
//                 //This makes sure that Proj2 references the correct version of Proj1
//                 AdditionalArgs = [sprintf "/p:PackageVersion=%s" releaseNotes.NugetVersion]
//             })
// )
)

// let invoke f = f ()
// let invokeAsync f = async { f () }

// type TargetFramework =
// | Full of string
// | Core of string

// let (|StartsWith|_|) (prefix: string) (s: string) =
//     if s.StartsWith prefix then Some() else None

// let getTargetFramework tf =
//     match tf with
//     | StartsWith "net4" -> Full tf
//     | StartsWith "netcoreapp" -> Core tf
//     | _ -> failwithf "Unknown TargetFramework %s" tf

// let getTargetFrameworksFromProjectFile (projFile : string)=
//     let doc = Xml.XmlDocument()
//     doc.Load(projFile)
//     doc.GetElementsByTagName("TargetFrameworks").[0].InnerText.Split(';')
//     |> Seq.map getTargetFramework
//     |> Seq.toList

// let selectRunnerForFramework tf =
//     let runMono = sprintf "mono -f %s --restore -c Release"
//     let runCore = sprintf "run -f %s -c Release"
//     match tf with
//     | Full t when isMono-> runMono t
//     | Full t -> runCore t
//     | Core t -> runCore t




Target.create "Test" (fun _ ->
    !! testsGlob
    |> Seq.iter (DotNet.test id)
    // runTests id
    // |> Seq.iter (invoke)
)
// let execProcAndReturnMessages filename args =
//     let args' = args |> String.concat " "
//     ProcessHelper.ExecProcessAndReturnMessages
//                 (fun psi ->
//                     psi.FileName <- filename
//                     psi.Arguments <-args'
//                 ) (TimeSpan.FromMinutes(1.))

// let pkill args =
//     execProcAndReturnMessages "pkill" args

// let killParentsAndChildren processId =
//     pkill [sprintf "-P %d" processId]

let dotnet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir


Target.create "WatchTests" (fun _ ->
    dotnet "watch run" "tests/MongoDB.Bson.FSharp.Tests/"
)

Target.create "Pack" (fun _ ->
        Paket.pack (fun c ->
            { c with
                //ProjectUrl = "https://github.com/vilinski/MongoDB.Bson.FSharp"
                //BuildConfig = "Release"
                OutputPath = "dist"
                Version = release.NugetVersion
                ReleaseNotes = release.Notes |> String.concat "\n"
                //OutputPath = c.ReleaseNotes
                // MSBuildParams =
                //     [
                //         sprintf "/p:PackageVersion=%s" releaseNotes.NugetVersion
                //         sprintf "/p:PackageReleaseNotes=\"%s\"" (String.Join("\n",releaseNotes.Notes))
                //     ]
            })
)

Target.create "Publish" (fun _ ->
    Paket.push(fun c ->
            { c with
                //PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )
)

let makeRelease _ =
    let branch = Git.Information.getBranchName ""
    let version = release.NugetVersion
    if Git.Information.getBranchName "" <> "master" then failwithf "Not on master, instead on '%s'" branch

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" version)
    Git.Branches.push ""

    Git.Branches.tag "" version
    Git.Branches.pushTag "" "origin" version

Target.create "ReleaseQuick" makeRelease
Target.create "Release" makeRelease

"Clean"
  ==> "Build"
  ==> "Test"
  ==> "Pack"
  ==> "Publish"
  ==> "Release"


Target.runOrDefault "Pack"

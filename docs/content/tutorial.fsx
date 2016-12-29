(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/MongoDB.Bson.FSharp"

(**
Introducing your project
========================

Say more

*)
#r "MongoDB.Bson.dll"
#r "MongoDB.Bson.FSharp.dll"
open MongoDB.Bson.FSharp

type Twitter = string
type EMailAddress = string
type PreferedContact =
    | NoContact
    | Twitter of Twitter
    | EMail of EMailAddress

type Person =
    { name: string
      contact: PreferedContact
    }
let me =
    { name = "Andreas Vilinski"
      contact = Twitter "@vilinski"
    }

do FSharpSerializer.Register()
let test() =
    let canBson = me |> toBson |> fromBson = me
    let canJson = me |> toJson |> fromJson = me
    canBson && canJson

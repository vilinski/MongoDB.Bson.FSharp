// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.

#r "bin/Debug/netstandard2.0/MongoDB.Bson.FSharp.dll"
#r "../../packages/MongoDB.Bson/lib/net45/MongoDB.Bson.dll"
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
let test() =
    FSharpSerializer.Register()
    me |> toBson |> fromBson

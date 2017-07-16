// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.

#I "bin/Release/net45"
#r "FSharp.Core.dll"
#r "MongoDB.Bson.dll"
#r "MongoDB.Bson.FSharp.dll"
open MongoDB.Bson.FSharp
open MkSerializer

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

let intSer = mkBsonSerializer<int>()
let i = 32

let testRound a =
    let s = mkBsonSerializer<_>()
    use stream = new System.IO.MemoryStream()
    use w = new MongoDB.Bson.IO.BsonBinaryWriter(stream, BsonBinaryWriterSettings.Defaults)
    let wr = w.Write
    let d = s.enc
    ()
let ei = intSer.enc i r
let serializer = MkSerializer.mkBsonSerializer<Person>()
let test() =
    FSharpSerializer.Register()
    me |> toBson |> fromBson

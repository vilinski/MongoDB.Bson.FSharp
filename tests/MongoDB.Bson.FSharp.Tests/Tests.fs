module MongoDB.Bson.FSharp.Tests

open System.IO
open MongoDB.Bson
open MongoDB.Bson.IO

open MongoDB.Bson.FSharp
open MkSerializer
open TestItems
open Expecto

// register F# serializers
do FSharpSerializer.Register()

let hexDump lineLength (b: byte array) =
    b
    |> Array.chunkBySize lineLength
    |> Array.map (fun bytes ->
        let pad =
            String.replicate (lineLength - Array.length bytes) "   "
        let hex =
            bytes
            |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
            |> String.concat " "
        let str =
            bytes
            |> System.Text.Encoding.UTF8.GetString
            |> Seq.map (fun c -> if System.Char.IsLetterOrDigit c then c else '.')
            |> Seq.toArray
            |> System.String
            |> sprintf "%s"
        hex + pad + " | " + str)
    |> String.concat System.Environment.NewLine

let testDefaultBsonDocument() =
    let d = testItems |> toBsonDoc
    d.ToString() |> printfn "%s"
    Expect.equal (d |> fromBsonDoc) testItems "test items document serializing"
let testDefaultBsonBinary() =
    let b = testItems |> toBsonBin
    b |> hexDump 32 |> printfn "%A"
    Expect.equal (b |> fromBsonBin) testItems "test items binary serializing"
let testDefaultBsonJson() =
    let j = testItems |> toJson
    j |> printfn "%s"
    Expect.equal (j |> fromJson) testItems "test items json serializing"

let toBsonDocument (t: 't) =
    let s = mkBsonSerializer<'t>()
    let mutable d = BsonDocument()
    use w = new BsonDocumentWriter(d, BsonDocumentWriterSettings.Defaults) :> IBsonWriter
    let wr = s.enc w t
    d
let fromBsonDocument<'t>(d: BsonDocument) =
    let s = mkBsonSerializer<'t>()
    use r = new BsonDocumentReader(d) :> IBsonReader
    s.dec r

let toBsonBinary (t: 't) =
    let s = mkBsonSerializer<'t>()
    use stream = new System.IO.MemoryStream()
    use w = new BsonBinaryWriter(stream, BsonBinaryWriterSettings.Defaults) :> IBsonWriter
    let wr = s.enc w t
    stream.ToArray()
let fromBsonBinary<'t>(a: byte array) =
    let s = mkBsonSerializer<'t>()
    use stream = new MemoryStream(a)
    use r = new BsonBinaryReader(stream) :> IBsonReader
    s.dec r

let toBsonJson(t: 't) =
    let s = mkBsonSerializer<'t>()
    use sw = new StringWriter()
    use w = new JsonWriter(sw) :> IBsonWriter
    let wr = s.enc w t
    sw.ToString()
let fromBsonJson(j: string) =
    let s = mkBsonSerializer<'t>()
    use r = new JsonReader(j) :> IBsonReader
    s.dec r

let testBinary (t: 't) =
    let bin = toBsonBinary t
    let t' = fromBsonBinary bin
    t = t'

let testDoc (t: 't) =
    let d = toBsonDocument t
    let t' = fromBsonDocument d
    t = t'

let testJson (t: 't) =
    let d = toBsonJson t
    let t' = fromBsonJson d
    t = t'
let testBsonDocument() =
    let d = testItems |> toBsonDoc
    d.ToString() |> printfn "%s"
    Expect.isTrue (testDoc testItems) "test items document serializing"
let testBsonBinary() =
    let b = testItems |> toBsonBin
    b |> printfn "%A"
    Expect.isTrue (testBinary testItems) "test items binary serializing"
let testBsonJson() =
    let j = testItems |> toJson
    j |> printfn "%s"
    Expect.isTrue (testJson testItems) "test items json serializing"
[<Tests>]
let tests =
  testList "all" [
    testList "samples" [
      testCase "Say hello all" <| fun _ ->
        let subject = Say.hello "all"
        Expect.equal subject "Hello all" "You didn't say hello"
    ]
    testList "bson" [
        testList "default" [
            testCase "from/to BsonDocument" testDefaultBsonDocument
            testCase "from/to json string" testDefaultBsonJson
            testCase "from/to Bson byte array" testDefaultBsonBinary
        ]
        // testList "with TypeShape" [
        //     testCase "from/to BsonDocument" testBsonDocument
        //     testCase "from/to json string" testBsonJson
        //     testCase "from/to Bson byte array" testBsonBinary
        // ]
    ]
  ]

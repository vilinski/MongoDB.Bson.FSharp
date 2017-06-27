module MongoDB.Bson.FSharp.Tests

open MongoDB.Bson.FSharp
open TestItems
open Expecto

// register F# serializers
do FSharpSerializer.Register()

let toBsonDocument() =
    Expect.equal (testItems |> toBson |> fromBson) testItems "test items serializing"
let toBsonArray() =
    Expect.equal (testItems |> toBson |> fromBson) testItems "test items serializing"
    Expect.equal (testItems |> toBson |> fromBson) testItems "test items serializing"
let toJsonString() =
    testItems |> toJson |> printfn "%s"
    Expect.equal (testItems |> toJson |> fromJson) testItems "test items serializing"

[<Tests>]
let tests =
  testList "all" [
    testList "samples" [
      testCase "Say hello all" <| fun _ ->
        let subject = Say.hello "all"
        Expect.equal subject "Hello all" "You didn't say hello"
    ]
    testList "bson" [
        testCase "from/to BsonDocument" toBsonDocument
        testCase "from/to json string" toJsonString
        testCase "from/to Bson byte array" toBsonArray
    ]
  ]
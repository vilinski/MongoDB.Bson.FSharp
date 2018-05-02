module MongoDB.Bson.FSharp.Tests

open MongoDB.Bson.FSharp
open MongoDB.Bson.Diff
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

let diffNoChanges() =
    let x = testItem |> toBson
    let result = diff x x
    Expect.equal [] result "no changes - no diff"
let diffSimple() =
    let x = testItem |> toBson
    let y = { testItem with Id = 1 } |> toBson
    let result = diff x y
    Expect.equal [] result "no changes - no diff"
let diffComplex() =
    let x = testItem |> toBson
    let y = { testItem with Id = 1 } |> toBson
    let result = diff x y
    Expect.equal [] result "no changes - no diff"

[<Tests>]
let tests =
  testList "all" [
    testList "bson" [
        testCase "from/to BsonDocument" toBsonDocument
        testCase "from/to json string" toJsonString
        testCase "from/to Bson byte array" toBsonArray
    ]
    testList "diff" [
        testCase "no changes" diffNoChanges
        testCase "simple" diffSimple
        testCase "complex" diffComplex
    ]
  ]

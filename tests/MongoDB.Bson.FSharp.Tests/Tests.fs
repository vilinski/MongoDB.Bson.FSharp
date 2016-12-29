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
    testList "bson" [
        testCase "from/to BsonDocument" toBsonDocument
        testCase "from/to json string" toJsonString
        testCase "from/to Bson byte array" toBsonArray
    ]

(*

open NUnit.Framework

[<Test>]
let ``hello returns 42`` () =
  let result = Library.hello 42
  printfn "%i" result
  Assert.AreEqual(42,result)
*)

[<EntryPoint>]
let main argv =
    Tests.runTestsInAssembly defaultConfig argv

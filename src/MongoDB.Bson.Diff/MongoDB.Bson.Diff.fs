module MongoDB.Bson.Diff

open MongoDB.Bson
open MongoDB.Bson.FSharp

let rec private oldnew (path: string list) a b =
    doc [
        "path", (BsonArray path :> BsonValue)
        "old", a
        "new", b
    ]

let rec private diffFlat (path: string list) (x: BsonValue) (y: BsonValue): BsonDocument list =
    if x.BsonType = y.BsonType
    then
        // TODO recursive diff subdocument
        match x.BsonType with
        | BsonType.Document ->
            let xs = x.AsBsonDocument.Elements |> Seq.toList
            let ys = y.AsBsonDocument.Elements |> Seq.toList
            diffFlatDocElements path xs ys
        | BsonType.Array ->
            let xs = x.AsBsonArray.Values |> Seq.toList
            let ys = y.AsBsonArray.Values |> Seq.toList
            diffFlatArrayElements path xs ys
        | _ -> if x = y then [] else [oldnew path x y]
    else [oldnew path x y]
and private diffFlatDocElements (path: string list) (xs: BsonElement list) (ys: BsonElement list) =
    ys
    |> List.zip xs
    |> List.filter (fun (x,y) -> x.Name <> y.Name)
    |> List.collect (fun (x,y) ->
        diffFlat (x.Name :: path) x.Value y.Value
    )
and private diffFlatArrayElements (path: string list) (xs: BsonValue list) (ys: BsonValue list) =
    ys
    |> List.zip xs
    |> List.mapi(fun i (x,y) -> diffFlat (string i :: path) x y)
    |> List.collect id
let diff (a: BsonDocument) (b: BsonDocument) =
    diffFlat [] a b

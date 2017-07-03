module MkSerializer

open TypeShape
open TypeShape_Utils
open MongoDB.Bson.IO
open MongoDB.Bson.Serialization


type 't ToBson = 't -> BsonWriter -> unit
open System.Reflection


let rec mkBsonSerializer<'t>() : 't ToBson =
        let ctx = new RecTypeManager()
        mkBsonSerializerCached<'t> ctx
and private mkBsonSerializerCached<'t> (ctx: RecTypeManager) : 't ToBson =
    match ctx.TryFind<'t ToBson>() with
    | Some p -> p
    | None ->
        ctx.CreateUninitialized<'t ToBson>(fun c t -> c.Value t) |> ignore
        let p = mkBsonSerializerAux<'t> ctx
        ctx.Complete p
and private mkBsonSerializerAux<'t>(ctx: RecTypeManager) : 't ToBson =
    let wrap(p: 'a ToBson) = unbox<'t ToBson> p
    let mkFieldSerializer (field: IShapeMember<'DeclaringType>) =
        field.Accept {
            new IMemberVisitor<'DeclaringType, string * ('DeclaringType ToBson)> with
                member __.Visit(field: ShapeMember<'DeclaringType, 'Field>) =
                    let fp = mkBsonSerializerCached<'Field> ctx
                    field.Label, fp << field.Project
        }
    let writeSeq (tp: 't ToBson) (ts: 't seq) (w: BsonWriter)  =
        w.WriteStartArray()
        ts |> Seq.iter (fun t -> tp t w)
        w.WriteEndArray()
    match shapeof<'t> with
    | Shape.Unit -> wrap(fun () w -> w.WriteNull())
    | Shape.Bool -> wrap(fun v w -> w.WriteBoolean v)
    | Shape.Int32 -> wrap(fun v w -> w.WriteInt32 v)
    | Shape.FSharpOption s ->
        s.Accept {
            new IFSharpOptionVisitor<'t ToBson> with
                member __.Visit<'a>() = // 't = 'a option
                    let tp = mkBsonSerializerCached<'a> ctx
                    wrap(fun v w ->
                            match v with
                            | None -> w.WriteNull()
                            | Some t -> tp t w
                        )
        }
    | Shape.FSharpList s ->
        s.Accept {
            new IFSharpListVisitor<'t ToBson> with
                member __.Visit<'t>() =
                    let tp = mkBsonSerializerCached<'a> ctx
                    wrap(fun ts w -> writeSeq tp ts w)
        }
    // | Shape.Array s ->
    //     s.Accept {
    //         new IArrayVisitor<'t ToBson> with
    //             member __.Visit<'a> rank =
    //                 let tp = mkBsonSerializerCached<'a> ctx
    //                 wrap(fun ts w -> writeSeq tp ts w)
    //     }
    | Shape.FSharpSet s ->
        s.Accept {
            new IFSharpSetVisitor<'t ToBson> with
                member __.Visit<'t when 't : comparison>() =
                    let tp = mkBsonSerializerCached<'a> ctx
                    wrap(fun ts w -> writeSeq tp ts w)
        }
    | Shape.FSharpMap s ->
        s.Accept {
            new IFSharpMapVisitor<'t ToBson> with
                member __.Visit<'k, 'v when 'k : comparison>() =
                    let kp = mkBsonSerializerCached<'k> ctx
                    let vp = mkBsonSerializerCached<'v> ctx
                    if typeof<'k> <> typeof<string> then // document
                        wrap(fun m w ->
                            w.WriteStartDocument()
                            m |> Map.iter(fun k v -> w.WriteName k; vp v w )
                            w.WriteEndDocument()
                        )
                    else // array of documents with k,v fields
                        wrap(fun m w ->
                            w.WriteStartArray()
                            let mutable i = 0
                            for KeyValue(k,v) in m do
                                i <- i + 1
                                i |> string |> w.WriteStartDocument
                                w.WriteName "k"
                                kp k w
                                w.WriteName "v"
                                vp v w
                                w.WriteEndDocument()
                                ()
                            w.WriteEndArray()
                        )
        }
    | Shape.Tuple (:? ShapeTuple<'t> as shape) ->
        let elemSerializers = shape.Elements |> Array.map mkFieldSerializer
        fun (t:'t) w ->
            w.WriteStartArray()
            elemSerializers
            |> Seq.iter (fun (_,ep) -> ep t w)
            w.WriteEndArray()


    | Shape.FSharpRecord (:? ShapeFSharpRecord<'t> as shape) ->
        let fieldSerializers = shape.Fields |> Array.map mkFieldSerializer
        fun (r:'t) w ->
            w.WriteStartDocument()
            fieldSerializers
            |> Seq.iter (fun (label, ep) ->
                w.WriteName(label)
                ep r w)
            w.WriteEndDocument()

    | Shape.FSharpUnion (:? ShapeFSharpUnion<'t> as shape) ->
        let mkUnionCaseSerializer (s : ShapeFSharpUnionCase<'t>) =
            let fieldSerializers = s.Fields |> Array.map mkFieldSerializer
            fun (u:'t) (w: BsonWriter) ->
                w.WriteStartArray()
                w.WriteString("1", s.CaseInfo.Name)

                match fieldSerializers with
                | [||] -> ()
                | [|_,fp|] ->
                    w.WriteName "2"
                    fp u w
                | fps ->
                    fps
                    |> Seq.iteri (fun i (_,fp) ->
                        i + 2 |> sprintf "%i" |> w.WriteName
                        fp u w)
                w.WriteEndArray()

        let caseSerializers = shape.UnionCases |> Array.map mkUnionCaseSerializer
        fun (u:'t) ->
            let printer = caseSerializers.[shape.GetTag u]
            printer u

    | Shape.Poco (:? ShapePoco<'t> as shape) ->
        let propSerializers = shape.Properties |> Array.map mkFieldSerializer
        fun (r:'t) w ->
            w.WriteStartDocument()
            propSerializers
            |> Seq.iter (fun (label, ep) ->
                w.WriteName label
                ep r w)
            w.WriteEndDocument()
    | _ -> failwithf "unsupported type '%O'" typeof<'t>

type MkSerializer() =
    member __.FromType (ty: System.Type) =
        ()
type ITypeShapeBasedSerializerProvider() =
    // { new MongoDB.Bson.Serialization.IBsonSerializationProvider with
    //     member __.GetSerializer(type:Type): IBsonSerializer = null
    // }
    member __.MkBsonSerializer<'t>() = mkBsonSerializer<'t>()
    member __.MkBsonSerializer(ty: System.Type) =
        typeof<ITypeShapeBasedSerializerProvider>
            .GetMethod("MkBsonSerializer")
            .MakeGenericMethod(ty)
            .Invoke(__, null)
    // interface MongoDB.Bson.Serialization.IBsonSerializationProvider with
    //     member __.GetSerializer (ty: System.Type) =
    //         let serialize: 't ToBson = __.MkBsonSerializer ty :> (:t ToBson)
    //         { new IBsonSerializer with
    //             member x.Serialize(ctx, args, value) =
    //                 serialize ctx.Writer args value
    //             member x.Deserialize (ctx, args) = null
    //             member x.ValueType = ty
    //         }

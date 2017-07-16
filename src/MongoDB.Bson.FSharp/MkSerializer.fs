module MkSerializer

open TypeShape
open TypeShape_Utils
open MongoDB.Bson
open MongoDB.Bson.IO
open MongoDB.Bson.Serialization


type 't Enc = 't -> BsonWriter -> unit
type 't Dec = BsonReader -> 't
type 't EncDec =
    { enc: 't Enc
      dec: 't Dec }

open System.Reflection

let noDec: 't Dec = function
    | _ -> Unchecked.defaultof<'t>

let rec mkBsonSerializer<'t>() : 't EncDec =
        let ctx = new RecTypeManager()
        mkBsonSerializerCached<'t> ctx
and private mkBsonSerializerCached<'t> (ctx: RecTypeManager) : 't EncDec =
    match ctx.TryFind<'t EncDec>() with
    | Some p -> p
    | None ->
        ctx.CreateUninitialized<'t EncDec>(fun c -> c.Value) |> ignore
        let p = mkBsonSerializerAux<'t> ctx
        ctx.Complete p
and private mkBsonSerializerAux<'t>(ctx: RecTypeManager) : 't EncDec =
    let wrap (enc: 'a Enc) (dec: 'a Dec) =
        { enc = enc; dec = dec } |> unbox<'t EncDec>
    let mkFieldSerializer (field: IShapeMember<'DeclaringType>) =
        field.Accept {
            new IMemberVisitor<'DeclaringType, string * ('DeclaringType EncDec)> with
                member __.Visit(field: ShapeMember<'DeclaringType, 'Field>) =
                    let p = mkBsonSerializerCached<'Field> ctx
                    let enc = p.enc << field.Project
                    let dec = noDec
                    field.Label, wrap enc dec
        }
    // let mkFieldDeserializer(field: IShapeWriteMember<'DeclaringType>) =
    //     field.Accept {
    //         new IWriteMemberVisitor<'DeclaringType, string * ('DeclaringType Dec)> with
    //             member __.Visit (shape : ShapeWriteMember<'DeclaringType, 'Field>) =
    //                 let rf = mkBsonSerializer<'Field>()
    //                 shape.Inject dt f
    //                 //gen { let! f = rf in return fun dt -> shape.Inject dt f } }
    //     }
    let writeSeq (tp: 't Enc) (ts: 't seq) (w: BsonWriter) =
        w.WriteStartArray()
        ts |> Seq.iter (fun t -> tp t w)
        w.WriteEndArray()
    let readSeq (tp: 't Dec) (r: BsonReader) =
        seq {
            do r.ReadStartArray()
            while r.ReadBsonType() <> BsonType.EndOfDocument do
                yield tp r
            do r.ReadEndArray()
        }

    match shapeof<'t> with
    | Shape.Unit ->
        wrap (fun () w -> w.WriteNull())
             (fun r -> r.ReadNull())
    | Shape.Bool ->
        wrap (fun v w -> w.WriteBoolean v)
             (fun r -> r.ReadBoolean())
    | Shape.Int32 ->
        wrap (fun v w -> w.WriteInt32 v)
             (fun r -> r.ReadInt32())
    | Shape.FSharpOption s ->
        s.Accept {
            new IFSharpOptionVisitor<'t EncDec> with
                member __.Visit<'a>() = // 't = 'a option
                    let tp = mkBsonSerializerCached<'a> ctx
                    wrap (fun v w ->
                            match v with
                            | None -> w.WriteNull()
                            | Some t -> tp.enc t w)
                         noDec
        }
    | Shape.FSharpList s ->
        s.Accept {
            new IFSharpListVisitor<'t EncDec> with
                member __.Visit<'t>() =
                    let tp = mkBsonSerializerCached<'a> ctx
                    wrap (fun (ts: 'a list) w -> writeSeq tp.enc ts w)
                         (fun r -> readSeq tp.dec r |> Seq.toList)
        }
    | Shape.Array s ->
        s.Accept {
            new IArrayVisitor<'t EncDec> with
                member __.Visit<'t> rank =
                    let tp = mkBsonSerializerCached<'a> ctx
                    wrap (fun (ts: 'a array) w -> writeSeq tp.enc ts w)
                         (fun r -> readSeq tp.dec r |> Seq.toArray)
        }
    // | Shape.FSharpSet s ->
    //     s.Accept {
    //         new IFSharpSetVisitor<'t EncDec> with
    //             member __.Visit<'t when 't : comparison>() =
    //                 let tp = mkBsonSerializerCached<'a> ctx
    //                 wrap (fun (ts: 'a Set) w -> writeSeq tp.enc ts w)
    //                      (fun r -> readSeq tp.dec r |> Set.ofSeq)
    //     }
    | Shape.FSharpMap s ->
        s.Accept {
            new IFSharpMapVisitor<'t EncDec> with
                member __.Visit<'k, 'v when 'k : comparison>() =
                    let kp = mkBsonSerializerCached<'k> ctx
                    let vp = mkBsonSerializerCached<'v> ctx
                    if typeof<'k> <> typeof<string> then // document
                        wrap (fun m w ->
                              w.WriteStartDocument()
                              m |> Map.iter(fun k v -> w.WriteName k; vp.enc v w )
                              w.WriteEndDocument())
                             noDec

                    else // array of documents with k,v fields
                        wrap (fun m w ->
                                w.WriteStartArray()
                                let mutable i = 0
                                for KeyValue(k,v) in m do
                                    i <- i + 1
                                    i |> string |> w.WriteStartDocument
                                    w.WriteName "k"
                                    kp.enc k w
                                    w.WriteName "v"
                                    vp.enc v w
                                    w.WriteEndDocument()
                                    ()
                                w.WriteEndArray())
                             noDec

        }
    | Shape.Tuple (:? ShapeTuple<'t> as shape) ->
        let elemSerializers = shape.Elements |> Array.map mkFieldSerializer
        wrap
            (fun (t:'t) w ->
                w.WriteStartArray()
                elemSerializers
                |> Seq.iter (fun (_,ep) -> ep.enc t w)
                w.WriteEndArray())
            noDec

    | Shape.FSharpRecord (:? ShapeFSharpRecord<'t> as shape) ->
        let fieldSerializers = shape.Fields |> Array.map mkFieldSerializer
        let dec (r: BsonReader) =
            let mutable target = shape.CreateUninitialized()
            for (lbl,ep) in fieldSerializers do
                let name = r.ReadName()
                let value = ep.dec r
                // TODO set record fields
                ()//target <- u target
            target
        wrap
            (fun (r:'t) w ->
                w.WriteStartDocument()
                fieldSerializers
                |> Seq.iter (fun (label, ep) ->
                    w.WriteName(label)
                    ep.enc r w)
                w.WriteEndDocument())
            dec

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
                    fp.enc u w
                | fps ->
                    fps
                    |> Seq.iteri (fun i (_,fp) ->
                        i + 2 |> sprintf "%i" |> w.WriteName
                        fp.enc u w)
                w.WriteEndArray()

        let caseSerializers = shape.UnionCases |> Array.map mkUnionCaseSerializer
        wrap
            (fun (u:'t) ->
                let printer = caseSerializers.[shape.GetTag u]
                printer u)
            noDec

    | Shape.Poco (:? ShapePoco<'t> as shape) ->
        let propSerializers = shape.Properties |> Array.map mkFieldSerializer
        wrap
            (fun (r:'t) w ->
                w.WriteStartDocument()
                propSerializers
                |> Seq.iter (fun (label, ep) ->
                    w.WriteName label
                    ep.enc r w)
                w.WriteEndDocument())
            noDec
    | _ -> failwithf "unsupported type '%O'" typeof<'t>


type ITypeShapeBasedSerializerProvider() =
    // { new MongoDB.Bson.Serialization.IBsonSerializationProvider with
    //     member __.GetSerializer(type:Type): IBsonSerializer = null
    // }
    member __.MkBsonSerializer<'t>() = mkBsonSerializer<'t>()
    member __.MkBsonSerializer(ty: System.Type) =
        typeof<ITypeShapeBasedSerializerProvider>
            .GetTypeInfo()
            .GetMethod("MkBsonSerializer")
            .MakeGenericMethod(ty)
            .Invoke(__, null)
    // interface MongoDB.Bson.Serialization.IBsonSerializationProvider with
    //     member __.GetSerializer (ty: System.Type) =
    //         let serialize: 't Enc = __.MkBsonSerializer ty :> (:t Enc)
    //         { new IBsonSerializer with
    //             member x.Serialize(ctx, args, value) =
    //                 serialize ctx.Writer args value
    //             member x.Deserialize (ctx, args) = null
    //             member x.ValueType = ty
    //         }

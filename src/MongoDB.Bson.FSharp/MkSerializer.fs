module MkSerializer

open TypeShape
open TypeShape_Utils
open MongoDB.Bson
open MongoDB.Bson.IO
open MongoDB.Bson.Serialization


type 't Enc = IBsonWriter -> 't -> unit
type 't Dec = IBsonReader -> 't
type 't EncDec =
    { enc: 't Enc
      dec: 't Dec }

type 't FieldEncDec =
    { memberName: string
      memberEnc: IBsonWriter -> 't -> unit
      memberDec: IBsonReader -> 't -> 't }


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
    let codec (enc: 'a Enc) (dec: 'a Dec) =
        { enc = enc; dec = dec } |> unbox<'t EncDec>
    let mkMemberSerializer (field: IShapeWriteMember<'DeclaringType>) =
        field.Accept {
            new IWriteMemberVisitor<'DeclaringType, 'DeclaringType FieldEncDec> with
                member __.Visit(shape: ShapeWriteMember<'DeclaringType, 'Field>) =
                    let p = mkBsonSerializerCached<'Field> ctx
                    let enc (w: IBsonWriter) c =
                        let m = shape.Project c
                        p.enc w m
                    let dec (r: IBsonReader) c =
                        let m = p.dec r
                        shape.Inject c m
                    { memberName = field.Label
                      memberEnc = enc
                      memberDec = dec }
        }
    let readIgnore r =
        // TODO read and throwaway the value
        ()
    let combineMemberSerializers (init: unit -> 'a) (members: 'a FieldEncDec []) =
        let decs = members |> Array.map(fun m -> m.memberName, m.memberDec) |> Map.ofArray
        let encs = members |> Array.map(fun m -> m.memberName, m.memberEnc)
        let enc (w: IBsonWriter) (c: 'a) =
            w.WriteStartDocument()
            for (name,enc) in encs do
                w.WriteName(name)
                enc w c
            w.WriteEndDocument()
        let dec (r: IBsonReader) =
            let mutable c = init()
            r.ReadStartDocument()
            while(r.ReadBsonType() <> BsonType.EndOfDocument) do
                let name = r.ReadName()
                match decs |> Map.tryFind name with
                | None -> readIgnore r
                | Some dec ->
                    c <- dec r c
            r.ReadEndDocument()
            c
        { enc = enc; dec = dec }
    let writeSeq (tp: 'a Enc) (w: IBsonWriter) (ts: 'a seq) =
        w.WriteStartArray()
        ts |> Seq.iter (fun t -> tp w t)
        w.WriteEndArray()
    let readSeq (tp: 'a Dec) (r: IBsonReader) =
        seq {
            do r.ReadStartArray()
            while r.ReadBsonType() <> BsonType.EndOfDocument do
                yield tp r
            do r.ReadEndArray()
        }

    let shape = shapeof<'t>
    printfn "shapeof<'t> is %A" shape
    match shape with
    | Shape.Unit ->
        codec (fun w () -> w.WriteNull())
              (fun r -> r.ReadNull())
    | Shape.Bool ->
        codec (fun w v -> w.WriteBoolean v)
              (fun r -> r.ReadBoolean())
    | Shape.Int32 ->
        codec (fun w v -> w.WriteInt32 v)
              (fun r -> r.ReadInt32())
    | Shape.FSharpOption s ->
        s.Accept {
            new IFSharpOptionVisitor<'t EncDec> with
                member __.Visit<'a>() = // 't = 'a option
                    let tp = mkBsonSerializerCached<'a> ctx
                    codec (fun w v ->
                            match v with
                            | None -> w.WriteNull()
                            | Some t -> tp.enc w t)
                          (fun r ->
                            if r.ReadBsonType() = BsonType.Null then None
                            else
                                tp.dec r |> Some
                          )
        }
    | Shape.FSharpList s ->
        s.Accept {
            new IFSharpListVisitor<'t EncDec> with
                member __.Visit<'t>() =
                    let tp = mkBsonSerializerCached<'t> ctx
                    codec (fun w (ts: 't list) -> writeSeq tp.enc w ts)
                          (fun r -> readSeq tp.dec r |> List.ofSeq)
        }
    | Shape.Array s ->
        s.Accept {
            new IArrayVisitor<'t EncDec> with
                member __.Visit<'t> rank =
                    let tp = mkBsonSerializerCached<'t> ctx
                    codec (fun w (ts: 't array) -> writeSeq tp.enc w ts)
                          (fun r -> readSeq tp.dec r |> Array.ofSeq)
        }
    // | Shape.FSharpSet s ->
    //     s.Accept {
    //         new IFSharpSetVisitor<'t EncDec> with
    //             member __.Visit<'t when 't : comparison>() =
    //                 let tp = mkBsonSerializerCached<'a> ctx
    //                 codec (fun w (ts: 'a Set) -> writeSeq tp.enc w ts)
    //                       (fun r -> readSeq tp.dec r |> Set.ofSeq)
    //     }
    | Shape.FSharpMap s ->
        s.Accept {
            new IFSharpMapVisitor<'t EncDec> with
                member __.Visit<'k, 'v when 'k : comparison>() =
                    let kp = mkBsonSerializerCached<'k> ctx
                    let vp = mkBsonSerializerCached<'v> ctx
                    if typeof<'k> <> typeof<string> then // document
                        codec (fun w m ->
                                 w.WriteStartDocument()
                                 m |> Map.iter(fun k v -> w.WriteName k; vp.enc w v )
                                 w.WriteEndDocument())
                              noDec

                    else // array of documents with k,v fields
                        codec (fun w m ->
                                w.WriteStartArray()
                                let mutable i = 0
                                for KeyValue(k,v) in m do
                                    i <- i + 1
                                    i |> string |> w.WriteStartDocument
                                    w.WriteName "k"
                                    kp.enc w k
                                    w.WriteName "v"
                                    vp.enc w v
                                    w.WriteEndDocument()
                                    ()
                                w.WriteEndArray())
                             noDec
        }
    | Shape.Tuple (:? ShapeTuple<'t> as shape) ->
        let elemSerializers = shape.Elements |> Array.map mkMemberSerializer
        codec
            (fun w (t:'t) ->
                w.WriteStartArray()
                elemSerializers
                |> Seq.iter (fun ep -> ep.memberEnc w t)
                w.WriteEndArray())
            noDec

    | Shape.FSharpRecord (:? ShapeFSharpRecord<'t> as shape) ->
        shape.Fields
        |> Array.map mkMemberSerializer
        |> combineMemberSerializers (fun () -> shape.CreateUninitialized())

    // | Shape.FSharpUnion (:? ShapeFSharpUnion<'t> as shape) ->
    //     let mkUnionCaseSerializer (s : ShapeFSharpUnionCase<'t>) =
    //         let fieldSerializers = s.Fields |> Array.map mkMemberSerializer
    //         fun (u:'t) (w: IBsonWriter) ->
    //             w.WriteStartArray()
    //             w.WriteString("1", s.CaseInfo.Name)

    //             match fieldSerializers with
    //             | [||] -> ()
    //             | [|fp|] ->
    //                 w.WriteName "2"
    //                 fp.memberEnc w u
    //             | fps ->
    //                 fps
    //                 |> Seq.iteri (fun i fp ->
    //                     i + 2 |> sprintf "%i" |> w.WriteName
    //                     fp.memberEnc w u)
    //             w.WriteEndArray()
    //     let caseSerializers = shape.UnionCases |> Array.map mkUnionCaseSerializer
    //     codec
    //         (fun w (u:'t) ->
    //             let enc = caseSerializers.[shape.GetTag u]
    //             enc w u)
    //         noDec

    // | Shape.Poco (:? ShapePoco<'t> as shape) ->
    //     let propSerializers = shape.Properties |> Array.map mkMemberSerializer
    //     codec
    //         (fun w (r:'t) ->
    //             w.WriteStartDocument()
    //             propSerializers
    //             |> Seq.iter (fun (label, ep) ->
    //                 w.WriteName label
    //                 ep.enc w r)
    //             w.WriteEndDocument())
    //         noDec
    | _ -> failwithf "unsupported type '%O'" typeof<'t>

type TypeShapeSerializer<'t>() =
    let codec = mkBsonSerializer<'t>()
    interface IBsonSerializer<'t> with
        member x.Serialize(context: BsonSerializationContext, args: BsonSerializationArgs, value: 't): unit =
            codec.enc context.Writer value
        member x.Serialize(context: BsonSerializationContext, args: BsonSerializationArgs, value: obj): unit =
            value |> unbox |> codec.enc context.Writer
        member x.Deserialize (context: BsonDeserializationContext, args: BsonDeserializationArgs): 't =
            codec.dec context.Reader
        member x.Deserialize (context: BsonDeserializationContext, args: BsonDeserializationArgs): obj =
            codec.dec context.Reader |> box
        member x.ValueType = typedefof<'t>
type TypeShapeSerializerProvider() =
    member __.MkBsonSerializer<'t>() = TypeShapeSerializer<'t>()
    member __.MkBsonSerializer(ty: System.Type): IBsonSerializer =
        typedefof<TypeShapeSerializerProvider>
            .GetTypeInfo()
            .GetMethod("MkBsonSerializer")
            .MakeGenericMethod(ty)
            .Invoke(__, null)
            |> unbox
    interface MongoDB.Bson.Serialization.IBsonSerializationProvider with
        member __.GetSerializer (ty: System.Type) =
            __.MkBsonSerializer ty
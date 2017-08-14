namespace MongoDB.Bson.FSharp

open Microsoft.FSharp.Reflection
open System
open System.Reflection
open MongoDB.Bson
open MongoDB.Bson.IO
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Options
open MongoDB.Bson.Serialization.Serializers
open MongoDB.Bson.Serialization.Conventions


[<AutoOpen>]
module internal Helpers =
    let IsUnion objType =
        FSharpType.IsUnion(objType)

    let IsOption objType =
        IsUnion objType &&
        objType.GetTypeInfo().IsGenericType &&
        objType.GetGenericTypeDefinition() = typedefof<_ option>

    let IsList objType = IsUnion objType &&
                            objType.GetTypeInfo().IsGenericType &&
                            objType.GetGenericTypeDefinition() = typedefof<_ list>

    let IsMap (objType: Type) =
        objType.GetTypeInfo().IsGenericType &&
        objType.GetGenericTypeDefinition() = typedefof<Map<_,_>>

    let IsSet (objType: Type) =
        objType.GetTypeInfo().IsGenericType &&
        objType.GetGenericTypeDefinition() = typedefof<Set<_>>

    let GetUnionCases objType =
        FSharpType.GetUnionCases(objType)
        |> Seq.map(fun x -> (x.Name, x))
        |> dict

    let IsRecord (objType) =
        FSharpType.IsRecord(objType)

    let GetRecordFields objType =
        FSharpType.GetRecordFields(objType)

    let rec configureDictSerializer representation (serializer: IBsonSerializer) =
        printfn "try configure dict serializer %s" <| serializer.GetType().FullName
        let rec configureChildDictSerializer
                (representation : DictionaryRepresentation)
                (serializer: IBsonSerializer): IBsonSerializer =
            match serializer with
            | :? IChildSerializerConfigurable as c ->
                printfn "try configure dict child serializer %s" <| c.ChildSerializer.GetType().FullName
                c.ChildSerializer
                |> configureDictSerializer representation
                |> c.WithChildSerializer
            | c -> c
        match serializer with
        | :? IDictionaryRepresentationConfigurable as dictRepConf ->
            printfn "configure dictionary representation"
            dictRepConf.WithDictionaryRepresentation representation
        | _ ->
            printfn "not a dict - return unconfigured"
            serializer
        |> configureChildDictSerializer representation

    let serializeSeq (context: BsonSerializationContext) (serializer: 'a IBsonSerializer) (items: 'a seq) =
        let writer = context.Writer
        writer.WriteStartArray()
        for item in items do
            serializer.Serialize(context, item)
        writer.WriteEndArray()
    let deserializeSeq (context: BsonDeserializationContext) (serializer: 'a IBsonSerializer)=
        let reader = context.Reader
        reader.ReadStartArray()
        let acc = ResizeArray()
        while reader.ReadBsonType() <> BsonType.EndOfDocument do
            let item = serializer.Deserialize(context)
            acc.Add item

        reader.ReadEndArray()
        // let res = serializer.Deserialize(context, args)
        // res |> unbox |> List.ofSeq<'a>
        acc
type OptionConvention() =
    inherit ConventionBase("F# Option Type")

    interface IMemberMapConvention with
        member this.Apply(memberMap) =
            let objType = memberMap.MemberType
            if IsOption objType then
                memberMap.SetDefaultValue None |> ignore
                memberMap.SetIgnoreIfNull true |> ignore

type RecordConvention() =
    inherit ConventionBase("F# Record Type")

    interface IClassMapConvention with
        member this.Apply(classMap) =
            let objType = classMap.ClassType

            if IsRecord objType then
                // printfn "convention for record %A" objType.Name

                classMap.SetIgnoreExtraElements(true)
                let fields = GetRecordFields objType
                let names = fields |> Array.map (fun x -> x.Name)
                let types = fields |> Array.map (fun x -> x.PropertyType)
                let ctor = objType.GetTypeInfo().GetConstructor(types)
                classMap.MapConstructor(ctor, names) |> ignore
                fields |> Array.iter (classMap.MapMember >> ignore)
/// Dictionary representation convention
///
/// ## Parameters
///  - `representation` - the specified dictionary representation used to serialize dictionaries
type DictionaryRepresentationConvention(representation : DictionaryRepresentation ) =
    inherit ConventionBase("Dictionary representation convention")

    interface IMemberMapConvention with
        member this.Apply(memberMap) =
            printfn "try apply dict convention to %A.%s (%s)"
                memberMap.ClassMap.ClassType.FullName
                memberMap.MemberName
                memberMap.MemberType.FullName
            memberMap.GetSerializer()
            |> configureDictSerializer representation
            |> memberMap.SetSerializer
            |> ignore


// type RecordSerializer<'TRecord>() =

//     inherit SerializerBase<'TRecord>()
//     let classMap = BsonClassMap.LookupClassMap(typeof<'TRecord>)
//     let serializer = BsonClassMapSerializer(classMap)
//     let fields = GetRecordFields typeof<'TRecord>

//     override this.Serialize(context, args, value) =
//         let recordType =
//             let t = typeof<'TRecord>
//             printfn "deserialize record %A = %A" t.Name value
//             t
//         let mutable nargs = args
//         nargs.NominalType <- typeof<'TRecord>
//         serializer.Serialize(context, nargs, value)

//     override this.Deserialize(context, args) =
//         let recordType =
//             let t = typeof<'TRecord>
//             printfn "deserialize record %A" t.Name
//             t
//         let mutable nargs = args
//         nargs.NominalType <- typeof<'TRecord>
//         serializer.Deserialize(context, nargs)

//     interface IBsonDocumentSerializer with
//         member x.TryGetMemberSerializationInfo(memberName, serializationInfo) =
//             if Array.exists (fun (el: PropertyInfo) -> el.Name = memberName) fields then
//                 let mm = classMap.GetMemberMap(memberName)
//                 serializationInfo <- new BsonSerializationInfo(mm.ElementName, mm.GetSerializer(), mm.MemberType)
//                 true
//             else
//                 false

type DiscriminatedUnionSerializer<'t>() =
    inherit SerializerBase<'t>()
    let caseFieldName = "case"
    let valueFieldName = "fields"
    let cases = GetUnionCases typeof<'t>

    let deserBy context args t =
        BsonSerializer.LookupSerializer(t).Deserialize(context, args)

    let serBy context args t v =
        BsonSerializer.LookupSerializer(t).Serialize(context, args, v)

    let readItems context args types =
        types
        |> Seq.fold(fun state t -> (deserBy context args t) :: state) []
        |> Seq.toArray
        |> Array.rev

    override this.Deserialize(context, args): 't =
        context.Reader.ReadStartDocument()

        context.Reader.ReadName(caseFieldName)
        let name = context.Reader.ReadString()
        let union = cases.[name]

        context.Reader.ReadName(valueFieldName)
        context.Reader.ReadStartArray()

        let items = readItems context args (union.GetFields() |> Seq.map(fun f -> f.PropertyType))

        context.Reader.ReadEndArray()
        context.Reader.ReadEndDocument()

        FSharpValue.MakeUnion(union, items) :?> 't

    override this.Serialize(context, args, value) =
        let case, fields = FSharpValue.GetUnionFields(value, typeof<'t>)

        context.Writer.WriteStartDocument()
        context.Writer.WriteName(caseFieldName)
        context.Writer.WriteString(case.Name)
        context.Writer.WriteStartArray(valueFieldName)

        fields
        |> Seq.zip(case.GetFields())
        |> Seq.iter(fun (field, value) -> serBy context args field.PropertyType value)

        context.Writer.WriteEndArray()
        context.Writer.WriteEndDocument()

type OptionSerializer<'a when 'a: equality>() =
    inherit SerializerBase<'a option>()

    let cases = GetUnionCases typeof<'a option>

    override this.Serialize(context, _args, value) =
        match value with
        | None -> BsonSerializer.Serialize(context.Writer, null)
        | Some x -> BsonSerializer.Serialize(context.Writer, x)

    override this.Deserialize(context, _args) =
        let genericTypeArgument = typeof<'a>

        let (case, args) =
                let value = if (genericTypeArgument.GetTypeInfo().IsPrimitive) then
                                BsonSerializer.Deserialize(context.Reader, typeof<obj>)
                            else
                                BsonSerializer.Deserialize(context.Reader, genericTypeArgument)
                match value with
                | null -> (cases.["None"], [||])
                | _ -> (cases.["Some"], [| value |])
        FSharpValue.MakeUnion(case, args) :?> 'a option

type ListSerializer<'a>() =
    inherit SerializerBase<'a list>()
    let itemSerializer = lazy (BsonSerializer.SerializerRegistry.GetSerializer<'a>())

    override this.Serialize(context, args, value) =
        serializeSeq context itemSerializer.Value value

    override this.Deserialize(context, args) =
        deserializeSeq context itemSerializer.Value |> List.ofSeq

type MapSerializer<'k, 'v when 'k: comparison>() =
    inherit SerializerBase<Map<'k, 'v>>()
    let keyType = typeof<'k>
    let representation =
        if keyType = typeof<string>
        then DictionaryRepresentation.Document
        else DictionaryRepresentation.ArrayOfDocuments
    let serializer =
        DictionaryInterfaceImplementerSerializer<System.Collections.Generic.Dictionary<'k, 'v>>()
            .WithDictionaryRepresentation(representation)

    override this.Serialize(context, args, value) =
        let dictValue =
            value
            |> Map.toSeq<'k, 'v>
            |> dict
            |> System.Collections.Generic.Dictionary<'k, 'v>
        serializer.Serialize(context, args, dictValue)

    override this.Deserialize(context, args) =
        serializer.Deserialize(context, args)
        |> Seq.map (|KeyValue|)
        |> Map.ofSeq<'k,'v>

type SetSerializer<'a when 'a: comparison>() =
    inherit SerializerBase<'a Set>()

    let itemSerializer = lazy (BsonSerializer.SerializerRegistry.GetSerializer<'a>())
    override this.Serialize(context, args, value) =
        serializeSeq context itemSerializer.Value value

    override this.Deserialize(context, args) =
        deserializeSeq context itemSerializer.Value |> Set.ofSeq

type FSharpTypeSerializationProvider() =
    let createSerializer (t:Type) =
        Activator.CreateInstance(t) :?> IBsonSerializer
    interface IBsonSerializationProvider with
        member this.GetSerializer(t) =
            let ti = t.GetTypeInfo()
            if IsOption t then
                typedefof<OptionSerializer<_>>.MakeGenericType (ti.GetGenericArguments())
                |> createSerializer
            elif IsList t then
                typedefof<ListSerializer<_>>.MakeGenericType (ti.GetGenericArguments())
                |> createSerializer
            elif IsMap t then
                typedefof<MapSerializer<_,_>>.MakeGenericType(ti.GetGenericArguments())
                |> createSerializer
            elif IsSet t then
                typedefof<SetSerializer<_>>.MakeGenericType(ti.GetGenericArguments())
                |> createSerializer
            elif IsUnion t then
                typedefof<DiscriminatedUnionSerializer<_>>.MakeGenericType(t)
                |> createSerializer
            // elif IsRecord objType then
            //     typedefof<RecordSerializer<_>>.MakeGenericType(objType)
            //     |> createSerializer
            else
                null

/// Registers the bson serializers and conventions for F# data types
module FSharpSerializer =
    let mutable private isRegistered = false

    /// Registers the bson serializers and conventions for F# data types
    /// Call it before the code using implicit bson serializers
    ///
    /// ## Example
    ///
    ///     let data = MyComplexFSharDataType()
    ///     let bsonDocument = data |> toBson
    ///     let bsonArray = data |> toBsonArray
    ///
    let Register() =
        if not isRegistered then
            BsonSerializer.RegisterSerializationProvider(FSharpTypeSerializationProvider())
            let pack = ConventionPack()
            pack.Add(OptionConvention())
            pack.Add(RecordConvention())
            //pack.Add(DictionaryRepresentationConvention(DictionaryRepresentation.ArrayOfArrays))
            ConventionRegistry.Register("F# type conventions", pack, (fun _ -> true))
            isRegistered <- true
/// functions wrapping the C# extension methods
[<AutoOpen>]
module BsonExtensionMethods =

    /// Deserializes an object from a `BsonDocument`
    let fromBsonDoc (doc: BsonDocument) = BsonSerializer.Deserialize doc

    /// Serializes an object to a `BsonDocument`
    let toBsonDoc = MongoDB.Bson.BsonExtensionMethods.ToBsonDocument

    /// Deserializes an object from a BSON byte array
    let fromBsonBin (array: byte array) = BsonSerializer.Deserialize array

    /// Serializes an object to a BSON byte array
    let toBsonBin = MongoDB.Bson.BsonExtensionMethods.ToBson

    /// serializes an object to a JSON string
    let toJson a =
        let toJsonSettings = JsonWriterSettings(Indent = true, OutputMode = JsonOutputMode.Shell)
        MongoDB.Bson.BsonExtensionMethods.ToJson(a, toJsonSettings)


    /// Seserializes an object from a JSON string
    let fromJson (json: string) = BsonSerializer.Deserialize json

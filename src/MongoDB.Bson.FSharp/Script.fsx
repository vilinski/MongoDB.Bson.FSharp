// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.

#r "bin/Debug/netstandard2.0/MongoDB.Bson.FSharp.dll"
#r "../../packages/MongoDB.Bson/lib/net45/MongoDB.Bson.dll"

open System
open MongoDB.Bson.FSharp

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
let test() =
    FSharpSerializer.Register()
    me |> toBson |> fromBson

let ``(╯°□°)╯`` (a,b) = (b,a)

/// http://bsonspec.org/spec.html
module BsonSpec =
    type EName = CString
    and CString = byte array // and trailing 0x00

    type BBType =
        | TDouble = 0x01
        | TString = 0x02
        | TDocument = 0x03
        | TArray = 0x04
        | TBinary = 0x05
        // Undefined
        | TObjectID = 0x07
        | TBool = 0x08
        | TDateTime = 0x09
        | TNull = 0x0A
        | TRegex = 0x0B
        // DBPointer
        | TJS = 0x0D
        // Symbol
        | TJSWithScope = 0x0F
        | TInt32 = 0x10
        | TTimestamp = 0x11
        | TInt64 = 0x12
        | TDecimal128 = 0x13
        | TMin = 0xFF
        | TMax = 0x7F
    type BBSubType =
        | BSGenericBin = 0x00
        | BSFunction = 0x01
        | BSUUID = 0x04
        | BSMD5 = 0x05
        | BSUserDefined = 0x80 // 0x80 - 0xFF
    type BBBin =
        struct
            val length: int32
            val bin: byte array
        end
    type BBElement =
        struct
            val name: string
            val value: BValue
        end

    and BBDocument =
        struct
            val length: int32
            val elements: BBElement list
        end

    and BinaryJsWithScope =
        struct
          val length: int32
          val js: string
          val scope: BBDocument
        end


    and BValue =
        | BDouble of double
        | BString of string
        | BDocument of BBDocument
        /// ['red', 'blue'] === {'0': 'red', '1': 'blue'}
        | BArray of BBDocument
        | BBinary of BBBin
        | BObjectID of byte array
        | BBool of bool
        | BDateTime of DateTime
        | BNull
        /// options chars are sorted "imlsu"
        | BRegex of pattern: string * options: string
        | BJS of string
        | BJsWithScope of BinaryJsWithScope
        | BInt32 of int32
        /// MongoDB internal timestamp for replication and sharding
        | BTimeStamp of increment: int * timestamp: int
        | BInt64 of int64
        | BDecimal of decimal
        | BMin | BMax

    // let write buffer element =
    //     let (name,value) = element
    //     match value with
    //     | BDouble d ->
    //         buffer.WriteTypeName TDouble name
    //         buffer.WriteDouble d

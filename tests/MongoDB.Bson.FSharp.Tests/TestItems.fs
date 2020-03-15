module TestItems

type TestClass() =
    member val AutoProperty  = "" with get, set

    // implement equality for test comparison
    override __.Equals(yobj) =
        match yobj with
        | :? TestClass as y -> y.AutoProperty = __.AutoProperty
        | _ -> false
    override __.GetHashCode() =
        __.AutoProperty.GetHashCode()
    interface System.IEquatable<TestClass> with
        member __.Equals other =
            other.AutoProperty = __.AutoProperty

type TestRecord =
    {
        Foo : string
    }
type SingleDU = SingleDU
type SingleDuInt = SingleDuInt of int
type SingleDuRecord = SingleDuRecord of TestRecord

type TestResult<'a> =
    | Failure
    | Result of cause: string * nr : 'a
    | NoResults of string

type TestItem =
    {
        Id : int
        Name : string
        Salary : decimal
        Array : int array
        RecursiveOpt : TestItem option
        Record : TestRecord
        Class : TestClass
        SingleDU : SingleDU
        SingleDuInt : SingleDuInt
        SingleDuRecord : SingleDuRecord
        Union : TestResult<int>
        UnionRecord : TestResult<TestRecord>
        MapStringInt : Map<string,int>
        MapIntString : Map<int,string>
        SetInt: int Set
        SetOptionInt: int option Set
    }
type TestItems =
    {
        testItems: TestItem list
    }

let testItem =
    {
        Id = 12345
        Name = "Adron"
        Salary = 13000m
        Array = [|10;20;30|]
        RecursiveOpt = None
        Record = { Foo = "test record" }
        Class = TestClass(AutoProperty = "test class")
        SingleDU = SingleDU
        SingleDuInt = SingleDuInt 42
        SingleDuRecord = SingleDuRecord { Foo = "foo" }
        Union = Result ("just", 5)
        UnionRecord = Result ("bar", { Foo = "foo" })
        MapStringInt = [("one", 1);("two", 2)] |> Map.ofList
        MapIntString = [(5, "five"); (1, "one")] |> Map.ofList
        SetInt = Set.empty
        SetOptionInt = Set.empty
    }

let testItems =
    let parent = testItem
    {
        testItems =
            [
                parent
                {
                    Id = 1234567890
                    Name = "Collider"
                    Salary = 26000.99m
                    Array = [||]
                    RecursiveOpt = Some parent
                    Record = { Foo = "bee" }
                    Class = TestClass(AutoProperty = "zzz")
                    SingleDU = SingleDU
                    SingleDuInt = SingleDuInt 44
                    SingleDuRecord = SingleDuRecord { Foo = "foo" }
                    Union = NoResults "just because"
                    UnionRecord = NoResults "already tested"
                    MapIntString = Map.empty
                    MapStringInt = Map.empty
                    SetInt = [9;8;7] |> Set.ofList
                    SetOptionInt = [Some 7; None; Some 8; None; Some 7] |> Set.ofList // deduplicates values
                }
            ]
    }


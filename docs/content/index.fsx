(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/MongoDB.Bson.FSharp"

(**
MongoDB.Bson.FSharp
======================

Documentation

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The MongoDB.Bson.FSharp library can be <a href="https://nuget.org/packages/MongoDB.Bson.FSharp">installed from NuGet</a>:
      <pre>PM> Install-Package MongoDB.Bson.FSharp</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

This example demonstrates using a function defined in this sample library.

*)
#r "MongoDB.Bson.dll"
#r "MongoDB.Bson.FSharp.dll"

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

do FSharpSerializer.Register()
let test() =
    let canBson = me |> toBson |> fromBson = me
    let canJson = me |> toJson |> fromJson = me
    canBson && canJson


(**

Samples & documentation
-----------------------

The library comes with comprehensible documentation.
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content].
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork
the project and submit pull requests. If you're adding a new public API, please also
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more information see the
[License file][license] in the GitHub repository.

  [content]: https://github.com/fsprojects/MongoDB.Bson.FSharp/tree/master/docs/content
  [gh]: https://github.com/fsprojects/MongoDB.Bson.FSharp
  [issues]: https://github.com/fsprojects/MongoDB.Bson.FSharp/issues
  [readme]: https://github.com/fsprojects/MongoDB.Bson.FSharp/blob/master/README.md
  [license]: https://github.com/fsprojects/MongoDB.Bson.FSharp/blob/master/LICENSE.txt
*)

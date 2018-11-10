module WilsonData.Test

open Xunit
open Newtonsoft.Json

let eq(v1,v2) =
  Assert.Equal(JsonConvert.SerializeObject v1, JsonConvert.SerializeObject v2)
[<Fact>]
let CanAccessCosmosFromDevMachine() =
  DataAccess.save false "max" "test" "testtuple" (123,"hello")
  eq(Some(123,"hello"), DataAccess.load "max" "test" "testtuple")

[<Fact>]
let CanCreateAndDelete() =
  let id = System.Guid.NewGuid().ToString()
  try
    DataAccess.save false id id id id
    eq(Some id, DataAccess.load id id id)
  finally
    eq(Some id, DataAccess.delete id id id)
    eq(None, DataAccess.load id id id)
  
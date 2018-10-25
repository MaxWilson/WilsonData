module WilsonData.Test

open Xunit
open Newtonsoft.Json

let eq(v1,v2) =
  Assert.Equal(JsonConvert.SerializeObject v1, JsonConvert.SerializeObject v2)
[<Fact>]
let CanAccessCosmosFromDevMachine() =
  DataAccess.save "max" "test" "testtuple" (123,"hello")
  eq(Some(123,"hello"), DataAccess.load "max" "test" "testtuple")
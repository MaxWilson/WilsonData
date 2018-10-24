namespace WilsonData

module API =
    open Microsoft.Extensions.Logging
    open Microsoft.Azure.WebJobs
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Mvc
    open Microsoft.Azure.WebJobs.Extensions.Http
    open Newtonsoft.Json
    open System.IO

    let ofReq<'t when 't: equality>(req: HttpRequest) =
      try
        if req = null || req.Body = null then Error "There is no body"
        else
          use body = new StreamReader(req.Body)
          let x = JsonConvert.DeserializeObject<'t>(body.ReadToEnd())
          if x = Unchecked.defaultof<'t> then
            None |> Result.Ok
          else
            x |> Some |> Result.Ok
      with
      exn -> Result.Error (exn.ToString())

    type DynamicStorageRow =
      {
      owner: string
      key: string
      ``type``: string
      value: obj
      }

    type RequestRow =
      {
      key: string
      ``type``: string
      value: obj
      }

    let mutable store = Map.empty

    // Necessary for some reason--without this nothing will work
    [<FunctionName("Heartbeat")>]
    let run([<HttpTrigger>] req: HttpRequest, log: ILogger) =
      ContentResult(Content="I am alive", ContentType="text")

    [<FunctionName("List")>]
    let list([<HttpTrigger>] req: HttpRequest, log: ILogger) =
      log.LogInformation("Executing")
      JsonResult(store)

    [<FunctionName("Save")>]
    let save([<HttpTrigger>] req: HttpRequest, log: ILogger) : ActionResult =
      try
        log.LogInformation("Executing")
        let x = req |> ofReq<RequestRow>
        match x with
        | Error err ->
          log.LogError (sprintf "Error in save(): '%A'" err)
          upcast (JsonResult err)
        | Ok None ->
          upcast (JsonResult "There is no body")
        | Ok (Some { key = key; value = v; ``type`` = t }) ->
          store <- (store |> Map.add key ({ DynamicStorageRow.value = box v; key = key; owner = "Unknown"; ``type`` = t }))
          upcast (store.[key] |> JsonResult)
      with
      exn ->
        log.LogError (sprintf "Unhandled exception in save(): '%A'" exn)
        upcast (StatusCodeResult(500))

    [<FunctionName("Load")>]
    let load([<HttpTrigger>] req: HttpRequest, log: ILogger) =
      log.LogInformation("Executing")
      JsonResult((28, 42, 16, "load"))


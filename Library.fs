namespace WilsonData

module API =
    open Microsoft.Extensions.Logging
    open Microsoft.Azure.WebJobs
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Mvc
    open Microsoft.Azure.WebJobs.Extensions.Http
    open Newtonsoft.Json
    open System.IO
    open System
    open System.Threading
    open System.Security.Claims

    let ofReq<'t when 't: equality and 't :> obj>(req: HttpRequest) =
      try
        use body = new StreamReader(req.Body)
        match JsonConvert.DeserializeObject<'t>(body.ReadToEnd()) with
          | v when v = Unchecked.defaultof<_> -> None
          | v -> Some v
        |> Result.Ok
      with
        exn -> Result.Error (exn.ToString())

    type DynamicStorageRow =
      {
      owner: string
      key: string
      ``type``: string
      value: obj
      }

    let mutable store = Map.empty

    let ident (req: HttpRequest) =
      let id =
        match req.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL-ID") with
        | true, v ->
          v.[0]
        | _ -> "Unknown"
      let name =
        match req.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL-NAME") with
        | true, v ->
          v.[0]
        | _ -> "Unknown"
      sprintf "%s\\%s" id name

    let myStores req =
      let me = ident req
      store |> Map.filter (fun _ (v: DynamicStorageRow) -> v.owner = me)

    [<FunctionName("List")>]
    let list([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route="List/{type}")>] req: HttpRequest, ``type``:string, log: ILogger) =
      let t = ``type``
      log.LogInformation(sprintf "Listing '%s'" t)
      JsonResult(myStores req |> Seq.map (function KeyValue(_, row) -> row.value))

    [<FunctionName("Save")>]
    let save([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route="Save/{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) : ActionResult =
      try
        let x = req |> ofReq<obj>
        let t = ``type``
        log.LogInformation(sprintf "Saving '%s' '%s': '%A'" t name x)
        match x with
        | Error err ->
          log.LogError (sprintf "Error in save(): '%A'" err)
          upcast ContentResult(Content=err, ContentType="text", StatusCode = Nullable 500)
        | Ok None ->
          upcast (ContentResult(Content="Must supply payload in request body", ContentType="text", StatusCode = Nullable 400))
        | Ok (Some v) ->
          let key = name
          let v = ({ DynamicStorageRow.value = box v; key = key; owner = ident req; ``type`` = t })
          store <- (store |> Map.add key v)
          upcast JsonResult(v.value)
      with
      exn ->
        log.LogError (sprintf "Unhandled exception in save(): '%A'" exn)
        upcast StatusCodeResult(500)

    [<FunctionName("Load")>]
    let load([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Load/{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) : ActionResult =
      let t = ``type``
      log.LogInformation(sprintf "Loading '%s' '%s'" t name)
      match (myStores req).TryGetValue(name) with
      | true, v ->
        log.LogInformation(sprintf "Loaded '%s' '%s': '%A'" t name v)
        upcast JsonResult(v.value)
      | _ ->
        upcast StatusCodeResult(404)


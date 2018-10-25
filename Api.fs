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
    open DataAccess

    let ofReq<'t when 't: equality and 't :> obj>(req: HttpRequest) =
      try
        use body = new StreamReader(req.Body)
        match JsonConvert.DeserializeObject<'t>(body.ReadToEnd()) with
          | v when v = Unchecked.defaultof<_> -> None
          | v -> Some v
        |> Result.Ok
      with
        exn -> Result.Error (exn.ToString())

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

    [<FunctionName("List")>]
    let list([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route="List/{type}")>] req: HttpRequest, ``type``:string, log: ILogger) =
      try
        let t = ``type``
        log.LogInformation(sprintf "Listing '%s'" t)
        JsonResult(DataAccess.list (ident req) t)
      with e ->
        JsonResult(e.ToString())

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
          DataAccess.save (ident req) t key v
          upcast JsonResult((DataAccess.load (ident req) t key).Value)
      with
      exn ->
        log.LogError (sprintf "Unhandled exception in save(): '%A'" exn)
        upcast StatusCodeResult(500)

    [<FunctionName("Load")>]
    let load([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Load/{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) : ActionResult =
      let t = ``type``
      log.LogInformation(sprintf "Loading '%s' '%s'" t name)
      match DataAccess.load (ident req) t name with
      | Some v ->
        log.LogInformation(sprintf "Loaded '%s' '%s': '%A'" t name v)
        upcast JsonResult(v)
      | _ ->
        upcast StatusCodeResult(404)


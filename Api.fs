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
    open System.Net.Http

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
      sprintf "%s.%s" id name

    let toResponse (action: IActionResult) =
      { new IActionResult with
          member this.ExecuteResultAsync (ctx: ActionContext) =
            async {
              do! (action.ExecuteResultAsync(ctx)) |> Async.AwaitTask
              let respH = ctx.HttpContext.Response.Headers
              match ctx.HttpContext.Request.Headers.TryGetValue("Origin") with
              | true, origin ->
                respH.Add("Access-Control-Allow-Credentials", Microsoft.Extensions.Primitives.StringValues("true"))
                respH.Add("Access-Control-Allow-Origin", origin)
                respH.Add("Access-Control-Allow-Methods", Microsoft.Extensions.Primitives.StringValues "GET, OPTIONS")
              | _ ->
                let origin = Microsoft.Extensions.Primitives.StringValues "http://localhost:8080"
                respH.Add("Access-Control-Allow-Credentials", Microsoft.Extensions.Primitives.StringValues("true"))
                respH.Add("Access-Control-Allow-Origin", origin)
                respH.Add("Access-Control-Allow-Methods", Microsoft.Extensions.Primitives.StringValues "GET, OPTIONS")
            } |> Async.StartAsTask :> System.Threading.Tasks.Task
        }

    [<FunctionName("List")>]
    let list([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route="List/{type}")>] req: HttpRequest, ``type``:string, log: ILogger) =
      try
        let t = ``type``
        log.LogInformation(sprintf "Listing '%s'" t)
        JsonResult(DataAccess.list (ident req) t) |> toResponse
      with e ->
        JsonResult(e.ToString())
      |> toResponse

    [<FunctionName("Save")>]
    let save([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route="Save/{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) =
      try
        let x = req |> ofReq<obj>
        let t = ``type``
        log.LogInformation(sprintf "Saving '%s' '%s': '%A'" t name x)
        match x with
        | Error err ->
          log.LogError (sprintf "Error in save(): '%A'" err)
          ContentResult(Content=err, ContentType="text", StatusCode = Nullable 500) |> toResponse
        | Ok None ->
          (ContentResult(Content="Must supply payload in request body", ContentType="text", StatusCode = Nullable 400)) |> toResponse
        | Ok (Some v) ->
          let key = name
          DataAccess.save (ident req) t key v
          JsonResult((DataAccess.load (ident req) t key).Value) |> toResponse
      with
      exn ->
        log.LogError (sprintf "Unhandled exception in save(): '%A'" exn)
        StatusCodeResult(500) |> toResponse

    [<FunctionName("Load")>]
    let load([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Load/{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) =
      let t = ``type``
      log.LogInformation(sprintf "Loading '%s' '%s'" t name)
      match DataAccess.load (ident req) t name with
      | Some v ->
        log.LogInformation(sprintf "Loaded '%s' '%s': '%A'" t name v)
        JsonResult(v) |> toResponse
      | _ ->
        StatusCodeResult(404) |> toResponse


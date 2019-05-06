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
        if req.HttpContext.User.Identity.IsAuthenticated |> not then
            "Anonymous"
        else
            let claim typ = req.HttpContext.User.Claims |> Seq.tryFind (fun c -> c.Type = typ) |> Option.map (fun c -> c.Value)
            let addDefault = Option.defaultValue ("Missing") // missing shouldn't happen
            let provider = claim "http://schemas.microsoft.com/identity/claims/identityprovider" |> addDefault
            let stableSid = claim "stable_sid" |> Option.defaultValue (claim ClaimTypes.NameIdentifier |> addDefault)
            sprintf "%s.%s" provider stableSid

    [<FunctionName("LoadAll")>]
    let loadAll([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route="{type}")>] req: HttpRequest, ``type``:string, log: ILogger) =
      try
        let t = ``type``
        log.LogInformation(sprintf "Listing '%s'" t)
        JsonResult(DataAccess.loadAll (ident req) t)
      with e ->
        JsonResult(e.ToString())

    // deprecated: V1
    [<FunctionName("List")>]
    let listV1([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route="List/{type}")>] req: HttpRequest, ``type``:string, log: ILogger) =
      loadAll(req, ``type``, log)

    let saveBase([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route="{type}/{name}")>] publish: bool, req: HttpRequest, ``type``: string, name: string, log: ILogger) : ActionResult =
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
          DataAccess.save publish (ident req) t key v
          upcast JsonResult((DataAccess.load (ident req) t key).Value)
      with
      exn ->
        log.LogError (sprintf "Unhandled exception in save(): '%A'" exn)
        upcast StatusCodeResult(500)

    [<FunctionName("SavePrivate")>]
    let savePrivate([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route="{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) : ActionResult =
      saveBase(false, req, ``type``, name, log)

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

    [<FunctionName("Delete")>]
    let delete([<HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route="{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) : ActionResult =
      try
        let t = ``type``
        log.LogInformation(sprintf "Deleting '%s' '%s'" t name)
        let key = name
        match DataAccess.delete (ident req) t key with
        | Some deleted ->
          upcast JsonResult(deleted)
        | None ->
          upcast StatusCodeResult(404)
      with
      exn ->
        log.LogError (sprintf "Unhandled exception in save(): '%A'" exn)
        upcast StatusCodeResult(500)

    [<FunctionName("Save")>]
    let publish([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route="Save/{type}/{name}")>] req: HttpRequest, ``type``: string, name: string, log: ILogger) : ActionResult =
      saveBase(true, req, ``type``, name, log)

    // endpoint for debugging identity properties
    [<FunctionName("Ident")>]
    let identity([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Ident")>] req: HttpRequest) : ActionResult =
        upcast ({|  identity = ident req
                    headers = req.Headers |> Seq.map (fun h -> {| name = h.Key; value = h.Value |}) |> Array.ofSeq
                    claims =
                        match (req.HttpContext.User) with
                        | null -> [||]
                        | p ->
                            p.Claims |> Seq.map(fun c -> {| claimType = c.Type; value = c.Value |}) |> Array.ofSeq
                    |}
                |> JsonResult)

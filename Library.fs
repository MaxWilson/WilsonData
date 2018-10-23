namespace WilsonData

module API =
    open Microsoft.Extensions.Logging
    open Microsoft.Azure.WebJobs
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Mvc
    open Microsoft.Azure.WebJobs.Extensions.Http

    [<FunctionName("Heartbeat")>]
    let run([<HttpTrigger>]req: HttpRequest, log: ILogger) =
      ContentResult(Content="I am alive", ContentType="text")

    [<FunctionName("List")>]
    let list([<HttpTrigger(AuthorizationLevel.Anonymous)>] req: HttpRequest, log: ILogger) =
      JsonResult((28, 42, 16, "list"))

    [<FunctionName("Save")>]
    let save([<HttpTrigger(AuthorizationLevel.Anonymous)>] req: HttpRequest, log: ILogger) =
      JsonResult((28, 42, 16, "save"))

    [<FunctionName("Load")>]
    let load([<HttpTrigger(AuthorizationLevel.Anonymous)>] req: HttpRequest, log: ILogger) =
      JsonResult((28, 42, 16, "load"))


namespace Skyminator

open System
open System.Net
open System.Runtime.CompilerServices
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open FsToolkit.ErrorHandling
open Skyminator
open Skyminator.WebsocketMb

[<Extension>]
type WebsocketExtensions =
    [<Extension>]
    static member UseWebsocketHandler(app: IApplicationBuilder) =
        app.Use
            (fun (context: HttpContext) (next: Func<Task>) ->
                async {
                    if context.Request.Path = PathString("/ws") then
                        if context.WebSockets.IsWebSocketRequest then
                            let! webSocket =
                                context.WebSockets.AcceptWebSocketAsync()
                                |> Async.AwaitTask

                            let tcs = WebsocketMb.mailbox.PostAndReply (fun channel -> ParentMsg.Add (webSocket, channel))
                                
                            do! tcs.Task |> Async.AwaitTask
                        else
                            context.Response.StatusCode <- int HttpStatusCode.BadRequest
                    else
                        do! next.Invoke() |> Async.AwaitTask
                }
                |> Async.StartAsTask
                :> Task)

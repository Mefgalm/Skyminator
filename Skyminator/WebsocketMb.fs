module Skyminator.WebsocketMb

open System
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks

[<RequireQualifiedAccess>]
type ParentMsg =
    | Add of WebSocket * AsyncReplyChannel<TaskCompletionSource>
    | SendAll of string
    | RemoveClient of Guid

[<RequireQualifiedAccess>]
type ClientMsg =
    | Send of string
    | Close

type MyList<'a> =
    | Empty
    | MyList of Head: 'a * Body: MyList<'a> 

let private toBytes (s: string) = Encoding.UTF8.GetBytes s

let private sendAsync id (msg: string) (ws: WebSocket) (parent: MailboxProcessor<ParentMsg>) =
    async {
        let encoded = toBytes msg
        let buffer = ArraySegment<Byte>(encoded, 0, encoded.Length)
        try 
            do! ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask
        with
           e -> parent.Post (ParentMsg.RemoveClient id)
    }

let startClient id (ws: WebSocket) (parent: MailboxProcessor<ParentMsg>) ct = MailboxProcessor.Start(fun inbox ->
        let buffer: byte array = Array.zeroCreate (1024 * 4) 
        
        let rec reader () = async {
            let receiveTask = ws.ReceiveAsync(ArraySegment<byte>(buffer), ct)
            let timeoutTask = Task.Delay(10_000).ContinueWith(fun _ -> Unchecked.defaultof<WebSocketReceiveResult>)
            
            let! firstCompleted = Task.WhenAny(receiveTask, timeoutTask) |> Async.AwaitTask
            
            if firstCompleted = timeoutTask then
                parent.Post (ParentMsg.RemoveClient id)
            else
                try 
                    do! firstCompleted |> Async.AwaitTask |> Async.Ignore
                with
                    _ -> parent.Post (ParentMsg.RemoveClient id)
            do! Async.Sleep 1000
                
            return! reader ()
        }
        
        Async.Start(reader(), ct)
        
        let rec loop () = async {
            match! inbox.Receive() with
            | ClientMsg.Send msg ->
                do! sendAsync id msg ws parent
            | ClientMsg.Close ->
                do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) |> Async.AwaitTask
            return! loop ()
        }
        loop ()
    , ct)

let mailbox =
    MailboxProcessor.Start(fun inbox ->
        let clients = ResizeArray<(Guid * MailboxProcessor<ClientMsg> * CancellationTokenSource * TaskCompletionSource)>()
        let rec loop () = async {
                match! inbox.Receive() with
                | ParentMsg.Add (ws, channel) ->
                    let tcs = TaskCompletionSource()
                    let ct = new CancellationTokenSource()
                    let id = Guid.NewGuid()
                    let client = startClient id ws inbox ct.Token
                    
                    clients.Add(id, client, ct, tcs)
                    channel.Reply tcs
                | ParentMsg.SendAll msg ->
                     for (_, mb, _, _) in clients do
                         mb.Post (ClientMsg.Send msg)
                | ParentMsg.RemoveClient removeId ->
                    let index =
                        clients
                        |> Seq.findIndex (fun (id, _, _, _) -> id = removeId)
                    
                    let (_, mb, cr, tcs) = clients.[index]
                    mb.Post (ClientMsg.Close)
                    cr.Cancel()
                    tcs.SetResult()
                    clients.RemoveAt index
                return! loop ()
            }
        loop ())

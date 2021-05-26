namespace Bot.Twitch

open System
open System.IO
open System.Net.Sockets
open Bot.Twitch

module TwitchClient =
    [<RequireQualifiedAccess>]
    type TwitchRequest =
        | Send of string
        | SendIrc of string

    let private sendMessageIrc (outputStream: StreamWriter) (message: string) =
        async {
            do! outputStream.WriteLineAsync message
                |> Async.AwaitTask

            do! outputStream.FlushAsync() |> Async.AwaitTask
        }

    let create (ip: string) (port: int) (userName: string) (password: string) (channel: string) callback =
        let sendMessage outputStream message =
            sendMessageIrc
                outputStream
                $":{userName}!{userName}@{userName}.tmi.twitch.tv PRIVMSG #{channel} : {message}"

        let rec reader (clientMb: MailboxProcessor<TwitchRequest>) (inputStream: StreamReader) =
            async {
                let! msg = inputStream.ReadLineAsync() |> Async.AwaitTask
                do callback clientMb msg
                return! reader clientMb inputStream
            }

        let rec pinger (twitchClient: MailboxProcessor<TwitchRequest>) =
            async {
                twitchClient.Post (TwitchRequest.SendIrc $"PING {ip}")
                do! Async.Sleep (TimeSpan.FromSeconds 5.)
                return! pinger twitchClient
            }

        MailboxProcessor.Start(fun inbox ->
            async {
                use tcpClient = new TcpClient()
                tcpClient.Connect(ip, port)
                let inputStream = new StreamReader(tcpClient.GetStream())
                let outputStream = new StreamWriter(tcpClient.GetStream())

                outputStream.WriteLine($"PASS {password}")
                outputStream.WriteLine($"NICK {userName}")
                outputStream.WriteLine($"USER {userName} 8 * :{userName}")
                outputStream.WriteLine($"JOIN #{channel}")
                outputStream.Flush()
                
                do! Async.StartChild(reader inbox inputStream) |> Async.Ignore
                do! Async.StartChild(pinger inbox) |> Async.Ignore

                let rec loop () =
                    async {
                        match! inbox.Receive() with
                        | TwitchRequest.Send message ->
                            do! sendMessage outputStream message
                        | TwitchRequest.SendIrc message ->
                            do! sendMessageIrc outputStream message
                        
                        return! loop ()
                    }

                return! loop ()
            })




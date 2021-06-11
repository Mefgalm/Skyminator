namespace Skyminator

open System
open Battle.Player
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Bot.Commands.Game
open Bot.Config
open Bot.Twitch.TwitchClient
open Bot.Chat.Twitch
open Skyminator.WebsocketMb

//TODO !commands -> link (maybe just text for now)

type Startup(configuration: IConfiguration) =
    let gameUpdateSub (gameMb: MailboxProcessor<BattleCommand>) =
        let callback (battleResponse: BattleResponse) =
            WebsocketMb.mailbox.Post (ParentMsg.SendAll "")
            
        gameMb.Post (BattleCommand.RegisterCallback callback)
    
    let rec pinger () = async {
        WebsocketMb.mailbox.Post (ParentMsg.SendAll "Hello")
        do! Async.Sleep 5000
        return! pinger ()
    } 
    
    member _.Configuration = configuration
    
    member _.ConfigureServices(services: IServiceCollection) =
        let twitchConfig = configuration.GetSection("TwitchConfig").Get<TwitchConfig>();
        
        let intiCommands = [
            "гдевебка", "ща врублю!"
            "discord", "https://discord.gg/qcanUY7VHd"
            "bot", "https://github.com/Mefgalm/Skyminator"
        ]
        
        let gameMb = battleMb twitchConfig.ReviveAfterMins
        let twitchChat = run gameMb intiCommands twitchConfig
        
        do gameUpdateSub gameMb
        
        Async.Start (pinger())
        
        services.AddSingleton<MailboxProcessor<BattleCommand>>(gameMb) |> ignore
        services.AddSingleton<MailboxProcessor<TwitchRequest>>(twitchChat) |> ignore
        
        services.AddControllers() |> ignore

    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore
            
        let webSocketOptions =
             WebSocketOptions(
                KeepAliveInterval = TimeSpan.FromSeconds(120.))
             
//        webSocketOptions.AllowedOrigins.Add("https://client.com");
//        webSocketOptions.AllowedOrigins.Add("https://www.client.com");            
            
        app.UseHttpsRedirection()
           .UseStaticFiles()
           .UseWebSockets(webSocketOptions)
           .UseRouting()
           .UseWebsocketHandler()
           .UseAuthorization()
           .UseEndpoints(fun endpoints ->
                endpoints.MapControllers() |> ignore
            ) |> ignore

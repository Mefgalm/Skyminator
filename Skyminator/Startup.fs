namespace Skyminator

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Bot.Commands.Game
open Bot.Config
open Bot.Twitch.TwitchClient
open Bot.Chat.Twitch

//TODO !commands -> link (maybe just text for now)

type Startup(configuration: IConfiguration) =
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
        
        services.AddSingleton<MailboxProcessor<BattleCommand>>(gameMb) |> ignore
        services.AddSingleton<MailboxProcessor<TwitchRequest>>(twitchChat) |> ignore
        
        services.AddControllers() |> ignore

    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore
        app.UseHttpsRedirection()
           .UseRouting()
           .UseAuthorization()
           .UseEndpoints(fun endpoints ->
                endpoints.MapControllers() |> ignore
            ) |> ignore

module Bot.Chat.Twitch

open System
open System.Text.RegularExpressions
open Bot.Twitch.TwitchClient
open Common
open Bot
open Bot.Config
open Bot.Twitch
open Bot.Commands.Game
open Bot.Database
open Bot.Runner
open FsToolkit.ErrorHandling

let toLower (str: string) = str.ToLower() 

let parseMessage (str: string) =
    if isNull str then
        None
    else
        let m = Regex(":([\d\w]+)![\w\d@\.]+ PRIVMSG #[\d\w]+ :(.+)").Match(str)
    
        if m.Success
        then Some(toLower m.Groups.[1].Value, toLower m.Groups.[2].Value)
        else None
   
let createChatCommand (nick, (msg: string)) =
    if msg.StartsWith("!") then
        match msg with
        | RegEx "!руина" _ ->
            Some ChatCommand.Ruin        
        | RegEx "!run (.+)" [ code ] ->
            Some <| ChatCommand.Run code        
        | RegEx "!who +@([\w_]+)" [target] ->
            Some <| ChatCommand.Who (toLower target) 
        | RegEx "!hp +@([\w_]+)" [target] ->
            Some <| ChatCommand.Hp (toLower target)
        | RegEx "!who" _ ->
            Some <| ChatCommand.Who nick
        | RegEx "!hp" _ ->
            Some <| ChatCommand.Hp nick
        | RegEx "!hug +@([\w_]+)" [ target ] ->
            Some <| ChatCommand.Hug (nick, target)
        | RegEx "!add-([a-zA-ZА-Яа-я]+) +(.+)" [command; content] ->
            Some <| ChatCommand.AddDynamicCommand (nick, command, content)
        | RegEx "!remove-([a-zA-ZА-Яа-я]+)" [command] ->
            Some <| ChatCommand.RemoveDynamicCommand (nick, command)        
        | RegEx "!battle (\d+)" [ (Integer mins) ] ->
            Some <| ChatCommand.StartBattle (nick, mins)
        | RegEx "!(\d+)d(\d+) *\+ *(\d+)?" [ (Integer count); (Integer power); (Integer plus) ] ->
            Some <| ChatCommand.DiceRollPlus(count, power, plus)
        | RegEx "!(\d+)d(\d+)" [ (Integer count); (Integer power) ] ->
            Some <| ChatCommand.DiceRoll(count, power)
        | RegEx "!join +(\w+)" [ playerClass ] ->
            Some <| ChatCommand.JoinBattle (nick, playerClass)
        | RegEx "!rules" [] ->
            Some <| ChatCommand.PlayersActions nick
        | RegEx "!(\w+) +@([\w_]+)" [ spellName; targetNick ] ->
            Some <| ChatCommand.Cast(nick, toLower targetNick, toLower spellName)
        | RegEx "!([a-zA-ZА-Яа-я]+)" [command] ->
            Some <| ChatCommand.GetDynamicCommandContext command
        | _ -> None
    else
        None

let clientCallback (gameMb: MailboxProcessor<BattleCommand>) (clientMb: MailboxProcessor<TwitchRequest>) msg =
    option {
        let! parsedMessage = parseMessage msg
        let! chatCommand = createChatCommand parsedMessage
        let! response = runCommand gameMb chatCommand
        
        Views.runnerResponseToString response
        |> TwitchRequest.Send
        |> clientMb.Post
    }
    |> ignore
    
let private initDb () =
    try 
        RuinDb.insertRuinModel { Id = 1; Count = 0 }
    with _ -> ()

let addDynamicCommand gameMb owner command content =
    runCommand gameMb (ChatCommand.AddDynamicCommand (owner, command, content)) |> ignore

let run (gameMb: MailboxProcessor<BattleCommand>) initCommands config =
    initDb()
    
    let twitchClient = create "irc.twitch.tv" 6667 "botomef" config.OAuth "mefgalm" (clientCallback gameMb)
    
    let twitchClientCallback response =
        twitchClient.Post (TwitchRequest.Send <| Views.responseToString response DateTime.UtcNow)
    
    gameMb.Post (BattleCommand.RegisterCallback twitchClientCallback)
    
    initCommands
    |> List.iter (fun (command, content) -> addDynamicCommand gameMb config.Owner command content)
    
    twitchClient
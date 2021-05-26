module Bot.Runner

open Battle.Player
open Bot
open Bot.Commands
open Bot.Commands.Game
open Bot.Database
open Bot.Commands.DynamicCommand
    
let shouldBeOwner nick = "mefgalm" = nick 
    
[<RequireQualifiedAccess>]
type RunnerResponse =
    | Ruin of int
    | Discord of string
    | Run of string
    | DiceRoll of int list * int option * int
    | DynamicCommandContent of string
    | Hug of string * string
    
let runCommand (battleMb: MailboxProcessor<BattleCommand>) chatCommand =
     match chatCommand with
     | ChatCommand.Ruin ->
         let ruinModel = RuinDb.getRuinModel()
         let newRuinModel = Ruin.ruin ruinModel
         RuinDb.updateRuinModel newRuinModel
         Some <| RunnerResponse.Ruin newRuinModel.Count
     | ChatCommand.Run code ->
         Some <| RunnerResponse.Run (CSharpRun.runCSharp code)
     | ChatCommand.DiceRoll (count, power) ->
         Dice.dice count power None
         |> Option.map (Some << RunnerResponse.DiceRoll)
         |> Option.defaultValue None
     | ChatCommand.DiceRollPlus (count, power, plus) ->
         Dice.dice count power (Some plus)
         |> Option.map (Some << RunnerResponse.DiceRoll)
         |> Option.defaultValue None
     | ChatCommand.StartBattle (self, mins) ->
         if shouldBeOwner self then
             battleMb.Post <| Init mins
         None
     | ChatCommand.Hug (self, target) ->
         Some <| RunnerResponse.Hug (self, target)
     | ChatCommand.GameOver self ->
         if shouldBeOwner self then
             battleMb.Post <| GameOver
         None             
     | ChatCommand.JoinBattle (self, playerClassStr) ->
         battleMb.Post <| Join (self, playerClassStr)
         None
     | ChatCommand.Cast (self, targetNick, spell) ->
         battleMb.Post <| Action (self, targetNick, spell)
         None
     | ChatCommand.Who target ->
         battleMb.Post <| Who target
         None
     | ChatCommand.Hp target ->
         battleMb.Post <| Hp target
         None
     | ChatCommand.PlayersActions self ->
         battleMb.Post <| PlayerActions self
         None
     | ChatCommand.AddDynamicCommand (self, command, content) ->
         if shouldBeOwner self then
             let dynamicCommand = { Id = command; Content = content }
             DynamicCommandDb.upsert dynamicCommand
         None
     | ChatCommand.RemoveDynamicCommand (self, command) ->
         if shouldBeOwner self then
             DynamicCommandDb.remove command
         None
     | ChatCommand.GetDynamicCommandContext command ->
          match DynamicCommandDb.tryGet command with
          | Some command -> Some <| RunnerResponse.DynamicCommandContent command.Content
          | None -> None
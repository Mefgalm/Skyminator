module Bot.Twitch.Views

open System
open Battle.Player
open Bot.Runner

let private effectView effect =
    match effect with
    | DamageTaken damage ->
        $"damage {damage}"
    | HealTaken heal ->
        $"heal {heal}"
    | StatusApplied (status, untilTimeSpan) ->
        $"status {status} for {int untilTimeSpan.TotalSeconds}"
    | AddGCD gcd ->
        $"gcd for: {gcd}"
        
let private effectInfoView effectInfo =
    $"{effectInfo.TargetId} => {effectView effectInfo.Effect}"
    
let private statusInfoView now (statusInfo: StatusInfo)  =
    $"status: {statusInfo.Status}, until: {int (now - statusInfo.Until).TotalSeconds}" 
    
let private playerView now player =
    let str = player.StatusInfos |> List.map (statusInfoView now) |> String.concat ", "
    $"Nick: {player.Id}, class: {player.Class}, hp: {player.Hp}, statuses: [{str}]"
    
let private spellView =
    function
    | WizardSpell Fireball ->
        "'fireball' 1d10"
    | WizardSpell Sheep ->
        "'sheep'(damage cancel this effect) for 5 mins. "
    | WarriorSpell Attack ->
        "'attack' 2d6"
    | WarriorSpell StunAttack ->
        "'stun' for 1 min"
    | HealerSpell Smite ->
        "'smite' 1d6"
    | HealerSpell Heal ->
        "'heal' 1d6"
    
let private battleErrorToString (now: DateTime) = function
    | BattleError.AlreadyJoined -> "Already joined"
    | Dead (nick, deathTime) ->
        $"{nick}, you are dead, seconds left {int (now - deathTime).TotalSeconds}"
    | InvalidSpellForClass -> "Invalid spell"
    | UnableToAction statusInfos ->
        let statusInfoStr = statusInfos |> List.map(fun si -> $"({statusInfoView now si})") |> String.concat ", "
        $"Unable to action: {statusInfoStr}"
    | PlayerNotFound nick -> $"{nick} not in the battle"
    | SpellNotFound spell -> $"{spell} not found"
    | ClassNotFound classStr -> $"{classStr} not found"
    | OnGCD (nick, secondsLeft) -> $"{nick} you have GCD, seconds left {secondsLeft}"
    
let responseToString (response: BattleResponse) (now: DateTime) =
    match response with
    | BattleResponse.Joined player ->
        playerView now player 
    | BattleResponse.EffectInfo effectInfos ->
        effectInfos |> List.map effectInfoView |> String.concat ", "
    | BattleResponse.Who player ->
        playerView now player 
    | BattleResponse.Hp (nick, hp) ->
        $"{nick} hp: {hp}"
    | BattleResponse.GameOver ->
        "Game over"
    | BattleResponse.BattleBegins mins ->
        $"Let's the battle begins for {mins} mins"
    | BattleResponse.PlayerActions spells ->
        let spellsStr = spells |> List.map spellView |> String.concat " | "
        $"Sample: !attack @{{nick}}. Your actions: {spellsStr}"
    | BattleResponse.BattleError error ->
        battleErrorToString now error
    
let private diceRollsView rolls plusOpt result =
    let diceThrowsStr =
        rolls
        |> List.map (fun x -> x.ToString())
        |> String.concat " + "

    match plusOpt with
    | Some plus -> $"{diceThrowsStr} (+ {plus}) = {result + plus}"
    | None -> $"{diceThrowsStr} = {result}"
    
let runnerResponseToString (runnerResponse: RunnerResponse) =
    match runnerResponse with
    | RunnerResponse.Ruin count -> $"Всего руин {count}"
    | RunnerResponse.Discord discord -> discord
    | RunnerResponse.Run code -> code
    | RunnerResponse.Hug (self, target) -> $"{self} обнимает {target}"
    | RunnerResponse.DiceRoll (rolls, plusOpt, result) -> diceRollsView rolls plusOpt result
    | RunnerResponse.DynamicCommandContent content -> content

﻿module Battle.Battle

open System
open Battle.Player
open FsToolkit.ErrorHandling
open Common

type Battle =
    { Players: Player list
      EndDateTime: DateTime }
    
let private gameOverAsync callback (sleep: int) =
    async {
        do! Async.Sleep sleep
        callback BattleResponse.GameOver
    }

let init endAfterMins callback (now: DateTime) =
    let sleepMiilis = (int) (endAfterMins * 60. * 1000.)
    
    Some (BattleResponse.BattleBegins (int endAfterMins)),    
    { Players = []
      EndDateTime = now.AddMinutes(endAfterMins) },
    gameOverAsync callback sleepMiilis
   
let private checkAlreadyInBattle nick battle =
    if battle.Players |> List.exists(fun x -> x.Id = nick) then
        Error AlreadyJoined
    else
        Ok ()
    
let getPlayerClass =
    function
    | "wiz" -> Ok <| PlayerClass.Wizard
    | "war" -> Ok <| PlayerClass.Warrior
    | "heal" -> Ok <| PlayerClass.Healer
    | x -> Error <| BattleError.ClassNotFound x 
    
let join nick playerClassStr battle = result {
    do! checkAlreadyInBattle nick battle
    let! playerClass = getPlayerClass playerClassStr
    let player = create nick playerClass
    let newBattle = { battle with Players = player:: battle.Players }
    return (Some <| BattleResponse.Joined player, newBattle)
}
        
let private checkPlayer nick battle =
    match battle.Players |> List.tryFind (fun p -> p.Id = nick) with
    | Some player -> Ok player
    | None -> Error <| PlayerNotFound nick
    
let private cancelExpiredStatusesForAllPlayers now battle =
    let cancelExpiredStatusesForPlayer now player =
        let activeStatuses = player.StatusInfos |> List.remove (fun s -> now >= s.Until)
        { player with StatusInfos = activeStatuses }
    
    let activePlayers = battle.Players |> List.map (cancelExpiredStatusesForPlayer now)
    { battle with Players = activePlayers }
    
let private revivePlayers reviveAfterMins now battle =
    let updatePlayer now (player: Player) =
        player.DeathTime
        |> Option.map (fun deathTime ->
            if deathTime.AddMinutes reviveAfterMins < now then
                { player with DeathTime = None
                              Hp = generateHp () } 
            else player)
        |> Option.defaultValue player
    
    { battle with Players = battle.Players |> List.map (updatePlayer now) }
    
let private resetGCD now battle =
    let resetGCD now player =
        let gcd =
            player.GCD
            |> Option.bind(fun gcd ->
                if now > gcd then
                    None
                else
                    Some gcd)
        { player with GCD = gcd }
    
    { battle with Players = battle.Players |> List.map (resetGCD now) }
    
let private preBattleChecks reviveAfterMins now =
    cancelExpiredStatusesForAllPlayers now
    >> revivePlayers reviveAfterMins now
    >> resetGCD now
      
      
let private applyEffectInfo effectInfos battle now =
    let newPlayers =
        List.fold (fun players effectInfo ->
            List.map (fun player ->
                if player.Id = effectInfo.TargetId then
                    applyEffect now player effectInfo.Effect
                else
                    player)
                players)
            battle.Players
            effectInfos
    
    { battle with Players = newPlayers }
    
let private filterEffectsToResponse effectInfos =
    List.remove (fun effectInfo ->
        match effectInfo.Effect with
        | AddGCD _ -> true
        | _ -> false)
        effectInfos
    
let action playerNick targetNick spell reviveAfterMins now battle = result {
    let preBattle = preBattleChecks reviveAfterMins now battle
    
    let! player = preBattle |> checkPlayer playerNick
    let! target = preBattle |> checkPlayer targetNick
    
    let! effectInfos = invoke player spell target now
    let postBattle = applyEffectInfo effectInfos preBattle now
    
    return (Some <| BattleResponse.EffectInfo (filterEffectsToResponse effectInfos), postBattle)
}

let who playerNick battle = result {
    let! player = battle |> checkPlayer playerNick
    return (Some <| BattleResponse.Who player, battle)
}

let playerActions playerNick battle = result {
    let! player = battle |> checkPlayer playerNick
    
    let spells =
        match player.Class with
        | Warrior -> [WarriorSpell Attack; WarriorSpell StunAttack]
        | Wizard -> [WizardSpell Fireball; WizardSpell Sheep]
        | Healer -> [HealerSpell Smite; HealerSpell Heal]
    
    return (Some <| BattleResponse.PlayerActions spells, battle)
}

let myHp playerNick battle = result {
    let! player = battle |> checkPlayer playerNick
    return (Some <| BattleResponse.Hp (playerNick, player.Hp), battle)
}

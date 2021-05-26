module Bot.Commands.Dice

open FsToolkit.ErrorHandling
open Bot

let validateCount value =
    if value > 0 && value <= 100 then Some value else None

let validatePower value =
    if value > 0 && value <= 100 then Some value else None

let dice countInt powerInt plusOpt =
    option {
        let! count = validateCount countInt
        let! power = validateCount powerInt
        
        let diceThrows = Domain.Dice.diceThrows count power
        let result = List.sum diceThrows 

        return diceThrows, plusOpt, result
    }
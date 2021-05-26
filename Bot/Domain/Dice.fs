module Bot.Domain.Dice

open System
open FsToolkit.ErrorHandling
open Bot.Rand

let diceThrows count power =
    List.init count (fun _ -> range power)
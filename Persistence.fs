module ScreenPal.Persistence

open System
open System.IO
open System.Text.Json
open ScreenPal.Domain

[<CLIMutable>]
type Persisted = {
    Name: string
    Hunger: int
    Energy: int
    Happiness: int
    Life: string
    Sleep: string
    CriticalTicks: int
    DeathMessageShown: bool
    LastTickAt: DateTimeOffset
}

let stateFilePath =
    let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
    let dir = Path.Combine(appData, "ScreenPal")
    Directory.CreateDirectory(dir) |> ignore
    Path.Combine(dir, "state.json")

let lifeToString life =
    match life with
    | Alive -> "Alive"
    | Dead -> "Dead"

let parseLife (s: string) =
    if s = "Dead" then Dead else Alive

let sleepToString sleep =
    match sleep with
    | Awake -> "Awake"
    | Asleep -> "Asleep"

let parseSleep (s: string) =
    if s = "Asleep" then Asleep else Awake

let save (data: Persisted) =
    try
        let json = JsonSerializer.Serialize(data)
        File.WriteAllText(stateFilePath, json)
    with _ -> ()

let load () : Persisted option =
    try
        if File.Exists(stateFilePath) then
            let json = File.ReadAllText(stateFilePath)
            let raw = JsonSerializer.Deserialize<Persisted>(json) |> box
            if isNull raw then None
            else
                let p = unbox<Persisted> raw
                if isNull (box p.Name) then None else Some p
        else None
    with _ -> None

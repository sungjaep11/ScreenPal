module ScreenPal.App

open Elmish
open ScreenPal.Domain

type Screen =
    | NameEntry of input: string
    | MainView
    | InMemoryGame of MemoryGame.State
    | InWordGame of WordGame.State

type Model = {
    Name: string
    Stats: Stats
    Life: Life
    Sleep: Sleep
    Screen: Screen
    CriticalTicks: int
    Rng: System.Random
    DeathMessageShown: bool
}

type Msg =
    | Tick
    | NameChanged of string
    | ConfirmName
    | Feed
    | ToggleSleep
    | OpenMemoryGame
    | OpenWordGame
    | MemoryMsg of MemoryGame.Msg
    | WordMsg of WordGame.Msg
    | ExitMinigame
    | NewGame

let private emptyModel rng =
    { Name = ""
      Stats = initialStats
      Life = Alive
      Sleep = Awake
      Screen = NameEntry ""
      CriticalTicks = 0
      Rng = rng
      DeathMessageShown = false }

let init () : Model * Cmd<Msg> =
    emptyModel (System.Random()), Cmd.none

let private delayedUnflipCmd : Cmd<Msg> =
    let work dispatch =
        async {
            do! Async.Sleep 800
            dispatch (MemoryMsg MemoryGame.Unflip)
        }
        |> Async.StartImmediate
    Cmd.ofEffect work

let private handleTick (model: Model) : Model =
    match model.Life, model.Screen with
    | Dead, _ -> model
    | _, InMemoryGame _
    | _, InWordGame _
    | _, NameEntry _ -> model
    | Alive, MainView ->
        let nextStats =
            match model.Sleep with
            | Awake -> Logic.tickAwake model.Stats
            | Asleep -> Logic.tickAsleep model.Stats
        let critical = Logic.isCritical nextStats
        let criticalTicks =
            if critical then model.CriticalTicks + 1 else 0
        let dead = criticalTicks >= DeathAfterCriticalTicks
        { model with
            Stats = nextStats
            CriticalTicks = criticalTicks
            Life = if dead then Dead else Alive
            Sleep = if dead then Awake else model.Sleep }

let private finishMinigame (model: Model) (won: bool) : Model =
    let newStats = Logic.applyMinigameResult won model.Stats
    { model with Stats = newStats; Screen = MainView }

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg, model.Screen with
    | Tick, _ -> handleTick model, Cmd.none

    | NameChanged text, NameEntry _ ->
        { model with Screen = NameEntry text }, Cmd.none

    | ConfirmName, NameEntry input ->
        let trimmed = input.Trim()
        if trimmed = "" then model, Cmd.none
        else
            { model with
                Name = trimmed
                Screen = MainView
                Stats = initialStats
                Life = Alive
                Sleep = Awake
                CriticalTicks = 0
                DeathMessageShown = false }, Cmd.none

    | Feed, MainView when Logic.canFeed model.Life model.Sleep model.Stats ->
        { model with Stats = Logic.feed model.Stats }, Cmd.none

    | ToggleSleep, MainView when Logic.canToggleSleep model.Life ->
        let next = if model.Sleep = Awake then Asleep else Awake
        { model with Sleep = next }, Cmd.none

    | OpenMemoryGame, MainView when Logic.canPlayMinigame model.Life model.Sleep ->
        let tries = Logic.memoryTriesFor model.Stats
        let state = MemoryGame.init model.Rng tries
        { model with Screen = InMemoryGame state }, Cmd.none

    | OpenWordGame, MainView when Logic.canPlayMinigame model.Life model.Sleep ->
        let attempts = Logic.wordAttemptsFor model.Stats
        let state = WordGame.init model.Rng attempts
        { model with Screen = InWordGame state }, Cmd.none

    | MemoryMsg m, InMemoryGame state ->
        let state', needsUnflip = MemoryGame.update m state
        let cmd = if needsUnflip then delayedUnflipCmd else Cmd.none
        { model with Screen = InMemoryGame state' }, cmd

    | WordMsg m, InWordGame state ->
        let state' = WordGame.update m state
        { model with Screen = InWordGame state' }, Cmd.none

    | ExitMinigame, InMemoryGame state ->
        if MemoryGame.isFinished state then
            finishMinigame model (MemoryGame.didWin state), Cmd.none
        else
            { model with Screen = MainView }, Cmd.none

    | ExitMinigame, InWordGame state ->
        if WordGame.isFinished state then
            finishMinigame model (WordGame.didWin state), Cmd.none
        else
            { model with Screen = MainView }, Cmd.none

    | NewGame, _ ->
        { model with
            Name = ""
            Stats = initialStats
            Life = Alive
            Sleep = Awake
            Screen = NameEntry ""
            CriticalTicks = 0
            DeathMessageShown = false }, Cmd.none

    | _ -> model, Cmd.none

let timerSubscription (model: Model) : (string list * ((Msg -> unit) -> System.IDisposable)) list =
    let start (dispatch: Msg -> unit) =
        let timer = new System.Timers.Timer(TickIntervalMs)
        timer.AutoReset <- true
        timer.Elapsed.Add(fun _ -> dispatch Tick)
        timer.Start()
        { new System.IDisposable with
            member _.Dispose() =
                timer.Stop()
                timer.Dispose() }
    [ [ "tick" ], start ]

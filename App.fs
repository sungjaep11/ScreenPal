module ScreenPal.App

open Elmish
open ScreenPal.Domain

type Screen =
    | NameEntry of input: string
    | MainView
    | InPlayMenu
    | InFoodRoulette of FoodRoulette.State
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
    AnimFrame: int
    LastTickAt: System.DateTimeOffset
    ShowMain: bool
    ConfirmingNewGame: bool
    Dialogue: (string * System.DateTimeOffset) option
    GameDialogueIndex: int
    LastGameDialogueAt: System.DateTimeOffset
}

type Msg =
    | Tick
    | AnimTick
    | SleepTick
    | NameChanged of string
    | ConfirmName
    | Feed
    | SpinFood
    | FoodSpinTick
    | EatChosenFood
    | CloseFoodRoulette
    | ToggleSleep
    | OpenPlayMenu
    | ClosePlayMenu
    | OpenMemoryGame
    | OpenWordGame
    | MemoryMsg of MemoryGame.Msg
    | WordMsg of WordGame.Msg
    | ExitMinigame
    | OpenNewGameConfirm
    | CancelNewGameConfirm
    | NewGame
    | ShowMainWindow
    | HideMainWindow
    | ExitApp
    | GameDialogueTick
    | HomeDialogueTick

let mutable shutdownAction : unit -> unit = id
let mutable openMainAction : unit -> unit = id

let IdleFrameCount = 10
let SleepFrameCount = 4
let DrculaFrameCount = 6
let AnimFrameMod = 60
let AnimIntervalMs = 120.0
let SleepEnergyIntervalMs = 30000.0
let SleepEnergyPerSleepTick = 5
let GameDialogueIntervalMs = 10000.0
let HomeDialogueIntervalMs = 18000.0
let DialogueDurationSec = 3.0

let feedDialogues =
    [| "Yum yum! 🐟"
       "So tasty!"
       "More please!"
       "Nom nom nom..."
       "You spoil me!"
       "Mmm, delicious!"
       "Hooray, food!"
       "Best human ever 💖" |]

let memoryGameDialogues =
    [| "Tip: match two cards with the same symbol!"
       "Tip: remember where you saw each symbol!"
       "Tip: every flip costs a try — choose wisely!"
       "Tip: find all 6 pairs to win!"
       "Hmm, think carefully..."
       "Pawsome moves!"
       "Concentrate... 🧠"
       "Almost there!"
       "Meow-velous!"
       "You got this! 😼" |]

let wordGameDialogues =
    [| "Tip: 🟩 green = right letter, right spot!"
       "Tip: 🟨 yellow = right letter, wrong spot!"
       "Tip: ⬜ gray = letter not in the word!"
       "Tip: must be exactly 5 letters!"
       "Tip: press Enter to submit your guess!"
       "Hmm, let me think..."
       "Meow-rvelous! 😼"
       "Concentrate... 🧠"
       "Almost there!"
       "Don't mess up, human!" |]

let homeDialogues =
    [| "Hi there!"
       "Pet me!"
       "*purr purr*"
       "Hello, human!"
       "I love you 💕"
       "Tip: feed me when 🍗 hunger drops!"
       "Tip: click 🌙 sleep to recharge ⚡ energy!"
       "Tip: play minigames to boost 💖 happiness!"
       "Tip: if a stat stays critical too long, I'll faint! 😿"
       "Tip: sleeping doesn't stop hunger — feed me first!"
       "Tip: winning a minigame makes me extra happy!"
       "Tip: losing a minigame still cheers me up a little!"
       "Bored... play with me!"
       "Meow!"
       "Whatcha doin?"
       "Hehe :3"
       "Yawn..." |]

let private pickRandom (rng: System.Random) (items: string array) =
    items.[rng.Next(items.Length)]

let private withDialogue (model: Model) (text: string) =
    let expires = System.DateTimeOffset.UtcNow.AddSeconds(DialogueDurationSec)
    { model with Dialogue = Some (text, expires) }

// True only once enough time has passed since the last minigame dialogue, so the
// free-running timer can't stack a second bubble right after the opening one.
let private gameDialogueDue (model: Model) =
    let elapsed = (System.DateTimeOffset.UtcNow - model.LastGameDialogueAt).TotalMilliseconds
    elapsed >= GameDialogueIntervalMs * 0.5

// Shows the next minigame dialogue in order (tips first), advancing the index.
let private nextGameDialogue (model: Model) (items: string array) =
    let now = System.DateTimeOffset.UtcNow
    let text = items.[model.GameDialogueIndex % items.Length]
    { model with
        Dialogue = Some (text, now.AddSeconds(DialogueDurationSec))
        GameDialogueIndex = model.GameDialogueIndex + 1
        LastGameDialogueAt = now }

let private emptyModel rng =
    { Name = ""
      Stats = initialStats
      Life = Alive
      Sleep = Awake
      Screen = NameEntry ""
      CriticalTicks = 0
      Rng = rng
      DeathMessageShown = false
      AnimFrame = 0
      LastTickAt = System.DateTimeOffset.UtcNow
      ShowMain = false
      ConfirmingNewGame = false
      Dialogue = None
      GameDialogueIndex = 0
      LastGameDialogueAt = System.DateTimeOffset.MinValue }

let private toPersisted (model: Model) : Persistence.Persisted =
    { Name = model.Name
      Hunger = model.Stats.Hunger
      Energy = model.Stats.Energy
      Happiness = model.Stats.Happiness
      Life = Persistence.lifeToString model.Life
      Sleep = Persistence.sleepToString model.Sleep
      CriticalTicks = model.CriticalTicks
      DeathMessageShown = model.DeathMessageShown
      LastTickAt = model.LastTickAt }

let private fromPersisted (p: Persistence.Persisted) (rng: System.Random) : Model =
    { Name = p.Name
      Stats = { Hunger = p.Hunger; Energy = p.Energy; Happiness = p.Happiness }
      Life = Persistence.parseLife p.Life
      Sleep = Persistence.parseSleep p.Sleep
      Screen = if System.String.IsNullOrWhiteSpace p.Name then NameEntry "" else MainView
      CriticalTicks = p.CriticalTicks
      Rng = rng
      DeathMessageShown = p.DeathMessageShown
      AnimFrame = 0
      LastTickAt = p.LastTickAt
      ShowMain = false
      ConfirmingNewGame = false
      Dialogue = None
      GameDialogueIndex = 0
      LastGameDialogueAt = System.DateTimeOffset.MinValue }

let private applyOneTick (stats: Stats, life: Life, sleep: Sleep, criticalTicks: int) =
    match life with
    | Dead -> (stats, life, sleep, criticalTicks)
    | Alive ->
        let nextStats =
            match sleep with
            | Awake -> Logic.tickAwake stats
            | Asleep -> Logic.tickAsleep stats
        let isCrit = Logic.isCritical nextStats
        let newCritical = if isCrit then criticalTicks + 1 else 0
        let dead = newCritical >= DeathAfterCriticalTicks
        let newLife = if dead then Dead else Alive
        let newSleep = if dead then Awake else sleep
        (nextStats, newLife, newSleep, newCritical)

let private catchUp (now: System.DateTimeOffset) (model: Model) : Model =
    if System.String.IsNullOrWhiteSpace model.Name then model
    else
        let elapsedMs = (now - model.LastTickAt).TotalMilliseconds
        let ticks = max 0 (int (elapsedMs / TickIntervalMs))
        if ticks = 0 then model
        else
            let mutable state = (model.Stats, model.Life, model.Sleep, model.CriticalTicks)
            for _ in 1 .. ticks do
                state <- applyOneTick state
            let (stats, life, sleep, critical) = state
            let advancedAt = model.LastTickAt.AddMilliseconds(float ticks * TickIntervalMs)
            { model with
                Stats = stats
                Life = life
                Sleep = sleep
                CriticalTicks = critical
                LastTickAt = advancedAt }

let private saveCmd (model: Model) : Cmd<Msg> =
    Cmd.ofEffect (fun _ -> Persistence.save (toPersisted model))

let init () : Model * Cmd<Msg> =
    let rng = System.Random()
    let now = System.DateTimeOffset.UtcNow
    let baseModel =
        match Persistence.load () with
        | Some p -> fromPersisted p rng
        | None -> emptyModel rng
    let m = catchUp now baseModel
    m, saveCmd m

let private delayedUnflipCmd : Cmd<Msg> =
    let work dispatch =
        async {
            do! Async.Sleep 800
            dispatch (MemoryMsg MemoryGame.Unflip)
        }
        |> Async.StartImmediate
    Cmd.ofEffect work

let FoodSpinFrameMs = 16

let private foodSpinTickCmd : Cmd<Msg> =
    let work dispatch =
        async {
            do! Async.Sleep FoodSpinFrameMs
            dispatch FoodSpinTick
        }
        |> Async.StartImmediate
    Cmd.ofEffect work

let private handleTick (model: Model) : Model =
    if System.String.IsNullOrWhiteSpace model.Name then model
    else
        let advancedAt = model.LastTickAt.AddMilliseconds(TickIntervalMs)
        let (stats, life, sleep, critical) =
            applyOneTick (model.Stats, model.Life, model.Sleep, model.CriticalTicks)
        { model with
            Stats = stats
            Life = life
            Sleep = sleep
            CriticalTicks = critical
            LastTickAt = advancedAt }

let private finishMinigame (model: Model) (won: bool) : Model =
    let newStats = Logic.applyMinigameResult won model.Stats
    { model with Stats = newStats; Screen = MainView }

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg, model.Screen with
    | Tick, _ ->
        let m' = handleTick model
        m', saveCmd m'

    | AnimTick, _ ->
        { model with AnimFrame = (model.AnimFrame + 1) % AnimFrameMod }, Cmd.none

    | SleepTick, _ when model.Life = Alive && model.Sleep = Asleep ->
        let newEnergy = min MaxStat (model.Stats.Energy + SleepEnergyPerSleepTick)
        if newEnergy = model.Stats.Energy then model, Cmd.none
        else
            let m' = { model with Stats = { model.Stats with Energy = newEnergy } }
            m', saveCmd m'

    | SleepTick, _ -> model, Cmd.none

    | ShowMainWindow, _ ->
        { model with ShowMain = true }, Cmd.none

    | HideMainWindow, _ ->
        { model with ShowMain = false }, Cmd.none

    | ExitApp, _ ->
        model, Cmd.ofEffect (fun _ -> shutdownAction ())

    | NameChanged text, NameEntry _ ->
        { model with Screen = NameEntry text }, Cmd.none

    | ConfirmName, NameEntry input ->
        let trimmed = input.Trim()
        if trimmed = "" then model, Cmd.none
        else
            let m' =
                { model with
                    Name = trimmed
                    Screen = MainView
                    Stats = initialStats
                    Life = Alive
                    Sleep = Awake
                    CriticalTicks = 0
                    DeathMessageShown = false
                    LastTickAt = System.DateTimeOffset.UtcNow }
            m', saveCmd m'

    | Feed, MainView when Logic.canFeed model.Life model.Sleep model.Stats ->
        let state = FoodRoulette.init model.Rng
        { model with Screen = InFoodRoulette state }, Cmd.none

    | SpinFood, InFoodRoulette state when state.Phase = FoodRoulette.Ready ->
        let now = System.DateTimeOffset.UtcNow
        let state' = FoodRoulette.start model.Rng now state
        Audio.playRouletteSpin ()
        { model with Screen = InFoodRoulette state' }, foodSpinTickCmd

    | FoodSpinTick, InFoodRoulette state when FoodRoulette.isSpinning state ->
        let now = System.DateTimeOffset.UtcNow
        let state' = FoodRoulette.advance now state
        let stillSpinning = FoodRoulette.isSpinning state'
        if not stillSpinning then Audio.stopRouletteSpin ()
        let cmd = if stillSpinning then foodSpinTickCmd else Cmd.none
        { model with Screen = InFoodRoulette state' }, cmd

    | FoodSpinTick, _ -> model, Cmd.none

    | EatChosenFood, InFoodRoulette state when FoodRoulette.isLanded state ->
        let food = FoodRoulette.currentFood state
        let stats' = Logic.feedBy food.HungerGain model.Stats
        let fed = { model with Stats = stats'; Screen = MainView }
        let m' = withDialogue fed (sprintf "%s %s!" (pickRandom model.Rng feedDialogues) food.Name)
        Audio.stopRouletteSpin ()
        m', saveCmd m'

    | CloseFoodRoulette, InFoodRoulette _ ->
        Audio.stopRouletteSpin ()
        { model with Screen = MainView }, Cmd.none

    | GameDialogueTick, InMemoryGame _ when gameDialogueDue model ->
        nextGameDialogue model memoryGameDialogues, Cmd.none

    | GameDialogueTick, InWordGame _ when gameDialogueDue model ->
        nextGameDialogue model wordGameDialogues, Cmd.none

    | GameDialogueTick, _ -> model, Cmd.none

    | HomeDialogueTick, MainView when model.Life = Alive && model.Sleep = Awake ->
        withDialogue model (pickRandom model.Rng homeDialogues), Cmd.none

    | HomeDialogueTick, _ -> model, Cmd.none

    | ToggleSleep, MainView when Logic.canToggleSleep model.Life ->
        let next = if model.Sleep = Awake then Asleep else Awake
        let m' = { model with Sleep = next }
        m', saveCmd m'

    | OpenPlayMenu, MainView when Logic.canPlayMinigame model.Life model.Sleep ->
        { model with Screen = InPlayMenu }, Cmd.none

    | ClosePlayMenu, InPlayMenu ->
        { model with Screen = MainView }, Cmd.none

    | OpenMemoryGame, InPlayMenu when Logic.canPlayMinigame model.Life model.Sleep ->
        let tries = Logic.memoryTriesFor model.Stats
        let state = MemoryGame.init model.Rng tries
        let m0 = { model with Screen = InMemoryGame state; GameDialogueIndex = 0 }
        nextGameDialogue m0 memoryGameDialogues, Cmd.none

    | OpenWordGame, InPlayMenu when Logic.canPlayMinigame model.Life model.Sleep ->
        let attempts = Logic.wordAttemptsFor model.Stats
        let state = WordGame.init model.Rng attempts
        let m0 = { model with Screen = InWordGame state; GameDialogueIndex = 0 }
        nextGameDialogue m0 wordGameDialogues, Cmd.none

    | MemoryMsg m, InMemoryGame state ->
        let state', needsUnflip = MemoryGame.update m state
        let cmd = if needsUnflip then delayedUnflipCmd else Cmd.none
        { model with Screen = InMemoryGame state' }, cmd

    | WordMsg m, InWordGame state ->
        let state' = WordGame.update m state
        { model with Screen = InWordGame state' }, Cmd.none

    | ExitMinigame, InMemoryGame state ->
        if MemoryGame.isFinished state then
            let m' = finishMinigame model (MemoryGame.didWin state)
            m', saveCmd m'
        else
            { model with Screen = MainView }, Cmd.none

    | ExitMinigame, InWordGame state ->
        if WordGame.isFinished state then
            let m' = finishMinigame model (WordGame.didWin state)
            m', saveCmd m'
        else
            { model with Screen = MainView }, Cmd.none

    | OpenNewGameConfirm, _ ->
        { model with ConfirmingNewGame = true }, Cmd.none

    | CancelNewGameConfirm, _ ->
        { model with ConfirmingNewGame = false }, Cmd.none

    | NewGame, _ ->
        let m' =
            { model with
                Name = ""
                Stats = initialStats
                Life = Alive
                Sleep = Awake
                Screen = NameEntry ""
                CriticalTicks = 0
                DeathMessageShown = false
                LastTickAt = System.DateTimeOffset.UtcNow
                ConfirmingNewGame = false }
        m', saveCmd m'

    | _ -> model, Cmd.none

let timerSubscription (_model: Model) : (string list * ((Msg -> unit) -> System.IDisposable)) list =
    let start (dispatch: Msg -> unit) =
        let onUi (msg: Msg) =
            Avalonia.Threading.Dispatcher.UIThread.Post(System.Action(fun () -> dispatch msg))
        let tickTimer = new System.Timers.Timer(TickIntervalMs)
        tickTimer.AutoReset <- true
        tickTimer.Elapsed.Add(fun _ -> onUi Tick)
        tickTimer.Start()
        let animTimer = new System.Timers.Timer(AnimIntervalMs)
        animTimer.AutoReset <- true
        animTimer.Elapsed.Add(fun _ -> onUi AnimTick)
        animTimer.Start()
        let sleepTimer = new System.Timers.Timer(SleepEnergyIntervalMs)
        sleepTimer.AutoReset <- true
        sleepTimer.Elapsed.Add(fun _ -> onUi SleepTick)
        sleepTimer.Start()
        let gameDialogueTimer = new System.Timers.Timer(GameDialogueIntervalMs)
        gameDialogueTimer.AutoReset <- true
        gameDialogueTimer.Elapsed.Add(fun _ -> onUi GameDialogueTick)
        gameDialogueTimer.Start()
        let homeDialogueTimer = new System.Timers.Timer(HomeDialogueIntervalMs)
        homeDialogueTimer.AutoReset <- true
        homeDialogueTimer.Elapsed.Add(fun _ -> onUi HomeDialogueTick)
        homeDialogueTimer.Start()
        { new System.IDisposable with
            member _.Dispose() =
                tickTimer.Stop()
                tickTimer.Dispose()
                animTimer.Stop()
                animTimer.Dispose()
                sleepTimer.Stop()
                sleepTimer.Dispose()
                gameDialogueTimer.Stop()
                gameDialogueTimer.Dispose()
                homeDialogueTimer.Stop()
                homeDialogueTimer.Dispose() }
    [ [ "timers" ], start ]

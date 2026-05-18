module ScreenPal.View

open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open ScreenPal.Domain
open ScreenPal.App

let private moodEmoji = function
    | Happy -> "😄"
    | Neutral -> "😐"
    | Sad -> "😢"

let private moodLabel = function
    | Happy -> "Happy"
    | Neutral -> "OK"
    | Sad -> "Sad"

let private petFace (life: Life) (sleep: Sleep) (stats: Stats) =
    match life with
    | Dead -> "💀"
    | Alive ->
        match sleep with
        | Asleep -> "😴"
        | Awake -> moodEmoji (Logic.mood stats)

let private statBar (label: string) (value: int) (color: string) : IView =
    StackPanel.create [
        StackPanel.orientation Orientation.Vertical
        StackPanel.spacing 2.0
        StackPanel.margin (4.0, 4.0)
        StackPanel.children [
            DockPanel.create [
                DockPanel.children [
                    TextBlock.create [
                        TextBlock.text label
                        TextBlock.fontWeight FontWeight.SemiBold
                        DockPanel.dock Dock.Left
                    ]
                    TextBlock.create [
                        TextBlock.text (sprintf "%d / %d" value MaxStat)
                        TextBlock.horizontalAlignment HorizontalAlignment.Right
                    ]
                ]
            ]
            ProgressBar.create [
                ProgressBar.minimum (float MinStat)
                ProgressBar.maximum (float MaxStat)
                ProgressBar.value (float value)
                ProgressBar.height 18.0
                ProgressBar.foreground (SolidColorBrush(Color.Parse(color)))
            ]
        ]
    ]
    :> IView

let nameEntryView (input: string) (dispatch: Msg -> unit) : IView =
    StackPanel.create [
        StackPanel.spacing 12.0
        StackPanel.margin 32.0
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.children [
            TextBlock.create [
                TextBlock.text "🐣 Welcome to ScreenPal!"
                TextBlock.fontSize 28.0
                TextBlock.fontWeight FontWeight.Bold
                TextBlock.horizontalAlignment HorizontalAlignment.Center
            ]
            TextBlock.create [
                TextBlock.text "Give your new pet a name:"
                TextBlock.fontSize 16.0
                TextBlock.horizontalAlignment HorizontalAlignment.Center
            ]
            TextBox.create [
                TextBox.text input
                TextBox.watermark "Pet name"
                TextBox.width 240.0
                TextBox.fontSize 16.0
                TextBox.onTextChanged (fun t -> dispatch (NameChanged t))
                TextBox.onKeyDown (fun e ->
                    if e.Key = Input.Key.Enter then dispatch ConfirmName)
            ]
            Button.create [
                Button.content "Adopt"
                Button.width 120.0
                Button.horizontalAlignment HorizontalAlignment.Center
                Button.isEnabled (input.Trim() <> "")
                Button.onClick (fun _ -> dispatch ConfirmName)
            ]
        ]
    ]
    :> IView

let mainView (model: Model) (dispatch: Msg -> unit) : IView =
    let dead = model.Life = Dead
    let asleep = model.Sleep = Asleep
    let face = petFace model.Life model.Sleep model.Stats
    let mood = Logic.mood model.Stats
    let statusText =
        if dead then sprintf "%s has died. 💔" model.Name
        elif asleep then sprintf "%s is sleeping..." model.Name
        else sprintf "%s is feeling %s" model.Name (moodLabel mood)

    DockPanel.create [
        DockPanel.margin 16.0
        DockPanel.children [
            // Header
            StackPanel.create [
                DockPanel.dock Dock.Top
                StackPanel.orientation Orientation.Horizontal
                StackPanel.horizontalAlignment HorizontalAlignment.Stretch
                StackPanel.margin (0.0, 0.0, 0.0, 12.0)
                StackPanel.children [
                    TextBlock.create [
                        TextBlock.text (sprintf "🐾 %s" model.Name)
                        TextBlock.fontSize 22.0
                        TextBlock.fontWeight FontWeight.Bold
                        TextBlock.verticalAlignment VerticalAlignment.Center
                    ]
                    Button.create [
                        Button.content "New Game"
                        Button.margin (16.0, 0.0, 0.0, 0.0)
                        Button.onClick (fun _ -> dispatch NewGame)
                    ]
                ]
            ]
            // Action buttons row (bottom)
            StackPanel.create [
                DockPanel.dock Dock.Bottom
                StackPanel.orientation Orientation.Horizontal
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.margin (0.0, 12.0, 0.0, 0.0)
                StackPanel.children [
                    Button.create [
                        Button.content "🍎 Feed"
                        Button.width 130.0
                        Button.margin 4.0
                        Button.padding (12.0, 8.0)
                        Button.isEnabled (Logic.canFeed model.Life model.Sleep model.Stats)
                        Button.onClick (fun _ -> dispatch Feed)
                    ]
                    Button.create [
                        Button.content (if asleep then "☀️ Wake" else "🌙 Sleep")
                        Button.width 130.0
                        Button.margin 4.0
                        Button.padding (12.0, 8.0)
                        Button.isEnabled (Logic.canToggleSleep model.Life)
                        Button.onClick (fun _ -> dispatch ToggleSleep)
                    ]
                    Button.create [
                        Button.content "🃏 Memory"
                        Button.width 130.0
                        Button.margin 4.0
                        Button.padding (12.0, 8.0)
                        Button.isEnabled (Logic.canPlayMinigame model.Life model.Sleep)
                        Button.onClick (fun _ -> dispatch OpenMemoryGame)
                    ]
                    Button.create [
                        Button.content "🔤 Word"
                        Button.width 130.0
                        Button.margin 4.0
                        Button.padding (12.0, 8.0)
                        Button.isEnabled (Logic.canPlayMinigame model.Life model.Sleep)
                        Button.onClick (fun _ -> dispatch OpenWordGame)
                    ]
                ]
            ]
            // Center: pet face + status + stats
            StackPanel.create [
                StackPanel.spacing 12.0
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.verticalAlignment VerticalAlignment.Center
                StackPanel.children [
                    TextBlock.create [
                        TextBlock.text face
                        TextBlock.fontSize 96.0
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                    ]
                    TextBlock.create [
                        TextBlock.text statusText
                        TextBlock.fontSize 16.0
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.foreground (
                            if dead then Brushes.IndianRed :> IBrush
                            else Brushes.Black :> IBrush)
                    ]
                    StackPanel.create [
                        StackPanel.orientation Orientation.Vertical
                        StackPanel.width 360.0
                        StackPanel.children [
                            statBar "🍗 Hunger" model.Stats.Hunger "#E67E22"
                            statBar "⚡ Energy" model.Stats.Energy "#3498DB"
                            statBar "💖 Happiness" model.Stats.Happiness "#E91E63"
                        ]
                    ]
                ]
            ]
        ]
    ]
    :> IView

// ----- Memory game view -----

let private memoryCardButton (card: MemoryGame.Card) (locked: bool) (dispatch: MemoryGame.Msg -> unit) : IView =
    let isShown = card.FaceUp || card.Matched
    let face = if isShown then card.Symbol else "?"
    Button.create [
        Button.content face
        Button.fontSize 28.0
        Button.width 70.0
        Button.height 70.0
        Button.margin 4.0
        Button.background (
            if card.Matched then Brushes.LightGreen :> IBrush
            elif isShown then Brushes.LightYellow :> IBrush
            else Brushes.LightGray :> IBrush)
        Button.isEnabled (not locked && not isShown)
        Button.onClick (fun _ -> dispatch (MemoryGame.Flip card.Id))
    ]
    :> IView

let memoryView (model: Model) (state: MemoryGame.State) (dispatch: Msg -> unit) : IView =
    let localDispatch m = dispatch (MemoryMsg m)
    let finished = MemoryGame.isFinished state
    let resultText =
        match state.Result with
        | Some MemoryGame.Won -> sprintf "🎉 %s is delighted! All pairs found." model.Name
        | Some MemoryGame.Lost -> sprintf "💤 Out of tries. %s had fun anyway." model.Name
        | None -> sprintf "Find all pairs. Tries left: %d" state.TriesLeft

    DockPanel.create [
        DockPanel.margin 16.0
        DockPanel.children [
            TextBlock.create [
                DockPanel.dock Dock.Top
                TextBlock.text "🃏 Memory Match"
                TextBlock.fontSize 22.0
                TextBlock.fontWeight FontWeight.Bold
                TextBlock.horizontalAlignment HorizontalAlignment.Center
            ]
            TextBlock.create [
                DockPanel.dock Dock.Top
                TextBlock.text resultText
                TextBlock.fontSize 14.0
                TextBlock.margin (0.0, 8.0)
                TextBlock.horizontalAlignment HorizontalAlignment.Center
            ]
            Button.create [
                DockPanel.dock Dock.Bottom
                Button.content (if finished then "Continue" else "Quit")
                Button.padding (16.0, 8.0)
                Button.horizontalAlignment HorizontalAlignment.Center
                Button.margin (0.0, 12.0, 0.0, 0.0)
                Button.onClick (fun _ -> dispatch ExitMinigame)
            ]
            WrapPanel.create [
                WrapPanel.orientation Orientation.Horizontal
                WrapPanel.horizontalAlignment HorizontalAlignment.Center
                WrapPanel.verticalAlignment VerticalAlignment.Center
                WrapPanel.maxWidth 340.0
                WrapPanel.children [
                    for card in state.Cards do
                        memoryCardButton card state.Locked localDispatch
                ]
            ]
        ]
    ]
    :> IView

// ----- Word game view -----

let private feedbackColor = function
    | WordGame.Correct -> "#6AAA64"
    | WordGame.Present -> "#C9B458"
    | WordGame.Absent -> "#787C7E"

let private letterCell (ch: char) (feedback: WordGame.LetterFeedback) : IView =
    Border.create [
        Border.width 44.0
        Border.height 44.0
        Border.margin 3.0
        Border.background (SolidColorBrush(Color.Parse(feedbackColor feedback)))
        Border.cornerRadius 4.0
        Border.child (
            TextBlock.create [
                TextBlock.text (string (System.Char.ToUpperInvariant ch))
                TextBlock.fontSize 22.0
                TextBlock.fontWeight FontWeight.Bold
                TextBlock.foreground Brushes.White
                TextBlock.horizontalAlignment HorizontalAlignment.Center
                TextBlock.verticalAlignment VerticalAlignment.Center
            ]
        )
    ]
    :> IView

let private emptyCell () : IView =
    Border.create [
        Border.width 44.0
        Border.height 44.0
        Border.margin 3.0
        Border.borderBrush Brushes.LightGray
        Border.borderThickness 1.0
        Border.cornerRadius 4.0
    ]
    :> IView

let private guessRow (guess: WordGame.Guess) : IView =
    StackPanel.create [
        StackPanel.orientation Orientation.Horizontal
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.children [
            for i in 0 .. 4 do
                letterCell guess.Letters.[i] guess.Feedback.[i]
        ]
    ]
    :> IView

let private blankRow () : IView =
    StackPanel.create [
        StackPanel.orientation Orientation.Horizontal
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.children [
            for _ in 0 .. 4 -> emptyCell ()
        ]
    ]
    :> IView

let wordView (model: Model) (state: WordGame.State) (dispatch: Msg -> unit) : IView =
    let localDispatch m = dispatch (WordMsg m)
    let finished = WordGame.isFinished state
    let resultText =
        match state.Result with
        | Some WordGame.Won ->
            sprintf "🎉 %s is delighted! You got it: %s" model.Name (state.Answer.ToUpperInvariant())
        | Some WordGame.Lost ->
            sprintf "💔 Out of attempts. The word was: %s" (state.Answer.ToUpperInvariant())
        | None ->
            sprintf "Guess the 5-letter word. Attempts left: %d" state.AttemptsLeft

    DockPanel.create [
        DockPanel.margin 16.0
        DockPanel.children [
            TextBlock.create [
                DockPanel.dock Dock.Top
                TextBlock.text "🔤 Word Guess"
                TextBlock.fontSize 22.0
                TextBlock.fontWeight FontWeight.Bold
                TextBlock.horizontalAlignment HorizontalAlignment.Center
            ]
            TextBlock.create [
                DockPanel.dock Dock.Top
                TextBlock.text resultText
                TextBlock.fontSize 14.0
                TextBlock.margin (0.0, 6.0)
                TextBlock.horizontalAlignment HorizontalAlignment.Center
            ]
            StackPanel.create [
                DockPanel.dock Dock.Bottom
                StackPanel.spacing 8.0
                StackPanel.margin (0.0, 10.0, 0.0, 0.0)
                StackPanel.children [
                    (match state.Error with
                     | Some err ->
                        TextBlock.create [
                            TextBlock.text err
                            TextBlock.foreground Brushes.IndianRed
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                        ] :> IView
                     | None ->
                        TextBlock.create [
                            TextBlock.text ""
                        ] :> IView)
                    StackPanel.create [
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                        StackPanel.spacing 8.0
                        StackPanel.children [
                            TextBox.create [
                                TextBox.text state.Input
                                TextBox.width 180.0
                                TextBox.fontSize 16.0
                                TextBox.isEnabled (not finished)
                                TextBox.watermark "5 letters"
                                TextBox.onTextChanged (fun t ->
                                    localDispatch (WordGame.InputChanged t))
                                TextBox.onKeyDown (fun e ->
                                    if e.Key = Input.Key.Enter && not finished then
                                        localDispatch WordGame.Submit)
                            ]
                            Button.create [
                                Button.content "Submit"
                                Button.isEnabled (not finished && state.Input.Length = 5)
                                Button.onClick (fun _ -> localDispatch WordGame.Submit)
                            ]
                            Button.create [
                                Button.content (if finished then "Continue" else "Quit")
                                Button.onClick (fun _ -> dispatch ExitMinigame)
                            ]
                        ]
                    ]
                ]
            ]
            StackPanel.create [
                StackPanel.spacing 2.0
                StackPanel.verticalAlignment VerticalAlignment.Center
                StackPanel.children [
                    for guess in state.Guesses do
                        guessRow guess
                    let remaining = state.MaxAttempts - List.length state.Guesses
                    for _ in 1 .. remaining do
                        blankRow ()
                ]
            ]
        ]
    ]
    :> IView

let view (model: Model) (dispatch: Msg -> unit) : IView =
    match model.Screen with
    | NameEntry input -> nameEntryView input dispatch
    | MainView -> mainView model dispatch
    | InMemoryGame state -> memoryView model state dispatch
    | InWordGame state -> wordView model state dispatch

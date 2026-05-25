module ScreenPal.View

open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Platform
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open ScreenPal.Domain
open ScreenPal.App

let private moodEmoji = function
    | Happy -> "😄"
    | Neutral -> "😐"
    | Sad -> "😢"

let private moodLabel = function
    | Happy -> "happy"
    | Neutral -> "ok"
    | Sad -> "sad"

let private petFace (life: Life) (sleep: Sleep) (stats: Stats) =
    match life with
    | Dead -> "💀"
    | Alive ->
        match sleep with
        | Asleep -> "😴"
        | Awake -> moodEmoji (Logic.mood stats)

let PetSize = 192.0
let MinigamePetSize = 220.0

let private loadSpriteSheet (assetPath: string) (frameCount: int) (scale: int) : IImage[] =
    let uri = System.Uri(sprintf "avares://ScreenPal/%s" assetPath)
    use stream = AssetLoader.Open(uri)
    use source = new Bitmap(stream)
    let frameW = source.PixelSize.Width / frameCount
    let frameH = source.PixelSize.Height
    let scaled =
        source.CreateScaledBitmap(
            PixelSize(source.PixelSize.Width * scale, frameH * scale),
            BitmapInterpolationMode.None)
    [|
        for i in 0 .. frameCount - 1 ->
            new CroppedBitmap(
                scaled,
                PixelRect(i * frameW * scale, 0, frameW * scale, frameH * scale))
            :> IImage
    |]

let private idleFrames = loadSpriteSheet "assets/Idle.png" IdleFrameCount 6
let private sleepFrames = loadSpriteSheet "assets/Box3.png" SleepFrameCount 6
let private drculaFrames = loadSpriteSheet "assets/drculacat.png" DrculaFrameCount 4

let private emojiFont = FontFamily("Segoe UI Emoji, Segoe UI, Apple Color Emoji, Noto Color Emoji")

let private pixelFont =
    FontFamily("avares://ScreenPal/assets/PressStart2P-Regular.ttf#Press Start 2P")

let private DialogueReservedHeight = 84.0

let private dialogueBubble (model: Model) : IView =
    let visible, text =
        match model.Dialogue with
        | Some (txt, exp) when exp > System.DateTimeOffset.UtcNow -> true, txt
        | _ -> false, ""
    Border.create [
        Border.height DialogueReservedHeight
        Border.background Brushes.Transparent
        Border.horizontalAlignment HorizontalAlignment.Center
        Border.verticalAlignment VerticalAlignment.Bottom
        Border.child (
            StackPanel.create [
                StackPanel.isVisible visible
                StackPanel.orientation Orientation.Vertical
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.verticalAlignment VerticalAlignment.Bottom
                StackPanel.spacing 0.0
                StackPanel.children [
                    Border.create [
                        Border.background (SolidColorBrush(Color.Parse("#FFFBEA")) :> IBrush)
                        Border.borderBrush (SolidColorBrush(Color.Parse("#1A1A1A")) :> IBrush)
                        Border.borderThickness 3.0
                        Border.cornerRadius 4.0
                        Border.padding (Thickness(12.0, 10.0, 12.0, 12.0))
                        Border.maxWidth 240.0
                        Border.horizontalAlignment HorizontalAlignment.Center
                        Border.child (
                            TextBlock.create [
                                TextBlock.text text
                                TextBlock.fontFamily pixelFont
                                TextBlock.fontSize 10.0
                                TextBlock.foreground (SolidColorBrush(Color.Parse("#1A1A1A")) :> IBrush)
                                TextBlock.textWrapping TextWrapping.Wrap
                                TextBlock.textAlignment TextAlignment.Center
                                TextBlock.horizontalAlignment HorizontalAlignment.Center
                                TextBlock.lineHeight 16.0
                            ]
                        )
                    ] :> IView
                    Border.create [
                        Border.width 10.0
                        Border.height 10.0
                        Border.background (SolidColorBrush(Color.Parse("#FFFBEA")) :> IBrush)
                        Border.borderBrush (SolidColorBrush(Color.Parse("#1A1A1A")) :> IBrush)
                        Border.borderThickness (Thickness(0.0, 0.0, 3.0, 3.0))
                        Border.horizontalAlignment HorizontalAlignment.Center
                        Border.margin (0.0, -6.0, 0.0, 0.0)
                        Border.renderTransformOrigin (RelativePoint(0.5, 0.5, RelativeUnit.Relative))
                        Border.renderTransform (RotateTransform(45.0))
                    ] :> IView
                ]
            ]
        )
    ] :> IView

let private animatedSprite (frames: IImage[]) (size: float) (animFrame: int) : IView =
    Image.create [
        Image.source frames.[animFrame % frames.Length]
        Image.width size
        Image.height size
        Image.stretch Stretch.None
        Image.horizontalAlignment HorizontalAlignment.Center
    ] :> IView

let private zzzText (animFrame: int) =
    match (animFrame / 6) % 3 with
    | 0 -> "Z"
    | 1 -> "Zz"
    | _ -> "Zzz"

let private sleepingVisual (animFrame: int) : IView =
    Grid.create [
        Grid.width PetSize
        Grid.height PetSize
        Grid.horizontalAlignment HorizontalAlignment.Center
        Grid.clipToBounds false
        Grid.children [
            animatedSprite sleepFrames PetSize animFrame
            TextBlock.create [
                TextBlock.text (zzzText animFrame)
                TextBlock.fontSize 22.0
                TextBlock.foreground (SolidColorBrush(Color.Parse("#5B6BA8")) :> IBrush)
                TextBlock.horizontalAlignment HorizontalAlignment.Right
                TextBlock.verticalAlignment VerticalAlignment.Top
                TextBlock.margin (0.0, -32.0, 24.0, 0.0)
            ]
        ]
    ] :> IView

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

let private petVisualFor (life: Life) (sleep: Sleep) (stats: Stats) (animFrame: int) : IView =
    if life = Dead then
        TextBlock.create [
            TextBlock.text "💀"
            TextBlock.fontSize 96.0
            TextBlock.horizontalAlignment HorizontalAlignment.Center
            TextBlock.verticalAlignment VerticalAlignment.Center
        ] :> IView
    elif sleep = Asleep then sleepingVisual animFrame
    else animatedSprite idleFrames PetSize animFrame

let petWindowView (model: Model) (_dispatch: Msg -> unit) : IView =
    Border.create [
        Border.background Brushes.Transparent
        Border.cursor (new Input.Cursor(Input.StandardCursorType.Hand))
        Border.horizontalAlignment HorizontalAlignment.Center
        Border.verticalAlignment VerticalAlignment.Bottom
        Border.child (petVisualFor model.Life model.Sleep model.Stats model.AnimFrame)
    ] :> IView

let private confirmNewGameOverlay (dispatch: Msg -> unit) : IView =
    Border.create [
        Border.background (SolidColorBrush(Color.Parse("#B0000000")) :> IBrush)
        Border.child (
            Border.create [
                Border.background Brushes.White
                Border.borderBrush Brushes.Black
                Border.borderThickness 2.0
                Border.padding (Thickness(28.0, 24.0, 28.0, 24.0))
                Border.cornerRadius 6.0
                Border.horizontalAlignment HorizontalAlignment.Center
                Border.verticalAlignment VerticalAlignment.Center
                Border.child (
                    StackPanel.create [
                        StackPanel.spacing 16.0
                        StackPanel.children [
                            TextBlock.create [
                                TextBlock.text "Start a new game?"
                                TextBlock.fontSize 18.0
                                TextBlock.fontWeight FontWeight.Bold
                                TextBlock.horizontalAlignment HorizontalAlignment.Center
                            ]
                            TextBlock.create [
                                TextBlock.text "Your current pet will be lost."
                                TextBlock.fontSize 12.0
                                TextBlock.horizontalAlignment HorizontalAlignment.Center
                            ]
                            StackPanel.create [
                                StackPanel.orientation Orientation.Horizontal
                                StackPanel.spacing 12.0
                                StackPanel.horizontalAlignment HorizontalAlignment.Center
                                StackPanel.margin (0.0, 8.0, 0.0, 0.0)
                                StackPanel.children [
                                    Button.create [
                                        Button.content "Yes, reset"
                                        Button.padding (14.0, 8.0, 14.0, 14.0)
                                        Button.onClick (fun _ -> dispatch NewGame)
                                    ]
                                    Button.create [
                                        Button.content "Cancel"
                                        Button.padding (14.0, 8.0, 14.0, 14.0)
                                        Button.onClick (fun _ -> dispatch CancelNewGameConfirm)
                                    ]
                                ]
                            ]
                        ]
                    ] :> IView
                )
            ] :> IView
        )
    ] :> IView

let mainView (model: Model) (dispatch: Msg -> unit) : IView =
    let dead = model.Life = Dead
    let asleep = model.Sleep = Asleep
    let mood = Logic.mood model.Stats
    let statusText =
        if dead then sprintf "%s has died. 💔" model.Name
        elif asleep then sprintf "%s is sleeping..." model.Name
        else sprintf "%s is feeling %s" model.Name (moodLabel mood)

    let petVisual = petVisualFor model.Life model.Sleep model.Stats model.AnimFrame

    let mainContent =
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
                        Button.onClick (fun _ -> dispatch OpenNewGameConfirm)
                    ]
                    Button.create [
                        Button.content "To Cat"
                        Button.margin (8.0, 0.0, 0.0, 0.0)
                        Button.onClick (fun _ -> dispatch HideMainWindow)
                    ]
                    Button.create [
                        Button.content "Exit"
                        Button.margin (8.0, 0.0, 0.0, 0.0)
                        Button.onClick (fun _ -> dispatch ExitApp)
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
                        Button.padding (12.0, 8.0, 12.0, 14.0)
                        Button.isEnabled (Logic.canFeed model.Life model.Sleep model.Stats)
                        Button.onClick (fun _ -> dispatch Feed)
                    ]
                    Button.create [
                        Button.content (if asleep then "☀️ Wake" else "🌙 Sleep")
                        Button.width 130.0
                        Button.margin 4.0
                        Button.padding (12.0, 8.0, 12.0, 14.0)
                        Button.isEnabled (Logic.canToggleSleep model.Life)
                        Button.onClick (fun _ -> dispatch ToggleSleep)
                    ]
                    Button.create [
                        Button.content "🎮 Play"
                        Button.width 130.0
                        Button.margin 4.0
                        Button.padding (12.0, 8.0, 12.0, 14.0)
                        Button.isEnabled (Logic.canPlayMinigame model.Life model.Sleep)
                        Button.onClick (fun _ -> dispatch OpenPlayMenu)
                    ]
                ]
            ]
            // Center: pet face + status + stats
            StackPanel.create [
                StackPanel.spacing 12.0
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.verticalAlignment VerticalAlignment.Center
                StackPanel.children [
                    dialogueBubble model
                    petVisual
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

    let body : IView = mainContent :> IView
    if model.ConfirmingNewGame then
        Grid.create [
            Grid.children [
                body
                confirmNewGameOverlay dispatch
            ]
        ] :> IView
    else body

// ----- Play menu view -----

let playMenuView (model: Model) (dispatch: Msg -> unit) : IView =
    DockPanel.create [
        DockPanel.margin 16.0
        DockPanel.children [
            TextBlock.create [
                DockPanel.dock Dock.Top
                TextBlock.text "🎮 Play"
                TextBlock.fontSize 22.0
                TextBlock.fontWeight FontWeight.Bold
                TextBlock.horizontalAlignment HorizontalAlignment.Center
            ]
            Button.create [
                DockPanel.dock Dock.Bottom
                Button.content "Back"
                Button.padding (16.0, 8.0, 16.0, 14.0)
                Button.horizontalAlignment HorizontalAlignment.Center
                Button.margin (0.0, 16.0, 0.0, 0.0)
                Button.onClick (fun _ -> dispatch ClosePlayMenu)
            ]
            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.spacing 18.0
                StackPanel.verticalAlignment VerticalAlignment.Center
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.children [
                    Button.create [
                        Button.content "🃏 Memory Match"
                        Button.width 280.0
                        Button.padding (16.0, 14.0, 16.0, 20.0)
                        Button.fontSize 16.0
                        Button.isEnabled (Logic.canPlayMinigame model.Life model.Sleep)
                        Button.onClick (fun _ -> dispatch OpenMemoryGame)
                    ]
                    Button.create [
                        Button.content "🔤 Word Guess"
                        Button.width 280.0
                        Button.padding (16.0, 14.0, 16.0, 20.0)
                        Button.fontSize 16.0
                        Button.isEnabled (Logic.canPlayMinigame model.Life model.Sleep)
                        Button.onClick (fun _ -> dispatch OpenWordGame)
                    ]
                ]
            ]
        ]
    ] :> IView

// ----- Memory game view -----

let private memoryCardButton (card: MemoryGame.Card) (locked: bool) (dispatch: MemoryGame.Msg -> unit) : IView =
    let isShown = card.FaceUp || card.Matched
    let faceLabel : IView =
        if isShown then
            TextBlock.create [
                TextBlock.text card.Symbol
                TextBlock.fontFamily emojiFont
                TextBlock.fontSize 42.0
                TextBlock.foreground Brushes.Black
                TextBlock.textAlignment TextAlignment.Center
                TextBlock.horizontalAlignment HorizontalAlignment.Center
                TextBlock.verticalAlignment VerticalAlignment.Center
                TextBlock.padding (Thickness(0.0))
            ] :> IView
        else
            TextBlock.create [
                TextBlock.text "?"
                TextBlock.fontFamily pixelFont
                TextBlock.fontSize 28.0
                TextBlock.foreground (SolidColorBrush(Color.Parse("#1A1A1A")) :> IBrush)
                TextBlock.textAlignment TextAlignment.Center
                TextBlock.horizontalAlignment HorizontalAlignment.Center
                TextBlock.verticalAlignment VerticalAlignment.Center
                TextBlock.padding (Thickness(0.0))
            ] :> IView
    Button.create [
        Button.content faceLabel
        Button.width 78.0
        Button.height 78.0
        Button.margin 4.0
        Button.padding (Thickness(0.0))
        Button.horizontalContentAlignment HorizontalAlignment.Center
        Button.verticalContentAlignment VerticalAlignment.Center
        Button.background (
            if card.Matched then Brushes.LightGreen :> IBrush
            elif isShown then Brushes.White :> IBrush
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

    let catColumn : IView =
        StackPanel.create [
            StackPanel.orientation Orientation.Vertical
            StackPanel.spacing 10.0
            StackPanel.width 240.0
            StackPanel.margin (48.0, 0.0, 0.0, 0.0)
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.children [
                dialogueBubble model
                animatedSprite drculaFrames MinigamePetSize model.AnimFrame
            ]
        ] :> IView

    let board : IView =
        WrapPanel.create [
            WrapPanel.orientation Orientation.Horizontal
            WrapPanel.horizontalAlignment HorizontalAlignment.Center
            WrapPanel.verticalAlignment VerticalAlignment.Center
            WrapPanel.maxWidth 340.0
            WrapPanel.margin (24.0, 0.0, 0.0, 0.0)
            WrapPanel.children [
                for card in state.Cards do
                    memoryCardButton card state.Locked localDispatch
            ]
        ] :> IView

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
                Button.padding (16.0, 8.0, 16.0, 14.0)
                Button.minWidth 180.0
                Button.horizontalAlignment HorizontalAlignment.Center
                Button.margin (0.0, 12.0, 0.0, 0.0)
                Button.onClick (fun _ -> dispatch ExitMinigame)
            ]
            DockPanel.create [
                DockPanel.children [
                    ContentControl.create [
                        DockPanel.dock Dock.Left
                        ContentControl.verticalAlignment VerticalAlignment.Center
                        ContentControl.content catColumn
                    ]
                    ContentControl.create [
                        ContentControl.horizontalAlignment HorizontalAlignment.Center
                        ContentControl.verticalAlignment VerticalAlignment.Center
                        ContentControl.content board
                    ]
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

    let catColumn : IView =
        StackPanel.create [
            StackPanel.orientation Orientation.Vertical
            StackPanel.spacing 10.0
            StackPanel.width 240.0
            StackPanel.margin (48.0, 0.0, 0.0, 0.0)
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.children [
                dialogueBubble model
                animatedSprite drculaFrames MinigamePetSize model.AnimFrame
            ]
        ] :> IView

    let board : IView =
        StackPanel.create [
            StackPanel.spacing 2.0
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.margin (24.0, 0.0, 0.0, 0.0)
            StackPanel.children [
                for guess in state.Guesses do
                    guessRow guess
                let remaining = state.MaxAttempts - List.length state.Guesses
                for _ in 1 .. remaining do
                    blankRow ()
            ]
        ] :> IView

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
                                Button.minWidth 140.0
                                Button.onClick (fun _ -> dispatch ExitMinigame)
                            ]
                        ]
                    ]
                ]
            ]
            DockPanel.create [
                DockPanel.children [
                    ContentControl.create [
                        DockPanel.dock Dock.Left
                        ContentControl.verticalAlignment VerticalAlignment.Center
                        ContentControl.content catColumn
                    ]
                    ContentControl.create [
                        ContentControl.horizontalAlignment HorizontalAlignment.Center
                        ContentControl.verticalAlignment VerticalAlignment.Center
                        ContentControl.content board
                    ]
                ]
            ]
        ]
    ]
    :> IView

// ----- Sakura overlay -----

let private SakuraPetalCount = 16
let private SakuraCycleFrames = 60
let private SakuraCanvasWidth = 820.0
let private SakuraCanvasHeight = 760.0

let private sakuraPetal (x: float) (y: float) (rotation: float) (size: float) (alpha: byte) : IView =
    Border.create [
        Border.width size
        Border.height (size * 1.4)
        Border.background (SolidColorBrush(Color.FromArgb(alpha, 255uy, 188uy, 210uy)) :> IBrush)
        Border.borderBrush (SolidColorBrush(Color.FromArgb(alpha, 255uy, 145uy, 180uy)) :> IBrush)
        Border.borderThickness 0.5
        Border.cornerRadius (CornerRadius(size * 0.6, size * 0.15, size * 0.6, size * 0.15))
        Border.renderTransformOrigin (RelativePoint(0.5, 0.5, RelativeUnit.Relative))
        Border.renderTransform (RotateTransform(rotation))
        Canvas.left x
        Canvas.top y
    ] :> IView

let private sakuraOverlay (animFrame: int) : IView =
    Canvas.create [
        Canvas.background Brushes.Transparent
        Canvas.isHitTestVisible false
        Canvas.children [
            for i in 0 .. SakuraPetalCount - 1 do
                let seedX = float ((i * 137 + 23) % 780)
                let phase = (animFrame + i * 4) % SakuraCycleFrames
                let progress = float phase / float SakuraCycleFrames
                let y = progress * (SakuraCanvasHeight + 60.0) - 30.0
                let sway = sin (progress * 6.2831853 * 2.0 + float i * 0.7) * 28.0
                let x = seedX + sway
                let rot = progress * 360.0 + float (i * 47)
                let size = 10.0 + float (i % 4) * 3.5
                let alpha = 160uy + byte (i % 3 * 30)
                sakuraPetal x y rot size alpha
        ]
    ] :> IView

let private withSakura (animFrame: int) (content: IView) : IView =
    Grid.create [
        Grid.children [
            sakuraOverlay animFrame
            content
        ]
    ] :> IView

let view (model: Model) (dispatch: Msg -> unit) : IView =
    match model.Screen with
    | NameEntry input -> withSakura model.AnimFrame (nameEntryView input dispatch)
    | MainView -> withSakura model.AnimFrame (mainView model dispatch)
    | InPlayMenu -> withSakura model.AnimFrame (playMenuView model dispatch)
    | InMemoryGame state -> withSakura model.AnimFrame (memoryView model state dispatch)
    | InWordGame state -> withSakura model.AnimFrame (wordView model state dispatch)

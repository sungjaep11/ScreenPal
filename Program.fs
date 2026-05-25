module ScreenPal.Program

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Interactivity
open Avalonia.Media
open Avalonia.Styling
open Avalonia.Themes.Fluent
open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.Types
open global.Elmish

let pixelFont =
    FontFamily("avares://ScreenPal/assets/PressStart2P-Regular.ttf#Press Start 2P")

let PetWindowSize = 240.0

type PetWindow() as this =
    inherit HostWindow()

    let mutable pressArgs : Avalonia.Input.PointerPressedEventArgs option = None
    let mutable startPos : Point option = None
    let mutable dragStarted = false
    let dragThreshold = 5.0

    do
        base.Title <- "ScreenPal Pet"
        base.SystemDecorations <- SystemDecorations.None
        base.Background <- Brushes.Transparent
        base.TransparencyLevelHint <- [ WindowTransparencyLevel.Transparent ]
        base.Width <- PetWindowSize
        base.Height <- PetWindowSize
        base.Topmost <- true
        base.ShowInTaskbar <- false
        base.CanResize <- false
        base.FontFamily <- pixelFont
        base.FontSize <- 16.0

        this.PointerPressed.Add(fun e ->
            let point = e.GetCurrentPoint(this)
            if point.Properties.IsLeftButtonPressed then
                pressArgs <- Some e
                startPos <- Some (e.GetPosition(this))
                dragStarted <- false)

        this.PointerMoved.Add(fun e ->
            match pressArgs, startPos with
            | Some pe, Some start when not dragStarted ->
                let cur = e.GetPosition(this)
                let dx = cur.X - start.X
                let dy = cur.Y - start.Y
                if (abs dx) + (abs dy) > dragThreshold then
                    dragStarted <- true
                    this.BeginMoveDrag(pe)
            | _ -> ())

        this.PointerReleased.Add(fun _ ->
            if pressArgs.IsSome && not dragStarted then
                App.openMainAction ()
            pressArgs <- None
            startPos <- None
            dragStarted <- false)

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "ScreenPal"
        base.Width <- 820.0
        base.Height <- 760.0
        base.MinWidth <- 720.0
        base.MinHeight <- 680.0
        base.FontFamily <- pixelFont
        base.FontSize <- 16.0
        let pinkBg =
            LinearGradientBrush(
                StartPoint = RelativePoint(0.0, 0.0, RelativeUnit.Relative),
                EndPoint = RelativePoint(0.0, 1.0, RelativeUnit.Relative))
        pinkBg.GradientStops.Add(GradientStop(Color.Parse("#FFF7FA"), 0.0))
        pinkBg.GradientStops.Add(GradientStop(Color.Parse("#FFE6F0"), 1.0))
        base.Background <- pinkBg
        this.Closing.Add(fun e ->
            e.Cancel <- true
            this.Hide())

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Light

        let textBlockStyle = Style(fun s -> s.OfType<TextBlock>())
        textBlockStyle.Setters.Add(Setter(TextBlock.PaddingProperty, Thickness(0.0, 3.0, 0.0, 4.0)))
        this.Styles.Add(textBlockStyle)

        let buttonStyle = Style(fun s -> s.OfType<Button>())
        buttonStyle.Setters.Add(Setter(Button.PaddingProperty, Thickness(12.0, 8.0, 12.0, 10.0)))
        this.Styles.Add(buttonStyle)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            let pet = PetWindow()
            let main = MainWindow()

            Audio.init ()

            let clickHandler =
                System.EventHandler<RoutedEventArgs>(fun _ args ->
                    let isSilent =
                        match args.Source with
                        | :? Control as c ->
                            match c.Tag with
                            | :? string as t -> t = "silent"
                            | _ -> false
                        | _ -> false
                    if not isSilent then Audio.playButtonPress ())
            main.AddHandler(Button.ClickEvent, clickHandler, RoutingStrategies.Bubble)

            App.shutdownAction <- (fun () ->
                Audio.stopRouletteSpin ()
                Audio.stopBackgroundMusic ()
                desktop.Shutdown(0))

            pet.Opened.Add(fun _ ->
                match pet.Screens.Primary with
                | null -> ()
                | screen ->
                    let scaling = pet.DesktopScaling
                    let work = screen.WorkingArea
                    let widthPx = int (PetWindowSize * scaling)
                    let heightPx = int (PetWindowSize * scaling)
                    let marginPx = int (16.0 * scaling)
                    pet.Position <-
                        PixelPoint(
                            work.Right - widthPx - marginPx,
                            work.Bottom - heightPx - marginPx))

            let petHost = pet :> IViewHost
            let mainHost = main :> IViewHost
            let mutable lastShowMain = false
            let setState (model: App.Model) (dispatch: App.Msg -> unit) =
                App.openMainAction <- (fun () -> dispatch App.ShowMainWindow)
                petHost.Update (Some (View.petWindowView model dispatch))
                mainHost.Update (Some (View.view model dispatch))
                if model.ShowMain <> lastShowMain then
                    if model.ShowMain then
                        pet.Hide()
                        main.Show()
                        main.Activate()
                        Audio.playBackgroundMusic ()
                    else
                        main.Hide()
                        pet.Show()
                        Audio.stopBackgroundMusic ()
                    lastShowMain <- model.ShowMain

            desktop.MainWindow <- pet

            Program.mkProgram App.init App.update (fun _ _ -> Unchecked.defaultof<IView>)
            |> Program.withSubscription App.timerSubscription
            |> Program.withSetState setState
            |> Program.run
        | _ -> ()

[<EntryPoint>]
let main argv =
    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .UseSkia()
        .StartWithClassicDesktopLifetime(argv)

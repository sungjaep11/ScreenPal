module ScreenPal.Program

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open global.Elmish

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "ScreenPal"
        base.Width <- 640.0
        base.Height <- 600.0
        base.MinWidth <- 480.0
        base.MinHeight <- 520.0

        Program.mkProgram App.init App.update View.view
        |> Program.withSubscription App.timerSubscription
        |> Avalonia.FuncUI.Elmish.Program.withHost this
        |> Program.run

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Light

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.MainWindow <- MainWindow()
        | _ -> ()

[<EntryPoint>]
let main argv =
    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .UseSkia()
        .StartWithClassicDesktopLifetime(argv)

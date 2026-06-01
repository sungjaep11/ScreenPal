module ScreenPal.Audio

open System
open System.IO
open System.Threading
open Avalonia.Platform
open LibVLCSharp.Shared

// Playback uses LibVLC (via LibVLCSharp) instead of NAudio: NAudio's WaveOutEvent is
// a thin wrapper over the Win32 winmm waveOut API and has no output backend on macOS
// or Linux, so it silently produced no sound there. LibVLC ships native players for
// Windows/macOS/Linux, giving us identical behaviour across operating systems.

let private openAssetBytes (path: string) : byte[] =
    let uri = Uri(sprintf "avares://ScreenPal/%s" path)
    use s = AssetLoader.Open(uri)
    use ms = new MemoryStream()
    s.CopyTo(ms)
    ms.ToArray()

// LibVLC opens media by path, so we unpack each bundled asset to a temp file once.
// Returns the temp path, or None if extraction fails.
let private extractToTemp (assetPath: string) (tempName: string) : string option =
    try
        let bytes = openAssetBytes assetPath
        let dest = Path.Combine(Path.GetTempPath(), tempName)
        File.WriteAllBytes(dest, bytes)
        Some dest
    with _ -> None

let mutable private libvlc : LibVLC option = None
let mutable private bgPath : string option = None
let mutable private spinPath : string option = None
let mutable private clickPath : string option = None

let mutable private bgPlayer : MediaPlayer option = None
let mutable private spinPlayer : MediaPlayer option = None

let init () =
    if libvlc.IsNone then
        try
            Core.Initialize()
            libvlc <- Some (new LibVLC())
            bgPath <- extractToTemp "assets/audio/background_music.mp3" "screenpal_bg.mp3"
            spinPath <- extractToTemp "assets/audio/roulette_spin.mp3" "screenpal_spin.mp3"
            clickPath <- extractToTemp "assets/audio/button_press.wav" "screenpal_click.wav"
        with _ -> ()

let private disposePlayer (mp: MediaPlayer) =
    try mp.Stop() with _ -> ()
    try mp.Dispose() with _ -> ()

let stopBackgroundMusic () =
    match bgPlayer with
    | Some mp ->
        bgPlayer <- None
        disposePlayer mp
    | None -> ()

let playBackgroundMusic () =
    match libvlc, bgPath, bgPlayer with
    | Some vlc, Some path, None ->
        try
            let mp = new MediaPlayer(vlc)
            mp.Volume <- 35
            use media = new Media(vlc, path, FromType.FromPath)
            // Loop the track for the whole session.
            media.AddOption(":input-repeat=65535")
            mp.Play(media) |> ignore
            bgPlayer <- Some mp
        with _ -> ()
    | _ -> ()

let stopRouletteSpin () =
    match spinPlayer with
    | Some mp ->
        spinPlayer <- None
        disposePlayer mp
    | None -> ()

let playRouletteSpin () =
    match libvlc, spinPath with
    | Some vlc, Some path ->
        stopRouletteSpin ()
        try
            let mp = new MediaPlayer(vlc)
            mp.Volume <- 70
            use media = new Media(vlc, path, FromType.FromPath)
            mp.Play(media) |> ignore
            spinPlayer <- Some mp
        with _ -> ()
    | _ -> ()

let playButtonPress () =
    match libvlc, clickPath with
    | Some vlc, Some path ->
        try
            let mp = new MediaPlayer(vlc)
            mp.Volume <- 80
            // Fire-and-forget one-shot: clean up off the LibVLC event thread once it ends.
            mp.EndReached.Add(fun _ ->
                ThreadPool.QueueUserWorkItem(fun _ -> disposePlayer mp) |> ignore)
            use media = new Media(vlc, path, FromType.FromPath)
            mp.Play(media) |> ignore
        with _ -> ()
    | _ -> ()

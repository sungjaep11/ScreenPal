module ScreenPal.Audio

open System
open System.IO
open System.Diagnostics
open System.Globalization
open System.Threading
open System.Runtime.InteropServices
open Avalonia.Platform
open LibVLCSharp.Shared

// Cross-platform audio. The preferred backend is LibVLC (via LibVLCSharp), which ships
// native players for Windows/macOS/Linux. However, the VideoLAN.LibVLC.Mac package only
// bundles an x86_64 dylib, so on Apple Silicon (arm64) the LibVLC native library cannot be
// loaded into the arm64 .NET process and initialization throws. NAudio is no better: its
// WaveOutEvent is a thin wrapper over the Win32 winmm waveOut API with no backend off
// Windows. So when LibVLC is unavailable we fall back to the operating system's built-in
// command-line player (afplay on macOS, ffplay/cvlc/etc. on Linux), which needs no bundled
// native binaries at all.

let private openAssetBytes (path: string) : byte[] =
    let uri = Uri(sprintf "avares://ScreenPal/%s" path)
    use s = AssetLoader.Open(uri)
    use ms = new MemoryStream()
    s.CopyTo(ms)
    ms.ToArray()

// Both backends open media by file path, so we unpack each bundled asset to a temp file
// once. Returns the temp path, or None if extraction fails.
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

// LibVLC players (used when the native library loaded successfully).
let mutable private bgPlayer : MediaPlayer option = None
let mutable private spinPlayer : MediaPlayer option = None

// ----- CLI fallback player (macOS / Linux, or anywhere LibVLC fails to load) -----

let private isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
let private isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

module WinMM =
    [<DllImport("winmm.dll", EntryPoint = "mciSendStringW", CharSet = CharSet.Unicode)>]
    extern int mciSendString(string lpstrCommand, string lpstrReturnString, int uReturnLength, nativeint hwndCallback)

    let play (alias: string) (path: string) (volume: float) (loop: bool) =
        mciSendString(sprintf "close %s" alias, null, 0, 0n) |> ignore
        mciSendString(sprintf "open \"%s\" type mpegvideo alias %s" path alias, null, 0, 0n) |> ignore
        let vol = int (volume * 1000.0)
        mciSendString(sprintf "setaudio %s volume to %d" alias vol, null, 0, 0n) |> ignore
        let repeatStr = if loop then " repeat" else ""
        mciSendString(sprintf "play %s%s" alias repeatStr, null, 0, 0n) |> ignore
        
    let stop (alias: string) =
        mciSendString(sprintf "stop %s" alias, null, 0, 0n) |> ignore
        mciSendString(sprintf "close %s" alias, null, 0, 0n) |> ignore

// Resolve a CLI audio player once. macOS always has afplay; on Linux we probe for a few
// common players (preferring ones that handle mp3 and support a volume flag).
let private linuxPlayer =
    lazy (
        let exists (cmd: string) =
            try
                let psi = ProcessStartInfo("which", cmd)
                psi.RedirectStandardOutput <- true
                psi.RedirectStandardError <- true
                psi.UseShellExecute <- false
                match Process.Start(psi) with
                | null -> false
                | started ->
                    use p = started
                    p.WaitForExit(2000) |> ignore
                    p.HasExited && p.ExitCode = 0
            with _ -> false
        [ "ffplay"; "mpg123"; "paplay"; "aplay" ] |> List.tryFind exists)

// Start a one-shot CLI playback of the given file. volume is a 0.0-1.0 multiplier (applied
// only on players that support it). Returns the Process so callers can stop it if needed.
let private startCliPlayer (path: string) (volume: float) : Process option =
    try
        let inv = CultureInfo.InvariantCulture
        let psi = ProcessStartInfo()
        psi.UseShellExecute <- false
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.CreateNoWindow <- true
        let configured =
            if isMac then
                psi.FileName <- "afplay"
                psi.ArgumentList.Add("-v")
                psi.ArgumentList.Add(volume.ToString(inv))
                psi.ArgumentList.Add(path)
                true
            else
                match linuxPlayer.Value with
                | Some "ffplay" ->
                    psi.FileName <- "ffplay"
                    for a in [ "-nodisp"; "-autoexit"; "-loglevel"; "quiet"
                               "-volume"; (int (volume * 100.0)).ToString(inv); path ] do
                        psi.ArgumentList.Add(a)
                    true
                | Some "mpg123" ->
                    psi.FileName <- "mpg123"
                    psi.ArgumentList.Add("-q")
                    psi.ArgumentList.Add(path)
                    true
                | Some "paplay" ->
                    psi.FileName <- "paplay"
                    psi.ArgumentList.Add(path)
                    true
                | Some "aplay" ->
                    psi.FileName <- "aplay"
                    psi.ArgumentList.Add("-q")
                    psi.ArgumentList.Add(path)
                    true
                | _ -> false
        if configured then
            match Process.Start(psi) with
            | null -> None
            | p -> Some p
        else None
    with _ -> None

// Fire-and-forget one-shot: start it and dispose the handle once it exits.
let private startCliOneShot (path: string) (volume: float) =
    match startCliPlayer path volume with
    | Some p ->
        try
            p.EnableRaisingEvents <- true
            p.Exited.Add(fun _ -> try p.Dispose() with _ -> ())
        with _ -> ()
    | None -> ()

let private killProcess (p: Process) =
    try if not p.HasExited then p.Kill() with _ -> ()
    try p.Dispose() with _ -> ()

// Background music loop (CLI). A background thread replays the track until cancelled,
// since the CLI players don't loop on their own.
let mutable private bgCliCts : CancellationTokenSource option = None
let mutable private bgCliProc : Process option = None

// Currently-playing roulette spin (CLI), so it can be stopped early.
let mutable private spinCliProc : Process option = None

let private usingVlc () = libvlc.IsSome

let init () =
    // Always extract the assets first so the CLI fallback has paths even if LibVLC fails.
    if bgPath.IsNone then bgPath <- extractToTemp "assets/audio/background_music.mp3" "screenpal_bg.mp3"
    if spinPath.IsNone then spinPath <- extractToTemp "assets/audio/roulette_spin.mp3" "screenpal_spin.mp3"
    if clickPath.IsNone then clickPath <- extractToTemp "assets/audio/button_press.wav" "screenpal_click.wav"
    // Try to bring up LibVLC. If the native library can't load (e.g. x86_64-only dylib on an
    // arm64 process), this throws and we silently stay on the CLI fallback.
    if libvlc.IsNone then
        try
            Core.Initialize()
            libvlc <- Some(new LibVLC())
        with _ -> ()

let private disposePlayer (mp: MediaPlayer) =
    try mp.Stop() with _ -> ()
    try mp.Dispose() with _ -> ()

let stopBackgroundMusic () =
    if usingVlc () then
        match bgPlayer with
        | Some mp ->
            bgPlayer <- None
            disposePlayer mp
        | None -> ()
    else if isWindows then
        WinMM.stop "bg"
    else
        match bgCliCts with
        | Some cts -> (try cts.Cancel() with _ -> ()); bgCliCts <- None
        | None -> ()
        match bgCliProc with
        | Some p -> bgCliProc <- None; killProcess p
        | None -> ()

let playBackgroundMusic () =
    match bgPath with
    | None -> ()
    | Some path ->
        if usingVlc () then
            match libvlc, bgPlayer with
            | Some vlc, None ->
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
        else if isWindows then
            WinMM.play "bg" path 0.35 true
        else
            // Already looping?
            match bgCliCts with
            | Some _ -> ()
            | None ->
                let cts = new CancellationTokenSource()
                bgCliCts <- Some cts
                let token = cts.Token
                let work () =
                    let mutable keepGoing = true
                    while keepGoing && not token.IsCancellationRequested do
                        match startCliPlayer path 0.35 with
                        | Some p ->
                            bgCliProc <- Some p
                            p.WaitForExit()
                            try p.Dispose() with _ -> ()
                        | None ->
                            // No usable player; stop trying instead of spinning.
                            keepGoing <- false
                let t = Thread(ThreadStart(work))
                t.IsBackground <- true
                t.Start()

let stopRouletteSpin () =
    if usingVlc () then
        match spinPlayer with
        | Some mp ->
            spinPlayer <- None
            disposePlayer mp
        | None -> ()
    else if isWindows then
        WinMM.stop "spin"
    else
        match spinCliProc with
        | Some p -> spinCliProc <- None; killProcess p
        | None -> ()

let playRouletteSpin () =
    match spinPath with
    | None -> ()
    | Some path ->
        stopRouletteSpin ()
        if usingVlc () then
            match libvlc with
            | Some vlc ->
                try
                    let mp = new MediaPlayer(vlc)
                    mp.Volume <- 70
                    use media = new Media(vlc, path, FromType.FromPath)
                    mp.Play(media) |> ignore
                    spinPlayer <- Some mp
                with _ -> ()
            | None -> ()
        else if isWindows then
            WinMM.play "spin" path 0.7 false
        else
            spinCliProc <- startCliPlayer path 0.7

let playButtonPress () =
    match clickPath with
    | None -> ()
    | Some path ->
        if usingVlc () then
            match libvlc with
            | Some vlc ->
                try
                    let mp = new MediaPlayer(vlc)
                    mp.Volume <- 80
                    // Fire-and-forget one-shot: clean up off the LibVLC event thread once it ends.
                    mp.EndReached.Add(fun _ ->
                        ThreadPool.QueueUserWorkItem(fun _ -> disposePlayer mp) |> ignore)
                    use media = new Media(vlc, path, FromType.FromPath)
                    mp.Play(media) |> ignore
                with _ -> ()
            | None -> ()
        else if isWindows then
            let alias = sprintf "click_%d" (Environment.TickCount)
            WinMM.play alias path 0.8 false
        else
            startCliOneShot path 0.8

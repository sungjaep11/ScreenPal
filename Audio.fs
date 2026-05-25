module ScreenPal.Audio

open System
open System.IO
open Avalonia.Platform
open NAudio.Wave

let private openAssetBytes (path: string) : byte[] =
    let uri = Uri(sprintf "avares://ScreenPal/%s" path)
    use s = AssetLoader.Open(uri)
    use ms = new MemoryStream()
    s.CopyTo(ms)
    ms.ToArray()

let private isWavPath (path: string) =
    path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)

let private makeReader (bytes: byte[]) (isWav: bool) : WaveStream =
    let ms = new MemoryStream(bytes)
    if isWav then new WaveFileReader(ms) :> WaveStream
    else new Mp3FileReader(ms) :> WaveStream

type private LoopStream(src: WaveStream) =
    inherit WaveStream()
    override _.WaveFormat = src.WaveFormat
    override _.Length = src.Length
    override _.Position
        with get () = src.Position
        and set (v) = src.Position <- v
    override _.Read(buffer, offset, count) =
        let mutable total = 0
        while total < count do
            let read = src.Read(buffer, offset + total, count - total)
            if read <= 0 then src.Position <- 0L
            else total <- total + read
        total
    override _.Dispose(disposing) =
        if disposing then src.Dispose()
        base.Dispose(disposing)

type private Channel = {
    mutable Out: WaveOutEvent option
    mutable Reader: WaveStream option
}

let private mkChannel () : Channel = { Out = None; Reader = None }

let private bg = mkChannel ()
let private spin = mkChannel ()

let mutable private bgBytes : byte[] = [||]
let mutable private bgWav = false
let mutable private spinBytes : byte[] = [||]
let mutable private spinWav = false
let mutable private clickBytes : byte[] = [||]
let mutable private clickWav = true
let mutable private initialized = false

let init () =
    if not initialized then
        try
            let bgPath = "assets/audio/background_music.mp3"
            let spinPath = "assets/audio/roulette_spin.mp3"
            let clickPath = "assets/audio/button_press.wav"
            bgBytes <- openAssetBytes bgPath
            bgWav <- isWavPath bgPath
            spinBytes <- openAssetBytes spinPath
            spinWav <- isWavPath spinPath
            clickBytes <- openAssetBytes clickPath
            clickWav <- isWavPath clickPath
            initialized <- true
        with _ -> ()

let private stopChannel (ch: Channel) =
    match ch.Out, ch.Reader with
    | Some out, Some rdr ->
        try out.Stop() with _ -> ()
        try out.Dispose() with _ -> ()
        try rdr.Dispose() with _ -> ()
    | _ -> ()
    ch.Out <- None
    ch.Reader <- None

let playBackgroundMusic () =
    if initialized && bg.Out.IsNone then
        try
            let src = makeReader bgBytes bgWav
            let loop = new LoopStream(src)
            let out = new WaveOutEvent()
            out.Init(loop)
            out.Volume <- 0.35f
            out.Play()
            bg.Out <- Some out
            bg.Reader <- Some (loop :> WaveStream)
        with _ -> ()

let stopBackgroundMusic () = stopChannel bg

let playRouletteSpin () =
    if initialized then
        stopChannel spin
        try
            let rdr = makeReader spinBytes spinWav
            let out = new WaveOutEvent()
            out.Init(rdr)
            out.Volume <- 0.7f
            out.Play()
            spin.Out <- Some out
            spin.Reader <- Some rdr
        with _ -> ()

let stopRouletteSpin () = stopChannel spin

let playButtonPress () =
    if initialized then
        try
            let rdr = makeReader clickBytes clickWav
            let out = new WaveOutEvent()
            out.Init(rdr)
            out.Volume <- 0.8f
            out.PlaybackStopped.Add(fun _ ->
                try out.Dispose() with _ -> ()
                try rdr.Dispose() with _ -> ())
            out.Play()
        with _ -> ()

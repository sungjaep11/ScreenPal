module ScreenPal.FoodRoulette

type Food = {
    Emoji: string
    Name: string
    HungerGain: int
}

let foods : Food[] = [|
    { Emoji = "🍪"; Name = "Cookie";   HungerGain = 8  }
    { Emoji = "🥕"; Name = "Carrot";   HungerGain = 10 }
    { Emoji = "🍎"; Name = "Apple";    HungerGain = 14 }
    { Emoji = "🍞"; Name = "Bread";    HungerGain = 18 }
    { Emoji = "🍰"; Name = "Cake";     HungerGain = 22 }
    { Emoji = "🍣"; Name = "Sushi";    HungerGain = 30 }
    { Emoji = "🍗"; Name = "Chicken";  HungerGain = 38 }
    { Emoji = "🍔"; Name = "Burger";   HungerGain = 50 }
|]

type Phase =
    | Ready
    | Spinning
    | Landed

type State = {
    StartIndex: int          // index at center when spin began
    TotalCells: int          // total cells to scroll across
    DurationMs: float
    StartTime: System.DateTimeOffset
    DisplayIndex: int        // index currently at center (floor of position)
    ScrollFrac: float        // 0..1 — fractional shift toward next cell
    Phase: Phase
}

let init (rng: System.Random) : State =
    let startIdx = rng.Next(foods.Length)
    { StartIndex = startIdx
      TotalCells = 0
      DurationMs = 0.0
      StartTime = System.DateTimeOffset.UtcNow
      DisplayIndex = startIdx
      ScrollFrac = 0.0
      Phase = Ready }

let start (rng: System.Random) (now: System.DateTimeOffset) (state: State) : State =
    let extraRotations = rng.Next(2, 4)
    let targetOffset = rng.Next(foods.Length)
    let total = extraRotations * foods.Length + targetOffset + foods.Length
    let duration = 2400.0 + float (rng.Next(0, 600))
    { state with
        Phase = Spinning
        StartIndex = state.DisplayIndex
        TotalCells = total
        DurationMs = duration
        StartTime = now
        ScrollFrac = 0.0 }

let private wrap n x =
    let r = x % n
    if r < 0 then r + n else r

let advance (now: System.DateTimeOffset) (state: State) : State =
    match state.Phase with
    | Spinning ->
        let elapsed = (now - state.StartTime).TotalMilliseconds
        let progress = min 1.0 (max 0.0 (elapsed / state.DurationMs))
        // cubic ease-out: 1 - (1 - t)^3
        let invT = 1.0 - progress
        let eased = 1.0 - invT * invT * invT
        let position = float state.TotalCells * eased
        let floorPos = int (System.Math.Floor position)
        let frac = position - float floorPos
        let n = foods.Length
        if progress >= 1.0 then
            { state with
                DisplayIndex = wrap n (state.StartIndex + state.TotalCells)
                ScrollFrac = 0.0
                Phase = Landed }
        else
            { state with
                DisplayIndex = wrap n (state.StartIndex + floorPos)
                ScrollFrac = frac }
    | _ -> state

let currentFood (state: State) : Food = foods.[state.DisplayIndex]

let foodAtOffset (state: State) (offset: int) : Food =
    let n = foods.Length
    foods.[wrap n (state.DisplayIndex + offset)]

let isLanded (state: State) =
    match state.Phase with
    | Landed -> true
    | _ -> false

let isSpinning (state: State) =
    match state.Phase with
    | Spinning -> true
    | _ -> false

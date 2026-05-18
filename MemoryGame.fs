module ScreenPal.MemoryGame

type Card = {
    Id: int
    Symbol: string
    FaceUp: bool
    Matched: bool
}

type Result = Won | Lost

type State = {
    Cards: Card list
    Flipped: int list           // ids of currently face-up, unmatched cards
    TriesLeft: int
    MaxTries: int
    Locked: bool                // true while waiting for unflip
    Result: Result option
}

type Msg =
    | Flip of int
    | Unflip
    | Dismiss

let private symbolPool =
    [| "🍎"; "🍌"; "🍇"; "🍒"; "🍓"; "🍋"; "🥝"; "🍑"
       "🍍"; "🥥"; "🥨"; "🍩"; "🧁"; "🍪"; "🍰"; "🍫" |]

let private shuffle (rng: System.Random) (items: 'a array) =
    let arr = Array.copy items
    let n = arr.Length
    for i in n - 1 .. -1 .. 1 do
        let j = rng.Next(i + 1)
        let tmp = arr.[i]
        arr.[i] <- arr.[j]
        arr.[j] <- tmp
    arr

let init (rng: System.Random) (maxTries: int) =
    let pairs = 6
    let chosen = (shuffle rng symbolPool) |> Array.take pairs
    let doubled = Array.append chosen chosen
    let shuffled = shuffle rng doubled
    let cards =
        shuffled
        |> Array.mapi (fun i s ->
            { Id = i; Symbol = s; FaceUp = false; Matched = false })
        |> Array.toList
    { Cards = cards
      Flipped = []
      TriesLeft = maxTries
      MaxTries = maxTries
      Locked = false
      Result = None }

let private cardById id cards = cards |> List.find (fun c -> c.Id = id)

let private allMatched cards =
    cards |> List.forall (fun c -> c.Matched)

let update (msg: Msg) (state: State) =
    if state.Result.IsSome then state, false
    else
        match msg with
        | Flip id ->
            if state.Locked then state, false
            else
                let card = cardById id state.Cards
                if card.FaceUp || card.Matched then state, false
                else
                    let cards' =
                        state.Cards
                        |> List.map (fun c ->
                            if c.Id = id then { c with FaceUp = true } else c)
                    let flipped' = state.Flipped @ [ id ]
                    if List.length flipped' < 2 then
                        { state with Cards = cards'; Flipped = flipped' }, false
                    else
                        // Two cards flipped. Compare.
                        let a = cardById flipped'.[0] cards'
                        let b = cardById flipped'.[1] cards'
                        if a.Symbol = b.Symbol then
                            let cards'' =
                                cards'
                                |> List.map (fun c ->
                                    if c.Id = a.Id || c.Id = b.Id then
                                        { c with Matched = true }
                                    else c)
                            let tries' = state.TriesLeft - 1
                            let won = allMatched cards''
                            let result =
                                if won then Some Won
                                elif tries' <= 0 then Some Lost
                                else None
                            { state with
                                Cards = cards''
                                Flipped = []
                                TriesLeft = tries'
                                Result = result }, false
                        else
                            // Mismatch: lock and schedule unflip.
                            let tries' = state.TriesLeft - 1
                            let result =
                                if tries' <= 0 then Some Lost else None
                            { state with
                                Cards = cards'
                                Flipped = flipped'
                                TriesLeft = tries'
                                Locked = result.IsNone
                                Result = result }, result.IsNone
        | Unflip ->
            let cards' =
                state.Cards
                |> List.map (fun c ->
                    if List.contains c.Id state.Flipped && not c.Matched then
                        { c with FaceUp = false }
                    else c)
            { state with Cards = cards'; Flipped = []; Locked = false }, false
        | Dismiss -> state, false

let isFinished (state: State) = state.Result.IsSome

let didWin (state: State) =
    match state.Result with
    | Some Won -> true
    | _ -> false

module ScreenPal.WordGame

type LetterFeedback = Correct | Present | Absent

type Guess = { Letters: string; Feedback: LetterFeedback[] }

type Result = Won | Lost

type State = {
    Answer: string
    Guesses: Guess list
    Input: string
    AttemptsLeft: int
    MaxAttempts: int
    Result: Result option
    Error: string option
}

type Msg =
    | InputChanged of string
    | Submit
    | Dismiss

let init (rng: System.Random) (maxAttempts: int) =
    { Answer = Words.pick rng
      Guesses = []
      Input = ""
      AttemptsLeft = maxAttempts
      MaxAttempts = maxAttempts
      Result = None
      Error = None }

let score (answer: string) (guess: string) =
    let feedback = Array.create 5 Absent
    let answerChars = answer.ToCharArray()
    let guessChars = guess.ToCharArray()
    let used = Array.create 5 false
    for i in 0 .. 4 do
        if guessChars.[i] = answerChars.[i] then
            feedback.[i] <- Correct
            used.[i] <- true
    for i in 0 .. 4 do
        if feedback.[i] <> Correct then
            let mutable matched = false
            let mutable j = 0
            while not matched && j < 5 do
                if not used.[j] && guessChars.[i] = answerChars.[j] then
                    feedback.[i] <- Present
                    used.[j] <- true
                    matched <- true
                j <- j + 1
    feedback

let private isAlpha (s: string) =
    s.Length = 5
    && s |> Seq.forall System.Char.IsLetter

let update (msg: Msg) (state: State) =
    if state.Result.IsSome then state
    else
        match msg with
        | InputChanged text ->
            let cleaned =
                text
                |> Seq.filter System.Char.IsLetter
                |> Seq.truncate 5
                |> Seq.map System.Char.ToLowerInvariant
                |> System.String.Concat
            { state with Input = cleaned; Error = None }
        | Submit ->
            if not (isAlpha state.Input) then
                { state with Error = Some "Enter exactly 5 letters." }
            else
                let guess = state.Input.ToLowerInvariant()
                let feedback = score state.Answer guess
                let newGuess = { Letters = guess; Feedback = feedback }
                let attemptsLeft = state.AttemptsLeft - 1
                let won = guess = state.Answer
                let result =
                    if won then Some Won
                    elif attemptsLeft <= 0 then Some Lost
                    else None
                { state with
                    Guesses = state.Guesses @ [ newGuess ]
                    Input = ""
                    AttemptsLeft = attemptsLeft
                    Result = result
                    Error = None }
        | Dismiss -> state

let isFinished (state: State) = state.Result.IsSome

let didWin (state: State) =
    match state.Result with
    | Some Won -> true
    | _ -> false

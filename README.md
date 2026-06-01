# ScreenPal

A desktop pet built with F# / .NET 10, using Avalonia + FuncUI + Elmish.

A pixel-art cat lives on your screen. Feed it, let it sleep, play minigames with it, or just watch it stare back at you. The cat keeps living even when the app is closed — its hunger, energy, and happiness keep ticking down while you are away.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Verify with: `dotnet --version` (should show `10.x.x`)

### Build

```powershell
dotnet build
```

### Run

```powershell
# Windows / Unix / macOS
dotnet run
```


## How to Play

### The Floating Pet

Clicking on "To Cat" opens a small transparent window to the corner of your screen. Click it to open the main window again. Drag it to reposition.

### Stats

Your cat has three stats, each in the range 0–100:

| Stat | Behavior |
|------|----------|
| Hunger | Decays while awake. Raised by `Feed`. |
| Energy | Decays while awake, recovers while asleep. Spent on minigames. |
| Happiness | Decays over time. Boosted by winning minigames. |

If any stat stays below the critical threshold (10) for too long, the cat dies.

### Actions

- **Feed** — Opens roulette where you randomly get selected a food to eat. Each food gives different amount of hunger to the pet. 
- **Sleep** — Recovers energy. While sleeping, the pet cannot take any other actions. 
- **Play** — opens the minigame menu. You can choose between Memory Match and Word Guess. 

## Minigames

### Memory Match

A grid of 12 face-down cards hides 6 matching pairs of food types. On each turn you flip two cards:

- If the two symbols **match**, they stay face-up.
- If they **don't match**, they flip back over after a short delay.

Revealing a pair costs one try either way. You win by clearing all 6 pairs before your tries run out. If tries hit zero first, you lose.

### Word Guess

Guess a hidden 5-letter word, Wordle-style. Type 5 letters and press enter (or click Submit). Each letter of the guess is colored by how it compares to the answer:

- **green** — correct letter in the correct spot
- **yellow** — letter is in the word, but in a different spot
- **gray** — letter is not in the word

You win by guessing the word before your attempts run out.

### Rewards

Rewards apply when you finish a game (a win or a loss). On finish the cat spends 15 energy, and its happiness changes by:

- **Win** → +35 happiness
- **Loss** → +15 happiness (cat still had fun)

**Quitting** a game before it finishes costs no energy and grants no happiness.

### Dialogue

The cat shows speech bubbles on the home screen, during meals, and inside minigames.

### New Game

Clicking `New Game` shows pop-up to reset game. Clicking `Yes, Reset` clears all data and opens a new game. 

### Exit

Clicking `Exit` quits the game. Game data still saves and progress still continues, so re-opening the game will have offline stats affected. Be careful to make sure your pet doesn't die! 

## Save Data

State is written to `%APPDATA%/ScreenPal/state.json` after every change. Tick catch-up runs at startup, so closing and reopening the app still ages your pet by however long you were away. "New Game" wipes the current pet and starts over.

## Project Structure

```
ScreenPal/
├── ScreenPal.fsproj    # .NET 10 F# project file
├── README.md
├── Domain.fs           # Core types and tuning constants
├── Logic.fs            # Pure stat/mood/tick functions
├── Persistence.fs      # JSON save/load to %APPDATA%/ScreenPal/state.json
├── Words.fs            # Word list for the word minigame
├── WordGame.fs         # Word Guess state machine
├── MemoryGame.fs       # Memory Match state machine
├── App.fs              # Elmish model, messages, update, timer subscriptions
├── View.fs             # FuncUI views (pet window, main, play menu, minigames)
├── Program.fs          # Avalonia app entry point and window wiring
└── assets/             # Sprite sheets and the Press Start 2P pixel font
```

## Key Types

```fsharp
// Three stats in the range 0–100, all decaying over time
type Stats = { Hunger: int; Energy: int; Happiness: int }

// Mood is derived from current stats
type Mood = Happy | Neutral | Sad

// The pet is alive until critical stats persist too long
type Life = Alive | Dead

// Awake stats decay; asleep ones recover energy
type Sleep = Awake | Asleep
```

## Rules Summary

- Hunger, Energy, and Happiness all decay while the cat is awake.
- Energy recovers only while the cat is asleep.
- Any stat staying below 10 for too long causes the pet to die.
- Finishing a minigame costs energy; winning gives more happiness, losing still gives a smaller amount.
- The save file is updated on every state change; the pet ages even while the app is closed.
- "New Game" deletes the current pet and starts fresh.

## Changes from the Proposal

The finalized game keeps every requirement from the original proposal, but the following choices ended up different or expanded during implementation.

### Two windows instead of one
The proposal described a single Avalonia window. The final app uses a small transparent floating movable pet window to the corner of the screen plus a separate main window opened by clicking the floater. I added this floating pet so the cat feels like a real desktop companion.

### Persistent save + offline aging
The proposal had time advance only while the main pet view is open and ScreenPal is alive. The implementation persists state to `%APPDATA%/ScreenPal/state.json` after every change and runs a tick catch-up at startup so the cat ages during the time the app was closed. I added this to make the pet feel continuous across sessions rather than resetting each launch.

### Feeding became a roulette system
The proposal said the Feed button would raise hunger toward full. In the final game, pressing Feed opens a spinning food selector with eight foods that give different amounts of hunger. The user spins the roulette and eats whatever it lands on. I added this to increase variety as simply clicking to increase hunger was not very entertaining. 

### Speech-bubble dialogue system
The cat now shows random speech bubbles on the home screen, during meals, and inside minigames — including gameplay tips for first-time players. This was added to give the pet personality and to teach the user the rules without a separate tutorial.


## AI Usage

I designed the game myself, and used an LLM as a typing assistant for the mechanical parts around that design.

### What I used the LLM for
- **Turning layout sketches into UI code.** Once I decided what a screen should show and roughly where each element should sit, I described it to the LLM and let it write the actual UI code, so I did not have to memorize the exact name of every button, panel, and layout property.
- **Loading and slicing the cat sprite images.** The helper that opens a sprite-sheet image and cuts it into the individual animation frames is the kind of repetitive code I described in one sentence and had the LLM write.
- **Background plumbing.** The timers that drive the game (stat decay, animation, sleep recovery), the small delays for things like flipping memory cards back over or spinning the food wheel, and the code that reads and writes the save file were all generated from a short description of what each one had to do.

### What I had to manually change or re-prompt
The biggest recurring issue was outdated Avalonia / FuncUI APIs. The first draft of almost any view used property or constructor names from older versions. I had to reprompt with the exact compiler error and the actual current API before it would settle on something that built.

I also had to fix **F# file-order issues** by hand. The LLM would sometimes call a function defined later in the same file or in a file that came after the current one in `ScreenPal.fsproj`. F# is strict about declaration order, so I had to either reorder the `<Compile Include="..."/>` entries or move the function myself.

### The main thing the LLM could not do correctly
**Visual and feel polish.** Anything that needed me to actually look at the running app such as the sprite alignment inside the dialogue layout, the easing curve on the food roulette so the spin feels good, and the timing of the memory-card unflip delay could not be solved by prompting. The LLM would produce code that compiled and ran but looked wrong, and I had to iterate on the numbers (sizes, durations, paddings, z-order) by running the game and tweaking until it felt right.

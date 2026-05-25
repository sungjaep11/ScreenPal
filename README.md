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

A small transparent window pins to the bottom-right of your screen. Click it to open the main window. Drag it to reposition.

### Stats

Your cat has three stats, each in the range 0–100:

| Stat | Behavior |
|------|----------|
| Hunger | Decays while awake. Raised by `Feed`. |
| Energy | Decays while awake, recovers while asleep. Spent on minigames. |
| Happiness | Decays over time. Boosted by winning minigames. |

If any stat stays below the critical threshold (10) for too long, the cat dies.

### Actions

- **Feed** — raises hunger.
- **Sleep** — recovers energy faster than the awake rate.
- **Play** — opens the minigame menu.

### Dialogue

The cat pipes up speech bubbles on the home screen, during meals, and inside minigames.

## Minigames

| Minigame | Description |
|----------|-------------|
| Memory Match | Flip pairs of emoji cards. Number of allowed tries scales with current stats. |
| Word Guess | Five-letter Wordle-style guesser. Attempts allowed scale with stats. |

Winning boosts happiness; losing costs a little. Each attempt spends energy.

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

## Module Overview

| Module | Responsibility |
|--------|----------------|
| Domain | Core types (`Stats`, `Mood`, `Life`, `Sleep`) and tuning constants (decay rates, thresholds, tick interval). |
| Logic | Pure functions for stat updates, mood derivation, and per-tick aging. |
| Persistence | JSON read/write of saved state under `%APPDATA%/ScreenPal/`. |
| Words | Static word list used by the Word Guess minigame. |
| WordGame | State machine for the five-letter word minigame. |
| MemoryGame | State machine for the emoji card-matching minigame. |
| App | Elmish `init` / `update` / messages, plus timer subscriptions that drive ticks. |
| View | FuncUI views: floating pet, main window, play menu, minigames, sakura overlay. |
| Program | Avalonia app entry point and window wiring. |

## Rules Summary

- Hunger, Energy, and Happiness all decay while the cat is awake.
- Energy recovers only while the cat is asleep.
- Any stat staying below 10 for too long causes the pet to die.
- Minigames cost energy; winning gives happiness, losing takes a smaller amount away.
- The save file is updated on every state change; the pet ages even while the app is closed.
- "New Game" deletes the current pet and starts fresh.

## Changes from the Proposal

The finalized game keeps every requirement from the original proposal, but the following choices ended up different or expanded during implementation.

### Two windows instead of one
The proposal described a single Avalonia window. The final app uses a small transparent floating pet window pinned to the bottom-right of the screen plus a separate main window opened by clicking the floater. The floating pet is always present so the cat feels like a real desktop companion; the main window is summoned only when the user wants to interact with stats or minigames.

### Persistent save + offline aging
The proposal had time advance only while the main pet view is open and ScreenPal is alive. The implementation persists state to `%APPDATA%/ScreenPal/state.json` after every change and runs a tick catch-up at startup so the cat ages during the time the app was closed. This makes the pet feel continuous across sessions rather than resetting each launch.

### Feeding became a "Food Roulette"
The proposal said the Feed button would raise hunger toward full. In the final game, pressing Feed opens a spinning food selector with eight foods that give different amounts of hunger. The user spins the roulette and eats whatever it lands on. This adds variety and a small element of chance to feeding without changing its core role.

### Speech-bubble dialogue system
The cat now shows random speech bubbles on the home screen, during meals, and inside minigames — including gameplay tips for first-time players. This was added to give the pet personality and to teach the user the rules without a separate tutorial.

### Background music and sound effects
The app now plays a looping background music track while running, a spinning-wheel sound effect when the food roulette is spinning, and a click sound effect on button presses. This was added to make the game feel more alive and to give immediate audio feedback for the two most interactive moments.


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

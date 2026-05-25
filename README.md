# ScreenPal

A desktop pet written in F# with Avalonia + FuncUI + Elmish.

A pixel-art cat lives on your screen. Feed it, let it sleep, play minigames with it, or just watch it stare back at you. The cat keeps living even when the app is closed. Take care of it!

## Running

Requires the .NET 10 SDK.

```powershell
dotnet run
```

## Gameplay

- **Floating pet** — a small transparent window pinned to the bottom-right of your screen. Click it to open the main window; drag to reposition.
- **Stats** — `Hunger`, `Energy`, `Happiness` (0–100). All three decay over time while awake. If any stays critical (below 10) for too long, the pet dies.
- **Actions** — `Feed` raises hunger; `Sleep` recovers energy faster; `Play` opens the minigame menu.
- **Dialogue** — the cat pipes up speech bubbles on the home screen, during meals, and inside minigames.

## Minigames

- **Memory Match** — flip pairs of emoji cards. Number of allowed tries scales with current stats. Winning boosts happiness; losing costs a little.
- **Word Guess** — five-letter Wordle-style guesser. Attempts allowed scale with stats.

## Project layout

| File | Purpose |
|------|---------|
| `Domain.fs` | Core types and tuning constants (stat decay rates, thresholds, intervals). |
| `Logic.fs` | Pure stat/mood/tick functions. |
| `Persistence.fs` | JSON save/load to `%APPDATA%/ScreenPal/state.json`. |
| `Words.fs` | Word list for the word minigame. |
| `MemoryGame.fs` / `WordGame.fs` | Minigame state machines. |
| `App.fs` | Elmish model, messages, update, and timer subscriptions. |
| `View.fs` | FuncUI views (pet window, main, play menu, minigames, sakura overlay). |
| `Program.fs` | Avalonia app entry point and window wiring. |
| `assets/` | Sprite sheets and the Press Start 2P pixel font. |

## Save data

State is written to `%APPDATA%/ScreenPal/state.json` after every change. Tick catch-up runs at startup, so closing and reopening the app still ages your pet by however long you were away. "New Game" wipes the current pet and starts over.

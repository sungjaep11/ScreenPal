module ScreenPal.Logic

open ScreenPal.Domain

let clamp value = max MinStat (min MaxStat value)

let isCritical stats =
    stats.Hunger <= CriticalThreshold
    || stats.Energy <= CriticalThreshold
    || stats.Happiness <= CriticalThreshold

let mood stats =
    if isCritical stats then Sad
    elif stats.Happiness <= SadThreshold then Sad
    elif stats.Happiness >= HappyThreshold then Happy
    else Neutral

let tickAwake stats =
    { Hunger = clamp (stats.Hunger - HungerDecayPerTick)
      Energy = clamp (stats.Energy - EnergyDecayPerTickAwake)
      Happiness = clamp (stats.Happiness - HappinessDecayPerTick) }

let tickAsleep stats =
    { Hunger = clamp (stats.Hunger - HungerDecayPerTick)
      Energy = clamp (stats.Energy + EnergyRecoveryPerTickAsleep)
      Happiness = clamp (stats.Happiness - HappinessDecayPerTick) }

let feed stats =
    { stats with Hunger = clamp (stats.Hunger + FeedHungerGain) }

let canFeed life sleep stats =
    life = Alive && sleep = Awake && stats.Hunger < MaxStat

let canToggleSleep life =
    life = Alive

let canPlayMinigame life sleep =
    life = Alive && sleep = Awake

let memoryTriesFor stats =
    let bonus = (stats.Happiness + stats.Energy) / 40
    8 + bonus

let wordAttemptsFor stats =
    let bonus = (stats.Happiness + stats.Energy) / 50
    5 + bonus

let applyMinigameResult won stats =
    let happinessGain =
        if won then MinigameWinHappiness else MinigameLossHappiness
    { stats with
        Energy = clamp (stats.Energy - MinigameEnergyCost)
        Happiness = clamp (stats.Happiness + happinessGain) }

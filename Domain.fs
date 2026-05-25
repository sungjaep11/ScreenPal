module ScreenPal.Domain

let MaxStat = 100
let MinStat = 0

let InitialHunger = 60
let InitialEnergy = 60
let InitialHappiness = 60

let HungerDecayPerTick = 2
let EnergyDecayPerTickAwake = 1
let EnergyRecoveryPerTickAsleep = 20
let HappinessDecayPerTick = 2

let CriticalThreshold = 10
let DeathAfterCriticalTicks = 24

let HappyThreshold = 80
let SadThreshold = 25

let FeedHungerGain = 25
let MinigameEnergyCost = 15
let MinigameWinHappiness = 35
let MinigameLossHappiness = 15

let TickIntervalMs = 1800000.0

type Stats = { Hunger: int; Energy: int; Happiness: int }

type Mood = Happy | Neutral | Sad

type Life = Alive | Dead

type Sleep = Awake | Asleep

let initialStats =
    { Hunger = InitialHunger
      Energy = InitialEnergy
      Happiness = InitialHappiness }

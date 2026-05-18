module ScreenPal.Words

let words =
    [| "apple"; "beach"; "bread"; "brick"; "brush"; "candy"; "chair"; "chess"
       "cloud"; "crane"; "dance"; "drink"; "earth"; "eagle"; "field"; "flame"
       "flute"; "fruit"; "ghost"; "grape"; "green"; "heart"; "honey"; "horse"
       "house"; "juice"; "knife"; "lemon"; "light"; "lunch"; "magic"; "mango"
       "money"; "mouse"; "music"; "night"; "ocean"; "panda"; "paper"; "peach"
       "piano"; "pizza"; "plant"; "queen"; "river"; "robot"; "salad"; "shark"
       "sheep"; "shoes"; "skill"; "smile"; "snake"; "snowy"; "solar"; "spice"
       "stone"; "storm"; "sugar"; "swing"; "table"; "tiger"; "toast"; "train"
       "tulip"; "vivid"; "water"; "whale"; "wheel"; "world"; "zebra"; "happy"
       "lucky"; "merry"; "peace"; "dream"; "cloud"; "sunny"; "berry"; "candy"
       "fairy"; "honey"; "jolly"; "novel"; "party"; "puppy"; "sweet"; "treat" |]

let pick (rng: System.Random) =
    words.[rng.Next(words.Length)]

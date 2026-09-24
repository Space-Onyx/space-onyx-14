# Content below taken from Goob Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
reagent-effect-condition-guidebook-total-damage =
    { $max ->
        [2147483648] тело имеет по крайней мере { NATURALFIXED($min, 2) } общего урона
       *[other]
            { $min ->
                [0] имеет не более { NATURALFIXED($max, 2) } общего урона
               *[other] имеет между { NATURALFIXED($min, 2) } и { NATURALFIXED($max, 2) } общего урона
            }
    }

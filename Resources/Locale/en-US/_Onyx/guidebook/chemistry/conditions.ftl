# Content below taken from Goob Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
reagent-effect-condition-guidebook-total-damage =
    { $max ->
        [2147483648] it has at least { NATURALFIXED($min, 2) } total damage
       *[other]
            { $min ->
                [0] it has at most { NATURALFIXED($max, 2) } total damage
               *[other] it has between { NATURALFIXED($min, 2) } and { NATURALFIXED($max, 2) } total damage
            }
    }

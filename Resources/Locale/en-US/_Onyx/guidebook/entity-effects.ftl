entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Suppresses
        *[other] suppress
    } { NATURALFIXED($amount, 2) } pain. Repeated doses stack; the effect fades over { NATURALFIXED($duration, 2) } { MANY("second", $duration) }.

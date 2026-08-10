entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Подавляет
        *[other] подавить
    } { NATURALFIXED($amount, 2) } ед. боли и ускоряет естественное восстановление боли максимум в { NATURALFIXED($recoveryMultiplier, 2) } раза. Повторные дозы складываются; эффект проходит за { NATURALFIXED($duration, 2) } { $duration ->
        [one] секунды
        [few] секунд
        *[other] секунд
    }.

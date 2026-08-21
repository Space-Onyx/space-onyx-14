entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Подавляет
        *[other] подавить
    } { NATURALFIXED($amount, 2) } ед. боли и ускоряет естественное восстановление боли максимум в { NATURALFIXED($recoveryMultiplier, 2) } раза. Повторные дозы складываются; эффект проходит за { NATURALFIXED($duration, 2) } { $duration ->
        [one] секунды
        [few] секунд
        *[other] секунд
    }.

entity-effect-guidebook-mend-fractures =
    { $chance ->
        [1] Снижает
        *[other] снизить
    } тяжесть подходящих переломов на { NATURALFIXED($amount, 2) } за метаболический тик. Типы: { $wounds }. Степени: от «{ $minimumGrade }» до «{ $maximumGrade }» включительно.

entity-effect-guidebook-all-fractures = все переломы

fracture-grade-hairline = трещина
fracture-grade-simple = простой перелом
fracture-grade-displaced = перелом со смещением
fracture-grade-comminuted = оскольчатый перелом

metabolism-stage-cybernetic-bloodstream = кибернетический поток
metabolism-stage-cybernetic-metabolites = метаболиты кибернетического потока
entity-effect-guidebook-circulatory-stream-modify-bleed = {$sign ->
    [ -1 ] Уменьшает кровотечение потока {$stream} на {$amount}
    [ 1 ] Усиливает кровотечение потока {$stream} на {$amount}
    *[other] Изменяет кровотечение потока {$stream} на {$amount}
}
entity-condition-guidebook-circulatory-stream = { $shouldhave ->
    [true] имеет поток {$stream}
    *[false] не имеет потока {$stream}
}
entity-effect-guidebook-circulatory-stream-wrapper = Действует на поток {$stream} — { $effect }

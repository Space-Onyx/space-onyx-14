ent-TapeRecorder = магнитофон
    .desc = Всё, что вы скажете в это устройство, может и будет использовано против вас в космическом суде.
ent-TapeRecorderFilled = { ent-TapeRecorder }
    .suffix = Записанный
    .desc = { ent-TapeRecorder.desc }
ent-CassetteTape = кассетная лента
    .desc = Магнитная лента, способная хранить до двух минут записи с каждой стороны.
ent-CassetteTapeInterview = { ent-CassetteTape }
    .suffix = Интервью с Гарри Смошем.
    .desc = { ent-CassetteTape.desc }
ent-TapeRecorderTranscript = расшифровка записи
    .desc = { ent-Paper.desc }
ent-TapeDeck = магнитофон
    .desc = Винтажный кассетный магнитофон, готовый воспроизводить свежие подкасты экипажа.
ent-TapeDeckCircuitboard = магнитофон (машинная плата)
    .desc = Машинная печатная плата для кассетного магнитофона.

cassette-repair-start = Вы начинаете перематывать плёнку обратно в { $item }.
cassette-repair-finish = Вам удаётся перемотать плёнку обратно в { $item }.
tape-cassette-position = Плёнка перемотана примерно на [color=green]{ $position }%[/color].
tape-cassette-damaged = Плёнка размотана, используйте ручку или отвёртку для починки.
tape-recorder-playing = Диктофон находится в режиме [color=green]воспроизведения[/color].
tape-recorder-stopped = Диктофон остановлен.
tape-recorder-empty = В Диктофоне нет кассеты.
tape-recorder-recording = Диктофон находится в режиме [color=red]записи[/color].
tape-recorder-rewinding = Диктофон находится в режиме [color=yellow]перемотки[/color].
tape-recorder-locked = Невозможно извлечь кассету во время работы Диктофона.
tape-recorder-voice-unknown = Неизвестный голос
tape-recorder-voice-unintelligible = Невнятная речь
tape-recorder-message-corruption = #
tape-recorder-menu-title = Диктофон
tape-recorder-menu-controls-label = Управление:
tape-recorder-menu-stopped-button = Пауза
tape-recorder-menu-recording-button = Запись
tape-recorder-menu-playing-button = Воспроизведение
tape-recorder-menu-rewinding-button = Перемотка
tape-recorder-menu-print-button = Распечатать расшифровку
tape-recorder-menu-cassette-label = Кассета: { $cassetteName }
tape-recorder-menu-no-cassette-label = Кассета не вставлена
tape-recorder-print-start-text = [bold]Начало записи[/bold]
tape-recorder-print-message-text = [bold][{ $time }] { $source }:[/bold] { $message }
tape-recorder-print-end-text = [bold]Конец записи[/bold]

health-examinable-pain-light = [color=yellow]болит[/color]
health-examinable-pain-strong = [color=orange]сильно болит[/color]
health-examinable-pain-terrible = [color=red]ужасно болит[/color]
health-examinable-pain-agony = [color=crimson]в агонии[/color]
health-examinable-part-title-self = [font size=11][color=DarkGray]Вы осматриваете себя на наличие повреждений.[/color][/font]
health-examinable-part-title-other = [font size=11][color=DarkGray]Вы осматриваете { $entity } на наличие повреждений.[/color][/font]
health-examinable-part-border = [color=Gray]────────────────────────[/color]
health-examinable-part-summary = [bold]{ CAPITALIZE($part) }[/bold]: { $severity }
health-examinable-part-summary-pain = [bold]{ CAPITALIZE($part) }[/bold]: { $severity }, { $pain }
health-examinable-part-chat-line = [font size=10]{ $summary }[/font]
health-examinable-part-chat-line-details = [font size=10]{ $summary }
    { "    " }[color=Gray]{ $details }[/color][/font]
health-examinable-part-damage = { $type }
health-examinable-part-severity-none = [color=green]в порядке[/color]
health-examinable-part-severity-minor = [color=yellow]слегка повреждена[/color]
health-examinable-part-severity-moderate = [color=orange]повреждена[/color]
health-examinable-part-severity-severe = [color=red]сильно повреждена[/color]
health-examinable-part-severity-critical = [color=crimson]изувечена[/color]
health-examinable-part-wound-stabilized = { $count } { $count ->
    [one] стабилизированная рана
    [few] стабилизированные раны
   *[other] стабилизированных ран
}
health-examinable-part-wound-closed = { $count } { $count ->
    [one] закрытая рана
    [few] закрытые раны
   *[other] закрытых ран
}
health-examinable-part-incision-open = { $count } { $count ->
    [one] открытый разрез
    [few] открытых разреза
   *[other] открытых разрезов
}
health-examinable-part-bleeding = активное кровотечение
health-examinable-part-fracture-hairline = трещина кости
health-examinable-part-fracture-simple = простой перелом
health-examinable-part-fracture-displaced = перелом со смещением
health-examinable-part-fracture-comminuted = оскольчатый перелом
health-examinable-part-scars = { $count } { $count ->
    [one] шрам
    [few] шрама
   *[other] шрамов
}

health-examinable-pain-light = [color=yellow]hurts[/color]
health-examinable-pain-strong = [color=orange]hurts badly[/color]
health-examinable-pain-terrible = [color=red]hurts terribly[/color]
health-examinable-pain-agony = [color=crimson]is in agony[/color]
health-examinable-part-title-self = [font size=11][color=DarkGray]You check yourself for injuries.[/color][/font]
health-examinable-part-title-other = [font size=11][color=DarkGray]You check { $entity } for injuries.[/color][/font]
health-examinable-part-border = [color=Gray]────────────────────────[/color]
health-examinable-part-summary = [bold]{ CAPITALIZE($part) }[/bold]: { $severity }
health-examinable-part-summary-pain = [bold]{ CAPITALIZE($part) }[/bold]: { $severity }, { $pain }
health-examinable-part-chat-line = [font size=10]{ $summary }[/font]
health-examinable-part-chat-line-details = [font size=10]{ $summary }
    { "    " }[color=Gray]{ $details }[/color][/font]
health-examinable-part-damage = { $type }
health-examinable-part-severity-none = [color=green]fine[/color]
health-examinable-part-severity-minor = [color=yellow]slightly damaged[/color]
health-examinable-part-severity-moderate = [color=orange]damaged[/color]
health-examinable-part-severity-severe = [color=red]badly damaged[/color]
health-examinable-part-severity-critical = [color=crimson]mangled[/color]
health-examinable-part-wound-open = { $count } open { $count ->
    [one] wound
   *[other] wounds
}
health-examinable-part-wound-stabilized = { $count } stabilized { $count ->
    [one] wound
   *[other] wounds
}
health-examinable-part-wound-closed = { $count } closed { $count ->
    [one] wound
   *[other] wounds
}
health-examinable-part-incision-open = { $count } open { $count ->
    [one] incision
   *[other] incisions
}
health-examinable-part-bleeding = active bleeding
health-examinable-part-fracture-hairline = hairline fracture
health-examinable-part-fracture-simple = simple fracture
health-examinable-part-fracture-displaced = displaced fracture
health-examinable-part-fracture-comminuted = comminuted fracture
health-examinable-part-scars = { $count } { $count ->
    [one] scar
   *[other] scars
}

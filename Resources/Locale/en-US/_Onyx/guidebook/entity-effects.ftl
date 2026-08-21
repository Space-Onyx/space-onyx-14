entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Suppresses
        *[other] suppress
    } { NATURALFIXED($amount, 2) } pain and multiplies natural pain recovery by up to { NATURALFIXED($recoveryMultiplier, 2) }. Repeated doses stack; the effect fades over { NATURALFIXED($duration, 2) } { MANY("second", $duration) }.

entity-effect-guidebook-mend-fractures =
    { $chance ->
        [1] Reduces
        *[other] reduce
    } matching fracture severity by { NATURALFIXED($amount, 2) } per metabolism tick. Types: { $wounds }. Grades: “{ $minimumGrade }” through “{ $maximumGrade }”, inclusive.

entity-effect-guidebook-all-fractures = all fractures

fracture-grade-hairline = hairline
fracture-grade-simple = simple
fracture-grade-displaced = displaced
fracture-grade-comminuted = comminuted

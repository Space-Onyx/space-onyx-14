interaction-LookAt-name = Stare
interaction-LookAt-description = Stare into the void and see it stare back.
interaction-LookAt-success-self-popup = You stare at {THE($target)}.
interaction-LookAt-success-target-popup = You feel {THE($user)} staring at you...
interaction-LookAt-success-others-popup = {THE($user)} stares at {THE($target)}.

interaction-Hug-name = Hug
interaction-Hug-description = A hug a day keeps the psychological horrors beyond your comprehension away.
interaction-Hug-success-self-popup = You hug {THE($target)}.
interaction-Hug-success-target-popup = {THE($user)} hugs you.
interaction-Hug-success-others-popup = {THE($user)} hugs {THE($target)}.

# <Onyx-InteractionVerbs>
interaction-Kiss-name = Kiss
interaction-Kiss-description = Give the target a kiss.
interaction-Kiss-success-self-popup = You kiss {THE($target)}.
interaction-Kiss-success-target-popup = {THE($user)} kisses you.
interaction-Kiss-success-others-popup = {THE($user)} kisses {THE($target)}.

interaction-StrongHug-name = Hug tightly
interaction-StrongHug-description = Give the target a warm, tight hug.
interaction-StrongHug-success-self-popup = You hug {THE($target)} tightly.
interaction-StrongHug-success-target-popup = {THE($user)} hugs you tightly.
interaction-StrongHug-success-others-popup = {THE($user)} hugs {THE($target)} tightly.

interaction-Slap-name = Slap
interaction-Slap-description = Give the target a sharp slap.
interaction-Slap-success-self-popup = You slap {THE($target)}.
interaction-Slap-success-target-popup = {THE($user)} slaps you.
interaction-Slap-success-others-popup = {THE($user)} slaps {THE($target)}.

interaction-Pet-name = Pat head
interaction-Pet-description = Gently pat the target on the head.
interaction-Pet-success-self-popup = You pat {THE($target)} on the head.
interaction-Pet-success-target-popup = {THE($user)} pats you on the head.
interaction-Pet-success-others-popup = {THE($user)} pats {THE($target)} on the head.

interaction-Handshake-name = Shake hands
interaction-Handshake-description = Offer the target a firm handshake.
interaction-Handshake-success-self-popup = You shake hands with {THE($target)}.
interaction-Handshake-success-target-popup = {THE($user)} shakes hands with you.
interaction-Handshake-success-others-popup = {THE($user)} shakes hands with {THE($target)}.

interaction-HighFive-name = High five
interaction-HighFive-description = Give the target a high five.
interaction-HighFive-success-self-popup = You high-five {THE($target)}.
interaction-HighFive-success-target-popup = {THE($user)} high-fives you.
interaction-HighFive-success-others-popup = {THE($user)} high-fives {THE($target)}.
# </Onyx-InteractionVerbs>

interaction-KnockOn-name = Knock
interaction-KnockOn-description = Knock on the target to attract attention.
interaction-KnockOn-success-self-popup = You knock on {THE($target)}.
interaction-KnockOn-success-target-popup = {THE($user)} knocks on you.
interaction-KnockOn-success-others-popup = {THE($user)} knocks on {THE($target)}.

# The below includes conditionals for if the user is holding an item
interaction-WaveAt-name = Wave at
interaction-WaveAt-description = Wave at the target. If you are holding an item, you will wave it.
interaction-WaveAt-success-self-popup = You wave {$hasUsed ->
    [false] at {THE($target)}.
    *[true] your {$used} at {THE($target)}.
}
interaction-WaveAt-success-target-popup = {THE($user)} waves {$hasUsed ->
    [false] at you.
    *[true] {POSS-PRONOUN($user)} {$used} at you.
}
interaction-WaveAt-success-others-popup = {THE($user)} waves {$hasUsed ->
    [false] at {THE($target)}.
    *[true] {POSS-PRONOUN($user)} {$used} at {THE($target)}.
}

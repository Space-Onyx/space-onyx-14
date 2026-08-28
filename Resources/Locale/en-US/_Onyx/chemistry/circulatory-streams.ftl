metabolism-stage-cybernetic-bloodstream = cybernetic stream
metabolism-stage-cybernetic-metabolites = cybernetic metabolites
entity-effect-guidebook-circulatory-stream-modify-bleed = {$sign ->
    [ -1 ] Reduces {$stream} bleeding by {$amount}
    [ 1 ] Increases {$stream} bleeding by {$amount}
    *[other] Modifies {$stream} bleeding by {$amount}
}
entity-condition-guidebook-circulatory-stream = { $shouldhave ->
    [true] has {$stream} stream
    *[false] has no {$stream} stream
}
entity-effect-guidebook-circulatory-stream-wrapper = Applies to {$stream} stream — { $effect }

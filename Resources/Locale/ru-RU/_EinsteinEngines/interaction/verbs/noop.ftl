interaction-LookAt-name = Смотреть
interaction-LookAt-description = Посмотрите в пустоту, и почувствуйте, как она смотрит на вас.
interaction-LookAt-success-self-popup = Вы смотрите на { $target }.
interaction-LookAt-success-target-popup = Вы чувствуете, что { $user } смотрит на вас...
interaction-LookAt-success-others-popup = { $user } смотрит на { $target }.
interaction-Hug-name = Обнять
interaction-Hug-description = Обнимашки помогают справиться с экзистенциальными страхами.
interaction-Hug-success-self-popup = Вы обнимаете { $target }.
interaction-Hug-success-target-popup = { $user } обнимает вас.
interaction-Hug-success-others-popup = { $user } обнимает { $target }.

# <Onyx-InteractionVerbs>
interaction-Kiss-name = Поцеловать
interaction-Kiss-description = Поцеловать цель.
interaction-Kiss-success-self-popup = Вы целуете { $target }.
interaction-Kiss-success-target-popup = { $user } целует вас.
interaction-Kiss-success-others-popup = { $user } целует { $target }.

interaction-StrongHug-name = Крепко обнять
interaction-StrongHug-description = Заключить цель в крепкие и тёплые объятия.
interaction-StrongHug-success-self-popup = Вы крепко обнимаете { $target }.
interaction-StrongHug-success-target-popup = { $user } крепко обнимает вас.
interaction-StrongHug-success-others-popup = { $user } крепко обнимает { $target }.

interaction-Slap-name = Дать пощёчину
interaction-Slap-description = Наградить цель звонкой пощёчиной.
interaction-Slap-success-self-popup = Вы даёте пощёчину { $target }.
interaction-Slap-success-target-popup = { $user } даёт вам пощёчину.
interaction-Slap-success-others-popup = { $user } даёт пощёчину { $target }.
# </Onyx-InteractionVerbs>

interaction-Pet-name = Погладить
interaction-Pet-description = Погладьте коллегу, чтобы избавить его от стресса.
interaction-Pet-success-self-popup = Вы гладите { $target } по { POSS-ADJ($target) } голове.
interaction-Pet-success-target-popup = { $user } гладит вас по голове.
interaction-Pet-success-others-popup = { $user } гладит { $target }.

# <Onyx-InteractionVerbs>
interaction-Handshake-name = Пожать руку
interaction-Handshake-description = Обменяться с целью крепким рукопожатием.
interaction-Handshake-success-self-popup = Вы пожимаете руку { $target }.
interaction-Handshake-success-target-popup = { $user } пожимает вам руку.
interaction-Handshake-success-others-popup = { $user } пожимает руку { $target }.

interaction-HighFive-name = Дать пять
interaction-HighFive-description = Звонко хлопнуть цель по ладони.
interaction-HighFive-success-self-popup = Вы даёте пять { $target }.
interaction-HighFive-success-target-popup = { $user } даёт вам пять.
interaction-HighFive-success-others-popup = { $user } даёт пять { $target }.
# </Onyx-InteractionVerbs>
interaction-KnockOn-name = Постучать
interaction-KnockOn-description = Постучите по существу, чтобы привлечь внимание.
interaction-KnockOn-success-self-popup = Вы стучите по { $target }.
interaction-KnockOn-success-target-popup = { $user } стучит по вам.
interaction-KnockOn-success-others-popup = { $user } стучит по { $target }.
interaction-Rattle-name = Потрясти
interaction-Rattle-success-self-popup = Вы трясёте { $target }.
interaction-Rattle-success-target-popup = { $user } трясёт вас.
interaction-Rattle-success-others-popup = { $user } трясёт { $target }.
# The below includes conditionals for if the user is holding an item
interaction-WaveAt-name = Помахать
interaction-WaveAt-description = Помашите существу. Если вы держите предмет, то помашете им.
interaction-WaveAt-success-self-popup =
    Вы машете { $hasUsed ->
        [false] на { $target }.
       *[true] вашим { $used } на { $target }.
    }
interaction-WaveAt-success-target-popup =
    { $user } машет { $hasUsed ->
        [false] на вас.
       *[true] { POSS-PRONOUN($user) } { $used } на вас.
    }
interaction-WaveAt-success-others-popup =
    { $user } машет { $hasUsed ->
        [false] на { $target }.
       *[true] { POSS-PRONOUN($user) } { $used } на { $target }.
    }

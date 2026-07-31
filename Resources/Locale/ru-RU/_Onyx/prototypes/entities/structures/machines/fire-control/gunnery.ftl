gunnery-window-title = Управление артиллерией
gunnery-window-disconnected = ОТКЛЮЧЕНО
gunnery-window-connected = ПОДКЛЮЧЕНО
gunnery-select-all = Выбрать все
gunnery-unselect-all = Снять выделение
gunnery-guns = Орудия
gunnery-server-examine-detail = Сервер использует [color={ $valueColor }]{ $usedProcessingPower }/{ $processingPower }[/color] своей вычислительной мощности.
gunnery-select-ballistics = Баллистика
gunnery-select-energy = Энергия
gunnery-select-missiles = Ракеты
gunnery-select-mining = Копание
gunner-console-display-label = Дисплей
ship-gun-class-component-examine-detail = Это орудие требует [color=yellow]{ $processingPower }[/color] единиц вычислительной мощности сервера для управления.

ent-GunneryServerBase = сервер контроля артиллерийских орудий
    .desc = Управление дистанционным артиллерийским орудием шаттла.
ent-GunneryServerLow = маломощный сервер контроля артиллерийских орудий
    .desc = { ent-GunneryServerBase.desc }
ent-GunneryServerMedium = среднемощный сервер контроля артиллерийских орудий
    .desc = { ent-GunneryServerBase.desc }
ent-GunneryServerHigh = мощный сервер контроля артиллерийских орудий
    .desc = { ent-GunneryServerBase.desc }
ent-GunneryServerUltra = ультра мощный сервер контроля артиллерийских орудий
    .desc = { ent-GunneryServerBase.desc }
ent-ComputerGunneryConsole = консоль контроля артиллерийских орудий
    .desc = Интерфейс с контролем артиллерийских орудий для управления вооружением.

ent-GunneryControlComputerCircuitboard = консоль контроля артиллерии (консольная плата)
    .desc = Консольная плата для консоли контроля артиллерии.
ent-MachineGCSLowCircuitboard = маломощный сервер контроля артиллерийских орудий (машинная плата)
    .desc = Машинная плата для сервера космических артиллерийских орудий.
    .suffix = { ent-BaseMachineCircuitboard.suffix }
ent-MachineGCSMediumCircuitboard = среднемощный сервер контроля артиллерийских орудий (машинная плата)
    .desc = Машинная плата для сервера космических артиллерийских орудий.
    .suffix = { ent-BaseMachineCircuitboard.suffix }
ent-MachineGCSHighCircuitboard = мощный сервер контроля артиллерийских орудий (машинная плата)
    .desc = Машинная плата для сервера космических артиллерийских орудий.
    .suffix = { ent-BaseMachineCircuitboard.suffix }
ent-MachineGCSUltraCircuitboard = ультра мощный сервер контроля артиллерийских орудий (машинная плата)
    .desc = Машинная плата для сервера космических артиллерийских орудий.
    .suffix = { ent-BaseMachineCircuitboard.suffix }

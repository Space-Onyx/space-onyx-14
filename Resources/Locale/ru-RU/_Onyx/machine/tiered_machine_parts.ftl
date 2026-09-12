# Space Onyx
# Copyright (C) 2026 Space Onyx contributors
#
# This file is licensed under AGPL-3.0-or-later.
# See LICENSES for the full license text.

tiered-machine-part-examine = Уровень эффективности: [color=cyan]{ $tier }[/color].
tiered-machine-part-panel-closed = Сначала откройте панель техобслуживания.
tiered-machine-part-unsupported = Эта машина не поддерживает данный модуль.
tiered-machine-part-not-better = Установленный модуль не хуже этого.
tiered-machine-part-installed = Установлен модуль { $tier } уровня.
tiered-machine-part-installed-examine = Установлен модуль «{ $kind }»: [color=cyan]{ $tier } уровень[/color].
tiered-machine-part-kind-servo = микроманипулятор
tiered-machine-part-kind-capacitor = конденсатор
tiered-machine-part-kind-matterbin = контейнер материи
tiered-machine-part-kind-scanner = cканирующий модуль
tiered-machine-part-kind-laser = микро-лазер
machine-part-exchanger-nothing = Подходящих модулей лучшего уровня не найдено.
machine-part-exchanger-complete = Модули машины улучшены.
tiered-machine-frame-missing = Не хватает обязательных модулей: { $amount }× { $kind }.
machine-upgrade-bluespace-mining-speed = скорость добычи
machine-upgrade-bluespace-mining-output = объём добычи
machine-upgrade-bluespace-mining-core-life = срок службы ядра
machine-upgrade-bluespace-mining-stability = подавление нестабильности
machine-upgrade-bluespace-mining-temperature = подавление температурных возмущений

stack-servo-drive = микроманипуляторы
stack-matter-recycler = контейнеры материи
stack-capacitor-module = конденсаторы
stack-scanner-module = сканирующие модули
stack-laser-module = микро-лазеры

ent-StandardServoDrive = микроманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
ent-CalibratedServoDrive = наноманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
ent-PrecisionServoDrive = пикоманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
ent-QuantumServoDrive = фемтоманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
ent-StandardMatterRecycler = контейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
ent-CalibratedMatterRecycler = улучшенный контейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
ent-PrecisionMatterRecycler = суперконтейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
ent-BluespaceMatterRecycler = блюспейс-контейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
ent-StandardCapacitorModule = конденсатор
    .desc = Базовый конденсатор, используемый при сборке различных устройств.
ent-CalibratedCapacitorModule = улучшенный конденсатор
    .desc = Улучшенный конденсатор, используемый при сборке различных устройств.
ent-PrecisionCapacitorModule = суперконденсатор
    .desc = Конденсатор сверхвысокой ёмкости, используемый при сборке различных устройств.
ent-QuadraticCapacitorModule = квадратичный конденсатор
    .desc = Квадратичный конденсатор, используемый при сборке различных устройств.
ent-StandardScannerModule = сканирующий модуль
    .desc = Компактный сканирующий модуль высокого разрешения для сборки некоторых устройств.
ent-CalibratedScannerModule = улучшенный сканирующий модуль
    .desc = Компактный сканирующий модуль высокого разрешения для сборки некоторых устройств.
ent-PhasicScannerModule = фазовый сканирующий модуль
    .desc = Компактный фазовый сканирующий модуль высокого разрешения для сборки некоторых устройств.
ent-TriphasicScannerModule = трифазовый сканирующий модуль
    .desc = Компактный трифазовый сканирующий модуль ультраразрешения для сборки некоторых устройств.
ent-StandardLaserModule = микролазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
ent-FocusedLaserModule = мощный микролазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
ent-UltrafineLaserModule = сверхмощный микролазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
ent-QuadphaseLaserModule = квадро-ультра микролазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
ent-MachinePartExchanger = обменник деталей машин
    .desc = Переносной инструмент для хранения и установки улучшенных модулей через открытую панель техобслуживания.
ent-BluespaceMachinePartExchanger = блюспейс-обменник деталей машин
    .desc = Продвинутый обменник модулей с дистанционной установкой через закрытые панели.
ent-BorgMachinePartExchanger = встроенный обменник деталей машин
    .desc = Обменник модулей, предназначенный для использования киборгами.
ent-BorgModuleMachineParts = модуль деталей машин
    .desc = Инженерный модуль со встроенным обменником деталей машин.

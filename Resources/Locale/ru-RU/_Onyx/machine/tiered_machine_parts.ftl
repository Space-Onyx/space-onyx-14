# Space Onyx
# Copyright (C) 2026 Space Onyx contributors
#
# This file is licensed under AGPL-3.0-or-later.
# See LICENSES for the full license text.

tiered-machine-part-examine = Уровень детали: [color=cyan]{ $tier }[/color].
tiered-machine-part-panel-closed = Сначала откройте панель техобслуживания.
tiered-machine-part-unsupported = Эта машина не поддерживает такую деталь.
tiered-machine-part-not-better = В машине уже установлена такая же или более эффективная деталь.
tiered-machine-part-installed = Установлена деталь { $tier } уровня.
tiered-machine-part-installed-examine = Установлена деталь «{ $kind }»: [color=cyan]уровень { $tier }[/color].
tiered-machine-part-kind-servo = манипулятор
tiered-machine-part-kind-capacitor = конденсатор
tiered-machine-part-kind-matterbin = контейнер материи
tiered-machine-part-kind-scanner = сканирующий модуль
tiered-machine-part-kind-laser = микро-лазер
machine-part-exchanger-nothing = Подходящих деталей лучшего уровня не найдено.
machine-part-exchanger-complete = Детали машины улучшены.
tiered-machine-frame-missing = Не хватает обязательных деталей: { $amount }× { $kind }.
machine-upgrade-bluespace-mining-speed = скорость добычи
machine-upgrade-bluespace-mining-output = объём добычи
machine-upgrade-bluespace-mining-core-life = срок службы ядра
machine-upgrade-bluespace-mining-stability = подавление нестабильности
machine-upgrade-bluespace-mining-temperature = подавление температурных возмущений

stack-servo-drive = манипуляторы
stack-matter-recycler = контейнеры материи
stack-capacitor-module = конденсаторы
stack-scanner-module = сканирующие модули
stack-laser-module = микро-лазеры

ent-StandardServoDrive = микроманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
    .suffix = Уровень 1
ent-CalibratedServoDrive = наноманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
    .suffix = Уровень 2
ent-PrecisionServoDrive = пикоманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
    .suffix = Уровень 3
ent-QuantumServoDrive = фемтоманипулятор
    .desc = Крошечный манипулятор, используемый при сборке различных устройств.
    .suffix = Уровень 4
ent-StandardMatterRecycler = контейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
    .suffix = Уровень 1
ent-CalibratedMatterRecycler = улучшенный контейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
    .suffix = Уровень 2
ent-PrecisionMatterRecycler = суперконтейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
    .suffix = Уровень 3
ent-BluespaceMatterRecycler = блюспейс-контейнер материи
    .desc = Контейнер для хранения сжатой материи, ожидающей реконструкции.
    .suffix = Уровень 4
ent-StandardCapacitorModule = конденсатор
    .desc = Базовый конденсатор, используемый при сборке различных устройств.
    .suffix = Уровень 1
ent-CalibratedCapacitorModule = улучшенный конденсатор
    .desc = Улучшенный конденсатор, используемый при сборке различных устройств.
    .suffix = Уровень 2
ent-PrecisionCapacitorModule = суперконденсатор
    .desc = Конденсатор сверхвысокой ёмкости, используемый при сборке различных устройств.
    .suffix = Уровень 3
ent-QuadraticCapacitorModule = квадратичный конденсатор
    .desc = Квадратичный конденсатор, используемый при сборке различных устройств.
    .suffix = Уровень 4
ent-StandardScannerModule = сканирующий модуль
    .desc = Компактный сканирующий модуль высокого разрешения для сборки некоторых устройств.
    .suffix = Уровень 1
ent-CalibratedScannerModule = улучшенный сканирующий модуль
    .desc = Компактный сканирующий модуль высокого разрешения для сборки некоторых устройств.
    .suffix = Уровень 2
ent-PhasicScannerModule = фазовый сканирующий модуль
    .desc = Компактный фазовый сканирующий модуль высокого разрешения для сборки некоторых устройств.
    .suffix = Уровень 3
ent-TriphasicScannerModule = трифазный сканирующий модуль
    .desc = Компактный трифазный сканирующий модуль ультраразрешения для сборки некоторых устройств.
    .suffix = Уровень 4
ent-StandardLaserModule = микро-лазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
    .suffix = Уровень 1
ent-FocusedLaserModule = мощный микро-лазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
    .suffix = Уровень 2
ent-UltrafineLaserModule = сверхмощный микро-лазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
    .suffix = Уровень 3
ent-QuadphaseLaserModule = квадро-ультра микро-лазер
    .desc = Крошечный лазер, используемый в некоторых устройствах.
    .suffix = Уровень 4
ent-MachinePartExchanger = обменник деталей машин
    .desc = Переносной инструмент для хранения и установки улучшенных деталей через открытую панель техобслуживания.
ent-BluespaceMachinePartExchanger = блюспейс-обменник деталей машин
    .desc = Продвинутый обменник деталей с дистанционной установкой через закрытые панели.
ent-BorgMachinePartExchanger = встроенный обменник деталей машин
    .desc = Обменник деталей, предназначенный для использования киборгами.
ent-BorgModuleMachineParts = модуль деталей машин
    .desc = Инженерный модуль со встроенным обменником деталей машин.

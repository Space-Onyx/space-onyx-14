neuro-interface-ui-chip = Нейрочип
neuro-interface-ui-cache = Кэш
neuro-interface-ui-router = Маршрутизатор
neuro-interface-ui-status-online = Работает
neuro-interface-ui-status-throttled = Ограничено
neuro-interface-ui-status-offline = Нет связи
neuro-interface-ui-status-disabled = Отключено
neuro-interface-ui-status-emp = ЭМИ

neuro-interface-title = Нейроинтерфейс
neuro-interface-heading = Нейроинтерфейс
neuro-interface-mode-short = { $mode }
neuro-interface-demand-label = Нейронагрузка
neuro-interface-demand-value = { $current } / { $max } ед.
neuro-interface-channels-value = Каналы: { $current } / { $max }
neuro-interface-overload-label = Перегрузка
neuro-interface-overload-value = { $value } ед.
neuro-interface-channel-overload-value = Лишних каналов: { $value }
neuro-interface-slot-chip = Нейрочип: { $name }
neuro-interface-slot-cache = Кэш: { $name }
neuro-interface-slot-router = Маршрутизатор: { $name }
neuro-interface-slot-empty = нет
neuro-interface-mode-heading = Режим перегрузки
neuro-interface-mode-throttle = Ограничение
neuro-interface-mode-throttle-tooltip = Избыток нагрузки уходит в ограничение мощности. Нервная ткань защищена.
neuro-interface-mode-overclock = Форсирование
neuro-interface-mode-overclock-tooltip = Ограничение снято. Перегрузка повреждает нервную ткань и интерфейс.

neuro-interface-nav-overview = Сеть
neuro-interface-nav-hardware = Комплектующие
neuro-interface-nav-augments = Аугментации
neuro-interface-overview-subtitle = Нагрузка, питание и аугментации
neuro-interface-hardware-subtitle = Чип, кэш, маршрутизатор и модули
neuro-interface-augments-subtitle = Поиск и управление
neuro-interface-components-heading = Основные модули
neuro-interface-extensions-heading = Расширения
neuro-interface-extensions-empty = Расширений нет.
neuro-interface-extension-entry = • { $name }
neuro-interface-region-head = Голова
neuro-interface-region-chest = Торс
neuro-interface-region-groin = Таз
neuro-interface-region-leftarm = Левая рука
neuro-interface-region-rightarm = Правая рука
neuro-interface-region-lefthand = Левая кисть
neuro-interface-region-righthand = Правая кисть
neuro-interface-region-leftleg = Левая нога
neuro-interface-region-rightleg = Правая нога
neuro-interface-region-leftfoot = Левая стопа
neuro-interface-region-rightfoot = Правая стопа
neuro-interface-region-other = Прочее
neuro-interface-region-all = Все области
neuro-interface-region-header = { $region } · { $count }
neuro-interface-search = Поиск...
neuro-interface-augment-count = { $count } шт.
neuro-interface-augments-empty = Ничего не найдено.
neuro-interface-button-enable = Включить
neuro-interface-button-disable = Отключить
neuro-interface-behavior-scalable = Мощность снижается плавно.
neuro-interface-behavior-binary = Нужен полный канал.
neuro-interface-tooltip-section-resources = Параметры канала
neuro-interface-tooltip-resource-load = Нейронагрузка: { $value } ед.
neuro-interface-tooltip-resource-power = Потребление: { $value } Вт
neuro-interface-tooltip-resource-output = Выход: { $value }%
neuro-interface-tooltip-section-behavior = Тип канала
neuro-interface-tooltip-section-integrated-item = Встроенный предмет
neuro-interface-tooltip-item-power-cost = Развернуть: { $extend } Дж. Убрать: { $retract } Дж.
neuro-interface-tooltip-item-no-power = Автономный привод.
neuro-interface-tooltip-section-tool-panel = Панель инструментов
neuro-interface-tooltip-tool-panel-power = Смена инструмента: { $value } Дж.
neuro-interface-tooltip-tool-panel-no-power = Автономный переключатель.
neuro-interface-tooltip-section-tool-panel-contents = Инструменты
neuro-interface-tooltip-section-effect = Выход привода
neuro-interface-tooltip-strength-effect = Усиление удара: +{ $value }%.
neuro-interface-tooltip-section-generation = Питание
neuro-interface-tooltip-reactor-generation = Выработка: { $value } Вт.
neuro-interface-tooltip-reactor-hunger = Расход питательных веществ: { $value } ед./Дж.

neuro-interface-examine-base-bandwidth = Собственная пропускная способность шины: [color=lightblue]{ $bandwidth } ед.[/color]
neuro-interface-examine-total-bandwidth = С установленными комплектующими доступно [color=cyan]{ $bandwidth } ед.[/color]
neuro-interface-examine-channels = Доступно одновременных каналов: [color=cyan]{ $channels }[/color].
neuro-interface-examine-expansion-modules = Занято модулей расширения: [color=lightblue]{ $count }[/color].
neuro-interface-examine-chip = Обеспечивает [color=cyan]{ $bandwidth } ед.[/color] нейролимита и [color=cyan]{ $channels }[/color] каналов.
neuro-interface-examine-cache = Хранит [color=cyan]{ $channels }[/color] дополнительных рабочих контекстов аугментаций.
neuro-interface-examine-router = Поддерживает строгую очередь из [color=cyan]{ $capacity }[/color] аугментаций.
neuro-interface-chip-effect = +{ $bandwidth } ед. лимита · +{ $channels } каналов
neuro-interface-cache-effect = +{ $channels } каналов
neuro-interface-router-effect = Очередь: { $current } / { $capacity }
neuro-interface-router-effect-missing = Маршрутизатор не установлен
neuro-interface-power-heading = Питание
neuro-interface-power-balance = +{ $generation } / −{ $consumption } Вт
neuro-interface-power-sources-empty = Источников нет.
neuro-interface-power-source-entry = Источник: { $source }
neuro-interface-batteries-empty = Батарей нет.
neuro-interface-battery-values = { $charge } / { $capacity } Дж · { $percent }% · { $rate } Вт
neuro-interface-examine-consumer = Требует [color=cyan]{ $demand } ед.[/color] нейронного канала; постоянное потребление — [color=lightblue]{ $power } Вт[/color].
neuro-interface-examine-scalable = При нехватке канала его мощность [color=yellow]снижается плавно[/color].
neuro-interface-examine-binary = Для работы ему нужен [color=yellow]полный канал[/color].

neuro-interface-tooltip-current-mode = Активный протокол перегрузки.
neuro-interface-tooltip-neuro-load = Нагрузка шины и занятые каналы.
neuro-interface-tooltip-overload = Избыток нагрузки и каналов.
neuro-interface-tooltip-power-network = Общая энергосеть аугментаций.
neuro-interface-tooltip-chip = Расширяет нейролимит и число каналов.
neuro-interface-tooltip-cache = Добавляет активные каналы.
neuro-interface-tooltip-router = Управляет приоритетом каналов.
neuro-interface-routing-position = Очередь: №{ $position }
neuro-interface-routing-auto = Авто
neuro-interface-routing-add = В очередь
neuro-interface-routing-remove = Авто
neuro-interface-routing-up-tooltip = Повысить приоритет.
neuro-interface-routing-down-tooltip = Понизить приоритет.
neuro-interface-routing-toggle-tooltip = Ручной или автоматический приоритет.
neuro-interface-routing-router-required = Маршрутизатор не установлен.
neuro-interface-routing-queue-full = Нет свободных каналов маршрутизации.
neuro-interface-tooltip-routing = Ручная очередь распределения ресурсов.
neuro-interface-tooltip-region-filter = Отбор по месту установки.
neuro-interface-tooltip-augment-count = Число узлов в списке.
neuro-interface-tooltip-battery-values = Заряд, ёмкость и поток мощности.

neuro-interface-ui-chip = Вычислительный чип
neuro-interface-ui-cache = Нейроморфный кэш
neuro-interface-ui-router = Нейромаршрутизатор
neuro-interface-ui-expansion-module = Модуль расширения
neuro-interface-ui-status-online = Стабильная связь
neuro-interface-ui-status-throttled = Сниженная мощность
neuro-interface-ui-status-offline = Канал недоступен
neuro-interface-ui-status-disabled = Отключено вручную
neuro-interface-ui-status-emp = Потеря сигнала: ЭМИ

neuro-interface-title = Нейронная сеть
neuro-interface-heading = Нейроинтерфейс
neuro-interface-subheading = Связь с установленными аугментациями
neuro-interface-current-mode = Режим: { $mode }
neuro-interface-mode-short = { $mode }
neuro-interface-bandwidth-label = Пропускная способность
neuro-interface-bandwidth-value = { $value } ед.
neuro-interface-demand-label = Нейронагрузка
neuro-interface-demand-value = { $value } ед.
neuro-interface-channels-value = каналы: { $current } / { $max }
neuro-interface-overload-label = Сверх предела
neuro-interface-overload-value = { $value } ед.
neuro-interface-channel-overload-value = лишние каналы: { $value }
neuro-interface-slot-chip = Нейрочип: { $name }
neuro-interface-slot-cache = Нейроморфный кэш: { $name }
neuro-interface-slot-router = Нейромаршрутизатор: { $name }
neuro-interface-slot-empty = не установлен
neuro-interface-mode-heading = Поведение при перегрузке
neuro-interface-mode-throttle = Безопасное ограничение
neuro-interface-mode-overclock = Форсированный режим
neuro-interface-mode-hint = Безопасный режим снижает мощность второстепенных каналов. Форсирование сохраняет отдачу ценой повреждения нервной ткани и интерфейса.
neuro-interface-overclock-damage-value = Прогноз форсирования: { $value } урона/с интерфейсу и мозгу
neuro-interface-module-telemetry = нагрузка { $demand } ед. · питание { $power } Вт · мощность { $efficiency }%

neuro-interface-nav-overview = Обзор сети
neuro-interface-nav-hardware = Устройство
neuro-interface-nav-augments = Аугментации
neuro-interface-overview-subtitle = Нагрузка нейронной шины и поведение при перегрузке
neuro-interface-hardware-subtitle = Основные комплектующие и установленные расширения
neuro-interface-augments-subtitle = Поиск и управление подключёнными каналами
neuro-interface-components-heading = Основные комплектующие
neuro-interface-extensions-heading = Модули расширения
neuro-interface-extensions-empty = Модули расширения не установлены.
neuro-interface-extension-entry = • { $name }
neuro-interface-module-connect = Подключить канал
neuro-interface-module-disconnect = Отключить канал

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
neuro-interface-region-other = Прочие узлы
neuro-interface-region-all = Все области
neuro-interface-region-header = { $region } · { $count }
neuro-interface-routing-header = Управляемая очередь · { $count }
neuro-interface-search = Поиск аугментации...
neuro-interface-augment-count = Найдено: { $count }
neuro-interface-augments-empty = Подходящие аугментации не найдены.
neuro-interface-button-enable = Включить
neuro-interface-button-disable = Отключить
neuro-interface-entry-brief = нагрузка { $load } ед. · мощность { $efficiency }% · { $behavior }
neuro-interface-behavior-scalable = плавное снижение мощности
neuro-interface-behavior-binary = требуется полный канал
neuro-interface-behavior-scalable-short = плавный канал
neuro-interface-behavior-binary-short = полный канал
neuro-interface-entry-tooltip = { $name }
    Статус: { $status }
    Нейронагрузка: { $demand } ед.
    Питание: { $power } Вт
    Выходная мощность: { $efficiency }%
    Маршрутизация: { $routing }
    Поведение: { $behavior }

neuro-interface-examine-base-bandwidth = Собственная пропускная способность шины: [color=lightblue]{ $bandwidth } ед.[/color]
neuro-interface-examine-total-bandwidth = С установленными комплектующими доступно [color=cyan]{ $bandwidth } ед.[/color]
neuro-interface-examine-channels = Доступно одновременных каналов: [color=cyan]{ $channels }[/color].
neuro-interface-examine-expansion-modules = Занято модулей расширения: [color=lightblue]{ $count }[/color].
neuro-interface-examine-chip = Обеспечивает [color=cyan]{ $bandwidth } ед.[/color] нейролимита и [color=cyan]{ $channels }[/color] каналов.
neuro-interface-examine-cache = Хранит [color=cyan]{ $channels }[/color] дополнительных рабочих контекстов аугментаций.
neuro-interface-examine-router = Поддерживает строгую очередь из [color=cyan]{ $capacity }[/color] аугментаций.
neuro-interface-chip-effect = Вклад: +{ $bandwidth } ед. пропускной способности, +{ $channels } каналов
neuro-interface-cache-effect = Вклад: +{ $channels } каналов
neuro-interface-router-effect = Управляемая очередь: { $current } / { $capacity }
neuro-interface-router-effect-missing = Маршрутизация не установлена: все каналы находятся в автоматическом пуле
neuro-interface-power-heading = Энергосеть аугментаций
neuro-interface-power-balance = +{ $generation } / −{ $consumption } Вт
neuro-interface-power-sources-empty = Активные источники зарядки не обнаружены.
neuro-interface-power-source-entry = Источник: { $source }
neuro-interface-batteries-empty = Батареи не установлены.
neuro-interface-battery-values = { $charge } / { $capacity } Дж · { $percent }% · { $rate } Вт
neuro-interface-examine-module = Это [color=lightblue]модуль расширения[/color] нейроинтерфейса.
neuro-interface-examine-emp-protection = Подавляет силу ЭМИ на [color=cyan]{ $strength }%[/color], а длительность помех — на [color=cyan]{ $duration }%[/color].
neuro-interface-examine-consumer = Требует [color=cyan]{ $demand } ед.[/color] нейронного канала; постоянное потребление — [color=lightblue]{ $power } Вт[/color].
neuro-interface-examine-scalable = При нехватке канала его мощность [color=yellow]снижается плавно[/color].
neuro-interface-examine-binary = Для работы ему нужен [color=yellow]полный канал[/color].

neuro-interface-tooltip-current-mode = Определяет поведение при нехватке ресурсов: безопасно ограничить часть аугментаций или удерживать их ценой перегрузки.
neuro-interface-tooltip-neuro-limit = Максимальная суммарная сложность сигналов, которую нейроинтерфейс обрабатывает без перегрузки.
neuro-interface-tooltip-neuro-load = Сколько вычислительной мощности сейчас запрашивают подключённые аугментации. Нижняя строка показывает занятые и доступные каналы.
neuro-interface-tooltip-overload = Нагрузка сверх безопасного предела. В безопасном режиме она ограничивается, а при форсировании повреждает мозг и интерфейс.
neuro-interface-tooltip-power-network = Общая энергосеть аугментаций. Плюс показывает текущую выработку, минус — потребление. Ниже перечислены источники и батареи.
neuro-interface-tooltip-chip = Главный вычислительный элемент. Увеличивает нейролимит и число аугментаций, поддерживаемых одновременно.
neuro-interface-tooltip-cache = Хранит готовые состояния управления и добавляет одновременные каналы, не увеличивая нейролимит.
neuro-interface-tooltip-router = Управляет порядком распределения пропускной способности и каналов. Без маршрутизатора ручные приоритеты недоступны.
neuro-interface-routing-position = позиция { $position } в управляемой очереди
neuro-interface-routing-auto = автоматическое распределение
neuro-interface-routing-position-short = №{ $position }
neuro-interface-routing-auto-short = АВТО
neuro-interface-routing-add = В очередь
neuro-interface-routing-remove = Авто
neuro-interface-routing-up-tooltip = Поднять аугментацию в управляемой очереди.
neuro-interface-routing-down-tooltip = Опустить аугментацию в управляемой очереди.
neuro-interface-routing-toggle-tooltip = Закрепить аугментацию в очереди или вернуть в автоматический пул.
neuro-interface-routing-router-required = Нужен нейромаршрутизатор со свободным местом в очереди.
neuro-interface-tooltip-routing = Закреплённые аугментации получают ресурсы первыми, строго по позиции. Автоматический пул обслуживается после них.
neuro-interface-tooltip-overclock-damage = Урон в секунду, который отдельно получат нейроинтерфейс и мозг при текущей нагрузке в форсированном режиме.
neuro-interface-tooltip-region-filter = Оставляет в списке только аугментации выбранной области тела.
neuro-interface-tooltip-augment-count = Количество аугментаций, подходящих под текущий поиск и фильтр.
neuro-interface-tooltip-battery-values = Текущий заряд, ёмкость, процент заполнения и поток энергии. Положительный поток заряжает батарею, отрицательный разряжает.
neuro-interface-tooltip-entry-brief = Нагрузка показывает сложность сигналов аугментации. Мощность показывает, насколько полно она сейчас работает.

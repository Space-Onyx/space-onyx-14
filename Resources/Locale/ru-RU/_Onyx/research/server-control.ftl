ent-ComputerResearchServerControl = консоль управления серверами РнД
    .desc = Отслеживает локальные сети РнД и управляет генерацией их серверов.
ent-ResearchServerControlComputerCircuitboard = плата консоли управления серверами РнД
    .desc = Компьютерная плата консоли управления серверами РнД.

research-server-control-title = Управление серверами РнД
research-server-control-servers = Локальные сети РнД
research-server-control-logs = Журнал сети
research-server-control-network = Сеть: { $network }
research-server-control-server-name = [{ $id }] { $name }
research-server-control-authority = Главный сервер сети
research-server-control-forwarded = Перенаправляет операции на сервер [{ $authorityId }]
research-server-control-telemetry = Питание: { $power } | Генерация: { $rate } очк./с | Баланс сети: { $points }
research-server-control-powered = включено
research-server-control-unpowered = отсутствует
research-server-control-state-enabled = включена
research-server-control-state-disabled = выключена
research-server-control-disable-generation = Выключить генерацию сервера
research-server-control-enable-generation = Включить генерацию сервера
research-server-control-configure-network = Настроить сеть
research-server-control-empty = На этом объекте нет серверов РнД.

research-network-log-empty = События сети отсутствуют.
research-network-log-search = Поиск по журналу сети...
research-network-log-user-unknown = неизвестно
research-network-log-user-with-job = { $name } ({ $job })
research-network-log-server-online = { $server } подключён к сети { $network }.
research-network-log-server-offline = { $server } отключён от сети { $network }.
research-network-log-generation-toggled = { $user } переключил генерацию сервера { $server }: { $state }.
research-network-log-technology-unlocked = { $user } открыл технологию «{ $technology }».
research-network-log-network-changed = { $user } переместил сервер { $server } из сети { $oldNetwork } в сеть { $newNetwork }.
research-network-log-network-left = { $user } отключил сервер { $server } от сети { $network }.

research-console-network-log-button = Журнал сети
research-console-network-log-title = Журнал сети РнД

research-server-network-examine = Сервер [bold]{ $name }[/bold]
    Сеть: [bold]{ $network }[/bold] | { $authority }
    Генерация: [bold]{ $generation }[/bold] очк./с ([bold]{ $state }[/bold]) | Баланс сети: [bold]{ $points }[/bold]
research-server-network-examine-authority = главный сервер сети
research-server-network-examine-forwarded = перенаправляет операции на сервер [{ $hash }]

research-server-network-title = Настройки сети РнД
research-server-network-server = Сервер [{ $id }]: { $name }

research-client-server-selection-authority-entry = [{ $id }] { $serverName } | { $network } | главный
research-client-server-selection-follower-entry = [{ $id }] { $serverName } | { $network } | перенаправление на [{ $authorityId }]
research-server-network-help = Укажите существующий ID для подключения к его сети или новый ID для создания отдельной чистой сети. Переименование сети из одного сервера сохраняет прогресс. Допустимы A-Z, 0-9, - и _.
research-server-network-apply = Применить ID сети

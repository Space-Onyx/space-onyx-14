# Space Onyx
# Copyright (C) 2026 Space Onyx contributors
#
# This file is licensed under AGPL-3.0-or-later.
# See LICENSES for the full license text.

research-experiment-ui-task = { $goal }: { $progress }/{ $target }
research-experiment-ui-empty = Нет совместимых экспериментов.
research-experiment-ui-status-active = Активен
research-experiment-ui-status-other-scanner = Нужен другой сканер
research-experiment-ui-status-locked = Заблокирован
research-experiment-ui-status-completed = Завершён
research-experiment-ui-details = Анализатор: { $source }
    Технология разблокировки: { $technologies }
    Награда: { $reward }
research-experiment-ui-source-any = ручной или стационарный анализатор
research-experiment-ui-source-handheld = только ручной сканер
research-experiment-ui-source-machine = только стационарный анализатор
research-experiment-ui-source-console = исследовательская консоль
research-experiment-ui-technology-none = доступен с начала смены
research-experiment-ui-reward-none = без исследовательских очков
research-experiment-ui-reward-entry = { $amount } { $type }
research-experiment-ui-source-label = Анализатор
research-experiment-ui-technology-label = Разблокировка
research-experiment-ui-reward-label = Награда
research-experiment-network-completed = { $user } завершил эксперимент «{ $experiment }».

research-experiment-uranium-name = Анализ урана
research-experiment-uranium-description = Просканируйте урановую руду, обработанный уран или содержащий уран образец.
research-experiment-uranium-goal = Зарегистрировать образец урана

research-experiment-ore-classification-name = Классификация руды
research-experiment-ore-classification-description = Просканируйте три разных вида необработанной руды.
research-experiment-ore-classification-goal = Зарегистрировать разные образцы руды

research-experiment-explosive-yield-name = Анализ мощности взрывчатки
research-experiment-explosive-yield-description = Просканируйте взрывное устройство с измеримой мощностью.
research-experiment-explosive-yield-goal = Зарегистрировать пригодное взрывное устройство

research-experiment-anomaly-core-name = Анализ ядра аномалии
research-experiment-anomaly-core-description = Просканируйте ядро аномалии.
research-experiment-anomaly-core-goal = Зарегистрировать ядро аномалии

research-experiment-cyborg-architecture-name = Анализ архитектуры киборга
research-experiment-cyborg-architecture-description = Просканируйте рабочий корпус киборга.
research-experiment-cyborg-architecture-goal = Зарегистрировать корпус киборга

research-experiment-cryoxadone-purity-name = Анализ чистоты криоксадона
research-experiment-cryoxadone-purity-description = Просканируйте образец, содержащий не менее 60 единиц криоксадона чистотой не менее 90%.
research-experiment-cryoxadone-purity-goal = Зарегистрировать образец криоксадона высокой чистоты

research-experiment-seed-diversity-name = Анализ разнообразия семян
research-experiment-seed-diversity-description = Просканируйте три разных вида пакетиков семян.
research-experiment-seed-diversity-goal = Зарегистрировать разные пакетики семян

research-experiment-tiered-parts-tier2-name = Бенчмарк калиброванных деталей
research-experiment-tiered-parts-tier2-description = Наши новые компоненты оборудования нуждаются в практических испытаниях. Просканируйте любые машины с калиброванными деталями и доложите о результатах.
research-experiment-tiered-parts-tier2-goal = Зарегистрировать детали 2 уровня

research-experiment-tiered-parts-tier3-name = Бенчмарк прецизионных деталей
research-experiment-tiered-parts-tier3-description = Наши новые компоненты оборудования нуждаются в практических испытаниях. Просканируйте любые машины с прецизионными деталями и доложите о результатах.
research-experiment-tiered-parts-tier3-goal = Зарегистрировать детали 3 уровня

research-experiment-rare-fish-name = Учёт редких рыб
research-experiment-rare-fish-description = Зарегистрируйте два разных редких вида рыб в их среде обитания.
research-experiment-rare-fish-goal = Зарегистрировать разных редких рыб

research-experiment-tritium-synthesis-name = Анализ синтеза трития
research-experiment-tritium-synthesis-description = Проанализируйте баллон или канистру, содержащую не менее 50 молей трития чистотой не менее 90%.
research-experiment-tritium-synthesis-goal = Зарегистрировать синтезированную партию трития

research-experiment-bluespace-crystal-name = Спектрометрия блюспейс-кристалла
research-experiment-bluespace-crystal-description = Проанализируйте синтезированный блюспейс-кристалл, чтобы подтвердить стабильность удерживающего поля перед разработкой блюспейс-хранилищ.
research-experiment-bluespace-crystal-goal = Зарегистрировать синтезированный блюспейс-кристалл

research-experiment-scanner-window-title = Сканер экспериментов
research-experiment-scanner-no-server = Нет подключения к серверу РнД.
research-experiment-scanner-no-compatible = Нет активных экспериментов для этого сканера.
research-experiment-scanner-no-match = Цель не подходит активным экспериментам.
research-experiment-scanner-duplicate = Цель уже зарегистрирована.
research-experiment-scanner-success = Образец { $target } зарегистрирован.

research-experiment-machine-window-title = Экспериментальный анализатор
research-experiment-machine-run = Запустить анализ
research-experiment-machine-samples-title = Образцы
research-experiment-machine-samples-hint = Положите незакреплённые предметы на платформу анализатора и запустите анализ. Образцы не разрушаются.
research-experiment-machine-experiments-title = Каталог экспериментов
research-experiment-machine-status = Состояние: { $status }
research-experiment-machine-status-idle = готов
research-experiment-machine-status-processing = идёт анализ…
research-experiment-machine-busy = Анализатор уже работает.
research-experiment-machine-no-samples = На платформе анализатора нет образцов для анализа.
research-experiment-machine-processing = Идёт анализ: { $count } { $count ->
    [one] образец
    [few] образца
   *[many] образцов
}…
research-experiment-machine-progressed = Данные записаны, прогресс экспериментов обновлён.
research-experiment-machine-completed = Экспериментов завершено: { $count }.

ent-ResearchExperimentScanner = сканер экспериментов
    .desc = Ручной сканер для регистрации образцов экспериментов РнД.
ent-ResearchExperimentMachine = экспериментальный анализатор
    .desc = Анализирует образцы для активных экспериментов РнД, не разрушая их.
ent-ResearchExperimentMachineCircuitboard = экспериментальный анализатор (машинная плата)
    .desc = Печатная плата экспериментального анализатора.
    .suffix = { ent-BaseMachineCircuitboard.suffix }

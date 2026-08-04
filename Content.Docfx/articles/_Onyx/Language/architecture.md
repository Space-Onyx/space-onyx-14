# Механика языков: архитектура рабочего билда Corvax Vanilla

Документ описывает фактическую реализацию механики языков в текущем рабочем билде
`corvaxvanilla/space-station-14`. Это описание существующего поведения, включая
известные ограничения. Оно не является проектом желаемой переработки.

## Назначение и границы

Языковая механика является серверно-авторитетным слоем над внутриигровым чатом.
Она отвечает за:

- набор языков, на которых сущность может говорить;
- набор языков, которые сущность понимает;
- выбор текущего языка;
- персональное искажение локальной речи для непонимающих слушателей;
- языки, воспринимаемые зрением вместо слуха;
- оформление речи языковыми цветами и шрифтами;
- временное расширение знаний переносными переводчиками и имплантами;
- owner-only синхронизацию языкового состояния и клиентское меню выбора.

Механика не заменяет базовую обработку речи. Проверки возможности говорить,
акценты, speech verbs, дальность, слышимость, inline actions, радио и replay
остаются частями соответствующих штатных подсистем.

## Карта реализации

### Общее ядро

| Путь | Назначение |
| --- | --- |
| `Content.Shared/_Onyx/Language/LanguagePrototype.cs` | Прототип языка, оформление и алгоритмы искажения. |
| `Content.Shared/_Onyx/Language/LanguageComponents.cs` | Компоненты говорящего, знаний и переводчиков. |
| `Content.Shared/_Onyx/Language/LanguageMessages.cs` | Сетевой выбор языка и событие агрегации знаний. |
| `Content.Shared/_Onyx/Language/TranslatorSystem.cs` | Examine и appearance переводчиков. |

### Сервер

| Путь | Назначение |
| --- | --- |
| `Content.Server/_Onyx/Language/LanguageSystem.cs` | Агрегация знаний, выбор языка, проверки и искажение. |
| `Content.Server/_Onyx/Language/TranslatorSystem.cs` | Учёт переводчиков, питания, контейнеров и имплантов. |
| `Content.Server/_Onyx/Language/LanguageCommands.cs` | Консольные команды `languagelist` и `languageselect`. |
| `Content.Server/Chat/Systems/ChatSystem.PrivateAPI.cs` | Персональная доставка речи и шёпота с учётом языка. |
| `Content.Server/Chat/Systems/ChatSystem.cs` | Входной поток IC-чата и передача `languageOverride`. |
| `Content.Server/Radio/EntitySystems/RadioSystem.cs` | Радиопередача и языковое оформление радио. |
| `Content.Server/Radio/RadioEvent.cs` | Контракт события приёма радио. |
| `Content.Server/Radio/EntitySystems/HeadsetSystem.cs` | Передача и приём через гарнитуру. |

### Клиент

| Путь | Назначение |
| --- | --- |
| `Content.Client/_Onyx/Language/LanguageSystem.cs` | Применение owner-only состояния и отправка выбора. |
| `Content.Client/_Onyx/Language/LanguageMenuWindow.xaml` | Разметка окна выбора. |
| `Content.Client/_Onyx/Language/LanguageMenuWindow.xaml.cs` | Заполнение списка, описаний и кнопок выбора. |
| `Content.Client/_Onyx/Language/LanguageMenuUIController.cs` | Жизненный цикл и переключение окна. |

### Интеграции

| Путь | Назначение |
| --- | --- |
| `Content.Shared/Abilities/Mime/MimePowersSystem.cs` | Выдача и удаление языка `Sign` у мима. |
| `Content.Server/Speech/Muting/MutingSystem.cs` | Исключение из немоты для мима, говорящего на `Sign`. |
| `Content.Shared/Chat/SharedChatSystem.cs` | Общий параметр `languageOverride`. |
| `Content.Server/Administration/Commands/OSay.cs` | Административная речь с явным языком. |
| `Content.Shared/Input/ContentKeyFunctions.cs` | Key function окна языков. |
| `Content.Client/Input/ContentContexts.cs` | Регистрация контекста ввода. |
| `Content.Client/UserInterface/Systems/MenuBar/GameTopMenuBarUIController.cs` | Кнопка меню. |
| `Resources/keybinds.yml` | Стандартная клавиша `L`. |

## Прототип языка

`LanguagePrototype` зарегистрирован как `language` prototype. Его ID является
значением `ProtoId<LanguagePrototype>` во всех компонентах и сообщениях.

Поля:

| Поле | Поведение |
| --- | --- |
| `ID` | Уникальный prototype ID. |
| `IsVisibleLanguage` | Сериализуется из YAML, но текущим кодом не используется. |
| `AlwaysUnderstood` | Делает язык понятным любому слушателю без проверки компонента. |
| `RequiresSight` | Заменяет слух проверкой зрения и прямой видимости. |
| `Speech` | Переопределяет цвет, обычный/жирный шрифт и размер текста. |
| `Obfuscation` | Определяет замену текста для непонимающего слушателя. |

Имя и описание не хранятся в прототипе. Они вычисляются по Fluent-ключам:

```text
language-<ID>-name
language-<ID>-description
```

### Оформление речи

`LanguageSpeechOverride` содержит необязательные `Color`, `FontId`,
`BoldFontId` и `FontSize`. При отсутствии значений используются настройки
`SpeechVerbPrototype`. Для шёпота используются собственные значения по
умолчанию.

Цвет с alpha-компонентом интерполируется с белым. Поэтому alpha определяет
силу языкового оттенка, а не прозрачность текста.

### Искажение

Базовый контракт `ObfuscationMethod.Obfuscate(message, roundId)` имеет две
реализации.

`ReplacementObfuscation` заменяет всё сообщение одним элементом `Replacement`.
Элемент выбирается детерминированно по стабильному hash сообщения и ID раунда.
Пустой список замен даёт `<?>`.

`SyllableObfuscation` обрабатывает каждую последовательность букв и цифр как
слово. Слово заменяется детерминированной последовательностью псевдослогов;
пробелы и пунктуация сохраняются. Количество слогов определяется диапазоном
`MinSyllables..MaxSyllables`. Пустой список или обратный диапазон дают `<?>`.

Одинаковое слово в одном раунде искажается одинаково. Смена `RoundId` меняет
результат. Регистр исходного слова не переносится в результат.

## Компоненты и владение состоянием

### `LanguageKnowledgeComponent`

Источник врождённых знаний:

- `SpokenLanguages`, YAML-поле `speaks`;
- `UnderstoodLanguages`, YAML-поле `understands`.

Оба YAML-поля обязательны. Компонент не сетевой. Само изменение его коллекций
не запускает пересчёт итогового состояния.

### `LanguageSpeakerComponent`

Runtime-кэш итоговых возможностей сущности:

- `CurrentLanguage`;
- `SpokenLanguages`;
- `UnderstoodLanguages`;
- `UnderstandsAllLanguages`.

Компонент сетевой, но `SendOnlyToOwner` скрывает состояние от чужих клиентов.
Сервер вручную формирует `LanguageSpeakerComponentState`. Клиент использует этот
компонент для меню; сервер использует его как авторитетный кэш при речи.

Начальное значение `CurrentLanguage` равно `Universal`. После агрегации оно
проверяется на наличие в `SpokenLanguages`.

### `UniversalLanguageSpeakerComponent`

Дополнительный источник знаний:

- `Enabled` включает источник;
- стандартный `SpokenLanguages` содержит `Psychomantic`;
- `UnderstandsAllLanguages` обычно равен `true`.

Компонент применяется к наблюдателям и административным наблюдателям. Его
добавление и удаление вызывают пересчёт языков.

### Переводчики

`BaseTranslatorComponent` содержит:

- `SpokenLanguages`, YAML-поле `spoken`;
- `UnderstoodLanguages`, YAML-поле `understood`;
- `RequiredLanguages`, YAML-поле `requires`;
- `Enabled`;
- `RequiresAllLanguages`, YAML-поле `requiresAll`.

Наследники:

- `HandheldTranslatorComponent`: переносное устройство, может выбирать новый
  язык при включении и показывать сведения при examine;
- `TranslatorImplantComponent`: имплантированный источник языков.

Компоненты переводчиков не сетевые. Клиент получает только агрегированный
результат через `LanguageSpeakerComponentState`.

## Агрегация знаний

`LanguageSystem.UpdateLanguages(speaker)` является центральной операцией
пересчёта:

1. Проверяет наличие `LanguageSpeakerComponent`.
2. Создаёт `CollectLanguageKnowledgeEvent`.
3. Поднимает событие локально на сущности говорящего.
4. `LanguageKnowledgeComponent` добавляет врождённые языки.
5. `UniversalLanguageSpeakerComponent` добавляет универсальные способности.
6. Серверный `TranslatorSystem` добавляет активные переводчики и импланты.
7. Итоговые множества полностью заменяют содержимое runtime-кэша.
8. `EnsureValidCurrentLanguage()` исправляет недоступный текущий язык.
9. `Dirty()` отправляет новое owner-only состояние.

На `MapInitEvent` пересчёт выполняется немедленно и повторно через
`Timer.Spawn(0)`. Отложенный повтор компенсирует порядок инициализации связанных
компонентов.

`CollectLanguageKnowledgeEvent` специально предназначен для расширения:
дополнительная система может подписаться на него и добавить знания без изменения
`LanguageSystem`. После изменения источника она обязана инициировать
`UpdateLanguages()`.

### Валидация текущего языка

Если `CurrentLanguage` отсутствует в `SpokenLanguages`, выбирается первый элемент
множества; при пустом множестве используется `Universal`.

`SetLanguage()` принимает только существующий прототип, входящий в
`SpokenLanguages`. Таким образом, поддельный клиентский `SetLanguageMessage` не
может выдать игроку неизвестный язык.

`GetCurrentLanguage()` возвращает прототип текущего языка. При отсутствии
компонента или неизвестном ID возвращается `Universal`.

## Клиентская синхронизация и выбор

Сервер отправляет владельцу `LanguageSpeakerComponentState` с копиями четырёх
runtime-полей. Клиентский `LanguageSystem` применяет state и вызывает событие
`LanguagesChanged` для локальной сущности.

Окно выбора:

- показывает локализованное имя текущего языка;
- сортирует `SpokenLanguages` по локализованному имени;
- показывает описание каждого языка;
- блокирует кнопку уже выбранного языка;
- вызывает `SelectLanguage()`, отправляющий `SetLanguageMessage`.

Сервер получает сообщение, использует только `AttachedEntity` отправившей сессии
и повторно проверяет право говорить на выбранном языке.

## Поток локальной речи

Основная интеграция находится в `ChatSystem.SendEntitySpeak()`.

1. `_actionBlocker.CanSpeak()` проверяет общую возможность говорить.
2. `TransformSpeech()` применяет штатные преобразования речи и акценты.
3. Inline actions временно защищаются от языкового искажения.
4. Определяется speech verb и отображаемое имя говорящего.
5. Выбирается валидный `languageOverride` либо результат
   `LanguageSystem.GetCurrentLanguage()`.
6. Создаётся понятная обёртка сообщения с оформлением языка.
7. Один раз вычисляется языковое искажение текста.
8. Для каждого потенциального получателя проверяется канал восприятия и дальность.
9. `CanUnderstand(listener, language.ID)` выбирает понятный или искажённый текст.
10. Для получателя создаётся персональная обёртка.
11. `ChatMessageToOne()` отправляет персональный результат.
12. Replay получает понятную общую версию.
13. `EntitySpokeEvent` получает преобразованный, но понятный текст.

`CanUnderstand()` возвращает `true`, когда:

- прототип имеет `AlwaysUnderstood`; либо
- у слушателя есть `LanguageSpeakerComponent` и `UnderstandsAllLanguages`; либо
- язык находится в `UnderstoodLanguages` слушателя.

Обработка персональна. Два слушателя одного сообщения могут получить разные
тексты.

## Поток шёпота

`ChatSystem.SendEntityWhisper()` использует тот же выбор языка и персональную
проверку понимания, затем применяет штатную механику дальнего шёпота.

Порядок искажений:

1. Непонимающему слушателю подставляется языковое искажение.
2. Для дальнего слушателя полученный текст дополнительно портится
   `ObfuscateMessageReadability(..., 0.2f)`.
3. При отсутствии прямой видимости скрывается личность говорящего.

`EntitySpokeEvent.ObfuscatedMessage` содержит штатную версию дальнего шёпота, а
не языковое искажение.

## Языки, требующие зрения

`LanguagePrototype.RequiresSight` переключает восприятие сообщения:

- слух и `CanHear()` не требуются;
- слепой слушатель с `BlindableComponent.IsBlind` исключается;
- требуется `ExamineSystem.InRangeUnOccluded()`;
- используются обычные радиусы речи или шёпота.

Текущий пример: `Sign`.

Интеграция мима напрямую добавляет `Sign` в `LanguageKnowledgeComponent` и
`LanguageSpeakerComponent` при инициализации `MimePowersComponent`, затем удаляет
его при shutdown. `MutingSystem` разрешает обход `MutedComponent` только если у
сущности есть `MimePowersComponent` и её текущий язык равен `Sign`.

Префикс радио для `Sign` не разбирается обычным потоком IC-чата.

## Радио

Радио использует `EntitySpokeEvent.Message`, то есть понятный преобразованный
текст. `RadioSystem.SendRadioMessage()` получает текущий язык отправителя только
для цвета, шрифта и размера сообщения.

Текущий `RadioReceiveEvent` содержит:

- текст;
- источник сообщения;
- радиоканал;
- источник радиосигнала;
- заранее созданный общий `MsgChatMessage`.

ID языка в событии отсутствует. Один `ChatMessage` передаётся всем подходящим
приёмникам. `HeadsetSystem` и intrinsic receiver отправляют этот общий объект
сессии без `CanUnderstand()` и без `Obfuscate()`.

Следствие: радио не сохраняет языковую секретность. Слушатель полностью понимает
радиосообщение на неизвестном языке. Переводчики не влияют на радиопонимание.
Повторное озвучивание через радиоустройство также не переносит исходный язык как
отдельное значение.

## Переводчики: серверный жизненный цикл

Переносной переводчик учитывается, когда он находится непосредственно в одном из
контейнеров сущности с `LanguageSpeakerComponent` и `LanguageKnowledgeComponent`.
Сканирование не рекурсивно.

Импланты берутся из `ImplantedComponent.ImplantContainer`.

Перед добавлением языков проверяется:

- `Enabled`;
- выполнение `RequiredLanguages`;
- режим any/all через `RequiresAllLanguages`.

Требования проверяются только против врождённого
`LanguageKnowledgeComponent.UnderstoodLanguages`, не против уже агрегированных
знаний. Поэтому переводчики не образуют цепочки зависимостей.

События, вызывающие обновление:

- включение через `ActivateInWorldEvent`;
- помещение в контейнер и извлечение;
- изменение или опустошение power cell;
- `ItemToggledEvent`;
- установка и удаление импланта.

Удаление из контейнера обрабатывается отложенно, чтобы состояние контейнера уже
соответствовало событию. При включении устройство может выбрать первый язык из
`SpokenLanguages`, отсутствующий во врождённых знаниях владельца. Порядок
`HashSet` не задаёт дизайнерский приоритет.

Shared `TranslatorSystem` формирует examine-текст о понятных, доступных для речи
и требуемых языках, а также обновляет appearance включённости.

## Прототипы и ресурсы

### Языки

Основные прототипы находятся в:

- `Resources/Prototypes/_Onyx/Language/languages.yml`;
- `Resources/Prototypes/_Onyx/Language/Standard/standard.yml`;
- `Resources/Prototypes/_Onyx/Language/Species-Specific/species-specific.yml`;
- `Resources/Prototypes/_Onyx/Language/Animals/animal.yml`;
- `Resources/Prototypes/_Onyx/Language/Faction/faction.yml`;
- `Resources/Prototypes/_Onyx/Language/xenomorph_hivemind.yml`;
- `Resources/Prototypes/_Onyx/Language/drone.yml`.

Особые прототипы:

- `Universal`: всегда понятен, используется как fallback;
- `Psychomantic`: всегда понятен, стандартная речь универсальных наблюдателей;
- `Sign`: требует зрения;
- `RobotTalk` и `DroneTalk`: используют моноширинное оформление;
- отдельные faction/animal-языки используют замену всего сообщения либо
  тематические псевдослоги.

### Выдача видовыми прототипами

`LanguageSpeakerComponent` начинается на базовых мобах. Врождённый
`LanguageKnowledgeComponent` обычно добавляется видовыми прототипами. Базовый
гуманоид получает `TauCetiBasic`; конкретные виды расширяют или заменяют наборы.

Значимые каталоги:

- `Resources/Prototypes/Body/species_base.yml`;
- `Resources/Prototypes/Body/Species/`;
- `Resources/Prototypes/Corvax/Body/Species/`;
- `Resources/Prototypes/_Onyx/Entities/Mobs/`.

Наблюдатели используют `UniversalLanguageSpeakerComponent`.

### Переводчики

- `Resources/Prototypes/_Onyx/Entities/Objects/Misc/translators.yml`;
- `Resources/Prototypes/_Onyx/Entities/Objects/Devices/translator_implants.yml`;
- `Resources/Prototypes/_Onyx/Recipes/Lathes/translators.yml`;
- `Resources/Prototypes/_Onyx/Recipes/Lathes/Packs/translators.yml`;
- `Resources/Prototypes/_Onyx/Research/civilianservices.yml`.

Ручные устройства используют `PowerCellDraw`; прототипы имплантов связаны с
соответствующими implanter entities.

### Локализация и шрифты

Основные RU-файлы:

- `Resources/Locale/ru-RU/_Onyx/language/language-menu.ftl`;
- `Resources/Locale/ru-RU/_Onyx/language/languages.ftl`;
- `Resources/Locale/ru-RU/_Onyx/language/language-chat.ftl`;
- `Resources/Locale/ru-RU/_Onyx/language/translators.ftl`;
- `Resources/Locale/ru-RU/_Onyx/chat/managers/chat-language.ftl`.

EN-локализация имеет соответствующие `_Onyx`-файлы. Отдельные языки также
локализованы рядом со своей подсистемой, например drone. Дополнительный шрифт
`MonospaceBold` объявлен в `Resources/Prototypes/_Onyx/Fonts/language.yml`.

## Программное использование

### Получение текущего языка

Серверный код использует `LanguageSystem.GetCurrentLanguage(entity)`. Метод
всегда возвращает прототип, включая fallback `Universal`.

### Проверка возможностей

- `CanSpeak(entity, languageId)` проверяет существование прототипа, компонент и
  членство в `SpokenLanguages`.
- `CanUnderstand(entity, languageId)` учитывает `AlwaysUnderstood`, универсальное
  понимание и `UnderstoodLanguages`.
- `SetLanguage(entity, languageId)` безопасно меняет текущий язык и вызывает
  `Dirty()`.

### Принудительный язык сообщения

Перегрузки `TrySendInGameICMessage()` и приватные методы речи принимают
`ProtoId<LanguagePrototype>? languageOverride`. Такой override меняет язык
конкретного сообщения, не меняя `CurrentLanguage`.

Текущий чат проверяет только существование override-прототипа. Право говорящего
на этот язык должно быть проверено вызывающей стороной. `OSay` ограничивает
выбор содержимым `SpokenLanguages`.

### Добавление нового источника знаний

Предпочтительный путь:

1. Хранить собственное состояние в тематическом компоненте.
2. Подписаться на `CollectLanguageKnowledgeEvent` нужной сущности.
3. Добавить языки в множества события либо установить
   `UnderstandsAllLanguages`.
4. При изменении источника вызвать серверный `LanguageSystem.UpdateLanguages()`.

Не следует напрямую изменять `LanguageSpeakerComponent`, поскольку следующий
пересчёт полностью заменит его множества.

### Добавление языка

Нужны:

1. `language` prototype в подходящем каталоге
   `Resources/Prototypes/_Onyx/Language/`.
2. Fluent-ключи `language-<ID>-name` и `language-<ID>-description` для
   поддерживаемых локалей.
3. Способ искажения и, при необходимости, speech overrides.
4. Выдача языка через `LanguageKnowledgeComponent`, переводчик либо обработчик
   `CollectLanguageKnowledgeEvent`.
5. Проверка локальной речи, шёпота, меню и жизненного цикла источника.

Новые файлы механики должны оставаться в соответствующих `_Onyx`-каталогах.
Неизбежные изменения файлов вне `_Onyx` требуют предметных маркеров `Onyx-*`.

## Команды

`LanguageCommands.cs` предоставляет:

- `languagelist`: выводит доступные языки;
- `languageselect <language>`: выбирает язык через серверную проверку.

Текст команд сейчас задан на английском непосредственно в C#.

## Интеграция с бумагой

Языковые метаданные бумаги хранятся отдельно от пользовательского Robust markup
в `PaperLanguageComponent`. Каждый `PaperLanguageSegment` задаёт UTF-16 диапазон
через `Start`, `Length` и `ProtoId<LanguagePrototype>`. Диапазоны отсортированы,
не пересекаются, покрывают содержимое; соседние диапазоны одного языка
объединяются.

Основные файлы:

- `Content.Shared/_Onyx/Language/Paper/PaperLanguageData.cs`;
- `Content.Server/_Onyx/Language/Paper/PaperLanguageSystem.cs`;
- `Content.Client/_Onyx/Language/Paper/PaperLanguageTag.cs`.

`PaperComponent.Content` не реплицируется как component state. Исходный текст
остаётся на сервере. При открытии сервер строит отдельный `PaperLanguageViewMessage`
для конкретного читателя:

- понятные сегменты сохраняются;
- непонятные сегменты обфусцируются существующим алгоритмом языка;
- пользовательские markup-теги сохраняются и не проходят через обфускацию;
- доверенный `[paperlang]` применяется только сервером;
- клиентский `PaperLanguageTag` получает шрифт и размер из прототипа языка;
- цвет бумаги определяется обычным paper style и пользовательским `[color]`.

Персональное представление заранее отправляется конкретному читателю при
приближении к бумаге и кэшируется клиентом до создания окна. Сервер повторяет
prefetch только при изменении документа, режима, штампов или языковых знаний
читателя. Если UI открыт без актуального prefetch, `BoundUIOpenedEvent` отправляет fallback.
Изменение уже открытого документа отправляет обычное BUI-сообщение. Представление
не кэшируется в компонентах читателя; до его получения окно показывает индикатор
загрузки.

Обфускация использует существующий `RoundId`, поэтому повторное открытие в одном
раунде даёт тот же текст. `AlwaysUnderstood` и `UnderstandsAllLanguages`
обрабатываются через обычный `LanguageSystem.CanUnderstand()`.

### Редактирование бумаги

Клиент записывает каждое событие изменения редактора как упорядоченную операцию
`Replace(start, deleteLength, insertedText)` относительно текущего персонального
представления. При сохранении передаются revision документа, generation конкретного
персонального представления и журнал операций. Язык и исходный текст клиент не
передаёт.

Сервер заново строит персональное представление указанной revision и последовательно
проигрывает операции над списком text spans. Понятный span имеет точное соответствие
видимых и исходных UTF-16 позиций. Непонятный span является атомарным: его можно
удалить или заменить целиком, но нельзя частично изменить либо вставить текст
внутрь. Вставки на границах разрешены.

Нетронутые spans сохраняют серверный оригинал и прежний язык. Вставленный текст
получает текущий язык автора. После replay сервер единственным проходом строит
содержимое и непересекающиеся языковые сегменты; соседние сегменты одного языка
объединяются. Ошибка любой операции отклоняет всё сохранение без изменения бумаги.

Сервер выдаёт право записи отдельно для пары бумага/автор только после успешного
взаимодействия ручкой. Открытый read-UI другого игрока права записи не даёт и не
сбрасывает чужого автора. Право удаляется после сохранения, закрытия UI либо
удаления бумаги/игрока. Save дополнительно проверяет открытый UI, revision и
generation, ограничивает число операций и объём вставок, проверяет UTF-16 границы.

Proactive view используется только как оптимизация открытия: он отправляется в
ограниченном радиусе при прямой видимости. Открытие UI всегда получает отдельное
авторитетное представление; prefetch не заменяет открытый редактор.

Язык с `RequiresSight`, включая `Sign`, нельзя сохранить на бумаге. Сервер делает
ранний выход, показывает popup и повторно отправляет write-view; окно остаётся
открытым.

Программный `PaperSystem.SetContent()` назначает всему заменённому тексту
`Universal`. Явные смешанные сегменты записываются через серверный
`PaperLanguageSystem.SetContent()`.

### Факс и копирование

`FaxPrintout` хранит `LanguageSegments`. Они проходят через локальную копию,
очередь печати, device-network payload и восстановление напечатанной бумаги.
Служебная приписка факса получает `Universal`.

`CloningItemEvent` копирует языковые сегменты после штатного копирования текста.
Штампы остаются отдельными от текстовых диапазонов.

## Тестирование

Текущие тесты:

- `Content.Tests/Shared/_Onyx/Language/TranslatorSystemTest.cs`.
- `Content.Tests/Shared/_Onyx/Language/PaperLanguageEditReplayTest.cs`.

Он проверяет только `TranslatorSystem.RequirementsMet()` для режимов any/all и
пустых требований.

Не покрыты автоматическими тестами:

- оба алгоритма искажения и стабильность по `RoundId`;
- агрегация и сброс `LanguageSpeakerComponent`;
- сетевое owner-only состояние;
- серверная валидация `SetLanguageMessage`;
- локальная речь и шёпот;
- `AlwaysUnderstood` и `RequiresSight`;
- mute/sign-интеграция;
- питание, контейнеры и импланты переводчиков;
- радио и replay;
- полнота локализации prototype ID.

## Известные ограничения и риски

1. Радио отправляет понятный текст всем получателям и не проверяет знание языка.
2. Пустой набор `SpokenLanguages` приводит к `CurrentLanguage = Universal`, хотя
   `CanSpeak(entity, Universal)` может вернуть `false`.
3. Отправка обычной речи не проверяет `CanSpeak()` для текущего языка.
4. `GetCurrentLanguage()` скрывает отсутствие или повреждение состояния fallback
   на `Universal`.
5. `IsVisibleLanguage` объявлен, но не используется.
6. `speech.requireSpeech` в
   `Resources/Prototypes/_Onyx/Language/xenomorph_hivemind.yml` не имеет
   соответствующего поля в `LanguageSpeechOverride` и не реализует поведение.
7. Runtime-изменения `LanguageKnowledgeComponent` не инициируют пересчёт
   автоматически.
8. Shutdown способностей мима удаляет `Sign` напрямую, но не вызывает общую
   валидацию `CurrentLanguage`; текущий язык может остаться недоступным.
9. Обход немоты для `Sign` привязан к `MimePowersComponent`, а не к свойству
   языка. Другой немой носитель `Sign` остаётся заблокирован.
10. Визуальные языки используют голосовые радиусы речи и шёпота.
11. Сущность без `BlindableComponent` считается способной видеть жесты.
12. Replay локальной речи и шёпота сохраняет понятную версию.
13. Переводчики не сканируются рекурсивно во вложенных контейнерах.
14. Требования переводчиков проверяются только по врождённым знаниям, поэтому
    переводчики нельзя цепочечно комбинировать.
15. Автовыбор выходного языка переводчика зависит от порядка `HashSet`.
16. Клиентское меню использует `_prototypes.Index()` для сетевых ID; неизвестный
    ID вызовет исключение вместо безопасного пропуска.
17. Appearance переводчика не имеет отдельной startup-синхронизации и зависит от
    событий toggle/power.
18. Prototype ID `OnyxTranslatorBase` использует запрещённый префикс `Onyx` в
    имени реализации.

## Инварианты текущей системы

При изменениях необходимо сохранять следующие свойства:

- сервер окончательно решает, какой язык может выбрать игрок;
- клиент получает языковые знания только своей сущности;
- локальное понимание вычисляется отдельно для каждого слушателя;
- inline actions не должны превращаться в языковые псевдослоги;
- штатные акценты применяются до языкового искажения;
- смена источника знаний должна завершаться полным пересчётом runtime-кэша;
- удаление языка должно валидировать `CurrentLanguage`;
- `AlwaysUnderstood` должно работать без компонента слушателя;
- визуальный язык не должен требовать слуха;
- новые источники знаний должны интегрироваться через
  `CollectLanguageKnowledgeEvent`, если нет доказанной необходимости менять ядро.

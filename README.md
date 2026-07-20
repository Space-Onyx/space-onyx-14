<div class="header" align="center">
<img alt="Space Station 14" width="880" height="300" src="https://raw.githubusercontent.com/space-wizards/asset-dump/de329a7898bb716b9d5ba9a0cd07f38e61f1ed05/github-logo.svg">
</div>

Space Onyx - это активно модифицируемый и основывающийся форк на [Corvax](https://github.com/space-syndicate/space-station-14)

## Ссылки

[Наш Discord](https://discord.gg/f5rcgkkgzm) | [Наша Вики](https://wiki.spaceonyx.online/) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Клиент без Steam](https://spacestation14.io/about/nightlies/) | [Основной репозиторий](https://github.com/space-wizards/space-station-14)

## Документация

На официальном сайте с [документацией](https://docs.spacestation14.io/) имеется вся необходимая информация о контенте SS14, движке, дизайне игры и многом другом. Также имеется много информации для начинающих разработчиков.

## Контрибьют

Мы рады принять вклад от любого человека. Заходите в Discord, если хотите помочь. У нас есть [список проблем](https://github.com/Space-Onyx/space-onyx-14/issues), которые нужно решить, и любой может за них взяться. Не бойтесь просить о помощи!
Только убедитесь, что ваши изменения и PRы соответствуют [руководству по контрибьюту](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

## Сборка

1. Склонируйте этот репозиторий локально.
2. Скачайте Dotnet 10 SDK с официального сайта Microsoft.
3. Откройте консоль в директории проекта.
4. Запустите `git submodule update --init --recursive` для инициализации подмодулей и скачивания движка.
5. Соберите проект с помощью `dotnet build`.

[Более подробная инструкция по запуску проекта.](https://docs.spacestation14.com/en/general-development/setup.html)

## Лицензия

Весь код проекта распространяется по лицензии AGPL-3.0-or-later. Для некоторых файлов также может быть доступна альтернативная лицензия, указанная в соответствующем файле `.license`. Полные тексты лицензий находятся в каталоге `LICENSES/`.

Большинство медиа-активов лицензированы по [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/), если не указано иное. Информация о лицензии и авторских правах для активов находится в файле метаданных. [Пример](https://github.com/space-wizards/space-station-14/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

Обратите внимание, что некоторые активы лицензированы по некоммерческой лицензии [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) или аналогичным некоммерческим лицензиям, и их необходимо удалить, если вы планируете использовать этот проект в коммерческих целях.

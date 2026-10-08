# Reserved Rules Lab

Небольшой React-прототип для экспериментов с правилами зарезервированных клеток.

## Что здесь

- `generator.mjs` — чистый генератор клеточного автомата:
  - строится фиксированный **core**: двери, коридоры к хабу, центр хаба и структура;
  - всё остальное поле — динамическое и управляется только клеточным автоматом;
  - у каждой роли есть отдельные правила **рождения** и **выживания** по количеству соседей каждого типа;
  - точки `Spawn`, `Loot`, `Npc` только сеются перед запуском автомата, но не фиксируются;
  - никаких постпроцессоров, которые принудительно добавляют, удаляют или обрезают клетки, больше нет.
- `app.mjs` — интерфейс на React + `htm`.
- `index.html` — точка входа, импортирует React через CDN.
- `styles.css` — тёмная тема и сетка.

## Запуск

Нужен любой статический HTTP-сервер, потому что браузер не даёт загружать ES-модули с `file://`.

```bash
cd Prototypes/ReservedRules
python3 -m http.server 5177
```

Открой:

```text
http://localhost:5177/
```

Первый запуск требует интернет, потому что React и `htm` приходят с `esm.sh`.

## Как экспериментировать

1. Выбери пресет: `boss`, `normal`, `treasure` или `shop`.
2. Открой раздел **Правила автомата**:
   - переключайся между режимами **Рождение** и **Выживание**;
   - выбери целевую роль (`Corridor`, `Maneuver`, `Spawn`, `Loot`, `NPC`, `Structure`);
   - для каждого типа соседей задай минимальное и максимальное количество;
   - в режиме **Рождение** правила применяются к свободным клеткам;
   - в режиме **Выживание** правила применяются к клеткам, у которых роль уже есть.
3. Смотри итерации слайдером — видно, как поле сходится.
4. Наведи на клетку: увидишь роли, принадлежность к core и количество соседей по всем типам.
5. Скопируй JSON правил, чтобы сохранить удачную конфигурацию или вставить её обратно.

## Связь с Unity

Роли в прототипе совпадают с твоим `CellRole`:

```csharp
[Flags]
public enum CellRole : byte
{
    None = 0,
    Corridor = 1,
    Maneuver = 2,
    Spawn = 4,
    Loot = 8,
    Npc = 16,
    Structure = 32
}
```

Прототип не меняет Unity-код. Он нужен, чтобы быстро подобрать локальные правила, прежде чем переносить их в `RoomGenerator` и `RoomTemplateConfig`.

## Публикация на GitHub Pages

Приложение статическое и использует относительные пути, поэтому его можно публиковать как в корень сайта, так и в подкаталог репозитория.

### Через GitHub Actions

1. Создай файл `.github/workflows/deploy-reserved-rules.yml`:

```yaml
name: Deploy Reserved Rules Lab

on:
  push:
    branches: [teacher] # или main — укажи свою рабочую ветку

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: pages
  cancel-in-progress: true

jobs:
  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/configure-pages@v5
      - uses: actions/upload-pages-artifact@v3
        with:
          path: Prototypes/ReservedRules
      - uses: actions/deploy-pages@v4
        id: deployment
```

2. В настройках репозитория открой **Settings → Pages**.
3. В блоке **Build and deployment** выбери **Source: GitHub Actions**.
4. Сделай push в рабочую ветку из `branches`.
5. После выполнения workflow открой адрес из раздела **Pages**.

### Без GitHub Actions

Если не хочешь настраивать CI, можно опубликовать ветку `gh-pages` только с содержимым `Prototypes/ReservedRules`:

```bash
git subtree push --prefix Prototypes/ReservedRules origin gh-pages
```

Затем в **Settings → Pages** выбери ветку `gh-pages` и корень `/`.

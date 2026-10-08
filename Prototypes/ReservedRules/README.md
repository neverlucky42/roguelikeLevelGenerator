# Reserved Rules Lab

Небольшой React-прототип для экспериментов с правилами зарезервированных клеток.

## Что здесь

- `generator.mjs` — чистый генератор клеточного автомата:
  - двери и хаб — якоря;
  - коридоры строятся от входов к хабу как начальные семена;
  - коридоры больше не жёстко зафиксированы — они подчиняются правилам выживания;
  - точки `Spawn`, `Loot`, `Npc` сеются до запуска автомата, чтобы коридоры и манёвр могли на них реагировать;
  - вокруг коридоров и хаба вырастает `Maneuver`;
  - несвязанные острова можно обрезать.
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
2. Крути базовые параметры автомата:
   - `Birth threshold` — сколько соседей нужно, чтобы свободная клетка стала `Maneuver`;
   - `Corridor ring` — минимум `Maneuver`-соседей вокруг коридора;
   - `Max reserved ratio` — целевая доля зарезервированных клеток;
   - `Prune islands` — обрезать несвязанные области.
3. Открой раздел **Правила выживания**:
   - выбери целевую роль (`Corridor`, `Maneuver`, `Spawn`, `Loot`, `NPC`, `Structure`);
   - для каждого типа соседей задай минимальное и максимальное количество;
   - клетка сохраняет роль, только если все диапазоны соблюдены.
4. Смотри итерации слайдером — видно, как поле сходится.
5. Наведи на клетку: увидишь роли, фиксацию и количество соседей по всем типам.
6. Скопируй JSON правил, чтобы сохранить удачную конфигурацию или вставить её обратно.

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

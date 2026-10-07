# Research: контур TypeSpec -> OpenAPI

Тикет: #7, дочерний карты #2. Дата проверки фактов: 2026-10-06 (npm registry, typespec.io, github.com/microsoft/typespec, github.com/microsoft/OpenAPI.NET).

## Решение в двух словах

- TypeSpec 1.16.0, все пакеты одной версией, Node >= 22.
- Папка `contracts/` со своими `package.json`, `tspconfig.yaml` (`kind: project`), `main.tsp`.
- Эмитим OpenAPI 3.0.0 в `contracts/generated/openapi.json`; артефакт коммитим, в CI проверяем drift.
- Линт это `tsp compile . --no-emit --warn-as-error` (отдельной команды `tsp lint` нет) плюс `tsp format --check`.
- Отдельный job `contracts` в `.github/workflows/ci.yml` по образцу job `frontend`.

## Версии пакетов

Актуальные `latest` на npm на 2026-10-06:

| Пакет | Версия | Назначение |
| --- | --- | --- |
| @typespec/compiler | 1.16.0 | компилятор, CLI `tsp`, встроенные декораторы (@service, @doc, @error, @tag) |
| @typespec/http | 1.16.0 | HTTP-биндинг: @route, @get/@post/..., @statusCode, @path, @query, @body |
| @typespec/openapi | 1.16.0 | @operationId, @tagMetadata, @info, @extension |
| @typespec/openapi3 | 1.16.0 | эмиттер OpenAPI 3.0 / 3.1 / 3.2 |

Факты из метаданных пакетов:

- Все четыре пакета версии 1.16.0 требуют Node `>=22.0.0` (поле `engines`). В CI уже используется Node 22 в job `frontend`, требование совместимо.
- peerDependencies у http/openapi/openapi3 требуют compiler `^1.16.0`, поэтому версии держим одинаковыми (lockstep), иначе npm ci упадёт на несовпадении peer-зависимостей.
- Релизная линия живая: 1.16.0 текущий релиз, release notes на typespec.io.

Как фиксируем: диапазоны с крышкой `^1.16.0` (конвенция client/package.json), воспроизводимость обеспечивают коммитимый `contracts/package-lock.json` и `npm ci` в CI.

Анти-рекомендация: пакет `@typespec/best-practices` не использовать. Его `latest` в npm это заброшенный снапшот `0.46.0-dev.0`, собранный под compiler 0.5x. Из линтер-рулсетов актуален `@typespec/http/all` из пакета @typespec/http.

## Структура папки contracts/

```
contracts/
  package.json          # private, только devDependencies
  package-lock.json     # коммитим, нужен для npm ci
  tspconfig.yaml
  main.tsp
  generated/
    openapi.json        # коммитимый артефакт, вход для #5 и #6
```

По мере роста спеки (тикет #8) рядом появятся `models.tsp`, `routes/*.tsp` и т.п.; структура выше это каркас.

`tsp init` с шаблоном "Generic REST API" скаффолдит ровно такую же структуру (main.tsp, tspconfig.yaml с `kind: project`, package.json с библиотеками http/rest/openapi/openapi3 и выбранным эмиттером openapi3). Руками воспроизводим то же самое, чтобы не тащить интерактивный wizard в CI, и не берём @typespec/rest (resource-абстракции нам не нужны).

package.json:

```json
{
  "name": "contracts",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "generate": "tsp compile .",
    "generate:watch": "tsp compile . --watch",
    "lint": "tsp compile . --no-emit --warn-as-error",
    "format": "tsp format \"**/*.tsp\"",
    "format:check": "tsp format --check \"**/*.tsp\""
  },
  "devDependencies": {
    "@typespec/compiler": "^1.16.0",
    "@typespec/http": "^1.16.0",
    "@typespec/openapi": "^1.16.0",
    "@typespec/openapi3": "^1.16.0"
  }
}
```

Замечания:

- Отдельной команды `tsp lint` в CLI нет (в 1.16 действия CLI: compile, format, init, install, info, code, vs). Правила линтера выполняются внутри `tsp compile`, а `--warn-as-error` превращает предупреждения в ненулевой exit code. Поэтому lint = компиляция без эмита.
- Каталог назван `generated/` намеренно: `dist/` попадает под корневой .gitignore репо, `generated/` нет. `node_modules/` уже покрыт .gitignore.

## tspconfig.yaml

```yaml
kind: project

emit:
  - "@typespec/openapi3"

options:
  "@typespec/openapi3":
    # Стабильное имя файла; расширение .json выбирает JSON-сериализацию (file-type по умолчанию выводится из расширения)
    output-file: "openapi.json"
    # Каноническая версия OpenAPI, см. следующий раздел
    openapi-versions:
      - "3.0.0"
    # LF для кроссплатформенно чистых диффов артефакта
    new-line: "lf"
    # Кладём артефакт в contracts/generated/, а не в tsp-output/@typespec/openapi3
    emitter-output-dir: "{project-root}/generated"

# Предупреждения = ошибки, рекомендация документации для CI
warn-as-error: true

linter:
  extends:
    - "@typespec/http/all"
```

Замечания:

- `kind: project` помечает границу проекта; `entrypoint` по умолчанию `main.tsp`, явно не указываем.
- `output-file` допускает интерполяцию `{service-name}`, `{version}` и т.п.; у нас один сервис без версионирования, поэтому имя фиксировано.
- Остальные опции эмиттера (`file-type`, `omit-unreachable-types`, `operation-id-strategy`, `enum-strategy`, `seal-object-schemas`, `safeint-strategy`) пока оставляем дефолтными; они задокументированы в README @typespec/openapi3 и понадобятся разве что по запросам из #5/#6.
- tspconfig.yaml ищется компилятором вверх по дереву от entrypoint; выше `contracts/` своего tspconfig.yaml нет, конфликтов конфигураций не будет.

## Версия OpenAPI: 3.0.0, а не 3.1

Эмиттер @typespec/openapi3 умеет `3.0.0`, `3.1.0` и `3.2.0` (опция `openapi-versions`, массив; дефолт `["3.0.0"]`). Официальный шаблон `tsp init` (rest) начиная с 1.16 предлагает `3.1.0`.

Рекомендация: канонический артефакт в 3.0.0. Аргументы:

1. Даунстрим-генераторы ещё не выбраны (тикеты #5 и #6 открыты). OpenAPI 3.0 поддерживают все кандидаты из обоих списков: orval, openapi-typescript + openapi-fetch, @hey-api/openapi-ts; NSwag, OpenAPI Generator, Microsoft.OpenApi.Readers. Поддержка 3.1 у части кандидатов ограничена или отсутствует; точную матрицу зафиксируют #5/#6. Выбор 3.0.0 не закрывает ни один сценарий, выбор 3.1 закрывает часть кандидатов.
2. Поверхность API простая: nullable-поля, enum, даты, ProblemDetails. Отличия 3.1 (JSON Schema 2020-12, type-массивы вместо `nullable: true`, числовые exclusiveMinimum) нам ничего не дают.
3. Microsoft.OpenApi 2.7.5, на котором закреплён бэкенд, читает и пишет и 3.0, и 3.1 (в v2.x есть `SerializeAsV31`, см. upgrade guide к v3). Ограничения со стороны .NET нет. AspNetCore.OpenApi 10.x совместим только с Microsoft.OpenApi 2.x, а 3.x нам всё равно запрещён (AGENTS.md: ломает source-генератор MapOpenApi).

Переключение это одна строка в tspconfig.yaml:

```yaml
openapi-versions: ["3.0.0", "3.1.0"]   # или только ["3.1.0"]
```

Решение пересматриваем, если #5 и #6 сойдутся на генераторах с полной поддержкой 3.1 (например openapi-typescript + Microsoft.OpenApi.Readers) и захотят более чистую семантику nullable.

## Моделирование API в TypeSpec

Требования к оформлению спецификации (вход для тикета #8, черновик контракта):

1. Корневой namespace с `@service(#{ title: "..." })`; title попадает в `info.title` OpenAPI.
2. Именование по официальному style guide: модели и enum PascalCase, свойства и операции camelCase, файлы kebab-case, отступ 2 пробела. Форматирование принудительно через `tsp format` + CI.
3. `@doc` и `@summary` на русском (конвенция репо: UI-текст на русском), тексты попадают в `description` схемы OpenAPI.
4. Роуты: `@route` на интерфейсах, HTTP-глаголы `@get`/`@post`/`@patch`/`@delete` на операциях, параметры через `@path`/`@query`, тело через `@body`.
5. Теги: встроенный `@tag` на интерфейсах, метаданные и порядок тегов через `@tagMetadata` из @typespec/openapi на namespace. Имена тегов на английском (`Bookings`, `EventTypes`, `Slots`): генераторы клиентов используют теги как технические идентификаторы (группировка методов, имена файлов), кириллица там ломает имена.
6. operationId руками не пишем: полагаемся на дефолтный `operation-id-strategy: "parent-container"`, ID собирается из имени контейнера и операции (например `EventTypes` + `list`). Если тикету #5 понадобится другой формат, переключаем опцию `operation-id-strategy` (есть варианты `fqn`, `explicit-only`, настраиваемый separator) или расставляем `@operationId` из @typespec/openapi точечно.
7. Ошибки: единый `@error`-мodel `ProblemDetails` (RFC 9457), отдаётся с `application/problem+json`; конкретные коды через `@statusCode`. ASP.NET Core сам возвращает ProblemDetails на стандартные 400/404/500, поэтому контракт совпадает с фактическим поведением бэкенда.

Скетч (не финальный контракт, а иллюстрация приёмов для #8):

```typespec
import "@typespec/http";
import "@typespec/openapi";

using Http;
using OpenAPI;

@service(#{ title: "Календарь звонков" })
namespace CallCalendar;

@error
model ProblemDetails {
  type?: string;
  title: string;
  status?: int32;
  detail?: string;
  instance?: string;
}

model NotFoundProblem {
  @statusCode statusCode: 404;
  @header contentType: "application/problem+json";
  @body problem: ProblemDetails;
}

model ConflictProblem {
  @statusCode statusCode: 409;
  @header contentType: "application/problem+json";
  @body problem: ProblemDetails;
}

model EventType {
  id: string;
  @minLength(1) name: string;
  description?: string;
  durationMinutes: int32;
}

@route("/event-types")
@tag("EventTypes")
interface EventTypes {
  @doc("Список типов событий")
  @get
  list(): EventType[] | ProblemDetails;
}
```

8. Даты: `plainDate` для дат `YYYY-MM-DD` (совпадает с `DateStringValue` Mantine v9 на клиенте), `utcDateTime` для меток времени слотов, `int32` для длительностей в минутах.
9. Валидация входа прямо в моделях: `@minLength`/`@maxLength`/`@pattern`/`@format("email")`. Это ляжет в схемы OpenAPI и дальше в генерируемую валидацию запросов (#6).
10. Версионирования нет: @typespec/versioning не подключаем, сервис один и без версий.

## Команды

Из каталога `contracts/` (`npm run <script>`), из корня репо: `npm --prefix contracts run <script>`.

| Команда | Что делает |
| --- | --- |
| `generate` | `tsp compile .`, эмитит `generated/openapi.json` |
| `generate:watch` | watch-режим компилятора |
| `lint` | компиляция без эмита, warnings как ошибки |
| `format` | форматирует `**/*.tsp` |
| `format:check` | проверка форматирования без записи, для CI |

## CI-шаг

Новый job `contracts` в `.github/workflows/ci.yml`, зеркалит job `frontend`:

```yaml
contracts:
  runs-on: ubuntu-latest
  defaults:
    run:
      working-directory: contracts
  steps:
    - uses: actions/checkout@v7

    - name: Setup Node
      uses: actions/setup-node@v4
      with:
        node-version: '22'
        cache: 'npm'
        cache-dependency-path: contracts/package-lock.json

    - name: Install
      run: npm ci

    - name: Lint (tsp compile, warnings as errors)
      run: npm run lint

    - name: Format check
      run: npm run format:check

    - name: Generate OpenAPI + drift check
      run: |
        npm run generate
        git diff --exit-code -- contracts/generated/openapi.json
```

Замечания:

- Node 22 удовлетворяет `engines` всех пакетов 1.16.0; `cache: 'npm'` с `cache-dependency-path` на lockfile зеркалит job `frontend`.
- Drift check падает, если `*.tsp` поменяли, а артефакт не перегенерировали: коммитимый openapi.json не расходится с контрактом.
- При внедрении добавить job в документированный в AGENTS.md порядок CI (backend -> format/test, frontend lint -> build -> test, contracts).

## Коммитим ли сгенерированный openapi.json

Да, как и зафиксировано в каркасе карты (#2): дифф артефакта это главный инструмент ревью изменений контракта, а артефакт служит входом для codegen в #5/#6 без обязательного Node-окружения у потребителя.

Условия чистых диффов:

- JSON с LF (`new-line: "lf"`), фиксированное имя файла (`output-file: "openapi.json"`).
- Детерминированный вывод эмиттера: один и тот же tsp даёт тот же файл.
- Drift check в CI (выше) принуждает коммитить свежую версию.
- Каталог `generated/`, а не `dist/` (последний в .gitignore).

## Риски и связи с другими тикетами

- #5 (клиент): требования выбранного генератора к форме схемы (operationId, enums, nullable) вернутся сюда дополнениями к разделу моделирования; основные рычаги уже есть (`operation-id-strategy`, `enum-strategy`).
- #6 (бэкенд): решит, уходит ли рантайм-эндпоинт `/openapi/v1.json` (MapOpenApi). Пока он жив, в репо два источника схемы; канонический именно `contracts/generated/openapi.json`.
- Дрейф версий TypeSpec: пакеты обновляем синхронно (lockstep), автоматики типа Renovate/Dependabot в репо нет.
- Если позже понадобится OpenAPI 3.2 (эмиттер уже умеет), это снова одна строка конфига, но Microsoft.OpenApi 2.7.5 его не читает (3.2 появился в Microsoft.OpenApi 3.x, который нам запрещён), значит 3.2 закрыт до снятия пина.

## Источники

- Версии, engines, peerDependencies: npm registry, https://registry.npmjs.org/@typespec/compiler/latest , https://registry.npmjs.org/@typespec/http/latest , https://registry.npmjs.org/@typespec/openapi/latest , https://registry.npmjs.org/@typespec/openapi3/latest
- Опции эмиттера openapi3 (openapi-versions, output-file, new-line, operation-id-strategy, enum-strategy): https://github.com/microsoft/typespec/blob/main/packages/openapi3/README.md
- Схема tspconfig.yaml (kind, entrypoint, emit, options, linter, warn-as-error, discovery): https://typespec.io/docs/handbook/configuration/configuration/
- CLI tsp (нет отдельной команды lint): https://typespec.io/docs/handbook/cli/ и список actions в https://github.com/microsoft/typespec/tree/main/packages/compiler/src/core/cli/actions
- Форматтер (`tsp format --check`): https://typespec.io/docs/handbook/formatter/
- Style guide (именование, отступы): https://typespec.io/docs/handbook/style-guide/
- Встроенные декораторы (@service, @doc, @summary, @error, @tag, @minLength, @pattern, @format): https://typespec.io/docs/standard-library/built-in-decorators/
- Декораторы @typespec/http (@route, @get, @statusCode, @body, @header, правило линтера @typespec/http/all): https://github.com/microsoft/typespec/blob/main/packages/http/README.md
- Декораторы @typespec/openapi (@operationId, @tagMetadata, @info): https://github.com/microsoft/typespec/blob/main/packages/openapi/README.md
- Шаблон tsp init rest (структура проекта, дефолт OpenAPI 3.1): https://github.com/microsoft/typespec/blob/main/packages/compiler/templates/scaffolding.json и https://github.com/microsoft/typespec/blob/main/packages/compiler/templates/rest/main.tsp
- Релиз 1.16.0: https://typespec.io/release-notes/typespec-1-16-0/
- Microsoft.OpenApi 2.x читает/пишет 3.0 и 3.1, совместимость AspNetCore.OpenApi 10.x только с 2.x: https://github.com/microsoft/OpenAPI.NET/blob/main/docs/upgrade-guide-3.md
- Заброшенность @typespec/best-practices: https://registry.npmjs.org/@typespec/best-practices/latest

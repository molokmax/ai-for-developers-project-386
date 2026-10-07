# Календарь звонков

Учебный проект Хекслета: сервис бронирования календаря. Два независимых стека в одном репо.

## Структура

- `src/CallCalendar.Api/` - бэкенд: .NET 10, ASP.NET Core Minimal API, EF Core + SQLite. Входная точка `Program.cs`; эндпоинты в `Endpoints/` (extension-методы на `IEndpointRouteBuilder`), данные в `Data/`
- `client/` - фронтенд: Vite + React 19 + TypeScript + Mantine v9, TanStack Query, react-router. Сгенерированный по контракту SDK: `src/api/gen/` (orval), руками не править
- `contracts/` - контракт API: TypeSpec (`main.tsp`) -> OpenAPI 3.0 (`generated/openapi.json`, артефакт коммитится)
- `tests/CallCalendar.Api.Tests/` - интеграционные тесты: xUnit + `WebApplicationFactory`, изолированная temp-SQLite на прогон

## Команды

Бэкенд (из корня, .NET SDK 10; Windows - запускать через `cmd /c "..."`, PowerShell съедает вывод кодов выхода):

```bash
dotnet build -p:TreatWarningsAsErrors=true          # линт стиля ловится здесь
dotnet format --verify-no-changes                   # проверка форматирования
dotnet test                                         # все тесты
dotnet test --filter FullyQualifiedName~SmokeTests  # один класс/метод
```

Клиент (из `client/`):

```bash
npm run lint     # oxlint, НЕ eslint
npm run build    # tsc -b + vite build (это же проверка типов)
npm test         # vitest run; watch: npm run test:watch
npm run api:gen       # регенерация SDK из ../contracts/generated/openapi.json (orval)
npm run api:gen:watch # watch-режим генерации SDK
```

Порядок в CI (`.github/workflows/ci.yml`): backend build(warnaserror) -> format -> test; frontend lint -> build -> test.

## Запуск dev

- Терминал 1: `dotnet run --launch-profile http` из `src/CallCalendar.Api` -> http://localhost:5262
- Терминал 2: `npm run dev` из `client` -> http://localhost:5173
- Фронт дергает API через Vite-прокси `/api`; CORS настраивать не нужно, HTTPS-редирект намеренно отключен в Program.cs
- OpenAPI-схема: `/openapi/v1.json`, только Development

## Бэкенд: нюансы

- В Development при старте сами применяются миграции и сидируются слоты (`DbSeeder`); `callcalendar.db` в gitignore
- Миграции из `src/CallCalendar.Api/`: `dotnet ef migrations add Name`; `**/Migrations/**` - генерируемый код, не править руками, style-правила там отключены
- `public partial class Program;` в конце Program.cs нужен для `WebApplicationFactory<Program>`, не удалять
- `Microsoft.OpenApi` закреплен на 2.7.5 (CVE-2026-49451, GHSA-v5pm-xwqc-g5wc). Не повышать до 3.x: ломает source-генератор `Microsoft.AspNetCore.OpenApi` (CS0200 на MapOpenApi)
- В шаблонах .NET 10 нет Swashbuckle/Swagger UI
- `Directory.Build.props` включает `EnforceCodeStyleInBuild`, `AnalysisMode=Recommended`, `GenerateDocumentationFile` (последняя - ради IDE0005 на билде, CS1591 подавлен)

## Фронтенд: нюансы (Mantine v9)

- Даты в API v9 - строки `YYYY-MM-DD` (`DateStringValue`), не `Date`. `DatePicker` - календарь без инпута; для поля ввода с дропдауном брать `DateInput`. Пропсов `cancelLabel`/`clearLabel` в v9 нет
- Уведомления: статический `notifications.show()` из `@mantine/notifications`; хук `useNotifications()` не даёт метод `show`
- Тесты в jsdom требуют моки `matchMedia`/`ResizeObserver` (уже в `src/test/setup.ts`), иначе Mantine падает при рендере
- Русская локализация дат: `DatesProvider settings={{ locale: 'ru' }}` + `import 'dayjs/locale/ru'`

## Контракт и генерация SDK

- Контракт API авторствуется в `contracts/main.tsp` (TypeSpec), это источник истины; артефакт `contracts/generated/openapi.json` коммитится
- После правки контракта: `npm run generate` из `contracts/`, затем `npm run api:gen` из `client/` - оба результата коммитятся (`contracts/generated/openapi.json` и `client/src/api/gen/`)
- `npm run api:gen:check` из `client/` - drift-check: регенерация + `git diff --exit-code ./src/api/gen`; годится для CI
- `client/src/api/gen/` генерируется orval, oxlint настроен игнорировать папку (ignorePatterns в `.oxlintrc.json`)
- orval законтрен точной версией в `client/package.json` (без `^`), обновление осознанное отдельным PR; требует Node >= 22.18

## Прочее

- Релизы ведёт release-please: пуш/мерж в `main` запускает `.github/workflows/release-please.yml`, который создаёт или обновляет release PR (`chore: release ...`). Релиз и тег создаются мержем release PR - версии руками не проставлять, теги руками не пушить. Отсюда обязательность Conventional Commits
- Сообщения коммитов - по спецификации Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `chore:`, `refactor:` и т.п.), текст на английском
- `.github/workflows/hexlet-check.yml` - служебный, не удалять/не редактировать/не переименовывать. Свои CI-шаги добавлять отдельным workflow
- UI-текст и комментарии в проекте на русском; на английском - только идентификаторы

## Agent skills

### Issue tracker

Issues трекаются в GitHub Issues этого репо через `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Дефолтная лексика: каждый лейбл равен имени роли. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` в корне репо. See `docs/agents/domain.md`.

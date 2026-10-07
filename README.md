# Календарь звонков


[![hexlet-check](https://github.com/molokmax/ai-for-developers-project-386/actions/workflows/hexlet-check.yml/badge.svg)](https://github.com/molokmax/ai-for-developers-project-386/actions)

Разработайте совместно с ИИ сервис для бронирования календаря

Учебный проект Хекслета: https://ru.hexlet.io/programs/ai-for-developers
Как это должно работать: https://files.hexlet.app/a/2ipc5m

## Стек

- Backend: .NET 10, ASP.NET Core Minimal API, EF Core + SQLite
- Frontend: TypeScript, Vite, React, Mantine, TanStack Query, React Router

## Установка

Клонирование и установка зависимостей:

```bash
git clone https://github.com/molokmax/ai-for-developers-project-386.git
cd ai-for-developers-project-386

# Backend
dotnet restore src/CallCalendar.Api

# Frontend
cd client
npm install
cd ..
```

Переменные окружения не требуются. Строка подключения к SQLite лежит в
`src/CallCalendar.Api/appsettings.Development.json`. Миграции и демо-данные
применяются автоматически при запуске в окружении Development.

## Использование

Запуск бэкенда (терминал 1):

```bash
cd src/CallCalendar.Api
dotnet run --launch-profile http
```

Сервис доступен на http://localhost:5262, OpenAPI-схема на http://localhost:5262/openapi/v1.json.

Запуск фронтенда (терминал 2):

```bash
cd client
npm run dev
```

Открыть http://localhost:5173. Фронт ходит на API через Vite-прокси (`/api`),
поэтому CORS не нужен.

## Docker

Образ содержит и API, и собранный SPA: контейнер раздаёт фронтенд и `/api` на одном порту.

```bash
# Локальный запуск (сборка + контейнер + volume для SQLite)
docker compose up --build
```

Приложение доступно на http://localhost:8080. Порт меняется переменной `PORT`:

```bash
PORT=3000 docker compose up --build
```

- SQLite-база лежит в named volume `callcalendar-data` (`/data/callcalendar.db`)
- Миграции и демо-данные применяются при старте контейнера (`RUN_MIGRATIONS=1`,
  отключается `-e RUN_MIGRATIONS=`)
- Сборка CI: PR - только `docker build` без публикации, push в `main` и теги `v*` -
  публикация в `ghcr.io/molokmax/ai-for-developers-project-386`
  (`.github/workflows/docker.yml`)
- Деплой последней релизной версии на VPS: `scripts/deploy.sh`, инструкция -
  [docs/deploy.md](docs/deploy.md)

Полезные команды:

```bash
# Линт бэкенда: проверка стиля (.editorconfig) и форматирования
dotnet build           # warnings = 0 при нарушениях стиля
dotnet format CallCalendar.slnx --verify-no-changes

# Автотесты бэкенда
dotnet test

# Сборка, проверка типов и линт фронтенда
cd client
npm run build    # tsc -b + vite build
npm run lint     # oxlint

# Тесты клиента (Vitest + Testing Library)
npm test
npm run test:watch

# Новые миграции EF Core
cd src/CallCalendar.Api && dotnet ef migrations add ИмяМиграции
```

---

<details>
<summary>Автоматические тесты Хекслета</summary>

Тесты запускаются на каждый коммит. За запуск отвечает файл `.github/workflows/hexlet-check.yml` — не удаляйте и не переименовывайте ни его, ни репозиторий.

</details>

## CI

Workflow `.github/workflows/ci.yml` запускается на каждый push и PR:

- backend: `dotnet build -p:TreatWarningsAsErrors=true` (линт стиля из
  `.editorconfig`), `dotnet format --verify-no-changes` (форматирование),
  `dotnet test`
- frontend: `npm run lint` (oxlint), `npm run build` (tsc + vite),
  `npm test` (vitest)

Файл `.github/workflows/hexlet-check.yml` - служебный, тесты Хекслета, он не изменён.

## О Хекслете

[Хекслет](https://ru.hexlet.io/) — школа программирования: авторские программы обучения с практикой, поддержкой наставников и реальными проектами, которые остаются в резюме. Этот репозиторий — один из таких проектов.

# Деплой на VPS

Скрипт `scripts/deploy.sh` поднимает на VPS последний релизный образ из GHCR
через обычный `docker run` (без docker compose). Образ содержит и API, и SPA:
контейнер раздаёт фронтенд и `/api` на одном порту.

Что делает скрипт:

1. Определяет имя образа из `git remote get-url origin`
   (например `ghcr.io/molokmax/ai-for-developers-project-386`).
2. Берёт версию: из аргумента, иначе из последнего релиза GitHub
   (`releases/latest`; при приватном репо - fallback на `gh release list`).
3. По SSH: `docker pull`, пересоздание контейнера, очистка висячих образов.
4. Ждёт зелёный `/api/health` до 60 секунд, иначе подсказывает, где смотреть логи.

Контейнер запускается с `-e RUN_MIGRATIONS=1` (миграции и сид применяются при
старте) и `-v callcalendar-data:/data` (SQLite переживает пересоздание
контейнера). Порт на хосте - `8088`, внутри контейнера - `8080`.

## Где что хранится

- SSH-параметры (IP, логин, ключ) - в `.env.deploy` в корне репо.
  Файл не коммитится (`.gitignore`), шаблон: `.env.deploy.example`.
- Версия образа - нигде не хранится: скрипт читает последний релиз GitHub.
  Релизы создаёт release-please (мерж release PR -> тег `v*` -> CI публикует
  образ с тегом `X.Y.Z`).

## Настройка VPS (один раз)

1. Установить Docker (официальный convenience-скрипт или по инструкции дистрибутива).
   Вместе с Docker ставится `curl` - он нужен скрипту для healthcheck.

   ```bash
   curl -fsSL https://get.docker.com | sh
   ```

2. Разрешить пользователю запуск docker (или деплоить от root).

   ```bash
   sudo usermod -aG docker deploy
   ```

3. Авторизоваться в GHCR, если пакет приватный (по умолчанию так). Нужен
   Personal Access Token с правом `read:packages`
   (GitHub -> Settings -> Developer settings -> Tokens).

   ```bash
   docker login ghcr.io
   # Login: <github-username>
   # Password: <PAT>
   ```

4. Открыть порт 8088 в firewall.

   ```bash
   sudo ufw allow 8088/tcp
   ```

5. Положить публичный ключ на сервер: `ssh-copy-id -i ~/.ssh/id_ed25519_vps deploy@<host>`.

## Настройка локальной машины

1. Установленный `curl`, `ssh`, `git`. Для приватного репозитория - `gh`
   (`gh auth login`), либо всегда указывать версию аргументом.
2. Скопировать шаблон и заполнить:

   ```bash
   cp .env.deploy.example .env.deploy
   ```

   ```dotenv
   DEPLOY_HOST=<ip-адрес VPS>
   DEPLOY_PORT=22
   DEPLOY_USER=<пользователь>
   DEPLOY_SSH_KEY=~/.ssh/id_ed25519_vps
   ```

Windows: скрипт запускать в Git Bash или WSL. `.gitattributes` принудительно
ставит LF для `*.sh`, поэтому после checkout скрипт работает как есть.

## Запуск

```bash
# Последний релиз
scripts/deploy.sh

# Конкретная версия (в том числе откат)
scripts/deploy.sh 1.2.3
```

Даунтайм между `docker rm -f` и `docker run` - порядка секунды.

## Диагностика

```bash
# Логи приложения
ssh <user>@<host> docker logs callcalendar

# Состояние контейнера и healthcheck
ssh <user>@<host> docker ps -a --filter name=callcalendar

# Список доступных версий (для отката)
gh release list --repo <owner>/<repo> --limit 5
```

Если healthcheck не проходит, но контейнер запущен: проверить `docker logs`
на ошибки миграций и что volume `callcalendar-data` существует
(`docker volume ls`). Повторный запуск скрипта безопасен: он пересоздаёт
контейнер, volume не трогает.

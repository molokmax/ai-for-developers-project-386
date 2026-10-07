#!/usr/bin/env bash
# Деплой последней релизной версии приложения на VPS.
#
# Использование:
#   scripts/deploy.sh            # задеплоить последний релиз из GitHub
#   scripts/deploy.sh 1.2.3      # задеплоить (или откатить на) конкретную версию
#
# Параметры SSH берутся из файла .env.deploy в корне репо (не коммитится),
# шаблон: .env.deploy.example. Подробности: docs/deploy.md.
set -euo pipefail

# Имя контейнера на сервере и порт на хосте VPS.
CONTAINER_NAME="callcalendar"
HOST_PORT="8088"
# Порт внутри контейнера фиксирован (PORT по умолчанию в образе).
CONTAINER_PORT="8080"

die() {
  echo "Ошибка: $*" >&2
  exit 1
}

# --- корень репо: скрипт можно звать из любого каталога ---
script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd "$script_dir/.." && pwd)
cd "$repo_root"

# --- имя образа из git remote ---
remote_url=$(git remote get-url origin 2>/dev/null) || die "git remote 'origin' не найден. Запускать из клона репозитория."
case "$remote_url" in
  https://github.com/*) repo_path=${remote_url#https://github.com/} ;;
  git@github.com:*)     repo_path=${remote_url#git@github.com:} ;;
  *) die "Remote '$remote_url' не похож на GitHub-репозиторий." ;;
esac
repo_path=${repo_path%.git}
# GHCR требует нижний регистр в имени образа.
image_base="ghcr.io/$(echo "$repo_path" | tr '[:upper:]' '[:lower:]')"

# --- параметры подключения из .env.deploy ---
env_file="$repo_root/.env.deploy"
[ -f "$env_file" ] || die "Нет файла $env_file. Создай его из шаблона .env.deploy.example (см. docs/deploy.md)."
# shellcheck source=/dev/null
source "$env_file"

: "${DEPLOY_HOST:?В .env.deploy не задан DEPLOY_HOST}"
: "${DEPLOY_USER:?В .env.deploy не задан DEPLOY_USER}"
DEPLOY_PORT=${DEPLOY_PORT:-22}
DEPLOY_SSH_KEY=${DEPLOY_SSH_KEY:-}

ssh_opts=(-o ConnectTimeout=10 -o StrictHostKeyChecking=accept-new -o BatchMode=yes)
if [ -n "$DEPLOY_SSH_KEY" ]; then
  # Разворачиваем ~ в домашний каталог: ssh -i сам этого не делает.
  DEPLOY_SSH_KEY=${DEPLOY_SSH_KEY/#\~/$HOME}
  [ -f "$DEPLOY_SSH_KEY" ] || die "SSH-ключ не найден: $DEPLOY_SSH_KEY"
  ssh_opts+=(-i "$DEPLOY_SSH_KEY")
fi
ssh_target="${DEPLOY_USER}@${DEPLOY_HOST}"

# --- версия: аргумент или последний релиз в GitHub ---
if [ $# -ge 1 ]; then
  version=$1
else
  version=$(curl -fsS "https://api.github.com/repos/$repo_path/releases/latest" \
    | sed -n 's/.*"tag_name" *: *"\([^"]*\)".*/\1/p' || true)
  if [ -z "$version" ]; then
    # Приватный репо: неавторизованный API возвращает 404. Пробуем авторизованный gh.
    command -v gh >/dev/null 2>&1 || die "Не удалось получить последний релиз через GitHub API. Установи gh (gh auth login) или укажи версию аргументом."
    version=$(gh release list --repo "$repo_path" --limit 1 --json tagName --jq '.[0].tagName') || die "gh не смог получить список релизов репозитория $repo_path."
  fi
fi
[ -n "$version" ] || die "Не удалось определить версию релиза."
version=${version#v}
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Версия '$version' не семвер вида X.Y.Z."
image="$image_base:$version"

echo "Деплой $image на $ssh_target (порт $HOST_PORT на хосте)..."

# Переменные передаются окружением перед bash -s: heredoc в кавычках,
# поэтому всё внутри выполняется на сервере без локальной подстановки.
ssh "${ssh_opts[@]}" "$ssh_target" \
  "DEPLOY_IMAGE='$image' CONTAINER_NAME='$CONTAINER_NAME' HOST_PORT='$HOST_PORT' CONTAINER_PORT='$CONTAINER_PORT' bash -s" <<'REMOTE'
set -euo pipefail
echo "==> docker pull $DEPLOY_IMAGE"
docker pull "$DEPLOY_IMAGE"
echo "==> перезапуск контейнера $CONTAINER_NAME"
docker rm -f "$CONTAINER_NAME" 2>/dev/null || true
docker run -d --name "$CONTAINER_NAME" \
  --restart unless-stopped \
  -p "$HOST_PORT:$CONTAINER_PORT" \
  -e RUN_MIGRATIONS=1 \
  -v callcalendar-data:/data \
  "$DEPLOY_IMAGE"
echo "==> очистка висячих образов"
docker image prune -f
REMOTE

# --- healthcheck: /api/health на хосте VPS через опубликованный порт ---
echo "==> healthcheck (до 60 секунд)"
health_ok=false
for _ in $(seq 1 30); do
  if ssh "${ssh_opts[@]}" "$ssh_target" "curl -fsS -m 5 http://localhost:$HOST_PORT/api/health >/dev/null 2>&1"; then
    health_ok=true
    break
  fi
  sleep 2
done

if [ "$health_ok" = true ]; then
  echo "Деплой успешен."
  echo "Версия: $version"
  echo "Образ:  $image"
  echo "URL:    http://$DEPLOY_HOST:$HOST_PORT"
else
  echo "Healthcheck не прошёл за 60 секунд." >&2
  echo "Логи контейнера: ssh $ssh_target docker logs $CONTAINER_NAME" >&2
  echo "Состояние:       ssh $ssh_target docker ps -a --filter name=$CONTAINER_NAME" >&2
  exit 1
fi

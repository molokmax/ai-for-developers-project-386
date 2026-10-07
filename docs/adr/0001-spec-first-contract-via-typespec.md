---
status: accepted
---

# Спецификация-first контракт API через TypeSpec

Контракт API авторствуется в TypeSpec (`contracts/main.tsp`); из него эмитится OpenAPI 3.0 (`contracts/generated/openapi.json`, коммитится в репо), из OpenAPI генерируется клиент фронтенда (orval, хуки TanStack Query) и бэкенд-артефакты (MinimalOpenAPI: records, маршруты, базовые хендлеры). Выбрано вместо code-first (рантайм-схема через `Microsoft.AspNetCore.OpenApi` / `MapOpenApi`): контракт становится единственным источником истины, диффуется в PR и проверяется в CI drift-check'ом, ручные дубликаты типов на фронте исчезают как класс.

## Considered Options

- Code-first с рантайм-эндпоинтом `/openapi/v1.json`: нет коммитимого артефакта, типы фронта дублируются руками (проблема, которую это решение и устраняет).
- TypeSpec `http-server-csharp` для бэкенда: alpha-стадия, генерирует контроллеры, не minimal API.
- NSwag / OpenAPI Generator для бэкенд-артефактов: не дают стабов под minimal API; оставлены запасным путём (не понадобился, подтверждено спайком).

## Consequences

- Валидация бэкенда делается собственным DataAnnotations-фильтром: встроенный `AddValidation` не видит типы из чужого source generator (проверено спайком).
- `MapOpenApi`, пакет `Microsoft.AspNetCore.OpenApi` и рантайм-эндпоинт `/openapi/v1.json` убираются; пин `Microsoft.OpenApi` 2.7.5 перестаёт быть критичным.
- Изменение API = правка `main.tsp` + регенерация; дифф `contracts/generated/openapi.json` обязателен в PR.
- Сгенерированный код бэкенда не коммитится (генерация при сборке), код клиента коммитится; generated-дереву бэкенда нужен `NoWarn` CS1591.

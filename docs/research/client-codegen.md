# Research: генерация клиента фронтенда из OpenAPI

Тикет: #5 (родительская карта: #2). Дата исследования: 2026-10-06. Все версии и факты проверены по первоисточникам (npm registry, GitHub API, официальная документация), ссылки в конце документа.

## Контекст

Клиент: React 19.2, TypeScript ~6.0.2, Vite 8, TanStack Query v5.104, ESM, Windows. Codegen отсутствует: ручные fetch-обёртки и руками продублированные типы в `client/src/api/` (сейчас там один `slots.ts` с `SlotDto`/`CreateBookingRequest`). По решениям карты #2 контракт пишется TypeSpec-first: из `contracts/` генерируется OpenAPI, из него клиент фронтенда. Значит, вход генератора у нас чистый и управляемый, разумные требования к форме спеки выполнимы.

## Критерии

Генерация хуков TanStack Query v5, качество и строгость типов, поддержка OpenAPI 3.0/3.1, живость проекта, интеграция в npm scripts и watch-режим, объём зависимостей. Дополнительно для нашего стека: совместимость с TypeScript 6 и с Vite 8.

## Факты по кандидатам

### orval 8.40.0 (основной кандидат)

- Релиз 2026-10-04; каденс примерно раз в 3-4 дня (8.33: 13.09, 8.35: 20.09, 8.37: 23.09, 8.39: 30.09, 8.40: 04.10). Репозиторий orval-labs/orval: 6509 звёзд, 15 открытых issues, коммиты ежедневно, организация с несколькими активными мейнтейнерами (anymaniax, melloware и др.). MIT.
- Хуки TanStack Query: `client: 'react-query'` генерирует полноценные `useQuery`/`useMutation`/`useInfiniteQuery` на каждую операцию, плюс `get*QueryKey`, `queryOptions`, хелперы `useSet*QueryData`/`useGet*QueryData`, поддержка `skipToken`, per-operation overrides. Поддержка именно v5 подтверждена changelog'ом (v8.40.0: prefetch через `queryClient.query()` на TanStack Query 5.102+).
- OpenAPI: Swagger 2.0, 3.0 и 3.1; внутри всё апгрейдится до 3.1 через `@scalar/openapi-parser` (см. input.mdx: трансформер запускается до "OpenAPI 2.0 to 3.1 upgrade"). В 8.35.x закрыта серия багов по OAS 3.1 nullable/binary. Вход по умолчанию валидируется, есть `--fail-on-warnings` для CI.
- TypeScript 6: пакет не имеет peerDependency на typescript, а сам репозиторий собран с typescript 6.0.3 (devDependencies). Конфликтов при установке с `typescript ~6.0.2` не будет.
- CLI: `--watch`/`-w`, `--clean`, `--formatter prettier|biome|oxfmt`, `--fail-on-warnings`. Конфиг `orval.config.ts` с `defineConfig`.
- Требования окружения: Node.js >= 22.18 (engines пакета и README).
- Зависимости генератора: около 26 прямых пакетов (набор `@orval/*`, `@scalar/openapi-parser`, chokidar, commander и т.д.), всё dev-only. Сгенерированный клиент работает на нативном fetch (`httpClient: 'fetch'`), runtime-зависимостей в бандле не появляется; axios не обязателен. Опциональный peer: prettier (нужен только для `--formatter prettier`).
- operationId: не обязателен. При отсутствии имя операции собирается из verb + path (PascalCase), что подтверждено исходником `getOperationId` в `packages/core/src/getters/operation.ts`. Но `override.operations`, фильтры и query key-настройки адресуются по operationId, поэтому для стабильного API он фактически нужен.
- Enums: plain `enum` даёт union-типы; `x-enumNames`/`x-enum-varnames`/`x-enumDescriptions` и OpenAPI 3.1 `oneOf`+`const` с `title` дают именованные const-объекты (`enumGenerationType: 'const' | 'union' | 'enum'`; вариант `'enum'` несовместим с `erasableSyntaxOnly`).

### openapi-typescript 7.13.0 + openapi-fetch 0.17.0 + openapi-react-query 0.5.4

- Релизы всей тройки: 2026-02-11 (8 месяцев назад); packument openapi-typescript менялся 2026-06-15. Монорепо openapi-ts/openapi-typescript: 8394 звезды, 284 открытых issues. MIT.
- Поддержка OpenAPI 3.0 и 3.1, включая дискриминаторы. Это эталон по строгости типов: генерация runtime-free типов, ключи по path/method, operationId не нужен.
- Хуков как codegen нет: `openapi-react-query` даёт runtime-обёртку `$api.useQuery('get', '/slots')` поверх `openapi-fetch` (0.x, peer `@tanstack/react-query ^5.80.0`).
- Блокер для нашего стека: `peerDependencies: { typescript: "^5.x" }` у openapi-typescript 7.13.0, devDeps тоже TS ^5.9.3. При нашем `typescript ~6.0.2` установка потребует `npm overrides`/`--legacy-peer-deps`, а корректность типов на TS 6 не проверена апстримом.
- CLI без watch-режима: в таблице флагов на странице CLI есть `--check`, но нет `--watch`. Нужен внешний watcher.
- Самый лёгкий footprint: у генератора 6 зависимостей, runtime openapi-fetch около 6 kB.

### @hey-api/openapi-ts 0.99.0 (запасной кандидат)

- Packument обновлялся 2026-09-30; репозиторий hey-api/hey-api: 5474 звёзд, 663 открытых issues, коммиты активные. MIT. Релизы частые, пакет в статусе 0.x: документация прямо требует пиновать точную версию и читать migration notes на каждый breaking release.
- `peerDependencies: { typescript: ">=5.5.3 || >=6.0.0 || 6.0.1-rc" }`: TypeScript 6 поддерживается декларативно. Node >= 22.18.
- TanStack Query v5 plugin (`@tanstack/react-query`): генерирует `*Options()` (queryOptions), `*Mutation()`, `*QueryKey()`, infinite-варианты. Это не готовые хуки, а options-фабрики, которые раскладываются в `useQuery(...)` руками: по стилю это современный рекомендуемый TanStack-паттерн, но по критерию "генерация хуков" слабее orval.
- OpenAPI: "accepts any OpenAPI specification" (3.0 и 3.1).
- CLI имеет `--watch`/`-w` с опциональным интервалом (проверено по исходнику `packages/codegen-cli/src/command.ts`). Плюс официальный `@hey-api/vite-plugin` с поддержкой Vite 5-8: генерация запускается при старте Vite-пайплайна (`vite.apply: 'serve'` для dev-only).
- Зависимости генератора: 10 пакетов (`@hey-api/codegen-core` и др.). Сгенерированный вывод содержит папки `client/` и `core/` со scaffolding-клиентом (`@hey-api/client-fetch`): runtime-код внутри output-папки, не из npm.

### kubb 5.5.x

- `@kubb/cli` 5.5.2 вышел 2026-10-06, релизы почти ежедневные (5.4.0: 29.09, 5.4.2: 01.10, 5.5.0: 05.10, 5.5.2: 06.10). kubb-labs/kubb: 1812 звёзд, 5 открытых issues. Фактический bus factor 1: stijnvanhulle.
- Читает OpenAPI 2.0/3.0/3.1 через `@kubb/adapter-oas` (внутри апгрейд до 3.1). `@kubb/plugin-react-query` генерирует настоящие хуки `useQuery`/`useMutation`/`useInfiniteQuery`, peer `@tanstack/react-query ^5.0.0`.
- CLI: `kubb generate --watch`/`-w` (проверено по исходнику `packages/cli/src/commands/generate.ts`: следит за файлом спеки или поллит URL). Node >= 22. Есть `unplugin-kubb` для Vite.
- Сильный, но самый молодой из зрелых кандидатов; стек плагинов тянет несколько `@kubb/*` пакетов.

### openapi-qraft 2.14.1 и openapi-generator

- `@openapi-qraft/react` 2.14.1 (2026-05-26), репозиторий OpenAPI-Qraft/openapi-qraft: 98 звёзд, последний push 2026-06-16. Proxy-based runtime-хуки, React 19 в peer range есть, но проект мал и в затишье: исключаем.
- openapi-generator (typescript-fetch и пр.): базовый вариант индустрии, но требует Java-runtime и даёт более слабые типы; для нашего стека глубоко не оценивался.

## Сравнение по критериям

| Критерий | orval | openapi-typescript + openapi-react-query | @hey-api/openapi-ts | kubb |
| --- | --- | --- | --- | --- |
| Хуки TanStack Query v5 | готовые хуки + query keys + helpers | runtime-обёртка, без codegen хуков | options-фабрики (queryOptions/mutationOptions) | готовые хуки |
| Строгость типов | высокая | эталонная | высокая | высокая |
| OpenAPI 3.0/3.1 | оба (апгрейд до 3.1) | оба | оба | оба (апгрейд до 3.1) |
| TypeScript 6 | нет peer-конфликта | peerDep `^5.x`, конфликт | peer range включает 6.x | нет peer-конфликта |
| Живость | очень высокая, 15 open issues | средняя, 284 open issues | высокая, 663 open issues, 0.x | очень высокая, bus factor 1 |
| npm scripts + watch | `--watch`, `--fail-on-warnings` | watch отсутствует | `--watch` + Vite-плагин | `--watch` |
| Зависимости | ~26 (dev-only), runtime 0 | минимум (генератор 6 + runtime 6 kB) | 10 (dev-only) + runtime в output | несколько `@kubb/*` |

## Рекомендация

Основной вариант: orval. Он единственный закрывает критерий "генерация хуков TanStack Query" напрямую (полноценные хуки, а не runtime-прокси или options-фабрики), при этом не конфликтует с TypeScript 6, имеет встроенные watch и CI-режим (`--fail-on-warnings`), валидирует входную спеку (страховка для TypeSpec-эмиттера), поддерживает и 3.0, и 3.1, и является самым живым проектом из рассмотренных с очень здоровым бэклогом (15 открытых issues при 6.5k звёзд). Форматтер `oxfmt` ложится на наш oxc-стек (oxlint), иначе prettier тащить только ради генерируемого кода не хотелось бы.

Запасной вариант: @hey-api/openapi-ts. Декларативная поддержка TS 6 в peer range, официальный плагин под Vite 8, современный стиль queryOptions. Риски: ветка 0.x с регулярными breaking changes (обязателен пин точной версии) и большой открытый бэклог. Если orval по какой-то причине не зайдёт (например, не понравится стиль сгенерированных хуков), переход на hey-api дешёвый: входной OpenAPI тот же.

Отвергнуто: openapi-typescript-тройка (peerDep на TS 5 при нашем TS 6, нет codegen хуков, нет watch), kubb (сильный, но молодой и с bus factor 1; держим на радаре), openapi-qraft (98 звёзд, затишье с мая 2026), openapi-generator (Java-runtime).

## Скетч конфига и команд (orval)

OpenAPI-документ берём из TypeSpec-эмиттера (`contracts/`), в dev можно альтернативно целиться в URL бэкенда (`http://localhost:5262/openapi/v1.json`, он отдаётся только в Development).

```ts
// client/orval.config.ts
import { defineConfig } from 'orval';

export default defineConfig({
  callcalendar: {
    input: {
      target: '../contracts/openapi.yaml',
    },
    output: {
      target: './src/api/gen/endpoints.ts',
      schemas: './src/api/gen/model',
      mode: 'tags-split',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        query: {
          useQuery: true,
          useMutation: true,
        },
      },
    },
  },
});
```

```json
// client/package.json
{
  "scripts": {
    "api:gen": "orval --config ./orval.config.ts --fail-on-warnings",
    "api:gen:watch": "orval --watch --config ./orval.config.ts",
    "api:gen:check": "npm run api:gen && git diff --exit-code ./src/api/gen"
  }
}
```

Сгенерированный код коммитим (CI в `api:gen:check` ловит рассинхрон со спекой), папку `src/api/gen` исключаем из oxlint. Форматирование: `--formatter oxfmt` либо без форматтера. Старый `client/src/api/slots.ts` после перехода удаляется, его типы приезжают из `src/api/gen/model`.

Запасной скетч (hey-api):

```ts
// client/openapi-ts.config.ts
import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: '../contracts/openapi.yaml',
  output: 'src/api/gen',
  plugins: ['@hey-api/client-fetch', '@tanstack/react-query'],
});
```

```json
{ "scripts": { "api:gen": "openapi-ts", "api:gen:watch": "openapi-ts --watch" } }
```

## Требования к форме OpenAPI (вход для TypeSpec-контракта)

Эти требования стоит зафиксировать в контракте `contracts/`, чтобы codegen был стабильным:

1. operationId: явный и уникальный у каждой операции (в TypeSpec это имя операции или `@operationId`). orval без него не падает, но имена хуков соберутся из verb+path и сломаются при смене роутов; кроме того, per-operation overrides и инвалидация адресуются по operationId.
2. Версия спеки: 3.0 или 3.1, обе принимаются (orval внутри апгрейдит до 3.1). nullable можно писать в любом стиле (`nullable: true` для 3.0 или `type: [..., "null"]` для 3.1), поддержка 3.1-форм в orval 8.35+ доработана специально.
3. Модели: именованные `components/schemas` (TypeSpec `model`), без анонимных inline-схем в ответах и запросах: inline-схемы получают автогенерированные имена и портят читаемость типов.
4. Enums: именованные схемы с `enum`; для читаемых имён членов полагаться на `x-enum-varnames`/`x-enumNames` (это то, что эмиттит TypeSpec для enum) или на 3.1-запись `oneOf`+`const` с `title`. По умолчанию получим union-типы строк.
5. Tags: проставить теги операциям, они управляют и группировкой файлов (`mode: 'tags-split'`), и фильтрацией.
6. Обязательность полей: аккуратно вести `required` в схемах, от этого зависит optionality в TS-типах. Даты: `format: date-time` по умолчанию мапится в `string` (есть опция трансформации в `Date`, нам не нужна: Mantine v9 работает со строками).
7. Спека должна быть валидной: orval по умолчанию валидирует вход парсером Scalar, невалидная спека это ошибка генерации. Это плюс для TypeSpec-first: эмиттер гарантирует валидность.
8. Стабильные имена схем без коллизий: переименование схемы это ломающее изменение сгенерированного API.

## Риски

- Node.js >= 22.18 обязателен для orval 8 (а также для hey-api 0.99 и kubb 5). Надо зафиксировать версию Node в CI и у разработчиков (например, `engines` в package.json клиента). Vite 8 сам по себе требует современный Node, так что это не новое ограничение, но 22.18 это свежий минимум.
- orval релизится очень часто (еженедельно и чаще): пинуем точную версию, обновляем осознанно через отдельные PR, иначе сгенерированный код будет "плыть".
- Объём dev-зависимостей orval (~26 пакетов) больше, чем у hey-api (10) и openapi-typescript (6). На runtime-бандл не влияет: сгенерированный клиент на нативном fetch.
- Стиль сгенерированных хуков orval (один options-объект параметром, кастомные обёртки над `useQuery`) отличается от каноничного TanStack-стиля. Если команде критичен именно каноничный `useQuery(queryOptions(...))`, запасной hey-api ближе.
- Windows: риск низкий, у orval в CI есть Windows-прогоны (changelog v8.36.0 упоминает фикс таймаутов vitest на Windows CI), пути в конфиге держим относительными через `./`.

## Источники

- https://registry.npmjs.org/orval/latest (версия 8.40.0, engines node >=22.18, зависимости, devDep typescript 6.0.3)
- https://api.github.com/repos/orval-labs/orval (звёзды, issues, активность)
- https://api.github.com/repos/orval-labs/orval/releases?per_page=8 (каденс релизов, TanStack Query 5.102+, фиксы OAS 3.1)
- https://raw.githubusercontent.com/orval-labs/orval/master/README.md (поддерживаемые клиенты, Node 22.18+)
- https://raw.githubusercontent.com/orval-labs/orval/master/docs/content/docs/quick-start.mdx, .../guides/react-query.mdx, .../guides/enums.mdx, .../reference/cli.mdx, .../reference/configuration/input.mdx (конфиг, react-query, enums, --watch/--fail-on-warnings, апгрейд до 3.1, валидация)
- https://raw.githubusercontent.com/orval-labs/orval/master/packages/core/src/getters/operation.ts (fallback имени операции без operationId)
- https://registry.npmjs.org/openapi-typescript/latest, https://registry.npmjs.org/openapi-fetch/latest, https://registry.npmjs.org/openapi-react-query/latest (версии, peerDep typescript ^5.x, peer @tanstack/react-query ^5.80.0)
- https://api.github.com/repos/openapi-ts/openapi-typescript (+ /releases) (звёзды, issues, даты релизов 7.12/7.13)
- https://openapi-ts.dev/introduction, https://openapi-ts.dev/cli (поддержка 3.0/3.1, флаги CLI без --watch)
- https://registry.npmjs.org/@hey-api%2Fopenapi-ts/latest (0.99.0, peer typescript >=5.5.3 || >=6.0.0, engines node >=22.18)
- https://api.github.com/repos/hey-api/hey-api (+ /releases) (звёзды, issues, активность)
- https://heyapi.dev/docs/openapi/typescript/get-started, .../configuration, .../configuration/vite, .../plugins/tanstack-query, .../output (0.x и пин версий, плагины, Vite 5-8, структура output)
- https://raw.githubusercontent.com/hey-api/hey-api/main/packages/codegen-cli/src/command.ts (CLI-флаг --watch)
- https://registry.npmjs.org/@kubb%2Fcli/latest, https://registry.npmjs.org/@kubb%2Fplugin-react-query/latest (5.5.2 / 5.1.4, peer @tanstack/react-query ^5)
- https://api.github.com/repos/kubb-labs/kubb (+ /releases) (звёзды, каденс)
- https://raw.githubusercontent.com/kubb-labs/kubb/main/README.md (OpenAPI 2.0/3.0/3.1, unplugin-kubb)
- https://raw.githubusercontent.com/kubb-labs/kubb/main/packages/cli/src/commands/generate.ts (`kubb generate --watch`)
- https://registry.npmjs.org/@openapi-qraft%2Freact/latest, https://api.github.com/repos/OpenAPI-Qraft/openapi-qraft (2.14.1, 98 звёзд, затишье)
- npm view time.modified/time.created для всех перечисленных пакетов (даты последних публикаций)

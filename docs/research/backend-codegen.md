# Research: генерация бэкенд-артефактов из OpenAPI (.NET 10 minimal API)

Тикет: [#6](https://github.com/molokmax/ai-for-developers-project-386/issues/6), карта: [#2](https://github.com/molokmax/ai-for-developers-project-386/issues/2).
Дата проверки фактов: 2026-10-06. Все версии и возможности сверены с первоисточниками (nuget.org, официальная документация, репозитории), ссылки в конце.

## Вопрос

Чем генерировать из OpenAPI (который эмитит TypeSpec из `contracts/`): C#-типы (DTO/records), схемы валидации запросов, заготовки эндпоинтов и маршрутов под .NET 10 minimal API. Ограничения из тикета: `net10.0`, `Nullable enable`, `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` в CI, `Microsoft.OpenApi` закреплён на 2.7.5 (CVE-2026-49451, не повышать до 3.x), эндпоинты живут в `Endpoints/` как extension-методы на `IEndpointRouteBuilder`.

## Обзор кандидатов

| Кандидат | Версия (на 2026-10-06) | Что генерирует | Minimal API | Вердикт |
|---|---|---|---|---|
| MinimalOpenAPI (Kralizek) | 1.1.0 (2026-10-03), MIT, net10.0, без зависимостей | Контрактные records/enums, базовые классы хендлеров с типизированными `Results<>`, DI-регистрация, маппинг маршрутов, OpenAPI-метаданные, DataAnnotations-метаданные констрейнтов | Да, это его единственная цель | Рекомендуемый, с оговорками по зрелости |
| NSwag | 14.7.1 (2026-04-20), MIT | C# DTO (в т.ч. records, NRT, DataAnnotations, System.Text.Json) и клиенты; серверные стабы только как MVC-контроллеры (`CSharpControllerGenerator`) | Нет (серверная часть) | Fallback: только DTO |
| OpenAPI Generator, генератор `aspnetcore` | линия 7.x, в конфиге уже есть `aspnetCoreVersion: 10.0` (дефолт) | Полный standalone-проект на MVC-контроллерах + Swashbuckle | Нет | Не подходит |
| TypeSpec `@typespec/http-server-csharp` | 0.58.0-alpha.32 | MVC-контроллеры (`ControllerBase`, `IActionResult` по changelog), модели, моки | Нет, и статус alpha | Не подходит сейчас |
| Microsoft.OpenApi.Readers / Microsoft.OpenApi 2.7.5 + свой генератор | 2.7.5 (2026-05-26); линия 2.x жива (2.12.2 от 2026-08-20), 2.7.4 помечен уязвимым | Ничего сам; даёт объектную модель (`OpenApiDocument.LoadAsync`) для своего генератора | Зависит от нас | Fallback: собственный генератор стабов |
| Kiota, Refitter, AutoRest, swagger-codegen | - | HTTP-клиенты (Kiota: "OpenAPI based HTTP Client code generator") | Нет | Не по задаче |
| Прочие source generators (напр. ErrorOrX) | - | Не из OpenAPI | - | Не по задаче |

Ключевой факт: готового серверного генератора под minimal API ни у Microsoft, ни в openapi-generator, ни в TypeSpec нет. Все «большие» инструменты генерируют MVC-контроллеры. Единственный найденный живой contract-first генератор именно под ASP.NET Core Minimal APIs - MinimalOpenAPI (Roslyn source generator, генерация на билде, OpenAPI 3.0 и 3.1, YAML/JSON, несколько документов на проект).

## Рекомендуемый контур

### Основной: MinimalOpenAPI 1.1.0 + встроенная валидация .NET 10

Пакет: `MinimalOpenAPI` 1.1.0 (таргетит `net10.0`, зависимостей нет: парсер YAML/JSON вшит в source generator как implementation detail, поэтому пин `Microsoft.OpenApi` 2.7.5 он вообще не трогает).

Подключение (после появления `contracts/openapi/openapi.yaml` из TypeSpec):

```xml
<!-- src/CallCalendar.Api/CallCalendar.Api.csproj -->
<PackageReference Include="MinimalOpenAPI" Version="1.1.0" />
<OpenApi Include="..\..\contracts\openapi\openapi.yaml"
         Namespace="CallCalendar"
         ReadWriteSchemaHandling="Auto"
         PublishAs="/openapi/schema.yaml" />
```

```csharp
// Program.cs
builder.Services.AddMinimalOpenApi();
builder.Services.AddValidation(); // валидация .NET 10, см. ниже
...
app.MapMinimalOpenApiEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApiSchemas(); // отдаёт авторский openapi.yaml
}
```

Что генерируется на каждую операцию: контрактные records/enums, абстрактный класс `<OperationId>EndpointBase` с типизированной сигнатурой `HandleAsync` и union `Results<Ok<T>, NotFound, ...>` по объявленным в спеке ответам, DI-регистрация, маппинг маршрутов и OpenAPI-метаданные. Отсутствующая реализация ловится диагностикой генератора (линейка `MOA001`-`MOA017`) на этапе компиляции: компилятор сам гоняет нас за контрактом.

Граница generated / ручной код:

- Генерируется (НЕ коммитим): контракты, базовые классы хендлеров, маршрутизация, DI, метаданные. Source generator работает внутри компиляции, файлов на диске нет; для инспекции включается стандартный `EmitCompilerGeneratedFiles` (путь добавить в `.gitignore`).
- Коммитим (ручное): `contracts/openapi/openapi.yaml` (эмит TypeSpec), классы-реализации `public sealed class CreateBookingEndpoint(AppDbContext db) : CreateBookingEndpointBase` с бизнес-логикой (EF Core, проверка пересечения слотов, окно 14 дней), endpoint-конфигурации (через генерируемый `EndpointConfigurationBase`, если понадобятся политики), инфраструктурные эндпоинты вне спеки (`/api/health` остаётся ручным `MapHealthEndpoints`).

Существующий паттерн `Endpoints/` как extension-методы на `IEndpointRouteBuilder` сохраняется для ручных эндпоинтов; спековые эндпоинты переезжают на генерируемый маппинг.

### Fallback (если spike по MinimalOpenAPI провалится): NSwag DTO-only + собственный генератор стабов

- DTO: NSwag 14.7.1 (`NSwag.ConsoleCore` как dotnet tool или `NSwag.MSBuild` для генерации на билде), генератор `openApiToCSharpClient` с выключенными клиентскими классами и включёнными DTO. Настройки генератора C# (по документации NJsonSchema `CSharpGeneratorSettings`): `classStyle: Record`, `generateNullableReferenceTypes: true`, `generateDataAnnotations: true`, `jsonLibrary: SystemTextJson`, `dateTimeType: System.DateTime`, `generateOptionalPropertiesAsNullable: true`. Точные имена ключей nswag.json финализируются на реализации по NSwag Configuration Document.
- Стабы маршрутов: свой консольный генератор (~200-400 строк) на `Microsoft.OpenApi` 2.7.5 (`OpenApiDocument.LoadAsync`, ридеры встроены в основной пакет линии 2.x), эмитящий `MapXxxEndpoints` extension-методы в текущем стиле `Endpoints/`. Пин 2.7.5 соблюдается (до 3.x не поднимаем; при желании можно поднять внутри линии 2.x, где актуальна 2.12.2).
- При коммите сгенерированного кода: заголовок `// <auto-generated/>` (освобождает от style-анализаторов при `EnforceCodeStyleInBuild`) + CI freshness-check (перегенерация + `git diff --exit-code`).

## Что коммитим, а что генерируется при сборке

Решение: при основном контуре (source generator) C#-артефакты не коммитятся вообще, генерируются на каждом билде из закоммиченного `openapi.yaml`. Это убирает класс проблем «забыли перегенерировать», не требует freshness-check в CI, а diff в PR всегда читаемый (меняется спека и ручные хендлеры, не простыни generated-кода). Коммитим: TypeSpec-источник, эмитнутый OpenAPI (для ревью и для фронтенд-codegen), ручные хендлеры.

## Валидация запросов

Решение: DataAnnotations из спеки + встроенная валидация .NET 10, а не FluentValidation.

Факты:

- В .NET 10 minimal APIs получили встроенную валидацию: `builder.Services.AddValidation()` регистрирует source generator + endpoint filter; валидируются query/header/body по атрибутам `System.ComponentModel.DataAnnotations` (включая records и вложенные объекты), ошибка = 400 с problem details (кастомизируется через `IProblemDetailsService`), на эндпоинт можно повесить `DisableValidation()`. Top-level API стабильно (экспериментальны только внутренние resolver API).
- MinimalOpenAPI эмитит DataAnnotations-метаданные из констрейнтов схем (string/number/array), но сам валидацию не исполняет (прямо указано в его Limitations).
- FluentValidation 12.1.1 (2025-12-03) жив и остаётся Apache-2.0, но правила пишутся руками и будут дрифтовать от спеки; берём только если появятся сложные кросс-полевые правила, не выразимые в схеме.

Важный технический риск (проверить в spike первым делом): `AddValidation` сам использует source generator и обнаруживает валидируемые типы синтаксически, в хендлерах той же сборки, а выход одного source generator не виден другому. Маппинг эндпоинтов у нас генерируется MinimalOpenAPI, поэтому авто-дiscovery может не сработать. Запасной путь без смены контура: свой endpoint filter (~30 строк на `Validator.TryValidateObject`) поверх тех же DataAnnotations, подключаемый через генерируемый `EndpointConfigurationBase` (у MinimalOpenAPI это штатная точка расширения для политик эндпоинтов). Решение о механике принимается по результатам spike, схема валидации (DataAnnotations из спеки) не меняется.

Бизнес-правила (свободность слота, окно записи 14 дней, конфликт 409) остаются в ручных хендлерах: это не валидация формата запроса.

## Судьба `/openapi/v1.json` (MapOpenApi)

Решение: уходит. После перехода на spec-first рантайм-генерация документа из кода (`AddOpenApi`/`MapOpenApi`, пакет `Microsoft.AspNetCore.OpenApi`) становится вторым, неканоническим источником правды и удаляется. Вместо неё авторский документ отдаётся как статика: `PublishAs="/openapi/schema.yaml"` + `app.MapOpenApiSchemas()` в Development (дескрипторы можно скормить Scalar/Swagger UI). Побочный плюс: из рантайм-проекта уходит зависимость на `Microsoft.OpenApi`, и пин 2.7.5 (CVE-2026-49451) перестаёт быть критичным для приложения; он останется актуален только если fallback-генератор возьмёт `Microsoft.OpenApi` как библиотеку. Drift-detection между кодом и спекой не нужен: соответствие маршрутов и сигнатур обеспечивает компилятор через генерируемые базовые классы и диагностики `MOAxxx`.

## Требования к форме OpenAPI (входной контракт для codegen)

- `openapi: 3.0.0` (дефолт эмиттера `@typespec/openapi3`, опция `openapi-versions`; 3.0.0 совместим и с MinimalOpenAPI, и с NSwag-fallback; 3.1.0 тоже поддержан MinimalOpenAPI, но сужает fallback).
- Обязательный уникальный `operationId` на каждую операцию: из него строятся имена классов (`GetSlotsEndpointBase`), дубликаты ловятся диагностикой.
- Без `oneOf`/`anyOf`: MinimalOpenAPI их не поддерживает. В TypeSpec это значит: не использовать union-типы в моделях API, вместо union литералов использовать enum; `allOf` допустим (флаттенится).
- Именованные component-схемы для тел запросов/ответов (inline поддержан, но именованные дают читаемые имена records); детерминированная нормализация имён с диагностикой коллизий есть.
- Констрейнты прямо в схемах: `required`, `minLength`/`maxLength`, `pattern`, `format` (`email`, `date-time`, `uuid`), `minimum`/`maximum`, `minItems`/`maxItems` - из них генерируются DataAnnotations.
- Все статус-коды операций объявлены явно (включая 400/404/409); для ошибок `application/problem+json` - под них генерируются типизированные обёртки.
- `readOnly`/`writeOnly` на свойствах (например, `id` записи) при `ReadWriteSchemaHandling="Auto"` даёт раздельные request/response контракты.
- YAML или JSON, один документ на сервис (несколько документов поддержаны, но нам хватит одного).
- OpenAPI 2.0 не поддерживается (и не нужен).

## Оценка рисков

| Риск | Уровень | Митигация |
|---|---|---|
| MinimalOpenAPI молод: 1.0.0 от 2026-07-15, ~1.8K загрузок суммарно, один мейнтейнер, 2 звезды | Средний-высокий | Spike-тикет до внедрения; MIT и открытый код (можно форкнуть/завендорить); поверхность контакта крошечная (csproj-итем + 2 вызова в Program.cs + наследование от базовых классов); задокументирован fallback (NSwag + свой генератор стабов) |
| `AddValidation` (.NET 10) может не увидеть типы из чужого source generator (генераторы не видят выход друг друга) | Средний | Проверка в spike; запасной путь - свой DataAnnotations endpoint filter через `EndpointConfigurationBase`, схема валидации не меняется |
| Сгенерированный код vs `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` + `dotnet format` | Низкий | Выход source generator помечается как generated и пропускается style-анализаторами; проверяется в spike полным CI-прогоном |
| Незрелые углы генератора (нет oneOf/anyOf, cookie-параметры вручную, >6 типов ответов = вложенные `Results<>`) | Низкий-средний | Требования к форме OpenAPI выше; наш API маленький (слоты, записи, типы событий), в ограничения вписываемся |
| Отказ от `Microsoft.AspNetCore.OpenApi` ломает dev-привычку ходить на `/openapi/v1.json` | Низкий | `MapOpenApiSchemas()` отдаёт авторский YAML на `/openapi/schema.yaml` в Development |
| Интеграционные тесты на `WebApplicationFactory<Program>` | Низкий | Генерация на билде работает и в тестовом проекте; `public partial class Program` нетронут |

## Следующий шаг

Завести spike/prototype-тикет (после появления `contracts/`): подключить MinimalOpenAPI 1.1.0 к ветке с минимальным контрактом (2-3 операции: список слотов, создание записи), прогнать полный CI (`dotnet build -p:TreatWarningsAsErrors=true`, `dotnet format --verify-no-changes`, `dotnet test`), проверить валидацию 400 на битом запросе и отдачу спеки. Критерий отказа: неустранимые конфликты с CI-строгостью или баги генератора на наших схемах; тогда уходим на fallback-контур.

## Источники

- MinimalOpenAPI: https://github.com/Kralizek/MinimalOpenApi (README, docs/architecture.md), https://www.nuget.org/packages/MinimalOpenAPI (версии 1.0.0 от 2026-07-15, 1.1.0 от 2026-10-03, MIT, net10.0, нет зависимостей)
- .NET 10, валидация minimal APIs и records: https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0 (секция "Validation support in Minimal APIs"), https://learn.microsoft.com/en-us/aspnet/core/fundamentals/validation?view=aspnetcore-10.0
- NSwag: https://www.nuget.org/packages/NSwag.CodeGeneration.CSharp (14.7.1), https://www.nuget.org/packages/NSwag.MSBuild (14.7.1), настройки генератора: https://github.com/RicoSuter/NJsonSchema/wiki/CSharpGeneratorSettings (ClassStyle=Record, GenerateNullableReferenceTypes, GenerateDataAnnotations, GenerateOptionalPropertiesAsNullable, JsonLibrary), https://github.com/RicoSuter/NSwag/wiki (CSharpControllerGenerator для серверных контроллеров, NSwag Configuration Document)
- OpenAPI Generator, aspnetcore: https://openapi-generator.tech/docs/generators/aspnetcore/ (SERVER-генератор, контроллеры + Swashbuckle, `aspnetCoreVersion` включает 10.0)
- TypeSpec: https://github.com/microsoft/typespec/tree/main/packages/openapi3 (опция `openapi-versions`, дефолт `["3.0.0"]`), https://github.com/microsoft/typespec/tree/main/packages/http-server-csharp (README, CHANGELOG: версия 0.58.0-alpha.32, генерация контроллеров `ControllerBase`/`IActionResult`)
- Microsoft.OpenApi: https://www.nuget.org/packages/Microsoft.OpenApi (2.7.5 от 2026-05-26; 2.7.4 deprecated с уязвимостью; линия 2.x актуальна до 2.12.2; 3.x актуальна 3.10.2; `OpenApiDocument.LoadAsync` в README), https://www.nuget.org/packages/Microsoft.OpenApi.Readers (линия 1.6.x, 2.0.0-preview.* deprecated: ридеры влиты в основной пакет 2.x)
- FluentValidation: https://www.nuget.org/packages/FluentValidation (12.1.1, Apache-2.0, net8.0)
- Kiota как клиентский генератор: https://github.com/microsoft/kiota ("OpenAPI based HTTP Client code generator")

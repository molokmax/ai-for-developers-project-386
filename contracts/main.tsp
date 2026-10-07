import "@typespec/http";

using TypeSpec.Http;

@service(#{ title: "Календарь звонков API" })
@doc("""
  API сервиса записи на звонок.
  Гость выбирает тип события и свободный слот в окне записи (14 календарных дней, включая текущий), владелец календаря управляет типами событий и смотрит предстоящие встречи.
  Регистрации и авторизации нет: административный раздел работает от имени единственного заранее заданного профиля владельца.
  Время в API передаётся в UTC (ISO 8601); сетка слотов вычисляется на сервере в часовом поясе владельца (по умолчанию Europe/Moscow).
  """)
namespace CallCalendar;

// ---------- Общие модели ----------

@doc("Тип события: вид встречи, которую владелец календаря предлагает для записи")
model EventType {
  @doc("Идентификатор типа события")
  id: int64;

  @doc("Название")
  @minLength(1)
  name: string;

  @doc("Описание")
  description: string;

  @doc("Длительность в минутах: кратна 30, минимум 30 (кратность проверяется на сервере)")
  @minValue(30)
  durationMinutes: int32;
}

@doc("Краткие данные типа события для вложения в другие модели")
model EventTypeSummary {
  @doc("Идентификатор типа события")
  id: int64;

  @doc("Название")
  name: string;
}

@doc("Свободный слот: возможное время начала записи")
model Slot {
  @doc("Начало слота (UTC). Сетка: каждые 30 минут в интервале 09:00-18:00 по времени владельца календаря")
  startUtc: utcDateTime;

  @doc("Конец слота (UTC): начало плюс длительность типа события. Запись всегда целиком помещается в интервал 09:00-18:00")
  endUtc: utcDateTime;
}

@doc("Запись: занятый слот с контактами гостя")
model Booking {
  @doc("Идентификатор записи")
  id: int64;

  @doc("Тип события")
  eventType: EventTypeSummary;

  @doc("Начало (UTC)")
  startUtc: utcDateTime;

  @doc("Конец (UTC)")
  endUtc: utcDateTime;

  @doc("Имя гостя")
  customerName: string;

  @doc("Email гостя")
  customerEmail: string;
}

@doc("Предстоящая встреча в списке владельца календаря")
model Meeting {
  @doc("Идентификатор записи")
  id: int64;

  @doc("Тип события")
  eventType: EventTypeSummary;

  @doc("Начало (UTC)")
  startUtc: utcDateTime;

  @doc("Конец (UTC)")
  endUtc: utcDateTime;

  @doc("Имя гостя")
  customerName: string;

  @doc("Email гостя")
  customerEmail: string;
}

// ---------- Запросы ----------

@doc("Запрос на создание типа события")
model CreateEventTypeRequest {
  @doc("Название")
  @minLength(1)
  name: string;

  @doc("Описание")
  description: string;

  @doc("Длительность в минутах: кратна 30, минимум 30 (кратность проверяется на сервере)")
  @minValue(30)
  durationMinutes: int32;
}

@doc("Запрос на создание записи")
model CreateBookingRequest {
  @doc("Идентификатор типа события")
  eventTypeId: int64;

  @doc("Начало слота (UTC), должно лежать на сетке с шагом 30 минут в пределах окна записи")
  startUtc: utcDateTime;

  @doc("Имя гостя")
  @minLength(1)
  customerName: string;

  @doc("Email гостя")
  @format("email")
  customerEmail: string;
}

// ---------- Ошибки (RFC 9457 Problem Details) ----------

@doc("Problem Details (RFC 9457)")
model ProblemDetails {
  @doc("URI типа проблемы")
  type?: string;

  @doc("Краткое описание типа проблемы")
  title?: string;

  @doc("HTTP-статус")
  status: int32;

  @doc("Описание конкретного случая")
  detail?: string;

  @doc("URI конкретного случая")
  instance?: string;
}

@error
@doc("Ошибка валидации запроса")
model ValidationProblem {
  @statusCode statusCode: 400;
  ...ProblemDetails;
}

@error
@doc("Ресурс не найден")
model NotFoundProblem {
  @statusCode statusCode: 404;
  ...ProblemDetails;
}

@error
@doc("Конфликт занятости: интервал пересекается с существующей записью")
model ConflictProblem {
  @statusCode statusCode: 409;
  ...ProblemDetails;
}

// ---------- Эндпоинты ----------

@tag("EventTypes")
@route("/api/event-types")
interface EventTypes {
  @doc("Список типов событий. Используется и каталогом гостя, и административным разделом владельца")
  @get
  list(): EventType[];

  @doc("Создать тип события (владелец календаря)")
  @post
  create(@body request: CreateEventTypeRequest):
    | {
        @statusCode statusCode: 201;
        @body eventType: EventType;
      }
    | ValidationProblem;
}

@tag("Slots")
@route("/api/slots")
interface Slots {
  @doc("""
    Свободные слоты для типа события в окне записи: 14 календарных дней по времени владельца, включая текущий день (прошедшие слоты сегодня недоступны).
    Слот свободен, если его интервал не пересекается с интервалами существующих записей любых типов событий.
    """)
  @get
  list(@query eventTypeId: int64): Slot[] | NotFoundProblem;
}

@tag("Bookings")
@route("/api/bookings")
interface Bookings {
  @doc("""
    Создать запись на выбранный слот (гость).
    Идемпотентно по заголовку Idempotency-Key: повтор с тем же ключом возвращает первичный результат, дубль не создаётся.
    При пересечении интервала с существующей записью: 409.
    """)
  @post
  create(
    @header("Idempotency-Key")
    @doc("Ключ идемпотентности: UUID, генерируется клиентом на попытку записи")
    idempotencyKey: string,

    @body request: CreateBookingRequest,
  ):
    | {
        @statusCode statusCode: 201;
        @header("Location") location: string;
        @body booking: Booking;
      }
    | ValidationProblem
    | NotFoundProblem
    | ConflictProblem;
}

@tag("Meetings")
@route("/api/meetings")
interface Meetings {
  @doc("Предстоящие встречи всех типов событий (владелец календаря), сортировка по времени начала; прошедшие не возвращаются")
  @get
  list(): Meeting[];
}

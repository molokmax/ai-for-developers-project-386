import { defineConfig } from 'orval'

// Конфиг генерации клиентского SDK из OpenAPI-контракта.
// Источник истины: contracts/main.tsp (TypeSpec) -> contracts/generated/openapi.json.
// Сгенерированный код коммитится; регенерация npm run api:gen, руками не править.
export default defineConfig({
  callcalendar: {
    input: '../contracts/generated/openapi.json',
    output: {
      target: './src/api/gen/endpoints.ts',
      schemas: './src/api/gen/model',
      // Группировка по тегам: на каждый тег (Bookings, EventTypes, ...) свой файл
      mode: 'tags-split',
      // Хуки TanStack Query + транспорт на нативном fetch (без axios)
      client: 'react-query',
      httpClient: 'fetch',
      // Чистить папки вывода перед генерацией, чтобы не оставались хвосты удалённых операций
      clean: true,
    },
  },
})

// Проверка, что сгенерированный SDK (orval, src/api/gen) реально работает в стеке
// фронтенда: хуки TanStack Query резолвятся, модели типов совпадают с ответом API.
// Файл лежит вне src/api/gen: папка генерации чистится при регенерации (clean: true).

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useEventTypesCreate, useEventTypesList } from './gen'
import type { EventType } from './gen/model'

const eventTypes: EventType[] = [
  { id: 1, name: 'Вводный звонок', description: 'Знакомство и созвон', durationMinutes: 30 },
]

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('SDK из OpenAPI-контракта', () => {
  it('useEventTypesList получает типы событий с /api/event-types', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      status: 200,
      headers: {},
      text: async () => JSON.stringify(eventTypes),
    })
    vi.stubGlobal('fetch', fetchMock)

    const { result } = renderHook(() => useEventTypesList(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/event-types',
      expect.objectContaining({ method: 'GET' }),
    )
    expect(result.current.data?.data).toEqual(eventTypes)
    expect(result.current.data?.status).toBe(200)
  })

  it('useEventTypesCreate отправляет тело CreateEventTypeRequest', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      status: 201,
      headers: {},
      text: async () => JSON.stringify(eventTypes[0]),
    })
    vi.stubGlobal('fetch', fetchMock)

    const { result } = renderHook(() => useEventTypesCreate(), { wrapper: createWrapper() })

    result.current.mutate({ data: { name: 'Обсуждение проекта', description: 'Дизайн', durationMinutes: 60 } })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/event-types',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }),
    )
    expect(result.current.data?.data).toEqual(eventTypes[0])
  })
})

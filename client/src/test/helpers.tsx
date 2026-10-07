// Общие обёртки для тестов страниц: провайдеры Mantine/Query/Router
// и мок fetch по маршрутам (сгенерированный SDK ходит в глобальный fetch).

import { MantineProvider } from '@mantine/core'
import { DatesProvider } from '@mantine/dates'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'

interface RenderPageOptions {
  route?: string
  path?: string
}

export function renderPage(ui: ReactNode, { route = '/', path = '/' }: RenderPageOptions = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MantineProvider>
        <DatesProvider settings={{ locale: 'ru', firstDayOfWeek: 1 }}>
          <Notifications />
          <MemoryRouter initialEntries={[route]}>
            <Routes>
              <Route path={path} element={ui} />
            </Routes>
          </MemoryRouter>
        </DatesProvider>
      </MantineProvider>
    </QueryClientProvider>,
  )
}

export interface FetchRoute {
  url: string
  method?: string
  status: number
  body?: unknown
}

// Ответ имитирует контракт gen-SDK: он читает только status и text()
export function mockFetch(routes: FetchRoute[]) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input).split('?')[0]
    const method = init?.method ?? 'GET'
    const match = routes.find(route => (route.method ?? 'GET') === method && route.url === url)
    const status = match?.status ?? 500
    return {
      ok: status < 400,
      status,
      headers: {},
      text: async () => JSON.stringify(match?.body ?? {}),
    }
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

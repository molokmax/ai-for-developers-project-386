import { fireEvent, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../../test/helpers'
import EventTypesPage from './EventTypesPage'

const existingType = {
  id: 1,
  name: 'Вводный звонок',
  description: 'Знакомство',
  durationMinutes: 30,
}

describe('EventTypesPage (администрирование типов событий)', () => {
  it('показывает список типов событий', async () => {
    mockFetch([{ url: '/api/event-types', status: 200, body: [existingType] }])

    renderPage(<EventTypesPage />, { route: '/admin/event-types', path: '/admin/event-types' })

    expect(await screen.findByText('Вводный звонок')).toBeDefined()
    expect(screen.getByText('30 минут')).toBeDefined()
  })

  it('создаёт тип события и обновляет список', async () => {
    const fetchMock = mockFetch([
      { url: '/api/event-types', status: 200, body: [existingType] },
      {
        url: '/api/event-types',
        method: 'POST',
        status: 201,
        body: { id: 2, name: 'Обсуждение проекта', description: 'Дизайн', durationMinutes: 60 },
      },
    ])

    renderPage(<EventTypesPage />, { route: '/admin/event-types', path: '/admin/event-types' })

    fireEvent.change(await screen.findByLabelText(/Название/), { target: { value: 'Обсуждение проекта' } })
    fireEvent.change(screen.getByLabelText(/Описание/), { target: { value: 'Дизайн' } })
    fireEvent.click(screen.getByRole('button', { name: 'Создать' }))

    await waitFor(() => {
      const createCall = fetchMock.mock.calls.find(
        ([url, init]) => String(url) === '/api/event-types' && init?.method === 'POST',
      )
      expect(createCall).toBeDefined()
      expect(JSON.parse(String(createCall![1]!.body))).toEqual({
        name: 'Обсуждение проекта',
        description: 'Дизайн',
        durationMinutes: 30,
      })
    })

    await waitFor(() => {
      const listCalls = fetchMock.mock.calls.filter(
        ([url, init]) => String(url) === '/api/event-types' && (init?.method ?? 'GET') === 'GET',
      )
      expect(listCalls.length).toBeGreaterThanOrEqual(2)
    })
  })

  it('при 400 подсвечивает невалидные поля', async () => {
    mockFetch([
      { url: '/api/event-types', status: 200, body: [existingType] },
      {
        url: '/api/event-types',
        method: 'POST',
        status: 400,
        body: {
          status: 400,
          title: 'Некорректный запрос',
          errors: { durationMinutes: ['Длительность должна быть кратна 30 минутам'] },
        },
      },
    ])

    renderPage(<EventTypesPage />, { route: '/admin/event-types', path: '/admin/event-types' })

    fireEvent.change(await screen.findByLabelText(/Название/), { target: { value: 'Обсуждение проекта' } })
    fireEvent.click(screen.getByRole('button', { name: 'Создать' }))

    expect(await screen.findByText('Длительность должна быть кратна 30 минутам')).toBeDefined()
  })
})

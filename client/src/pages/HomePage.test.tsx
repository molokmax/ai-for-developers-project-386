import { cleanup, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockFetch, renderPage } from '../test/helpers'
import HomePage from './HomePage'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const eventType = {
  id: 1,
  name: 'Вводный звонок',
  description: 'Знакомство и обсуждение задачи',
  durationMinutes: 30,
}

describe('HomePage (каталог типов событий)', () => {
  it('показывает каталог типов событий из API', async () => {
    mockFetch([{ url: '/api/event-types', status: 200, body: [eventType] }])

    renderPage(<HomePage />)

    expect(await screen.findByText('Вводный звонок')).toBeDefined()
    expect(screen.getByText('Знакомство и обсуждение задачи')).toBeDefined()
    expect(screen.getByText('30 минут')).toBeDefined()

    const link = screen.getByRole('link', { name: 'Записаться' })
    expect(link.getAttribute('href')).toBe('/book/1')
  })

  it('пустой каталог показывает сообщение', async () => {
    mockFetch([{ url: '/api/event-types', status: 200, body: [] }])

    renderPage(<HomePage />)

    expect(await screen.findByText('Нет доступных типов событий')).toBeDefined()
  })
})

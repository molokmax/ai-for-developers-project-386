import { screen } from '@testing-library/react'
import dayjs from 'dayjs'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../../test/helpers'
import MeetingsPage from './MeetingsPage'

const start = dayjs().add(2, 'day').hour(11).minute(0).second(0).millisecond(0).toISOString()

const meetings = [
  {
    id: 1,
    eventType: { id: 1, name: 'Вводный звонок' },
    startUtc: start,
    endUtc: dayjs(start).add(30, 'minute').toISOString(),
    customerName: 'Иван',
    customerEmail: 'ivan@example.com',
  },
  {
    id: 2,
    eventType: { id: 2, name: 'Ревью кода' },
    startUtc: dayjs(start).add(1, 'hour').toISOString(),
    endUtc: dayjs(start).add(2, 'hour').toISOString(),
    customerName: 'Мария',
    customerEmail: 'maria@example.com',
  },
]

describe('MeetingsPage (предстоящие встречи)', () => {
  it('показывает предстоящие встречи с гостями', async () => {
    mockFetch([{ url: '/api/meetings', status: 200, body: meetings }])

    renderPage(<MeetingsPage />, { route: '/admin/meetings', path: '/admin/meetings' })

    expect(await screen.findByText('Иван')).toBeDefined()
    expect(screen.getByText('Мария')).toBeDefined()
    expect(screen.getByText('ivan@example.com')).toBeDefined()
    expect(screen.getByText('Вводный звонок')).toBeDefined()
    expect(screen.getByText('Ревью кода')).toBeDefined()
    expect(screen.getByText(`${dayjs(start).format('DD.MM.YYYY HH:mm')}–${dayjs(start).add(30, 'minute').format('HH:mm')}`)).toBeDefined()
  })

  it('пустой список показывает сообщение', async () => {
    mockFetch([{ url: '/api/meetings', status: 200, body: [] }])

    renderPage(<MeetingsPage />, { route: '/admin/meetings', path: '/admin/meetings' })

    expect(await screen.findByText('Предстоящих встреч нет')).toBeDefined()
  })
})

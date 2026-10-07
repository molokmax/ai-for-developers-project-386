import { fireEvent, screen, waitFor } from '@testing-library/react'
import dayjs from 'dayjs'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderPage } from '../test/helpers'
import BookingPage from './BookingPage'

const eventType = {
  id: 1,
  name: 'Вводный звонок',
  description: 'Знакомство и обсуждение задачи',
  durationMinutes: 30,
}

// Слот «завтра 09:00» по локальному времени теста: группировка страницы идёт по локальной дате
const slotStart = dayjs()
  .add(1, 'day')
  .hour(9)
  .minute(0)
  .second(0)
  .millisecond(0)
  .toISOString()
const slotEnd = dayjs(slotStart).add(30, 'minute').toISOString()

const slots = [{ startUtc: slotStart, endUtc: slotEnd }]

const confirmedBooking = {
  id: 10,
  eventType: { id: 1, name: 'Вводный звонок' },
  startUtc: slotStart,
  endUtc: slotEnd,
  customerName: 'Иван',
  customerEmail: 'ivan@example.com',
}

function fillForm() {
  fireEvent.click(screen.getByRole('button', { name: '09:00' }))
  // withAsterisk добавляет «*» к тексту label, поэтому частичное совпадение
  fireEvent.change(screen.getByLabelText(/Имя/), { target: { value: 'Иван' } })
  fireEvent.change(screen.getByLabelText(/Email/), { target: { value: 'ivan@example.com' } })
}

describe('BookingPage (запись на слот)', () => {
  it('создаёт запись с Idempotency-Key и показывает подтверждение', async () => {
    const fetchMock = mockFetch([
      { url: '/api/event-types', status: 200, body: [eventType] },
      { url: '/api/slots', status: 200, body: slots },
      { url: '/api/bookings', method: 'POST', status: 201, body: confirmedBooking },
    ])

    renderPage(<BookingPage />, { route: '/book/1', path: '/book/:eventTypeId' })

    expect(await screen.findByText('Вводный звонок')).toBeDefined()

    fillForm()
    fireEvent.click(screen.getByRole('button', { name: 'Записаться' }))

    await waitFor(() => expect(screen.getByText('Вы записаны')).toBeDefined())
    expect(screen.getByText('Вводный звонок')).toBeDefined()

    const bookingCall = fetchMock.mock.calls.find(
      ([url, init]) => String(url) === '/api/bookings' && init?.method === 'POST',
    )
    expect(bookingCall).toBeDefined()
    const [, init] = bookingCall!
    const headers = init!.headers as Record<string, string>
    expect(headers['Idempotency-Key']).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[0-9a-f]{4}-[0-9a-f]{12}$/i)
    expect(JSON.parse(String(init!.body))).toEqual({
      eventTypeId: 1,
      startUtc: slotStart,
      customerName: 'Иван',
      customerEmail: 'ivan@example.com',
    })
  })

  it('подсвечивает невалидные поля без отправки запроса', async () => {
    const fetchMock = mockFetch([
      { url: '/api/event-types', status: 200, body: [eventType] },
      { url: '/api/slots', status: 200, body: slots },
    ])

    renderPage(<BookingPage />, { route: '/book/1', path: '/book/:eventTypeId' })

    fireEvent.click(await screen.findByRole('button', { name: '09:00' }))
    fireEvent.click(screen.getByRole('button', { name: 'Записаться' }))

    expect(await screen.findByText('Укажите имя')).toBeDefined()
    expect(screen.getByText('Укажите корректный email')).toBeDefined()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(0)
  })

  it('серверные ошибки 400 подсвечивает по полям формы', async () => {
    mockFetch([
      { url: '/api/event-types', status: 200, body: [eventType] },
      { url: '/api/slots', status: 200, body: slots },
      {
        url: '/api/bookings',
        method: 'POST',
        status: 400,
        body: {
          status: 400,
          title: 'One or more validation errors occurred.',
          // Валидация отдаёт ключи как имена C#-свойств (PascalCase)
          errors: { CustomerEmail: ['The CustomerEmail field is not a valid e-mail address.'] },
        },
      },
    ])

    renderPage(<BookingPage />, { route: '/book/1', path: '/book/:eventTypeId' })

    await screen.findByText('Вводный звонок')
    fillForm()
    fireEvent.click(screen.getByRole('button', { name: 'Записаться' }))

    expect(await screen.findByText(/not a valid e-mail/i)).toBeDefined()
  })

  it('при 409 показывает уведомление и перезапрашивает слоты', async () => {
    const fetchMock = mockFetch([
      { url: '/api/event-types', status: 200, body: [eventType] },
      { url: '/api/slots', status: 200, body: slots },
      {
        url: '/api/bookings',
        method: 'POST',
        status: 409,
        body: { status: 409, title: 'Конфликт занятости', detail: 'Интервал записи пересекается с существующей записью' },
      },
    ])

    renderPage(<BookingPage />, { route: '/book/1', path: '/book/:eventTypeId' })

    await screen.findByText('Вводный звонок')
    fillForm()
    fireEvent.click(screen.getByRole('button', { name: 'Записаться' }))

    expect(await screen.findByText('Слот уже занят')).toBeDefined()

    const slotsCalls = fetchMock.mock.calls.filter(
      ([url, init]) => String(url).startsWith('/api/slots') && (init?.method ?? 'GET') === 'GET',
    )
    expect(slotsCalls.length).toBeGreaterThanOrEqual(2)
  })

  it('неизвестный тип события показывает сообщение 404', async () => {
    mockFetch([
      { url: '/api/event-types', status: 200, body: [eventType] },
      { url: '/api/slots', status: 404, body: { status: 404, title: 'Тип события не найден' } },
    ])

    renderPage(<BookingPage />, { route: '/book/999', path: '/book/:eventTypeId' })

    expect(await screen.findByText('Тип события не найден')).toBeDefined()
  })
})

export interface SlotDto {
  id: number
  startUtc: string
  /** Длительность в формате TimeSpan ("01:00:00") */
  duration: string
  isBooked: boolean
}

export interface CreateBookingRequest {
  slotId: number
  customerName: string
  customerEmail: string
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init)

  if (!response.ok) {
    throw new Error(`Ошибка ${response.status}: ${response.statusText}`)
  }

  return (await response.json()) as T
}

export function fetchSlots(from?: Date, to?: Date): Promise<SlotDto[]> {
  const params = new URLSearchParams()

  if (from) params.set('from', from.toISOString())
  if (to) params.set('to', to.toISOString())

  const query = params.size > 0 ? `?${params.toString()}` : ''
  return request<SlotDto[]>(`/api/slots${query}`)
}

export function createBooking(payload: CreateBookingRequest): Promise<number> {
  return request<number>('/api/bookings', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
}

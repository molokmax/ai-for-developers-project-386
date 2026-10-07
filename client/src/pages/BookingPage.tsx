import { Button, Card, Container, Group, Skeleton, Stack, Text, TextInput, Title } from '@mantine/core'
import { DatePicker } from '@mantine/dates'
import { notifications } from '@mantine/notifications'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { bookingsCreate, getSlotsListQueryKey, useEventTypesList, useSlotsList } from '../api/gen'
import type { Booking, ProblemDetails, Slot } from '../api/gen/model'

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

type FieldErrors = Partial<Record<'name' | 'email', string>>

// Ошибки валидации 400 приходят по именам полей контракта (RFC 9457 validation problem)
function problemFieldErrors(problem: ProblemDetails & { errors?: Record<string, string[]> }): FieldErrors {
  const errors: FieldErrors = {}
  for (const [field, messages] of Object.entries(problem.errors ?? {})) {
    const message = messages[0]
    if (!message) continue
    if (field === 'customerName') errors.name = message
    if (field === 'customerEmail') errors.email = message
  }
  return errors
}

// Idempotency-Key: UUID на попытку записи (спека); в jsdom randomUUID может отсутствовать
function newIdempotencyKey(): string {
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  const hex = '0123456789abcdef'
  const pick = (length: number) =>
    Array.from({ length }, () => hex[Math.floor(Math.random() * hex.length)]).join('')
  return `${pick(8)}-${pick(4)}-4${pick(3)}-${pick(4)}-${pick(12)}`
}

function formatRange(startUtc: string, endUtc: string): string {
  return `${dayjs(startUtc).format('DD.MM.YYYY')} с ${dayjs(startUtc).format('HH:mm')} до ${dayjs(endUtc).format('HH:mm')}`
}

export default function BookingPage() {
  const params = useParams<{ eventTypeId: string }>()
  const eventTypeId = Number(params.eventTypeId)
  const queryClient = useQueryClient()

  const typesQuery = useEventTypesList()
  const eventType = typesQuery.data?.data.find(item => item.id === eventTypeId)

  const slotsQuery = useSlotsList({ eventTypeId })
  const slots = slotsQuery.data?.status === 200 ? slotsQuery.data.data : undefined
  const isUnknownEventType = slotsQuery.data?.status === 404

  const [date, setDate] = useState<string | null>(null)
  const [selectedSlot, setSelectedSlot] = useState<Slot | null>(null)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [confirmed, setConfirmed] = useState<Booking | null>(null)

  const slotsByDate = useMemo(() => {
    const map = new Map<string, Slot[]>()
    for (const slot of slots ?? []) {
      // Слоты приходят в UTC; для отображения группируем по локальной дате гостя
      const key = dayjs(slot.startUtc).format('YYYY-MM-DD')
      const daySlots = map.get(key)
      if (daySlots) {
        daySlots.push(slot)
      } else {
        map.set(key, [slot])
      }
    }
    return map
  }, [slots])

  const datesWithSlots = useMemo(() => [...slotsByDate.keys()].sort(), [slotsByDate])
  const today = dayjs().format('YYYY-MM-DD')
  const windowEnd = dayjs().add(13, 'day').format('YYYY-MM-DD')
  const activeDate = date ?? datesWithSlots[0] ?? today
  const daySlots = slotsByDate.get(activeDate) ?? []

  const bookingMutation = useMutation({
    mutationFn: (startUtc: string) =>
      bookingsCreate(
        { eventTypeId, startUtc, customerName: name.trim(), customerEmail: email.trim() },
        { headers: { 'Idempotency-Key': newIdempotencyKey() } },
      ),
    onSuccess: async response => {
      if (response.status === 201) {
        setConfirmed(response.data)
        return
      }

      if (response.status === 409) {
        notifications.show({
          title: 'Слот уже занят',
          message: 'Кто-то успел раньше. Выберите другое время.',
          color: 'red',
        })
        setSelectedSlot(null)
        await queryClient.invalidateQueries({ queryKey: getSlotsListQueryKey({ eventTypeId }) })
        return
      }

      if (response.status === 400) {
        const errors = problemFieldErrors(response.data)
        setFieldErrors(errors)
        if (Object.keys(errors).length === 0) {
          notifications.show({
            title: response.data.title ?? 'Некорректный запрос',
            message: response.data.detail ?? 'Проверьте данные записи',
            color: 'red',
          })
        }
        return
      }

      notifications.show({
        title: response.data.title ?? 'Ошибка',
        message: response.data.detail ?? 'Не удалось создать запись',
        color: 'red',
      })
    },
  })

  const submit = () => {
    const errors: FieldErrors = {}
    if (!name.trim()) errors.name = 'Укажите имя'
    if (!EMAIL_PATTERN.test(email.trim())) errors.email = 'Укажите корректный email'
    setFieldErrors(errors)
    if (errors.name || errors.email || !selectedSlot) return
    bookingMutation.mutate(selectedSlot.startUtc)
  }

  if (confirmed) {
    return (
      <Container size="sm" py="xl">
        <Stack gap="md" ta="center">
          <Title order={1}>Вы записаны</Title>
          <Text size="lg" fw={600}>
            {confirmed.eventType.name}
          </Text>
          <Text c="dimmed">{formatRange(confirmed.startUtc, confirmed.endUtc)}</Text>
          <Button component={Link} to="/" mx="auto">
            На главную
          </Button>
        </Stack>
      </Container>
    )
  }

  return (
    <Container size="md" py="xl">
      <Stack gap="lg">
        <Stack gap="xs">
          <Title order={1}>{eventType?.name ?? 'Запись на звонок'}</Title>
          {eventType?.description && <Text c="dimmed">{eventType.description}</Text>}
        </Stack>

        {typesQuery.isLoading || slotsQuery.isLoading ? (
          <Skeleton height={360} radius="sm" />
        ) : isUnknownEventType ? (
          <Text>Тип события не найден</Text>
        ) : (
          <Stack gap="md">
            <Card withBorder padding="lg">
              <Stack gap="md">
                <Text fw={600}>Выберите день</Text>
                <DatePicker
                  value={activeDate}
                  onChange={value => {
                    if (!value) return
                    setDate(value)
                    setSelectedSlot(null)
                  }}
                  minDate={today}
                  maxDate={windowEnd}
                />
                <Text fw={600}>{dayjs(activeDate).format('DD.MM.YYYY')}</Text>
                {daySlots.length === 0 ? (
                  <Text c="dimmed">Свободных слотов нет</Text>
                ) : (
                  <Group gap="xs">
                    {daySlots.map(slot => (
                      <Button
                        key={slot.startUtc}
                        variant={selectedSlot?.startUtc === slot.startUtc ? 'filled' : 'light'}
                        onClick={() => {
                          setSelectedSlot(slot)
                          setFieldErrors({})
                        }}
                      >
                        {dayjs(slot.startUtc).format('HH:mm')}
                      </Button>
                    ))}
                  </Group>
                )}
              </Stack>
            </Card>

            {selectedSlot && (
              <Card withBorder padding="lg">
                <Stack gap="md" maw={400}>
                  <Text fw={600}>Выбранное время</Text>
                  <Text c="dimmed">{formatRange(selectedSlot.startUtc, selectedSlot.endUtc)}</Text>
                  <TextInput
                    label="Имя"
                    withAsterisk
                    value={name}
                    error={fieldErrors.name}
                    onChange={event => {
                      setName(event.currentTarget.value)
                      setFieldErrors(prev => ({ ...prev, name: undefined }))
                    }}
                  />
                  <TextInput
                    label="Email"
                    withAsterisk
                    value={email}
                    error={fieldErrors.email}
                    onChange={event => {
                      setEmail(event.currentTarget.value)
                      setFieldErrors(prev => ({ ...prev, email: undefined }))
                    }}
                  />
                  <Button loading={bookingMutation.isPending} onClick={submit}>
                    Записаться
                  </Button>
                </Stack>
              </Card>
            )}
          </Stack>
        )}
      </Stack>
    </Container>
  )
}
